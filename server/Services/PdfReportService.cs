using MyQC.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Linq;

namespace MyQC.Services;

public class PdfReportService
{
    // Instance field for signature
    private string? _signaturePath;
    
    // Professional color palette - Official/Corporate style
    private static readonly string PrimaryDark = "#1a1a2e";
    private static readonly string PrimaryMid = "#2d2d44";
    private static readonly string AccentGreen = "#2d5016";
    private static readonly string AccentCyan = "#1e3a5f";
    private static readonly string AccentBlue = "#1e3a5f";
    private static readonly string TextPrimary = "#1a1a2e";
    private static readonly string TextSecondary = "#4a4a4a";
    private static readonly string TextMuted = "#6a6a6a";
    private static readonly string BorderLight = "#d0d0d0";
    private static readonly string BackgroundLight = "#f5f5f5";
    private static readonly string SuccessGreen = "#2d5016";
    private static readonly string ErrorRed = "#8b0000";
    private static readonly string WarningAmber = "#8b6914";

    public byte[] Build(TRSMeasurement m, string? signaturePath = null)
    {
        var reportId = m.Id > 0 ? $"TRS398-{m.Id:0000}" : $"TRS398-{DateTime.Now:yyyyMMddHHmm}";
        var protocol = "TRS-398";
        var energy = string.IsNullOrWhiteSpace(m.Energy) ? "-" : m.Energy;
        var beamType = (energy.Contains("e", StringComparison.OrdinalIgnoreCase)) ? "Electron" : "Photon";
        var tolerance = 2.0;
        var deviation = m.Ecart;
        var passFail = Math.Abs(deviation) <= tolerance ? "PASS" : "FAIL";
        var passFailColor = passFail == "PASS" ? SuccessGreen : ErrorRed;
        
        // Store signature path for use in signature section
        _signaturePath = signaturePath;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(0);
                page.DefaultTextStyle(TextStyle.Default.FontSize(9).FontColor(TextPrimary));

                page.Header().Element(e => DrawHeader(e, m, reportId, protocol, energy, beamType));

                page.Content().Padding(25).Column(col =>
                {
                    col.Spacing(12);

                    // Main Content Grid - Equipment & Conditions First
                    col.Item().Row(row =>
                    {
                        row.Spacing(10);
                        
                        // Left Column
                        row.RelativeItem().Column(leftCol =>
                        {
                            leftCol.Spacing(10);
                            leftCol.Item().Element(e => ModernSection(e, "Machine & Beam", AccentBlue, new[] {
                                ("LINAC Model", m.LinacName),
                                ("Serial Number", m.LinacId),
                                ("Beam Type", beamType),
                                ("Energy", energy),
                                ("Field Size", string.IsNullOrWhiteSpace(m.FieldSize) ? "10×10 cm²" : m.FieldSize)
                            }));
                            
                            leftCol.Item().Element(e => ModernSection(e, "Dosimetry System", AccentBlue, new[] {
                                ("Ion Chamber", m.Chamber),
                                ("Nd,w (Gy/nC)", m.Ndw.ToString("0.00000")),
                                ("TPR₂₀,₁₀", m.TPR2010 == 0 ? "-" : m.TPR2010.ToString("0.000")),
                                ("kQ,Q₀", m.kQ.ToString("0.0000"))
                            }));
                        });

                        // Right Column
                        row.RelativeItem().Column(rightCol =>
                        {
                            rightCol.Spacing(10);
                            rightCol.Item().Element(e => ModernSection(e, "Reference Conditions", AccentBlue, new[] {
                                ("SSD/SAD", "100 cm"),
                                ("Phantom", "Water"),
                                ("Reference Depth", "10 cm (zref)"),
                                ("Protocol", protocol)
                            }));
                            
                            rightCol.Item().Element(e => ModernSection(e, "Environment", AccentBlue, new[] {
                                ("Temperature", $"{m.T:0.0} °C"),
                                ("Pressure", $"{m.P:0.0} mbar"),
                                ("kTP", m.Ktp.ToString("0.0000"))
                            }));
                        });
                    });

                    // Readings Table
                    col.Item().Element(e => ReadingsSection(e, m));

                    // Corrections Section
                    col.Item().Element(e => CorrectionsSection(e, m));

                    // Final Results Section - Summary Cards at the END
                    col.Item().Row(row =>
                    {
                        row.Spacing(10);
                        row.RelativeItem().Element(e => SummaryCard(e, "📊", "Dose Result", 
                            $"{m.DW_Zref:0.0000} Gy", "Dw,Q at zref", AccentBlue));
                        row.RelativeItem().Element(e => SummaryCard(e, "📐", "Deviation", 
                            $"{deviation:+0.00;-0.00}%", $"Tolerance: ±{tolerance}%", 
                            Math.Abs(deviation) <= tolerance ? SuccessGreen : ErrorRed));
                        row.RelativeItem().Element(e => SummaryCard(e, "✓", "Status", 
                            passFail, passFail == "PASS" ? "Within tolerance" : "Out of tolerance", passFailColor));
                        row.RelativeItem().Element(e => SummaryCard(e, "🔬", "kQ Factor", 
                            $"{m.kQ:0.0000}", "Beam quality correction", AccentBlue));
                    });

                    // Notes & Signatures
                    col.Item().Element(e => SignatureSection(e, m.Notes, m.UserName, _signaturePath));
                });

                page.Footer().Element(e => DrawFooter(e, reportId, protocol));
            });
        }).GeneratePdf();
    }

    private static void DrawHeader(IContainer container, TRSMeasurement m, string reportId, string protocol, string energy, string beamType)
    {
        container.Background(PrimaryDark).Padding(25).Row(row =>
        {
            // Hospital Logo - Always try to show
            var logoPath = GetLogoPath(m.ClinicName ?? "");
            var hasLogo = !string.IsNullOrWhiteSpace(logoPath) && File.Exists(logoPath);
            
            if (hasLogo && !string.IsNullOrWhiteSpace(logoPath))
            {
                row.ConstantItem(80).AlignMiddle().Element(e =>
                {
                    try 
                    { 
                        e.Image(logoPath!).FitArea(); 
                    } 
                    catch 
                    { 
                        // If image fails to load, show placeholder
                        e.Width(80).Height(80).Background("#2d2d44").AlignCenter().Text("LOGO").FontSize(8).FontColor("#6a6a6a");
                    }
                });
                row.ConstantItem(20);
            }
            else
            {
                // Reserve space for logo area even if no logo
                row.ConstantItem(0);
            }

            // Clinic Information - Always show, use defaults if missing
            row.RelativeItem().Column(col =>
            {
                // Clinic Name - Large and prominent
                var clinicName = !string.IsNullOrWhiteSpace(m.ClinicName) 
                    ? m.ClinicName 
                    : "Medical Facility";
                col.Item().Text(clinicName).FontSize(20).Bold().FontColor(Colors.White);
                
                // Clinic Address
                if (!string.IsNullOrWhiteSpace(m.ClinicAddress))
                {
                    col.Item().Height(4);
                    col.Item().Text(m.ClinicAddress).FontSize(10).FontColor("#e0e0e0");
                }
                
                // Department/Division - Always show
                col.Item().Height(8);
                col.Item().Text("Medical Physics Department").FontSize(9).FontColor("#c0c0c0");
            });

            // Report ID and Date - Right side
            row.ConstantItem(150).AlignRight().Column(col =>
            {
                col.Item().Text("Calibration Report").FontSize(12).Bold().FontColor(Colors.White);
                col.Item().Height(6);
                col.Item().Text($"Report ID: {reportId}").FontSize(9).FontColor("#e0e0e0");
                col.Item().Text($"Date: {m.Date:yyyy-MM-dd HH:mm}").FontSize(9).FontColor("#e0e0e0");
            });
        });
    }

    private static void Badge(IContainer container, string text, string color)
    {
        container.Background(color)
            .Padding(4)
            .PaddingHorizontal(10)
            .Text(text)
            .FontSize(9)
            .Bold()
            .FontColor(Colors.White);
    }

    private static void SummaryCard(IContainer container, string icon, string label, string value, string subtitle, string accentColor)
    {
        container.Border(1)
            .BorderColor(BorderLight)
            .Background(Colors.White)
            .Column(col =>
            {
                // Accent top bar
                col.Item().Height(4).Background(accentColor);
                
                col.Item().Padding(12).Column(inner =>
                {
                    inner.Item().Row(r =>
                    {
                        r.AutoItem().Text(icon).FontSize(16);
                        r.ConstantItem(6);
                        r.RelativeItem().Text(label).FontSize(9).FontColor(TextSecondary);
                    });
                    inner.Item().Height(4);
                    inner.Item().Text(value).FontSize(16).Bold().FontColor(accentColor);
                    inner.Item().Text(subtitle).FontSize(8).FontColor(TextMuted);
                });
            });
    }

    private static void ModernSection(IContainer container, string title, string accentColor, (string label, string value)[] rows)
    {
        container.Border(1)
            .BorderColor(BorderLight)
            .Background(Colors.White)
            .Column(col =>
            {
                // Header with accent
                col.Item().Background(BackgroundLight).BorderBottom(1).BorderColor(BorderLight).Padding(10).Row(row =>
                {
                    row.ConstantItem(4).Background(accentColor);
                    row.ConstantItem(8);
                    row.RelativeItem().Text(title).FontSize(11).SemiBold().FontColor(TextPrimary);
                });

                // Content
                col.Item().Padding(10).Column(inner =>
                {
                    foreach (var (label, value) in rows)
                    {
                        inner.Item().PaddingVertical(4).Row(r =>
                        {
                            r.RelativeItem().Text(label).FontSize(9).FontColor(TextSecondary);
                            r.RelativeItem().AlignRight().Text(string.IsNullOrWhiteSpace(value) ? "-" : value)
                                .FontSize(9).SemiBold().FontColor(TextPrimary);
                        });
                    }
                });
            });
    }

    private static void ReadingsSection(IContainer container, TRSMeasurement m)
    {
        container.Border(1).BorderColor(BorderLight).Background(Colors.White).Column(col =>
        {
            // Header
            col.Item().Background(BackgroundLight).BorderBottom(1).BorderColor(BorderLight).Padding(10).Row(row =>
            {
                row.ConstantItem(4).Background(AccentBlue);
                row.ConstantItem(8);
                row.RelativeItem().Text("Electrometer Readings").FontSize(11).SemiBold().FontColor(TextPrimary);
            });

            // Table
            col.Item().Padding(10).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1.2f);
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn(1.2f);
                });

                // Header row
                table.Header(header =>
                {
                    header.Cell().Background(PrimaryDark).Padding(8).Text("Polarity").FontSize(9).Bold().FontColor(Colors.White);
                    header.Cell().Background(PrimaryDark).Padding(8).AlignCenter().Text("R₁").FontSize(9).Bold().FontColor(Colors.White);
                    header.Cell().Background(PrimaryDark).Padding(8).AlignCenter().Text("R₂").FontSize(9).Bold().FontColor(Colors.White);
                    header.Cell().Background(PrimaryDark).Padding(8).AlignCenter().Text("R₃").FontSize(9).Bold().FontColor(Colors.White);
                    header.Cell().Background(AccentBlue).Padding(8).AlignCenter().Text("Mean").FontSize(9).Bold().FontColor(Colors.White);
                });

                // M+ row
                table.Cell().Background(BackgroundLight).Padding(8).Text("M+ (+300V)").FontSize(9).SemiBold();
                table.Cell().Padding(8).AlignCenter().Text(m.M_plus_1.ToString("0.000")).FontSize(9);
                table.Cell().Padding(8).AlignCenter().Text(m.M_plus_2.ToString("0.000")).FontSize(9);
                table.Cell().Padding(8).AlignCenter().Text(m.M_plus_3.ToString("0.000")).FontSize(9);
                table.Cell().Background("#ecfdf5").Padding(8).AlignCenter().Text(m.Mean_plus.ToString("0.000")).FontSize(9).Bold().FontColor(SuccessGreen);

                // M- row
                table.Cell().Background(BackgroundLight).Padding(8).Text("M− (−300V)").FontSize(9).SemiBold();
                table.Cell().Padding(8).AlignCenter().Text(m.M_minus_1.ToString("0.000")).FontSize(9);
                table.Cell().Padding(8).AlignCenter().Text(m.M_minus_2.ToString("0.000")).FontSize(9);
                table.Cell().Padding(8).AlignCenter().Text(m.M_minus_3.ToString("0.000")).FontSize(9);
                table.Cell().Background("#ecfdf5").Padding(8).AlignCenter().Text(m.Mean_minus.ToString("0.000")).FontSize(9).Bold().FontColor(SuccessGreen);

                // M100V row
                table.Cell().Background(BackgroundLight).Padding(8).Text("M₁₀₀V (100V)").FontSize(9).SemiBold();
                table.Cell().Padding(8).AlignCenter().Text(m.M100V_1.ToString("0.000")).FontSize(9);
                table.Cell().Padding(8).AlignCenter().Text(m.M100V_2.ToString("0.000")).FontSize(9);
                table.Cell().Padding(8).AlignCenter().Text(m.M100V_3.ToString("0.000")).FontSize(9);
                table.Cell().Background("#ecfdf5").Padding(8).AlignCenter().Text(m.Mean100V.ToString("0.000")).FontSize(9).Bold().FontColor(SuccessGreen);
            });
        });
    }

    private static void CorrectionsSection(IContainer container, TRSMeasurement m)
    {
        container.Border(1).BorderColor(BorderLight).Background(Colors.White).Column(col =>
        {
            col.Item().Background(BackgroundLight).BorderBottom(1).BorderColor(BorderLight).Padding(10).Row(row =>
            {
                row.ConstantItem(4).Background(AccentBlue);
                row.ConstantItem(8);
                row.RelativeItem().Text("Correction Factors").FontSize(11).SemiBold().FontColor(TextPrimary);
            });

            col.Item().Padding(12).Column(inner =>
            {
                CorrectionRow(inner, "kTP", m.Ktp.ToString("0.0000"), "Temperature-pressure correction");
                CorrectionRow(inner, "kpol", m.Kpol.ToString("0.0000"), "Polarity correction");
                CorrectionRow(inner, "ks", m.Ks.ToString("0.0000"), "Ion recombination correction");
                CorrectionRow(inner, "kQ,Q₀", m.kQ.ToString("0.0000"), "Beam quality correction", true);
                
                inner.Item().Height(8);
                inner.Item().BorderTop(1).BorderColor(BorderLight).PaddingTop(8).Row(r =>
                {
                    r.RelativeItem().Text("Corrected M").FontSize(10).SemiBold().FontColor(TextPrimary);
                    r.AutoItem().Text(m.M_corr.ToString("0.0000")).FontSize(12).Bold().FontColor(AccentBlue);
                });
            });
        });
    }

    private static void CorrectionRow(ColumnDescriptor col, string label, string value, string description, bool highlight = false)
    {
        col.Item().PaddingVertical(4).Row(r =>
        {
            r.ConstantItem(50).Text(label).FontSize(10).SemiBold().FontColor(highlight ? AccentBlue : TextPrimary);
            r.ConstantItem(8);
            r.RelativeItem().Text(description).FontSize(8).FontColor(TextMuted);
            r.AutoItem().Text(value).FontSize(10).SemiBold().FontColor(highlight ? AccentBlue : TextPrimary);
        });
    }

    private static void FinalResultSection(IContainer container, TRSMeasurement m, double tolerance, string passFail, double deviation, string passFailColor)
    {
        container.Border(1).BorderColor(BorderLight).Background(Colors.White).Column(col =>
        {
            col.Item().Background(BackgroundLight).BorderBottom(1).BorderColor(BorderLight).Padding(10).Row(row =>
            {
                row.ConstantItem(4).Background(passFailColor);
                row.ConstantItem(8);
                row.RelativeItem().Text("Final Result").FontSize(11).SemiBold().FontColor(TextPrimary);
            });

            col.Item().Padding(12).Column(inner =>
            {
                // Big result display
                inner.Item().AlignCenter().Column(center =>
                {
                    center.Item().Text("Absorbed Dose to Water").FontSize(9).FontColor(TextSecondary);
                    center.Item().Height(4);
                    center.Item().Text($"{m.DW_Zref:0.0000}").FontSize(28).Bold().FontColor(AccentBlue);
                    center.Item().Text("Gy at zref").FontSize(10).FontColor(TextSecondary);
                });

                inner.Item().Height(12);

                // Pass/Fail badge
                inner.Item().AlignCenter().Element(e =>
                {
                    e.Background(passFailColor)
                        .Padding(8)
                        .PaddingHorizontal(24)
                        .Text(passFail)
                        .FontSize(14)
                        .Bold()
                        .FontColor(Colors.White);
                });

                inner.Item().Height(8);

                // Deviation info
                inner.Item().AlignCenter().Text($"Deviation: {deviation:+0.00;-0.00}% (Tolerance: ±{tolerance}%)")
                    .FontSize(9).FontColor(TextSecondary);
            });
        });
    }

    private static void SignatureSection(IContainer container, string notes, string measuredBy, string? signaturePath)
    {
        container.Border(1).BorderColor(BorderLight).Background(Colors.White).Column(col =>
        {
            col.Item().Background(BackgroundLight).BorderBottom(1).BorderColor(BorderLight).Padding(10).Row(row =>
            {
                row.ConstantItem(4).Background(TextSecondary);
                row.ConstantItem(8);
                row.RelativeItem().Text("Comments & Authorization").FontSize(11).SemiBold().FontColor(TextPrimary);
            });

            col.Item().Padding(12).Column(inner =>
            {
                // Notes
                if (!string.IsNullOrWhiteSpace(notes))
                {
                    inner.Item().Background(BackgroundLight).Border(1).BorderColor(BorderLight).Padding(10)
                        .Text(notes).FontSize(9).FontColor(TextPrimary);
                    inner.Item().Height(12);
                }

                // Signatures table
                inner.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn();
                        columns.RelativeColumn(1.5f);
                        columns.RelativeColumn(1.5f);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Background(PrimaryMid).Padding(8).Text("Role").FontSize(9).Bold().FontColor(Colors.White);
                        header.Cell().Background(PrimaryMid).Padding(8).Text("Name").FontSize(9).Bold().FontColor(Colors.White);
                        header.Cell().Background(PrimaryMid).Padding(8).Text("Date / Signature").FontSize(9).Bold().FontColor(Colors.White);
                    });

                    // Measured by - with digital signature if available
                    table.Cell().Background(BackgroundLight).Padding(8).Text("Measured by").FontSize(9);
                    table.Cell().Padding(8).Text(string.IsNullOrWhiteSpace(measuredBy) ? "-" : measuredBy).FontSize(9);
                    
                    // Check for digital signature
                    var sigPath = GetSignaturePath(signaturePath, measuredBy);
                    if (!string.IsNullOrWhiteSpace(sigPath) && File.Exists(sigPath))
                    {
                        table.Cell().Padding(4).Row(sigRow =>
                        {
                            sigRow.AutoItem().Text($"{DateTime.Now:yyyy-MM-dd}").FontSize(8);
                            sigRow.ConstantItem(8);
                            sigRow.ConstantItem(80).Height(30).Element(e => {
                                try { e.Image(sigPath).FitArea(); } catch { }
                            });
                        });
                    }
                    else
                    {
                        table.Cell().Padding(8).Text($"{DateTime.Now:yyyy-MM-dd}  _______________").FontSize(9);
                    }

                    // Reviewed by
                    table.Cell().Background(BackgroundLight).Padding(8).Text("Reviewed by").FontSize(9);
                    table.Cell().Padding(8).Text("-").FontSize(9);
                    table.Cell().Padding(8).Text("_______________").FontSize(9);

                    // Approved by
                    table.Cell().Background(BackgroundLight).Padding(8).Text("Approved by").FontSize(9);
                    table.Cell().Padding(8).Text("-").FontSize(9);
                    table.Cell().Padding(8).Text("_______________").FontSize(9);
                });
            });
        });
    }
    
    private static string? GetSignaturePath(string? providedPath, string? userName)
    {
        // First check provided path
        if (!string.IsNullOrWhiteSpace(providedPath))
        {
            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", providedPath.TrimStart('/'));
            if (File.Exists(fullPath)) return fullPath;
        }
        
        // Then check signatures directory for user
        var signaturesDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "signatures");
        if (Directory.Exists(signaturesDir) && !string.IsNullOrWhiteSpace(userName))
        {
            var userSig = Path.Combine(signaturesDir, $"{userName.Replace(" ", "_")}_signature.png");
            if (File.Exists(userSig)) return userSig;
            
            userSig = Path.Combine(signaturesDir, $"{userName.Replace(" ", "_")}_signature.jpg");
            if (File.Exists(userSig)) return userSig;
        }
        
        return null;
    }

    private static void DrawFooter(IContainer container, string reportId, string protocol)
    {
        container.Background(BackgroundLight).BorderTop(1).BorderColor(BorderLight).Padding(15).Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text($"Report: {reportId}").FontSize(8).FontColor(TextMuted);
                col.Item().Text($"Protocol: {protocol}").FontSize(8).FontColor(TextMuted);
            });
            
            row.RelativeItem().AlignCenter().Column(col =>
            {
                col.Item().Text("Generated by TRS-398").FontSize(8).FontColor(TextSecondary);
                col.Item().Text("Medical Physics Calibration System").FontSize(7).FontColor(TextMuted);
            });
            
            row.RelativeItem().AlignRight().Column(col =>
            {
                col.Item().Text($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}").FontSize(8).FontColor(TextMuted);
                col.Item().Text("Page 1 of 1").FontSize(8).FontColor(TextMuted);
            });
        });
    }

    private static string? GetLogoPath(string clinicName)
    {
        // Try multiple possible paths for logos directory
        var possiblePaths = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "logos"),
            Path.Combine(AppContext.BaseDirectory, "wwwroot", "logos"),
            Path.Combine(Environment.CurrentDirectory, "wwwroot", "logos")
        };
        
        string? logosDir = null;
        foreach (var path in possiblePaths)
        {
            if (Directory.Exists(path))
            {
                logosDir = path;
                break;
            }
        }
        
        if (logosDir == null) return null;
        
        // First try clinic-specific logo
        if (!string.IsNullOrWhiteSpace(clinicName) && !clinicName.Equals("Medical Facility", StringComparison.OrdinalIgnoreCase))
        {
            var sanitizedName = clinicName.Replace(" ", "_").Replace("/", "_").Replace("\\", "_");
            var clinicLogo = Path.Combine(logosDir, $"{sanitizedName}.png");
            if (File.Exists(clinicLogo)) return clinicLogo;
            
            clinicLogo = Path.Combine(logosDir, $"{sanitizedName}.jpg");
            if (File.Exists(clinicLogo)) return clinicLogo;
            
            clinicLogo = Path.Combine(logosDir, $"{sanitizedName}.jpeg");
            if (File.Exists(clinicLogo)) return clinicLogo;
        }
        
        // Try default logo files (common names)
        var defaultNames = new[] { "logo.png", "logo.jpg", "logo.jpeg", "hospital.png", "hospital.jpg", "clinic.png", "clinic.jpg" };
        foreach (var name in defaultNames)
        {
            var logoPath = Path.Combine(logosDir, name);
            if (File.Exists(logoPath)) return logoPath;
        }
        
        // Try any image file in logos directory
        try
        {
            var imageFiles = Directory.GetFiles(logosDir)
                .Where(f => new[] { ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp" }
                    .Contains(Path.GetExtension(f).ToLowerInvariant(), StringComparer.OrdinalIgnoreCase))
                .OrderBy(f => Path.GetFileName(f))
                .FirstOrDefault();
            
            if (imageFiles != null) return imageFiles;
        }
        catch { }
        
        return null;
    }

    public byte[] BuildSummaryReport(List<TRSMeasurement> measurements, string? clinicName = null)
    {
        var reportDate = DateTime.Now;
        var totalMeasurements = measurements.Count;
        var passCount = measurements.Count(m => Math.Abs(m.Ecart) <= 2.0);
        var failCount = totalMeasurements - passCount;
        var passRate = totalMeasurements > 0 ? (passCount * 100.0 / totalMeasurements) : 0;
        var avgDeviation = measurements.Count > 0 ? measurements.Average(m => Math.Abs(m.Ecart)) : 0;

        var logoPath = GetLogoPath(clinicName ?? measurements.FirstOrDefault()?.ClinicName ?? "");

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(0);
                page.DefaultTextStyle(TextStyle.Default.FontSize(9).FontColor(TextPrimary));

                page.Header().Element(e => DrawSummaryHeader(e, logoPath, clinicName ?? measurements.FirstOrDefault()?.ClinicName ?? "Medical Physics Department", reportDate));

                page.Content().Padding(25).Column(col =>
                {
                    col.Spacing(15);

                    // Summary Statistics
                    col.Item().Element(e => DrawSummaryStats(e, totalMeasurements, passCount, failCount, passRate, avgDeviation));

                    // Measurements Table
                    col.Item().Element(e => DrawMeasurementsTable(e, measurements));

                    // Footer
                    col.Item().Element(e => DrawSummaryFooter(e, reportDate));
                });

                page.Footer().Element(e => DrawSummaryPageFooter(e));
            });
        }).GeneratePdf();
    }

    private static void DrawSummaryHeader(IContainer container, string? logoPath, string clinicName, DateTime reportDate)
    {
        container.Background(PrimaryDark).Padding(25).Row(row =>
        {
            // Hospital Logo - Larger and more prominent
            if (logoPath != null && File.Exists(logoPath))
            {
                row.ConstantItem(80).AlignMiddle().Element(e =>
                {
                    try { e.Image(logoPath).FitArea(); } catch { }
                });
                row.ConstantItem(20);
            }

            // Clinic Information - Main focus
            row.RelativeItem().Column(col =>
            {
                // Clinic Name - Large and prominent
                col.Item().Text(clinicName).FontSize(20).Bold().FontColor(Colors.White);
                
                // Department
                col.Item().Height(8);
                col.Item().Text("Medical Physics Department").FontSize(9).FontColor("#c0c0c0");
                
                // Report Type
                col.Item().Height(6);
                col.Item().Text("Calibration Summary Report").FontSize(11).FontColor("#e0e0e0");
            });

            // Report Date - Right side
            row.ConstantItem(150).AlignRight().Column(col =>
            {
                col.Item().Text("Summary Report").FontSize(12).Bold().FontColor(Colors.White);
                col.Item().Height(6);
                col.Item().Text($"Generated: {reportDate:yyyy-MM-dd HH:mm}").FontSize(9).FontColor("#e0e0e0");
            });
        });
    }

    private static void DrawSummaryStats(IContainer container, int total, int pass, int fail, double passRate, double avgDeviation)
    {
        container.Background(BackgroundLight).Border(1).BorderColor(BorderLight).Padding(15).Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text("Total Measurements").FontSize(10).FontColor(TextSecondary);
                col.Item().Text(total.ToString()).FontSize(24).Bold().FontColor(AccentBlue);
            });

            row.RelativeItem().Column(col =>
            {
                col.Item().Text("Pass").FontSize(10).FontColor(TextSecondary);
                col.Item().Text(pass.ToString()).FontSize(24).Bold().FontColor(SuccessGreen);
            });

            row.RelativeItem().Column(col =>
            {
                col.Item().Text("Fail").FontSize(10).FontColor(TextSecondary);
                col.Item().Text(fail.ToString()).FontSize(24).Bold().FontColor(ErrorRed);
            });

            row.RelativeItem().Column(col =>
            {
                col.Item().Text("Pass Rate").FontSize(10).FontColor(TextSecondary);
                col.Item().Text($"{passRate:F1}%").FontSize(24).Bold().FontColor(AccentBlue);
            });

            row.RelativeItem().Column(col =>
            {
                col.Item().Text("Avg Deviation").FontSize(10).FontColor(TextSecondary);
                col.Item().Text($"{avgDeviation:F2}%").FontSize(24).Bold().FontColor(TextPrimary);
            });
        });
    }

    private static void DrawMeasurementsTable(IContainer container, List<TRSMeasurement> measurements)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(70);  // Date
                columns.RelativeColumn();     // User
                columns.ConstantColumn(60);   // Mode
                columns.ConstantColumn(50);   // Energy
                columns.RelativeColumn();     // Machine
                columns.ConstantColumn(50);   // Status
                columns.ConstantColumn(60);   // Deviation
            });

            table.Header(header =>
            {
                header.Cell().Element(CellStyle).Text("Date").FontSize(9).Bold();
                header.Cell().Element(CellStyle).Text("User").FontSize(9).Bold();
                header.Cell().Element(CellStyle).Text("Mode").FontSize(9).Bold();
                header.Cell().Element(CellStyle).Text("Energy").FontSize(9).Bold();
                header.Cell().Element(CellStyle).Text("Machine").FontSize(9).Bold();
                header.Cell().Element(CellStyle).Text("Status").FontSize(9).Bold();
                header.Cell().Element(CellStyle).Text("Deviation").FontSize(9).Bold();
            });

            foreach (var m in measurements.OrderByDescending(x => x.Date))
            {
                var isPass = Math.Abs(m.Ecart) <= 2.0;
                var statusColor = isPass ? SuccessGreen : ErrorRed;
                var statusText = isPass ? "PASS" : "FAIL";
                var modeText = m.Mode?.Contains("electron", StringComparison.OrdinalIgnoreCase) == true ? "E" : "P";

                table.Cell().Element(CellStyle).Text(m.Date.ToString("MM/dd/yy")).FontSize(8);
                table.Cell().Element(CellStyle).Text(m.UserName ?? "-").FontSize(8);
                table.Cell().Element(CellStyle).Text(modeText).FontSize(8);
                table.Cell().Element(CellStyle).Text(m.Energy ?? "-").FontSize(8);
                table.Cell().Element(CellStyle).Text(m.LinacName ?? "-").FontSize(8);
                table.Cell().Element(CellStyle).Text(statusText).FontSize(8).FontColor(statusColor).Bold();
                table.Cell().Element(CellStyle).Text($"{m.Ecart:F2}%").FontSize(8).FontColor(statusColor);
            }
        });
    }

    private static void DrawSummaryFooter(IContainer container, DateTime reportDate)
    {
        container.Background(BackgroundLight).Border(1).BorderColor(BorderLight).Padding(10).Column(col =>
        {
            col.Item().Text("Report Summary").FontSize(10).Bold().FontColor(TextPrimary);
            col.Item().Text($"This report contains all calibration measurements performed using the TRS-398 protocol.").FontSize(8).FontColor(TextSecondary);
            col.Item().Text($"Generated on {reportDate:yyyy-MM-dd HH:mm} by TRS-398 Calibration System").FontSize(8).FontColor(TextMuted);
        });
    }

    private static void DrawSummaryPageFooter(IContainer container)
    {
        container.Background(PrimaryMid).Padding(10).Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text("TRS-398 - Medical Physics Calibration System").FontSize(8).FontColor(Colors.White);
            });
            
            row.ConstantItem(100).AlignRight().Column(col =>
            {
                col.Item().Text($"Page 1").FontSize(8).FontColor("#cbd5e1");
            });
        });
    }

    private static IContainer CellStyle(IContainer container)
    {
        return container.Border(0.5f).BorderColor(BorderLight).Padding(5).Background(Colors.White);
    }
}
