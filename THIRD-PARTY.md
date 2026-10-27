# Third-party software shipped with SimRig Studio

| Component | License | Where |
|---|---|---|
| [Node.js](https://nodejs.org) runtime | MIT | `node\node.exe`, license in `licenses\node.js-LICENSE.txt` |
| [scs-sdk-plugin](https://github.com/truckermudgeon/scs-sdk-plugin) (telemetry plugin, `scs-telemetry.dll`) | MIT | `plugin\`, license in `licenses\scs-sdk-plugin-LICENSE.txt` |
| [trucksim-telemetry](https://github.com/kniffen/TruckSim-Telemetry) | MIT | `node_modules\` (parser of the plugin's shared memory) |
| [ws](https://github.com/websockets/ws) | MIT | `node_modules\` |
| [System.IO.Ports](https://www.nuget.org/packages/System.IO.Ports) | MIT | linked into `SimRigStudio.exe` |
| .NET 8 runtime | MIT | linked into `SimRigStudio.exe` (self-contained) |

The SCS Software SDK headers (`scssdk_*.h`, via the plugin) are used under SCS Software's SDK license.
SimRig Studio itself is GPL-3.0-or-later (see `LICENSE`).
