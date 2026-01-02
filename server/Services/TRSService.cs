using MyQC.Models;

namespace MyQC.Services;

public class TRSService
{
    private static readonly double[] ElectronR50Nodes = new[] { 1.5, 2.5, 3.5, 4.5, 5.5, 6.5, 7.5 };
    private static readonly double[] ElectronKqCyl = new[] { 0.998, 0.996, 0.994, 0.993, 0.992, 0.991, 0.990 };
    private static readonly double[] ElectronKqPP = new[] { 1.000, 0.999, 0.998, 0.997, 0.996, 0.995, 0.994 };

    public TRSMeasurement Calculate(TRSMeasurement input)
    {
        // Calculate means
        input.Mean_plus = (input.M_plus_1 + input.M_plus_2 + input.M_plus_3) / 3.0;
        input.Mean_minus = (input.M_minus_1 + input.M_minus_2 + input.M_minus_3) / 3.0;
        input.Mean100V = (input.M100V_1 + input.M100V_2 + input.M100V_3) / 3.0;
        
        // Ktp = (1013/P) * (273.2+T)/293.2
        input.Ktp = (1013.0 / input.P) * (273.2 + input.T) / 293.2;
        
        // Kpol = (|M+| + |M-|) / (2 * |M+|)
        input.Kpol = (Math.Abs(input.Mean_plus) + Math.Abs(input.Mean_minus)) / (2.0 * Math.Abs(input.Mean_plus));
        
        // Ks = 1.198 - (0.875 * Mean+/Mean100V) + (0.677 * (Mean+/Mean100V)^2)
        double ratio = input.Mean_plus / input.Mean100V;
        input.Ks = 1.198 - (0.875 * ratio) + (0.677 * ratio * ratio);
        
        var isElectron = string.Equals(input.Mode, "electron", StringComparison.OrdinalIgnoreCase);
        input.NdUsed = input.Ndw;
        input.kQUsed = input.kQ;

        if (isElectron)
        {
            if (input.R50 > 0 && input.Zref <= 0)
            {
                input.Zref = (0.6 * input.R50) - 0.1;
            }

            if (input.SSD <= 0) input.SSD = 100;
            var chamberType = (input.ChamberType ?? "cylindrical").ToLowerInvariant();
            var r50 = input.R50;
            double kq = input.kQ;

            if (chamberType.Contains("plane"))
            {
                input.NdUsed = input.Ndw_Qcross > 0 ? input.Ndw_Qcross : input.Ndw;
                kq = Interpolate(r50, ElectronR50Nodes, ElectronKqPP);
            }
            else
            {
                input.NdUsed = input.Ndw;
                kq = Interpolate(r50, ElectronR50Nodes, ElectronKqCyl);
            }

            input.kQUsed = kq;
            input.kQ = kq;
        }

        // M_corr = Mean+ * Ktp * Kpol * Ks
        input.M_corr = input.Mean_plus * input.Ktp * input.Kpol * input.Ks;
        
        // DW,Zref = M_corr * kQ * NdUsed
        input.DW_Zref = input.M_corr * input.kQUsed * input.NdUsed;
        
        // Ecart (%) = 100 * (1 - DW,Zref)
        input.Ecart = 100.0 * (1.0 - input.DW_Zref);
        
        input.Date = DateTime.Now;
        
        return input;
    }

    private static double Interpolate(double x, IReadOnlyList<double> nodes, IReadOnlyList<double> values)
    {
        if (nodes.Count == 0 || values.Count == 0) return 1.0;
        if (x <= nodes[0]) return values[0];
        if (x >= nodes[^1]) return values[^1];
        for (int i = 0; i < nodes.Count - 1; i++)
        {
            if (x >= nodes[i] && x <= nodes[i + 1])
            {
                var t = (x - nodes[i]) / (nodes[i + 1] - nodes[i]);
                return values[i] + t * (values[i + 1] - values[i]);
            }
        }
        return values[^1];
    }
}
