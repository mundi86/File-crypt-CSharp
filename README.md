# 🔐 privateCrypt

Ein schlankes Windows-Tool zum Ver- und Entschlüsseln von Dateien und Ordnern direkt per **Rechtsklick** im Windows Explorer.

---

## ✨ Features

- 🖱️ Windows-Kontextmenü-Integration (Rechtsklick auf Datei oder Ordner)
- 🔒 Verschlüsselt einzelne Dateien oder ganze Ordner rekursiv
- 🔑 AES-256-CBC mit PBKDF2-SHA256 (100.000 Iterationen)
- 👁️ Quick-Edit: Datei temporär entschlüsseln, anzeigen und automatisch wieder löschen
- 📦 Kein Setup nötig — läuft auf jedem Windows 10/11 (erfordert .NET Framework 4.8, immer vorinstalliert)
- 🔄 Rückwärtskompatibel mit Dateien die mit der alten Version verschlüsselt wurden

---

## 🚀 Installation

1. `privateCrypt.exe` als Administrator starten:
   ```
   privateCrypt.exe /install
   ```
2. Fertig — das Tool kopiert sich nach `%AppData%` und registriert die Kontextmenü-Einträge in der Windows Registry.

---

## 📋 Verwendung

### Datei verschlüsseln
Rechtsklick auf eine Datei → **Ver- | Entschlüsseln (AES256)** → Passwort eingeben → Enter

Die Originaldatei wird durch `dateiname.erweiterung.protected` ersetzt.

### Datei entschlüsseln
Rechtsklick auf eine `.protected` Datei → **Ver- | Entschlüsseln (AES256)** → Passwort eingeben → Enter

### Ordner verschlüsseln / entschlüsseln
Rechtsklick auf einen Ordner → **Verschlüsseln (AES256)** oder **Entschlüsseln (AES256)**

Alle Dateien im Ordner (rekursiv, außer `.db` Dateien) werden verarbeitet.

### Quick-Edit
Bei `.protected` Dateien ist die **Quick-Edit** Checkbox aktiviert: Die Datei wird temporär nach `%TEMP%` entschlüsselt, mit dem Standard-Programm geöffnet, und beim Schließen automatisch wieder gelöscht.

---

## 🔧 Technische Details

| Eigenschaft | Wert |
|---|---|
| Algorithmus | AES-256-CBC |
| Schlüsselableitung | PBKDF2-SHA256, 100.000 Iterationen |
| Salt | 32 Byte (zufällig) |
| IV | 16 Byte (zufällig) |
| Dateiformat | `PCv2` Magic + Salt + IV + Ciphertext |
| Framework | .NET Framework 4.8 |
| Platform | x86 (32-bit) |

### Dateiformat v2 (aktuell)
```
[PCv2 4 Bytes] [Salt 32 Bytes] [IV 16 Bytes] [Ciphertext]
```

### Dateiformat v1 (Legacy, nur Lesen)
Dateien die mit der alten Version erstellt wurden werden automatisch erkannt und können weiterhin entschlüsselt werden.

---

## ⚠️ Hinweise

- Das Passwort muss mindestens 4 Zeichen lang sein
- Die Originaldatei wird nach der Verschlüsselung gelöscht — **kein Backup!**
- Installation erfordert **Administrator-Rechte** (für Registry-Zugriff auf `HKCR`)
- `.db` Dateien werden beim Ordner-Modus übersprungen

---

## 🏗️ Build

Voraussetzungen: Visual Studio 2019+ oder MSBuild mit .NET Framework 4.8 SDK

```
msbuild File_crypt/File_crypt.sln /p:Configuration=Release /p:Platform=x86
```

Output: `File_crypt/File_crypt/bin/Release/privateCrypt.exe`
