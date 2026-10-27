# SimRig Studio

Drives the hardware of a truck-sim rig from the game's telemetry (Euro Truck Simulator 2 / American Truck Simulator):
analog gauge needles, WS2811 LED strips, Nextion touch displays, switched outputs. You write **formulas** (`truck.speed.kph`,
`x.fuelPercent`, …) in a web page; SimRig Studio evaluates them and sends the results to the boards over USB serial.

> Eski adıyla Rig Buddy Studio: Rig Buddy'nin içindeydi, bağımsız bir ürün olarak ayrıldı.

## Parts

| Folder | What |
|---|---|
| `app/` | The Node application: formula engine, board/LED/pin/Nextion drivers, the configuration page (http://127.0.0.1:62850/) |
| `host/` | The Windows host (.NET): owns the COM ports (serial hub, 127.0.0.1:62847) and reads the game's telemetry from the SCS plugin's shared memory (bridge, 127.0.0.1:62841); tray icon |
| `hardware/` | Octopus Pro gauge firmware (STM32F446), board notes, the portrait Nextion display |
| `dev/` | Tests (`npm test`) and a demo server with a fake board (`npm run demo`) |
| `setup/` | Installer (`SimRigStudio-Setup.exe`) and release scripts |

## Development

Everything is developed in Ubuntu (WSL); Windows only gets the released installer.

```bash
npm install --ignore-scripts
npm test          # engine, LEDs, pins, Nextion, power, sleep, re-home
npm run demo      # Studio page with a fake board on http://127.0.0.1:62898/
```

The Windows installer is built by GitHub Actions for a `v*.*.*` tag.

## Settings

`%LOCALAPPDATA%\SimRig Studio\data\studio.json` (set `SIMRIG_DATA` to move it). On first start the settings of an
existing Rig Buddy installation (`%LOCALAPPDATA%\Rig Buddy\data\studio.json`) are copied over.

## Telemetry plugin

Needs the [scs-sdk-plugin](https://github.com/truckermudgeon/scs-sdk-plugin) (MIT) in the game's `plugins` folder; the
installer offers to put it there.
