using Microsoft.EntityFrameworkCore;
using MyQC.Data;
using MyQC.Services;
using MyQC.Models;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using System.Security.Cryptography;

var builder = WebApplication.CreateBuilder(args);

// Configure JSON serialization to use camelCase
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

builder.Services.AddDbContext<MyQCDbContext>(options =>
    options.UseSqlite("Data Source=trs398.db"));

builder.Services.AddScoped<TRSService>();
builder.Services.AddScoped<PdfReportService>();

var app = builder.Build();
var env = app.Environment;

QuestPDF.Settings.License = LicenseType.Community;

app.MapGet("/", () => Results.Redirect("/index.html"));
app.UseStaticFiles();

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
// TRS CALCULATIONS & MEASUREMENTS
// ============================================================================
app.MapPost("/api/trs/calculate", (TRSMeasurement input, TRSService service) =>
{
    var result = service.Calculate(input);
    return Results.Ok(result);
});

app.MapPost("/api/trs/save", async (TRSMeasurement input, TRSService service, MyQCDbContext db) =>
{
    var result = service.Calculate(input);
    db.TRSMeasurements.Add(result);
    await db.SaveChangesAsync();
    return Results.Ok(result);
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
    var now = DateTime.Now;
    var thisMonth = measurements.Where(m => m.Date.Month == now.Month && m.Date.Year == now.Year).ToList();
    var thisYear = measurements.Where(m => m.Date.Year == now.Year).ToList();
    
    return Results.Ok(new {
        total = measurements.Count,
        thisMonth = thisMonth.Count,
        thisYear = thisYear.Count,
        passRate = measurements.Count > 0 
            ? Math.Round(measurements.Count(m => Math.Abs(m.Ecart) <= 2.0) * 100.0 / measurements.Count, 1) 
            : 0,
        avgDeviation = measurements.Count > 0 
            ? Math.Round(measurements.Average(m => Math.Abs(m.Ecart)), 2) 
            : 0,
        lastCalibration = measurements.OrderByDescending(m => m.Date).FirstOrDefault()?.Date,
        photonCount = measurements.Count(m => m.Mode == "Photon"),
        electronCount = measurements.Count(m => m.Mode == "Electron")
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
            passRate = Math.Round(g.Count(m => Math.Abs(m.Ecart) <= 2.0) * 100.0 / g.Count(), 1),
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
            sb.AppendLine($"<Cell><Data ss:Type=\"String\">{(Math.Abs(m.Ecart) <= 2.0 ? "PASS" : "FAIL")}</Data></Cell>");
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
app.MapGet("/api/trs/report/{id}", async (int id, MyQCDbContext db, PdfReportService pdfService, string? signature) =>
{
    var measurement = await db.TRSMeasurements.FindAsync(id);
    if (measurement is null) return Results.NotFound();
    
    var bytes = pdfService.Build(measurement, signature);
    var filename = $"trs398_report_{id}.pdf";
    return Results.File(bytes, "application/pdf", filename);
});

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

// ============================================================================
// DETECTORS
// ============================================================================
app.MapGet("/api/detectors", async () =>
{
    var path = Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", "detector_library.json"));
    if (!File.Exists(path)) return Results.NotFound();
    var json = await File.ReadAllTextAsync(path);
    return Results.Content(json, "application/json");
});

// ============================================================================
// LOGO MANAGEMENT
// ============================================================================
app.MapPost("/api/logo/upload", async (HttpRequest request) =>
{
    var logosDir = Path.Combine(env.WebRootPath ?? "wwwroot", "logos");
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
    var logosDir = Path.Combine(env.WebRootPath ?? "wwwroot", "logos");
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
    var logosDir = Path.Combine(env.WebRootPath ?? "wwwroot", "logos");
    var filePath = Path.Combine(logosDir, filename);
    
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
    var dbPath = Path.Combine(env.ContentRootPath, "trs398.db");
    if (!File.Exists(dbPath))
        return Results.NotFound(new { error = "Database not found" });
    
    var bytes = await File.ReadAllBytesAsync(dbPath);
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

    // Create backup of current database first
    var dbPath = Path.Combine(env.ContentRootPath, "trs398.db");
    var backupPath = Path.Combine(env.ContentRootPath, $"trs398_pre_restore_{DateTime.Now:yyyyMMdd_HHmmss}.db");
    
    if (File.Exists(dbPath))
        File.Copy(dbPath, backupPath);

    // Restore from uploaded file
    using (var stream = new FileStream(dbPath, FileMode.Create))
    {
        await file.CopyToAsync(stream);
    }

    return Results.Ok(new { 
        success = true, 
        message = "Database restored successfully",
        previousBackup = Path.GetFileName(backupPath)
    });
});

app.MapGet("/api/backup/list", () =>
{
    var backups = Directory.GetFiles(env.ContentRootPath, "*.db")
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
    
    // Returns PDF as base64 for frontend email handling
    
    return Results.Ok(new {
        success = true,
        message = "Report ready for email",
        pdfBase64 = Convert.ToBase64String(pdfBytes),
        filename = $"trs398_report_{body.MeasurementId}.pdf",
        to = body.To,
        subject = body.Subject ?? $"TRS-398 Calibration Report - {measurement.LinacName} - {measurement.Date:yyyy-MM-dd}"
    });
});

// ============================================================================
// USER AUTHENTICATION (Simple token-based)
// ============================================================================
var users = new Dictionary<string, UserInfo>
{
    ["admin"] = new UserInfo { Username = "admin", PasswordHash = HashPassword("admin123"), Role = "admin", FullName = "Administrator" },
    ["physicist"] = new UserInfo { Username = "physicist", PasswordHash = HashPassword("physics123"), Role = "physicist", FullName = "Medical Physicist" }
};

app.MapPost("/api/auth/login", (LoginRequest request) =>
{
    if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
        return Results.BadRequest(new { error = "Username and password required" });

    if (!users.TryGetValue(request.Username.ToLower(), out var user))
        return Results.Unauthorized();

    if (user.PasswordHash != HashPassword(request.Password))
        return Results.Unauthorized();

    // Generate simple token
    var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    
    return Results.Ok(new {
        success = true,
        token = token,
        user = new {
            username = user.Username,
            role = user.Role,
            fullName = user.FullName
        }
    });
});

app.MapPost("/api/auth/register", (RegisterRequest request) =>
{
    if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
        return Results.BadRequest(new { error = "Username and password required" });

    if (users.ContainsKey(request.Username.ToLower()))
        return Results.BadRequest(new { error = "Username already exists" });

    var newUser = new UserInfo
    {
        Username = request.Username.ToLower(),
        PasswordHash = HashPassword(request.Password),
        Role = request.Role ?? "physicist",
        FullName = request.FullName ?? request.Username
    };

    users[newUser.Username] = newUser;

    return Results.Ok(new { success = true, message = "User registered successfully" });
});

app.MapGet("/api/auth/users", () =>
{
    var userList = users.Values.Select(u => new {
        username = u.Username,
        role = u.Role,
        fullName = u.FullName
    }).ToList();

    return Results.Ok(new { users = userList });
});

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
    var signaturesDir = Path.Combine(env.WebRootPath ?? "wwwroot", "signatures");
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
    var signaturesDir = Path.Combine(env.WebRootPath ?? "wwwroot", "signatures");
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
// NOTIFICATIONS / REMINDERS
// ============================================================================
app.MapGet("/api/notifications", async (MyQCDbContext db) =>
{
    var measurements = await db.TRSMeasurements
        .OrderByDescending(m => m.Date)
        .ToListAsync();

    var notifications = new List<object>();
    var now = DateTime.Now;

    // Check for machines that haven't been calibrated recently
    var machineGroups = measurements.GroupBy(m => m.LinacName ?? "Unknown");
    foreach (var group in machineGroups)
    {
        var lastCalibration = group.Max(m => m.Date);
        var daysSince = (now - lastCalibration).TotalDays;
        
        if (daysSince > 30)
        {
            notifications.Add(new {
                type = "warning",
                title = "Calibration Due",
                message = $"{group.Key} hasn't been calibrated in {(int)daysSince} days",
                machine = group.Key,
                lastDate = lastCalibration
            });
        }
    }

    // Check for recent failed calibrations
    var recentFailed = measurements
        .Where(m => m.Date > now.AddDays(-7) && Math.Abs(m.Ecart) > 2.0)
        .ToList();

    foreach (var m in recentFailed)
    {
        notifications.Add(new {
            type = "error",
            title = "Failed Calibration",
            message = $"{m.LinacName} - {m.Energy} MV: Deviation {m.Ecart:F2}%",
            measurementId = m.Id,
            date = m.Date
        });
    }

    return Results.Ok(new { notifications });
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

// Request/Response classes
record LoginRequest(string Username, string Password);
record RegisterRequest(string Username, string Password, string? Role, string? FullName);
record EmailRequest(int MeasurementId, string To, string? Subject, string? Signature);

class UserInfo
{
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "physicist";
    public string FullName { get; set; } = "";
}
