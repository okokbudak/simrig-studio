# Builds SimRigStudio-Setup.exe into local\release (used by the GitHub release workflow, on a Windows runner):
#
#   powershell -ExecutionPolicy Bypass -File setup\build-release.ps1 -Version 0.1.0
#
# Needs: Node.js (npm) to fetch the app's dependencies, the .NET 8 SDK, Inno Setup 6.
param(
  [Parameter(Mandatory)][string]$Version
)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$Root = Split-Path $PSScriptRoot -Parent
$PluginUrl = 'https://github.com/truckermudgeon/scs-sdk-plugin/releases/download/v1.12.1/build-windows-latest.zip'
$out = Join-Path $Root 'local\release'
$app = Join-Path $out 'app'

function Step($msg) { Write-Host "`n=== $msg" -ForegroundColor Cyan }
function Exec([scriptblock]$Block, [string]$What) {
  & $Block
  if ($LASTEXITCODE -ne 0) { throw "$What failed (exit code $LASTEXITCODE)" }
}
function Get-File([string]$Url, [string]$OutFile) {
  Write-Host "  download $Url"
  Invoke-WebRequest -UseBasicParsing -Uri $Url -OutFile $OutFile
}

if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "version must look like 1.2.3 (got '$Version')" }
Remove-Item $out -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $app | Out-Null

Step 'tests'
Exec { node "$Root\dev\test-studio.mjs" } 'tests'

Step 'app dependencies (no dev packages, no native builds)'
Push-Location $Root
try { Exec { npm ci --omit=dev --ignore-scripts --no-audit --no-fund } 'npm ci' } finally { Pop-Location }

Step 'telemetry shim (stands in for the native addon)'
$shimDir = Join-Path $Root 'node_modules\trucksim-telemetry\build\Release'
New-Item -ItemType Directory -Force $shimDir | Out-Null
Copy-Item "$Root\app\scsSDKTelemetry.cjs" (Join-Path $shimDir 'scsSDKTelemetry.js')

Step 'Node.js runtime'
$base = 'https://nodejs.org/dist/latest-v24.x'
$sums = (Invoke-WebRequest -UseBasicParsing "$base/SHASUMS256.txt").Content
$line = ($sums -split "`n") | Where-Object { $_ -match 'node-v[\d.]+-win-x64\.zip\s*$' } | Select-Object -First 1
if (-not $line) { throw 'no Node.js win-x64 zip in SHASUMS256.txt' }
$hash, $file = $line.Trim() -split '\s+'
$zip = Join-Path $out $file
Get-File "$base/$file" $zip
if ((Get-FileHash $zip -Algorithm SHA256).Hash.ToLower() -ne $hash.ToLower()) { throw 'Node.js download: checksum mismatch' }
Expand-Archive $zip "$out\nodezip" -Force
$nodeDir = Join-Path "$out\nodezip" ($file -replace '\.zip$', '')

Step 'host (self-contained, no .NET install needed)'
Exec {
  dotnet publish "$Root\host\SimRigStudio.csproj" -c Release -o "$out\publish" --nologo -v q `
    -p:SelfContained=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=none -p:Version=$Version
} 'dotnet publish'

Step 'stage files'
Copy-Item "$out\publish\SimRigStudio.exe" $app
New-Item -ItemType Directory -Force "$app\node", "$app\plugin", "$app\licenses" | Out-Null
Copy-Item "$nodeDir\node.exe" "$app\node\"
Copy-Item "$Root\app" "$app\app" -Recurse
Copy-Item "$Root\node_modules" "$app\node_modules" -Recurse
Copy-Item "$Root\package.json" $app
$zip = Join-Path $out 'plugin.zip'
Get-File $PluginUrl $zip
Expand-Archive $zip "$out\plugin" -Force
$dll = Get-ChildItem "$out\plugin" -Recurse -Filter 'scs-telemetry.dll' | Select-Object -First 1
if (-not $dll) { throw 'scs-telemetry.dll not in the plugin download' }
Copy-Item $dll.FullName "$app\plugin\"
Copy-Item "$Root\LICENSE" "$app\licenses\LICENSE.txt"
Copy-Item "$Root\THIRD-PARTY.md" "$app\licenses\THIRD-PARTY.md"
Copy-Item "$nodeDir\LICENSE" "$app\licenses\node.js-LICENSE.txt"
# raw file, not the API: the API's anonymous rate limit is often used up on shared CI machines
Get-File 'https://raw.githubusercontent.com/truckermudgeon/scs-sdk-plugin/HEAD/LICENSE' "$app\licenses\scs-sdk-plugin-LICENSE.txt"
$size = (Get-ChildItem $app -Recurse -File | Measure-Object Length -Sum).Sum / 1MB
Write-Host ("  {0:N0} MB staged" -f $size)

Step 'installer (Inno Setup)'
$iscc = @("$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe", "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
  "$env:ProgramFiles\Inno Setup 6\ISCC.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup 6 not found' }
Exec { & $iscc /Q "/DAppVersion=$Version" "/DSourceDir=$app" "/DOutputDir=$out" "$Root\setup\simrigstudio.iss" } 'ISCC'

Get-ChildItem $out -File -Filter '*.exe' | ForEach-Object { '{0,-26} {1,8:N1} MB  sha256 {2}' -f $_.Name, ($_.Length / 1MB), (Get-FileHash $_.FullName).Hash.ToLower() }
