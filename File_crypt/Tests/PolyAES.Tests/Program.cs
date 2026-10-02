using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace crytec.Tests
{
    internal static class Program
    {
        private static readonly Dictionary<string, PolyAES> Sessions = new Dictionary<string, PolyAES>();
        private static string _workDir;

        private static int Main()
        {
            Console.WriteLine("privateCrypt - Krypto-Tests");
            Console.WriteLine("PBKDF2-Iterationen: " + PolyAES.Pbkdf2Iterations);

            _workDir = Path.Combine(Path.GetTempPath(), "privateCrypt-tests-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(_workDir);

            var t = new TestRunner();
            try
            {
                RoundTripTests.Run(t, WorkDir, Session);
                BackwardCompatTests.Run(t, WorkDir, Session);
                TamperTests.Run(t, WorkDir, Session);
                FileOpsTests.Run(t, WorkDir);
                LegacyVectorTests.Run(t);
            }
            finally
            {
                foreach (var kv in Sessions)
                    kv.Value.Dispose();
                Sessions.Clear();
                try { Directory.Delete(_workDir, true); } catch { }
            }

            return t.Summarize();
        }

        /// <summary>
        /// Liefert eine <see cref="PolyAES"/>-Instanz fuer ein Passwort. Wird
        /// zwischengespeichert, weil die PBKDF2-Ableitung pro Session kostet und
        /// die Tests sonst unzaehlige Sekunden braeuchten.
        /// </summary>
        internal static PolyAES Session(string password)
        {
            PolyAES s;
            if (!Sessions.TryGetValue(password, out s))
            {
                s = new PolyAES(password.ToCharArray());
                Sessions.Add(password, s);
            }
            return s;
        }

        internal static string WorkDir { get { return _workDir; } }

        /// <summary>Deterministische Pseudo-Zufallsdaten (kein kryptografischer Zufall noetig).</summary>
        internal static byte[] MakeData(int length)
        {
            byte[] data = new byte[length];
            for (int i = 0; i < length; i++)
                data[i] = (byte)((i * 31) + ((i >> 8) * 7) + (i >> 16));
            return data;
        }

        internal static bool SameBytes(byte[] a, byte[] b)
        {
            if (a == null || b == null) return a == b;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        internal static string Hex(byte[] data)
        {
            var sb = new StringBuilder(data.Length * 2);
            foreach (byte b in data) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        /// <summary>Liest die ersten <paramref name="count"/> Bytes einer Datei.</summary>
        internal static byte[] ReadHead(string path, int count)
        {
            byte[] buf = new byte[count];
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                int total = 0;
                while (total < count)
                {
                    int read = fs.Read(buf, total, count - total);
                    if (read <= 0) break;
                    total += read;
                }
            }
            return buf;
        }

        /// <summary>Schreibt <paramref name="count"/> Bytes ab <paramref name="offset"/> in eine Datei.</summary>
        internal static void OverwriteBytes(string path, long offset, byte[] replacement)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite))
            {
                fs.Position = offset;
                fs.Write(replacement, 0, replacement.Length);
                fs.Flush(true);
            }
        }

        /// <summary>
        /// Kippt genau ein Bit - und garantiert damit, dass sich die Datei auch
        /// wirklich aendert.
        ///
        /// <para>Nicht einfach einen festen Wert ueberschreiben: der Inhalt einer
        /// verschluesselten Datei ist zufaellig. Wer "0xFF" schreibt, trifft mit
        /// einer Wahrscheinlichkeit von 1:256 bereits den vorhandenen Wert, die
        /// Manipulation bleibt dann wirkungslos, die Datei ist weiterhin gueltig,
        /// die Entschluesselung gelingt - und der Test schlaegt fehl, ohne dass
        /// etwas im Programm kaputt waere. Mit XOR kann das nicht passieren.</para>
        /// </summary>
        internal static void FlipBit(string path, long offset, int mask = 0x01)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite))
            {
                fs.Position = offset;
                int b = fs.ReadByte();
                if (b < 0)
                    throw new IOException("Bitflip ueber Dateiende hinaus: " + path + " @ " + offset);

                fs.Position = offset;
                fs.WriteByte((byte)(b ^ mask));
                fs.Flush(true);
            }
        }

        internal static void Truncate(string path, long newLength)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite))
                fs.SetLength(newLength);
        }
    }
}