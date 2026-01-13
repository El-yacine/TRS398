# Installing .NET 8.0 SDK

## For Void Linux

Install .NET 8.0 SDK using xbps:

```bash
# Update package database
sudo xbps-install -S

# Install .NET SDK
sudo xbps-install dotnet-sdk

# Verify installation
dotnet --version
```

If the package is not available in Void repositories, you can install from Microsoft:

### Method 1: Using Microsoft's Installation Script (Recommended)

```bash
# Download and run the installation script
curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 8.0

# Add to PATH (add to ~/.zshrc or ~/.bashrc)
export DOTNET_ROOT=$HOME/.dotnet
export PATH=$PATH:$HOME/.dotnet:$HOME/.dotnet/tools

# Reload shell configuration
source ~/.zshrc  # or source ~/.bashrc
```

### Method 2: Manual Installation

1. Download .NET 8.0 SDK from: https://dotnet.microsoft.com/download/dotnet/8.0
2. Extract to a directory (e.g., `~/dotnet`)
3. Add to PATH:
   ```bash
   export DOTNET_ROOT=$HOME/dotnet
   export PATH=$PATH:$HOME/dotnet
   ```

## For Other Linux Distributions

### Ubuntu/Debian

```bash
# Add Microsoft package repository
wget https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

# Install .NET SDK
sudo apt-get update
sudo apt-get install -y dotnet-sdk-8.0
```

### Fedora/RHEL/CentOS

```bash
# Add Microsoft repository
sudo rpm --import https://packages.microsoft.com/keys/microsoft.asc
sudo dnf config-manager --add-repo https://packages.microsoft.com/config/fedora/$(rpm -E %fedora)/prod.repo

# Install .NET SDK
sudo dnf install -y dotnet-sdk-8.0
```

### Arch Linux

```bash
sudo pacman -S dotnet-sdk
```

## Verify Installation

After installing, verify it works:

```bash
dotnet --version
# Should show: 8.0.x or higher
```

## Troubleshooting

### "dotnet: command not found"

- Ensure you've added .NET to your PATH
- Restart your terminal or run: `source ~/.zshrc` (or `source ~/.bashrc`)
- Check installation location: `which dotnet` or `ls ~/.dotnet/dotnet`

### Permission Issues

If you get permission errors, you may need to:
- Use `sudo` for system-wide installation, OR
- Install to user directory (`~/.dotnet`) and add to PATH

### Multiple .NET Versions

If you have multiple versions installed:
```bash
# List installed versions
dotnet --list-sdks

# Use specific version (if needed)
dotnet --version 8.0.xxx
```

## Next Steps

Once .NET SDK is installed, you can:

1. Build the installer: `./build-installer.sh`
2. Run the application: `cd server && dotnet run --urls http://localhost:8000`
3. Build the project: `dotnet build server/MyQC.WebAPI.csproj`

