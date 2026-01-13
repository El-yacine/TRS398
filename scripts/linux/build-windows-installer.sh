#!/bin/bash
echo "========================================"
echo "TRS-398 Pro Windows Installer Builder"
echo "========================================"
echo ""

# Add .NET to PATH
export PATH=$PATH:$HOME/.dotnet

# Find dotnet
DOTNET=""
if command -v dotnet &> /dev/null; then
    DOTNET="dotnet"
elif [ -f "$HOME/.dotnet/dotnet" ]; then
    DOTNET="$HOME/.dotnet/dotnet"
else
    echo "ERROR: .NET SDK not found!"
    echo "Please install .NET 8.0 SDK"
    exit 1
fi

echo "Using: $DOTNET"

echo "Building Windows installer (cross-compile from Linux)..."
echo ""

# Navigate to installer directory
cd "$(dirname "$0")/installer"

# Clean previous builds
echo "Cleaning previous builds..."
$DOTNET clean -c Release > /dev/null 2>&1

# Create dist directory
mkdir -p ../dist/windows

# Publish for Windows x64
echo "Publishing for Windows x64..."
$DOTNET publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o "../dist/windows"

if [ $? -ne 0 ]; then
    echo ""
    echo "ERROR: Build failed!"
    exit 1
fi

# Copy app files
echo "Copying application files..."
mkdir -p ../dist/windows/AppFiles/server
cp -r ../server/* ../dist/windows/AppFiles/server/
cp ../detector_library.json ../dist/windows/AppFiles/

echo ""
echo "========================================"
echo "Build successful!"
echo "========================================"
echo ""
echo "Installer location: dist/windows/TRS398ProSetup.exe"
echo ""
echo "Copy TRS398ProSetup.exe and the AppFiles folder to Windows to install."

