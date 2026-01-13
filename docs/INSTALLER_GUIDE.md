# TRS-398 Professional - Installation Guide

This guide explains how to build and create the Windows installer for TRS-398 Professional.

## Prerequisites

### Required Software

1. **.NET 8.0 SDK** - [Download here](https://dotnet.microsoft.com/download/dotnet/8.0)
2. **WiX Toolset v3.11 or later** - [Download here](https://wixtoolset.org/releases/)
3. **Visual Studio 2022** (optional, for building installer via IDE) - [Download here](https://visualstudio.microsoft.com/)

### Optional but Recommended

- **Icon Editor** (IcoFX, GIMP, etc.) - For creating application icons

## Building the Installer

### Method 1: Using PowerShell Script (Recommended on Windows)

1. Open **PowerShell** as Administrator
2. Navigate to the project directory:
   ```powershell
   cd C:\path\to\TRS_398
   ```
3. Run the build script:
   ```powershell
   .\build-installer.ps1
   ```

The script will:
- Build and publish the server application
- Build and publish the desktop wrapper
- Create the Windows installer (MSI file)

### Method 2: Using Bash Script (Cross-platform)

On Linux or Mac (for cross-compilation to Windows):

```bash
chmod +x build-installer.sh
./build-installer.sh Release x64
```

This will create the application files, but you'll need Windows with WiX to create the installer.

### Method 3: Manual Build Steps

1. **Publish the server application:**
   ```powershell
   cd server
   dotnet publish -c Release -r win-x64 --self-contained false -o ..\publish\server
   ```

2. **Publish the desktop application:**
   ```powershell
   cd ..\TRS398Desktop
   dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o ..\publish\TRS398Desktop
   ```

3. **Copy detector library:**
   ```powershell
   Copy-Item detector_library.json -Destination ..\publish\server\
   ```

4. **Build the installer:**
   - Open `TRS398Installer\TRS398Installer.wixproj` in Visual Studio
   - Set the `PublishDir` property to point to your publish directory
   - Build the project (F6)

## Installer Features

The Windows installer includes:

- ✅ **Professional UI** - Modern installation wizard
- ✅ **Desktop Shortcut** - Quick access from desktop
- ✅ **Start Menu Entry** - Available in Windows Start Menu
- ✅ **Automatic .NET Runtime Check** - Verifies .NET 8.0 is installed
- ✅ **Clean Uninstall** - Removes all files and registry entries
- ✅ **Per-Machine Installation** - Installs for all users

## Installation Location

By default, the application installs to:
```
C:\Program Files\TRS398Pro\
├── server\          (Web API files)
└── Desktop\         (Desktop wrapper executable)
```

## Creating Application Icons

To add a custom icon:

1. Create or obtain a `.ico` file (256x256 recommended)
2. Place it at: `TRS398Desktop\app.ico`
3. Rebuild the desktop application
4. The installer will automatically use this icon

## Troubleshooting

### "WiX Toolset not found"

- Install WiX Toolset from https://wixtoolset.org/
- Ensure it's added to your PATH or install to default location

### "MSBuild not found"

- Install Visual Studio Build Tools or full Visual Studio
- Or use Visual Studio to build the installer project directly

### "Failed to publish server application"

- Ensure .NET 8.0 SDK is installed: `dotnet --version`
- Check that all project references are correct
- Verify the server project builds: `dotnet build server\MyQC.WebAPI.csproj`

### Installer builds but doesn't include all files

- Check that the `PublishDir` property in the WiX project points to the correct location
- Verify all files exist in the publish directory
- Use `heat.exe` (WiX tool) to automatically harvest files if needed

## Distribution

After building, you'll have:

- **Installer**: `TRS398Installer\bin\Release\TRS398Installer.msi`
- **Application Files**: `publish\` directory (for manual distribution if needed)

### Requirements for End Users

End users need:
- **Windows 10/11** (64-bit)
- **.NET 8.0 Runtime** - [Download here](https://dotnet.microsoft.com/download/dotnet/8.0)
  - The installer will check for this and prompt if missing

## Advanced: Self-Contained Build

To create a fully self-contained executable (no .NET Runtime required):

Modify the build script to use `--self-contained true`:

```powershell
dotnet publish $desktopProject `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -o $desktopPublishDir
```

**Note**: This will significantly increase the installer size (~100MB+ vs ~5MB).

## Support

For issues or questions:
1. Check the main README.md
2. Review build logs for specific errors
3. Ensure all prerequisites are installed correctly

