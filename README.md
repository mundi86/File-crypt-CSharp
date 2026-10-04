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
- 🧬 **Getrennte Schlüssel** für Verschlüsselung und Signatur (Container PCv4)
- 🛡️ Sicheres Löschen — Originaldaten werden vor dem Löschen überschrieben
- 🚫 Bestehende Dateien werden nie ungefragt überschrieben
- 📊 Ordner-Verschlüsselung mit Fortschrittsbalken und **Abbrechen**-Schaltfläche
- 💾 Geringer Speicherbedarf — auch sehr große Dateien und Altformate sind kein Problem
- 👁️ Quick-Edit: Datei temporär entschlüsseln, anzeigen und automatisch wieder löschen
- 📦 Kein Admin nötig — per-User Installation ohne UAC
- 🔄 Rückwärtskompatibel mit Dateien aus den Versionen 3.0, 2.0 und 1.0
- ⬆️ Ordner-Entschlüsseln bringt Altdateien automatisch auf das aktuelle Format
- ➕ Saubere Deinstallation über Windows „Apps & Features"
- 🔓 `.protected` Dateien zeigen Schloss-Icon im Explorer + „Entschlüsseln" direkt im Win11 Top-Menü
- 🌗 Moderne Oberfläche im Fluent-Stil — automatisch Hell/Dunkel, inkl. runder Ecken

> **Von Version 3.0 kommst du hierher?** In 3.0 waren Chiffrier- und Signaturschlüssel
> identisch abgeleitet. Es ist zum Glück kein Datenverlust entstanden, aber die
> Dateien sollten auf das neue Format gebracht werden. **Anleitung:
> [docs/MIGRATION.md](docs/MIGRATION.md)** — im Grunde genügt ein Rechtsklick auf den
> Ordner → „Entschlüsseln".

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

Exit-Code `0` = alles grün. 142 Prüfungen: Round-Trips über Block- und Puffergrenzen,
Manipulationserkennung (Bitfehler in Ciphertext/Salt/IV/Signatur, vertauschte Blöcke,
Kürzen, Anhängen), **getrennte Chiffrier-/Signaturschlüssel**, Rückwärtskompatibilität
mit v3, v2 und v1, Abbruchverhalten, atomare Schreibvorgänge sowie ein Test, der
beweist, dass die Altformate streamen statt den Speicher zu füllen.

Zusätzlich prüft ein End-to-End-Lauf die **echte EXE im Fenster** (31 Prüfungen):

```
.\File_crypt\Tests\e2e\Invoke-PrivateCryptTests.ps1
```

> Die E2E-Suite bedient das Fenster über **Windows UI Automation**. Auf
> Systemen ohne `UIAccessBroker` (hier: Build 26200, Dienst nicht vorhanden)
> kann sie den Passwortwert nicht ins Feld schreiben — dort schlägt der Lauf
> fehl, ohne dass das Programm defekt ist. Siehe „Zwei Fallstricke" unten.

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
- Meldet sich mit einem Dialog, danach ist das Fenster geschlossen

> ⚠️ **Wichtig bei einem Wechsel der Installationsart.** Der Inno-Installer legt
> zusätzlich `unins000.exe` und `unins000.dat` an und trägt sich selbst als
> Eintrag in „Apps & Features" ein. Ruft man danach `/install` auf, bleiben
> diese Dateien liegen und der alte Eintrag bestehen — die Deinstallation läuft
> dann über den alten Uninstaller und meldet die alte Version.
>
> Deshalb: **vor dem Wechsel die bestehende Installation über „Apps & Features"
> deinstallieren**, dann installieren. Genau so wurde auch der Wechsel von 3.0
> auf 4.0 durchgeführt.

### Option C — Ohne Installer, von Hand (Weg ohne Inno Setup)

Falls Inno Setup nicht zur Verfügung steht, lässt sich derselbe Zustand auch
ohne den Installer herstellen. Getestet und so wurde 4.0 installiert:

```powershell
# 1. Alte Version sauber entfernen (falls vorhanden)
& "$env:LOCALAPPDATA\privateCrypt\unins000.exe"     # im Dialog bestätigen

# 2. Neue EXE platzieren
$src = ".\File_crypt\File_crypt\bin\Release"
$dst = "$env:LOCALAPPDATA\privateCrypt"
New-Item -ItemType Directory -Path $dst -Force | Out-Null
Copy-Item "$src\privateCrypt.exe"       $dst -Force
Copy-Item "$src\privateCrypt.exe.config" $dst -Force

# 3. Registry-Einträge anlegen
$exe = "$dst\privateCrypt.exe"
$base = "HKCU:\Software\Classes"

#   Einzeldatei
$k = "$base\*\shell\Ver- | Entschlüsseln (AES256)"
New-Item -Path $k -Force | Out-Null
New-ItemProperty -Path $k -Name "icon" -Value ('"'+$exe+'",0') -PropertyType String -Force
New-Item -Path "$k\command" -Force | Out-Null
New-ItemProperty -Path "$k\command" -Name "(default)" -Value ('"'+$exe+'" "%1"') -PropertyType String -Force

#   Ordner verschlüsseln / entschlüsseln
foreach ($verb in @(
    @{n='Verschlüsseln (AES256)';  a='"e"'},
    @{n='Entschlüsseln (AES256)';  a='"d"'})) {
  $k = "$base\Directory\shell\$($verb.n)"
  New-Item -Path $k -Force | Out-Null
  New-ItemProperty -Path $k -Name "icon" -Value ('"'+$exe+'",0') -PropertyType String -Force
  New-ItemProperty -Path $k -Name "Position" -Value "Bottom" -PropertyType String -Force
  New-Item -Path "$k\command" -Force | Out-Null
  New-ItemProperty -Path "$k\command" -Name "(default)" -Value ('"'+$exe+'" "%1" '+$verb.a) -PropertyType String -Force
}

#   Dateizuordnung .protected -> ProgID
New-Item -Path "$base\.protected" -Force | Out-Null
New-ItemProperty -Path "$base\.protected" -Name "(default)"      -Value "privateCrypt.protected"                -PropertyType String -Force
New-ItemProperty -Path "$base\.protected" -Name "Content Type" -Value "application/privateCrypt.protected"    -PropertyType String -Force
New-ItemProperty -Path "$base\.protected" -Name "PerceivedType" -Value "text"                                -PropertyType String -Force

$p = "$base\privateCrypt.protected"
New-Item -Path $p -Force | Out-Null
New-ItemProperty -Path $p -Name "(default)" -Value "Verschlüsselte Datei (AES256)" -PropertyType String -Force
New-Item -Path "$p\DefaultIcon" -Force | Out-Null
New-ItemProperty -Path "$p\DefaultIcon" -Name "(default)" -Value ('"'+$exe+'",0') -PropertyType String -Force
New-Item -Path "$p\shell\open" -Force | Out-Null
New-ItemProperty -Path "$p\shell\open" -Name "(default)" -Value "🔓 Entschlüsseln (AES256)" -PropertyType String -Force
New-Item -Path "$p\shell\open\command" -Force | Out-Null
New-ItemProperty -Path "$p\shell\open\command" -Name "(default)" -Value ('"'+$exe+'" "%1"') -PropertyType String -Force

#   Eintrag für "Apps & Features"
$u = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\privateCrypt"
New-Item -Path $u -Force | Out-Null
New-ItemProperty -Path $u -Name "DisplayName"     -Value "privateCrypt"                -PropertyType String -Force
New-ItemProperty -Path $u -Name "DisplayVersion"  -Value "4.0"                          -PropertyType String -Force
New-ItemProperty -Path $u -Name "Publisher"       -Value "mundi86"                      -PropertyType String -Force
New-ItemProperty -Path $u -Name "DisplayIcon"     -Value ('"'+$exe+'",0')              -PropertyType String -Force
New-ItemProperty -Path $u -Name "UninstallString" -Value ('"'+$exe+'" /uninstall')     -PropertyType String -Force
New-ItemProperty -Path $u -Name "InstallLocation" -Value "$env:LOCALAPPDATA\privateCrypt" -PropertyType String -Force
New-ItemProperty -Path $u -Name "NoModify" -Value 1 -PropertyType DWord -Force
New-ItemProperty -Path $u -Name "NoRepair" -Value 1 -PropertyType DWord -Force

# 4. Startmenü-Eintrag
$ws = New-Object -ComObject WScript.Shell
$s  = $ws.CreateShortcut("$env:APPDATA\Microsoft\Windows\Start Menu\Programs\privateCrypt.lnk")
$s.TargetPath = $exe; $s.WorkingDirectory = $dst; $s.IconLocation = "$exe,0"
$s.Description = "Dateien per Rechtsklick ver- und entschlüsseln (AES-256)"; $s.Save()

# 5. Shell informieren, damit Explorer ohne Neustart aktualisiert
Add-Type @"
using System;using System.Runtime.InteropServices;
public class Sh {
  [DllImport("shell32.dll")] public static extern void SHChangeNotify(int e,uint f,IntPtr a,IntPtr b);
}
"@
[Sh]::SHChangeNotify(0x08000000, 0x0000, [IntPtr]::Zero, [IntPtr]::Zero)
```

**Nur HKCU** — kein Admin, kein UAC. Die Deinstallation läuft danach über
`privateCrypt.exe /uninstall`.

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
- Eintrag mit Version 4.0 sollte erscheinen

### ✅ Installation prüfen, ohne den Explorer zu bemühen

Dass wirklich die erwartete Version in `%LocalAppData%` liegt, lässt sich
direkt abfragen — das ist schneller und eindeutiger als ein Blick ins
Kontextmenü:

```powershell
Get-Item "$env:LOCALAPPDATA\privateCrypt\privateCrypt.exe" |
  Select-Object @{n='Version';e={$_.VersionInfo.FileVersion}}
```

Erwartet: `4.0.0.0`. Steht dort noch eine andere Version, testest du die alte.

Für die Registry-Pfade hilft ein Blick auf die Kommandos:

```powershell
$base = "HKCU:\Software\Classes"
# Ordner-Verben
foreach ($n in (Get-Item "$base\Directory\shell").GetSubKeyNames()) {
  $v = (Get-ItemProperty -LiteralPath "$base\Directory\shell\$n\command")."(default)"
  "$n`n   $v"
}
```

Erwartet wird für alle drei Verben ein Verweis auf
`%LocalAPPDATA%\privateCrypt\privateCrypt.exe`.

> **Namensfalle beim Abfragen:** Die Verben heißen `Ver- | Entschlüsseln (AES256)`
> und enthalten Umlaute. In einer PowerShell-Zeile wandert das leicht in eine
> kaputte Kodierung — dann liefert `Get-ChildItem` nur den Ordner „shell"
> ohne Untereinträge. Besser ist es, die Namen aus der Registry zu *lesen*,
> statt sie im Skript zu schreiben (siehe Schleife oben).

---

## 🧪 Zwei Fallstricke beim automatisierten Testen

Beide sind in diesem Projekt schon aufgetreten und haben jeweils eine
Fehldiagnose ausgelöst. Sie sind hier festgehalten, weil sie wieder auftreten
werden.

### 1. UI-Automation kann den Wert nicht ins Passwortfeld schreiben

Ein Skript, das das Passwort per `ValuePattern.SetValue()` setzt, **lässt das
Feld leer** — der Vorgang startet dann nicht, und es sieht wie ein Programmfehler
aus. Ursache ist in manchen Umgebungen die fehlende UI-Automation-Bridge
(auf diesem Rechner fehlt der Dienst `UIAccessBroker` vollständig).

**Gegenprobe, um sich nicht selbst zu täuschen:** Ein Fenster mit **nur**
Standard-WinForms-Elementen liefert in derselben Umgebung ebenfalls **null**
Kinder. Damit ist klar, dass es an der Umgebung liegt, nicht am Programm.

**Lösung:** Passwort wie ein Mensch eintippen:

```powershell
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;using System.Runtime.InteropServices;
public class K {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h,int c);
}
"@
$p = Start-Process $exe -ArgumentList "`"$datei`"" -PassThru
for ($i=0; $i -lt 50; $i++) {
  Start-Sleep -Milliseconds 300; $p.Refresh()
  if ($p.MainWindowHandle -ne 0) { break }
}
[K]::ShowWindow($p.MainWindowHandle, 9)     | Out-Null   # SW_RESTORE
[K]::SetForegroundWindow($p.MainWindowHandle) | Out-Null
Start-Sleep -Milliseconds 700
[System.Windows.Forms.SendKeys]::SendWait($password)
Start-Sleep -Milliseconds 400
[System.Windows.Forms.SendKeys]::SendWait("{ENTER}")
```

Enter genügt — `Form1` reagiert im Passwortfeld direkt darauf.

### 2. Bei `.protected` ist die Schnellansicht vorbelegt

Beim Entschlüsseln einer `.protected`-Datei ist **„Schnellansicht" automatisch
angehakt**. Das Programm entschlüsselt dann nur temporär, öffnet den
Standard-Viewer und **wartet auf dessen Beendigung**.

Ein Automatisierungslauf, der nur auf das Beenden des Programms wartet, läuft
deshalb in einen Timeout und meldet anschließend fälschlich „Klartext fehlt" —
obwohl Vorgang und Programm einwandfrei arbeiten.

**Lösung:** Die Option vor dem Start abwählen, oder für den Test mit
Schnellansicht rechnen und den Viewer schließen. Die E2E-Suite macht beides
(`-uncheckQuickEdit`).

### Inhalte nie als String vergleichen

Umlaute in Testdaten gehen durch die PowerShell-Konsolencodierung verloren und
machen einen korrekten Round-Trip kaputt. **Byteweise vergleichen**, z. B. über
einen Hash:

```powershell
$hashVorher = (Get-FileHash $plain -Algorithm SHA256).Hash
# … verschlüsseln und entschlüsseln …
if ((Get-FileHash $plain -Algorithm SHA256).Hash -eq $hashVorher) { "ok" }
```

---

## ❓ Nachfragen

### Das Kontextmenü zeigt noch die alte Version

Das Kontextmenü zeigt **immer** auf `%LocalAppData%\privateCrypt\privateCrypt.exe`.
Wenn dort noch eine ältere Version liegt, testest du die alte — und zwar
unabhängig davon, welche EXE du gebaut hast. Am zuverlässigsten prüfst du das
nicht über das Menü, sondern direkt:

```
%LocalAppData%\privateCrypt\privateCrypt.exe --version
```

Im Fenster selbst erkennbar an der Beschriftung über dem Passwortfeld:

| | Version 2.0 | Version 3.0 | Version 4.0 |
|---|---|---|---|
| Beschriftung | `crypt with -> PolyAES256` | `Verschlüsseln → AES-256-CBC + HMAC-SHA256` | `AES-256-CBC + HMAC-SHA256 · neues Format PCv4` |
| Fenster | schmal | 400 px breit | 520 px, Karte mit Passwortfeld und Hinweiszeile |
| Container | `PCv2` | `PCv3` | `PCv4` |

**Abhilfe:** einmalig die neue Version installieren (siehe Schritt 3), oder
die Tests direkt über die Kommandozeile starten und das Kontextmenü meiden:

```
privateCrypt.exe "C:\Pfad\datei.txt"
privateCrypt.exe "C:\Pfad\ordner" e
```

Wer nur die Dateieigenschaften der **gebauten** EXE ansieht, prüft die falsche
Datei — sie liegt unter `bin\Release`, das Menü zeigt auf `%LocalAppData%`.
Entscheidend ist daher immer der Pfad der installierten Kopie.

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

Dateien in einem älteren Format (v3, v2 oder das Original von 2011) werden beim
Entschlüsseln **sofort wieder verschlüsselt** — diesmal im aktuellen
Format PCv4 mit getrennten Schlüsseln. Am Ende steht der ganze Ordner auf v4, ohne
dass du etwas von Hand nachverschlüsseln musst.

```
Verarbeitet: 12
Auf PCv4 aktualisiert: 4 (1 aus PCv3, 2 aus Version 2.0, 1 aus dem Original von 2011)
```

Das ist der Zweck: v2- und v1-Dateien haben **keinen Integritätsschutz**,
v3-Dateien haben zwar einen, aber einen, der auf einem gemeinsamen Schlüssel beruht.
Nach dem Upgrade sind sie sauber getrennt.

Ausführlich: **[docs/MIGRATION.md](docs/MIGRATION.md)**

**Wichtig:** Die Dateien bleiben dabei **verschlüsselt** — es wird kein
Klartext dauerhaft abgelegt. Nur wenn du danach eine einzelne Datei
entschlüsselst, erscheint ihr Inhalt.

Der Ordner-Modus ist der einzige, der automatisch aktualisiert. Beim
Entschlüsseln einer **Einzeldatei** bleibt sie im alten Format; wer sie
aufrüsten will, entschlüsselt und verschlüsselt sie einfach erneut.

> **Sicherheitskopie vorher anlegen.** Der Ordnerlauf ist der einzige Vorgang, der
> Daten verändert, die nicht direkt aus einer Verschlüsselung stammen — er
> überschreibt nach dem Entschlüsseln wieder dieselbe Datei.

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
| Dateischlüssel | aus je einer Hälfte des Masterschlüssels per HMAC abgeleitet — **Chiffrier- und Signaturschlüssel sind nie identisch** |
| Salt / IV | 32 / 16 Byte, pro Datei zufällig |
| Dateiformat | `PCv4` Magic + Salt + IV + Ciphertext + HMAC (32 Byte) |
| Speicherbedarf | konstant, unabhängig von der Dateigröße (streaming, auch für Altformate) |
| Schreibweise | atomar über temporäre Datei, nie halb geschrieben |
| Datei-Löschung | Überschreiben mit Nullbytes vor Delete |
| Mindest-Passwort | 8 Zeichen beim Verschlüsseln, 4 beim Entschlüsseln |
| Framework | .NET Framework 4.8 (vorinstalliert auf Win 10/11) |
| Platform | x86 (32-bit) |
| Installation | Per-User, kein Admin nötig |
| UI-Theme | Automatisch Hell/Dunkel (Windows-System-Theme), Segoe UI Variable |
| Win11-Styling | DWM: dunkle Titelleiste + runde Ecken; Kartenrand folgt der Eckeneinstellung |
| Datei-Icon | `.protected` Dateien zeigen Schloss-Icon im Explorer |
| Win11-Menü | `.protected` Verb direkt im Top-Level (via ProgID) |

### Dateiformat v4 (aktuell)
```
[PCv4 4 B] [Salt 32 B] [IV 16 B] [Ciphertext ...] [HMAC-SHA256 32 B]
```
Die Signatur wird **geprüft, bevor** überhaupt Klartext entsteht. Ein falsches Passwort und
eine manipulierte Datei führen bewusst zur selben Meldung.

Der 64-Byte-Masterschlüssel wird in zwei Hälften geteilt: die erste liefert **ausschließlich**
den Chiffrierschlüssel, die zweite **ausschließlich** den Signaturschlüssel. Genau das war in
Version 3.0 versehentlich nicht der Fall — beide waren identisch. Details und Migrationsanleitung
in [docs/MIGRATION.md](docs/MIGRATION.md).

### Dateiformat v3 (nur noch Lesen, seit 4.0)
Dateien aus Version 3.0 (`[PCv3][Salt][IV][Ciphertext][HMAC]`) werden automatisch erkannt und
können weiterhin entschlüsselt werden — auch mit intaktem Integritätsschutz.

### Dateiformat v2 (nur noch Lesen, seit 3.0)
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
│   ├── PolyAES.cs               Verschlüsselung (v4/v3/v2/v1), streaming
│   ├── FileOps.cs               Dateisystem-Helfer (Endungen, Sammeln, SecureDelete)
│   ├── Theme.cs                 Farben, Schriften, Metrik (Hell/Dunkel)
│   ├── ModernControls.cs        Karte, Passwortfeld, Flächenknopf, Fortschrittsbalken
│   ├── cConfig.cs               Kontextmenü + Dateizuordnung (HKCU)
│   ├── Form1.cs / .Designer.cs  Oberfläche und Ablaufsteuerung
│   ├── Program.cs               Einstiegspunkt
│   └── app.manifest             DPI, Common Controls v6, asInvoker
└── Tests/
    ├── PolyAES.Tests/           Testsuite ohne externe Abhängigkeiten
    │   ├── RoundTripTests.cs
    │   ├── BackwardCompatTests.cs
    │   ├── LegacyFormatWriter.cs  unabhängige Erzeuger für v2/v3
    │   ├── TamperTests.cs
    │   ├── FileOpsTests.cs
    │   ├── LegacyVectorTests.cs
    │   └── legacy-vectors/      Fixed Vektoren aus dem 2011-Algorithmus
    └── e2e/
        └── Invoke-PrivateCryptTests.ps1   End-to-End gegen die echte EXE
installer/privateCrypt.iss       Inno Setup Installer
docs/MIGRATION.md               Anleitung für Dateien aus v1/v2/v3
docs/TESTING.md                 Teststrategie, was abgedeckt ist und was nicht
```

Weitere Details zur Kryptografie stehen in [SECURITY.md](SECURITY.md), die
Anleitung zum Umstieg auf das aktuelle Format in
[docs/MIGRATION.md](docs/MIGRATION.md), die Testabdeckung und ihre Lücken in
[docs/TESTING.md](docs/TESTING.md), die Änderungshistorie in
[CHANGELOG.md](CHANGELOG.md).

---

## 📌 Versionsangaben an vier Stellen

Die Version steht in **vier** Dateien und muss überall gleich sein:

| Datei | Ort | Wert |
|---|---|---|
| `File_crypt/File_crypt/Properties/AssemblyInfo.cs` | `AssemblyVersion`, `AssemblyFileVersion` | `4.0.0.0` |
| `File_crypt/File_crypt/app.manifest` | `assemblyIdentity/@version` | `4.0.0.0` |
| `File_crypt/File_crypt/cConfig.cs` | `AppVersion` (Uninstall-Eintrag) | `4.0` |
| `installer/privateCrypt.iss` | `AppVersion` | `4.0` |

Das ist nicht nur Kosmetik: wer nur in die Dateieigenschaften der EXE
schaut, erkennt sonst nicht, welche Version läuft. Genau das ist beim
Testen von 3.0 passiert — die Registry und der Installer meldeten schon
3.0, die EXE aber noch 2.0.0.0, und es wurde versehentlich die alte
Version getestet.

**Prüfen statt glauben** — das hier deckt alle vier Stellen auf:

```powershell
Select-String -Path `
  File_crypt\File_crypt\Properties\AssemblyInfo.cs -Pattern 'AssemblyVersion'
Select-String -Path File_crypt\File_crypt\app.manifest         -Pattern 'assemblyIdentity'
Select-String -Path File_crypt\File_crypt\cConfig.cs          -Pattern 'AppVersion\s*='
Select-String -Path installer\privateCrypt.iss                -Pattern '#define AppVersion'
```

Die `assemblyIdentity` im Manifest erscheint nicht in den
Dateieigenschaften — sie ist die Versionsidentität für Windows selbst
(Activation Context). Sie desynchronisiert zu lassen fällt deshalb
zuerst niemandem auf.

**Kurz prüfen, welche Version wirklich läuft:**

```
Programm\privateCrypt.exe --version
```
(bzw. Dateieigenschaften der EXE → Details)