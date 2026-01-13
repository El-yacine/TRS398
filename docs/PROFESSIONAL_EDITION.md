# TRS-398 Professional Edition

## Overview

TRS-398 Professional is a desktop application version of the TRS-398 calibration tool with a professional Windows installer. It provides:

- 🖥️ **Desktop Application** - Windows Forms wrapper with system tray integration
- 📦 **Professional Installer** - WiX-based MSI installer with modern UI
- 🚀 **Easy Distribution** - Single installer file for end users
- 🎯 **User-Friendly** - Automatic browser launch and system tray access

## What's Included

### Desktop Application (`TRS398Desktop`)

A Windows Forms application that:
- Launches the web server automatically
- Opens the browser to the application
- Provides system tray icon for quick access
- Handles clean shutdown of all processes

### Installer (`TRS398Installer`)

A WiX-based installer that:
- Installs to `Program Files\TRS398Pro`
- Creates desktop and Start Menu shortcuts
- Checks for .NET 8.0 Runtime requirement
- Provides clean uninstall

### Build Scripts

- `build-installer.ps1` - PowerShell script for Windows
- `build-installer.sh` - Bash script for cross-platform building

## Project Structure

```
TRS_398/
├── server/                    # Web API (existing)
│   ├── MyQC.WebAPI.csproj
│   └── ...
├── TRS398Desktop/             # NEW: Desktop wrapper
│   ├── TRS398Desktop.csproj
│   ├── Program.cs
│   └── app.ico
├── TRS398Installer/           # NEW: WiX installer
│   ├── TRS398Installer.wixproj
│   ├── Product.wxs
│   └── README.md
├── build-installer.ps1        # NEW: Build script (Windows)
├── build-installer.sh         # NEW: Build script (Linux/Mac)
├── INSTALLER_GUIDE.md         # NEW: Detailed installer guide
└── QUICK_START_INSTALLER.md   # NEW: Quick start guide
```

## Building the Professional Edition

### Quick Start

```powershell
# On Windows
.\build-installer.ps1
```

```bash
# On Linux/Mac (cross-compile)
./build-installer.sh
```

### Output

After building, you'll have:
- **Installer**: `TRS398Installer\bin\Release\TRS398Installer.msi`
- **Application Files**: `publish\` directory

## Installation Flow

1. **User runs installer** → MSI file
2. **Installer checks** → .NET 8.0 Runtime (prompts if missing)
3. **Files installed** → `C:\Program Files\TRS398Pro\`
4. **Shortcuts created** → Desktop and Start Menu
5. **User launches** → Desktop shortcut or Start Menu
6. **Application starts** → Web server launches, browser opens
7. **System tray icon** → Quick access and exit

## Key Features

### Desktop Application Features

- ✅ Automatic server startup
- ✅ Browser auto-launch
- ✅ System tray integration
- ✅ Clean process management
- ✅ Error handling and user feedback
- ✅ Path resolution for installed vs. development mode

### Installer Features

- ✅ Professional installation wizard
- ✅ Customizable install location
- ✅ Desktop and Start Menu shortcuts
- ✅ .NET Runtime detection
- ✅ Clean uninstall
- ✅ Per-machine installation

## Requirements

### For Building

- .NET 8.0 SDK
- WiX Toolset v3.11+
- (Optional) Visual Studio 2022

### For End Users

- Windows 10/11 (64-bit)
- .NET 8.0 Runtime
- Web browser (Chrome, Edge, Firefox, etc.)

## Customization

### Application Icon

1. Create or obtain a `.ico` file
2. Place at: `TRS398Desktop\app.ico`
3. Rebuild desktop application

### Installer Branding

Edit `TRS398Installer\Product.wxs`:
- Product name and version
- Manufacturer information
- Install location
- UI theme

### Self-Contained Build

To create a build that doesn't require .NET Runtime:

Modify build script to use `--self-contained true`. This increases size significantly (~100MB+).

## Distribution

### Recommended Distribution Package

1. **TRS398Installer.msi** - The installer
2. **README.txt** - Instructions for users
3. **Link to .NET 8.0 Runtime** - If not bundling

### User Instructions

1. Install .NET 8.0 Runtime (if not already installed)
2. Run `TRS398Installer.msi`
3. Follow installation wizard
4. Launch from Desktop or Start Menu

## Technical Details

### How It Works

1. **Desktop App** (`TRS398.exe`) launches
2. Finds `MyQC.WebAPI.dll` in `..\server\` directory
3. Starts `dotnet` process with the DLL
4. Waits for server to be ready (health check)
5. Opens default browser to `http://localhost:8000`
6. Shows system tray icon for management

### Port Configuration

Default port: **8000**

To change, modify:
- `TRS398Desktop\Program.cs` - `AppUrl` constant
- `server\Program.cs` - Default URL binding

### Database Location

Database (`trs398.db`) is created in the `server` directory at runtime.

## Support and Documentation

- **Quick Start**: See `QUICK_START_INSTALLER.md`
- **Detailed Guide**: See `INSTALLER_GUIDE.md`
- **Installer Project**: See `TRS398Installer/README.md`
- **Main Application**: See `README.md`

## Future Enhancements

Potential improvements:
- [ ] Embedded web server (no separate dotnet process)
- [ ] Self-contained build option
- [ ] Auto-update functionality
- [ ] Custom branding/theme
- [ ] Silent installation support
- [ ] Portable version (no installer)

## License

Same as main application - provided as-is for medical physics calibration purposes.

