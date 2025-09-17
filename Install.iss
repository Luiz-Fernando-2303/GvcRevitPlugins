[Setup]
AppName=GVC Checagem de terreno
AppVersion=1.2
DefaultDirName={userappdata}\Autodesk\Revit\Addins\2025 ; valor padrão temporário
OutputDir=.
OutputBaseFilename=GVC Checagem de terreno
Compression=lzma
SolidCompression=yes
DisableDirPage=no
PrivilegesRequired=lowest

[Files]
Source: "GVCRevit.addin"; DestDir: "{app}"; Flags: ignoreversion
Source: "Lupa Revit\*"; DestDir: "{app}\Lupa Revit"; Flags: recursesubdirs ignoreversion

[UninstallDelete]
Type: files; Name: "{app}\GVCRevit.addin"
Type: filesandordirs; Name: "{app}\Lupa Revit"

[Code]
var
  RevitVersionPage: TInputOptionWizardPage;

procedure InitializeWizard();
begin
  // Cria página de seleção do Revit
  RevitVersionPage := CreateInputOptionPage(
    wpSelectDir, // antes da página de seleção de pasta
    'Selecione a versão do Revit',
    'Escolha a versão do Revit que deseja instalar o plugin:',
    'Esta seleção determinará a pasta de instalação.',
    True, False
  );

  RevitVersionPage.Add('2022');
  RevitVersionPage.Add('2023');
  RevitVersionPage.Add('2024');
  RevitVersionPage.Add('2025');
  RevitVersionPage.Values[3] := True; // 2025 por padrão
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  i: Integer;
  SelectedVersion: string;
begin
  Result := True;

  // Quando avançar da página de seleção, atualiza o diretório
  if CurPageID = RevitVersionPage.ID then
  begin
    SelectedVersion := '2025'; // padrão
    for i := 0 to 3 do
      if RevitVersionPage.Values[i] then
      begin
        case i of
          0: SelectedVersion := '2022';
          1: SelectedVersion := '2023';
          2: SelectedVersion := '2024';
          3: SelectedVersion := '2025';
        end;
      end;

    // Atualiza o campo da pasta de instalação
    WizardForm.DirEdit.Text := ExpandConstant('{userappdata}\Autodesk\Revit\Addins\') + SelectedVersion;
  end;
end;
