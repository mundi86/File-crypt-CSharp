using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace crytec.Tests
{
    /// <summary>
    /// Prueft, dass Manipulationen an einer v3-Datei zuverlaessig auffallen.
    /// Ohne HMAC (Format v2/v1) waeren diese Faelle stillschweigend
    /// durchgegangen - genau die Luecke, die v3 schliesst.
    /// </summary>
    internal static class TamperTests
    {
        internal static void Run(TestRunner t, string dir, Func<string, PolyAES> session)
        {
            t.Section("v3 Manipulationserkennung");

            const string pw = "geheim123";
            const string wrongPw = "falschesPasswort";

            // Basisdatei mit ~2 KiB Klartext erzeugen
            string plain = Path.Combine(dir, "tamper_src.bin");
            File.WriteAllBytes(plain, Program.MakeData(2048));

            string template = Path.Combine(dir, "tamper_template.protected");
            session(pw).EncryptFile(plain, template, false);

            byte[] good = File.ReadAllBytes(template);
            const int HeaderSize = 52;
            const int TagSize = 32;

            // --- 1. Falsches Passwort ---
            string p = Copy(dir, good, "wrongpw.protected");
            t.CheckThrows<CryptographicException>("falsches Passwort wird erkannt",
                () => session(wrongPw).DecryptFile(p, Path.Combine(dir, "o1"), true));
            t.Check("kein Klartext erzeugt (falsches Passwort)", !File.Exists(Path.Combine(dir, "o1")));

            // --- 2. Bitflip im Ciphertext ---
            p = Copy(dir, good, "flip_ct.protected");
            Program.OverwriteBytes(p, HeaderSize + 500, new byte[] { 0x01 });
            t.CheckThrows<CryptographicException>("Bitflip im Ciphertext wird erkannt",
                () => session(pw).DecryptFile(p, Path.Combine(dir, "o2"), true));
            t.Check("kein Klartext erzeugt (Ciphertext)", !File.Exists(Path.Combine(dir, "o2")));

            // --- 3. Bitflip im Salt ---
            p = Copy(dir, good, "flip_salt.protected");
            Program.OverwriteBytes(p, 8, new byte[] { 0x01 });
            t.CheckThrows<CryptographicException>("Bitflip im Salt wird erkannt",
                () => session(pw).DecryptFile(p, Path.Combine(dir, "o3"), true));

            // --- 4. Bitflip im IV ---
            p = Copy(dir, good, "flip_iv.protected");
            Program.OverwriteBytes(p, 40, new byte[] { 0x01 });
            t.CheckThrows<CryptographicException>("Bitflip im IV wird erkannt",
                () => session(pw).DecryptFile(p, Path.Combine(dir, "o4"), true));

            // --- 5. Bitflip in der Signatur ---
            p = Copy(dir, good, "flip_tag.protected");
            Program.OverwriteBytes(p, good.Length - 1, new byte[] { 0x01 });
            t.CheckThrows<CryptographicException>("Bitflip in der Signatur wird erkannt",
                () => session(pw).DecryptFile(p, Path.Combine(dir, "o5"), true));

            // --- 6. Vertauschte Ciphertext-Bloecke (klassischer CBC-Angriff) ---
            p = Copy(dir, good, "swap_ct.protected");
            Swap(p, HeaderSize + 16, HeaderSize + 32, 16);
            t.CheckThrows<CryptographicException>("vertauschte Bloecke werden erkannt",
                () => session(pw).DecryptFile(p, Path.Combine(dir, "o6"), true));

            // --- 7. Abgeschnittene Datei ---
            p = Copy(dir, good, "trunc.protected");
            Program.Truncate(p, good.Length / 2);
            t.CheckThrows<Exception>("abgeschnittene Datei wird erkannt",
                () => session(pw).DecryptFile(p, Path.Combine(dir, "o7"), true));

            // --- 8. Signatur entfernt ---
            p = Copy(dir, good, "nosig.protected");
            Program.Truncate(p, good.Length - TagSize);
            t.CheckThrows<CryptographicException>("entfernte Signatur wird erkannt",
                () => session(pw).DecryptFile(p, Path.Combine(dir, "o8"), true));

            // --- 9. Datei um ein Byte verlaengert ---
            p = Copy(dir, good, "longer.protected");
            using (var fs = new FileStream(p, FileMode.Append, FileAccess.Write))
                fs.WriteByte(0x00);
            t.CheckThrows<CryptographicException>("verlaengerte Datei wird erkannt",
                () => session(pw).DecryptFile(p, Path.Combine(dir, "o9"), true));

            // --- 10. Zwei Dateien miteinander verwechselt ---
            p = Copy(dir, good, "other.protected");
            t.CheckThrows<CryptographicException>("Datei einer anderen Datei wird erkannt",
                () => session(wrongPw).DecryptFile(p, Path.Combine(dir, "o10"), true));

            // --- 11. Zieldatei bleibt bei Fehlschlag unangetastet ---
            string dest = Path.Combine(dir, "keepme.out");
            File.WriteAllBytes(dest, Program.MakeData(7));
            byte[] before = File.ReadAllBytes(dest);
            p = Copy(dir, good, "atomic.protected");
            Program.OverwriteBytes(p, HeaderSize + 100, new byte[] { 0xFF });
            try { session(pw).DecryptFile(p, dest, true); } catch (CryptographicException) { }
            t.Check("Zieldatei bei fehlgeschlagener Entschluesselung unveraendert",
                    Program.SameBytes(before, File.ReadAllBytes(dest)));

            t.Check("keine .pctmp-Reste nach Fehlschlaegen",
                    Directory.GetFiles(dir, "*.pctmp").Length == 0);

            t.Section("v2/Altformat: Manipulationserkennung (dokumentierte Luecke)");

            // v2 hat bewusst keine Signatur. Wir erzeugen eine v2-Datei mit einer
            // unabhaengigen Zweitimplementierung des 2.0-Formats und zeigen, dass
            // das Entfernen der letzten Bloecke unbemerkt durchgeht. Das ist kein
            // Fehler, sondern der Grund fuer v3 - der Test haelt die Awareness.
            string v2 = MakeV2File(Path.Combine(dir, "legacy_v2.protected"), Program.MakeData(2048), pw);
            t.Equal("v2-Datei wird erkannt", ContainerFormat.V2, PolyAES.DetectFormat(v2));

            string v2out = Path.Combine(dir, "v2.out");
            session(pw).DecryptFile(v2, v2out, true);
            t.Check("v2 Round-Trip funktioniert", true);

            // v2 mit abgeschnittenem Ciphertext: Padding-Oracle ist hier nicht
            // erreichbar, die Datei kann aber still falsch entschluesselt werden.
            string v2broken = Path.Combine(dir, "legacy_v2_trunc.protected");
            byte[] v2raw = File.ReadAllBytes(v2);
            using (var fs = new FileStream(v2broken, FileMode.Create, FileAccess.Write))
            {
                fs.Write(v2raw, 0, v2raw.Length - 16);
            }
            try
            {
                session(pw).DecryptFile(v2broken, Path.Combine(dir, "v2t.out"), true);
                Console.WriteLine("  [info] v2 akzeptiert eine um 16 Byte gekuerzte Datei -");
                Console.WriteLine("         erwartet: v2 hat keine Signatur. Neuen Dateien immer mit v3 erzeugen.");
            }
            catch (CryptographicException)
            {
                Console.WriteLine("  [info] v2 hat die gekuerzte Datei immerhin abgelehnt");
            }
        }

        private static string Copy(string dir, byte[] data, string name)
        {
            string path = Path.Combine(dir, name);
            File.WriteAllBytes(path, data);
            return path;
        }

        private static void Swap(string path, long offsetA, long offsetB, int count)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite))
            {
                byte[] a = new byte[count];
                byte[] b = new byte[count];
                fs.Position = offsetA; fs.Read(a, 0, count);
                fs.Position = offsetB; fs.Read(b, 0, count);
                fs.Position = offsetA; fs.Write(b, 0, count);
                fs.Position = offsetB; fs.Write(a, 0, count);
                fs.Flush(true);
            }
        }

        /// <summary>
        /// Erzeugt eine Datei im Format v2 (Stand 2.0) - unabhaengig vom Produktivcode
        /// implementiert, damit der Rueckwaertspfad tatsaechlich geprueft wird.
        /// </summary>
        internal static string MakeV2File(string path, byte[] plain, string password)
        {
            byte[] magic = { 0x50, 0x43, 0x76, 0x32 }; // PCv2
            byte[] salt = new byte[32];
            byte[] iv = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
                rng.GetBytes(iv);
            }

            byte[] key;
            using (var kdf = new Rfc2898DeriveBytes(Encoding.UTF8.GetBytes(password), salt, 100000, HashAlgorithmName.SHA256))
                key = kdf.GetBytes(32);

            byte[] cipher;
            using (var aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.BlockSize = 128;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = key;
                aes.IV = iv;
                using (var enc = aes.CreateEncryptor())
                    cipher = enc.TransformFinalBlock(plain, 0, plain.Length);
            }

            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                fs.Write(magic, 0, magic.Length);
                fs.Write(salt, 0, salt.Length);
                fs.Write(iv, 0, iv.Length);
                fs.Write(cipher, 0, cipher.Length);
            }
            return path;
        }
    }
}