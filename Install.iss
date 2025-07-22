[Setup]
AppName=GVC Checagem de terreno
AppVersion=1.0
DefaultDirName={userappdata}\Autodesk\Revit\Addins\2025
OutputDir=.
OutputBaseFilename=GVC Checagem de terreno
Compression=lzma
SolidCompression=yes
DisableDirPage=no

[Languages]
Name: "portuguese"; MessagesFile: "compiler:Languages\Portuguese.isl"

[Files]
; Copia o arquivo .addin
Source: "GVCRevit.addin"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2025"; Flags: ignoreversion

; Copia a pasta "Lupa Revit" inteira
Source: "Lupa Revit\*"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2025\Lupa Revit"; Flags: recursesubdirs ignoreversion

[UninstallDelete]
Type: files; Name: "{userappdata}\Autodesk\Revit\Addins\2025\GVCRevit.addin"
Type: filesandordirs; Name: "{userappdata}\Autodesk\Revit\Addins\2025\Lupa Revit"
