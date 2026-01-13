# TRS-398 Pro - Windows Installation Guide

## 📦 Installation Options

TRS-398 Pro provides multiple installation methods for Windows systems.

---

## Option 1: PowerShell GUI Installer (Recommended) 🖥️

The easiest way to install TRS-398 Pro on Windows is using the PowerShell GUI installer.

### Requirements
- Windows 10 or later
- PowerShell 5.1 or higher (included with Windows)
- .NET 8.0 Runtime (will be checked automatically)

### Installation Steps

1. **Open PowerShell as Administrator**:
   - Right-click Start menu
   - Select "Windows PowerShell (Admin)" or "Terminal (Admin)"

2. **Navigate to the windows directory**:
   ```powershell
   cd path\to\TRS398\windows
   ```

3. **Enable script execution** (if needed):
   ```powershell
   Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
   ```

4. **Run the GUI installer**:
   ```powershell
   .\Install-TRS398Pro.ps1
   ```

5. **Follow the installation wizard**:
   - Choose installation directory
   - Select options (desktop shortcut, start menu shortcut)
   - Review and confirm installation

6. **Launch the application**:
   - Desktop shortcut (if created)
   - Or run from Start menu

---

## Option 2: Batch Installer 💻

For users who prefer a simple batch script.

### Installation Steps

1. **Navigate to the windows directory**:
   ```cmd
   cd path\to\TRS398\windows
   ```

2. **Run the installer**:
   ```cmd
   install.bat
   ```

3. **Follow the prompts**:
   - Confirm installation directory
   - Choose to create shortcuts

### Installation Locations

- **Application files**: `C:\Program Files\TRS398Pro\` (or custom location)
- **Desktop shortcut**: `Desktop\TRS-398 Pro.lnk`
- **Start menu**: `Start Menu\Programs\TRS-398 Pro\`

---

## Option 3: Manual Installation 🔧

For advanced users who want full control.

### Prerequisites

1. **Install .NET 8.0 Runtime**:
   - Download from: https://dotnet.microsoft.com/download/dotnet/8.0
   - Run the installer
   - Verify installation:
     ```cmd
     dotnet --version
     ```

### Installation Steps

1. **Build the application**:
   ```cmd
   cd server
   dotnet publish -c Release -o ..\publish
   ```

2. **Run the application**:
   ```cmd
   cd ..\publish
   dotnet MyQC.WebAPI.dll --urls http://localhost:8000
   ```

3. **Access the application**:
   Open your browser and navigate to: `http://localhost:8000`

---

## 🚀 Running the Application

### After Installation

If installed using the installer, you can:

- **Double-click desktop shortcut**
- **Search "TRS-398 Pro" in Start menu**
- **Run from command line**:
  ```cmd
  cd "C:\Program Files\TRS398Pro"
  dotnet MyQC.WebAPI.dll --urls http://localhost:8000
  ```

### As a Windows Service

To run as a background service, you can use NSSM (Non-Sucking Service Manager):

1. **Download NSSM**: https://nssm.cc/download

2. **Install the service**:
   ```cmd
   nssm install TRS398Pro "C:\Program Files\dotnet\dotnet.exe" "C:\Program Files\TRS398Pro\MyQC.WebAPI.dll --urls http://localhost:8000"
   ```

3. **Start the service**:
   ```cmd
   nssm start TRS398Pro
   ```

---

## 🔧 Configuration

### Custom Port

To run on a different port:

```cmd
dotnet MyQC.WebAPI.dll --urls http://localhost:YOUR_PORT
```

### Custom Installation Directory

The installer allows you to choose a custom installation directory. Default is:
- `C:\Program Files\TRS398Pro\`

### Firewall Configuration

If you want to access the application from other computers on your network:

1. **Open Windows Firewall**:
   - Search "Windows Defender Firewall" in Start menu

2. **Add an inbound rule**:
   - Click "Advanced settings"
   - Click "Inbound Rules" → "New Rule"
   - Select "Port" → "TCP"
   - Enter port number (default: 8000)
   - Allow the connection
   - Apply to all profiles
   - Name it "TRS-398 Pro"

---

## 🗑️ Uninstallation

### If Installed via Installer

1. **Open Control Panel** → **Programs and Features**

2. **Find "TRS-398 Pro"** and click **Uninstall**

   OR

3. **Manual removal**:
   ```cmd
   # Remove application files
   rmdir /s "C:\Program Files\TRS398Pro"
   
   # Remove shortcuts
   del "%USERPROFILE%\Desktop\TRS-398 Pro.lnk"
   rmdir /s "%APPDATA%\Microsoft\Windows\Start Menu\Programs\TRS-398 Pro"
   ```

### Backup Your Data

Before uninstalling, backup your database:

```cmd
copy "C:\Program Files\TRS398Pro\trs398.db" "%USERPROFILE%\Desktop\trs398-backup.db"
```

---

## 🐛 Troubleshooting

### Application Won't Start

**Check .NET installation**:
```cmd
dotnet --version
```
Should show 8.0 or higher.

**Check if port is in use**:
```cmd
netstat -ano | findstr :8000
```
If port is in use, change it in the startup command.

### PowerShell Execution Policy Error

If you see "execution of scripts is disabled":

```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

### Permission Errors

**Run as Administrator**:
- Right-click installer → "Run as administrator"

**Check file permissions**:
- Ensure you have write access to the installation directory

### Database Errors

**Reset database** (⚠️ This will delete all data):
```cmd
del "C:\Program Files\TRS398Pro\trs398.db"
# Restart the application - it will create a new database
```

### Antivirus Blocking

Some antivirus software may flag the installer. This is a false positive. You can:
- Add an exception for the installation directory
- Temporarily disable antivirus during installation

---

## 📝 Notes

- The database is stored at: `[Installation Directory]\trs398.db`
- The application runs on port 8000 by default
- All data is stored locally - no cloud connection required
- The application requires .NET 8.0 Runtime (not SDK)

---

## 🔗 Related Documentation

- [Main README](../README.md)
- [Development Guide](../docs/DEVELOPMENT_OPPORTUNITIES.md)
- [Installer Guide](../docs/INSTALLER_GUIDE.md)

---

## 💡 Tips

### Running on Startup

To run TRS-398 Pro automatically on Windows startup:

1. **Create a shortcut** in the Startup folder:
   - Press `Win + R`
   - Type: `shell:startup`
   - Copy the TRS-398 Pro shortcut here

### Accessing from Network

To access from other computers:

1. **Configure firewall** (see Configuration section)
2. **Find your IP address**:
   ```cmd
   ipconfig
   ```
3. **Access from other computer**: `http://YOUR_IP:8000`

---

**Need Help?** Open an issue on [GitHub](https://github.com/El-yacine/TRS398/issues)

