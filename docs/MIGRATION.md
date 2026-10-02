# Migration der Dateien auf PCv4

Anleitung für alle, die privateCrypt 2.0 oder 3.0 benutzt haben und ihre
Dateien auf das aktuelle Format bringen wollen.

**Kurzfassung:** Es gibt nichts zu tun. Ein Rechtsklick auf den Ordner →
**Entschlüsseln** reicht. Danach steht der ganze Ordner auf PCv4.

---

## Warum überhaupt ein neues Format?

Beim Testen von Version 3.0 ist ein Fehler in der Krypto-Schicht aufgefallen,
den die damalige Dokumentation ausdrücklich als behoben beschrieben hatte.

In `PolyAES.DeriveFileKeys` wurden beide Schlüssel aus **demselben** Seed
abgeleitet:

```csharp
encKey = ComputeMasterHmac(seed);   // 32 Byte
macKey = ComputeMasterHmac(seed);   // 32 Byte — identisch
```

Das Ergebnis: **das AES-Schlüssel war bitweise gleich dem HMAC-Schlüssel.**
Beide Ableitungen ergaben denselben Wert, es gab keine Trennung — die Doku
versprach `enc`/`mac`, der Code kannte nur ein einziges Label.

Warum das relevant ist: Encrypt-then-MAC mit einem einzigen Schlüssel für
Verschlüsselung und Signatur ist genau die Konstruktion, die Kryptografie-Bausteine
auseinanderhalten sollen. Ein Fehler in einer der beiden Funktionen ist damit
nicht mehr isoliert. Für Dateien, die über ein Netzlaufwerk, einen Mailanhang oder
eine Cloud-Synchronisation wandern, ist das ein reales Risiko — der Angriffspfad
(Manipulation am Ciphertext) ist derselbe, den v3 mit der Signatur gerade schließen
wollte.

**Was NICHT betroffen ist:** Die Verschlüsselung selbst war und ist korrekt.
AES-256-CBC mit 300.000 PBKDF2-Iterationen ist nach wie vor solide. Wer seine
Dateien nur liest und niemandem sonst Zugriff darauf gibt, war nicht akut
gefährdet. Der Wechsel ist trotzdem sinnvoll, weil er die Konstruktionsregel
herstellt, auf die sich die Schutzwirkung von v3 stützt.

---

## Was ist ab wann was?

| Version | Format beim Schreiben | Liest |
|---|---|---|
| 1.0 (2011) | Rijndael-256, kein HMAC | — |
| 2.0 | `PCv2` | PCv2, 1.0 |
| 3.0 | `PCv3` | PCv3, PCv2, 1.0 |
| **4.0** | **`PCv4`** | **PCv4, PCv3, PCv2, 1.0** |

**Keine bestehende Datei wird unlesbar.** privateCrypt 4.0 öffnet alle drei
älteren Formate. Neue Dateien werden immer als PCv4 geschrieben.

---

## Die drei Wege

### Weg 1: Ordner-Entschlüsseln (empfohlen)

1. Rechtsklick auf den **Ordner** (nicht auf einzelne Dateien).
2. **Entschlüsseln (AES256)**.
3. Passwort eingeben.

Das war bisher schon der Weg für v2- und v1-Dateien. Neu ist, dass er jetzt auch
**PCv3-Dateien** erfasst.

Das Programm entschlüsselt jede Datei und verschlüsselt sie sofort wieder —
diesmal als PCv4. Am Ende:

```
Verarbeitet: 12
Übersprungen: 1
Auf PCv4 aktualisiert: 4 (1 aus PCv3, 2 aus Version 2.0, 1 aus dem Original von 2011)

Übersprungen:
  bericht.txt (Ziel existiert bereits)
```

**Es bleibt kein Klartext liegen.** Der Ordner ist am Ende genauso verschlüsselt
wie vorher — nur eben in einem besseren Format. Das ist der Sinn des Upgrades.

> **Wichtig:** Existiert zu einer Datei bereits das entschlüsselte Ziel
> (z. B. `bericht.txt` neben `bericht.txt.protected`), wird sie **übersprungen**
> und nicht verändert. privateCrypt überschreibt grundsätzlich nichts. Entweder
> den Konflikt vorher auflösen, oder die Datei einzeln nach dem Upgrade
> entschlüsseln.

**Sicherheitskopie vorher anlegen.** Der Ordnerlauf ist der einzige Vorgang, der
Daten verändert, die nicht direkt aus einer Verschlüsselung stammen — und er
überschreibt nach dem Entschlüsseln wieder dieselbe Datei. Bei Zweifeln vorher
kopieren.

### Weg 2: Einzelne Dateien nacheinander

Wenn kein Ordner vorliegt oder nur einzelne Dateien betroffen sind:

1. Datei entschlüsseln → Klartext entsteht, `.protected` verschwindet.
2. Datei sofort wieder verschlüsseln → PCv4 entsteht, Klartext wird gelöscht.

Das sind zwei Vorgänge und damit zwei Mal die Schüsselableitung (~1,5 s pro
Durchgang). Für eine Handvoll Dateien unproblematisch, für einen ganzen Ordner
nicht.

### Weg 3: Ohne Änderung an den Dateien

Verwendbar, wenn die Dateien ohnehin nur gelesen werden. Der Integritätsschutz
von PCv3 ist vorhanden — nur die Schlüsseltrennung dahinter fehlt. Siehe die
Einschätzung oben.

---

## Prüfen, ob es geklappt hat

```
privateCrypt.exe --version
```

zeigt die laufende Version und das aktive Format. Für eine einzelne Datei
genügt ein Rechtsklick → **Entschlüsseln**: die Beschriftung im Fenster nennt
das erkannte Format. `PCv3` bedeutet: noch nicht aktualisiert.

Oder per Kommandozeile, wenn eine Prüfung ohne Oberfläche gewünscht ist:

```powershell
# Liefert PCv3 oder PCv2 -> Datei ist noch nicht aktualisiert
privateCrypt.exe "C:\Daten\bericht.txt.protected"
```

---

## Sonderfälle

### Sehr große Altdateien

In Version 3.0 hat die Entschlüsselung von v2- und 2011-Dateien die **komplette
Datei in den Speicher geladen** — dreifach (Quelldatei, Puffer, Klartext). Da die
EXE 32-Bit ist, scheiterte das ab etwa 700 MB mit `OutOfMemoryException`.

In 4.0 laufen **alle** Altformate streamend. Eine 2-GB-Datei aus 2011 lässt sich
jetzt entschlüsseln. Wer diesen Fehler bisher kannte, muss nichts unternehmen —
er ist behoben.

### Dateien mit Umlauten und Leerzeichen

Unverändert, der Endungs-Test ignoriert Groß-/Kleinschreibung wie bisher.

### `.db`-Dateien

Werden beim Ordnerlauf weiterhin übersprungen. Das gilt für **beide** Richtungen,
auch beim Entschlüsseln — wer seine Datenbank im Ordner verschlüsselt hatte,
bekommt sie beim Ordner-Entschlüsseln deshalb nicht zurück. Das ist Altlast der
`.db`-Ausnahme und lässt sich nur durch Entfernen der Datei oder Verschieben
umgehen.

### Schnellansicht (Quick Edit)

Öffnet immer nur temporär und lässt das Format unverändert. Wer über die
Schnellansicht aufrüsten will, muss die Datei regulär entschlüsseln und neu
verschlüsseln.

---

## Wenn etwas nicht funktioniert

**„Entschlüsselung fehlgeschlagen: Passwort falsch oder Datei beschädigt"**

Die Meldung ist absichtlich zweideutig — sie unterscheidet nicht, ob das
Passwort falsch war oder die Datei manipuliert wurde. Diese Information wäre für
jemanden mit Zugriff auf die Datei nützlich. Beides prüfen:

- Passwort erneut eingeben (Groß-/Kleinschreibung zählt).
- Gegenprobe mit einer Datei, von der sicher bekannt ist, dass sie intakt ist.

**Nach dem Ordnerlauf sind Dateien verschwunden**

Der Ordnerlauf schließt das Fenster nach dem Abschluss. Bei Dateien mit
Konflikten steht die Liste im Zusammenfassungsfenster. Läuft das Fenster leer und
endet ohne Meldung, wurde alles verarbeitet.

**Ein Lauf wurde abgebrochen**

Bereits fertiggestellte Dateien bleiben in PCv4, die übrigen im alten Format.
Der Ordner ist damit gemischt — einfach den Lauf wiederholen. Der erneute Lauf
ist unkritisch: bereits aktualisierte Dateien werden erkannt und übersprungen,
denn sie sind PCv3 oder schon PCv4.