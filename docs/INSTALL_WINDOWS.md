# TRS-398 Pro — Installation on Windows

A step-by-step guide to run TRS-398 Pro on Windows 10 / 11.

---

## 1. Prerequisites

You need the **.NET 8.0 SDK** (or the ASP.NET Core 8.0 Runtime).

Check in PowerShell:

```powershell
dotnet --version    # should print 8.0.x
```

If it's missing, install it one of these ways:

- **Winget (recommended):**
  ```powershell
  winget install Microsoft.DotNet.SDK.8
  ```
- **Manual:** download from <https://dotnet.microsoft.com/download/dotnet/8.0> and run the installer.

Close and reopen PowerShell after installing so `dotnet` is on your PATH.

---

## 2. Get the code

```powershell
git clone https://github.com/El-yacine/TRS398.git
cd TRS398
```

(No Git? Download the repo ZIP from GitHub and extract it.)

---

## 3. Run it

```powershell
cd server
dotnet run --project MyQC.WebAPI.csproj --urls http://localhost:8000
```

The first launch restores packages and compiles (~30–60 s). When you see
`Now listening on: http://localhost:8000`, open your browser at:

> **http://localhost:8000**

The SQLite database and settings are created automatically on first run — no
separate database to install.

---

## 4. Optional: one-click launch shortcut

Create a file `Start-TRS398.bat` in the project root with:

```bat
@echo off
cd /d "%~dp0server"
start "" http://localhost:8000
dotnet run --project MyQC.WebAPI.csproj --urls http://localhost:8000
```

Double-click it to launch the app and open the browser automatically.

---

## 5. Optional: production build

```powershell
cd server
dotnet publish -c Release -o ..\publish
cd ..\publish
dotnet MyQC.WebAPI.dll --urls http://localhost:8000
```

---

## 6. Optional: desktop app (Electron)

With Node.js 18+ installed (`winget install OpenJS.NodeJS.LTS`):

```powershell
npm install
npm start
```

To build a Windows installer, see the `windows/` folder.

---

## 7. Optional: email alerts

```powershell
copy server\email_config.example.json server\email_config.json
notepad server\email_config.json   # fill SMTP details, set "AlertsEnabled": true
```

`email_config.json` is git-ignored and stays on your machine.

---

## Troubleshooting

| Symptom | Fix |
|--------|-----|
| `dotnet` is not recognized | Install the .NET 8 SDK (step 1), reopen PowerShell. |
| Port 8000 already in use | Use another port: `--urls http://localhost:8080`. |
| SmartScreen blocks a script | Right-click → Properties → **Unblock**, or run from a trusted folder. |
| Page won't refresh after an update | Hard refresh the browser: **Ctrl + Shift + R**. |

---

## Operators (no login required)

The app has **no login** — it records *who performed* each measurement via an
**Operator** picker in the top bar. Manage the list of operator names in
**Settings → Operators** (add / remove). The selected operator is saved with each
measurement and printed on the PDF report. No passwords, no user accounts.
