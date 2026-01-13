#!/bin/bash
# Build script for TRS-398 Professional (Linux/Mac - for cross-platform building)

set -e

echo "========================================"
echo "TRS-398 Professional Build Script"
echo "========================================"
echo ""

CONFIGURATION="${1:-Release}"
PLATFORM="${2:-x64}"

# Check if .NET SDK is installed
echo "Checking .NET SDK..."

# Try to find dotnet in common locations
DOTNET_PATH=""
if command -v dotnet &> /dev/null; then
    DOTNET_PATH=$(command -v dotnet)
elif [ -f "$HOME/.dotnet/dotnet" ]; then
    DOTNET_PATH="$HOME/.dotnet/dotnet"
    export PATH="$PATH:$HOME/.dotnet"
elif [ -f "/usr/bin/dotnet" ]; then
    DOTNET_PATH="/usr/bin/dotnet"
fi

if [ -z "$DOTNET_PATH" ] || [ ! -f "$DOTNET_PATH" ]; then
    echo "ERROR: .NET SDK not found. Please install .NET 8.0 SDK." >&2
    echo "" >&2
    echo "Installation instructions:" >&2
    echo "  - See SETUP_DOTNET.md for detailed instructions" >&2
    echo "  - Void Linux: sudo xbps-install dotnet-sdk" >&2
    echo "  - Or use Microsoft's script: curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 8.0" >&2
    echo "  - Then add to PATH: export PATH=\$PATH:\$HOME/.dotnet" >&2
    exit 1
fi

# Ensure dotnet is in PATH for this script
if [ -f "$HOME/.dotnet/dotnet" ]; then
    export PATH="$PATH:$HOME/.dotnet"
fi

DOTNET_VERSION=$(dotnet --version)
echo "Found .NET SDK version: $DOTNET_VERSION"
echo ""

# Set paths
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SERVER_PROJECT="$ROOT_DIR/server/MyQC.WebAPI.csproj"
DESKTOP_PROJECT="$ROOT_DIR/TRS398Desktop/TRS398Desktop.csproj"
PUBLISH_DIR="$ROOT_DIR/publish"

# Clean previous builds
echo "Cleaning previous builds..."
rm -rf "$PUBLISH_DIR"
mkdir -p "$PUBLISH_DIR"
echo ""

# Build and publish server
echo "Building and publishing server application..."
SERVER_PUBLISH_DIR="$PUBLISH_DIR/server"

# Determine target runtime based on OS
if [[ "$OSTYPE" == "msys" ]] || [[ "$OSTYPE" == "win32" ]] || [[ "$OSTYPE" == "cygwin" ]]; then
    RUNTIME_ID="win-x64"
    echo "Building for Windows (win-x64)..."
else
    RUNTIME_ID="linux-x64"
    echo "Building for Linux (linux-x64)..."
    echo "Note: For Windows deployment, build on Windows or use win-x64 target."
fi

dotnet publish "$SERVER_PROJECT" \
    -c "$CONFIGURATION" \
    -r "$RUNTIME_ID" \
    --self-contained false \
    -p:PublishSingleFile=false \
    -p:IncludeNativeLibrariesForSelfExtract=false \
    -o "$SERVER_PUBLISH_DIR"

if [ $? -ne 0 ]; then
    echo "ERROR: Failed to publish server application."
    exit 1
fi
echo "Server application published successfully."
echo ""

# Build and publish desktop wrapper (Windows only - skip on Linux)
echo "Building and publishing desktop application..."
DESKTOP_PUBLISH_DIR="$PUBLISH_DIR/TRS398Desktop"

# Windows Forms apps cannot be built on Linux, so skip on non-Windows
if [[ "$OSTYPE" == "msys" ]] || [[ "$OSTYPE" == "win32" ]] || [[ "$OSTYPE" == "cygwin" ]]; then
    # On Windows, build normally
    echo "Building Windows desktop application..."
    dotnet publish "$DESKTOP_PROJECT" \
        -c "$CONFIGURATION" \
        -r win-x64 \
        --self-contained false \
        -p:PublishSingleFile=true \
        -p:IncludeNativeLibrariesForSelfExtract=true \
        -o "$DESKTOP_PUBLISH_DIR"
    
    if [ $? -ne 0 ]; then
        echo "ERROR: Failed to publish desktop application."
        exit 1
    fi
    echo "Desktop application published successfully."
else
    # On Linux/Mac, skip desktop build (Windows Forms requires Windows)
    echo "Skipping desktop application build (Windows Forms requires Windows OS)."
    echo "The desktop wrapper can only be built on Windows."
    echo "Server application files are ready for Windows installer creation."
    echo ""
    echo "To build the complete installer with desktop app:"
    echo "  1. Transfer the 'publish/server' directory to a Windows machine"
    echo "  2. On Windows, run: .\\build-installer.ps1"
    echo ""
    mkdir -p "$DESKTOP_PUBLISH_DIR" || true
fi
echo ""

# Copy detector library
echo "Copying detector library..."
DETECTOR_LIB="$ROOT_DIR/detector_library.json"
if [ -f "$DETECTOR_LIB" ]; then
    cp "$DETECTOR_LIB" "$SERVER_PUBLISH_DIR/"
    echo "Detector library copied."
else
    echo "WARNING: detector_library.json not found."
fi
echo ""

echo "========================================"
echo "Build completed successfully!"
echo "========================================"
echo "Application files: $PUBLISH_DIR"
echo ""
echo "IMPORTANT NOTES:"
echo "  - The Windows build (win-x64) is for Windows machines only"
echo "  - Do NOT try to run .exe files on Linux using Wine"
echo "  - To test on Linux, use: ./run-server.sh"
echo ""
echo "Next steps:"
echo "  - Test on Linux: ./run-server.sh"
echo "  - For Windows installer: Transfer to Windows and run build-installer.ps1"
echo ""

