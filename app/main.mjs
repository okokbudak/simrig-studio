// SimRig Studio: formulas -> serial gauge boards, LEDs and Nextion displays, configured at http://127.0.0.1:62870/.
//
// This process decides what to send; SimRigStudio.exe (host/) owns the COM ports (serial hub, 127.0.0.1:62872) and
// reads the game's telemetry (telemetry bridge, 127.0.0.1:62871), which the shim below hands to trucksim-telemetry.
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createEngine } from './engine.mjs';
import { createHub } from './serialhub.mjs';
import { startStudio } from './server.mjs';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const WINDOWS = process.platform === 'win32';

/** Where the settings live: SIMRIG_DATA, else %LOCALAPPDATA%\SimRig Studio\data (Windows), else ~/.simrig-studio. */
function dataDir() {
  if (process.env.SIMRIG_DATA) return process.env.SIMRIG_DATA;
  if (WINDOWS) return path.join(process.env.LOCALAPPDATA ?? path.join(os.homedir(), 'AppData', 'Local'), 'SimRig Studio', 'data');
  return path.join(os.homedir(), '.simrig-studio');
}

/** Studio used to be part of Rig Buddy: take its settings over once (copied, the old file stays where it is). */
function takeOverRigBuddySettings(dir) {
  const target = path.join(dir, 'studio.json');
  if (fs.existsSync(target) || !WINDOWS || !process.env.LOCALAPPDATA) return;
  const legacy = path.join(process.env.LOCALAPPDATA, 'Rig Buddy', 'data', 'studio.json');
  if (!fs.existsSync(legacy)) return;
  fs.copyFileSync(legacy, target);
  console.log(`settings taken over from Rig Buddy: ${legacy}`);
}

/**
 * trucksim-telemetry loads a native addon that maps the game's shared memory (and needs a C++ compiler to build).
 * Here the addon is a small JS file that takes the memory from the host's bridge instead.
 */
function installTelemetryShim() {
  const dir = path.join(HERE, '..', 'node_modules', 'trucksim-telemetry', 'build', 'Release');
  const target = path.join(dir, 'scsSDKTelemetry.js');
  const shim = path.join(HERE, 'scsSDKTelemetry.cjs');
  if (fs.existsSync(target) && fs.readFileSync(target, 'utf8') === fs.readFileSync(shim, 'utf8')) return;
  fs.mkdirSync(dir, { recursive: true });
  fs.copyFileSync(shim, target);
}

try {
  installTelemetryShim();
} catch (e) {
  console.log(`telemetry shim: not (re)installed (${e.message}); using the one that is there`);
}
const { default: tst } = await import('trucksim-telemetry');

/** Full parsed telemetry (every property the plugin has), or null while no game runs. */
function readRaw() {
  try {
    const t = tst.getData();
    return t && !(t.truck.position.X === 0 && t.truck.position.Z === 0) ? t : null;
  } catch {
    return null;
  }
}

const DATA_DIR = dataDir();
fs.mkdirSync(DATA_DIR, { recursive: true });
takeOverRigBuddySettings(DATA_DIR);

const hub = createHub();
const engine = createEngine({ hub, getRaw: readRaw, dataDir: DATA_DIR });
startStudio({ engine, hub });
