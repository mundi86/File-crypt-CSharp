# Testing

Drei Ebenen. Die Unit-Tests laufen ohne externe Abhängigkeiten, der
End-to-End-Lauf braucht ein laufendes Windows-Desktop, die manuelle
Prüfung ersetzt nichts davon — sie ist die letzte Instanz.

| Ebene | Umfang | Aufwand | Ohne Netz/Installer |
|---|---|---|---|
| Unit-Tests (`PolyAES.Tests`) | Krypto-Schicht | Sekunden | ja |
| End-to-End (`Invoke-PrivateCryptTests.ps1`) | echte EXE im Fenster | ~2 min | ja |
| Manuell | Bedienung und Erscheinungsbild | ~15 min | ja |

---

## 1. Unit-Tests

Krypto-Schicht und Dateisystem-Helfer, 142 Prüfungen, bewusst als
Konsolen-Anwendung **ohne** NuGet und ohne Test-Framework — sonst
scheitert der Lauf an Paket-Restore.

```powershell
msbuild File_crypt\File_crypt.sln /p:Configuration=Release /p:Platform=x86
File_crypt\Tests\PolyAES.Tests\bin\Release\PolyAES.Tests.exe
```

Exit-Code `0` = alles grün. Das Skript druckt jeden Fehlschlag am Ende
noch einmal zusammen.

**Bitte vor jeder Änderung an `PolyAES.cs` oder `FileOps.cs` ausführen.**

### Was abgedeckt ist

| Gruppe | Prüfungen |
|---|---|
| Round-Trip | 0, 1, 15, 16, 17, 31, 32, 33, 4095, 4096, 4097, 65535, 65536, 65537, 100000 Byte — also über AES-Block- und Puffergrenzen hinweg; zusätzlich Größe des Containers geprüft |
| Zufälligkeit | gleicher Klartext → unterschiedliche Chiffre; Salt und IV variieren je Datei |
| Format | Magic-Bytes, Headeraufteilung, `DetectFormat` inkl. leerer Datei |
| **Schlüsseltrennung** | Chiffrier- und Signaturschlüssel sind verschieden, je 32 Byte, nicht null und in vielen Bytes unterschiedlich; Ableitung reproduzierbar; anderer Salt ergibt anderen Schlüssel; v3 reproduziert nachweislich den gemeinsamen Schlüssel von 3.0 |
| **Manipulation** | Bitflip in Ciphertext, Salt, IV und Signatur; vertauschte CBC-Blöcke; abgeschnittene Datei; entfernte Signatur; verlängerte Datei; falsches Passwort |
| Nebenläufigkeit | Ziel bleibt bei fehlgeschlagener Entschlüsselung unangetastet, keine `.pctmp`-Reste |
| Überschreiben | `overwrite=false` lässt das Ziel unverändert, `true` ersetzt es |
| **Abbruch** | `CancellationToken` meldet Abbruch, legt kein Ziel an, hinterlässt keine `.pctmp`-Reste und lässt ein bestehendes Ziel unverändert — für Verschlüsseln und Entschlüsseln |
| `FileOps` | case-insensitive Endung, `Replace`-Falle, leere Namen, Junction-Erkennung, `.db`-Übersprung |
| **Rückwärtskompatibilität** | 6 feste v1-Vektoren, v2-Round-Trip, **v3 aus Version 3.0** (einschließlich Manipulation), Fehlermeldungen bei zu kurzen Dateien und ungerader Blockgröße |
| **Speicherverbrauch** | 48-MB-Altdatei in v2 und im 2011-Format wird entschlüsselt, während der Working Set des Testprozesses beobachtet wird; er darf nicht mit der Dateigröße wachsen |

### Der v3-Test und warum er eine eigene Implementierung braucht

`LegacyFormatWriter.MakeV3File` bildet die Schlüsselableitung von 3.0 nach —
**einschließlich des Fehlers**, also mit einem einzigen Schlüssel für Chiffre
und Signatur. Nur so lässt sich prüfen, dass 4.0 eine echte 3.0-Datei öffnet.

Das ist keine Doppelung von Produktivcode, sondern der übliche Weg bei
Rückwärtskompatibilität: derselbe Aufruf wie im Produktivcode würde denselben
Fehler auf beiden Seiten reproduzieren und der Test bliebe grün. Ein Test, der
nur `DecryptFile` mit dem Ergebnis von `EncryptFile` prüft, sagt über die
Abwärtskompatibilität nichts aus.

Aus demselben Grund existieren die v1-Vektoren im Repository.

### Eine Stolperfalle beim Speichertest

Der erste Entwurf dieses Tests hat **sich selbst falsch beschuldigt**: Er hat
Klartext und Ergebnis mit `File.ReadAllBytes` verglichen und damit genau den
Speicherbedarf erzeugt, den er ausschließen wollte (gemessen: +144 MB für eine
48-MB-Datei).

Korrekt ist, die Vorlage auf der Platte zu lassen und über einen Hash zu
vergleichen — sowie den Testprozess im **Any-CPU-** statt **x86-**-Modus laufen
zu lassen. Als 32-Bit-Prozess wäre ein Speicherfehler in der Datei nämlich gar
nicht erst entstanden, und der Test hätte nichts nachweisen können.

### Die v1-Testvektoren

`Tests/PolyAES.Tests/legacy-vectors/` enthält Dateien, die mit dem
Algorithmus von 2011 erzeugt wurden — mit einer **vom Produktivcode
unabhängigen** Implementierung. Deshalb schlägt der Test fehl, falls die
Legacy-Entschlüsselung kaputtgeht, statt denselben Fehler auf beiden
Seiten zu reproduzieren.

Jede Vektordatei wird zusätzlich gegen ihre Prüfsumme aus `index.txt`
verifiziert, damit niemand die Testdaten versehentlich verändert.

---

## 2. End-to-End gegen die echte EXE

```powershell
.\File_crypt\Tests\e2e\Invoke-PrivateCryptTests.ps1
.\File_crypt\Tests\e2e\Invoke-PrivateCryptTests.ps1 -Exe "C:\Pfad\privateCrypt.exe"
```

Startet die gebaute Anwendung, bedient sie über Windows UI Automation und
prüft das Ergebnis auf der Platte. 31 Prüfungen:

1. Einzeldatei verschlüsseln → PCv4-Container, Original gelöscht
2. Round-Trip → Klartext bit-genau wiederhergestellt, `.protected` entfernt
3. **Bitflip im Ciphertext** → Fehlermeldung, **kein Klartext**, Quelldatei intakt
4. Falsches Passwort → kein Klartext, Quelldatei intakt
5. Zu kurzes Passwort → abgewiesen, nichts verschlüsselt
6. Ordner rekursiv → inkl. Unterordner, Leerzeichen im Namen, Großbuchstaben-Endung; `.db` übersprungen; keine Doppelverschlüsselung
7. Überschreiben-Schutz → bestehende Datei unverändert
8. **Auto-Upgrade** → zwei echte v1-Dateien aus `legacy-vectors` werden entschlüsselt und müssen als PCv4 wiederkommen, **ohne dass Klartext liegen bleibt**, und danach mit demselben Passwort wieder lesbar sein
9. `%TEMP%\privateCrypt-quickedit` enthält keine Reste

> Die Punktzahl bleibt bei 31, weil sich nur die Magic-Byte-Prüfung von
> `PCv3` auf `PCv4` geändert hat, nicht die Anzahl der Prüfungen.

### Zwei Fallen, die beide Zeit gekostet haben

Beide sind im Skriptkommentar begründet, hier zur Nachvollziehbarkeit:

**ASCII in Skriptdateien.** PowerShell 5.1 liest `.ps1` **ohne BOM** als
ANSI (Windows-1252). Ein Umlaut im Skriptliteral wird dadurch zu Müll und
Vergleiche gegen echte Fenstertitel schlagen fehl. Deshalb enthält das
Skript ausschließlich ASCII und greift über Namenspräfixe (`verschl`,
`entschl`) auf Schaltflächen zu.

**Nie den ersten Button einer Trefferliste nehmen.** Solange das Fenster
im Aufbau ist, liefert UI Automation unter Umständen nur die
Caption-Buttons des Titelfensters (Minimieren/Maximieren/Schließen). Ein
`Invoke()` darauf schließt das Fenster — mit der Folge, dass die
laufende Operation abbricht und die Meldung **„Vorgang abgebrochen"**
erscheint, obwohl niemand abgebrochen hat.

Genau dieser Fehlschluss hat eine Fehlersuche in `Form1.cs` ausgelöst,
die sich als vermeintlicher Programmbug darstellte. Belegt wurde das mit
einer temporären Protokolldatei: `CloseReason.UserClosing` 97 ms nach
Operationsstart, während das Fenster noch im Aufbau war
(`Buttons: 1` statt `Buttons: 4`).

Wer den Ablauf nachvollziehen möchte, setzt `PCLOG` auf eine Datei und
ruft den E2E-Lauf auf — ohne Log-Einträge ändert sich am Verhalten nichts:

```powershell
$env:PCLOG = "$env:TEMP\pclog.txt"
.\File_crypt\Tests\e2e\Invoke-PrivateCryptTests.ps1
Remove-Item Env:\PCLOG
```

---

## 3. Manuell prüfen

Was keine Automatisierung ersetzt: Bedienung, Erscheinungsbild, das
Gefühl beim Warten.

- **Button-Beschriftung lesbar?** „entschlüsseln" muss vollständig stehen.
  Der Knopf nimmt jetzt die volle Kartenbreite (440 px) ein, ist also bei jeder
  Skalierung breit genug — es ist trotzdem einen Blick wert.
- **Fensterbreite** 520 px Client, Höhe 260 px. Lange Dateinamen werden mit
  „…" gekürzt; der vollständige Name muss als Tooltip erscheinen.
- **Quick Edit**: temporär entschlüsseln, Viewer öffnet, beim Schließen des
  Viewers verschwindet die App und die Temp-Datei wird gelöscht.
- **Fortschritt und Abbrechen** bei einem Ordner mit vielen Dateien. In 3.0 lagen
  beide Elemente bei y = 118 und y = 140 in einem 95 px hohen Fenster und waren
  damit **nie erreichbar** — der Abbruch war undurchführbar. Beide Elemente
  müssen jetzt ohne Weiteres sichtbar sein.
- **Anzeigen/Verbergen** des Passworts: Link klicken, Klartext erscheint, beim
  Verlassen des Feldes wird wieder verdeckt.
- **Dunkelmodus** umschalten (Windows-Einstellungen → Personalisierung). Die
  Titelleiste, die Karten, das Passwortfeld und der Fortschrittsbalken müssen
  gemeinsam umschalten; im Standard-ProgressBar blieb previously ein heller
  Streifen stehen.
- **Ecken abrunden aus** (Windows → Personalisierung → Ecken für Fenster
  abrunden = Nie): die Karten müssen dann eckig sein, sonst wirkt die Karte
  unrund gegen ein eckiges Fenster.
- **Altdateien** aus 3.0, 2.0 und 2011 öffnen; die Hinweiszeile im Fenster nennt
  das erkannte Format und ob Integritätsschutz besteht.

### Passwort

`TestPasswort123` zum Ausprobieren. Beim **Verschlüsseln** sind
mindestens 8 Zeichen Pflicht, beim **Entschlüsseln** 4 — sonst wären
Altdateien mit kurzen Passwörtern nicht mehr erreichbar.

---

## Bekannte Lücken

Ehrlich benannt, was **nicht** abgesichert ist:

- **Die GUI-Logik ist nicht automatisiert getestet.** Die
  End-to-End-Prüfung fährt einmalig durch die echten Abläufe, ersetzt
  aber keinen Regressionstest. Für WinForms gibt es ohne NuGet keinen
  vernünftigen UI-Test-Rahmen.
- **Quick Edit mit Bildbetrachter wurde nicht verifiziert.** Der Pfad
  braucht einen installierten Bild-Viewer; in der Automatisierung stand
  keiner zur Verfügung. Der manuelle Test 8 deckt das ab.
- **Der Installer wurde nicht gebaut.** `installer/privateCrypt.iss`
  ist auf Version 3.0 gebracht (feste `AppId`, quoting bei
  `UninstallDisplayIcon`), aber Inno Setup war auf dem Build-Rechner
  nicht installiert. `privateCrypt_Setup.exe` im `dist/`-Ordner ist
  deshalb noch der alte 2.0-Stand — bitte neu kompilieren.
- **Kein Test der Registry-Registrierung.** `/install` und `/uninstall`
  schreiben in `HKCU`; das ist nur manuell prüfbar, weil Tests die
  Registry des Nutzers nicht verändern sollten.
- **Kein Test der Abbruchmitte im Ordnerlauf.** „Abbrechen" während
  vieler Dateien ist manuell getestet, nicht automatisiert.

---

## Vor einem Release

```powershell
# 1. Bauen
msbuild File_crypt\File_crypt.sln /t:Rebuild /p:Configuration=Release /p:Platform=x86

# 2. Unit-Tests  (Exit-Code muss 0 sein)
File_crypt\Tests\PolyAES.Tests\bin\Release\PolyAES.Tests.exe
if ($LASTEXITCODE -ne 0) { throw "Unit-Tests fehlgeschlagen" }

# 3. End-to-End  (meldet "FEHLGESCHLAGEN: 0")
.\File_crypt\Tests\e2e\Invoke-PrivateCryptTests.ps1

# 4. Installer bauen (Inno Setup 6 erforderlich)
iscc installer\privateCrypt.iss

# 5. Versionen muessen uebereinstimmen
#    AssemblyInfo.cs   -> AssemblyVersion
#    cConfig.cs        -> AppVersion (Uninstall-Eintrag)
#    privateCrypt.iss  -> AppVersion
```

Schritt 5 hat schon einmal zugeschlagen: `AssemblyInfo.cs` stand auf
2.0.0.0, während Registry und Installer 3.0 meldeten. Wer nur in die
Dateieigenschaften der EXE schaut, konnte 2.0 und 3.0 nicht
unterscheiden — und hat daraufhin versehentlich die alte Version
getestet.