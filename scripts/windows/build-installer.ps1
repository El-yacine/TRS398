# Build script for TRS-398 Professional Installer
# This script builds the application and creates a Windows installer

param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64"
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "TRS-398 Professional Build Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if .NET SDK is installed
Write-Host "Checking .NET SDK..." -ForegroundColor Yellow
$dotnetVersion = dotnet --version
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: .NET SDK not found. Please install .NET 8.0 SDK." -ForegroundColor Red
    exit 1
}
Write-Host "Found .NET SDK version: $dotnetVersion" -ForegroundColor Green
Write-Host ""

# Set paths
$rootDir = $PSScriptRoot
$serverProject = Join-Path $rootDir "server\MyQC.WebAPI.csproj"
$desktopProject = Join-Path $rootDir "TRS398Desktop\TRS398Desktop.csproj"
$publishDir = Join-Path $rootDir "publish"
$installerProject = Join-Path $rootDir "TRS398Installer\TRS398Installer.wixproj"

# Clean previous builds
Write-Host "Cleaning previous builds..." -ForegroundColor Yellow
if (Test-Path $publishDir) {
    Remove-Item $publishDir -Recurse -Force
}
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
Write-Host ""

# Build and publish server
Write-Host "Building and publishing server application..." -ForegroundColor Yellow
$serverPublishDir = Join-Path $publishDir "server"
dotnet publish $serverProject `
    -c $Configuration `
    -r win-x64 `
    --self-contained false `
    -p:PublishSingleFile=false `
    -p:IncludeNativeLibrariesForSelfExtract=false `
    -o $serverPublishDir

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Failed to publish server application." -ForegroundColor Red
    exit 1
}
Write-Host "Server application published successfully." -ForegroundColor Green
Write-Host ""

# Build and publish desktop wrapper
Write-Host "Building and publishing desktop application..." -ForegroundColor Yellow
$desktopPublishDir = Join-Path $publishDir "TRS398Desktop"
dotnet publish $desktopProject `
    -c $Configuration `
    -r win-x64 `
    --self-contained false `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $desktopPublishDir

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Failed to publish desktop application." -ForegroundColor Red
    exit 1
}
Write-Host "Desktop application published successfully." -ForegroundColor Green
Write-Host ""

# Copy detector library to publish directory
Write-Host "Copying detector library..." -ForegroundColor Yellow
$detectorLib = Join-Path $rootDir "detector_library.json"
if (Test-Path $detectorLib) {
    Copy-Item $detectorLib -Destination $serverPublishDir -Force
    Write-Host "Detector library copied." -ForegroundColor Green
} else {
    Write-Host "WARNING: detector_library.json not found." -ForegroundColor Yellow
}
Write-Host ""

# Check if WiX Toolset is installed
Write-Host "Checking for WiX Toolset..." -ForegroundColor Yellow
$wixPath = "${env:ProgramFiles(x86)}\WiX Toolset v3.11\bin"
if (-not (Test-Path $wixPath)) {
    $wixPath = "${env:ProgramFiles}\WiX Toolset v3.11\bin"
}
if (-not (Test-Path $wixPath)) {
    Write-Host "WARNING: WiX Toolset not found. Installer will not be built." -ForegroundColor Yellow
    Write-Host "Please install WiX Toolset from: https://wixtoolset.org/" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Build completed. Files are in: $publishDir" -ForegroundColor Green
    exit 0
}

Write-Host "Found WiX Toolset at: $wixPath" -ForegroundColor Green
Write-Host ""

# Build installer
Write-Host "Building installer..." -ForegroundColor Yellow
$msbuildPath = "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
if (-not (Test-Path $msbuildPath)) {
    $msbuildPath = "${env:ProgramFiles}\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe"
}
if (-not (Test-Path $msbuildPath)) {
    $msbuildPath = "${env:ProgramFiles}\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
}
if (-not (Test-Path $msbuildPath)) {
    $msbuildPath = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe"
}

if (Test-Path $msbuildPath) {
    & $msbuildPath $installerProject `
        /p:Configuration=$Configuration `
        /p:Platform=$Platform `
        /p:PublishDir="$publishDir\" `
        /p:ProjectDir="$rootDir\"

    if ($LASTEXITCODE -eq 0) {
        $installerPath = Join-Path $rootDir "TRS398Installer\bin\$Configuration\TRS398Installer.msi"
        if (Test-Path $installerPath) {
            Write-Host ""
            Write-Host "========================================" -ForegroundColor Green
            Write-Host "Build completed successfully!" -ForegroundColor Green
            Write-Host "========================================" -ForegroundColor Green
            Write-Host "Installer: $installerPath" -ForegroundColor Cyan
            Write-Host "Application files: $publishDir" -ForegroundColor Cyan
            Write-Host ""
        } else {
            Write-Host "WARNING: Installer file not found at expected location." -ForegroundColor Yellow
        }
    } else {
        Write-Host "WARNING: Failed to build installer. Check WiX project configuration." -ForegroundColor Yellow
        Write-Host "Application files are available in: $publishDir" -ForegroundColor Green
    }
} else {
    Write-Host "WARNING: MSBuild not found. Cannot build installer." -ForegroundColor Yellow
    Write-Host "Application files are available in: $publishDir" -ForegroundColor Green
    Write-Host "You can manually build the installer using Visual Studio." -ForegroundColor Yellow
}

Write-Host ""

