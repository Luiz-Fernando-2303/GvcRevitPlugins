[Setup]
AppName=GVC Checagem de terreno
AppVersion=1.2
DefaultDirName={userappdata}\Autodesk\Revit\Addins\2025
OutputDir=.
OutputBaseFilename=GVC Checagem de terreno
Compression=lzma
SolidCompression=yes
DisableDirPage=no
PrivilegesRequired=lowest

[Languages]
Name: "portuguese"; MessagesFile: "compiler:Languages\Portuguese.isl"

[Files]
; Copia o arquivo .addin
Source: "GVCRevit.addin"; DestDir: "{app}"; Flags: ignoreversion

; Copia a pasta "Lupa Revit" inteira
Source: "Lupa Revit\*"; DestDir: "{app}\Lupa Revit"; Flags: recursesubdirs ignoreversion

[UninstallDelete]
Type: files; Name: "{app}\GVCRevit.addin"
Type: filesandordirs; Name: "{app}\Lupa Revit"
