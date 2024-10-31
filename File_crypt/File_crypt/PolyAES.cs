/*
* Copyright (C) 2011, Dextrey (0xDEADDEAD)
* Removing this copyright notice is prohibited without permission from author
* Using this code in your own software product, commercial or not is allowed
*/

using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace crytec
{

    class PolyAES
    {


        /// <summary>
        /// verschlüsseln
        /// </summary>
        /// <param name="plainText"></param>
        /// <param name="Key"></param>
        /// <returns></returns>
        public byte[] PolyAES256Encrypt(byte[] plainText, string Key)
        {
            byte[] salt;
            SymmetricAlgorithm algo = new RijndaelManaged();
            RNGCryptoServiceProvider rngAlgo = new RNGCryptoServiceProvider();
            algo.Mode = CipherMode.CBC;
            byte[] key = System.Text.Encoding.ASCII.GetBytes(uplowme(getHashSha256(Key)));


            algo.GenerateIV();
            algo.BlockSize = 256;
            salt = new byte[32];
            rngAlgo.GetBytes(salt);
            Rfc2898DeriveBytes pwDeriveAlg = new Rfc2898DeriveBytes(key, salt, 2000);
            algo.Key = pwDeriveAlg.GetBytes(32);

            ICryptoTransform encTransform = algo.CreateEncryptor();

            byte[] enced = encTransform.TransformFinalBlock(plainText, 0, plainText.Length);

            int origLength = enced.Length;
            Array.Resize(ref enced, enced.Length + salt.Length);
            Buffer.BlockCopy(salt, 0, enced, origLength, salt.Length);

            origLength = enced.Length;
            Array.Resize(ref enced, enced.Length + algo.IV.Length);
            Buffer.BlockCopy(algo.IV, 0, enced, origLength, algo.IV.Length);

            return enced;
        }

        /// <summary>
        /// entschlüsseln
        /// </summary>
        /// <param name="cipherText"></param>
        /// <param name="Key"></param>
        /// <returns></returns>
        public byte[] PolyAES256Decrypt(byte[] cipherText, string Key)
        {
            byte[] salt;
            SymmetricAlgorithm algo = new RijndaelManaged();

            algo.Mode = CipherMode.CBC;
            algo.BlockSize = 256;
            RNGCryptoServiceProvider rngAlgo = new RNGCryptoServiceProvider();
            byte[] key = System.Text.Encoding.ASCII.GetBytes(uplowme(getHashSha256(Key)));
            byte[] cipherTextWithSalt = new byte[1];
            byte[] encSalt = new byte[1];
            byte[] origCipherText = new byte[1];
            byte[] encIv = new byte[1];

            Array.Resize(ref encIv, 32);
            Buffer.BlockCopy(cipherText, (int)(cipherText.Length - 32), encIv, 0, 32);
            Array.Resize(ref cipherTextWithSalt, (int)(cipherText.Length - 32));
            Buffer.BlockCopy(cipherText, 0, cipherTextWithSalt, 0, (int)(cipherText.Length - 32));

            Array.Resize(ref encSalt, 32);
            Buffer.BlockCopy(cipherTextWithSalt, (int)(cipherTextWithSalt.Length - 32), encSalt, 0, 32);
            Array.Resize(ref origCipherText, (int)(cipherTextWithSalt.Length - 32));
            Buffer.BlockCopy(cipherTextWithSalt, 0, origCipherText, 0, (int)(cipherTextWithSalt.Length - 32));

            algo.IV = encIv;
            salt = encSalt;
            Rfc2898DeriveBytes pwDeriveAlg = new Rfc2898DeriveBytes(key, salt, 2000);
            algo.Key = pwDeriveAlg.GetBytes(32);

            ICryptoTransform decTransform = algo.CreateDecryptor();
            byte[] plainText = decTransform.TransformFinalBlock(origCipherText, 0, origCipherText.Length);
            return plainText;
        }

        /// <summary>
        /// Verschlüsseln
        /// </summary>
        /// <param name="plainText"></param>
        /// <param name="Key"></param>
        /// <returns></returns>
        public byte[] PolyAES128Encrypt(byte[] plainText, string Key)
        {
            byte[] salt;
            SymmetricAlgorithm algo = new RijndaelManaged();
            RNGCryptoServiceProvider rngAlgo = new RNGCryptoServiceProvider();
            algo.Mode = CipherMode.CBC;
            byte[] key = System.Text.Encoding.ASCII.GetBytes(getHashSha256(Key));

            algo.GenerateIV();
            salt = new byte[32];
            rngAlgo.GetBytes(salt);
            Rfc2898DeriveBytes pwDeriveAlg = new Rfc2898DeriveBytes(key, salt, 2000);
            algo.Key = pwDeriveAlg.GetBytes(32);

            ICryptoTransform encTransform = algo.CreateEncryptor();

            byte[] enced = encTransform.TransformFinalBlock(plainText, 0, plainText.Length);

            int origLength = enced.Length;
            Array.Resize(ref enced, enced.Length + salt.Length);
            Buffer.BlockCopy(salt, 0, enced, origLength, salt.Length);

            origLength = enced.Length;
            Array.Resize(ref enced, enced.Length + algo.IV.Length);
            Buffer.BlockCopy(algo.IV, 0, enced, origLength, algo.IV.Length);

            return enced;
        }

        /// <summary>
        /// entschlüsseln
        /// </summary>
        /// <param name="cipherText"></param>
        /// <param name="Key"></param>
        /// <returns></returns>
        public byte[] PolyAES128Decrypt(byte[] cipherText, string Key)
        {
            byte[] salt;
            SymmetricAlgorithm algo = new RijndaelManaged();
            algo.Mode = CipherMode.CBC;
            RNGCryptoServiceProvider rngAlgo = new RNGCryptoServiceProvider();
            byte[] key = System.Text.Encoding.ASCII.GetBytes(getHashSha256(Key));
            byte[] cipherTextWithSalt = new byte[1];
            byte[] encSalt = new byte[1];
            byte[] origCipherText = new byte[1];
            byte[] encIv = new byte[1];

            Array.Resize(ref encIv, 16);
            Buffer.BlockCopy(cipherText, (int)(cipherText.Length - 16), encIv, 0, 16);
            Array.Resize(ref cipherTextWithSalt, (int)(cipherText.Length - 16));
            Buffer.BlockCopy(cipherText, 0, cipherTextWithSalt, 0, (int)(cipherText.Length - 16));

            Array.Resize(ref encSalt, 32);
            Buffer.BlockCopy(cipherTextWithSalt, (int)(cipherTextWithSalt.Length - 32), encSalt, 0, 32);
            Array.Resize(ref origCipherText, (int)(cipherTextWithSalt.Length - 32));
            Buffer.BlockCopy(cipherTextWithSalt, 0, origCipherText, 0, (int)(cipherTextWithSalt.Length - 32));

            algo.IV = encIv;
            salt = encSalt;
            Rfc2898DeriveBytes pwDeriveAlg = new Rfc2898DeriveBytes(key, salt, 2000);
            algo.Key = pwDeriveAlg.GetBytes(32);

            ICryptoTransform decTransform = algo.CreateDecryptor();
            byte[] plainText = decTransform.TransformFinalBlock(origCipherText, 0, origCipherText.Length);
            return plainText;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public string getHashSha256(string text)
        {
            byte[] bytes = Encoding.Unicode.GetBytes(text);
            SHA256Managed hashstring = new SHA256Managed();
            byte[] hash = hashstring.ComputeHash(bytes);
            string hashString = string.Empty;
            foreach (byte x in hash)
            {
                hashString += String.Format("{0:x2}", x);
            }
            return hashString;
        }

        public string hashme(string text)
        {
            Random rand = new Random();
            string data = string.Empty;


            foreach (char c in text)
            {
                if (rand.NextDouble() >= 0.5)
                    data = data + c.ToString().ToUpper();
                else
                    data = data + c.ToString();

            }

            return data;
        }



        public string uplowme(string text)
        {
            Random rand = new Random();
            string data = string.Empty;
            bool myswitch = true;

            foreach (char c in text)
            {
                if (myswitch)
                {
                    if (IsNumber(c.ToString()))
                    {
                        if (c.ToString() == "0")
                            data = data + "=";
                        if (c.ToString() == "1")
                            data = data + "!";
                        if (c.ToString() == "2")
                            data = data + "<";
                        if (c.ToString() == "3")
                            data = data + "§";
                        if (c.ToString() == "4")
                            data = data + "$";
                        if (c.ToString() == "5")
                            data = data + "%";
                        if (c.ToString() == "6")
                            data = data + "&";
                        if (c.ToString() == "7")
                            data = data + "/";
                        if (c.ToString() == "8")
                            data = data + "(";
                        if (c.ToString() == "9")
                            data = data + ")";
                    }
                    else
                    {
                        data = data + c.ToString().ToUpper();
                    }

                    myswitch = false;
                }
                else
                { data = data + c.ToString().ToLower(); myswitch = true; }
            }

            return data;
        }


        bool IsNumber(string text)
        {
            Regex regex = new Regex(@"^[-+]?[0-9]*\.?[0-9]+$");
            return regex.IsMatch(text);
        }
    }
}