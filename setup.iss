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
DefaultDirName={userappdata}\revit_mcp_plugin
DisableDirPage=yes
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=.
OutputBaseFilename=RevitMCPSetup
Compression=lzma
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Files]
; Copy C# Addin files to Revit 2027 directory (User AppData)
Source: "plugin\bin\AddIn 2027 Debug R27\*"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2027"; Flags: ignoreversion recursesubdirs createallsubdirs

; Copy MCP Server files
Source: "server\build\*"; DestDir: "{app}\server\build"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "server\node_modules\*"; DestDir: "{app}\server\node_modules"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "server\package.json"; DestDir: "{app}\server"; Flags: ignoreversion

; Copy install.exe helper to configure IDE settings
Source: "install.exe"; DestDir: "{app}"; Flags: ignoreversion

[Run]
; Run the configuration helper silently at the end of the installation to modify IDE JSONs
Filename: "{app}\install.exe"; StatusMsg: "Configuring AI clients (Cursor, Claude, Antigravity IDE)..."; Flags: runhidden
