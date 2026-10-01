using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace crytec.Tests
{
    /// <summary>
    /// Rueckwaertskompatibilitaet: Dateien, die mit dem Originalprogramm von 2011
    /// verschluesselt wurden, muessen weiterhin lesbar sein.
    ///
    /// Die Vektoren in legacy-vectors/ wurden mit einer vom Produktivcode unabhaengigen
    /// Implementierung des Originalalgorithmus erzeugt. Damit schlaegt dieser Test an,
    /// falls die Legacy-Entschluesselung kaputtgeht.
    /// </summary>
    internal static class LegacyVectorTests
    {
        internal static void Run(TestRunner t)
        {
            t.Section("Legacy (v1 von 2011) Rueckwaertskompatibilitaet");

            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "legacy-vectors");
            string indexPath = Path.Combine(dir, "index.txt");

            if (!File.Exists(indexPath))
            {
                t.Check("legacy-vectors/index.txt gefunden", false, "Datei fehlt: " + indexPath);
                return;
            }

            string[] lines = File.ReadAllLines(indexPath);
            int vectorCount = 0;

            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;

                // name|passwordHex(utf16le)|datei|sha256|blobLaenge|klartextLaenge
                string[] f = line.Split('|');
                if (f.Length != 6) continue;

                string name = f[0];
                string password = DecodeUtf16Hex(f[1]);
                string file = Path.Combine(dir, f[2]);
                string expectedSha = f[3];
                long blobLen = long.Parse(f[4]);
                long plainLen = long.Parse(f[5]);
                vectorCount++;

                if (!File.Exists(file))
                {
                    t.Check("Vektor " + name + " vorhanden", false, "fehlt: " + file);
                    continue;
                }

                byte[] blob = File.ReadAllBytes(file);

                // Die Testdaten selbst muessen unveraendert sein.
                t.Check("Vektor " + name + " unveraendert",
                        Program.Hex(Sha256(blob)) == expectedSha,
                        "Pruefsumme der Testdatei stimmt nicht");
                t.Check("Vektor " + name + " Laenge", blob.LongLength == blobLen,
                        "erwartet " + blobLen + ", war " + blob.LongLength);

                string outPath = Path.Combine(Path.GetTempPath(),
                    "pc-legacy-" + name + "-" + Guid.NewGuid().ToString("N").Substring(0, 6) + ".out");

                try
                {
                    Program.Session(password).DecryptFile(file, outPath, true);

                    byte[] plain = File.ReadAllBytes(outPath);
                    t.Check("Vektor " + name + " entschluesselt (" + plainLen + " Byte)",
                            plain.LongLength == plainLen,
                            "erwartet " + plainLen + " Byte, war " + plain.Length);
                    Console.WriteLine("         Passwort-Bytes: " + password.Length + " Zeichen, Klartext: " +
                                      (plainLen == 0 ? "(leer)" : Encoding.UTF8.GetString(plain)));
                }
                catch (Exception ex)
                {
                    t.Check("Vektor " + name + " entschluesselt", false,
                            ex.GetType().Name + ": " + ex.Message);
                }
                finally
                {
                    try { if (File.Exists(outPath)) File.Delete(outPath); } catch { }
                }
            }

            t.Check("alle Legacy-Vektoren ausgefuehrt", vectorCount >= 6,
                    "nur " + vectorCount + " Vektoren gefunden");

            // Falsches Passwort muss auch im Legacy-Pfad scheitern.
            string v = Path.Combine(dir, "padding.bin");
            if (File.Exists(v))
            {
                string outPath = Path.Combine(Path.GetTempPath(), "pc-legacy-wrong-" + Guid.NewGuid().ToString("N").Substring(0, 6));
                t.CheckThrows<CryptographicException>("Legacy: falsches Passwort wird abgelehnt",
                    () => Program.Session("voelligFalsch123").DecryptFile(v, outPath, true));
                try { if (File.Exists(outPath)) File.Delete(outPath); } catch { }
            }

            // Zu kurze / kaputte Datei -> verstaendliche Fehlermeldung, kein Absturz
            string junk = Path.Combine(Path.GetTempPath(), "pc-legacy-junk-" + Guid.NewGuid().ToString("N").Substring(0, 6));
            File.WriteAllBytes(junk, Program.MakeData(20));
            t.CheckThrows<CryptographicException>("Legacy: zu kurze Datei ergibt CryptographicException",
                () => Program.Session("egal").DecryptFile(junk, junk + ".out", true));
            try { File.Delete(junk); } catch { }

            string odd = Path.Combine(Path.GetTempPath(), "pc-legacy-odd-" + Guid.NewGuid().ToString("N").Substring(0, 6));
            File.WriteAllBytes(odd, Program.MakeData(101)); // ungerade Blockgroesse
            t.CheckThrows<CryptographicException>("Legacy: ungerade Blockgroesse ergibt CryptographicException",
                () => Program.Session("egal").DecryptFile(odd, odd + ".out", true));
            try { File.Delete(odd); } catch { }
        }

        private static string DecodeUtf16Hex(string hex)
        {
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return Encoding.Unicode.GetString(bytes);
        }

        private static byte[] Sha256(byte[] data)
        {
            using (var sha = SHA256.Create())
                return sha.ComputeHash(data);
        }
    }
}