# Running Windows Builds on Linux (Wine) - NOT RECOMMENDED

## ⚠️ Important Notice

**The Windows builds (`.exe` files) are designed for Windows machines only.**

Running Windows .NET applications through Wine is:
- ❌ Not officially supported
- ❌ Complex to set up (requires .NET Runtime in Wine)
- ❌ Prone to errors and compatibility issues
- ❌ Not the intended use case

## Why It Fails

When you try to run `MyQC.WebAPI.exe` through Wine, you see:
```
You must install .NET to run this application.
Failed to resolve hostfxr.dll [not found]
```

This happens because:
1. The build is **non-self-contained** (`--self-contained false`)
2. It requires .NET 8.0 Runtime to be installed
3. Wine doesn't have .NET Runtime installed by default
4. Installing .NET in Wine is complex and unreliable

## ✅ Recommended Approach

### For Linux Testing

**Use the Linux-native server directly:**

```bash
# Option 1: Run from published build
cd publish/server
~/.dotnet/dotnet MyQC.WebAPI.dll --urls http://localhost:8000

# Option 2: Use the run script
./run-server.sh

# Option 3: Run from source
cd server
dotnet run --project MyQC.WebAPI.csproj --urls http://localhost:8000
```

### For Windows Deployment

1. **Build on Linux** (what you just did):
   ```bash
   ./build-installer.sh
   ```

2. **Transfer to Windows**:
   - Copy the `publish/` directory to a Windows machine
   - Or use the build script on Windows: `.\build-installer.ps1`

3. **Run on Windows**:
   ```powershell
   cd publish\server
   dotnet MyQC.WebAPI.dll --urls http://localhost:8000
   ```

## If You Really Must Use Wine (Not Recommended)

If you absolutely need to test the Windows .exe on Linux:

1. **Install Wine with 32-bit support:**
   ```bash
   sudo xbps-install -S void-repo-multilib
   sudo xbps-install -S wine-32bit
   ```

2. **Install .NET Runtime in Wine:**
   - Download .NET 8.0 Runtime for Windows
   - Install it in Wine: `wine dotnet-runtime-8.0.x-win-x64.exe`
   - This is complex and may not work reliably

3. **Set Wine prefix:**
   ```bash
   export WINEPREFIX=~/.wine-trs398
   winecfg  # Configure Wine
   ```

**But again, this is NOT recommended.** Use the native Linux server instead.

## Summary

- ✅ **Linux testing**: Use `./run-server.sh` or run with `dotnet` directly
- ✅ **Windows deployment**: Build on Linux, transfer to Windows, run there
- ❌ **Wine testing**: Not recommended, use native Linux server instead

The server application works identically whether run on Linux or Windows - the only difference is the platform-specific executable wrapper.

