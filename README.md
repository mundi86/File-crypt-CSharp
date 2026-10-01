# 🔐 privateCrypt

![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)
![.NET Framework 4.8](https://img.shields.io/badge/.NET%20Framework-4.8-purple)
![Platform: Windows](https://img.shields.io/badge/Platform-Windows%2010%2F11-0078D4)

Ein schlankes Windows-Tool zum Ver- und Entschlüsseln von Dateien und Ordnern direkt per **Rechtsklick** im Windows Explorer.

---

## ✨ Features

- 🖱️ Windows-Kontextmenü-Integration (Rechtsklick auf Datei oder Ordner)
- 🔒 Verschlüsselt einzelne Dateien oder ganze Ordner rekursiv
- 🔑 AES-256-CBC **plus HMAC-SHA256** (Integritätsschutz) mit PBKDF2-SHA256 (300.000 Iterationen)
- 🛡️ Sicheres Löschen — Originaldaten werden vor dem Löschen überschrieben
- 🚫 Bestehende Dateien werden nie ungefragt überschrieben
- 📊 Ordner-Verschlüsselung mit Fortschrittsbalken und **Abbrechen**-Schaltfläche
- 💾 Geringer Speicherbedarf — auch sehr große Dateien sind kein Problem
- 👁️ Quick-Edit: Datei temporär entschlüsseln, anzeigen und automatisch wieder löschen
- 📦 Kein Admin nötig — per-User Installation ohne UAC
- 🔄 Rückwärtskompatibel mit Dateien aus den Versionen 2.0 und 1.0
- ⬆️ Ordner-Entschlüsseln bringt Altdateien automatisch auf das aktuelle Format
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

## 🧪 Schritt 1b: Tests laufen lassen

Die Krypto-Schicht hat eine Testsuite ohne externe Abhängigkeiten (kein NuGet-Restore nötig).
**Bitte vor jeder Änderung an `PolyAES.cs` oder `FileOps.cs` ausführen.**

```
msbuild File_crypt/File_crypt.sln /p:Configuration=Release /p:Platform=x86
File_crypt\Tests\PolyAES.Tests\bin\Release\PolyAES.Tests.exe
```

Exit-Code `0` = alles grün. 119 Prüfungen: Round-Trips über Block- und Puffergrenzen,
Manipulationserkennung (Bitfehler in Ciphertext/Salt/IV/Signatur, vertauschte Blöcke,
Kürzen, Anhängen), Rückwärtskompatibilität mit v2 und v1 sowie atomare Schreibvorgänge.

Zusätzlich prüft ein End-to-End-Lauf die **echte EXE im Fenster** (25 Prüfungen):

```
.\File_crypt\Tests\e2e\Invoke-PrivateCryptTests.ps1
```

**Ausführliche Übersicht, was abgedeckt ist und was nicht:**
[docs/TESTING.md](docs/TESTING.md)

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
3. Passwort eingeben (mind. 8 Zeichen) → Enter oder Button
4. Ergebnis: `test.txt` ist weg, `test.txt.protected` ist da

### ✅ Datei entschlüsseln
1. Rechtsklick auf `test.txt.protected` → **🔓 Entschlüsseln (AES256)** (direkt im Win11 Top-Menü)
2. Gleiches Passwort eingeben → Enter
3. Ergebnis: `test.txt` wieder da, `.protected` Datei weg

### ✅ Integritätsschutz prüfen
1. `test.txt.protected` mit einem Hex-Editor öffnen
2. Ein beliebiges Byte im Ciphertext-Bereich umstellen und speichern
3. Versuch zu entschlüsseln → **„Entschlüsselung fehlgeschlagen"**, es entsteht keine Klartextdatei

### ✅ Quick-Edit testen
1. Eine Bilddatei (`.jpg`) verschlüsseln → `bild.jpg.protected`
2. Rechtsklick auf `.protected` → Passwort eingeben
3. Die **Quick-Edit Checkbox ist automatisch aktiviert** → Enter
4. Bild öffnet sich im Viewer
5. Viewer schließen → App beendet sich, Temp-Datei wird gelöscht

### ✅ Ordner verschlüsseln
1. Ordner mit mehreren Dateien anlegen
2. Rechtsklick auf Ordner → **Verschlüsseln (AES256)**
3. Passwort eingeben → Fortschrittsbalken läuft, alle Dateien werden `.protected`
4. Rechtsklick auf Ordner → **Entschlüsseln (AES256)** → zurück

### ✅ Überschreiben-Schutz prüfen
1. `test.txt` und `test.txt.protected` nebeneinander anlegen
2. Ordner entschlüsseln → `test.txt` bleibt **unangetastet**, `test.txt` wird in der
   Zusammenfassung als übersprungen aufgelistet

### ✅ Apps & Features prüfen
- Windows-Taste → „Apps" → nach „privateCrypt" suchen
- Eintrag mit Version 3.0 sollte erscheinen

---

## ❓ Nachfragen

### Das Kontextmenü zeigt noch die alte Version

Das Kontextmenü zeigt **immer** auf `%LocalAppData%\privateCrypt\privateCrypt.exe`.
Wenn dort noch eine ältere Version liegt, testest du die alte — und zwar
unabhängig davon, welche EXE du gebaut hast. Erkennbar an der
Beschriftung über dem Passwortfeld:

| | Version 2.0 | Version 3.0 |
|---|---|---|
| Beschriftung | `crypt with -> PolyAES256` | `Verschlüsseln → AES-256-CBC + HMAC-SHA256` |
| Fenster | schmal | breiter |
| Container | `PCv2` | `PCv3` |

**Abhilfe:** einmalig die neue Version installieren (siehe Schritt 3), oder
die Tests direkt über die Kommandozeile starten und das Kontextmenü meiden:

```
privateCrypt.exe "C:\Pfad\datei.txt"
privateCrypt.exe "C:\Pfad\ordner" e
```

### „Vorgang abgebrochen" erscheint ungefragt

**Das ist das erwartete Verhalten, wenn du das Fenster schließt, während
„Schlüssel wird abgeleitet …" angezeigt wird.** Die Ableitung dauert rund
1,5 Sekunden und lässt sich sonst nicht abbrechen — bei einem Ordner mit
sehr vielen Dateien wäre sie sonst nicht mehr zu stoppen.

Einfach warten, bis sich das Fenster von selbst schließt. Es wurde schon
verifiziert, dass die Meldung auch dann **nicht** erscheint: über einen
Korrekturlauf durch alle Testfälle hinweg gab es keinen einzigen Abbruch
und keine Ausnahme. Details in [docs/TESTING.md](docs/TESTING.md).

### Was wird beim Entschlüsseln übersprungen

Existiert die Zieldatei bereits, wird sie **nicht** ersetzt. Die Datei
landet in der Zusammenfassung:

```
Verarbeitet: 4
Übersprungen: 1

Übersprungen:
  bericht.txt (Ziel existiert bereits)
```

Das ist Absicht — in Version 2.0 wurde an dieser Stelle still der
Klartext überschrieben.

### Verschlüsselte Datei lässt sich nicht öffnen

Prüfe zuerst, ob die Datei wirklich zu privateCrypt gehört. Eine
Textdatei, die man umbenannt hat, oder eine Datei, die mit einem anderen
Programm erzeugt wurde, ergibt:

> Entschlüsselung fehlgeschlagen: Passwort falsch oder Datei beschädigt bzw. verändert.

Diese Meldung ist absichtlich **zweideutig**. Es gibt auch dann keine
Klartextdatei — sie zu unterscheiden würde einem Angreifer verraten, ob
eine Manipulation vorliegt.

---

## 🗑️ Deinstallieren

### Über Windows Apps & Features (empfohlen)
Windows-Taste → Apps → „privateCrypt" suchen → Deinstallieren

### Über Kommandozeile
```
%LocalAppData%\privateCrypt\privateCrypt.exe /uninstall
```

Entfernt: Kontextmenü-Einträge, Dateizuordnung, installierte EXE, Uninstall-Eintrag

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

Alle Dateien im Ordner (rekursiv, außer `.db` Dateien) werden verarbeitet. Verzeichnisverknüpfungen (Junctions) werden übersprungen, damit der Lauf nicht in Zyklen gerät. Während der Verarbeitung lässt sich der Vorgang jederzeit **abbrechen**.

#### Altdateien werden dabei automatisch aktualisiert

Dateien im alten Format (v2 oder Original von 2011) werden beim
Entschlüsseln **sofort wieder verschlüsselt** — diesmal im aktuellen
Format mit HMAC-Signatur. Am Ende steht der ganze Ordner auf v3, ohne
dass du etwas von Hand nachverschlüsseln musst.

```
Verarbeitet: 12
Auf AES-256 + HMAC (PCv3) aktualisiert: 4 (3 aus Version 2.0, 1 aus dem Original von 2011)
```

Das ist der Zweck: v2- und v1-Dateien haben **keinen Integritätsschutz**.
Nach dem Upgrade sind sie manipulierbar geschützt.

**Wichtig:** Die Dateien bleiben dabei **verschlüsselt** — es wird kein
Klartext dauerhaft abgelegt. Nur wenn du danach eine einzelne Datei
entschlüsselst, erscheint ihr Inhalt.

Der Ordner-Modus ist der einzige, der automatisch aktualisiert. Beim
Entschlüsseln einer **Einzeldatei** bleibt sie im alten Format; wer sie
aufrüsten will, entschlüsselt und verschlüsselt sie einfach erneut.

### Quick-Edit
Bei `.protected` Dateien ist die **Quick-Edit Checkbox** aktiviert: Die Datei wird temporär nach `%TEMP%\privateCrypt-quickedit` entschlüsselt, mit dem Standard-Programm geöffnet, und beim Schließen automatisch sicher gelöscht. Reste eines zuvor abgestürzten Programms werden beim nächsten Start entfernt.

### Kommandozeile
```
privateCrypt.exe "<Datei oder Ordner>" [e|d]
```
`e` = verschlüsseln (Standard), `d` = entschlüsseln. Wird normalerweise nicht direkt gebraucht, da das Kontextmenü die Aufgabe übernimmt.

---

## 🔧 Technische Details

| Eigenschaft | Wert |
|---|---|
| Algorithmus | AES-256-CBC + HMAC-SHA256 (Encrypt-then-MAC) |
| Schlüsselableitung | PBKDF2-SHA256, 300.000 Iterationen, einmal pro Sitzung |
| Dateischlüssel | pro Datei aus dem Masterschlüssel per HMAC abgeleitet |
| Salt / IV | 32 / 16 Byte, pro Datei zufällig |
| Dateiformat | `PCv3` Magic + Salt + IV + Ciphertext + HMAC (32 Byte) |
| Speicherbedarf | konstant, unabhängig von der Dateigröße (streaming) |
| Schreibweise | atomar über temporäre Datei, nie halb geschrieben |
| Datei-Löschung | Überschreiben mit Nullbytes vor Delete |
| Mindest-Passwort | 8 Zeichen beim Verschlüsseln, 4 beim Entschüsseln |
| Framework | .NET Framework 4.8 (vorinstalliert auf Win 10/11) |
| Platform | x86 (32-bit) |
| Installation | Per-User, kein Admin nötig |
| UI-Theme | Automatisch Hell/Dunkel (Windows-System-Theme) |
| Win11-Styling | DWM: dunkle Titelleiste + runde Ecken |
| Datei-Icon | `.protected` Dateien zeigen Schloss-Icon im Explorer |
| Win11-Menü | `.protected` Verb direkt im Top-Level (via ProgID) |

### Dateiformat v3 (aktuell)
```
[PCv3 4 B] [Salt 32 B] [IV 16 B] [Ciphertext ...] [HMAC-SHA256 32 B]
```
Die Signatur wird **geprüft, bevor** überhaupt Klartext entsteht. Ein falsches Passwort und
eine manipulierte Datei führen bewusst zur selben Meldung.

### Dateiformat v2 (nur noch Lesen)
Dateien aus Version 2.0 (`[PCv2][Salt][IV][Ciphertext]`) werden automatisch erkannt und
können weiterhin entschlüsselt werden. Sie haben **keinen** Integritätsschutz — am besten
einmal entschlüsseln und neu verschlüsseln.

### Dateiformat v1 (Legacy, nur Lesen)
Dateien aus der 1.0-Version von 2011 werden automatisch erkannt und können weiterhin
entschlüsselt werden. Auch hier fehlt der Integritätsschutz.

---

## ⚠️ Hinweise

- Das Passwort muss beim **Verschlüsseln** mindestens 8 Zeichen lang sein (beim Entschlüsseln 4, damit ältere Dateien zugänglich bleiben). Eine lange Passphrase ist wesentlich wirksamer als jede Einstellung hier.
- Die Originaldatei wird nach der Verschlüsselung sicher überschrieben und gelöscht — **kein Backup!**
- Existiert die Zieldatei bereits, wird sie **nicht** überschrieben: die Datei wird übersprungen und in der Zusammenfassung aufgelistet.
- Der Ordner-Durchlauf lässt sich jederzeit abbrechen. Bereits fertiggestellte Dateien bleiben verarbeitet, die übrigen sind unverändert.
- Auf SSDs mit Wear-Leveling ist physisch vollständiges Löschen nicht garantiert — schützt aber vor Standard-Recovery-Tools
- `.db` Dateien werden beim Ordner-Modus übersprungen

---

## 📂 Projektstruktur

```
File_crypt/
├── File_crypt/                  Hauptprogramm
│   ├── PolyAES.cs               Verschlüsselung (v3/v2/v1), streaming
│   ├── FileOps.cs               Dateisystem-Helfer (Endungen, Sammeln, SecureDelete)
│   ├── cConfig.cs               Kontextmenü + Dateizuordnung (HKCU)
│   ├── Form1.cs / .Designer.cs  Oberfläche
│   ├── Program.cs               Einstiegspunkt
│   └── app.manifest             DPI, Common Controls v6, asInvoker
└── Tests/
    ├── PolyAES.Tests/           Testsuite ohne externe Abhängigkeiten
    │   ├── RoundTripTests.cs
    │   ├── TamperTests.cs
    │   ├── FileOpsTests.cs
    │   ├── LegacyVectorTests.cs
    │   └── legacy-vectors/      Fixed Vektoren aus dem 2011-Algorithmus
    └── e2e/
        └── Invoke-PrivateCryptTests.ps1   End-to-End gegen die echte EXE
installer/privateCrypt.iss       Inno Setup Installer
docs/TESTING.md                 Teststrategie, was abgedeckt ist und was nicht
```

Weitere Details zur Kryptografie stehen in [SECURITY.md](SECURITY.md), die
Testabdeckung und ihre Lücken in [docs/TESTING.md](docs/TESTING.md), die
Änderungshistorie in [CHANGELOG.md](CHANGELOG.md).

---

## 📌 Versionsangaben an drei Stellen

Die Version steht in **drei** Dateien und muss überall gleich sein:

| Datei | Ort |
|---|---|
| `File_crypt/File_crypt/Properties/AssemblyInfo.cs` | `AssemblyVersion` |
| `File_crypt/File_crypt/cConfig.cs` | `AppVersion` (Uninstall-Eintrag) |
| `installer/privateCrypt.iss` | `AppVersion` |

Das ist nicht nur Kosmetik: wer nur in die Dateieigenschaften der EXE
schaut, erkennt sonst nicht, welche Version läuft. Genau das ist beim
Testen von 3.0 passiert — die Registry und der Installer meldeten schon
3.0, die EXE aber noch 2.0.0.0, und es wurde versehentlich die alte
Version getestet.

**Kurz prüfen, welche Version wirklich läuft:**

```
Programm\privateCrypt.exe --version
```
(bzw. Dateieigenschaften der EXE → Details)