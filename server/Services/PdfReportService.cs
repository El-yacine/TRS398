using MyQC.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MyQC.Services;

public class PdfReportService
{
    public byte[] Build(TRSMeasurement m)
    {
        var accent = Colors.Blue.Medium;
        var accent2 = Colors.Green.Medium;
        var ink = Colors.Grey.Darken3;
        var muted = Colors.Grey.Darken1;
        var headerBg = Colors.Grey.Lighten3;
        var reportId = m.Id > 0 ? $"TRS398-{m.Id:0000}" : $"TRS398-{DateTime.Now:yyyyMMddHHmm}";
        var protocol = "IAEA TRS-398";
        var energy = string.IsNullOrWhiteSpace(m.Energy) ? "-" : m.Energy;
        var beamType = (energy.Contains("e", StringComparison.OrdinalIgnoreCase)) ? "Electron" : "Photon";
        var tolerance = 2.0;
        var deviation = m.Ecart;
        var passFail = Math.Abs(deviation) <= tolerance ? "PASS" : "FAIL";

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(20);
                page.DefaultTextStyle(TextStyle.Default.FontSize(9).FontColor(ink));

                page.Header().ShowOnce().Row(row =>
                {
                    // Logo column (if available)
                    var logoPath = GetLogoPath(m.ClinicName);
                    if (!string.IsNullOrWhiteSpace(logoPath) && File.Exists(logoPath))
                    {
                        row.ConstantItem(80).Element(e =>
                        {
                            try
                            {
                                e.Image(logoPath).FitArea();
                            }
                            catch
                            {
                                // If image fails to load, just skip it
                            }
                        });
                    }
                    else
                    {
                        row.ConstantItem(0);
                    }

                    row.RelativeItem().Column(col =>
                    {
                        var title = string.IsNullOrWhiteSpace(m.ClinicName)
                            ? "TRS-398 Calibration Report"
                            : $"{m.ClinicName} · TRS-398 Calibration";

                        col.Item().Text(title).FontSize(18).SemiBold().FontColor(accent);
                        col.Item().Text($"{protocol} · Report ID: {reportId}").FontColor(muted);
                        col.Item().Text($"Date/Time: {m.Date:yyyy-MM-dd HH:mm}").FontColor(muted);
                        if (!string.IsNullOrWhiteSpace(m.ClinicName))
                        {
                            col.Item().Text($"Clinic: {m.ClinicName}").FontColor(muted);
                        }
                        if (!string.IsNullOrWhiteSpace(m.ClinicAddress))
                        {
                            col.Item().Text($"Department: {m.ClinicAddress}").FontColor(muted);
                        }
                        if (!string.IsNullOrWhiteSpace(m.UserName))
                        {
                            col.Item().Text($"Contact: {m.UserName}").FontColor(muted);
                        }
                    });
                    row.ConstantItem(180).Column(col =>
                    {
                        col.Item().AlignRight().Text("Room: -").FontColor(muted);
                        col.Item().AlignRight().Text($"Protocol: {protocol}").FontColor(muted);
                        col.Item().AlignRight().Text($"Beam: {beamType}").FontColor(muted);
                        col.Item().AlignRight().Text($"Energy: {energy}").FontColor(muted);
                    });
                });

                page.Content().Column(col =>
                {
                    col.Spacing(6);
                    col.Item().BorderBottom(2).BorderColor(accent).PaddingBottom(4);

                    col.Item().Row(row =>
                    {
                        row.Spacing(4);
                        row.RelativeItem().Element(e => SectionTable(e, "Machine & Beam", headerBg, new (string, string)[] {
                            ("LINAC model", m.LinacName),
                            ("Serial", m.LinacId),
                            ("Beam type", beamType),
                            ("Energy", energy),
                            ("Dose rate", "-"),
                            ("Monitor units", "-")
                        }, true));
                        row.RelativeItem().Element(e => SectionTable(e, "Reference Conditions", headerBg, new (string, string)[] {
                            ("SSD/SAD", "100 cm"),
                            ("Field size", string.IsNullOrWhiteSpace(m.FieldSize) ? "10×10 cm²" : m.FieldSize),
                            ("Phantom", "Water"),
                            ("Reference depth", "10 cm (zref)"),
                            ("Beam quality", m.TPR2010 == 0 ? "-" : $"TPR20,10 = {m.TPR2010:0.000}")
                        }, true));
                    });

                    col.Item().Row(row =>
                    {
                        row.Spacing(4);
                        row.RelativeItem().Element(e => SectionTable(e, "Dosimetry System", headerBg, new (string, string)[] {
                            ("Chamber", m.Chamber),
                            ("Type / Volume", "Ion chamber / -"),
                            ("Nd,w,Q0 (Gy/nC)", m.Ndw.ToString("0.00000")),
                            ("Calibration lab/date", "- / -"),
                            ("Electrometer", "- / -"),
                            ("Electrometer factor/date", "- / -")
                        }, true));
                        row.RelativeItem().Element(e => SectionTable(e, "Environment", headerBg, new (string, string)[] {
                            ("Temperature (°C)", m.T.ToString("0.00")),
                            ("Pressure (mbar)", m.P.ToString("0.0")),
                            ("Humidity (%)", "-"),
                            ("Polarity", "+300V / -300V")
                        }, true));
                    });

                    col.Item().Element(e => ReadingsTable(e, headerBg, m));

                    col.Item().Row(row =>
                    {
                        row.Spacing(4);
                        row.RelativeItem().Element(e => SectionTable(e, "Corrections", headerBg, new (string, string)[] {
                            ("kTP", m.Ktp.ToString("0.0000")),
                            ("kpol", m.Kpol.ToString("0.0000")),
                            ("kion (Ks)", m.Ks.ToString("0.0000")),
                            ("kelec", "-"),
                            ("kQ,Q0", m.kQ.ToString("0.000")),
                            ("Corrected M", m.M_corr.ToString("0.0000"))
                        }, true));
                        row.RelativeItem().Element(e => ResultSection(e, m, accent, accent2, muted, tolerance, passFail, deviation));
                    });

                    col.Item().Element(e => SignatureSection(e, headerBg, m.Notes, m.UserName));
                });

                page.Footer().AlignCenter().Text("According to IAEA TRS-398").FontColor(muted);
            });
        }).GeneratePdf();
    }

    private static void SectionTable(IContainer container, string title, string headerBg, (string label, string value)[] rows, bool tight = false)
    {
        container.Border(1).BorderColor(Colors.Grey.Lighten3).Padding(8).Column(col =>
        {
            col.Spacing(tight ? 4 : 6);
            col.Item().Text(title).SemiBold().FontColor(Colors.Blue.Darken2);
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });

                table.Header(header =>
                {
                    header.Cell().Background(headerBg).Padding(tight ? 4 : 6).Text("Item").FontSize(9).SemiBold();
                    header.Cell().Background(headerBg).Padding(tight ? 4 : 6).Text("Value").FontSize(9).SemiBold();
                });

                foreach (var row in rows)
                {
                    table.Cell().Padding(tight ? 4 : 6).Text(row.label);
                    table.Cell().Padding(tight ? 4 : 6).Text(string.IsNullOrWhiteSpace(row.value) ? "-" : row.value);
                }
            });
        });
    }

    private static void ReadingsTable(IContainer container, string headerBg, TRSMeasurement m)
    {
        container.Border(1).BorderColor(Colors.Grey.Lighten3).Padding(8).Column(col =>
        {
            col.Spacing(4);
            col.Item().Text("Environment & Readings").SemiBold().FontColor(Colors.Blue.Darken2);
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });

                table.Header(header =>
                {
                    header.Cell().Background(headerBg).Padding(4).Text("Set").FontSize(9).SemiBold();
                    header.Cell().Background(headerBg).Padding(4).Text("R1").FontSize(9).SemiBold();
                    header.Cell().Background(headerBg).Padding(4).Text("R2").FontSize(9).SemiBold();
                    header.Cell().Background(headerBg).Padding(4).Text("R3").FontSize(9).SemiBold();
                });

                table.Cell().Padding(4).Text("M+");
                table.Cell().Padding(4).Text(m.M_plus_1.ToString("0.000"));
                table.Cell().Padding(4).Text(m.M_plus_2.ToString("0.000"));
                table.Cell().Padding(4).Text(m.M_plus_3.ToString("0.000"));

                table.Cell().Padding(4).Text("M-");
                table.Cell().Padding(4).Text(m.M_minus_1.ToString("0.000"));
                table.Cell().Padding(4).Text(m.M_minus_2.ToString("0.000"));
                table.Cell().Padding(4).Text(m.M_minus_3.ToString("0.000"));

                table.Cell().Padding(4).Text("M100V");
                table.Cell().Padding(4).Text(m.M100V_1.ToString("0.000"));
                table.Cell().Padding(4).Text(m.M100V_2.ToString("0.000"));
                table.Cell().Padding(4).Text(m.M100V_3.ToString("0.000"));
            });

            col.Item().Row(r =>
            {
                r.Spacing(4);
                r.RelativeItem().Text($"Mean M+: {m.Mean_plus:0.000}").FontColor(Colors.Green.Darken2);
                r.RelativeItem().Text($"Mean M-: {m.Mean_minus:0.000}").FontColor(Colors.Green.Darken2);
                r.RelativeItem().Text($"Mean 100V: {m.Mean100V:0.000}").FontColor(Colors.Green.Darken2);
            });
        });
    }

    private static void ResultSection(IContainer container, TRSMeasurement m, string accent, string accent2, string muted, double tolerance, string passFail, double deviation)
    {
        container.Border(1).BorderColor(Colors.Grey.Lighten3).Padding(10).Column(col =>
        {
            col.Spacing(4);
            col.Item().Text("Dose Result").SemiBold().FontColor(Colors.Blue.Darken2);

            col.Item().Row(r =>
            {
                r.Spacing(4);
                r.RelativeItem().Element(e => StatBox(e, "Dw,Q", m.DW_Zref.ToString("0.0000"), accent));
                r.RelativeItem().Element(e => StatBox(e, "Corrected M", m.M_corr.ToString("0.0000"), accent2));
                r.RelativeItem().Element(e => StatBox(e, "Dose / MU", "- Gy/MU", Colors.Grey.Darken1));
            });

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });

                table.Header(header =>
                {
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(6).Text("Baseline / Target").FontSize(10).SemiBold();
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(6).Text("Measured").FontSize(10).SemiBold();
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(6).Text("Assessment").FontSize(10).SemiBold();
                });

                table.Cell().Padding(6).Text("Dw,Q baseline: 1.0000");
                table.Cell().Padding(6).Text($"Dw,Q: {m.DW_Zref:0.0000}");
                table.Cell().Padding(6).Text($"Deviation: {deviation:0.00}%");

                table.Cell().Padding(6).Text($"Tolerance: ±{tolerance:0.0}%");
                table.Cell().Padding(6).Text("Dose/MU: -");
                table.Cell().Padding(6).Text($"Result: {passFail}").FontColor(passFail == "PASS" ? Colors.Green.Darken2 : Colors.Red.Medium).SemiBold();
            });
        });
    }

    private static void StatBox(IContainer container, string label, string value, string color)
    {
        container.Border(1)
            .BorderColor(color)
            .Background(Colors.White)
            .Padding(8)
            .Column(col =>
            {
                col.Spacing(2);
                col.Item().Text(label).FontSize(9).FontColor(color);
                col.Item().Text(value).FontSize(12).SemiBold().FontColor(color);
            });
    }

    private static void SignatureSection(IContainer container, string headerBg, string notes, string measuredBy)
    {
        container.Border(1).BorderColor(Colors.Grey.Lighten3).Padding(10).Column(col =>
        {
            col.Spacing(6);
            col.Item().Text("Comments & Signatures").SemiBold().FontColor(Colors.Blue.Darken2);

            if (!string.IsNullOrWhiteSpace(notes))
            {
                col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Background("#f9fbff").Padding(6).Text(notes);
            }

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });

                table.Header(header =>
                {
                    header.Cell().Background(headerBg).Padding(6).Text("Role").FontSize(10).SemiBold();
                    header.Cell().Background(headerBg).Padding(6).Text("Name").FontSize(10).SemiBold();
                    header.Cell().Background(headerBg).Padding(6).Text("Date / Signature").FontSize(10).SemiBold();
                });

                table.Cell().Padding(6).Text("Measured by");
                table.Cell().Padding(6).Text(string.IsNullOrWhiteSpace(measuredBy) ? "-" : measuredBy);
                table.Cell().Padding(6).Text($"{DateTime.Now:yyyy-MM-dd}  ____________");

                table.Cell().Padding(6).Text("Reviewed by");
                table.Cell().Padding(6).Text("-");
                table.Cell().Padding(6).Text("____________");

                table.Cell().Padding(6).Text("Approved by");
                table.Cell().Padding(6).Text("-");
                table.Cell().Padding(6).Text("____________");
            });
        });
    }

    private static string? GetLogoPath(string clinicName)
    {
        // Try to find logo in wwwroot/logos directory
        var logosDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "logos");
        if (Directory.Exists(logosDir))
        {
            if (!string.IsNullOrWhiteSpace(clinicName))
            {
                // Try clinic name based logo
                var clinicLogo = Path.Combine(logosDir, $"{clinicName.Replace(" ", "_")}.png");
                if (File.Exists(clinicLogo)) return clinicLogo;
                
                clinicLogo = Path.Combine(logosDir, $"{clinicName.Replace(" ", "_")}.jpg");
                if (File.Exists(clinicLogo)) return clinicLogo;
            }
            
            // Try default logo
            var defaultLogo = Path.Combine(logosDir, "logo.png");
            if (File.Exists(defaultLogo)) return defaultLogo;
            
            defaultLogo = Path.Combine(logosDir, "logo.jpg");
            if (File.Exists(defaultLogo)) return defaultLogo;
        }
        
        return null;
    }
}
