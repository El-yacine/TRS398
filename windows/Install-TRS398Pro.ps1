#Requires -Version 5.1
<#
.SYNOPSIS
    TRS-398 Pro Installer for Windows (PowerShell)
    
.DESCRIPTION
    Installs TRS-398 Pro Medical Physics Calibration System
    
.NOTES
    Version: 2.0.0
    Author: TRS-398 Pro Team
#>

[CmdletBinding()]
param(
    [switch]$Silent,
    [switch]$NoDesktopShortcut,
    [string]$InstallPath = "$env:LOCALAPPDATA\TRS398Pro"
)

$ErrorActionPreference = "Stop"

# Application info
$AppName = "TRS-398 Pro"
$AppVersion = "2.0.0"
$AppPort = 8000

# Paths
$StartMenuPath = "$env:APPDATA\Microsoft\Windows\Start Menu\Programs"
$DesktopPath = [Environment]::GetFolderPath("Desktop")

function Write-Banner {
    Write-Host ""
    Write-Host "===============================================================" -ForegroundColor Cyan
    Write-Host "                                                               " -ForegroundColor Cyan
    Write-Host "              TRS-398 Pro Installer v$AppVersion                    " -ForegroundColor Cyan
    Write-Host "          Medical Physics Calibration System                   " -ForegroundColor Cyan
    Write-Host "                                                               " -ForegroundColor Cyan
    Write-Host "===============================================================" -ForegroundColor Cyan
    Write-Host ""
}

function Write-Step {
    param([string]$Message)
    Write-Host "[+] " -ForegroundColor Green -NoNewline
    Write-Host $Message
}

function Write-Info {
    param([string]$Message)
    Write-Host "[i] " -ForegroundColor Blue -NoNewline
    Write-Host $Message
}

function Write-Warn {
    param([string]$Message)
    Write-Host "[!] " -ForegroundColor Yellow -NoNewline
    Write-Host $Message
}

function Write-Err {
    param([string]$Message)
    Write-Host "[x] " -ForegroundColor Red -NoNewline
    Write-Host $Message
}

function Test-DotNet {
    Write-Info "Checking for .NET runtime..."
    
    try {
        $dotnetVersion = & dotnet --version 2>$null
        if ($LASTEXITCODE -eq 0) {
            Write-Step ".NET found: version $dotnetVersion"
            return $true
        }
    }
    catch {
        # dotnet not found
    }
    
    return $false
}

function Install-DotNet {
    Write-Info "Installing .NET 8.0 runtime..."
    
    $installerUrl = "https://dot.net/v1/dotnet-install.ps1"
    $installerPath = "$env:TEMP\dotnet-install.ps1"
    
    try {
        Invoke-WebRequest -Uri $installerUrl -OutFile $installerPath -UseBasicParsing
        & $installerPath -Channel 8.0 -InstallDir "$env:LOCALAPPDATA\Microsoft\dotnet"
        
        # Add to PATH for current session
        $env:PATH = "$env:LOCALAPPDATA\Microsoft\dotnet;$env:PATH"
        
        # Add to user PATH permanently
        $userPath = [Environment]::GetEnvironmentVariable("PATH", "User")
        if ($userPath -notlike "*dotnet*") {
            [Environment]::SetEnvironmentVariable("PATH", "$env:LOCALAPPDATA\Microsoft\dotnet;$userPath", "User")
        }
        
        Remove-Item $installerPath -Force
        Write-Step ".NET 8.0 installed successfully"
        return $true
    }
    catch {
        Write-Err "Failed to install .NET: $_"
        return $false
    }
}

function New-Directories {
    Write-Info "Creating installation directories..."
    
    $dirs = @(
        $InstallPath,
        "$InstallPath\wwwroot\logos",
        "$InstallPath\data"
    )
    
    foreach ($dir in $dirs) {
        if (-not (Test-Path $dir)) {
            New-Item -ItemType Directory -Path $dir -Force | Out-Null
        }
    }
    
    Write-Step "Directories created"
}

function Copy-AppFiles {
    Write-Info "Copying application files..."
    
    $scriptDir = Split-Path -Parent $MyInvocation.ScriptName
    if (-not $scriptDir) {
        $scriptDir = Get-Location
    }
    
    $serverDir = Join-Path $scriptDir "server"
    
    if (Test-Path $serverDir) {
        Copy-Item -Path "$serverDir\*" -Destination $InstallPath -Recurse -Force
        Write-Step "Server files copied"
    }
    else {
        throw "Server directory not found at $serverDir"
    }
    
    $detectorLib = Join-Path $scriptDir "detector_library.json"
    if (Test-Path $detectorLib) {
        Copy-Item -Path $detectorLib -Destination $InstallPath -Force
        Write-Step "Detector library copied"
    }
}

function Build-Application {
    Write-Info "Building application..."
    
    Push-Location $InstallPath
    try {
        & dotnet restore 2>&1 | Out-Null
        & dotnet build --configuration Release 2>&1 | Out-Null
        
        if ($LASTEXITCODE -ne 0) {
            throw "Build failed"
        }
        
        Write-Step "Application built successfully"
    }
    finally {
        Pop-Location
    }
}

function New-Launcher {
    Write-Info "Creating launcher..."
    
    $launcherContent = @"
@echo off
setlocal
set "INSTALL_DIR=$InstallPath"
set "PORT=$AppPort"

REM Check if already running
tasklist /FI "IMAGENAME eq dotnet.exe" 2>NUL | find /I "dotnet.exe" >NUL
if %ERRORLEVEL% equ 0 (
    echo TRS-398 Pro is already running!
    start http://localhost:%PORT%
    exit /b 0
)

echo Starting TRS-398 Pro...
cd /d "%INSTALL_DIR%"
start /B dotnet run --urls http://localhost:%PORT%

echo Waiting for server to start...
timeout /t 5 /nobreak >nul

echo Opening browser...
start http://localhost:%PORT%

echo.
echo TRS-398 Pro is running at http://localhost:%PORT%
echo Close this window to stop the server.
pause >nul
"@
    
    $launcherPath = Join-Path $InstallPath "TRS398Pro.bat"
    Set-Content -Path $launcherPath -Value $launcherContent -Encoding ASCII
    
    Write-Step "Launcher created"
    return $launcherPath
}

function New-Shortcuts {
    param([string]$LauncherPath)
    
    $WshShell = New-Object -ComObject WScript.Shell
    
    # Start Menu shortcut
    Write-Info "Creating Start Menu shortcut..."
    $startMenuShortcut = Join-Path $StartMenuPath "TRS-398 Pro.lnk"
    $shortcut = $WshShell.CreateShortcut($startMenuShortcut)
    $shortcut.TargetPath = $LauncherPath
    $shortcut.WorkingDirectory = $InstallPath
    $shortcut.Description = "TRS-398 Pro - Medical Physics Calibration System"
    $shortcut.Save()
    Write-Step "Start Menu shortcut created"
    
    # Desktop shortcut
    if (-not $NoDesktopShortcut) {
        Write-Info "Creating Desktop shortcut..."
        $desktopShortcut = Join-Path $DesktopPath "TRS-398 Pro.lnk"
        $shortcut = $WshShell.CreateShortcut($desktopShortcut)
        $shortcut.TargetPath = $LauncherPath
        $shortcut.WorkingDirectory = $InstallPath
        $shortcut.Description = "TRS-398 Pro - Medical Physics Calibration System"
        $shortcut.Save()
        Write-Step "Desktop shortcut created"
    }
}

function New-Uninstaller {
    Write-Info "Creating uninstaller..."
    
    $uninstallerContent = @"
@echo off
echo TRS-398 Pro Uninstaller
echo =======================
echo.
set /p CONFIRM="Are you sure you want to uninstall TRS-398 Pro? (y/N): "
if /i not "%CONFIRM%"=="y" (
    echo Uninstall cancelled.
    pause
    exit /b 0
)

echo.
echo Removing application files...
rmdir /S /Q "$InstallPath" 2>nul
del "$StartMenuPath\TRS-398 Pro.lnk" 2>nul
del "$DesktopPath\TRS-398 Pro.lnk" 2>nul

echo.
echo TRS-398 Pro has been uninstalled.
pause
"@
    
    $uninstallerPath = Join-Path $InstallPath "Uninstall.bat"
    Set-Content -Path $uninstallerPath -Value $uninstallerContent -Encoding ASCII
    
    Write-Step "Uninstaller created: $uninstallerPath"
}

# Main installation
function Install-TRS398Pro {
    Write-Banner
    
    Write-Host "This installer will set up TRS-398 Pro on your system."
    Write-Host ""
    Write-Host "Installation directory: $InstallPath"
    Write-Host ""
    
    if (-not $Silent) {
        $continue = Read-Host "Continue with installation? (Y/n)"
        if ($continue -eq 'n' -or $continue -eq 'N') {
            Write-Host "Installation cancelled."
            return
        }
    }
    
    Write-Host ""
    Write-Host "Starting installation..."
    Write-Host ""
    
    # Check/install .NET
    if (-not (Test-DotNet)) {
        Write-Warn ".NET runtime not found."
        
        if (-not $Silent) {
            $installDotNet = Read-Host "Install .NET 8.0? (Y/n)"
            if ($installDotNet -ne 'n' -and $installDotNet -ne 'N') {
                if (-not (Install-DotNet)) {
                    Write-Err ".NET installation failed. Please install manually from:"
                    Write-Host "https://dotnet.microsoft.com/download/dotnet/8.0"
                    return
                }
            }
            else {
                Write-Err ".NET is required to run TRS-398 Pro"
                return
            }
        }
        else {
            if (-not (Install-DotNet)) {
                throw ".NET installation failed"
            }
        }
    }
    
    # Create directories
    New-Directories
    
    # Copy files
    Copy-AppFiles
    
    # Build application
    Build-Application
    
    # Create launcher
    $launcherPath = New-Launcher
    
    # Create shortcuts
    New-Shortcuts -LauncherPath $launcherPath
    
    # Create uninstaller
    New-Uninstaller
    
    Write-Host ""
    Write-Host "===============================================================" -ForegroundColor Green
    Write-Host "                                                               " -ForegroundColor Green
    Write-Host "            Installation completed successfully!              " -ForegroundColor Green
    Write-Host "                                                               " -ForegroundColor Green
    Write-Host "===============================================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "You can now run TRS-398 Pro by:"
    Write-Host ""
    Write-Host "  1. Desktop shortcut: Double-click 'TRS-398 Pro' on your desktop"
    Write-Host "  2. Start Menu: Search for 'TRS-398 Pro'"
    Write-Host "  3. Browser: http://localhost:$AppPort"
    Write-Host ""
    Write-Host "To uninstall: Run $InstallPath\Uninstall.bat"
    Write-Host ""
    
    if (-not $Silent) {
        $launch = Read-Host "Launch TRS-398 Pro now? (Y/n)"
        if ($launch -ne 'n' -and $launch -ne 'N') {
            Start-Process -FilePath $launcherPath
        }
    }
}

# Run installer
try {
    Install-TRS398Pro
}
catch {
    Write-Err "Installation failed: $_"
    if (-not $Silent) {
        Read-Host "Press Enter to exit"
    }
    exit 1
}

