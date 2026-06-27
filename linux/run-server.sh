#!/bin/bash
# Run TRS-398 server on Linux for testing

set -e

echo "========================================"
echo "TRS-398 Server - Linux Runner"
echo "========================================"
echo ""

# Check if port 8000 is in use
PORT=8000
if lsof -ti:$PORT >/dev/null 2>&1 || netstat -tlnp 2>/dev/null | grep -q ":$PORT " || ss -tlnp 2>/dev/null | grep -q ":$PORT "; then
    echo "⚠️  Port $PORT is already in use!"
    echo ""
    
    # Try to find the process
    PID=$(lsof -ti:$PORT 2>/dev/null || echo "")
    if [ -n "$PID" ]; then
        echo "Found process using port $PORT: PID $PID"
        ps -p $PID -o pid,cmd --no-headers 2>/dev/null || echo "  (Process details unavailable)"
        echo ""
        read -p "Kill existing process and start server? (y/N): " -n 1 -r
        echo ""
        if [[ $REPLY =~ ^[Yy]$ ]]; then
            kill $PID 2>/dev/null && sleep 1 && echo "✅ Process killed."
        else
            echo "Using alternative port 8001..."
            PORT=8001
        fi
    else
        echo "Could not identify process. Using alternative port 8001..."
        PORT=8001
    fi
    echo ""
fi

# Ensure dotnet is in PATH
if [ -f "$HOME/.dotnet/dotnet" ]; then
    export PATH="$PATH:$HOME/.dotnet"
fi

# Check if .NET SDK is installed
if ! command -v dotnet &> /dev/null; then
    echo "ERROR: .NET SDK not found." >&2
    echo "Please install .NET 8.0 SDK. See SETUP_DOTNET.md" >&2
    exit 1
fi

# Determine server path
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SERVER_DIR="$SCRIPT_DIR/server"
PUBLISH_DIR="$SCRIPT_DIR/publish/server"

# Check for Linux build first, then Windows build, then source
if [ -f "$SCRIPT_DIR/publish/server-linux/MyQC.WebAPI.dll" ]; then
    echo "Using Linux build from: publish/server-linux"
    cd "$SCRIPT_DIR/publish/server-linux"
    echo "Starting server on http://localhost:$PORT"
    dotnet MyQC.WebAPI.dll --urls http://localhost:$PORT
elif [ -f "$PUBLISH_DIR/MyQC.WebAPI.dll" ]; then
    # Check if it's a Windows build
    if [ -f "$PUBLISH_DIR/MyQC.WebAPI.exe" ]; then
        echo "WARNING: Found Windows build. Building Linux version..." >&2
        echo "Building for Linux..." >&2
        dotnet publish "$SERVER_DIR/MyQC.WebAPI.csproj" -c Release -r linux-x64 --self-contained false -o "$SCRIPT_DIR/publish/server-linux"
        cd "$SCRIPT_DIR/publish/server-linux"
        echo "Starting server on http://localhost:$PORT"
        dotnet MyQC.WebAPI.dll --urls http://localhost:$PORT
    else
        echo "Using published version from: $PUBLISH_DIR"
        cd "$PUBLISH_DIR"
        echo "Starting server on http://localhost:$PORT"
        dotnet MyQC.WebAPI.dll --urls http://localhost:$PORT
    fi
elif [ -f "$SERVER_DIR/MyQC.WebAPI.csproj" ]; then
    echo "Running from source..."
    cd "$SERVER_DIR"
    echo "Starting server on http://localhost:$PORT"
    dotnet run --project MyQC.WebAPI.csproj --urls http://localhost:$PORT
else
    echo "ERROR: Could not find server application." >&2
    echo "Please build first: ./build-installer.sh" >&2
    exit 1
fi

