using System;
using System.Collections.Generic;
using System.IO;

namespace crytec.Tests
{
    /// <summary>
    /// Tests fuer die Dateisystem-Helfer. Hier sitzt der Fix fuer die
    /// case-insensitive Endungserkennung und dafuer, dass <c>Replace</c> nicht
    /// mitten im Dateinamen zuschlaegt.
    /// </summary>
    internal static class FileOpsTests
    {
        internal static void Run(TestRunner t, string dir)
        {
            t.Section("FileOps: Endungserkennung");

            // Windows-Dateisysteme sind case-insensitiv - die Erkennung muss es
            // auch sein. Sonst wuerde "RECHNUNG.PROTECTED" verschluesselt statt
            // entschluesselt.
            t.Check("IsProtected: klein", FileOps.IsProtected("C:\\a\\b.protected"));
            t.Check("IsProtected: GROSS", FileOps.IsProtected("C:\\a\\b.PROTECTED"));
            t.Check("IsProtected: gemischt", FileOps.IsProtected("C:\\a\\b.Protected"));
            t.Check("IsProtected: kein Treffer", !FileOps.IsProtected("C:\\a\\b.txt"));
            t.Check("IsProtected: nur Endung am Ende", !FileOps.IsProtected("C:\\a\\protected.txt"));
            t.Check("IsProtected: null", !FileOps.IsProtected(null));

            t.Section("FileOps: Endung entfernen");

            t.Equal("einfacher Name", "C:\\a\\b.txt", FileOps.StripProtected("C:\\a\\b.txt.protected"));
            t.Equal("Grosse Endung", "C:\\a\\b.txt", FileOps.StripProtected("C:\\a\\b.txt.PROTECTED"));
            t.Equal("ohne Endung unveraendert", "C:\\a\\b.txt", FileOps.StripProtected("C:\\a\\b.txt"));

            // Der eigentliche Bug: Replace() haette auch den Inneren Treffer
            // mitgenommen und "archiv.protected.snapshot" zu "archiv.snapshot"
            // gemacht.
            t.Equal("Endung nur am Ende entfernt",
                    "C:\\a\\archiv.protected.snapshot.txt",
                    FileOps.StripProtected("C:\\a\\archiv.protected.snapshot.txt.protected"));

            // Kein leerer Dateiname, sonst waere das Ziel der Ordner selbst.
            t.Check("Dateiname nur aus Endung ergibt brauchbaren Namen",
                    Path.GetFileName(FileOps.StripProtected("C:\\a\\.protected")).Length > 0);

            t.Section("FileOps: Zielpfad");

            t.Equal("Ziel beim Verschluesseln", "C:\\a\\b.txt.protected", FileOps.TargetFor("C:\\a\\b.txt", true));
            t.Equal("Ziel beim Entschluesseln", "C:\\a\\b.txt", FileOps.TargetFor("C:\\a\\b.txt.protected", false));

            t.Section("FileOps: Ordner sammeln");

            string root = Path.Combine(dir, "scan");
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(Path.Combine(root, "unterordner"));
            Directory.CreateDirectory(Path.Combine(root, "leer"));

            File.WriteAllText(Path.Combine(root, "a.txt"), "a");
            File.WriteAllText(Path.Combine(root, "b.protected"), "b");
            File.WriteAllText(Path.Combine(root, "index.db"), "sqlite");
            File.WriteAllText(Path.Combine(root, "unterordner", "d.txt"), "d");

            List<string> files = FileOps.CollectFiles(root);

            t.Equal("Anzahl gesammelter Dateien", 3, files.Count);
            t.Check("a.txt gefunden", files.Exists(f => f.EndsWith("a.txt", StringComparison.Ordinal)));
            t.Check("b.protected gefunden", files.Exists(f => f.EndsWith("b.protected", StringComparison.Ordinal)));
            t.Check("d.txt im Unterordner gefunden", files.Exists(f => f.EndsWith("d.txt", StringComparison.Ordinal)));
            t.Check(".db uebersprungen", !files.Exists(f => f.EndsWith(".db", StringComparison.OrdinalIgnoreCase)));

            // Junction / Symbolischer Link: der Lauf darf nicht in eine Zykle geraten.
            string linkTarget = Path.Combine(dir, "linktarget");
            Directory.CreateDirectory(linkTarget);
            File.WriteAllText(Path.Combine(linkTarget, "x.txt"), "x");

            bool junctionMade = false;
            try
            {
                // mklink /J braucht keine Administratorrechte.
                var psi = new System.Diagnostics.ProcessStartInfo("cmd.exe",
                    "/c mklink /J \"" + Path.Combine(root, "zyklus") + "\" \"" + linkTarget + "\"");
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                using (var proc = System.Diagnostics.Process.Start(psi))
                {
                    proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit();
                    junctionMade = proc.ExitCode == 0 && Directory.Exists(Path.Combine(root, "zyklus"));
                }
            }
            catch { junctionMade = false; }

            if (junctionMade)
            {
                List<string> withLink = FileOps.CollectFiles(root);
                t.Check("Verzeichnisverknuepfung wird nicht verfolgt (keine Zykle)",
                        withLink.Count == files.Count,
                        "erwartet " + files.Count + ", war " + withLink.Count);
                t.Check("keine Datei ausserhalb des Stammbaums",
                        !withLink.Exists(f => f.IndexOf("linktarget", StringComparison.OrdinalIgnoreCase) >= 0));
            }
            else
            {
                Console.WriteLine("  [info] Junction konnte nicht angelegt werden - Test uebersprungen");
            }

            t.Section("FileOps: relative Pfade");

            t.Equal("Pfad relativ zum Stamm", "unterordner\\d.txt",
                    FileOps.MakeRelative(Path.Combine(root, "unterordner", "d.txt"), root));
            t.Equal("ausserhalb faellt auf Dateinamen zurueck", "anders.txt",
                    FileOps.MakeRelative("C:\\woanders\\anders.txt", root));
        }
    }
}