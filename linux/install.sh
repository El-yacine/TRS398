#!/bin/bash
#
# TRS-398 Pro Installer for Linux
# Medical Physics Calibration System
# Version 2.0.0
#

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Installation paths
INSTALL_DIR="${HOME}/.local/share/trs398-pro"
BIN_DIR="${HOME}/.local/bin"
DESKTOP_DIR="${HOME}/.local/share/applications"
CONFIG_DIR="${HOME}/.config/trs398-pro"

# Application info
APP_NAME="TRS-398 Pro"
APP_VERSION="2.0.0"
APP_PORT=8000

# Print banner
print_banner() {
    echo -e "${CYAN}"
    echo "╔═══════════════════════════════════════════════════════════════╗"
    echo "║                                                               ║"
    echo "║              TRS-398 Pro Installer v${APP_VERSION}                   ║"
    echo "║          Medical Physics Calibration System                   ║"
    echo "║                                                               ║"
    echo "╚═══════════════════════════════════════════════════════════════╝"
    echo -e "${NC}"
}

# Print step
print_step() {
    echo -e "${GREEN}[✓]${NC} $1"
}

# Print info
print_info() {
    echo -e "${BLUE}[i]${NC} $1"
}

# Print warning
print_warning() {
    echo -e "${YELLOW}[!]${NC} $1"
}

# Print error
print_error() {
    echo -e "${RED}[✗]${NC} $1"
}

# Check if .NET is installed
check_dotnet() {
    print_info "Checking for .NET runtime..."
    
    if command -v dotnet &> /dev/null; then
        DOTNET_VERSION=$(dotnet --version 2>/dev/null || echo "unknown")
        print_step ".NET found: version $DOTNET_VERSION"
        return 0
    fi
    
    # Check for local dotnet installation
    if [ -f "${HOME}/.dotnet/dotnet" ]; then
        export PATH="${HOME}/.dotnet:$PATH"
        DOTNET_VERSION=$(~/.dotnet/dotnet --version 2>/dev/null || echo "unknown")
        print_step ".NET found (local): version $DOTNET_VERSION"
        return 0
    fi
    
    return 1
}

# Install .NET
install_dotnet() {
    print_info "Installing .NET 8.0 runtime..."
    
    # Download and run Microsoft's install script
    curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
    chmod +x /tmp/dotnet-install.sh
    /tmp/dotnet-install.sh --channel 8.0 --install-dir "${HOME}/.dotnet"
    rm /tmp/dotnet-install.sh
    
    # Add to PATH
    export PATH="${HOME}/.dotnet:$PATH"
    
    # Add to shell profile
    SHELL_PROFILE=""
    if [ -f "${HOME}/.bashrc" ]; then
        SHELL_PROFILE="${HOME}/.bashrc"
    elif [ -f "${HOME}/.zshrc" ]; then
        SHELL_PROFILE="${HOME}/.zshrc"
    elif [ -f "${HOME}/.profile" ]; then
        SHELL_PROFILE="${HOME}/.profile"
    fi
    
    if [ -n "$SHELL_PROFILE" ]; then
        if ! grep -q "\.dotnet" "$SHELL_PROFILE" 2>/dev/null; then
            echo 'export PATH="$HOME/.dotnet:$PATH"' >> "$SHELL_PROFILE"
            print_step "Added .NET to PATH in $SHELL_PROFILE"
        fi
    fi
    
    print_step ".NET 8.0 installed successfully"
}

# Create directories
create_directories() {
    print_info "Creating installation directories..."
    
    mkdir -p "$INSTALL_DIR"
    mkdir -p "$BIN_DIR"
    mkdir -p "$DESKTOP_DIR"
    mkdir -p "$CONFIG_DIR"
    mkdir -p "$INSTALL_DIR/wwwroot/logos"
    mkdir -p "$INSTALL_DIR/data"
    
    print_step "Directories created"
}

# Copy application files
copy_files() {
    print_info "Copying application files..."
    
    # Get the directory where this script is located
    SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
    
    # Copy server files
    if [ -d "$SCRIPT_DIR/server" ]; then
        cp -r "$SCRIPT_DIR/server/"* "$INSTALL_DIR/"
        print_step "Server files copied"
    else
        print_error "Server directory not found!"
        exit 1
    fi
    
    # Copy detector library
    if [ -f "$SCRIPT_DIR/detector_library.json" ]; then
        cp "$SCRIPT_DIR/detector_library.json" "$INSTALL_DIR/"
        print_step "Detector library copied"
    fi
    
    # Set permissions
    chmod -R 755 "$INSTALL_DIR"
    
    print_step "All files copied successfully"
}

# Build application
build_application() {
    print_info "Building application..."
    
    cd "$INSTALL_DIR"
    
    # Restore and build
    if [ -f "${HOME}/.dotnet/dotnet" ]; then
        "${HOME}/.dotnet/dotnet" restore
        "${HOME}/.dotnet/dotnet" build --configuration Release
    else
        dotnet restore
        dotnet build --configuration Release
    fi
    
    print_step "Application built successfully"
}

# Create launcher script
create_launcher() {
    print_info "Creating launcher script..."
    
    cat > "$BIN_DIR/trs398-pro" << 'EOF'
#!/bin/bash
#
# TRS-398 Pro Launcher
#

INSTALL_DIR="${HOME}/.local/share/trs398-pro"
PORT=8000
BROWSER_DELAY=3

# Add .NET to PATH if needed
if [ -d "${HOME}/.dotnet" ]; then
    export PATH="${HOME}/.dotnet:$PATH"
fi

# Check if already running
if pgrep -f "MyQC.WebAPI" > /dev/null; then
    echo "TRS-398 Pro is already running!"
    echo "Opening browser..."
    xdg-open "http://localhost:${PORT}" 2>/dev/null || \
    sensible-browser "http://localhost:${PORT}" 2>/dev/null || \
    echo "Please open http://localhost:${PORT} in your browser"
    exit 0
fi

# Start the server
cd "$INSTALL_DIR"
echo "Starting TRS-398 Pro..."
echo "Server will be available at http://localhost:${PORT}"

# Run in background and open browser
if [ -f "${HOME}/.dotnet/dotnet" ]; then
    "${HOME}/.dotnet/dotnet" run --urls "http://localhost:${PORT}" &
else
    dotnet run --urls "http://localhost:${PORT}" &
fi

SERVER_PID=$!

# Wait for server to start
echo "Waiting for server to start..."
sleep $BROWSER_DELAY

# Check if server started successfully
if kill -0 $SERVER_PID 2>/dev/null; then
    echo "Server started successfully!"
    
    # Open browser
    xdg-open "http://localhost:${PORT}" 2>/dev/null || \
    sensible-browser "http://localhost:${PORT}" 2>/dev/null || \
    echo "Please open http://localhost:${PORT} in your browser"
    
    echo ""
    echo "TRS-398 Pro is running. Press Ctrl+C to stop."
    wait $SERVER_PID
else
    echo "Failed to start server!"
    exit 1
fi
EOF

    chmod +x "$BIN_DIR/trs398-pro"
    print_step "Launcher script created: $BIN_DIR/trs398-pro"
}

# Create desktop entry
create_desktop_entry() {
    print_info "Creating desktop shortcut..."
    
    # Create icon directory
    ICON_DIR="${HOME}/.local/share/icons/hicolor/256x256/apps"
    mkdir -p "$ICON_DIR"
    
    # Create a simple SVG icon
    cat > "$ICON_DIR/trs398-pro.svg" << 'EOF'
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
  <defs>
    <linearGradient id="bg" x1="0%" y1="0%" x2="100%" y2="100%">
      <stop offset="0%" style="stop-color:#0f172a"/>
      <stop offset="100%" style="stop-color:#1e293b"/>
    </linearGradient>
  </defs>
  <rect width="100" height="100" rx="20" fill="url(#bg)"/>
  <text x="50" y="45" text-anchor="middle" fill="#10b981" font-family="Arial" font-weight="bold" font-size="24">TRS</text>
  <text x="50" y="72" text-anchor="middle" fill="#06b6d4" font-family="Arial" font-weight="bold" font-size="20">398</text>
  <circle cx="50" cy="85" r="5" fill="#10b981"/>
</svg>
EOF

    # Create desktop entry
    cat > "$DESKTOP_DIR/trs398-pro.desktop" << EOF
[Desktop Entry]
Version=1.0
Type=Application
Name=TRS-398 Pro
Comment=Medical Physics Calibration System - IAEA TRS-398 Protocol
Exec=${BIN_DIR}/trs398-pro
Icon=trs398-pro
Terminal=false
Categories=Science;Medical;Education;
Keywords=physics;calibration;dosimetry;radiation;medical;
StartupNotify=true
EOF

    chmod +x "$DESKTOP_DIR/trs398-pro.desktop"
    
    # Update desktop database
    if command -v update-desktop-database &> /dev/null; then
        update-desktop-database "$DESKTOP_DIR" 2>/dev/null || true
    fi
    
    print_step "Desktop shortcut created"
}

# Create uninstaller
create_uninstaller() {
    print_info "Creating uninstaller..."
    
    cat > "$INSTALL_DIR/uninstall.sh" << 'EOF'
#!/bin/bash
#
# TRS-398 Pro Uninstaller
#

echo "TRS-398 Pro Uninstaller"
echo "======================="
echo ""

read -p "Are you sure you want to uninstall TRS-398 Pro? (y/N) " -n 1 -r
echo ""

if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    echo "Uninstall cancelled."
    exit 0
fi

echo "Removing application files..."
rm -rf "${HOME}/.local/share/trs398-pro"
rm -f "${HOME}/.local/bin/trs398-pro"
rm -f "${HOME}/.local/share/applications/trs398-pro.desktop"
rm -f "${HOME}/.local/share/icons/hicolor/256x256/apps/trs398-pro.svg"

# Optionally remove config
read -p "Remove configuration and data? (y/N) " -n 1 -r
echo ""
if [[ $REPLY =~ ^[Yy]$ ]]; then
    rm -rf "${HOME}/.config/trs398-pro"
    echo "Configuration removed."
fi

echo ""
echo "TRS-398 Pro has been uninstalled."
EOF

    chmod +x "$INSTALL_DIR/uninstall.sh"
    print_step "Uninstaller created: $INSTALL_DIR/uninstall.sh"
}

# Create systemd service (optional)
create_service() {
    print_info "Creating systemd user service..."
    
    SERVICE_DIR="${HOME}/.config/systemd/user"
    mkdir -p "$SERVICE_DIR"
    
    cat > "$SERVICE_DIR/trs398-pro.service" << EOF
[Unit]
Description=TRS-398 Pro Medical Physics Calibration System
After=network.target

[Service]
Type=simple
WorkingDirectory=${INSTALL_DIR}
ExecStart=${HOME}/.dotnet/dotnet run --urls http://localhost:${APP_PORT}
Restart=on-failure
RestartSec=10

[Install]
WantedBy=default.target
EOF

    print_step "Systemd service created"
    print_info "To enable auto-start: systemctl --user enable trs398-pro"
}

# Main installation
main() {
    print_banner
    
    echo "This installer will set up TRS-398 Pro on your system."
    echo ""
    echo "Installation directory: $INSTALL_DIR"
    echo "Binary directory: $BIN_DIR"
    echo ""
    
    read -p "Continue with installation? (Y/n) " -n 1 -r
    echo ""
    
    if [[ $REPLY =~ ^[Nn]$ ]]; then
        echo "Installation cancelled."
        exit 0
    fi
    
    echo ""
    echo "Starting installation..."
    echo ""
    
    # Check/install .NET
    if ! check_dotnet; then
        print_warning ".NET runtime not found."
        read -p "Install .NET 8.0? (Y/n) " -n 1 -r
        echo ""
        if [[ ! $REPLY =~ ^[Nn]$ ]]; then
            install_dotnet
        else
            print_error ".NET is required to run TRS-398 Pro"
            exit 1
        fi
    fi
    
    # Create directories
    create_directories
    
    # Copy files
    copy_files
    
    # Build application
    build_application
    
    # Create launcher
    create_launcher
    
    # Create desktop entry
    create_desktop_entry
    
    # Create uninstaller
    create_uninstaller
    
    # Create systemd service
    create_service
    
    echo ""
    echo -e "${GREEN}╔═══════════════════════════════════════════════════════════════╗${NC}"
    echo -e "${GREEN}║                                                               ║${NC}"
    echo -e "${GREEN}║            Installation completed successfully!              ║${NC}"
    echo -e "${GREEN}║                                                               ║${NC}"
    echo -e "${GREEN}╚═══════════════════════════════════════════════════════════════╝${NC}"
    echo ""
    echo "You can now run TRS-398 Pro by:"
    echo ""
    echo "  1. Command line:  trs398-pro"
    echo "  2. Application menu: Look for 'TRS-398 Pro'"
    echo "  3. Direct URL: http://localhost:${APP_PORT}"
    echo ""
    echo "To uninstall: ${INSTALL_DIR}/uninstall.sh"
    echo ""
    
    # Ask to launch
    read -p "Launch TRS-398 Pro now? (Y/n) " -n 1 -r
    echo ""
    if [[ ! $REPLY =~ ^[Nn]$ ]]; then
        "$BIN_DIR/trs398-pro"
    fi
}

# Run main
main "$@"

