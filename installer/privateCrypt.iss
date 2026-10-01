; privateCrypt Inno Setup Script
; Requires Inno Setup 6.x  (https://jrsoftware.org/isinfo.php)
;
; Build:  Open this .iss file in the Inno Setup IDE and press Compile (F9)
;         or run:  iscc privateCrypt.iss
;
; The compiled setup will be placed in:  ..\dist\privateCrypt_Setup.exe

#define AppName      "privateCrypt"
#define AppVersion   "3.0"
#define AppPublisher "mundi86"
#define AppExeName   "privateCrypt.exe"

[Setup]
AppName={#AppName}
; Feste Identität über alle Versionen hinweg. Ohne AppId würde Inno die
; Identität aus Name+Version ableiten und bei jedem Versionswechsel einen
; zweiten Eintrag in "Apps & Features" anlegen statt ein Upgrade zu machen.
; Wer bereits 2.0 installiert hat, muss diese einmal deinstallieren.
AppId={{8BC17DCE-B1E3-4681-81D2-863B45CB0BF3}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL=https://github.com/mundi86/File-crypt-CSharp
AppSupportURL=https://github.com/mundi86/File-crypt-CSharp/issues

; Per-user install — no UAC / admin required.
; This matches app.manifest, which requests execution level "asInvoker":
; privateCrypt only ever writes to HKCU and %LocalAppData%.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=commandline

DefaultDirName={localappdata}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes

; Output
OutputDir=..\dist
OutputBaseFilename=privateCrypt_Setup
SetupIconFile=..\File_crypt\File_crypt\1374605872_86255.ico

; Compression
Compression=lzma2/ultra64
SolidCompression=yes

; Uninstall display settings
UninstallDisplayName={#AppName} {#AppVersion}
; Anfuehrungszeichen noetig: der Installationspfad kann Leerzeichen enthalten.
UninstallDisplayIcon="{app}\{#AppExeName}"
WizardStyle=modern

; .NET Framework 4.8 check (always present on Windows 10/11)
; No MinVersion needed — .NET 4.8 ships with Win 10 1903+

[Languages]
Name: "german";   MessagesFile: "compiler:Languages\German.isl"
Name: "english";  MessagesFile: "compiler:Default.isl"

[Files]
Source: "..\File_crypt\File_crypt\bin\Release\{#AppExeName}"; \
        DestDir: "{app}"; \
        Flags: ignoreversion

[Icons]
; Start Menu
Name: "{autoprograms}\{#AppName}"; \
      Filename: "{app}\{#AppExeName}"; \
      Comment: "Dateien per Rechtsklick ver- und entschlüsseln (AES-256)"

[Registry]
; -----------------------------------------------------------------------
; Context menu: single files  — "Ver- | Entschlüsseln (AES256)"
; -----------------------------------------------------------------------
Root: HKCU; \
      Subkey: "Software\Classes\*\shell\Ver- | Entschlüsseln (AES256)"; \
      ValueType: string; ValueName: "icon"; \
      ValueData: """{app}\{#AppExeName}"",0"; \
      Flags: uninsdeletekey

Root: HKCU; \
      Subkey: "Software\Classes\*\shell\Ver- | Entschlüsseln (AES256)\command"; \
      ValueType: string; ValueName: ""; \
      ValueData: """{app}\{#AppExeName}"" ""%1"""

; -----------------------------------------------------------------------
; Context menu: folders — Verschlüsseln
; -----------------------------------------------------------------------
Root: HKCU; \
      Subkey: "Software\Classes\Directory\shell\Verschlüsseln (AES256)"; \
      ValueType: string; ValueName: "icon"; \
      ValueData: """{app}\{#AppExeName}"",0"; \
      Flags: uninsdeletekey

Root: HKCU; \
      Subkey: "Software\Classes\Directory\shell\Verschlüsseln (AES256)"; \
      ValueType: string; ValueName: "Position"; \
      ValueData: "Bottom"

Root: HKCU; \
      Subkey: "Software\Classes\Directory\shell\Verschlüsseln (AES256)\command"; \
      ValueType: string; ValueName: ""; \
      ValueData: """{app}\{#AppExeName}"" ""%1"" ""e"""

; -----------------------------------------------------------------------
; Context menu: folders — Entschlüsseln
; -----------------------------------------------------------------------
Root: HKCU; \
      Subkey: "Software\Classes\Directory\shell\Entschlüsseln (AES256)"; \
      ValueType: string; ValueName: "icon"; \
      ValueData: """{app}\{#AppExeName}"",0"; \
      Flags: uninsdeletekey

Root: HKCU; \
      Subkey: "Software\Classes\Directory\shell\Entschlüsseln (AES256)"; \
      ValueType: string; ValueName: "Position"; \
      ValueData: "Bottom"

Root: HKCU; \
      Subkey: "Software\Classes\Directory\shell\Entschlüsseln (AES256)\command"; \
      ValueType: string; ValueName: ""; \
      ValueData: """{app}\{#AppExeName}"" ""%1"" ""d"""

; -----------------------------------------------------------------------
; .protected file association — icon + Win11 top-level context menu
; ProgID-based association: verb appears directly in Win11 top context menu
; -----------------------------------------------------------------------
Root: HKCU; \
      Subkey: "Software\Classes\.protected"; \
      ValueType: string; ValueName: ""; \
      ValueData: "privateCrypt.protected"; \
      Flags: uninsdeletekey

Root: HKCU; \
      Subkey: "Software\Classes\privateCrypt.protected"; \
      ValueType: string; ValueName: ""; \
      ValueData: "Verschlüsselte Datei (AES256)"; \
      Flags: uninsdeletekey

Root: HKCU; \
      Subkey: "Software\Classes\privateCrypt.protected\DefaultIcon"; \
      ValueType: string; ValueName: ""; \
      ValueData: """{app}\{#AppExeName}"",0"

Root: HKCU; \
      Subkey: "Software\Classes\privateCrypt.protected\shell\open"; \
      ValueType: string; ValueName: ""; \
      ValueData: "🔓 Entschlüsseln (AES256)"

Root: HKCU; \
      Subkey: "Software\Classes\privateCrypt.protected\shell\open\command"; \
      ValueType: string; ValueName: ""; \
      ValueData: """{app}\{#AppExeName}"" ""%1"""

[UninstallDelete]
; Remove the entire install folder on uninstall
Type: filesandordirs; Name: "{app}"
