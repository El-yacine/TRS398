# TRS-398 Pro — Installation on Linux

A step-by-step guide to run TRS-398 Pro on any modern Linux distribution
(Debian/Ubuntu, Fedora, Arch, etc.).

---

## 1. Prerequisites

You need the **.NET 8.0 SDK** (or at minimum the ASP.NET Core 8.0 Runtime).

Check whether it's already installed:

```bash
dotnet --version    # should print 8.0.x
```

If it's missing, install it:

**Debian / Ubuntu**
```bash
sudo apt update
sudo apt install -y dotnet-sdk-8.0
```

**Fedora**
```bash
sudo dnf install -y dotnet-sdk-8.0
```

**Arch Linux**
```bash
sudo pacman -S dotnet-sdk aspnet-runtime
```

**Any distro (official script)**
```bash
curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0
export PATH="$HOME/.dotnet:$PATH"
```

---

## 2. Get the code

```bash
git clone https://github.com/El-yacine/TRS398.git
cd TRS398
```

---

## 3. Run it

```bash
cd server
dotnet run --project MyQC.WebAPI.csproj --urls http://localhost:8000
```

The first launch restores packages and compiles (takes ~30–60 s). When you see
`Now listening on: http://localhost:8000`, open a browser at:

> **http://localhost:8000**

The SQLite database (`trs398.db`) and your settings are created automatically
on first run, in the app data directory — no database server required.

---

## 4. Optional: run as a background service (systemd)

Create `/etc/systemd/system/trs398.service`:

```ini
[Unit]
Description=TRS-398 Pro
After=network.target

[Service]
WorkingDirectory=/opt/TRS398/server
ExecStart=/usr/bin/dotnet run --project MyQC.WebAPI.csproj --urls http://localhost:8000
Restart=on-failure
User=YOUR_USER

[Install]
WantedBy=multi-user.target
```

Then:

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now trs398
```

For a production deployment, publish a self-contained build instead:

```bash
cd server
dotnet publish -c Release -o ../publish
cd ../publish
dotnet MyQC.WebAPI.dll --urls http://localhost:8000
```

---

## 5. Optional: desktop app (Electron)

A desktop wrapper is included. With Node.js 18+ installed:

```bash
npm install
npm start
```

---

## 6. Optional: email alerts

To get an email when a calibration is out of tolerance:

```bash
cp server/email_config.example.json server/email_config.json
# edit server/email_config.json with your SMTP details, set "AlertsEnabled": true
```

`email_config.json` is git-ignored and stays on your machine.

---

## Troubleshooting

| Symptom | Fix |
|--------|-----|
| `dotnet: command not found` | Install the .NET 8 SDK (step 1) and reopen the terminal. |
| Port 8000 already in use | Use another port: `--urls http://localhost:8080`. |
| Page won't refresh after an update | Hard refresh the browser: **Ctrl + Shift + R**. |
| Permission denied writing the database | Run from a directory your user owns, or set a writable data path. |

---

## Default login accounts

The app ships with two seed accounts (used only if you enable login):

| Username   | Password     | Role      |
|------------|--------------|-----------|
| `admin`    | `admin123`   | admin     |
| `physicist`| `physics123` | physicist |

**Change these before any networked deployment.**
