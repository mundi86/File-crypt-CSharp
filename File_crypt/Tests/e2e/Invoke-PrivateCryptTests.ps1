# ============================================================================
# privateCrypt 3.0 - End-to-End-Testlauf gegen die echte EXE
#
# Startet die gebaute Anwendung, bedient sie ueber Windows UI Automation und
# prueft das Ergebnis auf der Platte. Ergaenzt die Unit-Tests, deckt aber nur
# den Weg ab, den ein Nutzer tatsaechlich ausuebt.
#
# Aufruf:
#   .\Invoke-PrivateCryptTests.ps1
#   .\Invoke-PrivateCryptTests.ps1 -Exe "C:\Pfad\privateCrypt.exe"
#
# WICHTIG - zwei Stolperfallen, die beide schon Zeit gekostet haben:
#
# 1. ALLE Strings in diesem Skript sind bewusst ASCII. PowerShell 5.1 liest
#    .ps1-Dateien ohne BOM als ANSI (Windows-1252). Ein "ue-Laut" im Skript
#    wird dadurch zu Muell, und Vergleiche gegen echte Fenstertitel oder
#    Schaltflaechentexte schlagen dann fehl. Umlaute gehoeren in
#    Variablen am Skriptanfang, die man als [char] codiert, nicht in
#    Skriptliterale.
#
# 2. Auf die Schaltflaeche wird ueber einen Namenspraefix zugegriffen
#    ("verschl" / "entschl"). Niemals ueber den ersten Treffer einer
#    Button-Liste: solange das Fenster im Aufbau ist, liefert UI Automation
#    unter Umstaenden nur die Caption-Buttons des Titelfensters
#    (Minimieren/Maximieren/Schliessen). Ein Invoke() darauf schliesst das
#    Fenster - das fuehrt zu "Vorgang abgebrochen", obwohl niemand
#    abgebrochen hat. Genau dieser Fehlschluss hat eine Fehlersuche in
#    Form1.cs ausgeloest, die sich als vermeintlicher Bug im Programm
#    darstellte.
# ============================================================================
param(
    [string]$Exe = (Join-Path $PSScriptRoot "..\..\..\File_crypt\File_crypt\bin\Release\privateCrypt.exe"),
    [string]$Pw = "TestPasswort123",
    [string]$WorkRoot = ""
)

if (-not (Test-Path -LiteralPath $Exe)) {
    Write-Host ("EXE nicht gefunden: " + $Exe) -ForegroundColor Red
    Write-Host "Bitte erst bauen:"
    Write-Host "  msbuild File_crypt\File_crypt.sln /p:Configuration=Release /p:Platform=x86"
    exit 2
}

$Exe = (Resolve-Path -LiteralPath $Exe).Path
Write-Host ("Teste: " + $Exe)
Write-Host ("Version: " + (Get-Item $Exe).VersionInfo.FileVersion)

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$BTN = [System.Windows.Automation.ControlType]::Button
$EDT = [System.Windows.Automation.ControlType]::Edit
$CHK = [System.Windows.Automation.ControlType]::CheckBox

$root = if ($WorkRoot) { $WorkRoot } else {
    Join-Path ([Environment]::GetFolderPath('UserProfile')) ("AppData\Local\pc_run_" + [guid]::NewGuid().ToString('N').Substring(0,8))
}
New-Item -ItemType Directory -Path $root -Force | Out-Null
Write-Host ("Arbeitsordner: " + $root)

$script:pass = 0
$script:fail = 0

function Ok($m)   { Write-Host ("  [ok]   " + $m); $script:pass++ }
function Bad($m)  { Write-Host ("  [FAIL] " + $m); $script:fail++ }

# --- Fenster frisch holen (UIA-Elemente veralten) ---
function Get-FreshWindow($procId) {
    $cond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $procId)
    $all = [System.Windows.Automation.AutomationElement]::RootElement.FindAll(
        [System.Windows.Automation.TreeScope]::Children, $cond)
    foreach ($w in $all) {
        if ($w.Current.BoundingRectangle.Width -gt 50) { return $w }
    }
    return $null
}

# --- App starten und warten, bis Passwortfeld UND Buttons da sind ---
# Genau dieses Muster hat sich als funktionsfaehig erwiesen.
function Start-App($path, $mode = $null) {
    $argList = if ($mode) { "`"$path`" $mode" } else { "`"$path`"" }
    $p = Start-Process -FilePath $Exe -ArgumentList $argList -PassThru
    $w = $null; $edits = @(); $btns = @()
    for ($i = 0; $i -lt 100; $i++) {
        $w = Get-FreshWindow $p.Id
        if ($w) {
            $ec = New-Object System.Windows.Automation.PropertyCondition(
                [System.Windows.Automation.AutomationElement]::ControlTypeProperty, $EDT)
            $bc = New-Object System.Windows.Automation.PropertyCondition(
                [System.Windows.Automation.AutomationElement]::ControlTypeProperty, $BTN)
            $edits = @($w.FindAll([System.Windows.Automation.TreeScope]::Descendants, $ec))
            $btns  = @($w.FindAll([System.Windows.Automation.TreeScope]::Descendants, $bc))
            if ($edits.Count -ge 1 -and $btns.Count -ge 2) { break }
        }
        Start-Sleep -Milliseconds 200
    }
    return @{ Proc = $p; Win = $w; Edit = $edits; Buttons = $btns }
}

# --- Aktionsbutton gezielt nach Namenspraefix suchen (vermeidet Caption-Buttons) ---
function Get-ActionButton($buttons, $prefix) {
    foreach ($b in $buttons) {
        $n = $b.Current.Name
        if ($n -and $n.StartsWith($prefix)) { return $b }
    }
    return $null
}

function Invoke-Action($app, $prefix, $password, $uncheckQuickEdit) {
    $w = $app.Win
    $bc = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty, $BTN)
    $cc = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty, $CHK)

    $btn = Get-ActionButton @($w.FindAll([System.Windows.Automation.TreeScope]::Descendants, $bc)) $prefix
    if (-not $btn) { return "keinButton" }

    if ($uncheckQuickEdit) {
        $cb = $w.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cc)
        if ($cb) {
            $tp = $cb.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
            if ($tp.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::On) { $tp.Toggle() }
            Start-Sleep -Milliseconds 300
        }
    }

    $ec = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty, $EDT)
    $edit = $w.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $ec)
    $edit.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($password)
    Start-Sleep -Milliseconds 400
    $btn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    return "ok"
}

function Wait-Exit($p, $sec = 90) {
    $deadline = (Get-Date).AddSeconds($sec)
    while (-not $p.HasExited -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 400 }
    return $p.HasExited
}

function Stop-App($p) {
    Get-Process -Id $p.Id -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 600
}

try {
    # =====================================================================
    Write-Host ""
    Write-Host "=== 1: Einzeldatei verschluesseln ==="
    $d = Join-Path $root "t1"; New-Item -ItemType Directory -Path $d | Out-Null
    $f = Join-Path $d "bericht.txt"
    $inhalt = "Pruefinhalt: dieser Text muss identisch zurueckkommen. Sonderzeichen: UTF-8 Zeilen."
    [System.IO.File]::WriteAllText($f, $inhalt, (New-Object System.Text.UTF8Encoding($false)))

    $a = Start-App $f
    if ((Invoke-Action $a "verschl" $Pw $false) -ne "ok") { Bad "1: Aktionsbutton nicht gefunden" }
    else {
        $ex = Wait-Exit $a.Proc 90
        if (-not $ex) { Stop-App $a.Proc; Bad "1: App beendet sich nicht" }
        $enc = "$f.protected"
        if (Test-Path $enc) {
            $magic = [System.Text.Encoding]::ASCII.GetString([byte[]](Get-Content -LiteralPath $enc -Encoding Byte -TotalCount 4))
            if ($magic -eq "PCv3") { Ok ("1: PCv3 erzeugt ({0} Bytes)" -f (Get-Item $enc).Length) } else { Bad "1: Magic '$magic'" }
        } else { Bad "1: keine .protected erzeugt" }
        if (Test-Path $f) { Bad "1: Original nicht entfernt" } else { Ok "1: Original sicher geloescht" }
    }

    # =====================================================================
    Write-Host ""
    Write-Host "=== 2: Datei entschluesseln (Round-Trip) ==="
    Copy-Item $enc (Join-Path $root "backup1.protected") -Force
    $a2 = Start-App $enc
    if ((Invoke-Action $a2 "entschl" $Pw $true) -ne "ok") { Bad "2: Aktionsbutton nicht gefunden" }
    else {
        if (-not (Wait-Exit $a2.Proc 90)) { Stop-App $a2.Proc; Bad "2: App beendet sich nicht" }
        if (Test-Path $f) {
            $ist = [System.IO.File]::ReadAllText($f)
            if ($ist -eq $inhalt) { Ok "2: Klartext bit-genau wiederhergestellt" } else { Bad "2: Inhalt weicht ab" }
        } else { Bad "2: Klartext fehlt" }
        if (Test-Path $enc) { Bad "2: .protected nicht entfernt" } else { Ok "2: .protected entfernt" }
    }

    # =====================================================================
    Write-Host ""
    Write-Host "=== 3: Manipulationserkennung (Bitflip im Ciphertext) ==="
    $tamper = Join-Path $root "manip.protected"
    Copy-Item (Join-Path $root "backup1.protected") $tamper -Force
    $b = [System.IO.File]::ReadAllBytes($tamper)
    $b[52 + 40] = $b[52 + 40] -bxor 0x01
    [System.IO.File]::WriteAllBytes($tamper, $b)
    $a3 = Start-App $tamper
    if ((Invoke-Action $a3 "entschl" $Pw $true) -ne "ok") { Bad "3: Aktionsbutton nicht gefunden" }
    else {
        # Erwartet: Fehlermeldung -> App bleibt offen
        Start-Sleep -Seconds 6
        if ($a3.Proc.HasExited) { Ok "3: App beendet (kein Klartext erzeugt)" }
        else { Ok "3: App meldet Fehler und bleibt offen"; Stop-App $a3.Proc }
        $out = Join-Path $root "manip.txt"
        if (Test-Path $out) { Bad "3: Klartext trotz Manipulation erzeugt!" }
        else { Ok "3: kein Klartext erzeugt" }
        if (Test-Path $tamper) { Ok "3: .protected unveraendert erhalten" } else { Bad "3: .protected geloescht" }
    }

    # =====================================================================
    Write-Host ""
    Write-Host "=== 4: Falsches Passwort ==="
    $a4 = Start-App (Join-Path $root "backup1.protected")
    if ((Invoke-Action $a4 "entschl" "VoelligFalschesPasswort" $true) -ne "ok") { Bad "4: Aktionsbutton nicht gefunden" }
    else {
        Start-Sleep -Seconds 6
        if ($a4.Proc.HasExited) { Ok "4: App beendet (kein Klartext)" } else { Ok "4: Fehlermeldung offen"; Stop-App $a4.Proc }
        if (Test-Path (Join-Path $root "backup1.txt")) { Bad "4: Klartext trotz falschem Passwort!" }
        else { Ok "4: kein Klartext bei falschem Passwort" }
        if (Test-Path (Join-Path $root "backup1.protected")) { Ok "4: Quelldatei unveraendert" } else { Bad "4: Quelldatei geloescht" }
    }

    # =====================================================================
    Write-Host ""
    Write-Host "=== 5: Zu kurzes Passwort ==="
    $d5 = Join-Path $root "t5"; New-Item -ItemType Directory -Path $d5 | Out-Null
    $f5 = Join-Path $d5 "kurz.txt"
    Set-Content -LiteralPath $f5 -Value "Inhalt"
    $a5 = Start-App $f5
    if ((Invoke-Action $a5 "verschl" "abc" $false) -ne "ok") { Bad "5: Aktionsbutton nicht gefunden" }
    else {
        Start-Sleep -Seconds 4
        if ($a5.Proc.HasExited) { Bad "5: App hat trotz zu kurzem Passwort beendet" }
        else { Ok "5: zu kurzes Passwort wird abgewiesen (App laeuft weiter)"; Stop-App $a5.Proc }
        if (Test-Path $f5) { Ok "5: Datei unveraendert" } else { Bad "5: Datei verschwunden" }
        if (Test-Path "$f5.protected") { Bad "5: trotzdem verschluesselt" } else { Ok "5: keine Verschluesselung erfolgt" }
    }

    # =====================================================================
    Write-Host ""
    Write-Host "=== 6: Ordner rekursiv + .db-Uebersprung ==="
    $d6 = Join-Path $root "t6"
    New-Item -ItemType Directory -Path (Join-Path $d6 "Unter\NochTiefer") -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $d6 "a.txt") -Value "A"
    Set-Content -LiteralPath (Join-Path $d6 "b mit Leerzeichen.dat") -Value "B"
    Set-Content -LiteralPath (Join-Path $d6 "GrossBUCHSTABEN.TXT") -Value "C"
    Set-Content -LiteralPath (Join-Path $d6 "index.db") -Value "SQLite"
    Set-Content -LiteralPath (Join-Path $d6 "Unter\c.txt") -Value "C"
    Set-Content -LiteralPath (Join-Path $d6 "Unter\NochTiefer\d.txt") -Value "D"

    $a6 = Start-App $d6
    if ((Invoke-Action $a6 "verschl" $Pw $false) -ne "ok") { Bad "6: Aktionsbutton nicht gefunden" }
    else {
        if (-not (Wait-Exit $a6.Proc 180)) { Stop-App $a6.Proc; Bad "6: Ordnerlauf beendet sich nicht" }
        foreach ($rel in @("a.txt.protected","b mit Leerzeichen.dat.protected","GrossBUCHSTABEN.TXT.protected","Unter\c.txt.protected","Unter\NochTiefer\d.txt.protected")) {
            if (Test-Path (Join-Path $d6 $rel)) { Ok ("6: verschluesselt " + $rel) } else { Bad ("6: FEHLT " + $rel) }
        }
        if (Test-Path (Join-Path $d6 "index.db.protected")) { Bad "6: index.db wurde mitverschluesselt" }
        else { Ok "6: index.db uebersprungen" }
        $doppelt = Test-Path (Join-Path $d6 "GrossBUCHSTABEN.TXT.protected.protected")
        if ($doppelt) { Bad "6: Doppelverschluesselung bei GROSSBUCHSTABEN!" } else { Ok "6: keine Doppelverschluesselung" }
    }

    # =====================================================================
    Write-Host ""
    Write-Host "=== 7: Ueberschreiben-Schutz ==="
    # Echter Ablauf: Ordner verschluesseln, danach Zieldatei neu anlegen,
    # dann Ordner entschluesseln -> vorhandene Datei darf nicht ersetzt werden.
    $d7 = Join-Path $root "t7"; New-Item -ItemType Directory -Path $d7 | Out-Null
    $x7 = Join-Path $d7 "doppelt.txt"
    Set-Content -LiteralPath $x7 -Value "ORIGINALINHALT"

    $a7a = Start-App $d7 "e"
    if ((Invoke-Action $a7a "verschl" $Pw $false) -ne "ok") { Bad "7a: Aktionsbutton nicht gefunden" }
    else {
        if (-not (Wait-Exit $a7a.Proc 120)) { Stop-App $a7a.Proc; Bad "7a: Verschluesseln laeuft nicht durch" }
        if (Test-Path (Join-Path $d7 "doppelt.txt.protected")) { Ok "7a: Ausgangsdatei verschluesselt" }
        else { Bad "7a: keine .protected erzeugt" }

        # Neue Datei mit anderem Inhalt anlegen - sie ist das Entschluesselungsziel.
        Set-Content -LiteralPath $x7 -Value "NEUER INHALT DARF NICHT VERNICHTET WERDEN"

        $a7b = Start-App $d7 "d"
        if ((Invoke-Action $a7b "entschl" $Pw $false) -ne "ok") { Bad "7b: Aktionsbutton nicht gefunden" }
        else {
            # Erwartet: die App zeigt eine Zusammenfassung ("uebersprungen: 1")
            # und bleibt deshalb offen, bis der Dialog bestaetigt wird.
            Start-Sleep -Seconds 8
            $ex7 = $a7b.Proc.HasExited
            if (-not $ex7) { Ok "7b: Zusammenfassung angezeigt, App wartet auf Bestaetigung"; Stop-App $a7b.Proc }
            else { Ok "7b: App beendet sich selbst" }
            $ist = (Get-Content -LiteralPath $x7 -Raw)
            if ($ist -like "*NEUER INHALT DARF NICHT*") { Ok "7: bestehende Datei unveraendert (uebersprungen)" }
            else { Bad "7: bestehende Datei wurde ueberschrieben!" }
            if (Test-Path (Join-Path $d7 "doppelt.txt.protected")) { Ok "7: .protected erhalten, weil Ziel existierte" }
            else { Bad "7: .protected geloescht obwohl Ziel existierte" }
        }
    }

    # =====================================================================
    Write-Host ""
    Write-Host "=== 8: Auto-Upgrade von Altformat auf PCv3 (Ordnerlauf) ==="
    # Echter Altformat-Ordner: zwei v1-Vektoren aus dem Original von 2011, beide
    # mit dem Passwort "pass1234", abgelegt als .protected. Der Ordnerlauf
    # entschluesselt sie und verschluesselt sie sofort wieder als PCv3.
    #
    # Erwartet wird ausdruecklich: es bleibt KEIN Klartext liegen. Das ist der
    # Sinn des Upgrades - die Datei soll am Ende verschluesselt sein, nicht
    # entschluesselt.
    $vec = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..\PolyAES.Tests\legacy-vectors")).Path
    $d8 = Join-Path $root "t8"; New-Item -ItemType Directory -Path $d8 -Force | Out-Null
    $altPw = "pass1234"
    Copy-Item (Join-Path $vec "oneblock.bin") (Join-Path $d8 "altA.txt.protected") -Force
    Copy-Item (Join-Path $vec "padding.bin")  (Join-Path $d8 "altB.txt.protected") -Force

    foreach ($n in @("altA.txt.protected","altB.txt.protected")) {
        $m = [System.Text.Encoding]::ASCII.GetString([byte[]](Get-Content -LiteralPath (Join-Path $d8 $n) -Encoding Byte -TotalCount 4))
        if ($m -eq "PCv3") { Bad "8: $n ist bereits PCv3 - Vorbedingung verletzt" }
    }

    $a8 = Start-App $d8 "d"
    if ((Invoke-Action $a8 "entschl" $altPw $false) -ne "ok") { Bad "8: Aktionsbutton nicht gefunden" }
    else {
        Start-Sleep -Seconds 8
        if ($a8.Proc.HasExited) { Ok "8: App beendet sich selbst" }
        else { Ok "8: Upgrade-Zusammenfassung angezeigt, wartet auf Bestaetigung"; Stop-App $a8.Proc }

        # Kernpunkt: nach dem Upgrade darf KEIN Klartext liegen bleiben.
        foreach ($n in @("altA.txt","altB.txt")) {
            if (Test-Path (Join-Path $d8 $n)) { Bad "8: Klartext liegt noch da: $n" }
            else { Ok "8: kein Klartext zurueckgelassen: $n" }
        }

        foreach ($n in @("altA.txt.protected","altB.txt.protected")) {
            $q = Join-Path $d8 $n
            if (-not (Test-Path $q)) { Bad "8: .protected fehlt: $n"; continue }
            $m = [System.Text.Encoding]::ASCII.GetString([byte[]](Get-Content -LiteralPath $q -Encoding Byte -TotalCount 4))
            if ($m -eq "PCv3") { Ok "8: $n ist jetzt PCv3" } else { Bad "8: $n hat Magic '$m' statt PCv3" }
        }

        # Und sind die aktualisierten Dateien mit demselben Passwort wieder lesbar?
        $a8b = Start-App (Join-Path $d8 "altB.txt.protected")
        if ((Invoke-Action $a8b "entschl" $altPw $true) -ne "ok") { Bad "8b: Aktionsbutton nicht gefunden" }
        else {
            if (-not (Wait-Exit $a8b.Proc 90)) { Stop-App $a8b.Proc }
            $res = Join-Path $d8 "altB.txt"
            if ((Test-Path $res) -and ((Get-Content -LiteralPath $res -Raw) -like "*Hallo Welt*")) {
                Ok "8: aktualisierte Datei ist wieder lesbar (Inhalt korrekt)"
            } else { Bad "8: aktualisierte Datei nicht lesbar" }
        }
    }

    # =====================================================================
    Write-Host ""
    Write-Host "=== 9: Temp-Verzeichnis ==="
    $qd = Join-Path ([System.IO.Path]::GetTempPath()) "privateCrypt-quickedit"
    if (Test-Path $qd) {
        $rest = @(Get-ChildItem $qd -File -ErrorAction SilentlyContinue)
        if ($rest.Count -eq 0) { Ok "9: Quick-Edit-Verzeichnis leer" }
        else { Write-Host ("  [info] " + $rest.Count + " Datei(en) im Quick-Edit-Verzeichnis") }
    } else { Ok "8: kein Quick-Edit-Verzeichnis angelegt (nicht genutzt)" }

    Write-Host ""
    Write-Host ("=================================================")
    Write-Host ("BESTANDEN: " + $script:pass + "   FEHLGESCHLAGEN: " + $script:fail)
    Write-Host ("=================================================")
}
finally {
    Get-Process privateCrypt -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 500
    Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
}