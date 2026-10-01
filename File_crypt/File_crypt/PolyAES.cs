/*
 * Copyright (C) 2011, Dextrey (0xDEADDEAD)
 * Removing this copyright notice is prohibited without permission from author
 * Using this code in your own software product, commercial or not is allowed
 */

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

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

        /// <summary>AES-256-CBC mit HMAC-SHA256, 3.0 (aktuelles Format).</summary>
        V3 = 3
    }

    /// <summary>
    /// Verschluesselung und Entschluesselung von Dateien.
    ///
    /// <para><b>Formate</b> (siehe SECURITY.md):</para>
    /// <list type="bullet">
    ///   <item><description><b>v3</b> (aktuell, schreiben + lesen):
    ///   <c>[magic "PCv3"][salt 32][iv 16][ciphertext][hmac-sha256 32]</c><br/>
    ///   AES-256-CBC, PKCS7, HMAC-SHA256 ueber Header + Ciphertext ("Encrypt-then-MAC").</description></item>
    ///   <item><description><b>v2</b> (2.0, nur lesen):
    ///   <c>[magic "PCv2"][salt 32][iv 16][ciphertext]</c><br/>
    ///   AES-256-CBC, PBKDF2-SHA256 mit 100.000 Iterationen. Ohne Integritaetsschutz.</description></item>
    ///   <item><description><b>v1 Legacy</b> (2011, nur lesen):
    ///   <c>[ciphertext][salt 32][iv 32]</c><br/>
    ///   Rijndael-256-CBC, PBKDF2-SHA1 mit 2.000 Iterationen,
    ///   Schluessel = <c>ASCII(uplowme(sha256hex(password)))</c>. Ohne Integritaetsschutz.</description></item>
    /// </list>
    ///
    /// <para><b>Schluesselableitung v3.</b> Aus dem Passwort wird einmal pro Vorgang ein
    /// 64-Byte-Masterschluessel abgeleitet (PBKDF2-HMAC-SHA256). Pro Datei werden daraus
    /// zwei|Schluessel per HMAC abgeleitet, getrennt nach Zweck ("enc"/"mac") und mit
    /// Salt + IV des Containers als Kontext. Das kostet die teure PBKDF2 nur einmal pro
    /// Lauf und haelt Ordneroperationen mit vielen Dateien schnell.</para>
    ///
    /// <para>Alle Methoden arbeiten streamend, der Speicherbedarf ist unabhaengig von der
    /// Dateigroesse. Zieldateien werden ueber eine temporaere Datei atomar ersetzt: bei
    /// einem Abbruch bleibt entweder die alte oder die neue Datei vollstaendig, nie eine
    /// halb geschriebene.</para>
    /// </summary>
    public sealed class PolyAES : IDisposable
    {
        private static readonly byte[] MagicV3 = { 0x50, 0x43, 0x76, 0x33 }; // "PCv3"
        private static readonly byte[] MagicV2 = { 0x50, 0x43, 0x76, 0x32 }; // "PCv2"

        private const int MagicSize = 4;
        private const int SaltSize = 32;
        private const int IvSize = 16;
        private const int KeySize = 32;
        private const int TagSize = 32;
        private const int V3HeaderSize = MagicSize + SaltSize + IvSize; // 52
        private const int CopyBufferSize = 64 * 1024;

        /// <summary>
        /// PBKDF2-Iterationen fuer die Master-Ableitung (v3).
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
            if (FixedTimeEquals(head, MagicV3))
                return ContainerFormat.V3;
            if (FixedTimeEquals(head, MagicV2))
                return ContainerFormat.V2;
            return ContainerFormat.Legacy;
        }

        /// <summary>
        /// Verschluesselt <paramref name="sourcePath"/> nach <paramref name="targetPath"/>.
        /// Das Ergebnis hat immer das Format v3.
        /// </summary>
        /// <param name="sourcePath">Klartextdatei.</param>
        /// <param name="targetPath">Ziel; wird atomar ersetzt.</param>
        /// <param name="overwrite">
        /// <c>false</c>: es wird nichts getan, wenn das Ziel existiert.
        /// <c>true</c>: das vorhandene Ziel wird ersetzt.
        /// </param>
        public void EncryptFile(string sourcePath, string targetPath, bool overwrite)
        {
            ThrowIfDisposed();
            if (sourcePath == null) throw new ArgumentNullException("sourcePath");
            if (targetPath == null) throw new ArgumentNullException("targetPath");

            // Klartext und Ziel duerfen nicht dieselbe Datei sein.
            if (AreSamePath(sourcePath, targetPath))
                throw new IOException("Quelldatei und Zieldatei sind identisch: '" + sourcePath + "'.");

            string tmp = targetPath + ".pctmp";
            try
            {
                using (var src = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, CopyBufferSize))
                {
                    using (var dst = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, CopyBufferSize))
                    {
                        EncryptV3(src, dst);
                        dst.Flush(true);
                    }
                }

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
        /// Das Eingangsformat (v3, v2 oder Legacy) wird automatisch erkannt.
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
            ThrowIfDisposed();
            if (sourcePath == null) throw new ArgumentNullException("sourcePath");
            if (targetPath == null) throw new ArgumentNullException("targetPath");

            if (AreSamePath(sourcePath, targetPath))
                throw new IOException("Quelldatei und Zieldatei sind identisch: '" + sourcePath + "'.");

            switch (DetectFormat(sourcePath))
            {
                case ContainerFormat.V3:
                    DecryptV3File(sourcePath, targetPath, overwrite);
                    break;
                case ContainerFormat.V2:
                    DecryptV2File(sourcePath, targetPath, overwrite);
                    break;
                case ContainerFormat.Legacy:
                    DecryptLegacyFile(sourcePath, targetPath, overwrite);
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
        // v3: AES-256-CBC + HMAC-SHA256 (Encrypt-then-MAC)
        // ------------------------------------------------------------------

        private void EncryptV3(Stream source, Stream target)
        {
            byte[] salt = new byte[SaltSize];
            byte[] iv = new byte[IvSize];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
                rng.GetBytes(iv);
            }

            byte[] header = BuildHeader(MagicV3, salt, iv);
            byte[] encKey = null;
            byte[] macKey = null;
            byte[] tag = null;

            try
            {
                DeriveFileKeys(salt, iv, "enc", out encKey, out macKey);

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
                        CopyStream(source, crypto);
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

        private void DecryptV3File(string sourcePath, string targetPath, bool overwrite)
        {
            string tmp = targetPath + ".pctmp";
            try
            {
                using (var src = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, CopyBufferSize))
                using (var dst = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, CopyBufferSize))
                {
                    byte[] header = ReadExactly(src, V3HeaderSize, sourcePath);
                    if (!FixedTimeEquals(0, header, 0, MagicV3, MagicSize))
                        throw new CryptographicException("Die Datei besitzt kein gültiges privateCrypt-Format.");

                    long cipherLen = src.Length - V3HeaderSize - TagSize;
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
                        DeriveFileKeys(salt, iv, "enc", out encKey, out macKey);

                        // Phase 1: Signatur pruefen, BEVOR irgendein Klartext entsteht.
                        VerifyTag(src, header, cipherLen, macKey, sourcePath);

                        // Phase 2: entschluesseln.
                        src.Position = V3HeaderSize;
                        using (var limited = new LengthLimitedStream(src, cipherLen))
                        using (var aes = CreateAes(encKey, iv))
                        using (var decryptor = aes.CreateDecryptor())
                        using (var crypto = new CryptoStream(limited, decryptor, CryptoStreamMode.Read))
                        {
                            CopyStream(crypto, dst);
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

                    src.Position = V3HeaderSize;
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
            }
        }

        // ------------------------------------------------------------------
        // v2: AES-256-CBC ohne Integritaetsschutz (nur lesend, seit 3.0)
        // ------------------------------------------------------------------

        private void DecryptV2File(string sourcePath, string targetPath, bool overwrite)
        {
            byte[] data = File.ReadAllBytes(sourcePath);
            byte[] plain = null;
            try
            {
                plain = DecryptV2(data, sourcePath);
                WriteAllBytesAtomic(plain, targetPath, overwrite);
            }
            finally
            {
                Array.Clear(data, 0, data.Length);
                if (plain != null) Array.Clear(plain, 0, plain.Length);
            }
        }

        private byte[] DecryptV2(byte[] data, string sourcePath)
        {
            if (data.Length < V3HeaderSize + 16)
                throw new CryptographicException(
                    "Die Datei ist beschädigt (nur " + data.Length + " Bytes).");

            byte[] salt = new byte[SaltSize];
            byte[] iv = new byte[IvSize];
            byte[] key = null;
            try
            {
                Buffer.BlockCopy(data, MagicSize, salt, 0, SaltSize);
                Buffer.BlockCopy(data, MagicSize + SaltSize, iv, 0, IvSize);

                int cipherLen = data.Length - V3HeaderSize;
                if (cipherLen % 16 != 0)
                    throw new CryptographicException("Die Datei ist beschädigt (unerwartete Blockgröße).");

                byte[] cipher = new byte[cipherLen];
                Buffer.BlockCopy(data, V3HeaderSize, cipher, 0, cipherLen);

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

                try
                {
                    using (var aes = CreateAes(key, iv))
                    using (var decryptor = aes.CreateDecryptor())
                        return decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
                }
                catch (CryptographicException)
                {
                    throw new CryptographicException(
                        "Entschlüsselung fehlgeschlagen: Passwort falsch oder Datei beschädigt.");
                }
            }
            finally
            {
                Array.Clear(salt, 0, salt.Length);
                Array.Clear(iv, 0, iv.Length);
                if (key != null) Array.Clear(key, 0, key.Length);
            }
        }

        // ------------------------------------------------------------------
        // v1 Legacy: Rijndael-256 (nur lesend, seit 2.0)
        // ------------------------------------------------------------------

        private void DecryptLegacyFile(string sourcePath, string targetPath, bool overwrite)
        {
            byte[] data = File.ReadAllBytes(sourcePath);
            byte[] plain = null;
            try
            {
                plain = DecryptLegacy(data);
                WriteAllBytesAtomic(plain, targetPath, overwrite);
            }
            finally
            {
                Array.Clear(data, 0, data.Length);
                if (plain != null) Array.Clear(plain, 0, plain.Length);
            }
        }

#pragma warning disable CS0618 // RijndaelManaged ist in .NET 6+ obsolet, auf .NET Framework 4.8 aber noetig
        // Legacy-Entschluesselung - exakt die urspruengliche Logik, damit Dateien aus
        // der 1.0-Version weiterhin geoeffnet werden koennen. Nur noch lesend.
        private byte[] DecryptLegacy(byte[] cipherText)
        {
            const int Trailing = 32; // Salt und IV stehen hinter dem Ciphertext
            const int LegacyBlockSize = 32; // Rijndael mit BlockSize 256

            if (cipherText.Length < (2 * Trailing) + LegacyBlockSize)
                throw new CryptographicException(
                    "Die Datei ist beschädigt (nur " + cipherText.Length + " Bytes, Legacy-Format erwartet mindestens 96).");

            int withSaltLen = cipherText.Length - Trailing;
            int actualLen = withSaltLen - Trailing;
            if (actualLen % LegacyBlockSize != 0)
                throw new CryptographicException("Die Datei ist beschädigt (unerwartete Blockgröße im Legacy-Format).");

            byte[] iv = new byte[Trailing];
            Buffer.BlockCopy(cipherText, withSaltLen, iv, 0, Trailing);

            byte[] salt = new byte[Trailing];
            Buffer.BlockCopy(cipherText, actualLen, salt, 0, Trailing);

            byte[] actualCipher = new byte[actualLen];
            Buffer.BlockCopy(cipherText, 0, actualCipher, 0, actualLen);

            byte[] key = Encoding.ASCII.GetBytes(UplowmeLegacy(GetHashSha256Legacy(ToString(_password))));
            byte[] derivedKey = null;

            try
            {
                using (var kdf = new Rfc2898DeriveBytes(key, salt, Pbkdf2IterationsLegacy))
                    derivedKey = kdf.GetBytes(32);

                using (var algo = new RijndaelManaged())
                {
                    algo.Mode = CipherMode.CBC;
                    algo.BlockSize = 256;
                    algo.Key = derivedKey;
                    algo.IV = iv;

                    try
                    {
                        using (var decryptor = algo.CreateDecryptor())
                            return decryptor.TransformFinalBlock(actualCipher, 0, actualCipher.Length);
                    }
                    catch (CryptographicException)
                    {
                        throw new CryptographicException(
                            "Entschlüsselung fehlgeschlagen: Passwort falsch oder Datei beschädigt.");
                    }
                }
            }
            finally
            {
                if (derivedKey != null) Array.Clear(derivedKey, 0, derivedKey.Length);
                Array.Clear(key, 0, key.Length);
                Array.Clear(salt, 0, salt.Length);
                Array.Clear(iv, 0, iv.Length);
                Array.Clear(actualCipher, 0, actualCipher.Length);
            }
        }
#pragma warning restore CS0618

        // ------------------------------------------------------------------
        // Schluesselableitung
        // ------------------------------------------------------------------

        /// <summary>
        /// Leitet Dateischluessel aus dem Master-Schluessel ab. Die Domänen-Trennung
        /// ("enc" / "mac") stellt sicher, dass derselbe Bytes-Bereich nie fuer beides
        /// verwendet wird; Salt und IV binden den Schluessel an genau diese Datei.
        /// </summary>
        private void DeriveFileKeys(byte[] salt, byte[] iv, string domain, out byte[] encKey, out byte[] macKey)
        {
            byte[] seed = new byte[domain.Length + SaltSize + IvSize];
            for (int i = 0; i < domain.Length; i++) seed[i] = (byte)domain[i];
            Buffer.BlockCopy(salt, 0, seed, domain.Length, SaltSize);
            Buffer.BlockCopy(iv, 0, seed, domain.Length + SaltSize, IvSize);

            try
            {
                encKey = ComputeMasterHmac(seed);
                macKey = ComputeMasterHmac(seed);
            }
            finally
            {
                Array.Clear(seed, 0, seed.Length);
            }
        }

        private byte[] ComputeMasterHmac(byte[] seed)
        {
            using (var hmac = new HMACSHA256(_masterKey))
                return hmac.ComputeHash(seed);
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
        private static void CopyStream(Stream src, Stream dst)
        {
            byte[] buffer = new byte[CopyBufferSize];
            try
            {
                int read;
                while ((read = src.Read(buffer, 0, buffer.Length)) > 0)
                    dst.Write(buffer, 0, read);
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
            if (ReadFully(stream, buffer, 0, count) != count)
                throw new CryptographicException(
                    "Die Datei ist beschädigt (Header unvollständig): '" + sourcePath + "'.");
            return buffer;
        }

        /// <summary>
        /// Ersetzt das Ziel atomar: erst wird <paramref name="tmp"/> vollstaendig geschrieben,
        /// dann das alte Ziel entfernt und die temporaere Datei umbenannt.
        /// </summary>
        private static void CommitFile(string tmp, string targetPath, bool overwrite)
        {
            if (File.Exists(targetPath))
            {
                if (!overwrite)
                    throw new IOException("Die Zieldatei existiert bereits und wurde nicht überschrieben: '" + targetPath + "'.");
                File.Delete(targetPath);
            }

            File.Move(tmp, targetPath);
        }

        private static void WriteAllBytesAtomic(byte[] data, string targetPath, bool overwrite)
        {
            string tmp = targetPath + ".pctmp";
            try
            {
                using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, CopyBufferSize))
                {
                    fs.Write(data, 0, data.Length);
                    fs.Flush(true);
                }
                CommitFile(tmp, targetPath, overwrite);
                tmp = null;
            }
            finally
            {
                if (tmp != null) SafeDelete(tmp);
            }
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

        private static string ToString(char[] value)
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