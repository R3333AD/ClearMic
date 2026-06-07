; ClearMic Inno Setup Script
; Phase 1-2 : avec dépendance VB-Cable
; Phase 3+  : standalone (APO inclus)

[Setup]
AppName=ClearMic
AppVersion=1.0.0
AppPublisher=R3333AD
DefaultDirName={autopf}\ClearMic
DefaultGroupName=ClearMic
UninstallDisplayIcon={app}\ClearMic.App.exe
OutputDir=..\releases
OutputBaseFilename=ClearMic-Setup-1.0.0

[Files]
Source: "..\ClearMic.App\bin\Release\net8.0-windows\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs
Source: "..\models\*"; DestDir: "{app}\models"; Flags: ignoreversion

[Icons]
Name: "{group}\ClearMic"; Filename: "{app}\ClearMic.App.exe"
Name: "{group}\Uninstall ClearMic"; Filename: "{uninstallexe}"

[Run]
Filename: "{app}\ClearMic.App.exe"; Description: "Launch ClearMic"; Flags: nowait postinstall skipifsilent
