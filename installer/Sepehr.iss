; Inno Setup script - builds installer/Output/Setup.exe from the "publish" folder.
#define AppName "Sepehr School Management"
#define AppVer "0.1.0"

[Setup]
AppId={{6E2C8B0A-5D0B-4F0E-9B7D-5E9E0C5A2A11}
AppName={#AppName}
AppVersion={#AppVer}
DefaultDirName={pf}\Sepehr
DefaultGroupName=Sepehr
OutputDir=Output
OutputBaseFilename=Setup
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=admin
UninstallDisplayIcon={app}\Sepehr.exe
WizardStyle=modern

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Dirs]
; Data folder lives outside Program Files so the app can write without admin rights.
Name: "{commonappdata}\Sepehr"; Permissions: users-modify
Name: "{commonappdata}\Sepehr\data"; Permissions: users-modify
Name: "{commonappdata}\Sepehr\backups"; Permissions: users-modify
Name: "{commonappdata}\Sepehr\logs"; Permissions: users-modify
Name: "{commonappdata}\Sepehr\media"; Permissions: users-modify

[Icons]
Name: "{group}\Sepehr"; Filename: "{app}\Sepehr.exe"
Name: "{group}\Uninstall Sepehr"; Filename: "{uninstallexe}"
Name: "{commondesktop}\Sepehr"; Filename: "{app}\Sepehr.exe"

[Run]
Filename: "{app}\Sepehr.exe"; Description: "Launch Sepehr"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeSetup(): Boolean;
var Release: Cardinal;
begin
  Result := True;
  // .NET Framework 4.8 => Release >= 528040
  if not RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release) or (Release < 528040) then
  begin
    MsgBox('.NET Framework 4.8 is required and was not found.' + #13#10 +
           'Install it first (on Windows 7 this also needs Service Pack 1), then run this setup again.', mbError, MB_OK);
    Result := False;
  end;
end;
