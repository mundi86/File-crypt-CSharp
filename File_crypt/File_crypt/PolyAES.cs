/*
 * Copyright (C) 2011, Dextrey (0xDEADDEAD)
 * Removing this copyright notice is prohibited without permission from author
 * Using this code in your own software product, commercial or not is allowed
 */

using System;
using System.Security.Cryptography;
using System.Text;

namespace crytec
{
    class PolyAES
    {
        // New encrypted file format (v2):
        //   [magic 4 bytes: "PCv2"] [salt 32 bytes] [IV 16 bytes] [ciphertext]
        //   AES-256-CBC, PKCS7 padding
        //   PBKDF2-SHA256 key derivation, 100,000 iterations
        //
        // Legacy format (v1, read-only):
        //   [ciphertext] [salt 32 bytes] [IV 32 bytes]
        //   Rijndael-256 CBC, PBKDF2-SHA1 2000 iterations
        //   Key derived via uplowme(SHA256(password))

        private static readonly byte[] MagicV2 = { 0x50, 0x43, 0x76, 0x32 }; // "PCv2"
        private const int SaltSize = 32;
        private const int IvSize = 16;
        private const int KeySize = 32;
        private const int Pbkdf2Iterations = 100_000;

        public byte[] PolyAES256Encrypt(byte[] plainText, string password)
        {
            byte[] salt = new byte[SaltSize];
            RandomNumberGenerator.Create().GetBytes(salt);

            using (var aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.BlockSize = 128;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.GenerateIV();

                using (var kdf = new Rfc2898DeriveBytes(password, salt, Pbkdf2Iterations, HashAlgorithmName.SHA256))
                {
                    aes.Key = kdf.GetBytes(KeySize);
                }

                byte[] iv = aes.IV; // 16 bytes
                byte[] derivedKey = aes.Key;
                try
                {
                    using (var transform = aes.CreateEncryptor())
                    {
                        byte[] cipherText = transform.TransformFinalBlock(plainText, 0, plainText.Length);

                        // Layout: magic(4) + salt(32) + IV(16) + ciphertext
                        byte[] result = new byte[MagicV2.Length + SaltSize + IvSize + cipherText.Length];
                        int pos = 0;
                        Buffer.BlockCopy(MagicV2, 0, result, pos, MagicV2.Length); pos += MagicV2.Length;
                        Buffer.BlockCopy(salt, 0, result, pos, SaltSize);          pos += SaltSize;
                        Buffer.BlockCopy(iv, 0, result, pos, IvSize);              pos += IvSize;
                        Buffer.BlockCopy(cipherText, 0, result, pos, cipherText.Length);
                        return result;
                    }
                }
                finally
                {
                    // Clear key material from memory
                    if (derivedKey != null) Array.Clear(derivedKey, 0, derivedKey.Length);
                    Array.Clear(salt, 0, salt.Length);
                }
            }
        }

        public byte[] PolyAES256Decrypt(byte[] data, string password)
        {
            // Detect format by magic header
            if (data.Length >= MagicV2.Length
                && data[0] == MagicV2[0] && data[1] == MagicV2[1]
                && data[2] == MagicV2[2] && data[3] == MagicV2[3])
            {
                return DecryptV2(data, password);
            }
            else
            {
                return DecryptLegacy(data, password);
            }
        }

        private byte[] DecryptV2(byte[] data, string password)
        {
            int minLen = MagicV2.Length + SaltSize + IvSize + 1;
            if (data.Length < minLen)
                throw new CryptographicException("File is too short to be a valid encrypted file.");

            int pos = MagicV2.Length;
            byte[] salt = new byte[SaltSize];
            Buffer.BlockCopy(data, pos, salt, 0, SaltSize); pos += SaltSize;

            byte[] iv = new byte[IvSize];
            Buffer.BlockCopy(data, pos, iv, 0, IvSize); pos += IvSize;

            int cipherLen = data.Length - pos;
            byte[] cipherText = new byte[cipherLen];
            Buffer.BlockCopy(data, pos, cipherText, 0, cipherLen);

            using (var aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.BlockSize = 128;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.IV = iv;

                byte[] derivedKey;
                using (var kdf = new Rfc2898DeriveBytes(password, salt, Pbkdf2Iterations, HashAlgorithmName.SHA256))
                {
                    derivedKey = kdf.GetBytes(KeySize);
                    aes.Key = derivedKey;
                }

                try
                {
                    using (var transform = aes.CreateDecryptor())
                    {
                        return transform.TransformFinalBlock(cipherText, 0, cipherText.Length);
                    }
                }
                finally
                {
                    // Clear key material from memory
                    Array.Clear(derivedKey, 0, derivedKey.Length);
                    Array.Clear(salt, 0, salt.Length);
                    Array.Clear(iv, 0, iv.Length);
                }
            }
        }

        // Legacy decrypt — keeps the old Rijndael-256 logic exactly as it was
        // so that files encrypted with the original code can still be opened.
#pragma warning disable CS0618 // RijndaelManaged is obsolete in .NET 6+, but fine on .NET Framework 4.8
        private byte[] DecryptLegacy(byte[] cipherText, string password)
        {
            byte[] key = Encoding.ASCII.GetBytes(UplowmeLegacy(GetHashSha256Legacy(password)));

            // Layout: [ciphertext] [salt 32 bytes] [IV 32 bytes]
            byte[] iv = new byte[32];
            Buffer.BlockCopy(cipherText, cipherText.Length - 32, iv, 0, 32);

            int withSaltLen = cipherText.Length - 32;
            byte[] salt = new byte[32];
            Buffer.BlockCopy(cipherText, withSaltLen - 32, salt, 0, 32);

            int actualLen = withSaltLen - 32;
            byte[] actualCipher = new byte[actualLen];
            Buffer.BlockCopy(cipherText, 0, actualCipher, 0, actualLen);

            using (var algo = new RijndaelManaged())
            {
                algo.Mode = CipherMode.CBC;
                algo.BlockSize = 256;
                algo.IV = iv;

                byte[] derivedKey;
                using (var kdf = new Rfc2898DeriveBytes(key, salt, 2000))
                {
                    derivedKey = kdf.GetBytes(32);
                    algo.Key = derivedKey;
                }

                try
                {
                    using (var transform = algo.CreateDecryptor())
                    {
                        return transform.TransformFinalBlock(actualCipher, 0, actualCipher.Length);
                    }
                }
                finally
                {
                    // Clear key material from memory
                    Array.Clear(derivedKey, 0, derivedKey.Length);
                    Array.Clear(key, 0, key.Length);
                    Array.Clear(salt, 0, salt.Length);
                    Array.Clear(iv, 0, iv.Length);
                }
            }
        }
#pragma warning restore CS0618

        private string GetHashSha256Legacy(string text)
        {
            byte[] bytes = Encoding.Unicode.GetBytes(text);
            using (var sha = new SHA256Managed())
            {
                byte[] hash = sha.ComputeHash(bytes);
                var sb = new System.Text.StringBuilder();
                foreach (byte b in hash)
                    sb.AppendFormat("{0:x2}", b);
                return sb.ToString();
            }
        }

        private string UplowmeLegacy(string text)
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
                        default:  sb.Append(char.ToUpper(c)); break;
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
