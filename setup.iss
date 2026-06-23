; Inno Setup Script for Revit MCP Plugin & Server
#define MyAppName "Revit MCP Plugin & Server"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Kasymov Islombek"
#define MyAppURL "https://github.com/jrekss/mcp-servers-for-revit"

[Setup]
AppId={{D1A25C76-79F8-4A1C-89B2-36E088F5B899}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autoappdata}\revit_mcp_plugin
DisableDirPage=yes
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog commandline
OutputDir=.
OutputBaseFilename=RevitMCPSetup
Compression=lzma
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[CustomMessages]
english.RevitTitle=Select Revit Versions
english.RevitSubTitle=Which Autodesk Revit versions would you like to install the plugin for?
english.Revit2022=Autodesk Revit 2022
english.Revit2023=Autodesk Revit 2023
english.Revit2024=Autodesk Revit 2024
english.Revit2025=Autodesk Revit 2025
english.Revit2026=Autodesk Revit 2026
english.Revit2027=Autodesk Revit 2027
english.ClientsTitle=Select AI Clients to Configure
english.ClientsSubTitle=Which AI editors or IDEs should be automatically configured to use this MCP server?
english.ClientCursor=Cursor
english.ClientWindsurf=Windsurf
english.ClientClaude=Claude Desktop
english.ClientCline=Cline (VS Code)
english.ClientVSCode=VS Code (mcp.json)
english.ClientCopilot=GitHub Copilot CLI
english.ClientGeminiCli=Gemini CLI
english.ClientClaudeCode=Claude Code
english.ClientWarp=Warp
english.ClientAntigravity=Antigravity IDE
english.ClientCodex=Codex
english.ClientOpenClaw=OpenClaw
english.ClientHermes=Hermes
english.ClientOpenCode=OpenCode

russian.RevitTitle=Выберите версии Revit
russian.RevitSubTitle=Для каких версий Autodesk Revit вы хотите установить плагин?
russian.Revit2022=Autodesk Revit 2022
russian.Revit2023=Autodesk Revit 2023
russian.Revit2024=Autodesk Revit 2024
russian.Revit2025=Autodesk Revit 2025
russian.Revit2026=Autodesk Revit 2026
russian.Revit2027=Autodesk Revit 2027
russian.ClientsTitle=Выберите ИИ-клиенты
russian.ClientsSubTitle=Какие редакторы или среды разработки настроить для использования этого MCP-сервера?
russian.ClientCursor=Cursor
russian.ClientWindsurf=Windsurf
russian.ClientClaude=Claude Desktop
russian.ClientCline=Cline (VS Code)
russian.ClientVSCode=VS Code (mcp.json)
russian.ClientCopilot=GitHub Copilot CLI
russian.ClientGeminiCli=Gemini CLI
russian.ClientClaudeCode=Claude Code
russian.ClientWarp=Warp
russian.ClientAntigravity=Antigravity IDE
russian.ClientCodex=Codex
russian.ClientOpenClaw=OpenClaw
russian.ClientHermes=Hermes
russian.ClientOpenCode=OpenCode

[Files]
; Conditional deployment of C# addins based on custom Pascal page checkbox state
Source: "plugin\bin\AddIn 2022 Release R22\*"; DestDir: "{autoappdata}\Autodesk\Revit\Addins\2022"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: InstallRevit2022
Source: "plugin\bin\AddIn 2023 Release R23\*"; DestDir: "{autoappdata}\Autodesk\Revit\Addins\2023"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: InstallRevit2023
Source: "plugin\bin\AddIn 2024 Release R24\*"; DestDir: "{autoappdata}\Autodesk\Revit\Addins\2024"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: InstallRevit2024
Source: "plugin\bin\AddIn 2025 Release R25\*"; DestDir: "{autoappdata}\Autodesk\Revit\Addins\2025"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: InstallRevit2025
Source: "plugin\bin\AddIn 2026 Release R26\*"; DestDir: "{autoappdata}\Autodesk\Revit\Addins\2026"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: InstallRevit2026
Source: "plugin\bin\AddIn 2027 Release R27\*"; DestDir: "{autoappdata}\Autodesk\Revit\Addins\2027"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: InstallRevit2027

; Copy MCP Server files
Source: "server\build\*"; DestDir: "{app}\server\build"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "server\node_modules\*"; DestDir: "{app}\server\node_modules"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "server\package.json"; DestDir: "{app}\server"; Flags: ignoreversion

; Copy install.exe helper to configure IDE settings
Source: "install.exe"; DestDir: "{app}"; Flags: ignoreversion

[Run]
; Run the configuration helper silently at the end of the installation to modify IDE JSONs
Filename: "{app}\install.exe"; Parameters: "--config-only --user-appdata ""{userappdata}"" --user-profile ""{code:GetUserProfile}"" --clients {code:GetSelectedClients}"; StatusMsg: "Configuring AI clients..."; Flags: runhidden

[Code]
var
  RevitPage: TInputOptionWizardPage;
  ClientsPage: TInputOptionWizardPage;
  
  IdxRevit2022, IdxRevit2023, IdxRevit2024, IdxRevit2025, IdxRevit2026, IdxRevit2027: Integer;
  IdxCursor, IdxWindsurf, IdxClaude, IdxCline, IdxVSCode, IdxCopilot, IdxGeminiCli, IdxClaudeCode, IdxWarp, IdxAntigravity, IdxCodex, IdxOpenClaw, IdxHermes, IdxOpenCode: Integer;

procedure LogToFile(Msg: String);
var
  LogPath: String;
  Lines: TStringList;
begin
  LogPath := ExpandConstant('{tmp}\revit_mcp_setup_debug.log');
  Lines := TStringList.Create;
  try
    if FileExists(LogPath) then
      Lines.LoadFromFile(LogPath);
    Lines.Add(Msg);
    Lines.SaveToFile(LogPath);
  finally
    Lines.Free;
  end;
end;

function GetUserProfile(Param: String): String;
var
  AppDataPath: String;
  LowerPath: String;
  PosApp: Integer;
begin
  AppDataPath := ExpandConstant('{userappdata}');
  LowerPath := Lowercase(AppDataPath);
  PosApp := Pos('\appdata\roaming', LowerPath);
  if PosApp > 0 then
    Result := Copy(AppDataPath, 1, PosApp - 1)
  else
    Result := ExpandConstant('{%USERPROFILE}');
end;

function GetUserProfilePath: String;
var
  AppDataPath: String;
  LowerPath: String;
  PosApp: Integer;
begin
  AppDataPath := ExpandConstant('{userappdata}');
  LowerPath := Lowercase(AppDataPath);
  PosApp := Pos('\appdata\roaming', LowerPath);
  if PosApp > 0 then
    Result := Copy(AppDataPath, 1, PosApp - 1)
  else
    Result := ExpandConstant('{%USERPROFILE}');
end;

function IsRevitInstalled(Version: String): Boolean;
begin
  Result := RegKeyExists(HKEY_LOCAL_MACHINE, 'SOFTWARE\Autodesk\Revit\' + Version) or
            RegKeyExists(HKEY_CURRENT_USER, 'SOFTWARE\Autodesk\Revit\' + Version) or
            RegKeyExists(HKEY_LOCAL_MACHINE, 'SOFTWARE\Autodesk\Revit\Autodesk Revit ' + Version) or
            RegKeyExists(HKEY_CURRENT_USER, 'SOFTWARE\Autodesk\Revit\Autodesk Revit ' + Version);
end;

function IsClientInstalled(Id: String): Boolean;
var
  UserProfile: String;
begin
  Result := False;
  UserProfile := GetUserProfilePath;
  if Id = 'cursor' then
    Result := DirExists(ExpandConstant('{userappdata}\Cursor')) or
              FileExists(UserProfile + '\.cursor\mcp.json') or
              FileExists(ExpandConstant('{userappdata}\Cursor\User\globalStorage\storage.json'));
  if Id = 'windsurf' then
    Result := DirExists(ExpandConstant('{userappdata}\Windsurf')) or
              DirExists(ExpandConstant('{userappdata}\Code - Windsurf')) or
              FileExists(UserProfile + '\.codeium\windsurf\mcp_config.json') or
              FileExists(ExpandConstant('{userappdata}\Windsurf\User\globalStorage\storage.json')) or
              FileExists(ExpandConstant('{userappdata}\Code - Windsurf\User\globalStorage\storage.json'));
  if Id = 'claude' then
    Result := DirExists(ExpandConstant('{userappdata}\Claude')) or
              DirExists(ExpandConstant('{userappdata}\Subliminal\Claude')) or
              DirExists(ExpandConstant('{userappdata}\EasyConnect\Claude')) or
              FileExists(ExpandConstant('{userappdata}\Claude\claude_desktop_config.json'));
  if Id = 'cline' then
    Result := DirExists(ExpandConstant('{userappdata}\Code\User\globalStorage\saoudrizwan.claude-dev')) or
              DirExists(ExpandConstant('{userappdata}\Antigravity IDE\User\globalStorage\saoudrizwan.claude-dev')) or
              FileExists(ExpandConstant('{userappdata}\Code\User\globalStorage\saoudrizwan.claude-dev\settings\cline_mcp_settings.json')) or
              FileExists(ExpandConstant('{userappdata}\Antigravity IDE\User\globalStorage\saoudrizwan.claude-dev\settings\cline_mcp_settings.json'));
  if Id = 'vscode' then
    Result := DirExists(ExpandConstant('{userappdata}\Code\User')) or
              FileExists(ExpandConstant('{userappdata}\Code\User\mcp.json'));
  if Id = 'copilot' then
    Result := DirExists(UserProfile + '\.copilot') or
              FileExists(UserProfile + '\.copilot\config.json') or
              FileExists(UserProfile + '\.copilot\mcp-config.json');
  if Id = 'gemini_cli' then
    Result := DirExists(UserProfile + '\.gemini') or
              FileExists(UserProfile + '\.gemini\settings.json') or
              FileExists(UserProfile + '\.gemini\config\mcp_config.json');
  if Id = 'claude_code' then
    Result := FileExists(UserProfile + '\.claude.json') or
              FileExists(UserProfile + '\.mcp.json');
  if Id = 'warp' then
    Result := DirExists(UserProfile + '\.warp') or
              FileExists(UserProfile + '\.warp\mcp.json');
  if Id = 'antigravity' then
    Result := DirExists(ExpandConstant('{userappdata}\Antigravity IDE')) or
              FileExists(ExpandConstant('{userappdata}\Antigravity IDE\User\globalStorage\storage.json')) or
              FileExists(UserProfile + '\.gemini\config\mcp_config.json');
  if Id = 'opencode' then
    Result := DirExists(ExpandConstant('{userappdata}\opencode')) or
              DirExists(ExpandConstant('{localappdata}\opencode')) or
              FileExists(UserProfile + '\.opencode.json') or
              FileExists(UserProfile + '\.config\opencode\opencode.json') or
              DirExists(ExpandConstant('{userappdata}\ai.opencode.desktop')) or
              FileExists(ExpandConstant('{userappdata}\ai.opencode.desktop\User\globalStorage\storage.json'));
  if Id = 'openclaw' then
    Result := DirExists(UserProfile + '\.openclaw') or
              FileExists(UserProfile + '\.openclaw\openclaw.json');
  if Id = 'hermes' then
    Result := DirExists(UserProfile + '\.hermes') or
              FileExists(UserProfile + '\.hermes\config.yaml');
  if Id = 'codex' then
    Result := DirExists(UserProfile + '\.codex') or
              FileExists(UserProfile + '\.codex\config.toml');
end;


function AnyRevitInstalled: Boolean;
begin
  Result := IsRevitInstalled('2022') or
            IsRevitInstalled('2023') or
            IsRevitInstalled('2024') or
            IsRevitInstalled('2025') or
            IsRevitInstalled('2026') or
            IsRevitInstalled('2027');
end;

function AnyClientInstalled: Boolean;
begin
  Result := IsClientInstalled('cursor') or
            IsClientInstalled('windsurf') or
            IsClientInstalled('claude') or
            IsClientInstalled('cline') or
            IsClientInstalled('vscode') or
            IsClientInstalled('copilot') or
            IsClientInstalled('gemini_cli') or
            IsClientInstalled('claude_code') or
            IsClientInstalled('warp') or
            IsClientInstalled('antigravity') or
            IsClientInstalled('codex') or
            IsClientInstalled('openclaw') or
            IsClientInstalled('hermes') or
            IsClientInstalled('opencode');
end;

function MyBoolToStr(B: Boolean): String;
begin
  if B then Result := 'True' else Result := 'False';
end;

procedure InitializeWizard;
begin
  IdxRevit2022 := -1; IdxRevit2023 := -1; IdxRevit2024 := -1;
  IdxRevit2025 := -1; IdxRevit2026 := -1; IdxRevit2027 := -1;
  
  IdxCursor := -1; IdxWindsurf := -1; IdxClaude := -1; IdxCline := -1;
  IdxVSCode := -1; IdxCopilot := -1; IdxGeminiCli := -1; IdxClaudeCode := -1; IdxWarp := -1;
  IdxAntigravity := -1; IdxCodex := -1; IdxOpenClaw := -1; IdxHermes := -1; IdxOpenCode := -1;

  LogToFile('=== InitializeWizard START ===');
  LogToFile('UserAppData: ' + ExpandConstant('{userappdata}'));
  LogToFile('UserProfile (derived): ' + GetUserProfilePath);
  LogToFile('IsClientInstalled(cursor): ' + MyBoolToStr(IsClientInstalled('cursor')));
  LogToFile('IsClientInstalled(windsurf): ' + MyBoolToStr(IsClientInstalled('windsurf')));
  LogToFile('IsClientInstalled(claude): ' + MyBoolToStr(IsClientInstalled('claude')));
  LogToFile('IsClientInstalled(cline): ' + MyBoolToStr(IsClientInstalled('cline')));
  LogToFile('IsClientInstalled(vscode): ' + MyBoolToStr(IsClientInstalled('vscode')));
  LogToFile('IsClientInstalled(copilot): ' + MyBoolToStr(IsClientInstalled('copilot')));
  LogToFile('IsClientInstalled(gemini_cli): ' + MyBoolToStr(IsClientInstalled('gemini_cli')));
  LogToFile('IsClientInstalled(claude_code): ' + MyBoolToStr(IsClientInstalled('claude_code')));
  LogToFile('IsClientInstalled(warp): ' + MyBoolToStr(IsClientInstalled('warp')));
  LogToFile('IsClientInstalled(antigravity): ' + MyBoolToStr(IsClientInstalled('antigravity')));
  LogToFile('IsClientInstalled(opencode): ' + MyBoolToStr(IsClientInstalled('opencode')));
  LogToFile('IsClientInstalled(openclaw): ' + MyBoolToStr(IsClientInstalled('openclaw')));
  LogToFile('IsClientInstalled(hermes): ' + MyBoolToStr(IsClientInstalled('hermes')));
  LogToFile('IsClientInstalled(codex): ' + MyBoolToStr(IsClientInstalled('codex')));
  LogToFile('AnyClientInstalled: ' + MyBoolToStr(AnyClientInstalled));

  // 1. Create Revit selection page
  RevitPage := CreateInputOptionPage(wpSelectDir,
    ExpandConstant('{cm:RevitTitle}'), ExpandConstant('{cm:RevitSubTitle}'),
    '',
    False, True);
    
  if not AnyRevitInstalled then
  begin
    IdxRevit2022 := RevitPage.Add(ExpandConstant('{cm:Revit2022}'));
    IdxRevit2023 := RevitPage.Add(ExpandConstant('{cm:Revit2023}'));
    IdxRevit2024 := RevitPage.Add(ExpandConstant('{cm:Revit2024}'));
    IdxRevit2025 := RevitPage.Add(ExpandConstant('{cm:Revit2025}'));
    IdxRevit2026 := RevitPage.Add(ExpandConstant('{cm:Revit2026}'));
    IdxRevit2027 := RevitPage.Add(ExpandConstant('{cm:Revit2027}'));
    
    RevitPage.Values[IdxRevit2022] := True;
    RevitPage.Values[IdxRevit2023] := True;
    RevitPage.Values[IdxRevit2024] := True;
    RevitPage.Values[IdxRevit2025] := True;
    RevitPage.Values[IdxRevit2026] := True;
    RevitPage.Values[IdxRevit2027] := True;
  end
  else
  begin
    if IsRevitInstalled('2022') then begin IdxRevit2022 := RevitPage.Add(ExpandConstant('{cm:Revit2022}')); RevitPage.Values[IdxRevit2022] := True; end;
    if IsRevitInstalled('2023') then begin IdxRevit2023 := RevitPage.Add(ExpandConstant('{cm:Revit2023}')); RevitPage.Values[IdxRevit2023] := True; end;
    if IsRevitInstalled('2024') then begin IdxRevit2024 := RevitPage.Add(ExpandConstant('{cm:Revit2024}')); RevitPage.Values[IdxRevit2024] := True; end;
    if IsRevitInstalled('2025') then begin IdxRevit2025 := RevitPage.Add(ExpandConstant('{cm:Revit2025}')); RevitPage.Values[IdxRevit2025] := True; end;
    if IsRevitInstalled('2026') then begin IdxRevit2026 := RevitPage.Add(ExpandConstant('{cm:Revit2026}')); RevitPage.Values[IdxRevit2026] := True; end;
    if IsRevitInstalled('2027') then begin IdxRevit2027 := RevitPage.Add(ExpandConstant('{cm:Revit2027}')); RevitPage.Values[IdxRevit2027] := True; end;
  end;

  // 2. Create Clients selection page
  ClientsPage := CreateInputOptionPage(RevitPage.ID,
    ExpandConstant('{cm:ClientsTitle}'), ExpandConstant('{cm:ClientsSubTitle}'),
    '',
    False, True);
    
  if not AnyClientInstalled then
  begin
    IdxCursor := ClientsPage.Add(ExpandConstant('{cm:ClientCursor}'));
    IdxWindsurf := ClientsPage.Add(ExpandConstant('{cm:ClientWindsurf}'));
    IdxClaude := ClientsPage.Add(ExpandConstant('{cm:ClientClaude}'));
    IdxCline := ClientsPage.Add(ExpandConstant('{cm:ClientCline}'));
    IdxVSCode := ClientsPage.Add(ExpandConstant('{cm:ClientVSCode}'));
    IdxCopilot := ClientsPage.Add(ExpandConstant('{cm:ClientCopilot}'));
    IdxGeminiCli := ClientsPage.Add(ExpandConstant('{cm:ClientGeminiCli}'));
    IdxClaudeCode := ClientsPage.Add(ExpandConstant('{cm:ClientClaudeCode}'));
    IdxWarp := ClientsPage.Add(ExpandConstant('{cm:ClientWarp}'));
    IdxAntigravity := ClientsPage.Add(ExpandConstant('{cm:ClientAntigravity}'));
    IdxCodex := ClientsPage.Add(ExpandConstant('{cm:ClientCodex}'));
    IdxOpenClaw := ClientsPage.Add(ExpandConstant('{cm:ClientOpenClaw}'));
    IdxHermes := ClientsPage.Add(ExpandConstant('{cm:ClientHermes}'));
    IdxOpenCode := ClientsPage.Add(ExpandConstant('{cm:ClientOpenCode}'));
    
    ClientsPage.Values[IdxCursor] := False;
    ClientsPage.Values[IdxWindsurf] := False;
    ClientsPage.Values[IdxClaude] := False;
    ClientsPage.Values[IdxCline] := False;
    ClientsPage.Values[IdxVSCode] := False;
    ClientsPage.Values[IdxCopilot] := False;
    ClientsPage.Values[IdxGeminiCli] := False;
    ClientsPage.Values[IdxClaudeCode] := False;
    ClientsPage.Values[IdxWarp] := False;
    ClientsPage.Values[IdxAntigravity] := False;
    ClientsPage.Values[IdxCodex] := False;
    ClientsPage.Values[IdxOpenClaw] := False;
    ClientsPage.Values[IdxHermes] := False;
    ClientsPage.Values[IdxOpenCode] := False;
  end
  else
  begin
    if IsClientInstalled('cursor') then begin IdxCursor := ClientsPage.Add(ExpandConstant('{cm:ClientCursor}')); ClientsPage.Values[IdxCursor] := True; end;
    if IsClientInstalled('windsurf') then begin IdxWindsurf := ClientsPage.Add(ExpandConstant('{cm:ClientWindsurf}')); ClientsPage.Values[IdxWindsurf] := True; end;
    if IsClientInstalled('claude') then begin IdxClaude := ClientsPage.Add(ExpandConstant('{cm:ClientClaude}')); ClientsPage.Values[IdxClaude] := True; end;
    if IsClientInstalled('cline') then begin IdxCline := ClientsPage.Add(ExpandConstant('{cm:ClientCline}')); ClientsPage.Values[IdxCline] := True; end;
    if IsClientInstalled('vscode') then begin IdxVSCode := ClientsPage.Add(ExpandConstant('{cm:ClientVSCode}')); ClientsPage.Values[IdxVSCode] := True; end;
    if IsClientInstalled('copilot') then begin IdxCopilot := ClientsPage.Add(ExpandConstant('{cm:ClientCopilot}')); ClientsPage.Values[IdxCopilot] := True; end;
    if IsClientInstalled('gemini_cli') then begin IdxGeminiCli := ClientsPage.Add(ExpandConstant('{cm:ClientGeminiCli}')); ClientsPage.Values[IdxGeminiCli] := True; end;
    if IsClientInstalled('claude_code') then begin IdxClaudeCode := ClientsPage.Add(ExpandConstant('{cm:ClientClaudeCode}')); ClientsPage.Values[IdxClaudeCode] := True; end;
    if IsClientInstalled('warp') then begin IdxWarp := ClientsPage.Add(ExpandConstant('{cm:ClientWarp}')); ClientsPage.Values[IdxWarp] := True; end;
    if IsClientInstalled('antigravity') then begin IdxAntigravity := ClientsPage.Add(ExpandConstant('{cm:ClientAntigravity}')); ClientsPage.Values[IdxAntigravity] := True; end;
    if IsClientInstalled('codex') then begin IdxCodex := ClientsPage.Add(ExpandConstant('{cm:ClientCodex}')); ClientsPage.Values[IdxCodex] := True; end;
    if IsClientInstalled('openclaw') then begin IdxOpenClaw := ClientsPage.Add(ExpandConstant('{cm:ClientOpenClaw}')); ClientsPage.Values[IdxOpenClaw] := True; end;
    if IsClientInstalled('hermes') then begin IdxHermes := ClientsPage.Add(ExpandConstant('{cm:ClientHermes}')); ClientsPage.Values[IdxHermes] := True; end;
    if IsClientInstalled('opencode') then begin IdxOpenCode := ClientsPage.Add(ExpandConstant('{cm:ClientOpenCode}')); ClientsPage.Values[IdxOpenCode] := True; end;
  end;
end;

function InstallRevit2022: Boolean; begin Result := (IdxRevit2022 <> -1) and RevitPage.Values[IdxRevit2022]; end;
function InstallRevit2023: Boolean; begin Result := (IdxRevit2023 <> -1) and RevitPage.Values[IdxRevit2023]; end;
function InstallRevit2024: Boolean; begin Result := (IdxRevit2024 <> -1) and RevitPage.Values[IdxRevit2024]; end;
function InstallRevit2025: Boolean; begin Result := (IdxRevit2025 <> -1) and RevitPage.Values[IdxRevit2025]; end;
function InstallRevit2026: Boolean; begin Result := (IdxRevit2026 <> -1) and RevitPage.Values[IdxRevit2026]; end;
function InstallRevit2027: Boolean; begin Result := (IdxRevit2027 <> -1) and RevitPage.Values[IdxRevit2027]; end;

function GetSelectedClients(Param: String): String;
var
  Clients: String;
begin
  Clients := '';
  if (IdxCursor <> -1) and ClientsPage.Values[IdxCursor] then Clients := Clients + 'cursor ';
  if (IdxWindsurf <> -1) and ClientsPage.Values[IdxWindsurf] then Clients := Clients + 'windsurf ';
  if (IdxClaude <> -1) and ClientsPage.Values[IdxClaude] then Clients := Clients + 'claude ';
  if (IdxCline <> -1) and ClientsPage.Values[IdxCline] then Clients := Clients + 'cline ';
  if (IdxVSCode <> -1) and ClientsPage.Values[IdxVSCode] then Clients := Clients + 'vscode ';
  if (IdxCopilot <> -1) and ClientsPage.Values[IdxCopilot] then Clients := Clients + 'copilot ';
  if (IdxGeminiCli <> -1) and ClientsPage.Values[IdxGeminiCli] then Clients := Clients + 'gemini_cli ';
  if (IdxClaudeCode <> -1) and ClientsPage.Values[IdxClaudeCode] then Clients := Clients + 'claude_code ';
  if (IdxWarp <> -1) and ClientsPage.Values[IdxWarp] then Clients := Clients + 'warp ';
  if (IdxAntigravity <> -1) and ClientsPage.Values[IdxAntigravity] then Clients := Clients + 'antigravity ';
  if (IdxCodex <> -1) and ClientsPage.Values[IdxCodex] then Clients := Clients + 'codex ';
  if (IdxOpenClaw <> -1) and ClientsPage.Values[IdxOpenClaw] then Clients := Clients + 'openclaw ';
  if (IdxHermes <> -1) and ClientsPage.Values[IdxHermes] then Clients := Clients + 'hermes ';
  if (IdxOpenCode <> -1) and ClientsPage.Values[IdxOpenCode] then Clients := Clients + 'opencode ';
  Result := Trim(Clients);
  LogToFile('GetSelectedClients result: ' + Result);
  LogToFile('Full command: --config-only --user-appdata "' + ExpandConstant('{userappdata}') + '" --user-profile "' + GetUserProfilePath + '" --clients ' + Result);
end;
