; ---------------------------------------------------------------------------
; Orbi — instalador para Windows (Inno Setup 6)
;
; Instalação por usuário (%LOCALAPPDATA%\Programs\Orbi), sem UAC.
; Gera: OrbiSetup-<versão>.exe + desinstalador + atalhos.
; ---------------------------------------------------------------------------

#define AppName "Orbi"
#define AppVersion "1.0.3"
#define AppPublisher "Orbi"
#define AppURL "https://orbi-app-auth.web.app"
#define AppExe "Orbi.exe"

[Setup]
AppMutex=Orbi.App.SingleInstance
AppId={{8F3C1A54-9C8E-4F42-9A6B-7E1F0D2C4B71}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL=https://github.com/Arthurowgg/TheOrbiApp/issues
AppUpdatesURL=https://github.com/Arthurowgg/TheOrbiApp/releases
DefaultDirName={localappdata}\Programs\Orbi
DefaultGroupName=Orbi
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=..\..\artifacts
OutputBaseFilename=OrbiSetup-{#AppVersion}
SetupIconFile=..\Orbi.App\Assets\orbi.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName} {#AppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
MinVersion=10.0.17763
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no
CloseApplicationsFilter=*.exe,*.dll
LicenseFile=license.txt
InfoBeforeFile=antes.txt
ChangesAssociations=no
AllowNoIcons=yes

[Code]
var
  LimpezaOk: Boolean;

{ Roda limpar-antigas.ps1 antes de instalar: encerra processos, desinstala
  versões encontradas, apaga pastas e atalhos antigos. }
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Script, Comando: String;
  Codigo: Integer;
begin
  Result := '';
  LimpezaOk := False;

  Script := ExpandConstant('{tmp}\limpar-antigas.ps1');
  if not FileExists(Script) then
  begin
    LimpezaOk := True;
    Exit;
  end;

  WizardForm.StatusLabel.Caption := 'Removendo versões antigas do Orbi…';
  Comando := '-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "' + Script + '" -Silencioso';

  if not Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'), Comando,
              '', SW_HIDE, ewWaitUntilTerminated, Codigo) then
  begin
    { sem PowerShell: segue, o Inno ainda sobrescreve os arquivos }
    Log('Limpeza: não consegui executar o PowerShell.');
    LimpezaOk := True;
    Exit;
  end;

  Log('Limpeza executada, código ' + IntToStr(Codigo));
  LimpezaOk := True;
end;

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na área de trabalho"; GroupDescription: "Atalhos:"; Flags: checkedonce
Name: "startup"; Description: "Abrir o Orbi junto com o Windows"; GroupDescription: "Inicialização:"; Flags: checkedonce

[Files]
Source: "..\..\artifacts\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "limpar-antigas.ps1"; DestDir: "{tmp}"; Flags: deleteafterinstall

[Icons]
Name: "{group}\Orbi"; Filename: "{app}\{#AppExe}"
Name: "{group}\Ajustes do Orbi"; Filename: "{app}\{#AppExe}"; Parameters: "--settings"
Name: "{group}\Orbi (diagnóstico)"; Filename: "{app}\{#AppExe}"; Parameters: "--diag"
Name: "{group}\Orbi (abrir com terminal)"; Filename: "{cmd}"; Parameters: "/k ""{app}\{#AppExe}"" --console"; WorkingDir: "{app}"; IconFilename: "{app}\{#AppExe}"
Name: "{group}\Orbi (modo seguro)"; Filename: "{app}\{#AppExe}"; Parameters: "--safe"
Name: "{group}\Desinstalar o Orbi"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Orbi"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon
Name: "{userstartup}\Orbi"; Filename: "{app}\{#AppExe}"; Parameters: "--tray"; Tasks: startup

[Run]
Filename: "{app}\{#AppExe}"; Description: "Abrir o Orbi agora"; Flags: nowait postinstall skipifsilent
Filename: "{cmd}"; Parameters: "/k ""{app}\{#AppExe}"" --console"; Description: "Abrir com terminal (mostra o log ao vivo)"; Flags: postinstall unchecked nowait skipifsilent
Filename: "{app}\{#AppExe}"; Parameters: "--diag"; Description: "Abrir o diagnóstico (se a barra não aparecer)"; Flags: postinstall unchecked nowait skipifsilent

[UninstallRun]
Filename: "{app}\{#AppExe}"; Parameters: "--uninstall"; Flags: runhidden

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
