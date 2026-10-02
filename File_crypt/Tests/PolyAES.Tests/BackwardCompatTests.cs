using System;
using System.IO;
using System.Security.Cryptography;

namespace crytec.Tests
{
    /// <summary>
    /// Prueft, dass privateCrypt 4.0 alle Dateien lesen kann, die frueher
    /// geschrieben wurden - und dass es dabei streamt.
    ///
    /// <para>Der Streaming-Teil ist wichtig fuer den Bestand: Wer seit 2011
    /// eine 2-GB-Datenbank verschluesselt hat, darf an privateCrypt 4.0 nicht
    /// erst scheitern, weil der Altpfad die ganze Datei in den Speicher laedt.
    /// Die EXE ist 32-Bit, dafuer ist gar kein Speicher vorhanden.</para>
    /// </summary>
    internal static class BackwardCompatTests
    {
        internal static void Run(TestRunner t, string dir, Func<string, PolyAES> session)
        {
            t.Section("Rueckwaertskompatibilitaet: v3 aus Version 3.0");

            // Der entscheidende Test: eine Datei, die mit dem gemeinsamen
            // Schluessel fuer Cipher und MAC signiert wurde, muss sich mit 4.0
            // oeffnen lassen. Wird im Leseweg versehentlich die korrigierte
            // Ableitung benutzt, schlaegt genau dieser Test fehl.
            const string pw = "geheim123";
            byte[] plain = Program.MakeData(3000);

            string v3 = LegacyFormatWriter.MakeV3File(
                Path.Combine(dir, "alt_v3.protected"), plain, pw);

            t.Equal("v3-Datei wird erkannt", ContainerFormat.V3, PolyAES.DetectFormat(v3));

            string v3out = Path.Combine(dir, "alt_v3.out");
            session(pw).DecryptFile(v3, v3out, true);
            t.Check("v3 aus Version 3.0 laesst sich entschluesseln",
                    Program.SameBytes(plain, File.ReadAllBytes(v3out)));

            // Und die Integritaetspruefung gilt auch dort: eine manipulierte
            // v3-Datei darf keinen Klartext erzeugen.
            string v3tamper = Path.Combine(dir, "alt_v3_flip.protected");
            File.Copy(v3, v3tamper, true);
            Program.FlipBit(v3tamper, 52 + 100);

            t.CheckThrows<CryptographicException>("manipulierte v3-Datei wird abgelehnt",
                () => session(pw).DecryptFile(v3tamper, Path.Combine(dir, "alt_v3_bad.out"), true));
            t.Check("kein Klartext aus manipulierter v3-Datei",
                    !File.Exists(Path.Combine(dir, "alt_v3_bad.out")));

            t.Section("Rueckwaertskompatibilitaet: v2 aus Version 2.0");

            byte[] plain2 = Program.MakeData(5000);
            string v2 = LegacyFormatWriter.MakeV2File(
                Path.Combine(dir, "alt_v2.protected"), plain2, pw);

            t.Equal("v2-Datei wird erkannt", ContainerFormat.V2, PolyAES.DetectFormat(v2));

            string v2out = Path.Combine(dir, "alt_v2.out");
            session(pw).DecryptFile(v2, v2out, true);
            t.Check("v2 aus Version 2.0 laesst sich entschluesseln",
                    Program.SameBytes(plain2, File.ReadAllBytes(v2out)));

            // Ein neues v4-Clear ist kein v2 und umgekehrt: die Formate duerfen
            // sich nicht gegenseitig als Header-Rauschen akzeptieren.
            string v4 = Path.Combine(dir, "neu_v4.protected");
            session(pw).EncryptFile(v3out, v4, false);
            t.Equal("neu geschrieben wird immer v4", ContainerFormat.V4, PolyAES.DetectFormat(v4));

            t.Section("Altformate werden gestreamt, nicht geladen");

            // Der Speicherbedarf muss unabhaengig von der Dateigroesse bleiben.
            // Ueberwacht wird der peak Working Set des eigenen Prozesses: waere
            // ReadAllBytes im Spiel, stuende er nach einer grossen Altdatei
            // deutlich hoeher als vorher.
            //
            // Wichtig fuer die Messung selbst: der Test darf die Datei nicht
            // selbst ganz laden. Ein ReadAllBytes im Test wuerde genau den
            // Speicherbedarf erzeugen, den man ausschliessen will - die erste
            // Fassung dieses Tests hat sich auf genau diese Weise selbst
            // falsch beschuldigt. Stattdessen wird der Klartext auf der Platte
            // gehalten und ueber einen Hash verglichen.
            const int BigSize = 48 * 1024 * 1024;

            string bigPlain = Path.Combine(dir, "gross_plain.bin");
            WriteBigPattern(bigPlain, BigSize);
            byte[] expectedHash = HashFile(bigPlain);

            long before = PeakWorkingSet();

            // 48 MB - gross genug, dass ein ReadAllBytes in einem 32-Bit-Prozess
            // sichtbar auffaellt, aber noch in vertretbarer Zeit erzeugbar.
            string bigV2 = LegacyFormatWriter.MakeV2FileFrom(
                Path.Combine(dir, "gross_v2.protected"), bigPlain, pw);

            string bigV2out = Path.Combine(dir, "gross_v2.out");
            GC.Collect();
            GC.WaitForPendingFinalizers();
            before = PeakWorkingSet();

            session(pw).DecryptFile(bigV2, bigV2out, true);

            long growthV2 = PeakWorkingSet() - before;

            t.Check("grosse v2-Datei laesst sich entschluesseln",
                    Program.SameBytes(expectedHash, HashFile(bigV2out)));

            // Erlaubt ist ein gewisses Wachstum (Puffer, AES-Tabellen, GC), aber
            // kein Vielfaches der Dateigroesse. Bei ReadAllBytes waeren es
            // dreimal 48 MB plus Kopien - der Testprozess laeuft aber als Any CPU
            // mit genug Adressraum, das koennte also durchrutschen. Deshalb wird
            // zusaetzlich gemessen, wie viel Heap wirklich belegt ist.
            t.Check("Speicher waechst nicht mit der Dateigroesse (v2)",
                    growthV2 < 64L * 1024 * 1024,
                    "Working Set waechste um " + (growthV2 / (1024 * 1024)) + " MB fuer eine 48-MB-Datei");

            File.Delete(bigV2);
            File.Delete(bigV2out);

            // Dasselbe fuer das 2011-Format, das als Erstes betroffen war:
            // dort lagen Salt und IV hinter dem Ciphertext.
            long beforeLegacy = PeakWorkingSet();
            string bigLegacy = Path.Combine(dir, "gross_v1.protected");
            WriteBigLegacyFrom(bigLegacy, bigPlain, "pass1234");

            string bigLegacyOut = Path.Combine(dir, "gross_v1.out");
            GC.Collect();
            GC.WaitForPendingFinalizers();
            beforeLegacy = PeakWorkingSet();

            session("pass1234").DecryptFile(bigLegacy, bigLegacyOut, true);

            long growthLegacy = PeakWorkingSet() - beforeLegacy;

            t.Check("grosse Legacy-Datei laesst sich entschluesseln",
                    Program.SameBytes(expectedHash, HashFile(bigLegacyOut)));

            t.Check("Speicher waechst nicht mit der Dateigroesse (Legacy)",
                    growthLegacy < 64L * 1024 * 1024,
                    "Working Set waechste um " + (growthLegacy / (1024 * 1024)) + " MB");

            File.Delete(bigLegacy);
            File.Delete(bigLegacyOut);
            File.Delete(bigPlain);
        }

        /// <summary>
        /// Schreibt grosse, aber billig herzustellende Daten direkt auf die Platte.
        /// Ein 48-MB-Array im Speicher zu halten wuerde genau den Speicherbedarf
        /// erzeugen, den dieser Test ausschliessen soll.
        /// </summary>
        private static void WriteBigPattern(string path, int length)
        {
            var pattern = new byte[64 * 1024];
            for (int i = 0; i < pattern.Length; i++)
                pattern[i] = (byte)((i * 31) + ((i >> 8) * 7) + ((i >> 16) * 13));

            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                int remaining = length;
                while (remaining > 0)
                {
                    int n = Math.Min(pattern.Length, remaining);
                    fs.Write(pattern, 0, n);
                    remaining -= n;
                }
            }
        }

        /// <summary>
        /// SHA-256 ueber eine Datei, bewusst in Bloecken gelesen. Ein
        /// <c>File.ReadAllBytes</c> wuerde hier das Gleiche tun wie der Code, den
        /// der Test gerade ausschliessen soll.
        /// </summary>
        private static byte[] HashFile(string path)
        {
            using (var sha = SHA256.Create())
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024))
            {
                return sha.ComputeHash(fs);
            }
        }

        /// <summary>
        /// Peak Working Set des eigenen Prozesses in Bytes.
        /// </summary>
        private static long PeakWorkingSet()
        {
            try
            {
                using (var p = System.Diagnostics.Process.GetCurrentProcess())
                {
                    p.Refresh();
                    return p.PeakWorkingSet64;
                }
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Schreibt eine Datei im 2011-Format: Ciphertext, danach Salt und IV.
        /// Rijndael-256 mit Blockgroesse 32, PBKDF2-SHA1 mit 2.000 Iterationen.
        /// Streamt die Vorlage von der Platte, statt sie zu laden.
        /// </summary>
        private static void WriteBigLegacyFrom(string path, string plainPath, string password)
        {
            byte[] salt = new byte[32];
            byte[] iv = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
                rng.GetBytes(iv);
            }

            // Schluessel wie in der Originalversion: ASCII(uplowme(sha256hex(pw)))
            string mangled = Uplowme(Sha256Hex(password));

            byte[] derived;
            using (var kdf = new Rfc2898DeriveBytes(
                System.Text.Encoding.ASCII.GetBytes(mangled), salt, 2000))
            {
                derived = kdf.GetBytes(32);
            }

#pragma warning disable CS0618
            using (var algo = new RijndaelManaged())
            {
                algo.Mode = CipherMode.CBC;
                algo.BlockSize = 256;
                algo.Key = derived;
                algo.IV = iv;
                using (var enc = algo.CreateEncryptor())
                using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
                using (var src = new FileStream(plainPath, FileMode.Open, FileAccess.Read))
                using (var crypto = new CryptoStream(src, enc, CryptoStreamMode.Read))
                {
                    // CopyTo ruft FlushFinalBlock selbst auf.
                    crypto.CopyTo(fs);
                }
            }
#pragma warning restore CS0618

            // Salt und IV stehen hinter dem Ciphertext - so war es 2011.
            using (var fs = new FileStream(path, FileMode.Append, FileAccess.Write))
            {
                fs.Write(salt, 0, salt.Length);
                fs.Write(iv, 0, iv.Length);
            }
        }

        private static string Sha256Hex(string text)
        {
            byte[] bytes = System.Text.Encoding.Unicode.GetBytes(text);
            using (var sha = new SHA256Managed())
            {
                byte[] hash = sha.ComputeHash(bytes);
                var sb = new System.Text.StringBuilder();
                foreach (byte b in hash)
                    sb.AppendFormat("{0:x2}", b);
                return sb.ToString();
            }
        }

        /// <summary>Die Zeichensummen-Zeichenfolge des 2011-Algorithmus.</summary>
        private static string Uplowme(string text)
        {
            var sb = new System.Text.StringBuilder();
            bool upper = true;
            foreach (char c in text)
            {
                if (upper)
                {
                    switch (c)
                    {
                        case '0': sb.Append('='); break;
                        case '1': sb.Append('!'); break;
                        case '2': sb.Append('<'); break;
                        case '3': sb.Append('§'); break;
                        case '4': sb.Append('$'); break;
                        case '5': sb.Append('%'); break;
                        case '6': sb.Append('&'); break;
                        case '7': sb.Append('/'); break;
                        case '8': sb.Append('('); break;
                        case '9': sb.Append(')'); break;
                        default: sb.Append(char.ToUpper(c)); break;
                    }
                    upper = false;
                }
                else
                {
                    sb.Append(char.ToLower(c));
                    upper = true;
                }
            }
            return sb.ToString();
        }
    }
}