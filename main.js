'use strict';

// The TRS-398 HTML pages use inline scripts which Electron flags as unsafe-inline.
// This is a dev-only cosmetic warning — not a runtime error; safe to suppress here.
process.env.ELECTRON_DISABLE_SECURITY_WARNINGS = 'true';

const { app, BrowserWindow, Menu, shell } = require('electron');

// On Windows with 125–150% DPI scaling Electron shrinks the CSS viewport
// (1280px window at 150% DPI → 853px CSS), triggering the 900px mobile
// breakpoint and collapsing the sidebar/forms. Forcing 1:1 keeps the
// CSS viewport at the logical window size regardless of Windows DPI.
if (process.platform === 'win32') {
  app.commandLine.appendSwitch('force-device-scale-factor', '1');
}
const { spawn }  = require('child_process');
const path       = require('path');
const fs         = require('fs');
const http       = require('http');

// ── Config ────────────────────────────────────────────────────────────────────

const PORT    = 5897;
const isDev   = !app.isPackaged;

let mainWindow   = null;
let splashWindow = null;
let serverProc   = null;

// ── Path helpers ──────────────────────────────────────────────────────────────

function serverBinDir() {
  return isDev
    ? path.join(__dirname, 'server')
    : path.join(process.resourcesPath, 'server-bin');
}

function serverExe() {
  if (isDev) return null; // uses dotnet run
  const bin = process.platform === 'win32' ? 'MyQC.WebAPI.exe' : 'MyQC.WebAPI';
  return path.join(process.resourcesPath, 'server-bin', bin);
}

function userDataDir() {
  return app.getPath('userData');
}

// ── Start .NET server ─────────────────────────────────────────────────────────

function prepareUserData() {
  const dir = userDataDir();
  if (!fs.existsSync(dir)) fs.mkdirSync(dir, { recursive: true });

  // Copy the initial SQLite db to userData on first run so it lives
  // in a writable location, not inside Program Files.
  const dbDest = path.join(dir, 'trs398.db');
  if (!fs.existsSync(dbDest)) {
    const candidates = [
      path.join(serverBinDir(), 'trs398.db'),
      path.join(__dirname, 'server', 'trs398.db'),
    ];
    for (const src of candidates) {
      if (fs.existsSync(src)) { fs.copyFileSync(src, dbDest); break; }
    }
    // If no initial db found, EnsureCreated() in Program.cs will create one.
  }
}

function startServer() {
  return new Promise((resolve, reject) => {
    prepareUserData();

    const env = {
      ...process.env,
      ASPNETCORE_URLS:        `http://localhost:${PORT}`,
      ASPNETCORE_ENVIRONMENT: 'Production',
      TRS398_DATA_DIR:        userDataDir(),
    };

    if (isDev) {
      // Development: spawn dotnet run (ASPNETCORE_URLS env var sets the port).
      // Uses `dotnet` from PATH; override with the DOTNET_PATH env var if needed.
      const dotnetBin = process.env.DOTNET_PATH || 'dotnet';

      serverProc = spawn(dotnetBin, ['run'], {
        cwd:   path.join(__dirname, 'server'),
        env,
        stdio: ['ignore', 'pipe', 'pipe'],
      });
    } else {
      // Production: self-contained Windows exe
      serverProc = spawn(serverExe(), [], {
        cwd:   serverBinDir(),
        env,
        stdio: ['ignore', 'pipe', 'pipe'],
      });
    }

    serverProc.stdout?.on('data', d => process.stdout.write('[server] ' + d));
    serverProc.stderr?.on('data', d => process.stderr.write('[server] ' + d));
    serverProc.on('error', reject);
    serverProc.on('exit', code => {
      if (code !== 0 && code !== null)
        console.error(`[server] exited with code ${code}`);
    });

    pollHealth(resolve, reject);
  });
}

function pollHealth(resolve, reject) {
  const deadline = Date.now() + 90_000; // 90 s max

  function attempt() {
    const req = http.get(`http://localhost:${PORT}/api/health`, res => {
      if (res.statusCode === 200) return resolve();
      schedule();
    });
    req.on('error', schedule);
    req.setTimeout(1000, () => { req.destroy(); schedule(); });
  }

  function schedule() {
    if (Date.now() > deadline) return reject(new Error('Server did not start within 90 s'));
    setTimeout(attempt, 600);
  }

  setTimeout(attempt, 2000);
}

// ── Splash window ─────────────────────────────────────────────────────────────

function showSplash() {
  splashWindow = new BrowserWindow({
    width:      420,
    height:     280,
    frame:      false,
    resizable:  false,
    center:     true,
    alwaysOnTop: true,
    backgroundColor: '#0b1120',
    webPreferences: { contextIsolation: true, nodeIntegration: false },
  });
  splashWindow.loadFile(path.join(__dirname, 'splash.html'));
}

function setSplashStatus(msg, isError) {
  if (!splashWindow) return;
  const color = isError ? '#ef4444' : '#6b7fa3';
  splashWindow.webContents.executeJavaScript(
    `(function(){
       var el = document.getElementById('status');
       if(el){ el.textContent=${JSON.stringify(msg)}; el.style.color=${JSON.stringify(color)}; }
     })()`
  ).catch(() => {});
}

// ── Main window ───────────────────────────────────────────────────────────────

function createMainWindow() {
  mainWindow = new BrowserWindow({
    width:           1280,
    height:          840,
    minWidth:        900,
    minHeight:       600,
    backgroundColor: '#0b1120',
    show:            false,
    webPreferences: {
      preload:          path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration:  false,
      sandbox:          true,
    },
  });

  Menu.setApplicationMenu(null);

  // Block navigation outside localhost
  mainWindow.webContents.on('will-navigate', (e, url) => {
    if (!url.startsWith(`http://localhost:${PORT}`)) {
      e.preventDefault();
      shell.openExternal(url);
    }
  });
  mainWindow.webContents.setWindowOpenHandler(({ url }) => {
    shell.openExternal(url);
    return { action: 'deny' };
  });

  mainWindow.loadURL(`http://localhost:${PORT}/`);

  mainWindow.once('ready-to-show', () => {
    splashWindow?.close();
    splashWindow = null;
    mainWindow.show();
    mainWindow.focus();
  });

  mainWindow.on('closed', () => { mainWindow = null; });
}

// ── App lifecycle ─────────────────────────────────────────────────────────────

app.whenReady().then(async () => {
  showSplash();
  setSplashStatus('Starting TRS-398 server…');

  try {
    await startServer();
    setSplashStatus('Loading application…');
    createMainWindow();
  } catch (err) {
    console.error('[main] Failed to start server:', err.message);
    setSplashStatus('Server failed to start — check logs and restart.', true);
    setTimeout(() => app.quit(), 6000);
  }
});

app.on('before-quit', () => {
  serverProc?.kill();
  serverProc = null;
});

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') {
    serverProc?.kill();
    app.quit();
  }
});

app.on('activate', () => {
  if (!mainWindow && !splashWindow) {
    showSplash();
    startServer()
      .then(createMainWindow)
      .catch(err => {
        console.error(err);
        app.quit();
      });
  }
});
