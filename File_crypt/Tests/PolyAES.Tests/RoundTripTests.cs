using System;
using System.IO;
using System.Security.Cryptography;

namespace crytec.Tests
{
    internal static class RoundTripTests
    {
        internal static void Run(TestRunner t, string dir, Func<string, PolyAES> session)
        {
            t.Section("v3 Round-Trip und Container-Format");

            const string pw = "geheim123";

            // Groessen rund um die AES-Blockgrenze (16) und die Puffergrenze (64 KiB),
            // damit kein Sonderfall ungetestet bleibt.
            int[] sizes = { 0, 1, 15, 16, 17, 31, 32, 33, 4095, 4096, 4097, 65535, 65536, 65537, 100000 };

            foreach (int size in sizes)
            {
                string src = Path.Combine(dir, "rt_" + size + ".bin");
                string enc = Path.Combine(dir, "rt_" + size + ".protected");
                string dec = Path.Combine(dir, "rt_" + size + ".out");
                byte[] data = Program.MakeData(size);
                File.WriteAllBytes(src, data);

                session(pw).EncryptFile(src, enc, false);
                session(pw).DecryptFile(enc, dec, false);

                t.Check("Round-Trip " + size + " Byte", Program.SameBytes(data, File.ReadAllBytes(dec)));

                // Container-Groesse: 52 Byte Header + Ciphertext + 32 Byte HMAC.
                // PKCS7 haengt immer ein vollstaendiges Padding-Block an - auch dann,
                // wenn die Laenge schon ein Vielfaches von 16 ist.
                int expected = 52 + ((size / 16) + 1) * 16 + 32;
                t.Check("Container-Groesse " + size + " Byte",
                        new FileInfo(enc).Length == expected,
                        "erwartet " + expected + ", war " + new FileInfo(enc).Length);

                t.Check("Magic == PCv3 (" + size + ")",
                        Program.Hex(Program.ReadHead(enc, 4)) == "50437633");
            }

            t.Section("v3 Randfaelle");

            // Leeres Passwort wird abgelehnt
            t.CheckThrows<ArgumentException>("leeres Passwort wird abgelehnt",
                () => new PolyAES(new char[0]));

            t.CheckThrows<ArgumentNullException>("null Passwort wird abgelehnt",
                () => new PolyAES(null));

            // Gleicher Inhalt -> verschiedene Chiffre (zufaelliges Salt/IV je Datei)
            string a = Path.Combine(dir, "rand_a.bin");
            string b = Path.Combine(dir, "rand_b.bin");
            File.WriteAllBytes(a, Program.MakeData(512));
            File.WriteAllBytes(b, Program.MakeData(512));
            session(pw).EncryptFile(a, a + ".protected", false);
            session(pw).EncryptFile(b, b + ".protected", false);
            t.Check("gleicher Klartext -> unterschiedliche Chiffre",
                    !Program.SameBytes(File.ReadAllBytes(a + ".protected"), File.ReadAllBytes(b + ".protected")));

            // Salt und IV sind je Datei zufaellig
            t.Check("Salt/I-V variieren",
                    !Program.SameBytes(
                        Slice(File.ReadAllBytes(a + ".protected"), 4, 32),
                        Slice(File.ReadAllBytes(b + ".protected"), 4, 32)));

            // Format-Erkennung
            t.Equal("DetectFormat erkennt v3", ContainerFormat.V3, PolyAES.DetectFormat(a + ".protected"));
            t.Equal("DetectFormat erkennt Unknown bei leerer Datei", ContainerFormat.Unknown, PolyAES.DetectFormat(WriteEmpty(Path.Combine(dir, "leer.bin"))));

            // Ueberschreiben-Schutz
            string target = Path.Combine(dir, "ow.protected");
            File.WriteAllBytes(Path.Combine(dir, "ow.bin"), Program.MakeData(64));
            session(pw).EncryptFile(Path.Combine(dir, "ow.bin"), target, false);
            byte[] firstCipher = File.ReadAllBytes(target);

            t.CheckThrows<IOException>("bestehendes Ziel wird nicht ueberschrieben",
                () => session(pw).EncryptFile(Path.Combine(dir, "ow.bin"), target, false));
            t.Check("Ziel bei overwrite=false unveraendert", Program.SameBytes(firstCipher, File.ReadAllBytes(target)));

            byte[] original = File.ReadAllBytes(Path.Combine(dir, "ow.bin"));
            session(pw).EncryptFile(Path.Combine(dir, "ow.bin"), target, true);
            t.Check("Ziel wird bei overwrite=true ersetzt", !Program.SameBytes(firstCipher, File.ReadAllBytes(target)));
            session(pw).DecryptFile(target, Path.Combine(dir, "ow.out"), true);
            t.Check("Inhalt nach overwrite weiterhin korrekt",
                    Program.SameBytes(original, File.ReadAllBytes(Path.Combine(dir, "ow.out"))));

            // Quelle und Ziel identisch -> Ablehnung
            t.CheckThrows<IOException>("Quelle == Ziel wird abgelehnt",
                () => session(pw).EncryptFile(Path.Combine(dir, "ow.bin"), Path.Combine(dir, "ow.bin"), true));

            // Keine Reste nach dem Lauf
            string[] leftovers = Directory.GetFiles(dir, "*.pctmp");
            t.Check("keine .pctmp-Reste", leftovers.Length == 0,
                    leftovers.Length > 0 ? string.Join(", ", leftovers) : null);

            // Dispose loest den Zustand
            var single = new PolyAES("wegwerf".ToCharArray());
            single.Dispose();
            t.CheckThrows<ObjectDisposedException>("Zugriff nach Dispose", () => single.EncryptFile(a, a + ".x", true));
            t.Check("Dispose zweimal ist harmlos", DisposeTwice(single));
        }

        private static string WriteEmpty(string path)
        {
            File.WriteAllBytes(path, new byte[0]);
            return path;
        }

        private static bool DisposeTwice(PolyAES p)
        {
            try { p.Dispose(); p.Dispose(); return true; }
            catch { return false; }
        }

        internal static byte[] Slice(byte[] src, int offset, int count)
        {
            byte[] r = new byte[count];
            Buffer.BlockCopy(src, offset, r, 0, count);
            return r;
        }
    }
}