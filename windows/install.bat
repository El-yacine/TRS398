@echo off
REM ============================================================
REM TRS-398 Pro Installer for Windows
REM Medical Physics Calibration System
REM Version 2.0.0
REM ============================================================

setlocal EnableDelayedExpansion

REM Colors using ANSI (Windows 10+)
set "GREEN=[92m"
set "RED=[91m"
set "YELLOW=[93m"
set "CYAN=[96m"
set "NC=[0m"

REM Installation paths
set "INSTALL_DIR=%LOCALAPPDATA%\TRS398Pro"
set "START_MENU=%APPDATA%\Microsoft\Windows\Start Menu\Programs"
set "DESKTOP=%USERPROFILE%\Desktop"

REM Application info
set "APP_NAME=TRS-398 Pro"
set "APP_VERSION=2.0.0"
set "APP_PORT=8000"

REM Print banner
echo.
echo %CYAN%===============================================================%NC%
echo %CYAN%                                                               %NC%
echo %CYAN%              TRS-398 Pro Installer v%APP_VERSION%                    %NC%
echo %CYAN%          Medical Physics Calibration System                   %NC%
echo %CYAN%                                                               %NC%
echo %CYAN%===============================================================%NC%
echo.

echo This installer will set up TRS-398 Pro on your system.
echo.
echo Installation directory: %INSTALL_DIR%
echo.

set /p CONTINUE="Continue with installation? (Y/n): "
if /i "%CONTINUE%"=="n" (
    echo Installation cancelled.
    pause
    exit /b 0
)

echo.
echo Starting installation...
echo.

REM Check for .NET
echo %GREEN%[i]%NC% Checking for .NET runtime...
where dotnet >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo %YELLOW%[!]%NC% .NET runtime not found.
    echo.
    echo Please install .NET 8.0 Runtime from:
    echo https://dotnet.microsoft.com/download/dotnet/8.0
    echo.
    echo After installing .NET, run this installer again.
    echo.
    set /p OPENURL="Open download page in browser? (Y/n): "
    if /i not "!OPENURL!"=="n" (
        start https://dotnet.microsoft.com/download/dotnet/8.0
    )
    pause
    exit /b 1
)

for /f "tokens=*" %%i in ('dotnet --version 2^>nul') do set DOTNET_VERSION=%%i
echo %GREEN%[+]%NC% .NET found: version %DOTNET_VERSION%

REM Create directories
echo %GREEN%[i]%NC% Creating installation directories...
if not exist "%INSTALL_DIR%" mkdir "%INSTALL_DIR%"
if not exist "%INSTALL_DIR%\wwwroot\logos" mkdir "%INSTALL_DIR%\wwwroot\logos"
if not exist "%INSTALL_DIR%\data" mkdir "%INSTALL_DIR%\data"
echo %GREEN%[+]%NC% Directories created

REM Copy files
echo %GREEN%[i]%NC% Copying application files...
set "SCRIPT_DIR=%~dp0"

if exist "%SCRIPT_DIR%server" (
    xcopy /E /I /Y "%SCRIPT_DIR%server\*" "%INSTALL_DIR%\" >nul
    echo %GREEN%[+]%NC% Server files copied
) else (
    echo %RED%[x]%NC% Server directory not found!
    pause
    exit /b 1
)

if exist "%SCRIPT_DIR%detector_library.json" (
    copy /Y "%SCRIPT_DIR%detector_library.json" "%INSTALL_DIR%\" >nul
    echo %GREEN%[+]%NC% Detector library copied
)

REM Build application
echo %GREEN%[i]%NC% Building application...
cd /d "%INSTALL_DIR%"
dotnet restore >nul 2>&1
dotnet build --configuration Release >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo %RED%[x]%NC% Build failed!
    pause
    exit /b 1
)
echo %GREEN%[+]%NC% Application built successfully

REM Create launcher batch file
echo %GREEN%[i]%NC% Creating launcher...
(
echo @echo off
echo setlocal
echo set "INSTALL_DIR=%INSTALL_DIR%"
echo set "PORT=%APP_PORT%"
echo.
echo REM Check if already running
echo tasklist /FI "IMAGENAME eq dotnet.exe" 2^>NUL ^| find /I "dotnet.exe" ^>NUL
echo if %%ERRORLEVEL%% equ 0 ^(
echo     echo TRS-398 Pro is already running!
echo     start http://localhost:%%PORT%%
echo     exit /b 0
echo ^)
echo.
echo echo Starting TRS-398 Pro...
echo cd /d "%%INSTALL_DIR%%"
echo start /B dotnet run --urls http://localhost:%%PORT%%
echo.
echo echo Waiting for server to start...
echo timeout /t 5 /nobreak ^>nul
echo.
echo echo Opening browser...
echo start http://localhost:%%PORT%%
echo.
echo echo TRS-398 Pro is running.
echo echo Close this window to stop the server.
echo pause ^>nul
) > "%INSTALL_DIR%\TRS398Pro.bat"

echo %GREEN%[+]%NC% Launcher created

REM Create Start Menu shortcut
echo %GREEN%[i]%NC% Creating Start Menu shortcut...
set "SHORTCUT=%START_MENU%\TRS-398 Pro.lnk"
powershell -Command "$ws = New-Object -ComObject WScript.Shell; $s = $ws.CreateShortcut('%SHORTCUT%'); $s.TargetPath = '%INSTALL_DIR%\TRS398Pro.bat'; $s.WorkingDirectory = '%INSTALL_DIR%'; $s.Description = 'TRS-398 Pro - Medical Physics Calibration System'; $s.Save()"
echo %GREEN%[+]%NC% Start Menu shortcut created

REM Create Desktop shortcut
echo %GREEN%[i]%NC% Creating Desktop shortcut...
set "DESKTOP_SHORTCUT=%DESKTOP%\TRS-398 Pro.lnk"
powershell -Command "$ws = New-Object -ComObject WScript.Shell; $s = $ws.CreateShortcut('%DESKTOP_SHORTCUT%'); $s.TargetPath = '%INSTALL_DIR%\TRS398Pro.bat'; $s.WorkingDirectory = '%INSTALL_DIR%'; $s.Description = 'TRS-398 Pro - Medical Physics Calibration System'; $s.Save()"
echo %GREEN%[+]%NC% Desktop shortcut created

REM Create uninstaller
echo %GREEN%[i]%NC% Creating uninstaller...
(
echo @echo off
echo echo TRS-398 Pro Uninstaller
echo echo =======================
echo echo.
echo set /p CONFIRM="Are you sure you want to uninstall TRS-398 Pro? (y/N): "
echo if /i not "%%CONFIRM%%"=="y" ^(
echo     echo Uninstall cancelled.
echo     pause
echo     exit /b 0
echo ^)
echo.
echo echo Removing application files...
echo rmdir /S /Q "%INSTALL_DIR%" 2^>nul
echo del "%START_MENU%\TRS-398 Pro.lnk" 2^>nul
echo del "%DESKTOP%\TRS-398 Pro.lnk" 2^>nul
echo.
echo echo TRS-398 Pro has been uninstalled.
echo pause
) > "%INSTALL_DIR%\Uninstall.bat"

echo %GREEN%[+]%NC% Uninstaller created

echo.
echo %GREEN%===============================================================%NC%
echo %GREEN%                                                               %NC%
echo %GREEN%            Installation completed successfully!              %NC%
echo %GREEN%                                                               %NC%
echo %GREEN%===============================================================%NC%
echo.
echo You can now run TRS-398 Pro by:
echo.
echo   1. Desktop shortcut: Double-click "TRS-398 Pro" on your desktop
echo   2. Start Menu: Search for "TRS-398 Pro"
echo   3. Direct: Run %INSTALL_DIR%\TRS398Pro.bat
echo   4. Browser: http://localhost:%APP_PORT%
echo.
echo To uninstall: Run %INSTALL_DIR%\Uninstall.bat
echo.

set /p LAUNCH="Launch TRS-398 Pro now? (Y/n): "
if /i not "%LAUNCH%"=="n" (
    start "" "%INSTALL_DIR%\TRS398Pro.bat"
)

pause

