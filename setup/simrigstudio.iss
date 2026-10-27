; SimRig Studio installer (Inno Setup 6). Built by setup\build-release.ps1, which stages the files first:
;   <SourceDir>\SimRigStudio.exe      self-contained .NET host
;   <SourceDir>\node\node.exe         Node.js runtime
;   <SourceDir>\app\, node_modules\, package.json   the Node app
;   <SourceDir>\plugin\scs-telemetry.dll
;   <SourceDir>\licenses\...
;
;   ISCC.exe /DAppVersion=0.1.0 /DSourceDir=... /DOutputDir=... setup\simrigstudio.iss
;
; Besides copying files: the SCS telemetry plugin into ETS2 / ATS and, optionally, autostart. Everything stays on
; this PC's loopback, so no firewall rules.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\local\release\app"
#endif
#ifndef OutputDir
  #define OutputDir "..\local\release"
#endif

[Setup]
AppId={{C4F0A5B2-8D31-4E6A-A7B9-31E2D6F4C805}
AppName=SimRig Studio
AppVersion={#AppVersion}
AppVerName=SimRig Studio {#AppVersion}
AppPublisher=Orhan Kökbudak
AppCopyright=Copyright (C) 2026 Orhan Kökbudak. GPL-3.0-or-later.
DefaultDirName={autopf}\SimRig Studio
DefaultGroupName=SimRig Studio
DisableProgramGroupPage=yes
; admin: the plugin goes into the game folders
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir={#OutputDir}
OutputBaseFilename=SimRigStudio-Setup
SetupIconFile=..\host\simrig.ico
UninstallDisplayIcon={app}\SimRigStudio.exe
UninstallDisplayName=SimRig Studio
WizardStyle=modern
Compression=lzma2/ultra64
SolidCompression=yes
ShowLanguageDialog=auto
UsePreviousLanguage=no
VersionInfoVersion={#AppVersion}
VersionInfoProductName=SimRig Studio

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "tr"; MessagesFile: "compiler:Languages\Turkish.isl"

[CustomMessages]
en.TaskPlugin=Install the telemetry plugin into ETS2 / ATS (needed to read the game)
en.TaskAutostart=Start SimRig Studio with Windows
en.StatusPlugin=Installing the telemetry plugin...
en.RemoveData=Also delete the settings SimRig Studio created?%n%n%1
tr.TaskPlugin=Telemetri eklentisini ETS2 / ATS'ye kur (oyunu okumak için gerekli)
tr.TaskAutostart=SimRig Studio'yu Windows ile birlikte başlat
tr.StatusPlugin=Telemetri eklentisi kuruluyor...
tr.RemoveData=SimRig Studio'nun oluşturduğu ayarlar da silinsin mi?%n%n%1

[Tasks]
Name: "plugin"; Description: "{cm:TaskPlugin}"
Name: "autostart"; Description: "{cm:TaskAutostart}"
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; Flags: unchecked

[InstallDelete]
; an update replaces the app as a whole (no stale files from older versions)
Type: filesandordirs; Name: "{app}\app"
Type: filesandordirs; Name: "{app}\node_modules"

[Files]
Source: "{#SourceDir}\SimRigStudio.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\package.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\node\*"; DestDir: "{app}\node"; Flags: ignoreversion recursesubdirs
Source: "{#SourceDir}\app\*"; DestDir: "{app}\app"; Flags: ignoreversion recursesubdirs
Source: "{#SourceDir}\node_modules\*"; DestDir: "{app}\node_modules"; Flags: ignoreversion recursesubdirs
Source: "{#SourceDir}\plugin\*"; DestDir: "{app}\plugin"; Flags: ignoreversion
Source: "{#SourceDir}\licenses\*"; DestDir: "{app}\licenses"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\SimRig Studio"; Filename: "{app}\SimRigStudio.exe"
Name: "{autodesktop}\SimRig Studio"; Filename: "{app}\SimRigStudio.exe"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "SimRig Studio"; \
  ValueData: """{app}\SimRigStudio.exe"" --autostart"; Tasks: autostart; Flags: uninsdeletevalue

[Run]
Filename: "{app}\SimRigStudio.exe"; Parameters: "--install-plugin"; Flags: runhidden; Tasks: plugin; StatusMsg: "{cm:StatusPlugin}"
Filename: "{app}\SimRigStudio.exe"; Description: "{cm:LaunchProgram,SimRig Studio}"; Flags: nowait postinstall skipifsilent runasoriginaluser

[UninstallRun]
; the telemetry plugin stays in the games: other telemetry tools use the same one

[Code]
const
  AppMutex = 'Local\SimRigStudio.Host';

{ Asks a running SimRig Studio (any copy) to quit and waits for it, so its files and ports are free. }
procedure QuitRunningApp(Exe: String);
var
  Code, Waited: Integer;
begin
  if not CheckForMutexes(AppMutex) then Exit;
  Exec(Exe, '--quit', '', SW_HIDE, ewWaitUntilTerminated, Code);
  Waited := 0;
  while CheckForMutexes(AppMutex) and (Waited < 15000) do
  begin
    Sleep(250);
    Waited := Waited + 250;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  if CheckForMutexes(AppMutex) then
  begin
    ExtractTemporaryFile('SimRigStudio.exe');
    QuitRunningApp(ExpandConstant('{tmp}\SimRigStudio.exe'));
  end;
  Result := '';
end;

function InitializeUninstall(): Boolean;
begin
  QuitRunningApp(ExpandConstant('{app}\SimRigStudio.exe'));
  Result := True;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Data: String;
begin
  if CurUninstallStep <> usPostUninstall then Exit;
  Data := ExpandConstant('{localappdata}\SimRig Studio');
  { silent uninstalls (updates by script) keep the data }
  if (not UninstallSilent) and DirExists(Data) and
     (MsgBox(FmtMessage(CustomMessage('RemoveData'), [Data]), mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES) then
    DelTree(Data, True, True, True);
end;
