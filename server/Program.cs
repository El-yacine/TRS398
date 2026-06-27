using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using MyQC.Data;
using MyQC.Services;
using MyQC.Models;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using System.Security.Cryptography;
using System.IO.Compression;

var builder = WebApplication.CreateBuilder(args);

// When launched by the Electron wrapper, TRS398_DATA_DIR points to the user's
// AppData folder so the database and uploads survive app updates.
// In development (dotnet run) it falls back to the current working directory.
var dataDir = Environment.GetEnvironmentVariable("TRS398_DATA_DIR")
              ?? Directory.GetCurrentDirectory();
Directory.CreateDirectory(dataDir);
var dbPath          = Path.Combine(dataDir, "trs398.db");
var emailConfigPath = Path.Combine(dataDir, "email_config.json");

// Configure JSON serialization to use camelCase
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

builder.Services.AddDbContext<MyQCDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

builder.Services.AddScoped<TRSService>();
builder.Services.AddScoped<PdfReportService>();

var app = builder.Build();
var env = app.Environment;

QuestPDF.Settings.License = LicenseType.Community;

// Global safety net: no unhandled exception should crash a response.
// Returns clean JSON 500 instead of a raw stack trace / dropped connection.
app.Use(async (ctx, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[unhandled] {ctx.Request.Method} {ctx.Request.Path}: {ex}");
        if (!ctx.Response.HasStarted)
        {
            ctx.Response.Clear();
            ctx.Response.StatusCode = 500;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsJsonAsync(new { error = "An unexpected server error occurred." });
        }
    }
});

// Content-Security-Policy header — required to suppress Electron security warnings
// when the app is loaded via loadURL() rather than loadFile().
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' https://unpkg.com https://cdn.jsdelivr.net; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
        "font-src 'self' https://fonts.gstatic.com https://unpkg.com data:; " +
        "img-src 'self' data: blob:; " +
        "connect-src 'self'; " +
        "object-src 'none';";
    await next();
});

// Open-source build: no license gate — the app is free to run after cloning.

app.MapGet("/", () => Results.Redirect("/index.html"));
app.UseStaticFiles(new StaticFileOptions {
    OnPrepareResponse = ctx =>
    {
        // Always revalidate HTML/CSS/JS so UI changes show on a normal refresh
        // (prevents the browser from showing a stale cached page).
        var h = ctx.Context.Response.Headers;
        h.CacheControl = "no-store, no-cache, must-revalidate";
        h.Pragma = "no-cache";
        h.Expires = "0";
    }
});

// Serve user-writable uploads from dataDir so they survive app updates
// even when wwwroot is inside a read-only install directory.
var logosStaticDir = Path.Combine(dataDir, "logos");
Directory.CreateDirectory(logosStaticDir);
app.UseStaticFiles(new StaticFileOptions {
    FileProvider = new PhysicalFileProvider(logosStaticDir),
    RequestPath  = "/logos"
});

var sigsStaticDir = Path.Combine(dataDir, "signatures");
Directory.CreateDirectory(sigsStaticDir);
app.UseStaticFiles(new StaticFileOptions {
    FileProvider = new PhysicalFileProvider(sigsStaticDir),
    RequestPath  = "/signatures"
});

// ============================================================================
// SETTINGS (centralized server-side store)
// ============================================================================
var settingsPath = Path.Combine(dataDir, "settings.json");

app.MapGet("/api/settings", () =>
{
    if (!File.Exists(settingsPath)) return Results.Ok(new { });
    try
    {
        var json = File.ReadAllText(settingsPath);
        var obj = JsonSerializer.Deserialize<JsonElement>(json);
        return Results.Ok(obj);
    }
    catch { return Results.Ok(new { }); }
});

app.MapPost("/api/settings", async (HttpContext ctx) =>
{
    using var reader = new StreamReader(ctx.Request.Body);
    var body = await reader.ReadToEndAsync();
    try { JsonDocument.Parse(body); } // validate JSON
    catch { return Results.BadRequest(new { error = "Invalid JSON" }); }
    await File.WriteAllTextAsync(settingsPath, body);
    return Results.Ok(new { saved = true });
});

// ============================================================================
// HEALTH & INFO
// ============================================================================
app.MapGet("/api/health", () => Results.Ok(new {
    status = "ok",
    app = "TRS-398 Pro",
    version = "2.1.0",
    time = DateTime.Now,
    features = new[] { "charts", "email", "themes", "excel", "signature", "i18n", "shortcuts", "backup", "mobile", "auth" }
}));

// ============================================================================
// LICENSE
// ============================================================================
app.MapGet("/api/license/status", () => Results.Ok(new { licensed = true, key = (string?)null }));

// ============================================================================
// TRS CALCULATIONS & MEASUREMENTS
// ============================================================================
app.MapPost("/api/trs/calculate", (TRSMeasurement input, TRSService service) =>
{
    if (input is null) return Results.BadRequest(new { error = "No measurement data provided." });
    try
    {
        return Results.Ok(service.Calculate(input));
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = "Could not calculate: " + ex.Message });
    }
});

app.MapPost("/api/trs/save", async (TRSMeasurement input, TRSService service, MyQCDbContext db) =>
{
    if (input is null) return Results.BadRequest(new { error = "No measurement data provided." });
    try
    {
        var result    = service.Calculate(input);
        var tolerance = GetTolerance(result.LinacId, result.Energy, dataDir);
        db.TRSMeasurements.Add(result);
        await db.SaveChangesAsync();
        if (Math.Abs(result.Ecart) > tolerance)
            _ = SendAlertAsync(result, tolerance); // fire-and-forget, never blocks the response
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        return Results.Problem("Could not save measurement: " + ex.Message);
    }
});

app.MapGet("/api/trs/measurements", async (MyQCDbContext db) =>
{
    var measurements = await db.TRSMeasurements
        .OrderByDescending(m => m.Date)
        .Take(100)
        .ToListAsync();
    return Results.Ok(measurements);
});

app.MapDelete("/api/trs/measurements/{id}", async (int id, MyQCDbContext db) =>
{
    var measurement = await db.TRSMeasurements.FindAsync(id);
    if (measurement == null) return Results.NotFound();
    db.TRSMeasurements.Remove(measurement);
    await db.SaveChangesAsync();
    return Results.Ok(new { deleted = id });
});

// ============================================================================
// STATISTICS & CHARTS DATA
// ============================================================================
app.MapGet("/api/stats/overview", async (MyQCDbContext db) =>
{
    var measurements = await db.TRSMeasurements.ToListAsync();
    var tolDoc = LoadToleranceDoc(dataDir);
    bool Passes(TRSMeasurement m) => Math.Abs(m.Ecart) <= GetToleranceFromDoc(tolDoc, m.LinacId, m.Energy);
    var now = DateTime.Now;
    var thisMonth = measurements.Where(m => m.Date.Month == now.Month && m.Date.Year == now.Year).ToList();
    var thisYear = measurements.Where(m => m.Date.Year == now.Year).ToList();

    return Results.Ok(new {
        total = measurements.Count,
        thisMonth = thisMonth.Count,
        thisYear = thisYear.Count,
        passRate = measurements.Count > 0
            ? Math.Round(measurements.Count(Passes) * 100.0 / measurements.Count, 1)
            : 0,
        avgDeviation = measurements.Count > 0 
            ? Math.Round(measurements.Average(m => Math.Abs(m.Ecart)), 2) 
            : 0,
        lastCalibration = measurements.OrderByDescending(m => m.Date).FirstOrDefault()?.Date,
        photonCount = measurements.Count(m => string.Equals(m.Mode, "photon", StringComparison.OrdinalIgnoreCase)),
        electronCount = measurements.Count(m => string.Equals(m.Mode, "electron", StringComparison.OrdinalIgnoreCase))
    });
});

app.MapGet("/api/stats/trends", async (MyQCDbContext db, int? months) =>
{
    var monthsBack = months ?? 12;
    var startDate = DateTime.Now.AddMonths(-monthsBack);
    
    var measurements = await db.TRSMeasurements
        .Where(m => m.Date >= startDate)
        .OrderBy(m => m.Date)
        .ToListAsync();
    
    // Group by month
    var monthlyData = measurements
        .GroupBy(m => new { m.Date.Year, m.Date.Month })
        .Select(g => new {
            month = $"{g.Key.Year}-{g.Key.Month:D2}",
            count = g.Count(),
            avgDeviation = Math.Round(g.Average(m => Math.Abs(m.Ecart)), 2),
            passRate = Math.Round(g.Count(m => Math.Abs(m.Ecart) <= GetToleranceFromDoc(LoadToleranceDoc(dataDir), m.LinacId, m.Energy)) * 100.0 / g.Count(), 1),
            avgDose = Math.Round(g.Average(m => m.DW_Zref), 3)
        })
        .ToList();
    
    // Group by energy
    var energyData = measurements
        .GroupBy(m => m.Energy)
        .Select(g => new {
            energy = g.Key,
            count = g.Count(),
            avgDeviation = Math.Round(g.Average(m => Math.Abs(m.Ecart)), 2)
        })
        .OrderBy(e => e.energy)
        .ToList();
    
    // Group by machine
    var machineData = measurements
        .GroupBy(m => m.LinacName ?? "Unknown")
        .Select(g => new {
            machine = g.Key,
            count = g.Count(),
            avgDeviation = Math.Round(g.Average(m => Math.Abs(m.Ecart)), 2)
        })
        .OrderByDescending(m => m.count)
        .ToList();
    
    return Results.Ok(new {
        monthly = monthlyData,
        byEnergy = energyData,
        byMachine = machineData,
        deviationHistory = measurements.Select(m => new {
            date = m.Date.ToString("yyyy-MM-dd"),
            deviation = m.Ecart,
            energy = m.Energy,
            machine = m.LinacName
        }).ToList()
    });
});

// ============================================================================
// EXPORT (CSV & EXCEL)
// ============================================================================
app.MapGet("/api/trs/export", async (MyQCDbContext db, string? format) =>
{
    var measurements = await db.TRSMeasurements.OrderByDescending(m => m.Date).ToListAsync();
    
    if (format?.ToLower() == "excel")
    {
        // Excel XML format (can be opened in Excel)
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<?mso-application progid=\"Excel.Sheet\"?>");
        sb.AppendLine("<Workbook xmlns=\"urn:schemas-microsoft-com:office:spreadsheet\"");
        sb.AppendLine(" xmlns:ss=\"urn:schemas-microsoft-com:office:spreadsheet\">");
        sb.AppendLine("<Worksheet ss:Name=\"TRS-398 Measurements\">");
        sb.AppendLine("<Table>");
        
        // Header row
        sb.AppendLine("<Row>");
        var headers = new[] { "Date", "User", "Mode", "Clinic", "Energy", "LINAC", "Chamber", "kQ", "Ndw", "Dose (Gy)", "Deviation (%)", "Status" };
        foreach (var h in headers)
            sb.AppendLine($"<Cell><Data ss:Type=\"String\">{h}</Data></Cell>");
        sb.AppendLine("</Row>");
        
        // Data rows
        foreach (var m in measurements)
        {
            sb.AppendLine("<Row>");
            sb.AppendLine($"<Cell><Data ss:Type=\"String\">{m.Date:yyyy-MM-dd HH:mm}</Data></Cell>");
            sb.AppendLine($"<Cell><Data ss:Type=\"String\">{m.UserName}</Data></Cell>");
            sb.AppendLine($"<Cell><Data ss:Type=\"String\">{m.Mode}</Data></Cell>");
            sb.AppendLine($"<Cell><Data ss:Type=\"String\">{m.ClinicName}</Data></Cell>");
            sb.AppendLine($"<Cell><Data ss:Type=\"Number\">{m.Energy}</Data></Cell>");
            sb.AppendLine($"<Cell><Data ss:Type=\"String\">{m.LinacName}</Data></Cell>");
            sb.AppendLine($"<Cell><Data ss:Type=\"String\">{m.Chamber}</Data></Cell>");
            sb.AppendLine($"<Cell><Data ss:Type=\"Number\">{m.kQUsed}</Data></Cell>");
            sb.AppendLine($"<Cell><Data ss:Type=\"Number\">{m.Ndw}</Data></Cell>");
            sb.AppendLine($"<Cell><Data ss:Type=\"Number\">{m.DW_Zref}</Data></Cell>");
            sb.AppendLine($"<Cell><Data ss:Type=\"Number\">{m.Ecart}</Data></Cell>");
            sb.AppendLine($"<Cell><Data ss:Type=\"String\">{(Math.Abs(m.Ecart) <= GetTolerance(m.LinacId, m.Energy, dataDir) ? "PASS" : "FAIL")}</Data></Cell>");
            sb.AppendLine("</Row>");
        }
        
        sb.AppendLine("</Table>");
        sb.AppendLine("</Worksheet>");
        sb.AppendLine("</Workbook>");
        
        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return Results.File(bytes, "application/vnd.ms-excel", "trs398_export.xls");
    }
    else
    {
        // CSV format
        var sb = new StringBuilder();
        sb.AppendLine("Date,User,Mode,Clinic,Energy,LINAC Name,LINAC ID,Field Size,Chamber,ChamberType,TPR20/10,R50,Zref,SSD,T,P,kQUsed,NdUsed,Ndw,M+1,M+2,M+3,Mean+,M-1,M-2,M-3,Mean-,M100V1,M100V2,M100V3,Mean100V,Ktp,Kpol,Ks,M_corr,DW_Zref,Ecart,Notes");
        foreach (var m in measurements)
        {
            sb.AppendLine($"{m.Date:yyyy-MM-dd HH:mm},{m.UserName},{m.Mode},{m.ClinicName},{m.Energy},{m.LinacName},{m.LinacId},{m.FieldSize},{m.Chamber},{m.ChamberType},{m.TPR2010},{m.R50},{m.Zref},{m.SSD},{m.T},{m.P},{m.kQUsed},{m.NdUsed},{m.Ndw},{m.M_plus_1},{m.M_plus_2},{m.M_plus_3},{m.Mean_plus},{m.M_minus_1},{m.M_minus_2},{m.M_minus_3},{m.Mean_minus},{m.M100V_1},{m.M100V_2},{m.M100V_3},{m.Mean100V},{m.Ktp},{m.Kpol},{m.Ks},{m.M_corr},{m.DW_Zref},{m.Ecart},\"{m.Notes}\"");
        }
        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return Results.File(bytes, "text/csv", "trs398_export.csv");
    }
});

// ============================================================================
// PDF REPORT WITH SIGNATURE
// ============================================================================
app.MapGet("/api/trs/report/all", async (MyQCDbContext db, PdfReportService pdfService) =>
{
    var measurements = await db.TRSMeasurements
        .OrderByDescending(m => m.Date)
        .ToListAsync();

    if (measurements.Count == 0)
        return Results.BadRequest(new { error = "No measurements found" });

    var bytes = pdfService.BuildSummaryReport(measurements);
    var filename = $"trs398_summary_report_{DateTime.Now:yyyyMMdd}.pdf";
    return Results.File(bytes, "application/pdf", filename);
});

// ZIP of all individual reports — each file named with date + linac + energy
app.MapGet("/api/trs/report/zip", async (MyQCDbContext db, PdfReportService pdfService) =>
{
    var measurements = await db.TRSMeasurements
        .OrderBy(m => m.Date)
        .ToListAsync();

    if (measurements.Count == 0)
        return Results.BadRequest(new { error = "No measurements found" });

    using var ms = new MemoryStream();
    using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
    {
        // Track filenames to avoid duplicates
        var usedNames = new Dictionary<string, int>();
        foreach (var m in measurements)
        {
            var linac = string.IsNullOrWhiteSpace(m.LinacName) ? "LINAC" : m.LinacName.Replace(" ", "_");
            var dateStr = m.Date.ToString("yyyy-MM-dd");
            var energy  = (m.Energy ?? "").Replace("/", "-");
            var baseName = $"{dateStr}_{linac}_{energy}";
            // deduplicate
            if (!usedNames.TryAdd(baseName, 1))
            {
                usedNames[baseName]++;
                baseName = $"{baseName}_{usedNames[baseName]}";
            }
            var entryName = $"{baseName}.pdf";

            var pdfBytes = pdfService.Build(m, null);
            var entry = zip.CreateEntry(entryName, CompressionLevel.Fastest);
            using var es = entry.Open();
            await es.WriteAsync(pdfBytes);
        }
    }

    ms.Position = 0;
    var zipBytes = ms.ToArray();
    var zipName  = $"TRS398_Reports_{DateTime.Now:yyyy-MM-dd}.zip";
    return Results.File(zipBytes, "application/zip", zipName);
});

app.MapGet("/api/trs/report/{id}", async (int id, MyQCDbContext db, PdfReportService pdfService, string? signature) =>
{
    var measurement = await db.TRSMeasurements.FindAsync(id);
    if (measurement is null) return Results.NotFound();

    var bytes = pdfService.Build(measurement, signature);
    var filename = $"trs398_report_{id}.pdf";
    return Results.File(bytes, "application/pdf", filename);
});

// ============================================================================
// DETECTORS
// ============================================================================
app.MapGet("/api/detectors", async () =>
{
    // Search order: next to exe (packaged), one level up (dev), beside resources
    var candidates = new[]
    {
        Path.Combine(env.ContentRootPath, "detector_library.json"),
        Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", "detector_library.json")),
        Path.Combine(AppContext.BaseDirectory, "detector_library.json"),
    };
    var path = candidates.FirstOrDefault(File.Exists);
    if (path is null) return Results.NotFound();
    var json = await File.ReadAllTextAsync(path);
    return Results.Content(json, "application/json");
});

// ============================================================================
// LOGO MANAGEMENT
// ============================================================================
app.MapPost("/api/logo/upload", async (HttpRequest request) =>
{
    var logosDir = Path.Combine(dataDir, "logos");
    if (!Directory.Exists(logosDir))
        Directory.CreateDirectory(logosDir);

    if (!request.HasFormContentType)
        return Results.BadRequest(new { error = "Expected form data" });

    var form = await request.ReadFormAsync();
    var file = form.Files.GetFile("logo");
    var clinicName = form["clinicName"].ToString();

    if (file == null || file.Length == 0)
        return Results.BadRequest(new { error = "No file uploaded" });

    var allowedTypes = new[] { "image/png", "image/jpeg", "image/jpg", "image/gif", "image/webp" };
    if (!allowedTypes.Contains(file.ContentType.ToLower()))
        return Results.BadRequest(new { error = "Invalid file type. Only PNG, JPEG, GIF, and WebP are allowed." });

    if (file.Length > 5 * 1024 * 1024)
        return Results.BadRequest(new { error = "File too large. Maximum size is 5MB." });

    var extension = Path.GetExtension(file.FileName).ToLower();
    if (string.IsNullOrEmpty(extension)) extension = ".png";
    
    string filename;
    if (!string.IsNullOrWhiteSpace(clinicName))
    {
        var safeName = string.Join("_", clinicName.Split(Path.GetInvalidFileNameChars()));
        filename = $"{safeName}{extension}";
    }
    else
    {
        filename = $"logo{extension}";
    }

    var filePath = Path.Combine(logosDir, filename);

    using (var stream = new FileStream(filePath, FileMode.Create))
    {
        await file.CopyToAsync(stream);
    }

    return Results.Ok(new { 
        success = true, 
        filename = filename,
        path = $"/logos/{filename}",
        message = "Logo uploaded successfully"
    });
});

app.MapGet("/api/logos", () =>
{
    var logosDir = Path.Combine(dataDir, "logos");
    if (!Directory.Exists(logosDir))
        return Results.Ok(new { logos = Array.Empty<object>() });

    var logos = Directory.GetFiles(logosDir)
        .Where(f => new[] { ".png", ".jpg", ".jpeg", ".gif", ".webp" }.Contains(Path.GetExtension(f).ToLower()))
        .Select(f => new {
            filename = Path.GetFileName(f),
            path = $"/logos/{Path.GetFileName(f)}",
            size = new FileInfo(f).Length,
            modified = new FileInfo(f).LastWriteTime
        })
        .ToList();

    return Results.Ok(new { logos });
});

app.MapDelete("/api/logo/{filename}", (string filename) =>
{
    var logosDir = Path.GetFullPath(Path.Combine(dataDir, "logos"));
    var filePath = Path.GetFullPath(Path.Combine(logosDir, filename));

    if (!filePath.StartsWith(logosDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        return Results.BadRequest(new { error = "Invalid filename" });

    if (!File.Exists(filePath))
        return Results.NotFound(new { error = "Logo not found" });

    File.Delete(filePath);
    return Results.Ok(new { success = true, message = "Logo deleted" });
});

// ============================================================================
// DATABASE BACKUP & RESTORE
// ============================================================================
app.MapGet("/api/backup", async (MyQCDbContext db) =>
{
    var backupDbPath = Path.Combine(dataDir, "trs398.db");
    if (!File.Exists(backupDbPath))
        return Results.NotFound(new { error = "Database not found" });

    var bytes = await File.ReadAllBytesAsync(backupDbPath);
    var filename = $"trs398_backup_{DateTime.Now:yyyyMMdd_HHmmss}.db";
    return Results.File(bytes, "application/octet-stream", filename);
});

app.MapPost("/api/backup/restore", async (HttpRequest request, MyQCDbContext db) =>
{
    if (!request.HasFormContentType)
        return Results.BadRequest(new { error = "Expected form data" });

    var form = await request.ReadFormAsync();
    var file = form.Files.GetFile("backup");

    if (file == null || file.Length == 0)
        return Results.BadRequest(new { error = "No backup file uploaded" });

    // Save uploaded .db to a temp file
    var tmpPath = Path.Combine(dataDir, $"trs398_upload_{Guid.NewGuid():N}.db");
    try
    {
        using (var fs = File.Create(tmpPath))
            await file.CopyToAsync(fs);

        // Backup current db before overwriting
        var dbPath     = Path.Combine(dataDir, "trs398.db");
        var backupPath = Path.Combine(dataDir, $"trs398_pre_restore_{DateTime.Now:yyyyMMdd_HHmmss}.db");
        if (File.Exists(dbPath)) File.Copy(dbPath, backupPath);

        // Use SQLite online backup API to copy all pages from uploaded db
        // into the live connection — no restart needed
        var liveConn = (Microsoft.Data.Sqlite.SqliteConnection)db.Database.GetDbConnection();
        bool wasOpen = liveConn.State == System.Data.ConnectionState.Open;
        if (!wasOpen) liveConn.Open();

        using var srcConn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={tmpPath}");
        srcConn.Open();
        srcConn.BackupDatabase(liveConn);   // copies src → live
        srcConn.Close();

        if (!wasOpen) liveConn.Close();

        // Refresh EF's identity cache
        foreach (var entry in db.ChangeTracker.Entries().ToList())
            entry.State = Microsoft.EntityFrameworkCore.EntityState.Detached;

        var count = await db.TRSMeasurements.CountAsync();
        return Results.Ok(new {
            success = true,
            records = count,
            message  = "Database restored successfully",
            previousBackup = Path.GetFileName(backupPath)
        });
    }
    finally
    {
        if (File.Exists(tmpPath)) File.Delete(tmpPath);
    }
});

app.MapGet("/api/backup/list", () =>
{
    var backups = Directory.GetFiles(dataDir, "*.db")
        .Where(f => Path.GetFileName(f).Contains("backup") || Path.GetFileName(f).Contains("pre_restore"))
        .Select(f => new {
            filename = Path.GetFileName(f),
            size = new FileInfo(f).Length,
            created = new FileInfo(f).CreationTime
        })
        .OrderByDescending(b => b.created)
        .ToList();

    return Results.Ok(new { backups });
});

// ============================================================================
// TRANSFER BUNDLE — export all data + settings as a single .zip
// ============================================================================
app.MapGet("/api/transfer/export", async (HttpContext ctx) =>
{
    var settingFiles = new[] {
        "email_config.json", "backup_config.json", "tolerances.json",
        "detector_library_custom.json", "license.json", "settings.json"
    };

    using var ms = new MemoryStream();
    using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
    {
        // Database
        var dbFile = Path.Combine(dataDir, "trs398.db");
        if (File.Exists(dbFile))
        {
            var entry = zip.CreateEntry("trs398.db", CompressionLevel.Optimal);
            using var dbStream = File.OpenRead(dbFile);
            using var entryStream = entry.Open();
            await dbStream.CopyToAsync(entryStream);
        }

        // Settings files
        foreach (var name in settingFiles)
        {
            var path = Path.Combine(dataDir, name);
            if (!File.Exists(path)) continue;
            var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
            using var src = File.OpenRead(path);
            using var dst = entry.Open();
            await src.CopyToAsync(dst);
        }

        // Manifest
        var manifest = zip.CreateEntry("manifest.json");
        using var mw = new StreamWriter(manifest.Open());
        await mw.WriteAsync(JsonSerializer.Serialize(new {
            exportedAt = DateTime.Now.ToString("o"),
            app = "TRS-398 Pro",
            version = "2.1.0"
        }));
    }

    ms.Seek(0, SeekOrigin.Begin);
    var filename = $"trs398_transfer_{DateTime.Now:yyyyMMdd_HHmmss}.zip";
    ctx.Response.Headers.Append("Content-Disposition", $"attachment; filename=\"{filename}\"");
    ctx.Response.ContentType = "application/zip";
    await ms.CopyToAsync(ctx.Response.Body);
});

app.MapPost("/api/transfer/import", async (HttpRequest request, MyQCDbContext db) =>
{
    if (!request.HasFormContentType || request.Form.Files.Count == 0)
        return Results.BadRequest(new { error = "No file uploaded" });

    var file = request.Form.Files[0];
    if (!file.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        return Results.BadRequest(new { error = "Must be a .zip bundle file" });

    using var stream = file.OpenReadStream();
    using var zip = new ZipArchive(stream, ZipArchiveMode.Read);

    var restored = new List<string>();

    foreach (var entry in zip.Entries)
    {
        if (entry.Name == "manifest.json") continue;

        var destPath = Path.Combine(dataDir, entry.Name);

        if (entry.Name == "trs398.db")
        {
            // Pre-backup current db before overwriting
            var current = Path.Combine(dataDir, "trs398.db");
            if (File.Exists(current))
                File.Copy(current, Path.Combine(dataDir, $"trs398_pre_import_{DateTime.Now:yyyyMMdd_HHmmss}.db"), overwrite: true);
        }

        using var src = entry.Open();
        using var dst = File.Create(destPath);
        await src.CopyToAsync(dst);
        restored.Add(entry.Name);
    }

    return Results.Ok(new {
        success = true,
        restored,
        message = "Bundle imported. Restart the app to reload the database."
    });
});

// ============================================================================
// CONTROL CHART — Shewhart chart data per beam/linac
// ============================================================================
app.MapGet("/api/control-chart", async (MyQCDbContext db, string? energy, string? linac, int months = 24) =>
{
    var cutoff = DateTime.Now.AddMonths(-months);
    var q = db.TRSMeasurements.Where(m => m.Date >= cutoff);
    if (!string.IsNullOrWhiteSpace(energy)) q = q.Where(m => m.Energy == energy);
    if (!string.IsNullOrWhiteSpace(linac))
        q = q.Where(m => m.LinacId == linac || m.LinacName == linac);

    var data = await q.OrderBy(m => m.Date).ToListAsync();
    if (!data.Any()) return Results.Ok(new { points = Array.Empty<object>(), stats = (object?)null, violations = Array.Empty<object>() });

    var vals = data.Select(m => m.Ecart).ToList();
    var mean = vals.Average();
    var sd   = vals.Count > 1 ? Math.Sqrt(vals.Sum(v => Math.Pow(v - mean, 2)) / (vals.Count - 1)) : 0.0;
    var ucl  = mean + 2 * sd;
    var lcl  = mean - 2 * sd;
    var wHi  = mean + sd;
    var wLo  = mean - sd;

    string Zone(double v) =>
        Math.Abs(v - mean) > 2 * sd ? "action" :
        Math.Abs(v - mean) > sd     ? "warning" : "normal";

    var points = data.Select((m, i) => new
    {
        index     = i + 1,
        date      = m.Date.ToString("yyyy-MM-dd"),
        time      = m.Date.ToString("HH:mm"),
        dw        = Math.Round(m.DW_Zref, 4),
        deviation = Math.Round(m.Ecart, 4),
        energy    = m.Energy,
        userName  = m.UserName,
        zone      = Zone(m.Ecart)
    }).ToArray();

    // Nelson rule violations
    var viols = new List<object>();
    // Rule 1: point outside ±2σ
    foreach (var (p, i) in points.Select((p, i) => (p, i)))
        if (p.zone == "action")
            viols.Add(new { rule = 1, point = i + 1, date = p.date,
                desc = $"Point #{i+1} outside ±2σ control limits (dev={p.deviation:+0.00;-0.00}%)" });
    // Rule 2: 9 consecutive points same side of mean
    for (int i = 8; i < points.Length; i++)
    {
        var seg = points.Skip(i - 8).Take(9).ToArray();
        if (seg.All(p => p.deviation >= mean) || seg.All(p => p.deviation <= mean))
            viols.Add(new { rule = 2, point = i + 1, date = points[i].date,
                desc = $"9 consecutive points on same side of mean (ending point #{i+1})" });
    }
    // Rule 3: 6 consecutive points monotonically increasing or decreasing
    for (int i = 5; i < points.Length; i++)
    {
        var seg = points.Skip(i - 5).Take(6).Select(p => p.deviation).ToArray();
        bool up   = seg.Zip(seg.Skip(1), (a, b) => b > a).All(x => x);
        bool down = seg.Zip(seg.Skip(1), (a, b) => b < a).All(x => x);
        if (up || down)
            viols.Add(new { rule = 3, point = i + 1, date = points[i].date,
                desc = $"6 consecutive points trending {(up ? "up" : "down")} (ending point #{i+1})" });
    }

    // De-duplicate: keep only first occurrence per rule+point group
    var seen = new HashSet<string>();
    var uniqueViols = viols.Where(v =>
    {
        var key = $"{((dynamic)v).rule}-{((dynamic)v).point}";
        return seen.Add(key);
    }).ToArray();

    return Results.Ok(new
    {
        points,
        stats = new
        {
            n            = data.Count,
            mean         = Math.Round(mean, 4),
            sd           = Math.Round(sd, 4),
            ucl          = Math.Round(ucl, 4),
            lcl          = Math.Round(lcl, 4),
            warnHi       = Math.Round(wHi, 4),
            warnLo       = Math.Round(wLo, 4),
            inControl    = points.Count(p => p.zone == "normal"),
            inWarning    = points.Count(p => p.zone == "warning"),
            outOfControl = points.Count(p => p.zone == "action"),
            energies     = data.Select(m => m.Energy).Distinct().OrderBy(e => e).ToArray()
        },
        violations = uniqueViols
    });
});

// ============================================================================
// MULTI-LINAC — aggregated per-machine stats
// ============================================================================
// machine registry: machines added manually (may have no measurements yet)
var machinesPath = Path.Combine(dataDir, "machines.json");

List<MachineReg> LoadMachines()
{
    try
    {
        if (File.Exists(machinesPath))
            return System.Text.Json.JsonSerializer.Deserialize<List<MachineReg>>(File.ReadAllText(machinesPath)) ?? new();
    }
    catch { /* corrupt/missing → empty registry */ }
    return new();
}
void SaveMachines(List<MachineReg> list)
{
    try { File.WriteAllText(machinesPath, System.Text.Json.JsonSerializer.Serialize(list, new System.Text.Json.JsonSerializerOptions { WriteIndented = true })); }
    catch { /* best effort */ }
}

app.MapGet("/api/linacs", async (MyQCDbContext db) =>
{
    var all = await db.TRSMeasurements.ToListAsync();

    var derived = all
        .GroupBy(m => !string.IsNullOrWhiteSpace(m.LinacId) ? m.LinacId
                    : !string.IsNullOrWhiteSpace(m.LinacName) ? m.LinacName
                    : "Unknown")
        .Select(g =>
        {
            var sorted   = g.OrderBy(m => m.Date).ToList();
            var last     = sorted.Last();
            var recent   = sorted.Where(m => m.Date >= DateTime.Now.AddMonths(-3)).ToList();
            var tolDoc   = LoadToleranceDoc(dataDir);
            var allPass  = g.Count(m => Math.Abs(m.Ecart) <= GetToleranceFromDoc(tolDoc, m.LinacId, m.Energy));
            var recPass  = recent.Count(m => Math.Abs(m.Ecart) <= GetToleranceFromDoc(tolDoc, m.LinacId, m.Energy));
            var beams    = g.Select(m => m.Energy).Distinct().OrderBy(e => e).ToArray();
            var nextDue  = (int)(last.Date.AddDays(30) - DateTime.Now).TotalDays;
            var lastTol  = GetToleranceFromDoc(tolDoc, last.LinacId, last.Energy);

            string status = nextDue < 0            ? "overdue"
                          : nextDue <= 3           ? "urgent"
                          : nextDue <= 7           ? "warning"
                          : Math.Abs(last.Ecart) > lastTol ? "fail"
                          : "ok";

            return new
            {
                id             = g.Key,
                name           = !string.IsNullOrWhiteSpace(last.LinacName) ? last.LinacName : g.Key,
                serialId       = last.LinacId ?? "",
                beams,
                totalQA        = g.Count(),
                passRate       = Math.Round(100.0 * allPass / g.Count(), 1),
                recentPassRate = recent.Any() ? Math.Round(100.0 * recPass / recent.Count, 1) : (double?)null,
                lastDate       = last.Date.ToString("yyyy-MM-dd"),
                lastDeviation  = Math.Round(last.Ecart, 3),
                lastPassed     = Math.Abs(last.Ecart) <= lastTol,
                tolerance      = lastTol,
                nextDue        = last.Date.AddDays(30).ToString("yyyy-MM-dd"),
                daysUntilDue   = nextDue,
                status,
                firstDate      = sorted.First().Date.ToString("yyyy-MM-dd"),
                months         = (int)Math.Ceiling((last.Date - sorted.First().Date).TotalDays / 30.0),
                hasData        = true
            };
        })
        .OrderBy(l => l.name)
        .ToList();

    var result = new List<object>();
    result.AddRange(derived);

    // merge in registered machines that don't yet have measurements
    var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var d in derived) { keys.Add(d.id); keys.Add(d.name); }
    foreach (var mr in LoadMachines())
    {
        var key = !string.IsNullOrWhiteSpace(mr.SerialId) ? mr.SerialId! : mr.Name;
        if (keys.Contains(key) || keys.Contains(mr.Name)) continue;
        result.Add(new
        {
            id             = key,
            name           = mr.Name,
            serialId       = mr.SerialId ?? "",
            beams          = Array.Empty<string>(),
            totalQA        = 0,
            passRate       = (double?)null,
            recentPassRate = (double?)null,
            lastDate       = (string?)null,
            lastDeviation  = 0.0,
            lastPassed     = true,
            tolerance      = 2.0,
            nextDue        = (string?)null,
            daysUntilDue   = (int?)null,
            status         = "new",
            firstDate      = (string?)null,
            months         = 0,
            hasData        = false
        });
    }

    return Results.Ok(result);
});

// add a machine to the registry
app.MapPost("/api/linacs", (MachineReg body) =>
{
    if (body is null || string.IsNullOrWhiteSpace(body.Name))
        return Results.BadRequest(new { error = "Machine name is required." });

    var name = body.Name.Trim();
    var serial = string.IsNullOrWhiteSpace(body.SerialId) ? null : body.SerialId!.Trim();
    var list = LoadMachines();

    if (list.Any(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)
                   || (serial != null && string.Equals(x.SerialId, serial, StringComparison.OrdinalIgnoreCase))))
        return Results.Conflict(new { error = "A machine with this name or serial already exists." });

    list.Add(new MachineReg(name, serial));
    SaveMachines(list);
    return Results.Ok(new { success = true, name });
});

// remove a machine — only if no measurements reference it
app.MapDelete("/api/linacs/{id}", async (string id, MyQCDbContext db) =>
{
    id = Uri.UnescapeDataString(id ?? "");
    if (string.IsNullOrWhiteSpace(id))
        return Results.BadRequest(new { error = "Machine id is required." });

    var linked = await db.TRSMeasurements.CountAsync(m => m.LinacId == id || m.LinacName == id);
    if (linked > 0)
        return Results.Conflict(new {
            error = $"Cannot remove this machine — {linked} measurement(s) are linked to it. Delete those measurements first.",
            measurements = linked
        });

    var list = LoadMachines();
    var removed = list.RemoveAll(x => string.Equals(x.Name, id, StringComparison.OrdinalIgnoreCase)
                                   || (x.SerialId != null && string.Equals(x.SerialId, id, StringComparison.OrdinalIgnoreCase)));
    SaveMachines(list);

    if (removed == 0)
        return Results.NotFound(new { error = "Machine not found in registry." });
    return Results.Ok(new { success = true });
});

// ============================================================================
// OPERATORS  (names that perform measurements — no passwords, no login)
// ============================================================================
var operatorsPath = Path.Combine(dataDir, "operators.json");

List<string> LoadOperators()
{
    try
    {
        if (File.Exists(operatorsPath))
            return System.Text.Json.JsonSerializer.Deserialize<List<string>>(File.ReadAllText(operatorsPath)) ?? new();
    }
    catch { /* corrupt/missing → empty */ }
    return new();
}
void SaveOperators(List<string> list) =>
    File.WriteAllText(operatorsPath, System.Text.Json.JsonSerializer.Serialize(list, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

app.MapGet("/api/operators", () => Results.Ok(LoadOperators()));

app.MapPost("/api/operators", (NameDto body) =>
{
    var name = body?.Name?.Trim();
    if (string.IsNullOrWhiteSpace(name))
        return Results.BadRequest(new { error = "Operator name is required." });
    var list = LoadOperators();
    if (list.Any(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase)))
        return Results.Conflict(new { error = "That operator already exists." });
    list.Add(name);
    SaveOperators(list);
    return Results.Ok(new { success = true, name });
});

app.MapDelete("/api/operators/{name}", (string name) =>
{
    name = Uri.UnescapeDataString(name ?? "");
    var list = LoadOperators();
    var removed = list.RemoveAll(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase));
    SaveOperators(list);
    return removed == 0
        ? Results.NotFound(new { error = "Operator not found." })
        : Results.Ok(new { success = true });
});

// ============================================================================
// TOLERANCES
// ============================================================================
app.MapGet("/api/tolerances", () =>
{
    var path = Path.Combine(dataDir, "tolerances.json");
    if (!File.Exists(path))
        return Results.Ok(new { @default = 2.0, machines = new { } });
    try { return Results.Content(File.ReadAllText(path), "application/json"); }
    catch { return Results.Ok(new { @default = 2.0, machines = new { } }); }
});

app.MapPost("/api/tolerances", async (HttpContext ctx) =>
{
    var body = await new StreamReader(ctx.Request.Body).ReadToEndAsync();
    // Validate it's valid JSON before saving
    try { JsonDocument.Parse(body); }
    catch { return Results.BadRequest(new { error = "Invalid JSON" }); }
    await File.WriteAllTextAsync(Path.Combine(dataDir, "tolerances.json"), body);
    return Results.Ok(new { success = true });
});

// ============================================================================
// QA CALENDAR
// ============================================================================
app.MapGet("/api/calendar", async (MyQCDbContext db, int? year, int? month) =>
{
    var y = year  ?? DateTime.Now.Year;
    var m = month ?? DateTime.Now.Month;
    var start = new DateTime(y, m, 1);
    var end   = start.AddMonths(1);

    var measurements = await db.TRSMeasurements
        .Where(x => x.Date >= start && x.Date < end)
        .OrderBy(x => x.Date)
        .ToListAsync();

    var tolDoc = LoadToleranceDoc(dataDir);

    var byDay = measurements
        .GroupBy(x => x.Date.Day)
        .ToDictionary(
            g => g.Key,
            g => g.Select(x => new {
                id        = x.Id,
                time      = x.Date.ToString("HH:mm"),
                energy    = x.Energy,
                linacName = x.LinacName ?? "Unknown",
                deviation = Math.Round(x.Ecart, 3),
                passed    = Math.Abs(x.Ecart) <= GetToleranceFromDoc(tolDoc, x.LinacId, x.Energy),
                user      = x.UserName,
                tolerance = GetToleranceFromDoc(tolDoc, x.LinacId, x.Energy)
            }).ToArray()
        );

    // Next-due dates: last measurement per machine+energy → +30 days
    var allMeasurements = await db.TRSMeasurements.ToListAsync();
    var nextDueDates = allMeasurements
        .GroupBy(x => new { Id = x.LinacId ?? x.LinacName ?? "?", Energy = x.Energy ?? "?" })
        .Select(g =>
        {
            var last = g.OrderByDescending(x => x.Date).First();
            var due  = last.Date.AddDays(30);
            return new {
                linacId   = g.Key.Id,
                energy    = g.Key.Energy,
                linacName = last.LinacName ?? g.Key.Id,
                dueDate   = due.ToString("yyyy-MM-dd"),
                dueYear   = due.Year,
                dueMonth  = due.Month,
                dueDay    = due.Day,
                lastDate  = last.Date.ToString("yyyy-MM-dd"),
                overdue   = due.Date < DateTime.Now.Date
            };
        })
        .Where(x => x.dueYear == y && x.dueMonth == m)  // only show due dates in this month
        .ToArray();

    return Results.Ok(new {
        year              = y,
        month             = m,
        daysInMonth       = DateTime.DaysInMonth(y, m),
        firstDayOfWeek    = (int)start.DayOfWeek,   // 0=Sun … 6=Sat
        byDay,
        nextDueDates
    });
});

// ============================================================================
// DETECTOR LIBRARY — SAVE CUSTOM
// ============================================================================
app.MapPost("/api/detectors/save", async (HttpContext ctx) =>
{
    var body = await new StreamReader(ctx.Request.Body).ReadToEndAsync();
    try { JsonDocument.Parse(body); }
    catch { return Results.BadRequest(new { error = "Invalid JSON" }); }
    // Save to userData so it persists across app updates
    var customPath = Path.Combine(dataDir, "detector_library_custom.json");
    await File.WriteAllTextAsync(customPath, body);
    return Results.Ok(new { success = true, path = customPath });
});

// Override GET /api/detectors to prefer custom library
app.MapGet("/api/detectors/list", async () =>
{
    // Custom (user-edited) takes priority over bundled
    var customPath = Path.Combine(dataDir, "detector_library_custom.json");
    if (File.Exists(customPath))
        return Results.Content(await File.ReadAllTextAsync(customPath), "application/json");

    var candidates = new[]
    {
        Path.Combine(env.ContentRootPath, "detector_library.json"),
        Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", "detector_library.json")),
        Path.Combine(AppContext.BaseDirectory, "detector_library.json"),
    };
    var path = candidates.FirstOrDefault(File.Exists);
    if (path is null) return Results.NotFound();
    return Results.Content(await File.ReadAllTextAsync(path), "application/json");
});

// ============================================================================
// AUTO-BACKUP CONFIG
// ============================================================================
app.MapGet("/api/backup/config", () =>
{
    var cfgPath = Path.Combine(dataDir, "backup_config.json");
    if (!File.Exists(cfgPath))
        return Results.Ok(new { enabled = false, keepCount = 10, folder = "", lastBackup = (string?)null });
    try
    {
        var raw = File.ReadAllText(cfgPath);
        return Results.Content(raw, "application/json");
    }
    catch { return Results.Ok(new { enabled = false, keepCount = 10, folder = "", lastBackup = (string?)null }); }
});

app.MapPost("/api/backup/config", async (HttpContext ctx) =>
{
    var body = await new StreamReader(ctx.Request.Body).ReadToEndAsync();
    var cfgPath = Path.Combine(dataDir, "backup_config.json");
    await File.WriteAllTextAsync(cfgPath, body);
    return Results.Ok(new { success = true });
});

// ============================================================================
// DEMO SEED — generate 12 months of realistic QA measurements
// Schedule: ~55% Saturday morning, ~25% Sunday morning,
//           ~12% Friday 17–18h (after last treatment),
//            ~8% Monday 08–09h (3 days after Friday treatment)
// ============================================================================
app.MapPost("/api/demo/seed", async (HttpContext ctx, MyQCDbContext db, TRSService svc) =>
{
    using var sr = new StreamReader(ctx.Request.Body);
    var body = await sr.ReadToEndAsync();
    using var jdoc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
    bool clearFirst = jdoc.RootElement.TryGetProperty("clearFirst", out var cf) && cf.GetBoolean();

    if (clearFirst)
    {
        db.TRSMeasurements.RemoveRange(db.TRSMeasurements);
        await db.SaveChangesAsync();
    }

    var rng = new Random();

    // Machines & beams to generate for
    var machines = new[]
    {
        new { Id = "VHD1", Name = "Versa HD",  Energies = new[] { "6X", "10X", "15X" } },
        new { Id = "TB1",  Name = "TrueBeam",  Energies = new[] { "6X", "10X", "6FFF" } },
    };

    string[] physicists = { "Physicist A", "Physicist B", "Physicist C" };

    // Day-type weights: 0=Saturday, 1=Sunday, 2=Friday-evening, 3=Monday-post
    int[] weights = { 55, 25, 12, 8 };

    int DayTypePick()
    {
        int r = rng.Next(100), cum = 0;
        for (int i = 0; i < weights.Length; i++) { cum += weights[i]; if (r < cum) return i; }
        return 0;
    }

    // Given a reference Monday in a week, return measurement date/time for day type
    DateTime MeasurementDate(DateTime weekMonday, int dayType)
    {
        DateTime d = dayType switch
        {
            0 => weekMonday.AddDays(5),  // Saturday
            1 => weekMonday.AddDays(6),  // Sunday
            2 => weekMonday.AddDays(4),  // Friday
            _ => weekMonday.AddDays(7),  // next Monday
        };
        // Time range
        int hMin = dayType switch { 2 => 17, 3 => 8, _ => 8 };
        int hMax = dayType switch { 2 => 18, 3 => 9, _ => 11 };
        int h = rng.Next(hMin, hMax + 1);
        int m = rng.Next(0, 60);
        return d.Date.AddHours(h).AddMinutes(m);
    }

    // Spread over the last 12 months, one session per ~4 weeks per machine
    var now  = DateTime.Now;
    var start = now.AddMonths(-12);
    // Find first Monday on or after start
    var cursor = start;
    while (cursor.DayOfWeek != DayOfWeek.Monday) cursor = cursor.AddDays(1);

    var added = new List<TRSMeasurement>();

    // Available day slots within the QA weekend (relative to cursor Monday)
    // Fri = +4 (17–18h, after treatment), Sat = +5 (08–11h), Sun = +6 (08–11h)
    DateTime SlotDate(DateTime monday, int daysOffset)
    {
        var d    = monday.AddDays(daysOffset);
        bool fri = daysOffset == 4;
        int h    = fri ? rng.Next(17, 19) : rng.Next(8, 12);
        int m    = rng.Next(0, 60);
        return d.Date.AddHours(h).AddMinutes(m);
    }

    // Pick how to split N energies across Fri/Sat/Sun for one machine session.
    // Returns list of (dayOffset, energy) pairs.
    List<(int slot, string energy)> SplitAcrossWeekend(string[] energies)
    {
        var result = new List<(int, string)>();
        // Possible day-slot sets (always includes Sat; Fri and Sun are optional)
        //   pattern 0 = Sat only (all beams)             — 30%
        //   pattern 1 = Sat + Sun                        — 30%
        //   pattern 2 = Fri + Sat                        — 20%
        //   pattern 3 = Fri + Sat + Sun                  — 20%
        int pat = rng.Next(100) switch { < 30 => 0, < 60 => 1, < 80 => 2, _ => 3 };

        var slots = pat switch
        {
            0 => new[] { 5 },
            1 => new[] { 5, 6 },
            2 => new[] { 4, 5 },
            _ => new[] { 4, 5, 6 }
        };

        // Shuffle energies then distribute round-robin across chosen slots
        var shuffled = energies.OrderBy(_ => rng.Next()).ToList();
        for (int i = 0; i < shuffled.Count; i++)
            result.Add((slots[i % slots.Length], shuffled[i]));

        // Sort by slot so time is chronological
        result.Sort((a, b) => a.Item1.CompareTo(b.Item1));
        return result;
    }

    while (cursor < now.AddDays(-7))
    {
        foreach (var machine in machines)
        {
            // Split this machine's energies across Fri/Sat/Sun of the same weekend
            var assignments = SplitAcrossWeekend(machine.Energies);

            // Track time offset per slot so beams within the same day are spaced apart
            var slotOffsets = new Dictionary<int, TimeSpan>();

            foreach (var pair in assignments)
            {
                int slot   = pair.Item1;
                string energy = pair.Item2;
                if (!slotOffsets.ContainsKey(slot)) slotOffsets[slot] = TimeSpan.Zero;
                var dt = SlotDate(cursor, slot).Add(slotOffsets[slot]);
                slotOffsets[slot] += TimeSpan.FromMinutes(rng.Next(20, 35));

                double roll      = rng.NextDouble();
                double devTarget = roll < 0.70 ? rng.NextDouble() * 1.2 - 0.6
                                 : roll < 0.90 ? rng.NextDouble() * 2.0 - 1.0
                                 :               rng.NextDouble() * 5.0 - 2.5;

                double kQ       = 0.9985;
                double ndw      = 0.04967;
                double dwTarget = 1.0 + devTarget / 100.0;
                double mBase    = dwTarget / (kQ * ndw);
                double noise    = 0.0003;

                var meas = new TRSMeasurement
                {
                    Date        = dt,
                    UserName    = physicists[rng.Next(physicists.Length)],
                    Mode        = "photon",
                    ClinicName  = "Centre de Radiothérapie",
                    Energy      = energy,
                    LinacId     = machine.Id,
                    LinacName   = machine.Name,
                    FieldSize   = "10x10",
                    Chamber     = "FC65-G",
                    ChamberType = "cylindrical",
                    Ndw         = ndw,
                    kQ          = kQ,
                    T           = 20.0 + rng.NextDouble() * 2 - 1,
                    P           = 1013.0 + rng.NextDouble() * 4 - 2,
                    M_plus_1    = mBase + (rng.NextDouble() - 0.5) * noise,
                    M_plus_2    = mBase + (rng.NextDouble() - 0.5) * noise,
                    M_plus_3    = mBase + (rng.NextDouble() - 0.5) * noise,
                    M_minus_1   = -(mBase + (rng.NextDouble() - 0.5) * noise),
                    M_minus_2   = -(mBase + (rng.NextDouble() - 0.5) * noise),
                    M_minus_3   = -(mBase + (rng.NextDouble() - 0.5) * noise),
                    M100V_1     = mBase + (rng.NextDouble() - 0.5) * noise * 2,
                    M100V_2     = mBase + (rng.NextDouble() - 0.5) * noise * 2,
                    M100V_3     = mBase + (rng.NextDouble() - 0.5) * noise * 2,
                    SSD         = 100,
                };
                added.Add(svc.Calculate(meas));
            }
        }

        cursor = cursor.AddDays(28 + rng.Next(-3, 4));
        while (cursor.DayOfWeek != DayOfWeek.Monday) cursor = cursor.AddDays(1);
    }

    db.TRSMeasurements.AddRange(added);
    await db.SaveChangesAsync();

    var dayCounts = added.GroupBy(m => m.Date.DayOfWeek)
                         .ToDictionary(g => g.Key.ToString(), g => g.Count());
    return Results.Ok(new { seeded = added.Count, byDay = dayCounts });
});

// ============================================================================
// TIMEZONE FIX — shift all stored dates by N hours (for legacy UTC offset data)
// ============================================================================
app.MapPost("/api/admin/fix-timezone", async (HttpContext ctx, MyQCDbContext db) =>
{
    using var sr = new StreamReader(ctx.Request.Body);
    var body = await sr.ReadToEndAsync();
    using var doc = JsonDocument.Parse(body);
    if (!doc.RootElement.TryGetProperty("offsetHours", out var el)) return Results.BadRequest(new { error = "offsetHours required" });
    int offsetHours = el.GetInt32();
    if (offsetHours == 0) return Results.Ok(new { updated = 0 });
    var all = await db.TRSMeasurements.ToListAsync();
    foreach (var m in all) m.Date = m.Date.AddHours(offsetHours);
    await db.SaveChangesAsync();
    return Results.Ok(new { updated = all.Count });
});

// ============================================================================
// PREDICTIONS — linear regression per beam/energy
// ============================================================================
app.MapGet("/api/predictions", async (MyQCDbContext db) =>
{
    var cutoff = DateTime.Now.AddMonths(-12);
    var all = await db.TRSMeasurements.Where(m => m.Date >= cutoff).OrderBy(m => m.Date).ToListAsync();
    var tolDoc = LoadToleranceDoc(dataDir);
    var passing = all.Where(m => Math.Abs(m.Ecart) <= GetToleranceFromDoc(tolDoc, m.LinacId, m.Energy)).ToList();

    var predictions = passing
        .GroupBy(m => m.Energy ?? "?")
        .Select(g =>
        {
            var pts = g.OrderBy(m => m.Date).ToList();
            // x = days since first in group, y = Dw
            var t0   = pts.First().Date;
            var xs   = pts.Select(m => (m.Date - t0).TotalDays).ToArray();
            var ys   = pts.Select(m => m.DW_Zref).ToArray();
            int n    = pts.Count;

            double slope = 0, intercept = ys.Average();
            if (n >= 3)
            {
                double mx = xs.Average(), my = ys.Average();
                double num = xs.Zip(ys, (x, y) => (x - mx) * (y - my)).Sum();
                double den = xs.Select(x => (x - mx) * (x - mx)).Sum();
                slope     = den > 0 ? num / den : 0;
                intercept = my - slope * mx;
            }

            // Predict at +30 days from last measurement
            var lastDate  = pts.Last().Date;
            var nextDue   = lastDate.AddDays(30);
            double xNext  = (nextDue - t0).TotalDays;
            double predDw = Math.Round(intercept + slope * xNext, 5);

            // Std deviation of residuals as uncertainty estimate
            double[] resid = pts.Select((m, i) => m.DW_Zref - (intercept + slope * xs[i])).ToArray();
            double sigma   = n >= 3
                ? Math.Round(Math.Sqrt(resid.Select(r => r * r).Sum() / (n - 2)), 5)
                : 0.005;

            double predEcart = Math.Round((predDw - 1.0) * 100, 3);
            string trend = slope >  0.000005 ? "rising"
                         : slope < -0.000005 ? "falling"
                         : "stable";
            var predTol  = GetToleranceFromDoc(tolDoc, g.First().LinacId, g.Key);
            bool likelyPass = Math.Abs(predEcart) + sigma * 200 <= predTol;

            return new {
                energy      = g.Key,
                linac       = pts.Last().LinacName ?? "Unknown",
                lastDate    = lastDate.ToString("yyyy-MM-dd"),
                lastDw      = Math.Round(pts.Last().DW_Zref, 5),
                lastEcart   = Math.Round(pts.Last().Ecart, 3),
                nextDue     = nextDue.ToString("yyyy-MM-dd"),
                daysUntil   = (int)(nextDue - DateTime.Now.Date).TotalDays,
                predictedDw = predDw,
                predictedEcart = predEcart,
                uncertainty = sigma,
                trend,
                likelyPass,
                pointsUsed  = n,
            };
        })
        .OrderBy(p => p.energy)
        .ToList();

    return Results.Ok(new { predictions });
});

// ============================================================================
// EMAIL CONFIG
// ============================================================================
EmailConfig LoadEmailConfig() =>
    File.Exists(emailConfigPath)
        ? JsonSerializer.Deserialize<EmailConfig>(File.ReadAllText(emailConfigPath))!
        : new EmailConfig();

async Task SendAlertAsync(TRSMeasurement m, double tolerance)
{
    try
    {
        var cfg = LoadEmailConfig();
        if (!cfg.AlertsEnabled || !cfg.IsConfigured || string.IsNullOrWhiteSpace(cfg.AlertTo)) return;
        var sign  = m.Ecart >= 0 ? "+" : "";
        var msg   = new MimeMessage();
        msg.From.Add(new MailboxAddress(cfg.FromName, cfg.FromAddress));
        msg.To.Add(MailboxAddress.Parse(cfg.AlertTo));
        msg.Subject = $"⚠️ Out-of-Tolerance: {m.LinacName} {m.Energy} — {sign}{m.Ecart:F2}%";
        msg.Body = new TextPart("html")
        {
            Text = $@"
<h2 style='color:#dc2626;font-family:sans-serif'>⚠️ Out-of-Tolerance Alert</h2>
<table style='font-family:sans-serif;font-size:14px;border-collapse:collapse'>
  <tr><td style='padding:4px 12px 4px 0;color:#666'>Machine</td><td><b>{m.LinacName}</b></td></tr>
  <tr><td style='padding:4px 12px 4px 0;color:#666'>Energy</td><td><b>{m.Energy}</b></td></tr>
  <tr><td style='padding:4px 12px 4px 0;color:#666'>Deviation</td><td><b style='color:#dc2626'>{sign}{m.Ecart:F2}%</b></td></tr>
  <tr><td style='padding:4px 12px 4px 0;color:#666'>Tolerance</td><td>±{tolerance:F1}%</td></tr>
  <tr><td style='padding:4px 12px 4px 0;color:#666'>Dose (Gy)</td><td>{m.DW_Zref:F4}</td></tr>
  <tr><td style='padding:4px 12px 4px 0;color:#666'>Physicist</td><td>{m.UserName}</td></tr>
  <tr><td style='padding:4px 12px 4px 0;color:#666'>Date</td><td>{m.Date:dd MMM yyyy HH:mm}</td></tr>
  <tr><td style='padding:4px 12px 4px 0;color:#666'>Mode</td><td>{m.Mode}</td></tr>
</table>
<p style='font-family:sans-serif;font-size:12px;color:#999;margin-top:20px'>TRS-398 Pro — automatic alert</p>"
        };
        using var client = new SmtpClient();
        await client.ConnectAsync(cfg.Host, cfg.Port, cfg.UseTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto);
        await client.AuthenticateAsync(cfg.Username, cfg.Password);
        await client.SendAsync(msg);
        await client.DisconnectAsync(true);
    }
    catch { /* alert failure must never break measurement save */ }
}

app.MapGet("/api/email/config", () => Results.Ok(LoadEmailConfig()));

app.MapPost("/api/email/config", async (HttpRequest request) =>
{
    var cfg = await request.ReadFromJsonAsync<EmailConfig>();
    if (cfg is null) return Results.BadRequest();
    File.WriteAllText(emailConfigPath, JsonSerializer.Serialize(cfg,
        new JsonSerializerOptions { WriteIndented = true }));
    return Results.Ok(new { saved = true });
});

app.MapPost("/api/email/test", async (HttpRequest request) =>
{
    var body = await request.ReadFromJsonAsync<JsonElement>();
    var to   = body.GetProperty("to").GetString() ?? "";
    var cfg  = LoadEmailConfig();
    if (!cfg.IsConfigured) return Results.BadRequest(new { error = "Email not configured" });
    try
    {
        var msg = new MimeMessage();
        msg.From.Add(MailboxAddress.Parse(cfg.FromAddress));
        msg.To.Add(MailboxAddress.Parse(to));
        msg.Subject = "TRS-398 Pro — Test Email";
        msg.Body    = new TextPart("plain") { Text = "Email configuration is working correctly.\n\nTRS-398 Pro" };
        using var client = new SmtpClient();
        await client.ConnectAsync(cfg.Host, cfg.Port, cfg.UseTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto);
        await client.AuthenticateAsync(cfg.Username, cfg.Password);
        await client.SendAsync(msg);
        await client.DisconnectAsync(true);
        return Results.Ok(new { sent = true });
    }
    catch (Exception ex) { return Results.Ok(new { sent = false, error = ex.Message }); }
});

// ============================================================================
// EMAIL REPORT
// ============================================================================
app.MapPost("/api/email/report", async (HttpRequest request, MyQCDbContext db, PdfReportService pdfService) =>
{
    var body = await request.ReadFromJsonAsync<EmailRequest>();
    if (body == null)
        return Results.BadRequest(new { error = "Invalid request" });

    var measurement = await db.TRSMeasurements.FindAsync(body.MeasurementId);
    if (measurement == null)
        return Results.NotFound(new { error = "Measurement not found" });

    var pdfBytes = pdfService.Build(measurement, body.Signature);
    var cfg      = LoadEmailConfig();

    if (cfg.IsConfigured && !string.IsNullOrWhiteSpace(body.To))
    {
        try
        {
            var msg = new MimeMessage();
            msg.From.Add(MailboxAddress.Parse(cfg.FromAddress));
            msg.To.Add(MailboxAddress.Parse(body.To));
            msg.Subject = body.Subject ?? $"TRS-398 Report — {measurement.LinacName} {measurement.Energy} {measurement.Date:yyyy-MM-dd}";
            var builder = new BodyBuilder
            {
                TextBody = $"Please find attached the TRS-398 calibration report for {measurement.LinacName} ({measurement.Energy}) performed on {measurement.Date:dd MMMM yyyy}.\n\nTRS-398 Pro"
            };
            builder.Attachments.Add($"TRS398_{measurement.Id:D4}.pdf", pdfBytes, new ContentType("application","pdf"));
            msg.Body = builder.ToMessageBody();
            using var client = new SmtpClient();
            await client.ConnectAsync(cfg.Host, cfg.Port, cfg.UseTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto);
            await client.AuthenticateAsync(cfg.Username, cfg.Password);
            await client.SendAsync(msg);
            await client.DisconnectAsync(true);
            return Results.Ok(new { success = true, sent = true, message = $"Report sent to {body.To}" });
        }
        catch (Exception ex)
        {
            return Results.Ok(new { success = true, sent = false, error = ex.Message,
                pdfBase64 = Convert.ToBase64String(pdfBytes) });
        }
    }

    // No SMTP configured — return PDF as base64 fallback
    return Results.Ok(new {
        success  = true,
        sent     = false,
        message  = "Email not configured — download PDF manually",
        pdfBase64 = Convert.ToBase64String(pdfBytes),
        filename = $"trs398_report_{body.MeasurementId}.pdf",
    });
});

// Note: this app uses operator names (no passwords / no login) — see /api/operators.

// ============================================================================
// TRANSLATIONS / I18N
// ============================================================================
app.MapGet("/api/i18n/{lang}", (string lang) =>
{
    var translations = GetTranslations(lang);
    return Results.Ok(translations);
});

// ============================================================================
// SIGNATURE UPLOAD
// ============================================================================
app.MapPost("/api/signature/upload", async (HttpRequest request) =>
{
    var signaturesDir = Path.Combine(dataDir, "signatures");
    if (!Directory.Exists(signaturesDir))
        Directory.CreateDirectory(signaturesDir);

    if (!request.HasFormContentType)
        return Results.BadRequest(new { error = "Expected form data" });

    var form = await request.ReadFormAsync();
    var file = form.Files.GetFile("signature");
    var userName = form["userName"].ToString();

    if (file == null || file.Length == 0)
        return Results.BadRequest(new { error = "No signature uploaded" });

    var extension = Path.GetExtension(file.FileName).ToLower();
    if (string.IsNullOrEmpty(extension)) extension = ".png";
    
    var safeName = string.Join("_", (userName ?? "signature").Split(Path.GetInvalidFileNameChars()));
    var filename = $"{safeName}_signature{extension}";
    var filePath = Path.Combine(signaturesDir, filename);

    using (var stream = new FileStream(filePath, FileMode.Create))
    {
        await file.CopyToAsync(stream);
    }

    return Results.Ok(new { 
        success = true, 
        filename = filename,
        path = $"/signatures/{filename}"
    });
});

app.MapGet("/api/signatures", () =>
{
    var signaturesDir = Path.Combine(dataDir, "signatures");
    if (!Directory.Exists(signaturesDir))
        return Results.Ok(new { signatures = Array.Empty<object>() });

    var signatures = Directory.GetFiles(signaturesDir)
        .Where(f => new[] { ".png", ".jpg", ".jpeg" }.Contains(Path.GetExtension(f).ToLower()))
        .Select(f => new {
            filename = Path.GetFileName(f),
            path = $"/signatures/{Path.GetFileName(f)}"
        })
        .ToList();

    return Results.Ok(new { signatures });
});

// ============================================================================
// PROFILES / TEMPLATES
// ============================================================================
var profilesPath = Path.Combine(dataDir, "profiles.json");

List<ProfileDoc> LoadProfiles()
{
    if (!File.Exists(profilesPath)) return new();
    try { return JsonSerializer.Deserialize<List<ProfileDoc>>(File.ReadAllText(profilesPath),
              new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new(); }
    catch { return new(); }
}

void SaveProfiles(List<ProfileDoc> profiles)
{
    File.WriteAllText(profilesPath, JsonSerializer.Serialize(profiles,
        new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
}

app.MapGet("/api/profiles", () => Results.Ok(LoadProfiles()));

app.MapGet("/api/profiles/default", () =>
{
    var profiles = LoadProfiles();
    var def = profiles.FirstOrDefault(p => p.IsDefault) ?? profiles.FirstOrDefault();
    return def is not null ? Results.Ok(def) : Results.NotFound();
});

app.MapPost("/api/profiles", async (HttpRequest request) =>
{
    var body = await new StreamReader(request.Body).ReadToEndAsync();
    ProfileDoc? incoming;
    try { incoming = JsonSerializer.Deserialize<ProfileDoc>(body,
              new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
    catch { return Results.BadRequest("Invalid JSON"); }
    if (incoming is null || string.IsNullOrWhiteSpace(incoming.Name))
        return Results.BadRequest("Name is required");

    var profiles = LoadProfiles();
    var newProfile = incoming with
    {
        Id        = Guid.NewGuid().ToString(),
        CreatedAt = DateTime.UtcNow.ToString("o"),
        UpdatedAt = DateTime.UtcNow.ToString("o"),
        IsDefault = !profiles.Any() || incoming.IsDefault,
    };
    if (newProfile.IsDefault) foreach (var p in profiles) profiles[profiles.IndexOf(p)] = p with { IsDefault = false };
    profiles.Add(newProfile);
    SaveProfiles(profiles);
    return Results.Ok(newProfile);
});

app.MapPut("/api/profiles/{id}", async (string id, HttpRequest request) =>
{
    var body = await new StreamReader(request.Body).ReadToEndAsync();
    ProfileDoc? incoming;
    try { incoming = JsonSerializer.Deserialize<ProfileDoc>(body,
              new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
    catch { return Results.BadRequest("Invalid JSON"); }
    if (incoming is null) return Results.BadRequest();

    var profiles = LoadProfiles();
    var idx = profiles.FindIndex(p => p.Id == id);
    if (idx < 0) return Results.NotFound();

    var updated = incoming with { Id = id, UpdatedAt = DateTime.UtcNow.ToString("o") };
    if (updated.IsDefault)
        for (int i = 0; i < profiles.Count; i++)
            profiles[i] = profiles[i] with { IsDefault = profiles[i].Id == id };
    profiles[idx] = updated;
    SaveProfiles(profiles);
    return Results.Ok(updated);
});

app.MapDelete("/api/profiles/{id}", (string id) =>
{
    var profiles = LoadProfiles();
    var idx = profiles.FindIndex(p => p.Id == id);
    if (idx < 0) return Results.NotFound();
    bool wasDefault = profiles[idx].IsDefault;
    profiles.RemoveAt(idx);
    if (wasDefault && profiles.Any()) profiles[0] = profiles[0] with { IsDefault = true };
    SaveProfiles(profiles);
    return Results.Ok();
});

app.MapPost("/api/profiles/{id}/default", (string id) =>
{
    var profiles = LoadProfiles();
    if (!profiles.Any(p => p.Id == id)) return Results.NotFound();
    for (int i = 0; i < profiles.Count; i++)
        profiles[i] = profiles[i] with { IsDefault = profiles[i].Id == id };
    SaveProfiles(profiles);
    return Results.Ok();
});

// ============================================================================
// NOTIFICATIONS / REMINDERS
// ============================================================================
app.MapGet("/api/notifications", async (MyQCDbContext db) =>
{
    var measurements = await db.TRSMeasurements
        .OrderByDescending(m => m.Date)
        .ToListAsync();

    var notifications = new List<object>();
    var now = DateTime.Now.Date;

    var notifTolDoc = LoadToleranceDoc(dataDir);
    // Per beam/energy: find last PASS measurement, compute next due date (+ 30 days)
    var beamGroups = measurements
        .Where(m => Math.Abs(m.Ecart) <= GetToleranceFromDoc(notifTolDoc, m.LinacId, m.Energy))
        .GroupBy(m => new { Linac = m.LinacName ?? "Unknown", Energy = m.Energy ?? "?" });

    foreach (var group in beamGroups)
    {
        var lastPass    = group.Max(m => m.Date).Date;
        var nextDue     = lastPass.AddDays(30);
        var daysUntil   = (nextDue - now).TotalDays;
        var daysSince   = (now - lastPass).TotalDays;

        string type, title, message;

        if (daysUntil < 0)
        {
            type    = "overdue";
            title   = "QA Overdue";
            message = $"{group.Key.Linac} — {group.Key.Energy}: overdue by {(int)Math.Abs(daysUntil)} day(s)";
        }
        else if (daysUntil <= 3)
        {
            type    = "urgent";
            title   = daysUntil == 0 ? "QA Due Today" : $"QA Due in {(int)daysUntil} Day(s)";
            message = $"{group.Key.Linac} — {group.Key.Energy}: due {nextDue:dd MMM yyyy}";
        }
        else if (daysUntil <= 7)
        {
            type    = "warning";
            title   = $"QA Due in {(int)daysUntil} Days";
            message = $"{group.Key.Linac} — {group.Key.Energy}: due {nextDue:dd MMM yyyy}";
        }
        else
        {
            continue; // more than a week away — no notification needed
        }

        notifications.Add(new {
            type,
            title,
            message,
            linac       = group.Key.Linac,
            energy      = group.Key.Energy,
            lastDate    = lastPass.ToString("yyyy-MM-dd"),
            dueDate     = nextDue.ToString("yyyy-MM-dd"),
            daysUntil   = (int)daysUntil,
        });
    }

    // Recent failed calibrations (last 7 days) — separate alert
    var recentFailed = measurements
        .Where(m => m.Date > DateTime.Now.AddDays(-7) && Math.Abs(m.Ecart) > GetToleranceFromDoc(notifTolDoc, m.LinacId, m.Energy))
        .GroupBy(m => new { m.LinacName, m.Energy })
        .Select(g => g.OrderByDescending(x => x.Date).First());

    foreach (var m in recentFailed)
    {
        notifications.Add(new {
            type        = "fail",
            title       = "Recent Failed QA",
            message     = $"{m.LinacName} — {m.Energy}: deviation {m.Ecart:+0.00;-0.00}% on {m.Date:dd MMM}",
            linac       = m.LinacName,
            energy      = m.Energy,
            lastDate    = m.Date.ToString("yyyy-MM-dd"),
            dueDate     = (string?)null,
            daysUntil   = 0,
            measurementId = m.Id,
        });
    }

    // Sort: overdue first, then urgent, then warning, then fail
    int SortRank(string t) => t switch { "overdue"=>0,"urgent"=>1,"warning"=>2,"fail"=>3,_=>9 };
    notifications.Sort((a, b) => SortRank(((dynamic)a).type).CompareTo(SortRank(((dynamic)b).type)));

    return Results.Ok(new { notifications, count = notifications.Count });
});

// ============================================================================
// DATABASE INITIALIZATION
// ============================================================================
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MyQCDbContext>();
    db.Database.EnsureCreated();
    EnsureSchema(db);
}

// Auto-backup timer: fires every hour, runs backup if enabled and due
var autoBackupTimer = new Timer(_ =>
{
    try
    {
        var cfgPath = Path.Combine(dataDir, "backup_config.json");
        if (!File.Exists(cfgPath)) return;
        var cfg = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(cfgPath));
        if (!cfg.TryGetProperty("enabled", out var en) || !en.GetBoolean()) return;

        // Check lastBackup — skip if done in last 23h
        if (cfg.TryGetProperty("lastBackup", out var lb) && lb.ValueKind == JsonValueKind.String)
        {
            if (DateTime.TryParse(lb.GetString(), out var last) && (DateTime.Now - last).TotalHours < 23) return;
        }

        var folder = cfg.TryGetProperty("folder", out var fld) && fld.ValueKind == JsonValueKind.String
                     && !string.IsNullOrWhiteSpace(fld.GetString())
                   ? fld.GetString()! : Path.Combine(dataDir, "auto-backups");
        Directory.CreateDirectory(folder);

        var src  = Path.Combine(dataDir, "trs398.db");
        var dest = Path.Combine(folder, $"trs398_auto_{DateTime.Now:yyyyMMdd_HHmm}.db");
        if (File.Exists(src)) File.Copy(src, dest, overwrite: true);

        // Prune old backups — keep last N
        int keep = cfg.TryGetProperty("keepCount", out var kc) ? kc.GetInt32() : 10;
        var old = Directory.GetFiles(folder, "trs398_auto_*.db")
                            .OrderByDescending(f => f).Skip(keep).ToArray();
        foreach (var f in old) File.Delete(f);

        // Update lastBackup timestamp
        var updated = new
        {
            enabled    = true,
            keepCount  = keep,
            folder     = cfg.TryGetProperty("folder", out var f2) ? f2.GetString() : "",
            lastBackup = DateTime.Now.ToString("o")
        };
        File.WriteAllText(cfgPath, JsonSerializer.Serialize(updated));
    }
    catch { /* swallow — don't crash the server for a backup failure */ }
}, null, TimeSpan.FromMinutes(5), TimeSpan.FromHours(1));

app.Run();

// ============================================================================
// HELPER METHODS & CLASSES
// ============================================================================

static string HashPassword(string password)
{
    using var sha256 = SHA256.Create();
    var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password + "TRS398_SALT"));
    return Convert.ToBase64String(bytes);
}

static object GetTranslations(string lang)
{
    return lang.ToLower() switch
    {
        "fr" => new {
            language = "Français",
            app_title = "TRS-398 Pro - Calibration",
            dashboard = "Tableau de bord",
            measurements = "Mesures",
            history = "Historique",
            settings = "Paramètres",
            calculate = "Calculer",
            save = "Enregistrer",
            export = "Exporter",
            print = "Imprimer",
            energy = "Énergie",
            temperature = "Température",
            pressure = "Pression",
            chamber = "Chambre",
            dose = "Dose",
            deviation = "Écart",
            status = "Statut",
            pass = "CONFORME",
            fail = "NON CONFORME",
            photon = "Photon",
            electron = "Électron",
            clinic = "Clinique",
            user = "Utilisateur",
            date = "Date",
            notes = "Notes",
            backup = "Sauvegarde",
            restore = "Restaurer",
            theme = "Thème",
            dark = "Sombre",
            light = "Clair"
        },
        "es" => new {
            language = "Español",
            app_title = "TRS-398 Pro - Calibración",
            dashboard = "Panel de control",
            measurements = "Mediciones",
            history = "Historial",
            settings = "Configuración",
            calculate = "Calcular",
            save = "Guardar",
            export = "Exportar",
            print = "Imprimir",
            energy = "Energía",
            temperature = "Temperatura",
            pressure = "Presión",
            chamber = "Cámara",
            dose = "Dosis",
            deviation = "Desviación",
            status = "Estado",
            pass = "APROBADO",
            fail = "RECHAZADO",
            photon = "Fotón",
            electron = "Electrón",
            clinic = "Clínica",
            user = "Usuario",
            date = "Fecha",
            notes = "Notas",
            backup = "Respaldo",
            restore = "Restaurar",
            theme = "Tema",
            dark = "Oscuro",
            light = "Claro"
        },
        "ar" => new {
            language = "العربية",
            app_title = "TRS-398 Pro - المعايرة",
            dashboard = "لوحة التحكم",
            measurements = "القياسات",
            history = "السجل",
            settings = "الإعدادات",
            calculate = "احسب",
            save = "حفظ",
            export = "تصدير",
            print = "طباعة",
            energy = "الطاقة",
            temperature = "درجة الحرارة",
            pressure = "الضغط",
            chamber = "الغرفة",
            dose = "الجرعة",
            deviation = "الانحراف",
            status = "الحالة",
            pass = "ناجح",
            fail = "فاشل",
            photon = "فوتون",
            electron = "إلكترون",
            clinic = "العيادة",
            user = "المستخدم",
            date = "التاريخ",
            notes = "ملاحظات",
            backup = "نسخ احتياطي",
            restore = "استعادة",
            theme = "المظهر",
            dark = "داكن",
            light = "فاتح"
        },
        _ => new {
            language = "English",
            app_title = "TRS-398 Pro - Calibration",
            dashboard = "Dashboard",
            measurements = "Measurements",
            history = "History",
            settings = "Settings",
            calculate = "Calculate",
            save = "Save",
            export = "Export",
            print = "Print",
            energy = "Energy",
            temperature = "Temperature",
            pressure = "Pressure",
            chamber = "Chamber",
            dose = "Dose",
            deviation = "Deviation",
            status = "Status",
            pass = "PASS",
            fail = "FAIL",
            photon = "Photon",
            electron = "Electron",
            clinic = "Clinic",
            user = "User",
            date = "Date",
            notes = "Notes",
            backup = "Backup",
            restore = "Restore",
            theme = "Theme",
            dark = "Dark",
            light = "Light"
        }
    };
}

static void EnsureSchema(MyQCDbContext db)
{
    using var conn = db.Database.GetDbConnection();
    conn.Open();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "PRAGMA table_info(TRSMeasurements)";
    var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    using (var reader = cmd.ExecuteReader())
    {
        while (reader.Read())
        {
            existing.Add(reader.GetString(1));
        }
    }

    var columns = new (string Name, string Sql)[]
    {
        ("LinacName", "TEXT"),
        ("LinacId", "TEXT"),
        ("FieldSize", "TEXT"),
        ("Chamber", "TEXT"),
        ("TPR2010", "REAL"),
        ("ClinicName", "TEXT"),
        ("ClinicAddress", "TEXT"),
        ("Mode", "TEXT"),
        ("R50", "REAL"),
        ("Zref", "REAL"),
        ("SSD", "REAL"),
        ("ChamberType", "TEXT"),
        ("Ndw_Qcross", "REAL"),
        ("R50_cross", "REAL"),
        ("NdUsed", "REAL"),
        ("kQUsed", "REAL"),
        ("Signature", "TEXT")
    };

    foreach (var column in columns)
    {
        if (!existing.Contains(column.Name))
        {
            db.Database.ExecuteSqlRaw($"ALTER TABLE TRSMeasurements ADD COLUMN {column.Name} {column.Sql}");
        }
    }
}

// ── Tolerance helpers ─────────────────────────────────────────────────────────

static JsonElement? LoadToleranceDoc(string dir)
{
    var path = Path.Combine(dir, "tolerances.json");
    if (!File.Exists(path)) return null;
    try { return JsonDocument.Parse(File.ReadAllText(path)).RootElement; }
    catch { return null; }
}

// Lookup order: machine+beam → machine default → global default → 2.0
static double GetToleranceFromDoc(JsonElement? doc, string? linacId, string? energy)
{
    if (doc is null) return 2.0;
    var root = doc.Value;
    if (!string.IsNullOrEmpty(linacId) && root.TryGetProperty("machines", out var machines))
    {
        if (machines.TryGetProperty(linacId, out var machine))
        {
            if (!string.IsNullOrEmpty(energy)
                && machine.TryGetProperty("beams", out var beams)
                && beams.TryGetProperty(energy, out var beamTol))
                return beamTol.GetDouble();
            if (machine.TryGetProperty("default", out var macDef))
                return macDef.GetDouble();
        }
    }
    if (root.TryGetProperty("default", out var globalDef)) return globalDef.GetDouble();
    return 2.0;
}

static double GetTolerance(string? linacId, string? energy, string dir)
    => GetToleranceFromDoc(LoadToleranceDoc(dir), linacId, energy);

// Request/Response classes
record LoginRequest(string Username, string Password);
record MachineReg(string Name, string? SerialId);
record NameDto(string? Name);
record ProfileDoc(
    string Id            = "",
    string Name          = "",
    bool   IsDefault     = false,
    string Notes         = "",
    string ClinicName    = "",
    string ClinicAddress = "",
    string ClinicPhone   = "",
    string ClinicEmail   = "",
    string Department    = "",
    string UserName      = "",
    string UserTitle     = "",
    string UserEmail     = "",
    string LinacName     = "",
    string LinacId       = "",
    string LinacModel    = "",
    string FieldSize     = "10×10",
    double Ndw           = 0.04789,
    double DefaultTemp   = 22.0,
    double DefaultPressure = 1013.0,
    string DefaultEnergy = "6X",
    string DefaultMode   = "photon",
    double Tolerance     = 2.0,
    string CreatedAt     = "",
    string UpdatedAt     = "");
record RegisterRequest(string Username, string Password, string? Role, string? FullName);
record EmailRequest(int MeasurementId, string To, string? Subject, string? Signature);

class EmailConfig
{
    public string Host          { get; set; } = "smtp.gmail.com";
    public int    Port          { get; set; } = 587;
    public bool   UseTls        { get; set; } = true;
    public string Username      { get; set; } = "";
    public string Password      { get; set; } = "";
    public string FromAddress   { get; set; } = "";
    public string FromName      { get; set; } = "TRS-398 Pro";
    public bool   AlertsEnabled { get; set; } = false;
    public string AlertTo       { get; set; } = "";
    public bool   IsConfigured  => !string.IsNullOrWhiteSpace(Host)
                                && !string.IsNullOrWhiteSpace(Username)
                                && !string.IsNullOrWhiteSpace(Password)
                                && !string.IsNullOrWhiteSpace(FromAddress);
}

class UserInfo
{
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "physicist";
    public string FullName { get; set; } = "";
}
