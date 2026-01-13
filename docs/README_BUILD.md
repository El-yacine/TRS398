# Build Instructions - TRS-398 Professional

## Quick Reference

### For Linux Testing/Development
```bash
# Build for Linux
./build-installer.sh

# Run server
./run-server.sh
```

### For Windows Deployment
```bash
# Option 1: Build on Linux for Windows (cross-compile)
dotnet publish server/MyQC.WebAPI.csproj -c Release -r win-x64 --self-contained false -o publish/server-win

# Option 2: Build on Windows
.\build-installer.ps1
```

## Important: Platform-Specific Builds

### Linux Build (`linux-x64`)
- ✅ Can run directly on Linux
- ✅ Includes Linux native libraries (.so files)
- ❌ Cannot run on Windows
- Use for: Development, testing on Linux

### Windows Build (`win-x64`)
- ✅ Can run directly on Windows
- ✅ Includes Windows native libraries (.dll files)
- ❌ Cannot run on Linux (even with Wine)
- Use for: Windows deployment, installer creation

## Common Issues

### "Cannot load QuestPdfSkia.so" on Linux
**Cause**: You're trying to run a Windows build on Linux.

**Solution**: Build for Linux:
```bash
dotnet publish server/MyQC.WebAPI.csproj -c Release -r linux-x64 -o publish/server
./run-server.sh
```

### "Failed to resolve hostfxr.dll" in Wine
**Cause**: Trying to run Windows .exe through Wine.

**Solution**: Don't use Wine. Run the Linux build instead:
```bash
./run-server.sh
```

### Missing .NET Runtime
**Cause**: Non-self-contained build requires .NET Runtime.

**Solution**: Install .NET 8.0 Runtime on the target platform.

## Build Targets Explained

| Target | Platform | Native Libs | Use Case |
|--------|----------|--------------|----------|
| `linux-x64` | Linux | .so files | Linux development/testing |
| `win-x64` | Windows | .dll files | Windows deployment |
| `osx-x64` | macOS | .dylib files | macOS deployment |

## Self-Contained vs Framework-Dependent

### Framework-Dependent (Default)
- ✅ Smaller size (~50MB)
- ✅ Requires .NET Runtime installed
- ✅ Faster startup
- Use for: Most deployments

### Self-Contained
- ✅ No .NET Runtime required
- ❌ Larger size (~150MB+)
- ❌ Slower startup
- Use for: Standalone distribution

To build self-contained:
```bash
dotnet publish -c Release -r linux-x64 --self-contained true
```

