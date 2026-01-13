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
echo.

:: Start the server in background
start /B dotnet run --urls http://localhost:8000

:: Wait for server to start
echo Waiting for server to start...
timeout /t 4 /nobreak >nul

:: Open browser
echo Opening browser...
start http://localhost:8000

echo.
echo ═══════════════════════════════════════════════════════════
echo  TRS-398 Pro is running at: http://localhost:8000
echo  
echo  Keep this window open while using the application.
echo  Press any key to stop the server and exit.
echo ═══════════════════════════════════════════════════════════
echo.

pause >nul

:: Kill dotnet processes
taskkill /F /IM dotnet.exe >nul 2>&1

echo.
echo Server stopped. Goodbye!
timeout /t 2 >nul

