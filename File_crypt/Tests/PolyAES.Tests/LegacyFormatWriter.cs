using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace crytec.Tests
{
    /// <summary>
    /// Erzeugt Dateien in den alten Container-Formaten.
    ///
    /// <para><b>Warum eine eigene Implementierung statt des Produktivcodes?</b>
    /// Ein Test, der denselben Aufruf benutzt wie die Funktion, die er prueft,
    /// bestaetigt nur, dass diese Funktion sich selbst konsistent verhaelt. Ein
    /// Fehler in der Ableitung wird dann auf beiden Seiten reproduziert und der
    /// Test bleibt gruen. Deshalb sind die Formate hier unabhaengig nachgebaut -
    /// wie es bei den v1-Vektoren im Repository bereits Methode ist.</para>
    ///
    /// <para>Besonders wichtig fuer <see cref="MakeV3File"/>: es bildet genau den
    /// Fehler von Version 3.0 nach, also <b>denselben</b> Schluessel fuer
    /// Verschluesselung und Signatur. Nur so laesst sich pruefen, dass privateCrypt
    /// 4.0 eine echte 3.0-Datei oeffnen kann - und dass es sie nicht etwa mit der
    /// korrigierten Ableitung versucht und scheitert.</para>
    /// </summary>
    internal static class LegacyFormatWriter
    {
        /// <summary>
        /// Erzeugt eine Datei im Format v3.0: PCv3, Salt, IV, Ciphertext, HMAC -
        /// mit dem gemeinsamen Schluessel fuer Cipher und MAC.
        /// </summary>
        internal static string MakeV3File(string path, byte[] plain, string password)
        {
            const string pw = "geheim123";
            if (password != pw)
                throw new ArgumentException("Fester Testschluessel erwartet: " + pw);

            byte[] magic = { 0x50, 0x43, 0x76, 0x33 }; // PCv3
            byte[] salt = new byte[32];
            byte[] iv = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
                rng.GetBytes(iv);
            }

            // Schluessel 1:1 wie privateCrypt 3.0 - PBKDF2 ueber den festen
            // Master-Salt, 300.000 Iterationen, 64 Byte.
            byte[] master;
            using (var kdf = new Rfc2898DeriveBytes(
                new UTF8Encoding(false).GetBytes(pw),
                Encoding.UTF8.GetBytes("privateCrypt/v3/master-key-salt"),
                300000, HashAlgorithmName.SHA256))
            {
                master = kdf.GetBytes(64);
            }

            // Seed: "enc" + Salt + IV. Genau so hat 3.0 beide Schluessel
            // abgeleitet - deshalb sind sie identisch.
            byte[] seed = new byte[3 + 32 + 16];
            seed[0] = (byte)'e'; seed[1] = (byte)'n'; seed[2] = (byte)'c';
            Buffer.BlockCopy(salt, 0, seed, 3, 32);
            Buffer.BlockCopy(iv, 0, seed, 35, 16);

            byte[] key;
            using (var hmac = new HMACSHA256(master))
                key = hmac.ComputeHash(seed);

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

            using (var mac = new HMACSHA256(key))
            {
                mac.TransformBlock(magic, 0, 4, null, 0);
                mac.TransformBlock(salt, 0, 32, null, 0);
                mac.TransformBlock(iv, 0, 16, null, 0);
                mac.TransformBlock(cipher, 0, cipher.Length, null, 0);
                mac.TransformFinalBlock(new byte[0], 0, 0);

                using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
                {
                    fs.Write(magic, 0, 4);
                    fs.Write(salt, 0, 32);
                    fs.Write(iv, 0, 16);
                    fs.Write(cipher, 0, cipher.Length);
                    fs.Write(mac.Hash, 0, 32);
                }
            }

            return path;
        }

        /// <summary>
        /// Erzeugt eine Datei im Format v2.0: PCv2, Salt, IV, Ciphertext -
        /// ohne Signatur, PBKDF2-SHA256 mit 100.000 Iterationen.
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
            using (var kdf = new Rfc2898DeriveBytes(
                new UTF8Encoding(false).GetBytes(password), salt, 100000, HashAlgorithmName.SHA256))
            {
                key = kdf.GetBytes(32);
            }

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
                fs.Write(magic, 0, 4);
                fs.Write(salt, 0, 32);
                fs.Write(iv, 0, 16);
                fs.Write(cipher, 0, cipher.Length);
            }

            return path;
        }
    /// <summary>
        /// Erzeugt eine Datei im Format v2.0 aus einer Vorlage, die als Datei
        /// vorliegt. Streamt - der Test soll an grossen Daten nichts selbst in
        /// den Speicher laden.
        /// </summary>
        internal static string MakeV2FileFrom(string path, string plainPath, string password)
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
            using (var kdf = new Rfc2898DeriveBytes(
                new UTF8Encoding(false).GetBytes(password), salt, 100000, HashAlgorithmName.SHA256))
            {
                key = kdf.GetBytes(32);
            }

            using (var aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.BlockSize = 128;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = key;
                aes.IV = iv;

                using (var enc = aes.CreateEncryptor())
                using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
                {
                    fs.Write(magic, 0, 4);
                    fs.Write(salt, 0, 32);
                    fs.Write(iv, 0, 16);

                    using (var src = new FileStream(plainPath, FileMode.Open, FileAccess.Read))
                    using (var crypto = new CryptoStream(src, enc, CryptoStreamMode.Read))
                    {
                        // CopyTo ruft FlushFinalBlock selbst auf; ein zusaetzlicher
                        // Aufruf waere der zweite und wuerde eine Ausnahme werfen.
                        crypto.CopyTo(fs);
                    }
                }
            }

            return path;
        }
    }
}