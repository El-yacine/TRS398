# TRS-398 Pro - Linux Installation Guide

## 📦 Installation Options

TRS-398 Pro provides multiple installation methods for Linux systems.

---

## Option 1: GUI Installer (Recommended) 🖥️

The easiest way to install TRS-398 Pro on Linux is using the graphical installer.

### Requirements
- Python 3.6 or higher
- Tkinter (usually included with Python)

### Installation Steps

1. **Navigate to the linux directory**:
   ```bash
   cd linux
   ```

2. **Run the GUI installer**:
   ```bash
   python3 installer-gui.py
   ```

3. **Follow the installation wizard**:
   - Choose installation directory
   - Select options (desktop shortcut, systemd service)
   - Review and confirm installation

4. **Launch the application**:
   - Desktop shortcut (if created)
   - Or run: `trs398-pro` from terminal

---

## Option 2: Command-Line Installer 💻

For users comfortable with the command line.

### Installation Steps

1. **Navigate to the linux directory**:
   ```bash
   cd linux
   ```

2. **Make the installer executable**:
   ```bash
   chmod +x install.sh
   ```

3. **Run the installer**:
   ```bash
   ./install.sh
   ```

4. **Follow the prompts**:
   - Confirm installation directory
   - Choose to create desktop shortcut
   - Choose to create systemd service

### Installation Locations

- **Application files**: `~/.local/share/trs398-pro/`
- **Binary**: `~/.local/bin/trs398-pro`
- **Desktop shortcut**: `~/.local/share/applications/trs398-pro.desktop`
- **Systemd service**: `/etc/systemd/system/trs398-pro.service` (if created)

---

## Option 3: Manual Installation 🔧

For advanced users who want full control.

### Prerequisites

1. **Install .NET 8.0 Runtime**:
   ```bash
   # Ubuntu/Debian
   wget https://dot.net/v1/dotnet-install.sh
   chmod +x dotnet-install.sh
   ./dotnet-install.sh --channel 8.0

   # Or use package manager
   sudo apt-get update
   sudo apt-get install -y dotnet-runtime-8.0
   ```

2. **Verify .NET installation**:
   ```bash
   dotnet --version
   ```

### Installation Steps

1. **Build the application**:
   ```bash
   cd server
   dotnet publish -c Release -o ../publish
   ```

2. **Run the application**:
   ```bash
   cd ../publish
   dotnet MyQC.WebAPI.dll --urls http://localhost:8000
   ```

3. **Access the application**:
   Open your browser and navigate to: `http://localhost:8000`

---

## 🚀 Running the Application

### After Installation

If installed using the installer, you can run:

```bash
trs398-pro
```

### Manual Start

```bash
cd ~/.local/share/trs398-pro
dotnet MyQC.WebAPI.dll --urls http://localhost:8000
```

### As a System Service

If you created a systemd service:

```bash
# Start the service
sudo systemctl start trs398-pro

# Enable auto-start on boot
sudo systemctl enable trs398-pro

# Check status
sudo systemctl status trs398-pro
```

---

## 🔧 Configuration

### Custom Port

To run on a different port:

```bash
dotnet MyQC.WebAPI.dll --urls http://localhost:YOUR_PORT
```

### Custom Installation Directory

The installer allows you to choose a custom installation directory. Default is:
- `~/.local/share/trs398-pro/`

---

## 🗑️ Uninstallation

### If Installed via Installer

```bash
# Remove application files
rm -rf ~/.local/share/trs398-pro

# Remove binary
rm ~/.local/bin/trs398-pro

# Remove desktop shortcut
rm ~/.local/share/applications/trs398-pro.desktop

# Remove systemd service (if created)
sudo systemctl stop trs398-pro
sudo systemctl disable trs398-pro
sudo rm /etc/systemd/system/trs398-pro.service
sudo systemctl daemon-reload
```

### Backup Your Data

Before uninstalling, backup your database:

```bash
cp ~/.local/share/trs398-pro/trs398.db ~/trs398-backup.db
```

---

## 🐛 Troubleshooting

### Application Won't Start

**Check .NET installation**:
```bash
dotnet --version
```
Should show 8.0 or higher.

**Check port availability**:
```bash
netstat -tuln | grep 8000
```
If port is in use, change it in the startup command.

### Permission Errors

**Make scripts executable**:
```bash
chmod +x install.sh
chmod +x installer-gui.py
```

**Check file permissions**:
```bash
ls -la ~/.local/share/trs398-pro
```

### Database Errors

**Reset database** (⚠️ This will delete all data):
```bash
rm ~/.local/share/trs398-pro/trs398.db
# Restart the application - it will create a new database
```

---

## 📝 Notes

- The database is stored at: `~/.local/share/trs398-pro/trs398.db`
- Logs are written to: `/tmp/trs398_clean.log`
- The application runs on port 8000 by default
- All data is stored locally - no cloud connection required

---

## 🔗 Related Documentation

- [Main README](../README.md)
- [Development Guide](../docs/DEVELOPMENT_OPPORTUNITIES.md)
- [Installer Guide](../docs/INSTALLER_GUIDE.md)

---

**Need Help?** Open an issue on [GitHub](https://github.com/El-yacine/TRS398/issues)

