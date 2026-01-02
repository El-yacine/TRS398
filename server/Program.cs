using Microsoft.EntityFrameworkCore;
using MyQC.Data;
using MyQC.Services;
using MyQC.Models;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<MyQCDbContext>(options =>
    options.UseSqlite("Data Source=trs398.db"));

builder.Services.AddScoped<TRSService>();
builder.Services.AddScoped<PdfReportService>();

var app = builder.Build();
var env = app.Environment;

QuestPDF.Settings.License = LicenseType.Community;

app.MapGet("/", () => Results.Redirect("/index.html"));
app.UseStaticFiles();

// API endpoints
app.MapGet("/api/health", () => Results.Ok(new { status = "ok", app = "TRS-398 Clean", time = DateTime.Now }));

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

app.MapGet("/api/trs/export", async (MyQCDbContext db) =>
{
    var measurements = await db.TRSMeasurements.OrderByDescending(m => m.Date).ToListAsync();
    var sb = new StringBuilder();
    sb.AppendLine("Date,User,Mode,Clinic,Energy,LINAC Name,LINAC ID,Field Size,Chamber,ChamberType,TPR20/10,R50,Zref,SSD,T,P,kQUsed,NdUsed,Ndw,M+1,M+2,M+3,Mean+,M-1,M-2,M-3,Mean-,M100V1,M100V2,M100V3,Mean100V,Ktp,Kpol,Ks,M_corr,DW_Zref,Ecart,Notes");
    foreach (var m in measurements)
    {
        sb.AppendLine($"{m.Date:yyyy-MM-dd HH:mm},{m.UserName},{m.Mode},{m.ClinicName},{m.Energy},{m.LinacName},{m.LinacId},{m.FieldSize},{m.Chamber},{m.ChamberType},{m.TPR2010},{m.R50},{m.Zref},{m.SSD},{m.T},{m.P},{m.kQUsed},{m.NdUsed},{m.Ndw},{m.M_plus_1},{m.M_plus_2},{m.M_plus_3},{m.Mean_plus},{m.M_minus_1},{m.M_minus_2},{m.M_minus_3},{m.Mean_minus},{m.M100V_1},{m.M100V_2},{m.M100V_3},{m.Mean100V},{m.Ktp},{m.Kpol},{m.Ks},{m.M_corr},{m.DW_Zref},{m.Ecart},\"{m.Notes}\"");
    }
    var bytes = Encoding.UTF8.GetBytes(sb.ToString());
    return Results.File(bytes, "text/csv", "trs398_export.csv");
});

app.MapGet("/api/trs/report/{id}", async (int id, MyQCDbContext db, PdfReportService pdfService) =>
{
    var measurement = await db.TRSMeasurements.FindAsync(id);
    if (measurement is null) return Results.NotFound();
    var bytes = pdfService.Build(measurement);
    var filename = $"trs398_report_{id}.pdf";
    return Results.File(bytes, "application/pdf", filename);
});

app.MapGet("/api/detectors", async () =>
{
    var path = Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", "detector_library.json"));
    if (!File.Exists(path)) return Results.NotFound();
    var json = await File.ReadAllTextAsync(path);
    return Results.Content(json, "application/json");
});

// Create database
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MyQCDbContext>();
    db.Database.EnsureCreated();
    EnsureSchema(db);
}

app.Run();

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
        ("kQUsed", "REAL")
    };

    foreach (var column in columns)
    {
        if (!existing.Contains(column.Name))
        {
            db.Database.ExecuteSqlRaw($"ALTER TABLE TRSMeasurements ADD COLUMN {column.Name} {column.Sql}");
        }
    }
}
