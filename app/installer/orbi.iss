; ---------------------------------------------------------------------------
; Orbi — instalador para Windows (Inno Setup 6)
;
; Instalação por usuário (%LOCALAPPDATA%\Programs\Orbi), sem UAC.
; Gera: OrbiSetup-<versão>.exe + desinstalador + atalhos.
; ---------------------------------------------------------------------------

#define AppName "Orbi"
#define AppVersion "1.0.7"
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
WizardImageFile=assets\banner.bmp
WizardSmallImageFile=assets\wizard.bmp
WizardImageStretch=no
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
  LimpezaTexto: String;

{ Lê o log deixado pelo limpar-antigas.ps1 e guarda para mostrar no final. }
procedure LerLogLimpeza;
var
  Linhas: TArrayOfString;
  i: Integer;
  Arquivo: String;
begin
  LimpezaTexto := '';
  Arquivo := ExpandConstant('{tmp}\orbi-limpeza.txt');
  if not FileExists(Arquivo) then
    Arquivo := GetEnv('TEMP') + '\orbi-limpeza.txt';
  if FileExists(Arquivo) and LoadStringsFromFile(Arquivo, Linhas) then
  begin
    for i := 0 to GetArrayLength(Linhas) - 1 do
      LimpezaTexto := LimpezaTexto + Linhas[i] + #13#10;
  end;
end;

{ Roda limpar-antigas.ps1 ANTES de instalar. O script precisa ser extraído para
  a pasta temporária antes (ExtractTemporaryFile), porque a cópia da seção [Files]
  só acontece depois desta etapa — era esse o motivo da limpeza nunca rodar. }
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Script, Comando, LogArquivo: String;
  Codigo: Integer;
begin
  Result := '';
  LimpezaOk := False;
  LerLogLimpeza;

  Script := ExpandConstant('{tmp}\limpar-antigas.ps1');
  { ExtractTemporaryFile é procedure: ela mesma levanta erro se o arquivo não
    existir no setup (o [Files] usa dontcopy justamente para isso). }
  ExtractTemporaryFile('limpar-antigas.ps1');
  if not FileExists(Script) then
  begin
    Log('Limpeza: não consegui extrair limpar-antigas.ps1.');
    LimpezaOk := True;
    Exit;
  end;

  LogArquivo := ExpandConstant('{tmp}\orbi-limpeza.txt');

  WizardForm.StatusLabel.Caption := 'Removendo versões antigas do Orbi…';
  Comando := '-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "' + Script +
             '" -Silencioso -Log "' + LogArquivo + '"';

  if not Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'), Comando,
              '', SW_HIDE, ewWaitUntilTerminated, Codigo) then
  begin
    { sem PowerShell: segue, o Inno ainda sobrescreve os arquivos }
    Log('Limpeza: não consegui executar o PowerShell.');
    LimpezaOk := True;
    Exit;
  end;

  Log('Limpeza executada, código ' + IntToStr(Codigo));
  LerLogLimpeza;
  LimpezaOk := True;
end;

procedure CurPageChanged(CurPageID: Integer);
var
  Resumo: String;
begin
  if CurPageID = wpFinished then
  begin
    { O que fazer agora, sem precisar procurar nada. }
    WizardForm.FinishedLabel.Caption :=
      'O Orbi está no Menu Iniciar e na bandeja, perto do relógio.' + #13#10 +
      'Aperte Alt+Shift+O em qualquer programa para abrir a barra.';
  end;

  if (CurPageID = wpFinished) and (LimpezaTexto <> '') then
  begin
    Resumo := Trim(LimpezaTexto);
    if Length(Resumo) > 700 then
      Resumo := Copy(Resumo, 1, 700) + '…';
    WizardForm.FinishedLabel.Caption :=
      WizardForm.FinishedLabel.Caption + #13#10#13#10 +
      'Limpeza das versões antigas:' + #13#10 + Resumo;
  end;
end;

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na área de trabalho"; GroupDescription: "Atalhos:"; Flags: checkedonce
Name: "startup"; Description: "Abrir o Orbi junto com o Windows"; GroupDescription: "Inicialização:"; Flags: checkedonce

[Files]
Source: "..\..\artifacts\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "limpar-antigas.ps1"; DestDir: "{tmp}"; Flags: dontcopy

[Icons]
Name: "{group}\Orbi"; Filename: "{app}\{#AppExe}"
Name: "{group}\Ajustes do Orbi"; Filename: "{app}\{#AppExe}"; Parameters: "--settings"
Name: "{group}\Orbi (diagnóstico)"; Filename: "{app}\{#AppExe}"; Parameters: "--diag"
Name: "{group}\Orbi (abrir com terminal)"; Filename: "{cmd}"; Parameters: "/k ""{app}\{#AppExe}"" --console-keep"; WorkingDir: "{app}"; IconFilename: "{app}\{#AppExe}"
Name: "{group}\Orbi (abrir log em arquivo)"; Filename: "{app}\{#AppExe}"; Parameters: "--console-keep"
Name: "{group}\Orbi (autoteste)"; Filename: "{app}\{#AppExe}"; Parameters: "--selftest"
Name: "{group}\Orbi (modo seguro)"; Filename: "{app}\{#AppExe}"; Parameters: "--safe"
Name: "{group}\Desinstalar o Orbi"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Orbi"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon
Name: "{userstartup}\Orbi"; Filename: "{app}\{#AppExe}"; Parameters: "--tray"; Tasks: startup

[Run]
; 1º: abre a barra e, junto, o diagnóstico — assim a pessoa SEMPRE vê algo.
Filename: "{app}\{#AppExe}"; Parameters: "--diag"; Description: "Abrir o Orbi agora"; Flags: nowait postinstall skipifsilent
; Terminal com o log ao vivo (CONOUT$ — antes o terminal abria vazio).
Filename: "{cmd}"; Parameters: "/k ""{app}\{#AppExe}"" --console-keep"; Description: "Abrir com terminal (log ao vivo)"; Flags: postinstall unchecked nowait skipifsilent
Filename: "{app}\{#AppExe}"; Parameters: "--selftest"; Description: "Autoteste (gera orbi-selftest.txt na Area de trabalho)"; Flags: postinstall unchecked nowait skipifsilent

[UninstallRun]
Filename: "{app}\{#AppExe}"; Parameters: "--uninstall"; Flags: runhidden

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
