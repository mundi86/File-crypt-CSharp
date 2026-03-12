# 🔐 privateCrypt

Ein schlankes Windows-Tool zum Ver- und Entschlüsseln von Dateien und Ordnern direkt per **Rechtsklick** im Windows Explorer.

---

## ✨ Features

- 🖱️ Windows-Kontextmenü-Integration (Rechtsklick auf Datei oder Ordner)
- 🔒 Verschlüsselt einzelne Dateien oder ganze Ordner rekursiv
- 🔑 AES-256-CBC mit PBKDF2-SHA256 (100.000 Iterationen)
- 🛡️ Sicheres Löschen — Originaldaten werden vor dem Löschen überschrieben
- 👁️ Quick-Edit: Datei temporär entschlüsseln, anzeigen und automatisch wieder löschen
- 📦 Kein Admin nötig — per-User Installation ohne UAC
- 🔄 Rückwärtskompatibel mit Dateien die mit der alten Version verschlüsselt wurden
- ➕ Saubere Deinstallation über Windows „Apps & Features"
- 🔓 `.protected` Dateien zeigen Schloss-Icon im Explorer + „Entschlüsseln" direkt im Win11 Top-Menü
- 🌗 Modernes Passwort-Fenster — passt sich automatisch dem Hell/Dunkel-Theme an (Win11 runde Ecken)

---

## 🏗️ Schritt 1: Bauen (Build)

**Voraussetzungen:**
- Visual Studio 2019+ (Community reicht) mit Workload „.NET-Desktopentwicklung"
- oder MSBuild mit .NET Framework 4.8 SDK

**In Visual Studio:**
1. `File_crypt/File_crypt.sln` öffnen
2. Konfiguration oben auf **Release** und **x86** stellen
3. **Build → Projektmappe erstellen** (Strg+Umschalt+B)

**Oder per Kommandozeile:**
```
msbuild File_crypt/File_crypt.sln /p:Configuration=Release /p:Platform=x86
```

Output: `File_crypt/File_crypt/bin/Release/privateCrypt.exe`

---

## 📦 Schritt 2: Installer bauen (optional aber empfohlen)

**Voraussetzungen:**
- [Inno Setup 6](https://jrsoftware.org/isdl.php) installieren (kostenlos, ~5 MB)

**Vorgehen:**
1. Inno Setup öffnen
2. `installer/privateCrypt.iss` öffnen
3. **Compile** drücken (F9)

Output: `dist/privateCrypt_Setup.exe`

> Alternativ zum Installer: die EXE direkt per `/install` Flag installieren (siehe unten).

---

## 🚀 Schritt 3: Installieren

### Option A — Installer (empfohlen) ✅
```
dist\privateCrypt_Setup.exe
```
- Klick durch den Wizard
- **Kein Admin / kein UAC** nötig
- Erscheint danach in **Windows Apps & Features**
- Kontextmenü sofort aktiv

### Option B — Direktinstallation (ohne Installer)
```
File_crypt\File_crypt\bin\Release\privateCrypt.exe /install
```
- Kopiert die EXE nach `%LocalAppData%\privateCrypt\`
- Registriert Kontextmenü und Uninstall-Eintrag automatisch

---

## 📋 Schritt 4: Testen

### ✅ Datei verschlüsseln
1. Beliebige Testdatei anlegen (z.B. `test.txt` mit einem Text)
2. Rechtsklick auf die Datei → **Ver- | Entschlüsseln (AES256)**
3. Passwort eingeben (mind. 4 Zeichen) → Enter oder Button
4. Ergebnis: `test.txt` ist weg, `test.txt.protected` ist da

### ✅ Datei entschlüsseln
1. Rechtsklick auf `test.txt.protected` → **🔓 Entschlüsseln (AES256)** (direkt im Win11 Top-Menü)
2. Gleiches Passwort eingeben → Enter
3. Ergebnis: `test.txt` wieder da, `.protected` Datei weg

### ✅ Quick-Edit testen
1. Eine Bilddatei (`.jpg`) verschlüsseln → `bild.jpg.protected`
2. Rechtsklick auf `.protected` → Passwort eingeben
3. Die **Quick-Edit Checkbox ist automatisch aktiviert** → Enter
4. Bild öffnet sich im Viewer
5. Viewer schließen → App beendet sich, Temp-Datei gelöscht

### ✅ Ordner verschlüsseln
1. Ordner mit mehreren Dateien anlegen
2. Rechtsklick auf Ordner → **Verschlüsseln (AES256)**
3. Passwort eingeben → alle Dateien werden `.protected`
4. Rechtsklick auf Ordner → **Entschlüsseln (AES256)** → zurück

### ✅ Apps & Features prüfen
- Windows-Taste → „Apps" → nach „privateCrypt" suchen
- Eintrag mit Version 2.0 sollte erscheinen

---

## 🗑️ Deinstallieren

### Über Windows Apps & Features (empfohlen)
Windows-Taste → Apps → „privateCrypt" suchen → Deinstallieren

### Über Kommandozeile
```
%LocalAppData%\privateCrypt\privateCrypt.exe /uninstall
```

Entfernt: Kontextmenü-Einträge, installierte EXE, Uninstall-Eintrag

---

## 📋 Verwendung im Detail

### Datei verschlüsseln
Rechtsklick auf eine Datei → **Ver- | Entschlüsseln (AES256)** → Passwort eingeben → Enter

Die Originaldatei wird durch `dateiname.erweiterung.protected` ersetzt.

### Datei entschlüsseln
Rechtsklick auf eine `.protected` Datei → **🔓 Entschlüsseln (AES256)** (direkt im Win11 Top-Menü) → Passwort eingeben → Enter

> **Hinweis Win11-Kontextmenü:**
> - `.protected` Dateien: „Entschlüsseln" erscheint **direkt im Top-Level-Menü** (via Dateiverknüpfung)
> - Normale Dateien / Ordner: „Ver- | Entschlüsseln" bzw. „Verschlüsseln / Entschlüsseln" → unter **„Weitere Optionen anzeigen"** — das ist Win11's Design, nicht änderbar ohne COM-DLL

### Ordner verschlüsseln / entschlüsseln
Rechtsklick auf einen Ordner → **Verschlüsseln (AES256)** oder **Entschlüsseln (AES256)**

Alle Dateien im Ordner (rekursiv, außer `.db` Dateien) werden verarbeitet.

### Quick-Edit
Bei `.protected` Dateien ist die **Quick-Edit Checkbox** aktiviert: Die Datei wird temporär nach `%TEMP%` entschlüsselt, mit dem Standard-Programm geöffnet, und beim Schließen automatisch sicher gelöscht.

---

## 🔧 Technische Details

| Eigenschaft | Wert |
|---|---|
| Algorithmus | AES-256-CBC |
| Schlüsselableitung | PBKDF2-SHA256, 100.000 Iterationen |
| Salt | 32 Byte (zufällig) |
| IV | 16 Byte (zufällig) |
| Dateiformat | `PCv2` Magic + Salt + IV + Ciphertext |
| Datei-Löschung | Überschreiben mit Nullbytes vor Delete |
| Framework | .NET Framework 4.8 (vorinstalliert auf Win 10/11) |
| Platform | x86 (32-bit) |
| Installation | Per-User, kein Admin nötig |
| UI-Theme | Automatisch Hell/Dunkel (Windows-System-Theme) |
| Win11-Styling | DWM: dunkle Titelleiste + runde Ecken |
| Datei-Icon | `.protected` Dateien zeigen Schloss-Icon im Explorer |
| Win11-Menü | `.protected` Verb direkt im Top-Level (via ProgID) |

### Dateiformat v2 (aktuell)
```
[PCv2 4 Bytes] [Salt 32 Bytes] [IV 16 Bytes] [Ciphertext]
```

### Dateiformat v1 (Legacy, nur Lesen)
Dateien die mit der alten Version erstellt wurden werden automatisch erkannt und können weiterhin entschlüsselt werden.

---

## ⚠️ Hinweise

- Das Passwort muss mindestens 4 Zeichen lang sein
- Die Originaldatei wird nach der Verschlüsselung sicher überschrieben und gelöscht — **kein Backup!**
- Auf SSDs mit Wear-Leveling ist physisch vollständiges Löschen nicht garantiert — schützt aber vor Standard-Recovery-Tools
- `.db` Dateien werden beim Ordner-Modus übersprungen
