@echo off
echo ========================================
echo TRS-398 Pro Windows Installer Builder
echo ========================================
echo.

:: Check for .NET SDK
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo ERROR: .NET SDK not found!
    echo Please install .NET 8.0 SDK from https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

echo Building Windows installer...
echo.

:: Navigate to installer directory
cd /d "%~dp0installer"

:: Clean previous builds
echo Cleaning previous builds...
dotnet clean -c Release >nul 2>&1

:: Publish for Windows x64
echo Publishing for Windows x64...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o "..\dist\windows"

if errorlevel 1 (
    echo.
    echo ERROR: Build failed!
    pause
    exit /b 1
)

:: Copy app files
echo Copying application files...
xcopy /E /I /Y "..\server" "..\dist\windows\AppFiles\server" >nul
copy /Y "..\detector_library.json" "..\dist\windows\AppFiles\" >nul

echo.
echo ========================================
echo Build successful!
echo ========================================
echo.
echo Installer location: dist\windows\TRS398ProSetup.exe
echo.
echo To install, run TRS398ProSetup.exe on Windows.
echo.
pause

