using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Threading;
using Microsoft.Win32;

namespace crytec
{
    /// <summary>
    /// Dateisystem-Helfer fuer den Explorer-Kontext. Bewusst frei von
    /// Oberflaechenabhaengigkeiten, damit sie einzeln getestet werden koennen.
    /// </summary>
    internal static class FileOps
    {
        /// <summary>Endung verschluesselter Dateien.</summary>
        public const string ProtectedExtension = ".protected";

        /// <summary>
        /// Dateiendungen, die beim Ordnerlauf uebersprungen werden - typischerweise
        /// Indizes von Programmen, die beim Schreiben gerade geoeffnet sind.
        /// </summary>
        private static readonly string[] SkipExtensions = { ".db" };

        /// <summary>Erkennt die Endung case-insensitiv (Windows-Dateisystem ist so).</summary>
        public static bool IsProtected(string path)
        {
            return path != null
                && path.EndsWith(ProtectedExtension, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Entfernt die Endung - exakt am Dateiende. Bewusst kein <c>Replace</c>:
        /// das wuerde auch mitten im Namen zuschlagen ("a.protected.protected").
        /// </summary>
        public static string StripProtected(string path)
        {
            if (!IsProtected(path))
                return path;

            string dir = Path.GetDirectoryName(path) ?? string.Empty;
            string name = Path.GetFileName(path);
            name = name.Substring(0, name.Length - ProtectedExtension.Length);

            // Ein Dateiname, der nur aus der Endung besteht, ist sinnlos -
            // dann waere das Ziel der Ordner selbst.
            if (name.Length == 0)
                name = "entschluesselt";

            return Path.Combine(dir, name);
        }

        /// <summary>Liefert den Zieldateinamen fuer eine Operation.</summary>
        public static string TargetFor(string sourcePath, bool encrypt)
        {
            return encrypt ? sourcePath + ProtectedExtension : StripProtected(sourcePath);
        }

        /// <summary>
        /// Sammelt Dateien rekursiv. Verzeichnisverknuepfungen (Junctions, Symbolic
        /// Links) werden ausgelassen, weil sie zu Zyklen fuehren koennen - der
        /// klassische Fall ist "Application Data".
        /// </summary>
        public static List<string> CollectFiles(string root)
        {
            var result = new List<string>();
            CollectFilesRecursive(root, result, 0);
            return result;
        }

        private static void CollectFilesRecursive(string path, List<string> result, int depth)
        {
            // Schutz gegen absurd tief verschachtelte Strukturen.
            if (depth > 128)
                return;

            // Ein einzelnes unlesbares Verzeichnis darf den ganzen Lauf nicht
            // abbrechen - typisch: geschuetzte Systemordner unter %LocalAppData%.
            //
            // Abgefangen werden alle drei Exception-Typen, die die BCL hier nennen
            // kann: UnauthorizedAccessException (Rechte), DirectoryNotFoundException
            // (wurde waehrend des Laufs geloescht) und IOException (z. B. ein
            // unterbrechbares Volume, ein Netzlaufwerk mit Zeitueberschreitung
            // oder ein Pfad, der zum Zeitpunkt des Aufrufs zu lang war).
            // Ohne den IOException-Fall bricht ein einziger solcher Ordner den
            // kompletten Lauf ab - bei einem Lauf ueber den Benutzerordner kann
            // das jederzeit ein hängendes Netzlaufwerk ausloesen.
            string[] files;
            try
            {
                files = Directory.GetFiles(path);
            }
            catch (UnauthorizedAccessException)
            {
                files = new string[0];
            }
            catch (DirectoryNotFoundException)
            {
                files = new string[0];
            }
            catch (IOException)
            {
                files = new string[0];
            }

            foreach (string file in files)
            {
                if (!HasSkippedExtension(file))
                    result.Add(file);
            }

            string[] dirs;
            try
            {
                dirs = Directory.GetDirectories(path);
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }
            catch (DirectoryNotFoundException)
            {
                return;
            }
            catch (IOException)
            {
                return;
            }

            foreach (string dir in dirs)
            {
                if (IsReparsePoint(dir))
                    continue;

                CollectFilesRecursive(dir, result, depth + 1);
            }
        }

        private static bool HasSkippedExtension(string path)
        {
            foreach (string ext in SkipExtensions)
            {
                if (path.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static bool IsReparsePoint(string path)
        {
            try
            {
                return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
            }
            catch (UnauthorizedAccessException)
            {
                return true; // lieber ueberspringen als abbrechen
            }
            catch (IOException)
            {
                return true;
            }
        }

        /// <summary>
        /// Ueberschreibt den Inhalt mit Nullen und loescht die Datei danach.
        ///
        /// <para>Achtung: Auf SSDs mit Wear-Leveling, bei virtuellen Platten und in
        /// verschluesselten Containern kann eine vollstaendige physische Loeschung nicht
        /// garantiert werden. Das ist eine Eigenschaft des Datentraegers und nicht des
        /// Programms - der Null-Schritt hebt nur die einfache Wiederherstellung
        /// ueber Dateisystem-Tools auf.</para>
        /// </summary>
        public static void SecureDelete(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;

            try
            {
                long length = new FileInfo(path).Length;
                if (length > 0)
                {
                    using (var fs = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None))
                    {
                        byte[] zeros = new byte[(int)Math.Min(length, 65536)];
                        long written = 0;
                        while (written < length)
                        {
                            int chunk = (int)Math.Min(zeros.Length, length - written);
                            fs.Write(zeros, 0, chunk);
                            written += chunk;
                        }

                        // Bis auf den Datentraeger schreiben, nicht nur in den
                        // CLR-Puffer.
                        fs.Flush(true);
                    }
                }

                File.Delete(path);
            }
            catch
            {
                // Wenigstens loeschen, wenn das Ueberschreiben nicht gelingt.
                try { File.Delete(path); } catch { }
            }
        }

        /// <summary>
        /// Loescht <paramref name="path"/> und wartet kurz, falls das Programm die
        /// Datei noch offen haelt. Gibt <c>true</c> zurueck, wenn die Datei
        /// verschwunden ist.
        /// </summary>
        public static bool SecureDeleteWhenUnlocked(string path, int maxAttempts = 30, int delayMs = 250)
        {
            for (int attempt = 0; attempt <= maxAttempts; attempt++)
            {
                if (!File.Exists(path))
                    return true;

                SecureDelete(path);

                if (!File.Exists(path))
                    return true;

                if (attempt < maxAttempts)
                    System.Threading.Thread.Sleep(delayMs);
            }

            return !File.Exists(path);
        }

        /// <summary>
        /// Prueft, ob ein gestarteter Betrachter ueberwacht werden kann.
        ///
        /// <para>Wartet hoechstens <paramref name="graceMs"/> Millisekunden: Laeuft der
        /// Prozess danach noch, gilt er als ueberwachbar (<c>true</c>) und der Aufrufer
        /// kann auf sein Ende warten. Endet er innerhalb der Karenzzeit oder gibt es
        /// gar keinen Prozess (<c>null</c>, z. B. ShellExecute ohne Handle), kommt
        /// <c>false</c> zurueck - dann darf die geoeffnete Datei noch nicht geloescht
        /// werden, weil der Betrachter (etwa die Fotos-App, deren Starter-Stub sofort
        /// endet) sie moeglicherweise gerade erst laedt.</para>
        /// </summary>
        internal static bool IsViewerTrackable(Process viewer, int graceMs, CancellationToken token)
        {
            if (viewer == null)
                return false;

            try
            {
                if (viewer.WaitForExit(graceMs))
                    return false;

                token.ThrowIfCancellationRequested();
                return true;
            }
            catch (InvalidOperationException)
            {
                // Kein Prozess-Handle (ShellExecute ohne neues Prozessobjekt).
                return false;
            }
        }

        /// <summary>
        /// Prueft konservativ, ob Windows die Endung einer App zuordnen kann.
        ///
        /// <para>Gibt <c>true</c> nur bei positivem Nachweis zurueck (ProgId mit
        /// Oeffnen-Befehl, Benutzerwahl oder SystemFileAssociations). UWP-Apps
        /// wie Fotos registrieren keinen klassischen Befehl, oeffnen aber
        /// trotzdem - deshalb zaehlt auch ein blosser
        /// <c>shell\open</c>-Schluessel. Im Zweifel kommt <c>false</c>: Der
        /// Aufrufer wartet dann auf "Fertig", statt die Temporaerdatei
        /// moeglicherweise zu frueh zu loeschen (z. B. waehrend noch die
        /// "Oeffnen mit"-Auswahl offen ist).</para>
        /// </summary>
        internal static bool HasAssociatedApp(string extension)
        {
            if (string.IsNullOrEmpty(extension))
                return false;

            try
            {
                using (RegistryKey ext = Registry.ClassesRoot.OpenSubKey(extension))
                {
                    string progId = ext == null ? null : ext.GetValue(null) as string;
                    if (!string.IsNullOrEmpty(progId))
                    {
                        using (RegistryKey command = Registry.ClassesRoot.OpenSubKey(progId + @"\shell\open\command"))
                            if (command != null)
                                return true;

                        using (RegistryKey open = Registry.ClassesRoot.OpenSubKey(progId + @"\shell\open"))
                            if (open != null)
                                return true;
                    }
                }

                using (RegistryKey choice = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\" + extension + @"\UserChoice"))
                {
                    string chosen = choice == null ? null : choice.GetValue("ProgId") as string;
                    if (!string.IsNullOrEmpty(chosen))
                        return true;
                }

                using (RegistryKey sys = Registry.ClassesRoot.OpenSubKey(
                    @"SystemFileAssociations\" + extension + @"\shell\open\command"))
                {
                    if (sys != null)
                        return true;
                }
            }
            catch
            {
                // Ohne Auskunft gilt: keine Zuordnung - der sichere Weg.
            }

            return false;
        }

        /// <summary>Macht aus einem absoluten Pfad einen Pfad relativ zu einem Wurzelordner.</summary>
        public static string MakeRelative(string path, string root)
        {
            try
            {
                string prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                + Path.DirectorySeparatorChar;
                if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return path.Substring(prefix.Length);
            }
            catch (ArgumentException)
            {
                // Ungueltiger Pfad - auf den Dateinamen zurueckfallen.
            }

            return Path.GetFileName(path);
        }
    }
}