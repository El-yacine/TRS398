# TRS398
=======
# ✅ TRS-398 CLEAN APPLICATION

## FEATURES

✅ **CLEAN CODE** - Separate folder, no old code
✅ **EXCEL-LIKE INTERFACE** - Simple and professional
✅ **KPOL INCLUDED** - With M+, M-, and M100V measurements
✅ **3 MEASUREMENTS EACH** - For M+, M-, and M100V
✅ **AUTO-CALCULATIONS** - Real-time results
✅ **HISTORY** - View all past measurements
✅ **EXPORT** - CSV export
✅ **DATABASE** - SQLite for storage

## HOW TO RUN

```bash
cd /home/floky/TRS398_high_energy_photons/TRS398_Clean
./START.sh
```

Then open: **http://localhost:8000**

## MEASUREMENTS

### Input Fields:
- Energy (6X, 10X, 15X, 18X)
- T (Temperature °C)
- P (Pressure mBar)
- kQ (Beam quality factor)

### Measurements (3 each):
- **M+ (Positive polarity at +300V)**: M1, M2, M3
- **M- (Negative polarity at -300V)**: M1, M2, M3
- **M100V (at 100V)**: M1, M2, M3

### Auto-Calculated:
- Mean M+ (average of 3)
- Mean M- (average of 3)
- Mean M100V (average of 3)
- **Ktp** - Temperature/pressure correction
- **Kpol** - Polarity correction: (|M+| + |M-|) / (2 × |M+|)
- **Ks** - Recombination correction
- **M_corr** - Corrected reading
- **DW,Zref** - Absorbed dose
- **Ecart (%)** - Deviation

## PAGES

- **Main** (http://localhost:8000) - Measurement entry
- **History** (http://localhost:8000/history.html) - View all measurements

## COLOR SCHEME

- **Yellow** - Input cells (T, P, kQ)
- **White** - Measurement cells
- **Green** - Mean values and results
- **Blue** - Correction factors (Ktp, Kpol, Ks)
- **Red Bold** - Final results (DW,Zref, Ecart)

## DATABASE

Location: `server/trs398.db`
Type: SQLite

## STOP APPLICATION

Press `Ctrl+C` in terminal

## OLD APPLICATIONS REMOVED

The old complex code at http://localhost:5000 is no longer used.
This is the ONLY clean application now running on port 8000.

