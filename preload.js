'use strict';

const { contextBridge } = require('electron');

// Minimal surface — the app runs entirely as a web page served from localhost.
// No IPC needed beyond this placeholder so contextIsolation stays intact.
contextBridge.exposeInMainWorld('electronApp', {
  version: process.env.npm_package_version ?? '2.1.0',
});
