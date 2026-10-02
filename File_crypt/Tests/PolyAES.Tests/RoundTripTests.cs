using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;

namespace crytec.Tests
{
    internal static class RoundTripTests
    {
        internal static void Run(TestRunner t, string dir, Func<string, PolyAES> session)
        {
            t.Section("v4 Round-Trip und Container-Format");

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

                t.Check("Magic == PCv4 (" + size + ")",
                        Program.Hex(Program.ReadHead(enc, 4)) == "50437634");
            }

            t.Section("v4 Schluesseltrennung (der Grund fuer das neue Format)");

            // Der Kern der Korrektur: in Version 3.0 wurden Chiffrier- und
            // Signaturschluessel aus demselben Seed abgeleitet und waren damit
            // byteweise gleich. Wenn dieser Test fehlschlaegt, ist wieder ein
            // einziger Schluessel fuer beides im Spiel.
            byte[] salt = new byte[32];
            byte[] iv = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
                rng.GetBytes(iv);
            }

            byte[] encKey, macKey;
            session(pw).DeriveFileKeys(salt, iv, out encKey, out macKey);

            t.Check("Chiffrier- und Signaturschluessel sind verschieden",
                    !Program.SameBytes(encKey, macKey),
                    "beide waren " + Program.Hex(encKey));
            t.Check("Schluessel haben je 32 Byte",
                    encKey.Length == 32 && macKey.Length == 32);
            t.Check("Schluessel sind nicht leer",
                    !AllZero(encKey) && !AllZero(macKey));

            // Und sie duerfen sich auch nicht nur in einem Bit unterscheiden -
            // das waere ein getarnter Gleichheitsfall.
            int differing = 0;
            for (int i = 0; i < 32; i++) if (encKey[i] != macKey[i]) differing++;
            t.Check("Schluessel unterscheiden sich in vielen Bytes", differing >= 16,
                    "nur " + differing + " von 32 Bytes unterschiedlich");

            // Gleiche Eingabe -> gleiche Schluessel. Andernfalls waere die
            // Entschluesselung nicht reproduzierbar.
            byte[] encKey2, macKey2;
            session(pw).DeriveFileKeys(salt, iv, out encKey2, out macKey2);
            t.Check("Ableitung ist reproduzierbar",
                    Program.SameBytes(encKey, encKey2) && Program.SameBytes(macKey, macKey2));

            // Anderer Salt -> anderer Schluessel. Bindung an die Datei.
            salt[0] ^= 0xFF;
            byte[] encKey3, macKey3;
            session(pw).DeriveFileKeys(salt, iv, out encKey3, out macKey3);
            t.Check("Anderer Salt ergibt anderen Schluessel",
                    !Program.SameBytes(encKey, encKey3));

            // Der Beweis, dass die Trennung wirkt: eine v3-Datei laesst sich mit
            // den v4-Schluesseln NICHT entschluesseln. Wuerde der Leseweg von v3
            // faelschlich die v4-Ableitung benutzen, waere eine Datei aus 3.0
            // nicht mehr erreichbar.
            byte[] v3Enc, v3Mac;
            byte[] v3Salt = new byte[32];
            byte[] v3Iv = new byte[16];
            using (var rng2 = RandomNumberGenerator.Create())
            {
                rng2.GetBytes(v3Salt);
                rng2.GetBytes(v3Iv);
            }
            PolyAES.DeriveV3Keys(MasterKeyOf(session(pw)), v3Salt, v3Iv, out v3Enc, out v3Mac);
            t.Check("v3 reproduziert den gemeinsamen Schluessel (Fehler von 3.0)",
                    Program.SameBytes(v3Enc, v3Mac),
                    "eine korrigierte Ableitung wuerde hier abweichen");

            t.Section("v4 Randfaelle");

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
            t.Equal("DetectFormat erkennt v4", ContainerFormat.V4, PolyAES.DetectFormat(a + ".protected"));
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

            t.Section("Abbruch (CancellationToken)");

            // Ein abgebrochener Vorgang darf das Ziel nicht veraendern und keine
            // temporaeren Dateien hinterlassen.
            string cancelTarget = Path.Combine(dir, "cancel.protected");
            string cancelSrc = Path.Combine(dir, "cancel.bin");
            File.WriteAllBytes(cancelSrc, Program.MakeData(200000));

            byte[] beforeCancel;
            using (var cts = new CancellationTokenSource())
            {
                // Sofort abbrechen: der Vorgang muss noch gar nichts anlegen.
                cts.Cancel();
                bool threw = false;
                try { session(pw).EncryptFile(cancelSrc, cancelTarget, true, cts.Token); }
                catch (OperationCanceledException) { threw = true; }
                t.Check("abgebrochene Verschluesselung meldet Abbruch", threw);
            }
            t.Check("kein Ziel bei Abbruch vor Beginn", !File.Exists(cancelTarget));
            t.Check("keine .pctmp-Reste bei Abbruch",
                    Directory.GetFiles(dir, "cancel*.pctmp").Length == 0);

            // Ein bestehendes Ziel bleibt bei Abbruch unangetastet.
            session(pw).EncryptFile(cancelSrc, cancelTarget, false);
            beforeCancel = File.ReadAllBytes(cancelTarget);
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();
                try { session(pw).EncryptFile(cancelSrc, cancelTarget, true, cts.Token); }
                catch (OperationCanceledException) { }
            }
            t.Check("bestehendes Ziel bleibt bei Abbruch unveraendert",
                    Program.SameBytes(beforeCancel, File.ReadAllBytes(cancelTarget)));

            // Und dasselbe fuer die Entschluesselung.
            string cancelPlain = Path.Combine(dir, "cancel.out");
            File.WriteAllBytes(cancelPlain, Program.MakeData(64));
            byte[] plainBefore = File.ReadAllBytes(cancelPlain);
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();
                try { session(pw).DecryptFile(cancelTarget, cancelPlain, true, cts.Token); }
                catch (OperationCanceledException) { }
            }
            t.Check("bestehendes Ziel bleibt bei abgebrochener Entschluesselung unveraendert",
                    Program.SameBytes(plainBefore, File.ReadAllBytes(cancelPlain)));
        }

        private static bool AllZero(byte[] data)
        {
            foreach (byte b in data) if (b != 0) return false;
            return true;
        }

        /// <summary>
        /// Liest den Masterschluessel aus einer Session. Nur fuer den Test, der
        /// die v3-Ableitung unabhaengig nachrechnen will.
        /// </summary>
        private static byte[] MasterKeyOf(PolyAES p)
        {
            var field = typeof(PolyAES).GetField("_masterKey",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return (byte[])field.GetValue(p);
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