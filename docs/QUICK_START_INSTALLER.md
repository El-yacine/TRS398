# Quick Start: Building TRS-398 Professional Installer

## Prerequisites

1. Install **.NET 8.0 SDK**: https://dotnet.microsoft.com/download/dotnet/8.0
2. Install **WiX Toolset v3.11+**: https://wixtoolset.org/releases/
3. (Optional) Install **Visual Studio 2022** for easier building

## Build Steps

### Option 1: Automated Build (Recommended)

1. Open **PowerShell** in the project directory
2. Run:
   ```powershell
   .\build-installer.ps1
   ```
3. Find your installer at: `TRS398Installer\bin\Release\TRS398Installer.msi`

### Option 2: Manual Build

1. **Publish Server:**
   ```powershell
   cd server
   dotnet publish -c Release -r win-x64 --self-contained false -o ..\publish\server
   cd ..
   ```

2. **Publish Desktop:**
   ```powershell
   cd TRS398Desktop
   dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o ..\publish\TRS398Desktop
   cd ..
   ```

3. **Copy Detector Library:**
   ```powershell
   Copy-Item detector_library.json -Destination publish\server\
   ```

4. **Build Installer:**
   - Open `TRS398Installer\TRS398Installer.wixproj` in Visual Studio
   - Right-click project → Properties → Build
   - Set `PublishDir` to: `$(SolutionDir)..\publish\`
   - Build (F6)

## Testing the Installer

1. Run the generated `.msi` file
2. Follow the installation wizard
3. Launch from Desktop shortcut or Start Menu
4. The application should open in your browser automatically

## Distribution

The installer requires:
- **Windows 10/11** (64-bit)
- **.NET 8.0 Runtime** (will prompt if missing)

Distribute both:
- `TRS398Installer.msi` - The installer
- Link to .NET 8.0 Runtime download (or bundle it)

## Troubleshooting

**"WiX Toolset not found"**
- Install WiX from: https://wixtoolset.org/
- Restart PowerShell/Visual Studio after installation

**"MSBuild not found"**
- Install Visual Studio Build Tools
- Or use Visual Studio IDE to build

**Files missing in installer**
- Check that `PublishDir` property points to correct location
- Verify all files exist in `publish` directory
- Rebuild both server and desktop projects

## Next Steps

- Add custom application icon (see INSTALLER_GUIDE.md)
- Customize installer UI (see TRS398Installer/README.md)
- Create self-contained build (no .NET Runtime required, but larger size)

