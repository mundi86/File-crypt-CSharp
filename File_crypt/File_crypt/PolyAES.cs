/*
 * Copyright (C) 2011, Dextrey (0xDEADDEAD)
 * Removing this copyright notice is prohibited without permission from author
 * Using this code in your own software product, commercial or not is allowed
 */

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace crytec
{
    /// <summary>Container-Format einer verschluesselten Datei.</summary>
    public enum ContainerFormat
    {
        /// <summary>Kein bekanntes Format (z. B. leere oder zu kurze Datei).</summary>
        Unknown = 0,

        /// <summary>Originales Format von 2011 (nur noch lesend).</summary>
        Legacy = 1,

        /// <summary>AES-256-CBC ohne Authentifizierung, 2.0 (nur noch lesend).</summary>
        V2 = 2,

        /// <summary>AES-256-CBC mit HMAC-SHA256, 3.0 (nur noch lesend).</summary>
        V3 = 3,

        /// <summary>
        /// AES-256-CBC mit HMAC-SHA256 und getrennten Schluesseln, 4.0
        /// (aktuelles Format beim Schreiben).
        /// </summary>
        V4 = 4
    }

    /// <summary>
    /// Verschluesselung und Entschluesselung von Dateien.
    ///
    /// <para><b>Formate</b> (siehe SECURITY.md):</para>
    /// <list type="bullet">
    ///   <item><description><b>v4</b> (aktuell, schreiben + lesen):
    ///   <c>[magic "PCv4"][salt 32][iv 16][ciphertext][hmac-sha256 32]</c><br/>
    ///   AES-256-CBC, PKCS7, HMAC-SHA256 ueber Header + Ciphertext ("Encrypt-then-MAC").
    ///   Der Chiffrierschluessel und der Signaturschluessel stammen aus
    ///   <b>zwei getrennten Haelften</b> des Masterschluessels.</description></item>
    ///   <item><description><b>v3</b> (3.0, nur lesen):
    ///   <c>[magic "PCv3"][salt 32][iv 16][ciphertext][hmac-sha256 32]</c><br/>
    ///   Gleicher Aufbau wie v4, aber beide Schluessel wurden in 3.0 aus
    ///   <b>demselben</b> Seed abgeleitet und waren damit byteweise gleich.
    ///   Wird noch gelesen, damit bereits erzeugte Dateien erreichbar bleiben.</description></item>
    ///   <item><description><b>v2</b> (2.0, nur lesen):
    ///   <c>[magic "PCv2"][salt 32][iv 16][ciphertext]</c><br/>
    ///   AES-256-CBC, PBKDF2-SHA256 mit 100.000 Iterationen. Ohne Integritaetsschutz.</description></item>
    ///   <item><description><b>v1 Legacy</b> (2011, nur lesen):
    ///   <c>[ciphertext][salt 32][iv 32]</c><br/>
    ///   Rijndael-256-CBC, PBKDF2-SHA1 mit 2.000 Iterationen,
    ///   Schluessel = <c>ASCII(uplowme(sha256hex(password)))</c>. Ohne Integritaetsschutz.</description></item>
    /// </list>
    ///
    /// <para><b>Schluesselableitung v4.</b> Aus dem Passwort wird einmal pro Vorgang ein
    /// 64-Byte-Masterschluessel abgeleitet (PBKDF2-HMAC-SHA256). Dessen erste Haelfte
    /// dient ausschliesslich der Verschluesselung, die zweite ausschliesslich der
    /// Signatur - ein Byte des einen Schluessels kann also nie Teil des anderen werden.
    /// Pro Datei werden daraus per HMAC-Schluessel abgeleitet, gebunden an Salt + IV des
    /// Containers. Das kostet die teure PBKDF2 nur einmal pro Lauf und haelt
    /// Ordneroperationen mit vielen Dateien schnell.</para>
    ///
    /// <para>Alle Methoden arbeiten streamend, der Speicherbedarf ist unabhaengig von der
    /// Dateigroesse - das gilt auch fuer die Altformate v2 und v1. Zieldateien werden
    /// ueber eine temporaere Datei atomar ersetzt: bei einem Abbruch bleibt entweder die
    /// alte oder die neue Datei vollstaendig, nie eine halb geschriebene.</para>
    /// </summary>
    public sealed class PolyAES : IDisposable
    {
        private static readonly byte[] MagicV4 = { 0x50, 0x43, 0x76, 0x34 }; // "PCv4"
        private static readonly byte[] MagicV3 = { 0x50, 0x43, 0x76, 0x33 }; // "PCv3"
        private static readonly byte[] MagicV2 = { 0x50, 0x43, 0x76, 0x32 }; // "PCv2"

        private const int MagicSize = 4;
        private const int SaltSize = 32;
        private const int IvSize = 16;
        private const int KeySize = 32;
        private const int TagSize = 32;

        /// <summary>Magic + Salt + IV.Fuer v2, v3 und v4 identisch.</summary>
        private const int V4HeaderSize = MagicSize + SaltSize + IvSize; // 52

        /// <summary>Trailing Salt und IV des 2011-Formats, je 32 Byte.</summary>
        private const int LegacyTrailing = 32;

        /// <summary>Blockgroesse des 2011-Formats: Rijndael mit 256-Bit-Bloecken.</summary>
        private const int LegacyBlockSize = 32;

        private const int CopyBufferSize = 64 * 1024;

        /// <summary>
        /// PBKDF2-Iterationen fuer die Master-Ableitung (v3 und v4).
        ///
        /// <para>Messwerte auf einem gewoehnlichen Desktop (ca. 1,4 s):</para>
        /// <list type="bullet">
        ///   <item><description>2.000 (Format v1 von 2011) - heute als KDF nicht mehr vertretbar</description></item>
        ///   <item><description>100.000 (Format v2)</description></item>
        ///   <item><description><b>300.000 - hier gewaehlt</b>: 150x staerker als v1, 3x so stark
        ///   wie v2, und trotzdem unter 1,5 s.</description></item>
        /// </list>
        ///
        /// <para>Die Ableitung laeuft einmal pro Session, nicht pro Datei, deshalb bleiben
        /// Ordner mit vielen Dateien schnell. Wer einen laengeren Wartezeitraum akzeptiert,
        /// kann den Wert hier erhoehen - hoeher ist immer besser.</para>
        ///
        /// <para><b>Wichtig:</b> Der Wert darf nur together mit einem neuen
        /// Container-Format geaendert werden. Wird er fuer ein bestehendes Format
        /// angehoben, laesst sich kein einzige bereits geschriebene Datei mehr
        /// oeffnen - die Ableitung ergibt dann einen anderen Schluessel. Aus diesem
        /// Grund ist er bewusst eine Konstante und keine Option.</para>
        /// </summary>
        public const int Pbkdf2Iterations = 300000;

        private const int Pbkdf2IterationsV2 = 100000;
        private const int Pbkdf2IterationsLegacy = 2000;

        private static readonly byte[] EmptyBuffer = new byte[0];

        /// <summary>
        /// Fester Salt fuer die Master-Ableitung. Er verhindert Rainbow-Tables ueber
        /// Anwendungen hinweg; die pro Datei zufaelligen Salts sorgen fuer die
        /// Eindeutigkeit innerhalb von privateCrypt.
        /// </summary>
        private static readonly byte[] MasterSalt = Encoding.UTF8.GetBytes("privateCrypt/v3/master-key-salt");

        /// <summary>Label fuer die Chiffrierschluessel-Ableitung (v4).</summary>
        private static readonly byte[] LabelEnc = Encoding.UTF8.GetBytes("privateCrypt/v4/enc");

        /// <summary>Label fuer die Signaturschluessel-Ableitung (v4).</summary>
        private static readonly byte[] LabelMac = Encoding.UTF8.GetBytes("privateCrypt/v4/mac");

        /// <summary>
        /// Domain-Seed der Version 3.0. In 3.0 wurden daraus Chiffrier- und
        /// Signaturschluessel abgeleitet - beide waren identisch, weil nur ein
        /// Label benutzt wurde. Wird ausschliesslich zum Lesen von v3-Dateien
        /// gebraucht; see <see cref="DeriveV3Keys"/>.
        /// </summary>
        private static readonly byte[] LegacyV3Domain = { (byte)'e', (byte)'n', (byte)'c' };

        private char[] _password;
        private byte[] _masterKey;
        private bool _disposed;

        /// <summary>
        /// Erstellt eine neue Session. Die PBKDF2-Ableitung laeuft einmal hier und wird
        /// fuer alle Dateien der Session wiederverwendet.
        /// </summary>
        /// <param name="password">Passwort. Wird kopiert; die Instanz loescht ihre Kopie in <see cref="Dispose"/>.</param>
        public PolyAES(char[] password)
        {
            if (password == null) throw new ArgumentNullException("password");
            if (password.Length == 0) throw new ArgumentException("Passwort darf nicht leer sein.", "password");

            _password = (char[])password.Clone();

            byte[] pw = ToUtf8(_password);
            try
            {
                using (var kdf = new Rfc2898DeriveBytes(pw, MasterSalt, Pbkdf2Iterations, HashAlgorithmName.SHA256))
                    _masterKey = kdf.GetBytes(2 * KeySize);
            }
            finally
            {
                Array.Clear(pw, 0, pw.Length);
            }
        }

        // ------------------------------------------------------------------
        // Oeffentliche API
        // ------------------------------------------------------------------

        /// <summary>Erkennt das Format einer Datei, ohne sie zu entschluesseln.</summary>
        public static ContainerFormat DetectFormat(string path)
        {
            if (path == null) throw new ArgumentNullException("path");

            byte[] head = new byte[MagicSize];
            int read;
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                read = ReadFully(fs, head, 0, MagicSize);

            if (read < MagicSize)
                return ContainerFormat.Unknown;
            if (FixedTimeEquals(head, MagicV4))
                return ContainerFormat.V4;
            if (FixedTimeEquals(head, MagicV3))
                return ContainerFormat.V3;
            if (FixedTimeEquals(head, MagicV2))
                return ContainerFormat.V2;
            return ContainerFormat.Legacy;
        }

        /// <summary>
        /// Verschluesselt <paramref name="sourcePath"/> nach <paramref name="targetPath"/>.
        /// Das Ergebnis hat immer das Format v4.
        /// </summary>
        /// <param name="sourcePath">Klartextdatei.</param>
        /// <param name="targetPath">Ziel; wird atomar ersetzt.</param>
        /// <param name="overwrite">
        /// <c>false</c>: es wird nichts getan, wenn das Ziel existiert.
        /// <c>true</c>: das vorhandene Ziel wird ersetzt.
        /// </param>
        public void EncryptFile(string sourcePath, string targetPath, bool overwrite)
        {
            EncryptFile(sourcePath, targetPath, overwrite, CancellationToken.None);
        }

        /// <summary>
        /// Verschluesselt mit Abbruchmoeglichkeit. Wird der Token ausgeloest, wird die
        /// temporaere Datei entfernt und das Ziel nicht angefasst.
        /// </summary>
        public void EncryptFile(string sourcePath, string targetPath, bool overwrite, CancellationToken token)
        {
            ThrowIfDisposed();
            if (sourcePath == null) throw new ArgumentNullException("sourcePath");
            if (targetPath == null) throw new ArgumentNullException("targetPath");

            // Klartext und Ziel duerfen nicht dieselbe Datei sein.
            if (AreSamePath(sourcePath, targetPath))
                throw new IOException("Quelldatei und Zieldatei sind identisch: '" + sourcePath + "'.");

            token.ThrowIfCancellationRequested();

            string tmp = targetPath + ".pctmp";
            try
            {
                using (var src = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, CopyBufferSize))
                {
                    using (var dst = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, CopyBufferSize))
                    {
                        EncryptV4(src, dst, token);
                        dst.Flush(true);
                    }
                }

                token.ThrowIfCancellationRequested();
                CommitFile(tmp, targetPath, overwrite);
                tmp = null;
            }
            finally
            {
                if (tmp != null) SafeDelete(tmp);
            }
        }

        /// <summary>
        /// Entschluesselt <paramref name="sourcePath"/> nach <paramref name="targetPath"/>.
        /// Das Eingangsformat (v4, v3, v2 oder Legacy) wird automatisch erkannt.
        /// </summary>
        /// <param name="sourcePath">Verschluesselte Datei.</param>
        /// <param name="targetPath">Ziel; wird atomar ersetzt.</param>
        /// <param name="overwrite">
        /// <c>false</c>: es wird nichts getan, wenn das Ziel existiert.
        /// <c>true</c>: das vorhandene Ziel wird ersetzt.
        /// </param>
        /// <exception cref="CryptographicException">
        /// Falsches Passwort, manipulierte oder beschaedigte Datei.
        /// </exception>
        public void DecryptFile(string sourcePath, string targetPath, bool overwrite)
        {
            DecryptFile(sourcePath, targetPath, overwrite, CancellationToken.None);
        }

        /// <summary>
        /// Entschluesselt mit Abbruchmoeglichkeit. Wird der Token ausgeloest, wird die
        /// temporaere Datei entfernt und das Ziel nicht angefasst.
        /// </summary>
        public void DecryptFile(string sourcePath, string targetPath, bool overwrite, CancellationToken token)
        {
            ThrowIfDisposed();
            if (sourcePath == null) throw new ArgumentNullException("sourcePath");
            if (targetPath == null) throw new ArgumentNullException("targetPath");

            if (AreSamePath(sourcePath, targetPath))
                throw new IOException("Quelldatei und Zieldatei sind identisch: '" + sourcePath + "'.");

            token.ThrowIfCancellationRequested();

            switch (DetectFormat(sourcePath))
            {
                case ContainerFormat.V4:
                    DecryptAuthenticatedFile(sourcePath, targetPath, overwrite, true, token);
                    break;
                case ContainerFormat.V3:
                    DecryptAuthenticatedFile(sourcePath, targetPath, overwrite, false, token);
                    break;
                case ContainerFormat.V2:
                    DecryptV2File(sourcePath, targetPath, overwrite, token);
                    break;
                case ContainerFormat.Legacy:
                    DecryptLegacyFile(sourcePath, targetPath, overwrite, token);
                    break;
                default:
                    throw new CryptographicException(
                        "Die Datei ist zu kurz, um eine verschluesselte Datei zu sein: '" + sourcePath + "'.");
            }
        }

        /// <summary>Loescht Passwort und Masterschluessel aus dem Speicher.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_password != null)
            {
                Array.Clear(_password, 0, _password.Length);
                _password = null;
            }
            if (_masterKey != null)
            {
                Array.Clear(_masterKey, 0, _masterKey.Length);
                _masterKey = null;
            }
        }

        // ------------------------------------------------------------------
        // v4: AES-256-CBC + HMAC-SHA256 mit getrennten Schluesseln (Encrypt-then-MAC)
        // ------------------------------------------------------------------

        private void EncryptV4(Stream source, Stream target, CancellationToken token)
        {
            byte[] salt = new byte[SaltSize];
            byte[] iv = new byte[IvSize];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
                rng.GetBytes(iv);
            }

            byte[] header = BuildHeader(MagicV4, salt, iv);
            byte[] encKey = null;
            byte[] macKey = null;
            byte[] tag = null;

            try
            {
                DeriveFileKeys(salt, iv, out encKey, out macKey);

                target.Write(header, 0, header.Length);

                using (var mac = new HMACSHA256(macKey))
                {
                    // Signiert werden Header und Ciphertext - nicht der Klartext.
                    mac.TransformBlock(header, 0, header.Length, null, 0);

                    // Der Hash wird ueber genau die Bytes berechnet, die in die
                    // Zieldatei geschrieben werden.
                    var tap = new HashingWriteStream(target, mac);
                    using (var aes = CreateAes(encKey, iv))
                    using (var encryptor = aes.CreateEncryptor())
                    using (var crypto = new CryptoStream(tap, encryptor, CryptoStreamMode.Write))
                    {
                        CopyStream(source, crypto, token);
                        crypto.FlushFinalBlock();
                    }

                    mac.TransformFinalBlock(EmptyBuffer, 0, 0);
                    tag = mac.Hash;
                }

                target.Write(tag, 0, TagSize);
            }
            finally
            {
                Array.Clear(salt, 0, salt.Length);
                Array.Clear(iv, 0, iv.Length);
                if (encKey != null) Array.Clear(encKey, 0, encKey.Length);
                if (macKey != null) Array.Clear(macKey, 0, macKey.Length);
            }
        }

        /// <summary>
        /// Entschluesselt v3 oder v4. Beide Formate haben identischen Aufbau und
        /// unterscheiden sich nur in der Schluesselableitung.
        /// </summary>
        /// <param name="useV4Keys">
        /// <c>true</c> fuer v4 (getrennte Schluessel), <c>false</c> fuer v3
        /// (reproduziert exakt die Ableitung der Version 3.0).
        /// </param>
        private void DecryptAuthenticatedFile(string sourcePath, string targetPath, bool overwrite,
            bool useV4Keys, CancellationToken token)
        {
            string tmp = targetPath + ".pctmp";
            try
            {
                using (var src = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, CopyBufferSize))
                using (var dst = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, CopyBufferSize))
                {
                    byte[] header = ReadExactly(src, V4HeaderSize, sourcePath);
                    byte[] expectedMagic = useV4Keys ? MagicV4 : MagicV3;
                    if (!FixedTimeEquals(0, header, 0, expectedMagic, MagicSize))
                        throw new CryptographicException("Die Datei besitzt kein gültiges privateCrypt-Format.");

                    long cipherLen = src.Length - V4HeaderSize - TagSize;
                    if (cipherLen <= 0 || cipherLen % 16 != 0)
                        throw new CryptographicException(
                            "Die Datei ist beschädigt (unerwartete Dateilänge: " + src.Length + " Bytes).");

                    byte[] salt = new byte[SaltSize];
                    byte[] iv = new byte[IvSize];
                    byte[] encKey = null;
                    byte[] macKey = null;
                    try
                    {
                        Buffer.BlockCopy(header, MagicSize, salt, 0, SaltSize);
                        Buffer.BlockCopy(header, MagicSize + SaltSize, iv, 0, IvSize);

                        if (useV4Keys)
                            DeriveFileKeys(salt, iv, out encKey, out macKey);
                        else
                            DeriveV3Keys(_masterKey, salt, iv, out encKey, out macKey);

                        token.ThrowIfCancellationRequested();

                        // Phase 1: Signatur pruefen, BEVOR irgendein Klartext entsteht.
                        VerifyTag(src, header, cipherLen, macKey, sourcePath);

                        // Phase 2: entschluesseln.
                        src.Position = V4HeaderSize;
                        using (var limited = new LengthLimitedStream(src, cipherLen))
                        using (var aes = CreateAes(encKey, iv))
                        using (var decryptor = aes.CreateDecryptor())
                        using (var crypto = new CryptoStream(limited, decryptor, CryptoStreamMode.Read))
                        {
                            CopyStream(crypto, dst, token);
                            dst.Flush(true);
                        }
                    }
                    finally
                    {
                        Array.Clear(salt, 0, salt.Length);
                        Array.Clear(iv, 0, iv.Length);
                        if (encKey != null) Array.Clear(encKey, 0, encKey.Length);
                        if (macKey != null) Array.Clear(macKey, 0, macKey.Length);
                    }
                }

                token.ThrowIfCancellationRequested();
                CommitFile(tmp, targetPath, overwrite);
                tmp = null;
            }
            finally
            {
                if (tmp != null) SafeDelete(tmp);
            }
        }

        /// <summary>Prueft die HMAC-Signatur ueber Header + Ciphertext (konstante Laufzeit).</summary>
        private static void VerifyTag(FileStream src, byte[] header, long cipherLen, byte[] macKey, string sourcePath)
        {
            byte[] storedTag = new byte[TagSize];
            byte[] buffer = new byte[CopyBufferSize];

            try
            {
                byte[] computed;
                using (var mac = new HMACSHA256(macKey))
                {
                    mac.TransformBlock(header, 0, header.Length, null, 0);

                    src.Position = V4HeaderSize;
                    long remaining = cipherLen;
                    while (remaining > 0)
                    {
                        int want = (int)Math.Min(buffer.Length, remaining);
                        int read = ReadFully(src, buffer, 0, want);
                        if (read <= 0)
                            throw new CryptographicException("Die Datei ist beschädigt (unerwartetes Dateiende).");

                        mac.TransformBlock(buffer, 0, read, null, 0);
                        remaining -= read;
                    }

                    mac.TransformFinalBlock(EmptyBuffer, 0, 0);
                    computed = mac.Hash;
                }

                src.Position = src.Length - TagSize;
                if (ReadFully(src, storedTag, 0, TagSize) != TagSize)
                    throw new CryptographicException("Die Datei ist beschädigt (Signatur fehlt).");

                // Absichtlich keine Unterscheidung zwischen "falsches Passwort" und
                // "manipulierte Datei" - das waere eine Information fuer Angreifer.
                if (!FixedTimeEquals(computed, storedTag))
                    throw new CryptographicException(
                        "Entschlüsselung fehlgeschlagen: Passwort falsch oder Datei beschädigt bzw. verändert.");
            }
            finally
            {
                Array.Clear(storedTag, 0, storedTag.Length);
                Array.Clear(buffer, 0, buffer.Length);
            }
        }

        // ------------------------------------------------------------------
        // v2: AES-256-CBC ohne Integritaetsschutz (nur lesend, seit 3.0)
        // ------------------------------------------------------------------

        /// <summary>
        /// Streamt eine v2-Datei. Bewusst kein <c>ReadAllBytes</c>: die Datei ist
        /// potenziell beliebig gross, und die EXE ist 32-Bit. Ein 2-GB-Altdokument
        /// waere mit <c>ReadAllBytes</c> dreifach im Speicher (Datei + Puffer +
        /// Klartextergebnis) und wuerde an <c>OutOfMemoryException</c> scheitern.
        /// </summary>
        private void DecryptV2File(string sourcePath, string targetPath, bool overwrite, CancellationToken token)
        {
            string tmp = targetPath + ".pctmp";
            try
            {
                using (var src = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, CopyBufferSize))
                using (var dst = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, CopyBufferSize))
                {
                    byte[] header = ReadExactly(src, V4HeaderSize, sourcePath);
                    if (!FixedTimeEquals(0, header, 0, MagicV2, MagicSize))
                        throw new CryptographicException("Die Datei besitzt kein gültiges privateCrypt-Format.");

                    long cipherLen = src.Length - V4HeaderSize;
                    if (cipherLen <= 0 || cipherLen % 16 != 0)
                        throw new CryptographicException("Die Datei ist beschädigt (unerwartete Blockgröße).");

                    byte[] salt = new byte[SaltSize];
                    byte[] iv = new byte[IvSize];
                    byte[] key = null;
                    try
                    {
                        Buffer.BlockCopy(header, MagicSize, salt, 0, SaltSize);
                        Buffer.BlockCopy(header, MagicSize + SaltSize, iv, 0, IvSize);

                        byte[] pw = ToUtf8(_password);
                        try
                        {
                            using (var kdf = new Rfc2898DeriveBytes(pw, salt, Pbkdf2IterationsV2, HashAlgorithmName.SHA256))
                                key = kdf.GetBytes(KeySize);
                        }
                        finally
                        {
                            Array.Clear(pw, 0, pw.Length);
                        }

                        token.ThrowIfCancellationRequested();

                        // Ab hier wird nur noch gestreamt. Ein falsches Passwort faellt
                        // ueber das Padding auf - CryptoStream meldet es beim Lesen.
                        src.Position = V4HeaderSize;
                        using (var limited = new LengthLimitedStream(src, cipherLen))
                        {
                            DecryptCbcTo(limited, dst, key, iv, token);
                        }
                        dst.Flush(true);
                    }
                    finally
                    {
                        Array.Clear(salt, 0, salt.Length);
                        Array.Clear(iv, 0, iv.Length);
                        if (key != null) Array.Clear(key, 0, key.Length);
                    }
                }

                token.ThrowIfCancellationRequested();
                CommitFile(tmp, targetPath, overwrite);
                tmp = null;
            }
            finally
            {
                if (tmp != null) SafeDelete(tmp);
            }
        }

        // ------------------------------------------------------------------
        // v1 Legacy: Rijndael-256 (nur lesend, seit 2.0)
        // ------------------------------------------------------------------

        /// <summary>
        /// Streamt eine Datei aus dem Original von 2011. Aufbau:
        /// <c>[ciphertext][salt 32][iv 32]</c> - Salt und IV stehen hinter dem
        /// Ciphertext und werden deshalb per Sprung gelesen, statt die ganze Datei
        /// zu laden.
        /// </summary>
        private void DecryptLegacyFile(string sourcePath, string targetPath, bool overwrite, CancellationToken token)
        {
            string tmp = targetPath + ".pctmp";
            try
            {
                using (var src = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, CopyBufferSize))
                using (var dst = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, CopyBufferSize))
                {
                    long total = src.Length;
                    if (total < (2 * LegacyTrailing) + LegacyBlockSize)
                        throw new CryptographicException(
                            "Die Datei ist beschädigt (nur " + total + " Bytes, Legacy-Format erwartet mindestens 96).");

                    long cipherLen = total - 2L * LegacyTrailing;
                    if (cipherLen % LegacyBlockSize != 0)
                        throw new CryptographicException("Die Datei ist beschädigt (unerwartete Blockgröße im Legacy-Format).");

                    byte[] salt = new byte[LegacyTrailing];
                    byte[] iv = new byte[LegacyTrailing];
                    byte[] derivedKey = null;

                    try
                    {
                        // Salt und IV aus dem hinteren Dateibereich lesen. Das geht nur,
                        // weil src seekbar ist - ein Stream von der Platte ist es.
                        src.Position = cipherLen;
                        ReadFully(src, salt, 0, LegacyTrailing);
                        src.Position = total - LegacyTrailing;
                        ReadFully(src, iv, 0, LegacyTrailing);

                        byte[] key = Encoding.ASCII.GetBytes(UplowmeLegacy(GetHashSha256Legacy(ToLegacyString(_password))));
                        try
                        {
                            using (var kdf = new Rfc2898DeriveBytes(key, salt, Pbkdf2IterationsLegacy))
                                derivedKey = kdf.GetBytes(KeySize);

                            token.ThrowIfCancellationRequested();

                            src.Position = 0;
                            using (var limited = new LengthLimitedStream(src, cipherLen))
                            {
                                DecryptLegacyCbcTo(limited, dst, derivedKey, iv, token);
                            }
                            dst.Flush(true);
                        }
                        finally
                        {
                            Array.Clear(key, 0, key.Length);
                        }
                    }
                    finally
                    {
                        Array.Clear(salt, 0, salt.Length);
                        Array.Clear(iv, 0, iv.Length);
                        if (derivedKey != null) Array.Clear(derivedKey, 0, derivedKey.Length);
                    }
                }

                token.ThrowIfCancellationRequested();
                CommitFile(tmp, targetPath, overwrite);
                tmp = null;
            }
            finally
            {
                if (tmp != null) SafeDelete(tmp);
            }
        }

        /// <summary>
        /// Entschluesselt einen AES-CBC-Stream in <paramref name="target"/> und
        /// uebersetzt Fehler in eine einheitliche Meldung.
        /// </summary>
        private static void DecryptCbcTo(Stream source, Stream target, byte[] key, byte[] iv,
            CancellationToken token)
        {
            try
            {
                using (var aes = CreateAes(key, iv))
                using (var decryptor = aes.CreateDecryptor())
                using (var crypto = new CryptoStream(source, decryptor, CryptoStreamMode.Read))
                {
                    CopyStream(crypto, target, token);
                }
            }
            catch (CryptographicException)
            {
                // Fast immer ein falsches Passwort: bei AES-CBC faellt die
                // Entschluesselung ueber das Padding auf. Die Meldung bleibt
                // absichtlich zweideutig.
                throw new CryptographicException(
                    "Entschlüsselung fehlgeschlagen: Passwort falsch oder Datei beschädigt.");
            }
        }

#pragma warning disable CS0618 // RijndaelManaged ist in .NET 6+ obsolet, auf .NET Framework 4.8 aber noetig
        /// <summary>Entschluesselt den 2011-Blockcipher (Rijndael-256) in einen Stream.</summary>
        private static void DecryptLegacyCbcTo(Stream source, Stream target, byte[] key, byte[] iv,
            CancellationToken token)
        {
            try
            {
                using (var algo = new RijndaelManaged())
                {
                    algo.Mode = CipherMode.CBC;
                    algo.BlockSize = 256;
                    algo.Key = key;
                    algo.IV = iv;

                    using (var decryptor = algo.CreateDecryptor())
                    using (var crypto = new CryptoStream(source, decryptor, CryptoStreamMode.Read))
                    {
                        CopyStream(crypto, target, token);
                    }
                }
            }
            catch (CryptographicException)
            {
                throw new CryptographicException(
                    "Entschlüsselung fehlgeschlagen: Passwort falsch oder Datei beschädigt.");
            }
        }
#pragma warning restore CS0618

        // ------------------------------------------------------------------
        // Schluesselableitung
        // ------------------------------------------------------------------

        /// <summary>
        /// Leitet die Dateischluessel fuer Format v4 ab.
        ///
        /// <para><b>Der Kern des Formats.</b> Der Masterschluessel ist 64 Byte lang und
        /// wird in zwei Haelften geteilt: die erste speist ausschliesslich
        /// <c>HMAC(encMaster, "…/enc" ‖ salt ‖ iv)</c>, die zweite ausschliesslich
        /// <c>HMAC(macMaster, "…/mac" ‖ salt ‖ iv)</c>. Chiffrier- und
        /// Signaturschluessel koennen dadurch nie identisch sein - das war in Version
        /// 3.0 der Fall und ist der Grund fuer das neue Format.</para>
        ///
        /// <para>Salt und IV binden den Schluessel zusaetzlich an genau diese Datei:
        /// derselbe Klartext unter gleichem Passwort ergibt trotzdem jedes Mal einen
        /// anderen Schluessel und damit eine andere Chiffre.</para>
        /// </summary>
        internal void DeriveFileKeys(byte[] salt, byte[] iv, out byte[] encKey, out byte[] macKey)
        {
            byte[] encMaster = null;
            byte[] macMaster = null;
            try
            {
                SplitMasterKey(out encMaster, out macMaster);

                byte[] encSeed = BuildSeed(LabelEnc, salt, iv);
                byte[] macSeed = BuildSeed(LabelMac, salt, iv);
                try
                {
                    using (var encHmac = new HMACSHA256(encMaster))
                        encKey = encHmac.ComputeHash(encSeed);
                    using (var macHmac = new HMACSHA256(macMaster))
                        macKey = macHmac.ComputeHash(macSeed);
                }
                finally
                {
                    Array.Clear(encSeed, 0, encSeed.Length);
                    Array.Clear(macSeed, 0, macSeed.Length);
                }
            }
            finally
            {
                Array.Clear(encMaster, 0, encMaster.Length);
                Array.Clear(macMaster, 0, macMaster.Length);
            }
        }

        /// <summary>
        /// Reproduziert die Schluesselableitung der Version 3.0 fuer das Lesen von
        /// v3-Dateien.
        ///
        /// <para>3.0 hat <b>beide</b> Schluessel aus demselben Seed abgeleitet und
        /// damit denselben 32-Byte-Schluessel als Chiffrier- und als Signaturschluessel
        /// benutzt. Das wird hier unveraendert nachgebildet - sonst waeren alle mit 3.0
        /// erzeugten Dateien nicht mehr lesbar. Neu geschrieben wird ausschliesslich v4
        /// ueber <see cref="DeriveFileKeys"/>.</para>
        /// </summary>
        internal static void DeriveV3Keys(byte[] masterKey, byte[] salt, byte[] iv,
            out byte[] encKey, out byte[] macKey)
        {
            if (masterKey == null) throw new ArgumentNullException("masterKey");
            if (masterKey.Length != 2 * KeySize)
                throw new ArgumentException("Masterschlüssel muss 64 Byte lang sein.", "masterKey");

            byte[] seed = BuildSeed(LegacyV3Domain, salt, iv);
            try
            {
                // In 3.0 wurde der komplette 64-Byte-Masterschluessel als HMAC-Schluessel
                // benutzt - nicht eine Haelfte.
                using (var hmac = new HMACSHA256(masterKey))
                    encKey = hmac.ComputeHash(seed);

                macKey = (byte[])encKey.Clone();
            }
            finally
            {
                Array.Clear(seed, 0, seed.Length);
            }
        }

        /// <summary>Teilt den Masterschluessel in eine Chiffrier- und eine Signaturhaelfte.</summary>
        private void SplitMasterKey(out byte[] encMaster, out byte[] macMaster)
        {
            if (_masterKey == null || _masterKey.Length != 2 * KeySize)
                throw new ObjectDisposedException("PolyAES");

            encMaster = new byte[KeySize];
            macMaster = new byte[KeySize];
            Buffer.BlockCopy(_masterKey, 0, encMaster, 0, KeySize);
            Buffer.BlockCopy(_masterKey, KeySize, macMaster, 0, KeySize);
        }

        /// <summary>
        /// Baut den Ableitungs-Seed: Label, danach Salt und IV des Containers.
        /// Durch die feste Reihenfolge ist die Konkatenation eindeutig - Label, Salt
        /// und IV haben zwar feste Laengen, die explizite Reihenfolge macht es trotzdem
        /// unabhaengig von zukuenftigen Aenderungen.
        /// </summary>
        private static byte[] BuildSeed(byte[] label, byte[] salt, byte[] iv)
        {
            byte[] seed = new byte[label.Length + SaltSize + IvSize];
            Buffer.BlockCopy(label, 0, seed, 0, label.Length);
            Buffer.BlockCopy(salt, 0, seed, label.Length, SaltSize);
            Buffer.BlockCopy(iv, 0, seed, label.Length + SaltSize, IvSize);
            return seed;
        }

        // ------------------------------------------------------------------
        // Hilfsfunktionen
        // ------------------------------------------------------------------

        private static Aes CreateAes(byte[] key, byte[] iv)
        {
            Aes aes = Aes.Create();
            aes.KeySize = KeySize * 8;
            aes.BlockSize = IvSize * 8;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = key;
            aes.IV = iv;
            return aes;
        }

        private static byte[] BuildHeader(byte[] magic, byte[] salt, byte[] iv)
        {
            byte[] header = new byte[MagicSize + SaltSize + IvSize];
            Buffer.BlockCopy(magic, 0, header, 0, MagicSize);
            Buffer.BlockCopy(salt, 0, header, MagicSize, SaltSize);
            Buffer.BlockCopy(iv, 0, header, MagicSize + SaltSize, IvSize);
            return header;
        }

        /// <summary>
        /// Kopiert mit festem Puffer. Der Puffer wird anschliessend geleert, weil er beim
        /// Entschluesseln Klartext enthaelt.
        /// </summary>
        private static void CopyStream(Stream src, Stream dst, CancellationToken token)
        {
            byte[] buffer = new byte[CopyBufferSize];
            try
            {
                int read;
                while ((read = src.Read(buffer, 0, buffer.Length)) > 0)
                {
                    // In grossen Bloecken pruefen, damit ein Abbruch nicht erst nach
                    // dem Ende einer 64-KiB-Kongruenz wirkt.
                    token.ThrowIfCancellationRequested();
                    dst.Write(buffer, 0, read);
                }
            }
            finally
            {
                Array.Clear(buffer, 0, buffer.Length);
            }
        }

        private static int ReadFully(Stream stream, byte[] buffer, int offset, int count)
        {
            int total = 0;
            while (total < count)
            {
                int read = stream.Read(buffer, offset + total, count - total);
                if (read <= 0) break;
                total += read;
            }
            return total;
        }

        private static byte[] ReadExactly(Stream stream, int count, string sourcePath)
        {
            byte[] buffer = new byte[count];
            try
            {
                if (ReadFully(stream, buffer, 0, count) != count)
                    throw new CryptographicException(
                        "Die Datei ist beschädigt (Header unvollständig): '" + sourcePath + "'.");
            }
            catch
            {
                Array.Clear(buffer, 0, buffer.Length);
                throw;
            }
            return buffer;
        }

        /// <summary>
        /// Ersetzt das Ziel atomar.
        ///
        /// <para><b>Warum nicht einfach Delete und Move:</b> zwischen beiden Aufrufen
        /// gibt es ein Fenster, in dem die Zieldatei nicht existiert. Stuerzt der
        /// Rechner dort ab, ist der Inhalt weg - bei einer Datei, die gerade
        /// entschluesselt wurde, ein Totalverlust. <see cref="File.Replace"/> tauscht
        /// dagegen auf Dateisystemebene aus: die alte Datei bleibt sichtbar, bis die
        /// neue vollstaendig an ihrem Platz steht.</para>
        ///
        /// <para>Falls Replace nicht unterstuetzt wird (exotische Netzwerk- oder
        /// FAT-Volumes), bleibt der bisherige Weg als Rueckfall - die Zieldatei geht
        /// dabei nicht verloren, es entfaellt nur die Unterbrechungsfreiheit.</para>
        /// </summary>
        private static void CommitFile(string tmp, string targetPath, bool overwrite)
        {
            if (File.Exists(targetPath))
            {
                if (!overwrite)
                    throw new IOException("Die Zieldatei existiert bereits und wurde nicht überschrieben: '" + targetPath + "'.");

                try
                {
                    // backupFileName = null: die alte Datei soll nicht behalten werden.
                    File.Replace(tmp, targetPath, null);
                    return;
                }
                catch (PlatformNotSupportedException)
                {
                    // Auf dem Dateisystem nicht verfuegbar - unten folgt der Rueckfall.
                }
                catch (NotSupportedException)
                {
                }
                catch (IOException)
                {
                    // Manche Netzlaufwerke werfen das auch. Ein Fehler, der wirklich
                    // bedeutet "Ziel ist weg", faellt durch und wird weiter oben
                    // sichtbar, statt hier still zu werden.
                }

                File.Delete(targetPath);
            }

            File.Move(tmp, targetPath);
        }

        private static void SafeDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { /* Aufraeumen ist optional - nicht weiter melden. */ }
        }

        private static bool AreSamePath(string a, string b)
        {
            return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
        }

        private static byte[] ToUtf8(char[] value)
        {
            return new UTF8Encoding(false).GetBytes(value);
        }

        private static string ToLegacyString(char[] value)
        {
            // Nur fuer das Legacy-Format, das zwingend einen String braucht
            // (Encoding.ASCII.GetBytes). Wird nicht zwischengespeichert.
            return new string(value);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException("PolyAES");
        }

        // ------------------------------------------------------------------
        // Vergleichsfunktionen mit konstanter Laufzeit
        // ------------------------------------------------------------------

        /// <summary>Vergleicht zwei Byte-Arrays ohne fruehzeitigen Abbruch.</summary>
        public static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null) return false;
            // Die Laenge ist keine Geheimnis, der Vergleich selbst schon.
            if (a.Length != b.Length) return false;
            return FixedTimeEquals(0, a, 0, b, a.Length);
        }

        private static bool FixedTimeEquals(int aOffset, byte[] a, int bOffset, byte[] b, int count)
        {
            if (a == null || b == null) return false;
            if (count < 0 || aOffset < 0 || bOffset < 0) return false;
            if (aOffset + count > a.Length || bOffset + count > b.Length) return false;

            int acc = 0;
            for (int i = 0; i < count; i++)
                acc |= a[aOffset + i] ^ b[bOffset + i];
            return acc == 0;
        }

        // ------------------------------------------------------------------
        // Legacy-Hilfsfunktionen (bestimmen das v1-Format, unveraendert)
        // ------------------------------------------------------------------

        private static string GetHashSha256Legacy(string text)
        {
            byte[] bytes = Encoding.Unicode.GetBytes(text);
            using (var sha = new SHA256Managed())
            {
                byte[] hash = sha.ComputeHash(bytes);
                var sb = new StringBuilder();
                foreach (byte b in hash)
                    sb.AppendFormat("{0:x2}", b);
                return sb.ToString();
            }
        }

        private static string UplowmeLegacy(string text)
        {
            var sb = new StringBuilder();
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
                        case '3': sb.Append('§'); break; // U+00A7
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

        // ------------------------------------------------------------------
        // Interne Stream-Helfer
        // ------------------------------------------------------------------

        /// <summary>
        /// Leitet Schreibzugriffe an den inneren Stream weiter und berechnet dabei
        /// inkrementell einen Hash. <see cref="Dispose"/> schliesst den inneren Stream
        /// bewusst nicht - die Signatur wird erst nach dem Schreiben angehaengt.
        /// </summary>
        private sealed class HashingWriteStream : Stream
        {
            private readonly Stream _inner;
            private readonly HashAlgorithm _hash;

            public HashingWriteStream(Stream inner, HashAlgorithm hash)
            {
                _inner = inner;
                _hash = hash;
            }

            public override bool CanRead { get { return false; } }
            public override bool CanSeek { get { return false; } }
            public override bool CanWrite { get { return true; } }
            public override long Length { get { throw new NotSupportedException(); } }

            public override long Position
            {
                get { throw new NotSupportedException(); }
                set { throw new NotSupportedException(); }
            }

            public override void Flush() { _inner.Flush(); }

            public override void Write(byte[] buffer, int offset, int count)
            {
                _inner.Write(buffer, offset, count);
                _hash.TransformBlock(buffer, offset, count, null, 0);
            }

            public override int Read(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
            public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
            public override void SetLength(long value) { throw new NotSupportedException(); }

            protected override void Dispose(bool disposing)
            {
                // _inner und _hash bleiben offen - beides gehoert dem Aufrufer.
                base.Dispose(disposing);
            }
        }

        /// <summary>
        /// Liest hoechstens <c>_length</c> Bytes aus dem inneren Stream und schliesst ihn
        /// beim Dispose nicht. Wird gebraucht, damit <see cref="CryptoStream"/> nicht ueber
        /// den Ciphertext hinaus in die Signatur liest.
        /// </summary>
        private sealed class LengthLimitedStream : Stream
        {
            private readonly Stream _inner;
            private long _remaining;

            public LengthLimitedStream(Stream inner, long length)
            {
                _inner = inner;
                _remaining = length;
            }

            public override bool CanRead { get { return true; } }
            public override bool CanSeek { get { return false; } }
            public override bool CanWrite { get { return false; } }
            public override long Length { get { throw new NotSupportedException(); } }

            public override long Position
            {
                get { throw new NotSupportedException(); }
                set { throw new NotSupportedException(); }
            }

            public override void Flush() { }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (_remaining <= 0) return 0;
                if (count > _remaining) count = (int)_remaining;

                int read = _inner.Read(buffer, offset, count);
                if (read > 0) _remaining -= read;
                return read;
            }

            public override void Write(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
            public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
            public override void SetLength(long value) { throw new NotSupportedException(); }

            protected override void Dispose(bool disposing)
            {
                // inneren Stream nicht schliessen - gehoert dem Aufrufer.
                base.Dispose(disposing);
            }
        }
    }
}