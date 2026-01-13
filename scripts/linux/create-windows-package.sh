#!/bin/bash
#
# Creates a Windows distribution package for TRS-398 Pro
# This creates a ZIP file that users can extract and run
#

echo "========================================"
echo "TRS-398 Pro Windows Package Builder"
echo "========================================"
echo ""

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
DIST_DIR="$SCRIPT_DIR/dist"
PACKAGE_DIR="$DIST_DIR/TRS398Pro-Windows"

# Clean previous
rm -rf "$PACKAGE_DIR"
mkdir -p "$PACKAGE_DIR"

echo "Copying application files..."

# Copy server files
cp -r "$SCRIPT_DIR/server/"* "$PACKAGE_DIR/"

# Copy detector library
cp "$SCRIPT_DIR/detector_library.json" "$PACKAGE_DIR/"

# Create launcher batch file
cat > "$PACKAGE_DIR/Start TRS-398 Pro.bat" << 'LAUNCHER'
@echo off
title TRS-398 Pro
cd /d "%~dp0"

echo.
echo  ╔═══════════════════════════════════════════════════════╗
echo  ║                                                       ║
echo  ║              TRS-398 Pro v2.0.0                      ║
echo  ║       Medical Physics Calibration System              ║
echo  ║                                                       ║
echo  ╚═══════════════════════════════════════════════════════╝
echo.

:: Check for .NET
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo [ERROR] .NET Runtime not found!
    echo.
    echo Please install .NET 8.0 Runtime from:
    echo https://dotnet.microsoft.com/download/dotnet/8.0
    echo.
    pause
    start https://dotnet.microsoft.com/download/dotnet/8.0
    exit /b 1
)

echo Starting server...
start /B dotnet run --urls http://localhost:8000
timeout /t 4 /nobreak >nul
start http://localhost:8000

echo.
echo TRS-398 Pro is running at: http://localhost:8000
echo Keep this window open. Press any key to stop.
echo.
pause >nul
taskkill /F /IM dotnet.exe >nul 2>&1
LAUNCHER

# Create install script
cat > "$PACKAGE_DIR/Install.bat" << 'INSTALL'
@echo off
title TRS-398 Pro Installer
setlocal enabledelayedexpansion

echo.
echo  ╔═══════════════════════════════════════════════════════╗
echo  ║                                                       ║
echo  ║         TRS-398 Pro Installer v2.0.0                 ║
echo  ║       Medical Physics Calibration System              ║
echo  ║                                                       ║
echo  ╚═══════════════════════════════════════════════════════╝
echo.

set "INSTALL_DIR=%LOCALAPPDATA%\TRS398Pro"

echo This will install TRS-398 Pro to:
echo   %INSTALL_DIR%
echo.
set /p CONFIRM="Continue? (Y/n): "
if /i "%CONFIRM%"=="n" (
    echo Installation cancelled.
    pause
    exit /b 0
)

echo.
echo [1/4] Checking .NET Runtime...
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo.
    echo [!] .NET 8.0 Runtime is required but not installed.
    echo.
    echo Opening download page...
    start https://dotnet.microsoft.com/download/dotnet/8.0
    echo.
    echo Please install .NET 8.0 Runtime and run this installer again.
    pause
    exit /b 1
)
echo     .NET found!

echo.
echo [2/4] Creating installation directory...
if exist "%INSTALL_DIR%" (
    rmdir /S /Q "%INSTALL_DIR%" >nul 2>&1
)
mkdir "%INSTALL_DIR%"

echo.
echo [3/4] Copying files...
xcopy /E /I /Y "%~dp0*" "%INSTALL_DIR%\" >nul
del "%INSTALL_DIR%\Install.bat" >nul 2>&1
echo     Files copied!

echo.
echo [4/4] Creating shortcuts...

:: Create Desktop shortcut using PowerShell
powershell -NoProfile -Command "$ws = New-Object -ComObject WScript.Shell; $s = $ws.CreateShortcut('%USERPROFILE%\Desktop\TRS-398 Pro.lnk'); $s.TargetPath = '%INSTALL_DIR%\Start TRS-398 Pro.bat'; $s.WorkingDirectory = '%INSTALL_DIR%'; $s.Description = 'TRS-398 Pro Medical Physics Calibration'; $s.Save()"

:: Create Start Menu shortcut
if not exist "%APPDATA%\Microsoft\Windows\Start Menu\Programs\TRS-398 Pro" (
    mkdir "%APPDATA%\Microsoft\Windows\Start Menu\Programs\TRS-398 Pro"
)
powershell -NoProfile -Command "$ws = New-Object -ComObject WScript.Shell; $s = $ws.CreateShortcut('%APPDATA%\Microsoft\Windows\Start Menu\Programs\TRS-398 Pro\TRS-398 Pro.lnk'); $s.TargetPath = '%INSTALL_DIR%\Start TRS-398 Pro.bat'; $s.WorkingDirectory = '%INSTALL_DIR%'; $s.Description = 'TRS-398 Pro Medical Physics Calibration'; $s.Save()"

echo     Shortcuts created!

echo.
echo ═══════════════════════════════════════════════════════════
echo  Installation Complete!
echo.
echo  TRS-398 Pro has been installed to:
echo    %INSTALL_DIR%
echo.
echo  Shortcuts created:
echo    - Desktop: TRS-398 Pro
echo    - Start Menu: TRS-398 Pro
echo.
echo  To start: Double-click the desktop shortcut
echo ═══════════════════════════════════════════════════════════
echo.

set /p LAUNCH="Launch TRS-398 Pro now? (Y/n): "
if /i not "%LAUNCH%"=="n" (
    start "" "%INSTALL_DIR%\Start TRS-398 Pro.bat"
)

echo.
echo Done!
pause
INSTALL

# Create uninstaller
cat > "$PACKAGE_DIR/Uninstall.bat" << 'UNINSTALL'
@echo off
title TRS-398 Pro Uninstaller

echo.
echo  TRS-398 Pro Uninstaller
echo  =======================
echo.

set "INSTALL_DIR=%LOCALAPPDATA%\TRS398Pro"

set /p CONFIRM="Are you sure you want to uninstall TRS-398 Pro? (y/N): "
if /i not "%CONFIRM%"=="y" (
    echo Uninstall cancelled.
    pause
    exit /b 0
)

echo.
echo Removing files...
rmdir /S /Q "%INSTALL_DIR%" >nul 2>&1
del "%USERPROFILE%\Desktop\TRS-398 Pro.lnk" >nul 2>&1
rmdir /S /Q "%APPDATA%\Microsoft\Windows\Start Menu\Programs\TRS-398 Pro" >nul 2>&1

echo.
echo TRS-398 Pro has been uninstalled.
pause
UNINSTALL

# Create README
cat > "$PACKAGE_DIR/README.txt" << 'README'
╔═══════════════════════════════════════════════════════════════╗
║                                                               ║
║              TRS-398 Pro v2.0.0                              ║
║         Medical Physics Calibration System                    ║
║                                                               ║
╚═══════════════════════════════════════════════════════════════╝

INSTALLATION
============

Option 1: Quick Install (Recommended)
-------------------------------------
1. Double-click "Install.bat"
2. Follow the on-screen instructions
3. Desktop and Start Menu shortcuts will be created

Option 2: Portable Use
----------------------
1. Extract this folder anywhere
2. Double-click "Start TRS-398 Pro.bat"
3. No installation needed!


REQUIREMENTS
============
- Windows 10/11
- .NET 8.0 Runtime (installer will prompt if missing)
  Download: https://dotnet.microsoft.com/download/dotnet/8.0


USAGE
=====
1. Start TRS-398 Pro using the desktop shortcut or Start Menu
2. A browser window will open at http://localhost:8000
3. Keep the console window open while using the application
4. Press any key in the console to stop the server


UNINSTALL
=========
Run "Uninstall.bat" or delete the installation folder.


SUPPORT
=======
For issues, please contact your system administrator.

README

echo "Creating ZIP archive..."
cd "$DIST_DIR"
rm -f TRS398Pro-Windows.zip
zip -r TRS398Pro-Windows.zip TRS398Pro-Windows/

echo ""
echo "========================================"
echo "Package created successfully!"
echo "========================================"
echo ""
echo "Output: $DIST_DIR/TRS398Pro-Windows.zip"
echo ""
echo "To install on Windows:"
echo "  1. Extract the ZIP file"
echo "  2. Run Install.bat"
echo ""
ls -lh "$DIST_DIR/TRS398Pro-Windows.zip"

