namespace MyQC.Models;

// TRS-398 measurement with M+, M-, M100V and Kpol
public class TRSMeasurement
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public string Mode { get; set; } = "photon"; // photon or electron
    public string UserName { get; set; } = "";
    public string Energy { get; set; } = "";
    public string LinacName { get; set; } = "";
    public string LinacId { get; set; } = "";
    public string FieldSize { get; set; } = "";
    public string Chamber { get; set; } = "";
    public double TPR2010 { get; set; }
    public double R50 { get; set; }
    public double Zref { get; set; }
    public double SSD { get; set; } = 100;
    public string ChamberType { get; set; } = "cylindrical";
    public double Ndw_Qcross { get; set; } // for plane-parallel cross calibration
    public double R50_cross { get; set; }   // R50 used for cross-calibration reference
    public double NdUsed { get; set; }
    public double kQUsed { get; set; }
    public string ClinicName { get; set; } = "";
    public string ClinicAddress { get; set; } = "";
    
    // Environmental conditions
    public double T { get; set; }           // Temperature °C
    public double P { get; set; }           // Pressure mBar
    public double kQ { get; set; }          // Beam quality factor
    public double Ndw { get; set; } = 0.04789; // Chamber calibration factor
    
    // M+ (Positive polarity +300V) - 3 measurements
    public double M_plus_1 { get; set; }
    public double M_plus_2 { get; set; }
    public double M_plus_3 { get; set; }
    
    // M- (Negative polarity -300V) - 3 measurements
    public double M_minus_1 { get; set; }
    public double M_minus_2 { get; set; }
    public double M_minus_3 { get; set; }
    
    // M100V - 3 measurements
    public double M100V_1 { get; set; }
    public double M100V_2 { get; set; }
    public double M100V_3 { get; set; }
    
    // Calculated means
    public double Mean_plus { get; set; }
    public double Mean_minus { get; set; }
    public double Mean100V { get; set; }
    
    // Correction factors
    public double Ktp { get; set; }
    public double Kpol { get; set; }        // Polarity correction
    public double Ks { get; set; }
    
    // Final results
    public double M_corr { get; set; }
    public double DW_Zref { get; set; }
    public double Ecart { get; set; }
    
    public string Notes { get; set; } = "";
}
