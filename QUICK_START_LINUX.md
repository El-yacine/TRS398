# Quick Start - Running TRS-398 on Linux

## ⚠️ STOP: Don't Use Wine!

**DO NOT run `MyQC.WebAPI.exe` with Wine. It will NOT work.**

The Windows `.exe` file is for Windows machines only. On Linux, use the `.dll` file with `dotnet`.

## ✅ Correct Way to Run on Linux

### Method 1: Use the Run Script (Recommended)

```bash
cd /home/floky/TRS_398
./run-server.sh
```

This script will:
- Automatically find the Linux build
- Build it if needed
- Start the server
- Open in your browser

### Method 2: Run Linux Build Directly

```bash
# Navigate to Linux build directory
cd /home/floky/TRS_398/publish/server-linux

# Run with dotnet
~/.dotnet/dotnet MyQC.WebAPI.dll --urls http://localhost:8000
```

### Method 3: Run from Source

```bash
cd /home/floky/TRS_398/server
dotnet run --project MyQC.WebAPI.csproj --urls http://localhost:8000
```

## Building for Linux

If you need to build for Linux:

```bash
cd /home/floky/TRS_398
./build-installer.sh
```

This will automatically build for Linux when run on Linux.

Or manually:

```bash
dotnet publish server/MyQC.WebAPI.csproj -c Release -r linux-x64 -o publish/server-linux
```

## Accessing the Application

Once the server is running:
- Open your browser
- Go to: `http://localhost:8000`
- The application will load

## Stopping the Server

Press `Ctrl+C` in the terminal where it's running, or:

```bash
pkill -f "MyQC.WebAPI.dll"
```

## Troubleshooting

### "Cannot find dotnet"
```bash
export PATH=$PATH:$HOME/.dotnet
```

Or add to `~/.zshrc`:
```bash
export PATH=$PATH:$HOME/.dotnet
```

### "Cannot load QuestPdfSkia.so"
You're trying to run a Windows build. Use the Linux build instead:
```bash
./run-server.sh
```

### "Port 8000 already in use"
Change the port:
```bash
dotnet MyQC.WebAPI.dll --urls http://localhost:8001
```

## Summary

- ✅ **Linux**: Use `MyQC.WebAPI.dll` with `dotnet`
- ✅ **Windows**: Use `MyQC.WebAPI.exe` (on Windows machine)
- ❌ **Wine**: Don't use it - use the Linux build instead

The server works identically on both platforms - just use the correct build for your OS!

