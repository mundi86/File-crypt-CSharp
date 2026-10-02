using System;
using System.Collections.Generic;
using System.IO;

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