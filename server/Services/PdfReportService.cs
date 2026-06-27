using System.Globalization;
using MyQC.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MyQC.Services;

public class PdfReportService
{
    private static readonly CultureInfo IC = CultureInfo.InvariantCulture;

    // Register Lato TTF once so all PDF text uses a properly embedded font
    // that includes every ASCII glyph (period, comma, etc.) regardless of OS locale.
    static PdfReportService()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "LatoFont"),
            Path.Combine(Directory.GetCurrentDirectory(), "LatoFont"),
        };
        foreach (var dir in candidates)
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var ttf in Directory.GetFiles(dir, "*.ttf"))
                QuestPDF.Drawing.FontManager.RegisterFont(File.OpenRead(ttf));
            break;
        }
    }

    // ── Classic monochrome palette ────────────────────────────────────────────
    private const string Black  = "#000000";
    private const string Ink    = "#1A1A1A";
    private const string Label  = "#555555";
    private const string Rule   = "#999999";
    private const string Stripe = "#F5F5F5";
    private const string White  = "#FFFFFF";
    private const string Green  = "#166534";
    private const string Red    = "#991B1B";

    private static string DataDir =>
        Environment.GetEnvironmentVariable("TRS398_DATA_DIR")
        ?? Directory.GetCurrentDirectory();

    // Force InvariantCulture for this thread so all number formatting
    // (including inside QuestPDF) uses '.' as decimal separator, not Arabic ٫.
    private static void EnforceInvariantCulture()
    {
        System.Threading.Thread.CurrentThread.CurrentCulture   = IC;
        System.Threading.Thread.CurrentThread.CurrentUICulture = IC;
    }

    // Build "22.2" from integer arithmetic — avoids ANY locale decimal separator.
    // The '.' is a literal char constant, not produced by ToString().
    private static string N1(double v)
    {
        v = Math.Round(v, 1);
        int whole = (int)v;
        int frac  = (int)Math.Round(Math.Abs(v - whole) * 10);
        return whole.ToString(IC) + "." + frac.ToString(IC);
    }

    // =========================================================================
    // SINGLE MEASUREMENT REPORT
    // =========================================================================
    public byte[] Build(TRSMeasurement m, string? signatureHint = null)
    {
        EnforceInvariantCulture();
        var reportId   = m.Id > 0 ? $"TRS398-{m.Id:D4}" : $"DRAFT-{DateTime.Now:HHmm}";
        var isElectron = m.Mode?.Equals("electron", StringComparison.OrdinalIgnoreCase) == true;
        var beamLabel  = isElectron ? "Electron" : "Photon";
        var deviation  = m.Ecart;
        var pass       = Math.Abs(deviation) <= 2.0;
        var logoPath   = FindLogo(m.ClinicName);
        var sigPath    = FindSignature(signatureHint, m.UserName);

        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(40);
                page.MarginTop(0);
                page.MarginBottom(24);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(Ink).FontFamily("Lato"));

                page.Header().Element(e => Header(e, m, reportId, beamLabel, pass, logoPath));
                page.Content().PaddingTop(16).Column(body =>
                {
                    body.Spacing(14);
                    body.Item().Element(e => InfoRow(e, m, beamLabel, isElectron, reportId));
                    body.Item().Element(e => ReadingsTable(e, m));
                    body.Item().Element(e => CorrectionsAndResult(e, m, pass, deviation));
                    body.Item().Element(e => SignatureBlock(e, m, sigPath));
                });
                page.Footer().Element(e => Footer(e, reportId, m.Date));
            });
        }).GeneratePdf();
    }

    // ── Header ───────────────────────────────────────────────────────────────
    private static void Header(IContainer c, TRSMeasurement m, string reportId,
        string beamLabel, bool pass, string? logoPath)
    {
        c.Column(col =>
        {
            // Top rule
            col.Item().Height(2).Background(Black);
            col.Item().Height(2);

            col.Item().PaddingVertical(14).Row(row =>
            {
                // Logo
                if (logoPath is not null && File.Exists(logoPath))
                {
                    row.ConstantItem(56).AlignMiddle().Element(e =>
                    {
                        try { e.Image(logoPath).FitArea(); } catch { }
                    });
                    row.ConstantItem(14);
                }

                // Clinic identity
                row.RelativeItem().AlignMiddle().Column(clinic =>
                {
                    var name = string.IsNullOrWhiteSpace(m.ClinicName) ? "Medical Facility" : m.ClinicName;
                    clinic.Item().Text(name).FontSize(16).Bold().FontColor(Black);
                    if (!string.IsNullOrWhiteSpace(m.ClinicAddress))
                        clinic.Item().PaddingTop(2)
                            .Text(m.ClinicAddress).FontSize(8).FontColor(Label);
                    clinic.Item().PaddingTop(2)
                        .Text("Department of Medical Physics  —  Radiation Dosimetry")
                        .FontSize(7.5f).FontColor(Label);
                });

                // Right: report meta
                row.ConstantItem(160).AlignRight().Column(meta =>
                {
                    meta.Item().AlignRight().Element(e =>
                        e.Border(1).BorderColor(Black)
                         .PaddingHorizontal(10).PaddingVertical(4)
                         .Text("IAEA TRS-398").FontSize(8.5f).Bold().FontColor(Black));

                    meta.Item().PaddingTop(8).AlignRight()
                        .Text($"{beamLabel} Dosimetry Report")
                        .FontSize(8).FontColor(Ink);
                    meta.Item().PaddingTop(2).AlignRight()
                        .Text($"Report No:  {reportId}").FontSize(8).FontColor(Ink);
                    meta.Item().PaddingTop(2).AlignRight()
                        .Text($"Date:  {m.Date:dd MMMM yyyy}").FontSize(8).FontColor(Ink);
                    meta.Item().PaddingTop(2).AlignRight()
                        .Text($"Time:  {m.Date:HH:mm}").FontSize(8).FontColor(Ink);
                });
            });

            // Bottom rule
            col.Item().Height(1).Background(Black);
            col.Item().Height(4).Background(White);
        });
    }

    // ── Info panels ──────────────────────────────────────────────────────────
    private static void InfoRow(IContainer c, TRSMeasurement m,
        string beamLabel, bool isElectron, string reportId)
    {
        var bqLabel = isElectron ? "R₅₀  (g/cm²)" : "TPR₂₀,₁₀";
        var bqValue = isElectron
            ? (m.R50 > 0 ? m.R50.ToString("0.00", IC) : "—")
            : (m.TPR2010 > 0 ? m.TPR2010.ToString("0.000", IC) : "—");

        c.Row(row =>
        {
            row.Spacing(10);
            row.RelativeItem().Element(e => Panel(e, "PERSONNEL", new[]
            {
                ("Physicist",  OrDash(m.UserName)),
                ("Department", "Medical Physics"),
                ("Protocol",   "IAEA TRS-398"),
                ("Report ID",  reportId),
            }));
            row.RelativeItem().Element(e => Panel(e, "TREATMENT MACHINE", new[]
            {
                ("LINAC",      OrDash(m.LinacName)),
                ("Serial No.", OrDash(m.LinacId)),
                ("Beam Type",  beamLabel),
                ("Energy",     OrDash(m.Energy)),
                ("Field Size", string.IsNullOrWhiteSpace(m.FieldSize) ? "10×10 cm²" : m.FieldSize),
            }));
            row.RelativeItem().Element(e => Panel(e, "REFERENCE CONDITIONS", new[]
            {
                ("Ion Chamber", OrDash(m.Chamber)),
                (bqLabel,       bqValue),
                ("z_ref (cm)",  m.Zref > 0 ? m.Zref.ToString("0.00", IC) : "10.00"),
                ("SSD / SAD",   $"{m.SSD.ToString("0.0", IC)} cm"),
                ("T  /  P",     $"{N1(m.T)} °C  ·  {N1(m.P)} mBar"),
            }));
        });
    }

    private static void Panel(IContainer c, string title,
        (string label, string value)[] rows)
    {
        c.Border(1).BorderColor(Black).Column(col =>
        {
            col.Item().Background(Ink).PaddingHorizontal(8).PaddingVertical(5)
               .Text(title).FontSize(7.5f).Bold().FontColor(White);

            col.Item().Background(White).PaddingHorizontal(8).PaddingVertical(4).Column(inner =>
            {
                for (int i = 0; i < rows.Length; i++)
                {
                    var (lbl, val) = rows[i];
                    inner.Item().PaddingVertical(3).Row(r =>
                    {
                        r.RelativeItem().Text(lbl).FontSize(8).FontColor(Label);
                        r.AutoItem().Text(val).FontSize(8.5f).SemiBold().FontColor(Ink);
                    });
                    if (i < rows.Length - 1)
                        inner.Item().BorderBottom(0.5f).BorderColor(Rule);
                }
            });
        });
    }

    // ── Readings table ───────────────────────────────────────────────────────
    private static void ReadingsTable(IContainer c, TRSMeasurement m)
    {
        c.Border(1).BorderColor(Black).Column(col =>
        {
            col.Item().Background(Ink).PaddingHorizontal(8).PaddingVertical(5)
               .Text("ELECTROMETER READINGS  (nC)")
               .FontSize(7.5f).Bold().FontColor(White);

            col.Item().Padding(8).Table(table =>
            {
                table.ColumnsDefinition(def =>
                {
                    def.RelativeColumn(2f);
                    def.RelativeColumn();
                    def.RelativeColumn();
                    def.RelativeColumn();
                    def.RelativeColumn(1.5f);
                });

                table.Header(h =>
                {
                    h.Cell().BorderBottom(1).BorderColor(Black).PaddingVertical(5).PaddingHorizontal(6)
                        .Text("Polarity / Voltage").FontSize(8).Bold().FontColor(Ink);
                    foreach (var r in new[] { "R₁", "R₂", "R₃", "Mean" })
                        h.Cell().BorderBottom(1).BorderColor(Black).PaddingVertical(5).AlignCenter()
                            .Text(r).FontSize(8).Bold().FontColor(Ink);
                });

                ReadingRow(table, "M⁺  (+ 300 V)",  m.M_plus_1,  m.M_plus_2,  m.M_plus_3,  m.Mean_plus,  White);
                ReadingRow(table, "M⁻  (− 300 V)",  m.M_minus_1, m.M_minus_2, m.M_minus_3, m.Mean_minus, Stripe);
                ReadingRow(table, "M₁₀₀V  (100 V)", m.M100V_1,   m.M100V_2,   m.M100V_3,   m.Mean100V,   White);
            });
        });
    }

    private static void ReadingRow(TableDescriptor t, string label,
        double r1, double r2, double r3, double mean, string bg)
    {
        t.Cell().Background(bg).PaddingVertical(6).PaddingHorizontal(6)
            .Text(label).FontSize(8.5f).FontColor(Ink);
        foreach (var v in new[] { r1, r2, r3 })
            t.Cell().Background(bg).PaddingVertical(6).AlignCenter()
                .Text(v.ToString("F4", IC)).FontSize(8.5f).FontColor(Ink);
        t.Cell().Background(Stripe).PaddingVertical(6).AlignCenter()
            .Text(mean.ToString("F4", IC)).FontSize(8.5f).Bold().FontColor(Ink);
    }

    // ── Corrections + Result ─────────────────────────────────────────────────
    private static void CorrectionsAndResult(IContainer c, TRSMeasurement m, bool pass, double deviation)
    {
        c.Row(row =>
        {
            row.Spacing(10);

            // Correction factors
            row.RelativeItem(1.3f).Border(1).BorderColor(Black).Column(col =>
            {
                col.Item().Background(Ink).PaddingHorizontal(8).PaddingVertical(5)
                   .Text("CORRECTION FACTORS")
                   .FontSize(7.5f).Bold().FontColor(White);

                col.Item().Padding(8).Column(factors =>
                {
                    Factor(factors, "k_TP",   m.Ktp.ToString("F5", IC),  "Temperature–pressure");
                    Factor(factors, "k_pol",  m.Kpol.ToString("F5", IC), "Polarity correction");
                    Factor(factors, "k_s",    m.Ks.ToString("F5", IC),   "Ion recombination");
                    Factor(factors, "k_Q",    (m.kQUsed > 0 ? m.kQUsed : m.kQ).ToString("F5", IC), "Beam quality");
                    Factor(factors, "N_d,w",  m.Ndw.ToString("F5", IC),  "Calibration factor (Gy/nC)");

                    factors.Item().PaddingTop(6).BorderTop(1).BorderColor(Black).PaddingBottom(4);

                    factors.Item().Row(r =>
                    {
                        r.RelativeItem().Column(lc =>
                        {
                            lc.Item().Text("M_corr").FontSize(9).Bold().FontColor(Ink);
                            lc.Item().PaddingTop(1)
                              .Text("M⁺ × k_TP × k_pol × k_s").FontSize(7.5f).Italic().FontColor(Label);
                        });
                        r.AutoItem().AlignMiddle()
                         .Text(m.M_corr.ToString("F5", IC)).FontSize(11).Bold().FontColor(Ink);
                    });
                });
            });

            // Final result
            row.RelativeItem().Border(1).BorderColor(Black).Column(col =>
            {
                col.Item().Background(Ink).PaddingHorizontal(8).PaddingVertical(5)
                   .Text("FINAL RESULT  —  IAEA TRS-398")
                   .FontSize(7.5f).Bold().FontColor(White);

                col.Item().PaddingTop(18).PaddingHorizontal(16).Column(res =>
                {
                    res.Item().AlignCenter()
                       .Text("Absorbed Dose to Water  D_w,Q")
                       .FontSize(8.5f).FontColor(Label);

                    res.Item().PaddingTop(4).AlignCenter()
                       .Text(m.DW_Zref.ToString("F5", IC))
                       .FontSize(34).Bold().FontColor(Black);

                    res.Item().AlignCenter()
                       .Text("Gy  at  z_ref").FontSize(10).FontColor(Label);

                    // PASS / FAIL box
                    res.Item().PaddingTop(14).AlignCenter().Element(e =>
                    {
                        var color = pass ? Green : Red;
                        e.Border(2).BorderColor(color)
                         .PaddingHorizontal(32).PaddingVertical(6)
                         .Text(pass ? "PASS" : "FAIL")
                         .FontSize(18).Bold().FontColor(color);
                    });

                    res.Item().PaddingTop(10).AlignCenter().Column(dev =>
                    {
                        dev.Item().AlignCenter()
                           .Text($"Deviation  {(deviation >= 0 ? "+" : "")}{deviation.ToString("0.00", IC)} %")
                           .FontSize(11).Bold().FontColor(pass ? Green : Red);
                        dev.Item().PaddingTop(3).AlignCenter()
                           .Text("Tolerance:  ± 2.00 %").FontSize(8).FontColor(Label);
                    });

                    res.Item().PaddingTop(14).BorderTop(0.5f).BorderColor(Rule)
                       .PaddingTop(6).AlignCenter()
                       .Text("D_w,Q  =  M_corr × k_Q,Q₀ × N_d,w")
                       .FontSize(8).Italic().FontColor(Label);
                });
            });
        });
    }

    private static void Factor(ColumnDescriptor col, string symbol, string value, string description)
    {
        col.Item().PaddingVertical(4).Row(r =>
        {
            r.ConstantItem(50).Text(symbol).FontSize(8.5f).Bold().FontColor(Ink);
            r.RelativeItem().Text(description).FontSize(7.5f).Italic().FontColor(Label);
            r.ConstantItem(70).AlignRight().Text(value).FontSize(8.5f).SemiBold().FontColor(Ink);
        });
        col.Item().BorderBottom(0.5f).BorderColor(Rule);
    }

    // ── Signature block ──────────────────────────────────────────────────────
    private static void SignatureBlock(IContainer c, TRSMeasurement m, string? sigPath)
    {
        c.Border(1).BorderColor(Black).Column(col =>
        {
            col.Item().Background(Ink).PaddingHorizontal(8).PaddingVertical(5)
               .Text("AUTHORIZATION & SIGNATURE")
               .FontSize(7.5f).Bold().FontColor(White);

            col.Item().Padding(8).Column(inner =>
            {
                if (!string.IsNullOrWhiteSpace(m.Notes))
                {
                    inner.Item().Border(0.5f).BorderColor(Rule)
                        .Padding(8).Text(m.Notes).FontSize(8.5f).Italic().FontColor(Ink);
                    inner.Item().Height(8);
                }

                inner.Item().Table(table =>
                {
                    table.ColumnsDefinition(def =>
                    {
                        def.ConstantColumn(90);
                        def.RelativeColumn();
                        def.RelativeColumn(2);
                    });

                    table.Header(h =>
                    {
                        foreach (var hdr in new[] { "Role", "Name", "Date  /  Signature" })
                            h.Cell().BorderBottom(1).BorderColor(Black).PaddingVertical(5).PaddingHorizontal(6)
                              .Text(hdr).FontSize(8).Bold().FontColor(Ink);
                    });

                    bool alt = false;
                    SigRow(table, "Measured by",   OrDash(m.UserName), m.Date.ToString("yyyy-MM-dd"), sigPath, alt = !alt);
                    SigRow(table, "Reviewed by",   "",  "________________________", null, alt = !alt);
                    SigRow(table, "Approved (QA)", "", "________________________", null, alt = !alt);
                });
            });
        });
    }

    private static void SigRow(TableDescriptor t, string role, string name, string date,
        string? sigPath, bool alt)
    {
        var bg = alt ? Stripe : White;
        t.Cell().Background(bg).PaddingVertical(7).PaddingHorizontal(6)
            .Text(role).FontSize(8.5f).FontColor(Label);
        t.Cell().Background(bg).PaddingVertical(7).PaddingHorizontal(6)
            .Text(name).FontSize(8.5f).SemiBold().FontColor(Ink);

        if (sigPath is not null && File.Exists(sigPath))
        {
            t.Cell().Background(bg).Padding(5).Row(sr =>
            {
                sr.AutoItem().AlignMiddle().Text(date).FontSize(8).FontColor(Label);
                sr.ConstantItem(8);
                sr.ConstantItem(90).Height(30).Element(e =>
                {
                    try { e.Image(sigPath!).FitArea(); } catch { }
                });
            });
        }
        else
        {
            t.Cell().Background(bg).PaddingVertical(7).PaddingHorizontal(6)
                .Text(date).FontSize(8.5f).FontColor(Label);
        }
    }

    // ── Footer ───────────────────────────────────────────────────────────────
    private static void Footer(IContainer c, string reportId, DateTime date)
    {
        c.BorderTop(1).BorderColor(Black).PaddingTop(6).Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem()
                   .Text($"{reportId}  ·  IAEA TRS-398  ·  TRS-398 Pro")
                   .FontSize(7.5f).FontColor(Label);
                row.AutoItem()
                   .Text($"{date:dd MMM yyyy  HH:mm}  ·  Page 1 of 1")
                   .FontSize(7.5f).FontColor(Label);
            });
            col.Item().PaddingTop(3).AlignCenter()
               .Text($"© {DateTime.Now:yyyy} Yacine El Attaoui — Medical Physicist  ·  TRS-398 Pro")
               .FontSize(7f).FontColor(Label);
        });
    }

    // =========================================================================
    // SUMMARY REPORT
    // =========================================================================
    public byte[] BuildSummaryReport(List<TRSMeasurement> measurements, string? clinicName = null)
    {
        EnforceInvariantCulture();
        var now      = DateTime.Now;
        var clinic   = clinicName ?? measurements.FirstOrDefault()?.ClinicName ?? "Medical Facility";
        var total    = measurements.Count;
        var pass     = measurements.Count(x => Math.Abs(x.Ecart) <= 2.0);
        var fail     = total - pass;
        var passRate = total > 0 ? pass * 100.0 / total : 0;
        var avgDev   = total > 0 ? measurements.Average(x => Math.Abs(x.Ecart)) : 0;
        var logoPath = FindLogo(clinic);

        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(40);
                page.MarginTop(0);
                page.MarginBottom(24);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(Ink).FontFamily("Lato"));

                page.Header().Element(e => SummaryHeader(e, clinic,
                    measurements.FirstOrDefault()?.ClinicAddress, now, logoPath));
                page.Content().PaddingTop(16).Column(body =>
                {
                    body.Spacing(14);
                    body.Item().Element(e => SummaryStats(e, total, pass, fail, passRate, avgDev));
                    body.Item().Element(e => SummaryTable(e, measurements));
                    body.Item().Element(e => SummaryNotes(e, now));
                });
                var capturedNow = now;
                page.Footer().Element(e => SummaryFooter(e, capturedNow));
            });
        }).GeneratePdf();
    }

    private static void SummaryHeader(IContainer c, string clinicName,
        string? address, DateTime now, string? logoPath)
    {
        c.Column(col =>
        {
            col.Item().Height(2).Background(Black);
            col.Item().Height(2);

            col.Item().PaddingVertical(14).Row(row =>
            {
                if (logoPath is not null && File.Exists(logoPath))
                {
                    row.ConstantItem(56).AlignMiddle().Element(e =>
                    { try { e.Image(logoPath).FitArea(); } catch { } });
                    row.ConstantItem(14);
                }

                row.RelativeItem().AlignMiddle().Column(clinic =>
                {
                    clinic.Item().Text(clinicName).FontSize(16).Bold().FontColor(Black);
                    if (!string.IsNullOrWhiteSpace(address))
                        clinic.Item().PaddingTop(2)
                              .Text(address).FontSize(8).FontColor(Label);
                    clinic.Item().PaddingTop(2)
                          .Text("Department of Medical Physics  —  Calibration Summary Report")
                          .FontSize(7.5f).FontColor(Label);
                });

                row.ConstantItem(160).AlignRight().Column(meta =>
                {
                    meta.Item().AlignRight().Element(e =>
                        e.Border(1).BorderColor(Black)
                         .PaddingHorizontal(10).PaddingVertical(4)
                         .Text("IAEA TRS-398").FontSize(8.5f).Bold().FontColor(Black));
                    meta.Item().PaddingTop(8).AlignRight()
                        .Text($"Generated: {now:dd MMMM yyyy}").FontSize(8).FontColor(Ink);
                    meta.Item().PaddingTop(2).AlignRight()
                        .Text($"Time: {now:HH:mm}").FontSize(8).FontColor(Ink);
                });
            });

            col.Item().Height(1).Background(Black);
            col.Item().Height(4).Background(White);
        });
    }

    private static void SummaryStats(IContainer c, int total, int pass, int fail,
        double passRate, double avgDev)
    {
        c.Border(1).BorderColor(Black).Column(col =>
        {
            col.Item().Background(Ink).PaddingHorizontal(8).PaddingVertical(5)
               .Text("STATISTICAL OVERVIEW")
               .FontSize(7.5f).Bold().FontColor(White);

            col.Item().Padding(12).Row(row =>
            {
                row.Spacing(0);
                Stat(row, "Total Calibrations", total.ToString(),       Ink);
                row.ConstantItem(1).Background(Rule);
                Stat(row, "Pass",               pass.ToString(),        Green);
                row.ConstantItem(1).Background(Rule);
                Stat(row, "Fail",               fail.ToString(),        fail > 0 ? Red : Label);
                row.ConstantItem(1).Background(Rule);
                Stat(row, "Pass Rate",          $"{passRate.ToString("F1", IC)} %",  passRate >= 95 ? Green : Red);
                row.ConstantItem(1).Background(Rule);
                Stat(row, "Avg |Deviation|",    $"{avgDev.ToString("F2", IC)} %",   avgDev < 2 ? Green : Red);
            });
        });
    }

    private static void Stat(RowDescriptor row, string label, string value, string color)
    {
        row.RelativeItem().AlignCenter().Column(col =>
        {
            col.Item().AlignCenter().Text(label).FontSize(8).FontColor(Label);
            col.Item().PaddingTop(5).AlignCenter()
               .Text(value).FontSize(22).Bold().FontColor(color);
        });
    }

    private static void SummaryTable(IContainer c, List<TRSMeasurement> measurements)
    {
        c.Border(1).BorderColor(Black).Column(col =>
        {
            col.Item().Background(Ink).PaddingHorizontal(8).PaddingVertical(5)
               .Text("CALIBRATION RECORDS")
               .FontSize(7.5f).Bold().FontColor(White);

            col.Item().Padding(8).Table(table =>
            {
                table.ColumnsDefinition(def =>
                {
                    def.ConstantColumn(56);
                    def.RelativeColumn(1.4f);
                    def.ConstantColumn(52);
                    def.ConstantColumn(36);
                    def.ConstantColumn(44);
                    def.RelativeColumn();
                    def.ConstantColumn(50);
                    def.ConstantColumn(36);
                });

                table.Header(h =>
                {
                    foreach (var hdr in new[] { "Date", "LINAC", "Serial", "Mode", "Energy", "Physicist", "Dev (%)", "Status" })
                        h.Cell().BorderBottom(1).BorderColor(Black).PaddingVertical(5).PaddingHorizontal(5)
                          .Text(hdr).FontSize(8).Bold().FontColor(Ink);
                });

                int idx = 0;
                foreach (var m in measurements.OrderByDescending(x => x.Date))
                {
                    var isPass = Math.Abs(m.Ecart) <= 2.0;
                    var bg     = idx++ % 2 == 0 ? White : Stripe;
                    var devc   = isPass ? Green : Red;

                    table.Cell().Background(bg).PaddingVertical(5).PaddingHorizontal(5)
                        .Text(m.Date.ToString("dd/MM/yy")).FontSize(8).FontColor(Ink);
                    table.Cell().Background(bg).PaddingVertical(5).PaddingHorizontal(5)
                        .Text(OrDash(m.LinacName)).FontSize(8).SemiBold().FontColor(Ink);
                    table.Cell().Background(bg).PaddingVertical(5).PaddingHorizontal(5)
                        .Text(OrDash(m.LinacId)).FontSize(7.5f).FontColor(Label);
                    table.Cell().Background(bg).PaddingVertical(5).PaddingHorizontal(5)
                        .Text(m.Mode?.Contains("electron", StringComparison.OrdinalIgnoreCase) == true ? "e⁻" : "γ")
                        .FontSize(8).FontColor(Ink);
                    table.Cell().Background(bg).PaddingVertical(5).PaddingHorizontal(5)
                        .Text(OrDash(m.Energy)).FontSize(8).FontColor(Ink);
                    table.Cell().Background(bg).PaddingVertical(5).PaddingHorizontal(5)
                        .Text(OrDash(m.UserName)).FontSize(8).FontColor(Ink);
                    table.Cell().Background(bg).PaddingVertical(5).PaddingHorizontal(5)
                        .Text((m.Ecart >= 0 ? "+" : "") + m.Ecart.ToString("0.00", IC)).FontSize(8).Bold().FontColor(devc);
                    table.Cell().Background(bg).PaddingVertical(5).PaddingHorizontal(5)
                        .Text(isPass ? "PASS" : "FAIL").FontSize(8).Bold().FontColor(devc);
                }
            });
        });
    }

    private static void SummaryFooter(IContainer c, DateTime now)
    {
        c.BorderTop(1).BorderColor(Black).PaddingTop(6).Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem()
                   .Text("TRS-398 Pro  ·  Calibration Summary Report  ·  IAEA TRS-398")
                   .FontSize(7.5f).FontColor(Label);
                row.AutoItem()
                   .Text($"Generated: {now:dd MMM yyyy  HH:mm}  ·  Page 1 of 1")
                   .FontSize(7.5f).FontColor(Label);
            });
            col.Item().PaddingTop(3).AlignCenter()
               .Text($"© {now:yyyy} Yacine El Attaoui — Medical Physicist  ·  TRS-398 Pro")
               .FontSize(7f).FontColor(Label);
        });
    }

    private static void SummaryNotes(IContainer c, DateTime now)
    {
        c.Border(1).BorderColor(Black).Padding(10).Column(col =>
        {
            col.Item().Text("PROTOCOL REFERENCE").FontSize(7.5f).Bold().FontColor(Ink);
            col.Item().Height(1).Background(Rule);
            col.Item().PaddingTop(6)
               .Text("All calibrations performed following the IAEA TRS-398 code of practice " +
                     "for determination of absorbed dose to water in external beam radiotherapy. " +
                     "Pass criterion: |deviation| ≤ 2.0 % from reference absorbed dose.")
               .FontSize(8).FontColor(Label).LineHeight(1.4f);
            col.Item().PaddingTop(4)
               .Text($"Report generated by TRS-398 Pro  ·  {now:dd MMMM yyyy  HH:mm}")
               .FontSize(7.5f).Italic().FontColor(Label);
        });
    }

    // =========================================================================
    // HELPERS
    // =========================================================================
    private static string OrDash(string? s) =>
        string.IsNullOrWhiteSpace(s) ? "—" : s;

    private static string? FindLogo(string? clinicName)
    {
        var dir = Path.Combine(DataDir, "logos");
        if (!Directory.Exists(dir)) return null;

        if (!string.IsNullOrWhiteSpace(clinicName))
        {
            var safe = string.Concat(clinicName.Split(Path.GetInvalidFileNameChars())).Replace(' ', '_');
            foreach (var ext in new[] { ".png", ".jpg", ".jpeg", ".webp" })
            {
                var p = Path.Combine(dir, safe + ext);
                if (File.Exists(p)) return p;
            }
        }

        foreach (var name in new[] { "logo.png", "logo.jpg", "logo.jpeg" })
        {
            var p = Path.Combine(dir, name);
            if (File.Exists(p)) return p;
        }

        return Directory.EnumerateFiles(dir)
            .FirstOrDefault(f => new[] { ".png", ".jpg", ".jpeg", ".webp" }
                .Contains(Path.GetExtension(f).ToLowerInvariant()));
    }

    private static string? FindSignature(string? hint, string? userName)
    {
        var sigDir = Path.Combine(DataDir, "signatures");

        if (!string.IsNullOrWhiteSpace(hint))
        {
            var rel  = hint.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var full = Path.Combine(DataDir, rel);
            if (File.Exists(full)) return full;
        }

        if (string.IsNullOrWhiteSpace(userName) || !Directory.Exists(sigDir))
            return null;

        var safe = userName.Replace(' ', '_');
        foreach (var ext in new[] { ".png", ".jpg", ".jpeg" })
        {
            var p = Path.Combine(sigDir, $"{safe}_signature{ext}");
            if (File.Exists(p)) return p;
        }
        return null;
    }
}
