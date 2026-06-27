# TRS-398 Pro - Medical Physics Calibration System

<div align="center">

![Version](https://img.shields.io/badge/version-2.1.0-blue.svg)
![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)
![License](https://img.shields.io/badge/license-MIT-green.svg)
![Platform](https://img.shields.io/badge/platform-Linux%20%7C%20Windows-lightgrey.svg)

**A professional, user-friendly application for TRS-398 photon and electron beam calibration measurements**

[Features](#-features) • [Quick Start](#-quick-start) • [Installation](#-installation) • [Documentation](#-documentation) • [Contributing](#-contributing)

</div>

---

## 📋 Table of Contents

- [Overview](#-overview)
- [Features](#-features)
- [Requirements](#-requirements)
- [Quick Start](#-quick-start)
- [Installation](#-installation)
  - [Linux](#linux-installation)
  - [Windows](#windows-installation)
- [Usage](#-usage)
- [Project Structure](#-project-structure)
- [Documentation](#-documentation)
- [Development](#-development)
- [Contributing](#-contributing)
- [License](#-license)

---

## 🎯 Overview

TRS-398 Pro is a modern, web-based application designed for medical physicists to perform absorbed dose calibrations following the **IAEA TRS-398 protocol**. It provides an intuitive interface for calculating correction factors (Ktp, Kpol, Ks) and generating professional calibration reports.

### Project Launch & Development Methodology

TRS-398 Pro was launched in January 2026 as a comprehensive solution for medical physics calibration workflows. The project was developed using a modern, cross-platform architecture built on **.NET 8.0** with **ASP.NET Core Minimal API** for the backend, providing a lightweight and performant RESTful API layer. The frontend was implemented using **vanilla JavaScript** with a responsive, mobile-first design approach, ensuring accessibility across desktop, tablet, and mobile devices. Data persistence is handled through **SQLite** with **Entity Framework Core**, offering a lightweight, file-based database solution that requires no separate database server installation. Professional PDF report generation is powered by **QuestPDF**, enabling the creation of high-quality calibration certificates with custom clinic branding and digital signature support. The application follows a service-oriented architecture with clear separation of concerns: business logic in dedicated service classes (TRSService for calculations, PdfReportService for report generation), data models for type safety, and a clean API design that supports future extensibility. Cross-platform deployment was achieved through .NET's self-contained publishing capabilities, allowing the same codebase to run natively on both Linux and Windows with platform-specific installers (Python Tkinter GUI for Linux, PowerShell GUI for Windows). The development methodology emphasized user-centric design, with features like real-time calculations, automatic kQ factor interpolation from a pre-loaded chamber library, multi-language support (English/French), and comprehensive settings management. The project structure was organized to support both end-users and developers, with clear separation between platform-specific installation scripts, shared core application code, and comprehensive documentation, making it easy to maintain, extend, and contribute to the project.

### Key Highlights

- ✅ **Easy-to-use interface** - Clean, intuitive design
- ✅ **Automatic calculations** - Real-time computation of all correction factors
- ✅ **Chamber library** - Pre-loaded detector library with automatic kQ selection
- ✅ **Professional reports** - Generate PDF reports with your hospital logo
- ✅ **Cross-platform** - Works on Linux and Windows
- ✅ **Multi-language** - English and French support
- ✅ **Modern UI** - Dark/Light themes with customizable color schemes

---

## ✨ Features

### Core Functionality
- **TRS-398 Calculations**: Full support for photon and electron beam calibrations
- **Chamber Library**: Pre-loaded detector database with automatic kQ factor interpolation
- **Real-time Calculations**: Live computation of correction factors as you type
- **Measurement History**: Track all your past measurements with filtering and search
- **PDF Reports**: Generate professional calibration reports with digital signatures
- **CSV Export**: Export data for further analysis in Excel or other tools

### User Experience
- **Dashboard**: Statistics, trends, and visual analytics
- **Settings Management**: Comprehensive configuration options
- **Keyboard Shortcuts**: Power user features for faster workflow
- **Auto-save**: Draft measurements saved automatically
- **Toast Notifications**: Non-intrusive feedback for all actions
- **Responsive Design**: Works great on desktop, tablet, and mobile

### Technical Features
- **SQLite Database**: Lightweight, local data storage
- **RESTful API**: Clean API design for future integrations
- **Multi-language Support**: i18n ready with EN/FR translations
- **Theme System**: Dark/Light modes with customizable accent colors
- **Backup & Restore**: Database backup and restore functionality

---

## 📦 Requirements

### Minimum Requirements
- **.NET 8.0 Runtime** or SDK ([Download](https://dotnet.microsoft.com/download))
- **Web Browser** (Chrome, Firefox, Edge, or Safari)
- **2 GB RAM**
- **100 MB disk space**

### For Building from Source
- **.NET 8.0 SDK**
- **Git** (for cloning the repository)

---

## 🚀 Quick Start

### Option 1: Run from Source (Recommended for Development)

```bash
# Clone the repository
git clone https://github.com/El-yacine/TRS398.git
cd TRS398

# Navigate to server directory
cd server

# Run the application
dotnet run --project MyQC.WebAPI.csproj --urls http://localhost:8000
```

Then open your browser and navigate to: **http://localhost:8000**

### Option 2: Use Platform-Specific Installers

- **Linux**: See [Linux Installation Guide](linux/README.md)
- **Windows**: See [Windows Installation Guide](windows/README.md)

---

## 💻 Installation

### Linux Installation

TRS-398 Pro provides multiple installation options for Linux:

#### Option 1: GUI Installer (Recommended)
```bash
cd linux
python3 installer-gui.py
```

#### Option 2: Command-Line Installer
```bash
cd linux
chmod +x install.sh
./install.sh
```

#### Option 3: Manual Installation
```bash
cd server
dotnet publish -c Release -o ../publish
cd ../publish
dotnet MyQC.WebAPI.dll --urls http://localhost:8000
```

For detailed Linux installation instructions, see [linux/README.md](linux/README.md)

### Windows Installation

TRS-398 Pro provides multiple installation options for Windows:

#### Option 1: PowerShell GUI Installer (Recommended)
```powershell
cd windows
.\Install-TRS398Pro.ps1
```

#### Option 2: Batch Installer
```cmd
cd windows
install.bat
```

#### Option 3: Manual Installation
```powershell
cd server
dotnet publish -c Release -o ..\publish
cd ..\publish
dotnet MyQC.WebAPI.dll --urls http://localhost:8000
```

For detailed Windows installation instructions, see [windows/README.md](windows/README.md)

---

## 📖 Usage

### Making a Measurement

1. **Select Mode**: Choose Photon or Electron TRS-398
2. **Enter Energy**: Select or enter beam energy (e.g., 6X, 10X, 15X)
3. **Select Chamber**: Choose from the chamber library
4. **Enter Beam Quality**:
   - For Photons: Enter TPR20,10 (kQ calculated automatically)
   - For Electrons: Enter R50 (kQ calculated automatically)
5. **Environmental Conditions**: Enter Temperature (°C) and Pressure (mBar)
6. **Take Measurements**:
   - **M+**: Three readings with positive polarity (+300V)
   - **M-**: Three readings with negative polarity (-300V)
   - **M100V**: Three readings at 100V
7. **Review Results**: Check Ecart (%) - should be within ±2% for PASS
8. **Save**: Click "Save Measurement" to store in database

### Viewing History

- Click "History" in the navigation
- Filter by energy, mode, or status
- Search by user, energy, or notes
- Export individual PDF reports
- Export all data as CSV

### Generating Reports

- Click "PDF" button in History table for individual reports
- Reports include all measurement data, corrections, and results
- Add your hospital logo by placing `logo.png` in `server/wwwroot/logos/`

### Settings & Configuration

Access Settings (⚙️) to configure:
- **Chamber Library**: Select and manage ionization chambers
- **Clinic Information**: Organization details
- **LINAC Configuration**: Machine settings
- **Defaults**: Pre-filled values for measurements
- **Display**: Theme, colors, and appearance
- **Data**: Backup and restore options

---

## 📁 Project Structure

```
TRS398/
├── README.md                 # This file
├── LICENSE                   # License file
├── .gitignore               # Git ignore rules
├── detector_library.json    # Chamber detector library
│
├── server/                  # Core application (shared)
│   ├── Program.cs           # API endpoints and startup
│   ├── MyQC.WebAPI.csproj  # Project file
│   ├── Data/               # Database context
│   ├── Models/             # Data models
│   ├── Services/           # Business logic
│   │   ├── TRSService.cs   # TRS-398 calculations
│   │   └── PdfReportService.cs  # PDF generation
│   └── wwwroot/            # Frontend files
│       ├── index.html      # Main calibration page
│       ├── history.html    # History view
│       ├── dashboard.html  # Statistics dashboard
│       └── logos/          # Hospital logos for PDFs
│
├── linux/                   # Linux-specific files
│   ├── README.md           # Linux installation guide
│   ├── install.sh          # Command-line installer
│   └── installer-gui.py    # GUI installer
│
├── windows/                 # Windows-specific files
│   ├── README.md           # Windows installation guide
│   ├── install.bat         # Batch installer
│   └── Install-TRS398Pro.ps1  # PowerShell installer
│
├── scripts/                 # Build scripts
│   ├── linux/              # Linux build scripts
│   └── windows/           # Windows build scripts
│
└── docs/                    # Documentation
    ├── DEVELOPMENT_OPPORTUNITIES.md
    ├── INSTALLER_GUIDE.md
    └── ...
```

---

## 📚 Documentation

### User Documentation
- [Linux Installation Guide](docs/INSTALL_LINUX.md)
- [Windows Installation Guide](docs/INSTALL_WINDOWS.md)
- [Installer Guide](docs/INSTALLER_GUIDE.md)

### Developer Documentation
- [Development Opportunities](docs/DEVELOPMENT_OPPORTUNITIES.md)
- [Project Guidelines](AGENTS.md)

### API Documentation
The application exposes a RESTful API. Key endpoints:
- `GET /api/health` - Health check
- `POST /api/trs/calculate` - Calculate TRS-398 results
- `POST /api/trs/save` - Save measurement
- `GET /api/trs/measurements` - Get all measurements
- `GET /api/stats/overview` - Get statistics
- `GET /api/trs/report/{id}` - Generate PDF report

---

## 🛠️ Development

### Prerequisites
- .NET 8.0 SDK
- Git
- Code editor (VS Code, Visual Studio, or Rider)

### Building from Source

```bash
# Clone repository
git clone https://github.com/El-yacine/TRS398.git
cd TRS398

# Build the project
cd server
dotnet build

# Run in development mode
dotnet run --project MyQC.WebAPI.csproj --urls http://localhost:8000
```

### Running Tests

```bash
# Run unit tests (when available)
dotnet test
```

### Contributing

See [DEVELOPMENT_OPPORTUNITIES.md](docs/DEVELOPMENT_OPPORTUNITIES.md) for a list of features you can contribute to.

---

## 🤝 Contributing

Contributions are welcome! Here's how you can help:

1. **Fork the repository**
2. **Create a feature branch**: `git checkout -b feature/amazing-feature`
3. **Commit your changes**: `git commit -m 'Add amazing feature'`
4. **Push to the branch**: `git push origin feature/amazing-feature`
5. **Open a Pull Request**

### Areas for Contribution
- 🐛 Bug fixes
- ✨ New features
- 📝 Documentation improvements
- 🎨 UI/UX enhancements
- 🌍 Translations
- 🧪 Tests

---

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

## 🙏 Acknowledgments

- **IAEA TRS-398**: Based on the IAEA Technical Reports Series No. 398
- **QuestPDF**: For PDF generation capabilities
- **.NET Community**: For excellent tools and libraries

---

## 📞 Support

- **Issues**: [GitHub Issues](https://github.com/El-yacine/TRS398/issues)
- **Discussions**: [GitHub Discussions](https://github.com/El-yacine/TRS398/discussions)

---

## 🗺️ Roadmap

See [DEVELOPMENT_OPPORTUNITIES.md](docs/DEVELOPMENT_OPPORTUNITIES.md) for a comprehensive list of planned features and improvements.

### Upcoming Features
- 🔐 Enhanced user authentication
- 📊 Advanced analytics and charts
- 📱 Progressive Web App (PWA)
- ☁️ Cloud backup and sync
- 🔗 DICOM/HL7 integration

---

<div align="center">

**Made with ❤️ for Medical Physicists**

Created and maintained by **Yacine El Attaoui — Medical Physicist**

**Contributors:** Aziz Oustous

[⭐ Star this repo](https://github.com/El-yacine/TRS398) if you find it useful!

</div>
