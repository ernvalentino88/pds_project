using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Security.Cryptography;

namespace Utility
{
    public class Security
    {
        public static RSACryptoServiceProvider generateRSAKey()
        {
            RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(2048);
            RSAParameters param = rsa.ExportParameters(false);
            param.Exponent = new byte[] {1, 0, 1};

            return rsa;
        }

        public static byte[] getModulus(RSA rsa)
        {
            return rsa.ExportParameters(false).Modulus;
        }

        public static byte[] getExponent(RSA rsa)
        {
            return rsa.ExportParameters(false).Exponent;
        }

        public static RSACryptoServiceProvider getPublicKey(byte[] modulus, byte[] exponent)
        {
            RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
            RSAParameters param = new RSAParameters();
            param.Modulus = modulus;
            param.Exponent = exponent;
            rsa.ImportParameters(param);

            return rsa;
        }

        public static byte[] RSADecrypt(RSACryptoServiceProvider rsa, byte[] DataToDecrypt, bool DoOAEPPadding)
        {
            try
            {
                byte[] decryptedData;
                decryptedData = rsa.Decrypt(DataToDecrypt, DoOAEPPadding);
                return decryptedData;
            }
            catch (CryptographicException)
            {
                return null;
            }
        }

        public static byte[] RSAEncrypt(RSACryptoServiceProvider rsa, byte[] DataToEncrypt, bool DoOAEPPadding)
        {
            try
            {
                byte[] encryptedData;
                encryptedData = rsa.Encrypt(DataToEncrypt, DoOAEPPadding);

                return encryptedData;
            }
            catch (CryptographicException)
            {
                return null;
            }
        }

        public static AesCryptoServiceProvider generateAESKey()
        {
            AesCryptoServiceProvider aes = new AesCryptoServiceProvider();
            aes.KeySize = 256;
            aes.GenerateKey();
            aes.GenerateIV();
            
            return aes;
        }

        public static AesCryptoServiceProvider getAESKey(byte[] Key, byte[] IV)
        {
            AesCryptoServiceProvider aes = new AesCryptoServiceProvider();
            aes.Key = Key;
            aes.IV = IV;

            return aes;
        }

        public static ICryptoTransform getEncryptor(Aes aes)
        {
            return aes.CreateEncryptor(aes.Key, aes.IV);
        }

        public static ICryptoTransform getDecryptor(Aes aes)
        {
            return aes.CreateDecryptor(aes.Key, aes.IV);
        }

        public static byte[] AESEncrypt(Aes aes, byte[] dataToEncrypt)
        {
            byte[] encryptedBytes = null;
            using (MemoryStream ms = new MemoryStream())
            {
                
                using (var cs = new CryptoStream(ms, getEncryptor(aes), CryptoStreamMode.Write))
                {
                    cs.Write(dataToEncrypt, 0, dataToEncrypt.Length);
                    cs.Close();
                }
                encryptedBytes = ms.ToArray();
                
            }

            return encryptedBytes;
        }

        public static byte[] AESDecrypt(Aes aes, byte[] dataToDecrypt)
        {
            byte[] decryptedBytes = null;
            using (MemoryStream ms = new MemoryStream())
            {

                using (var cs = new CryptoStream(ms, getDecryptor(aes), CryptoStreamMode.Write))
                {
                    cs.Write(dataToDecrypt, 0, dataToDecrypt.Length);
                    cs.Close();
                }
                decryptedBytes = ms.ToArray();

            }

            return decryptedBytes;
        }

        public static byte[] AESEncrypt(Aes aes, String plaintext)
        {
            byte[] dataToEncrypt = Encoding.UTF8.GetBytes(plaintext);
            byte[] encryptedBytes = null;
            using (MemoryStream ms = new MemoryStream())
            {

                using (var cs = new CryptoStream(ms, getEncryptor(aes), CryptoStreamMode.Write))
                {
                    cs.Write(dataToEncrypt, 0, dataToEncrypt.Length);
                    cs.Close();
                }
                encryptedBytes = ms.ToArray();

            }

            return encryptedBytes;
        }

        public static byte[] AESEncrypt(Aes aes, UInt64 n)
        {
            byte[] dataToEncrypt = BitConverter.GetBytes(n);
            byte[] encryptedBytes = null;
            using (MemoryStream ms = new MemoryStream())
            {

                using (var cs = new CryptoStream(ms, getEncryptor(aes), CryptoStreamMode.Write))
                {
                    cs.Write(dataToEncrypt, 0, dataToEncrypt.Length);
                    cs.Close();
                }
                encryptedBytes = ms.ToArray();

            }

            return encryptedBytes;
        }

        public static UInt64 AESDecryptUInt64(Aes aes, byte[] dataToDecrypt)
        {
            byte[] decryptedBytes = null;
            using (MemoryStream ms = new MemoryStream())
            {

                using (var cs = new CryptoStream(ms, getDecryptor(aes), CryptoStreamMode.Write))
                {
                    cs.Write(dataToDecrypt, 0, dataToDecrypt.Length);
                    cs.Close();
                }
                decryptedBytes = ms.ToArray();

            }

            return BitConverter.ToUInt64(decryptedBytes, 0);
        }

        public static String AESDecryptString(Aes aes, byte[] dataToDecrypt)
        {
            byte[] decryptedBytes = null;
            using (MemoryStream ms = new MemoryStream())
            {

                using (var cs = new CryptoStream(ms, getDecryptor(aes), CryptoStreamMode.Write))
                {
                    cs.Write(dataToDecrypt, 0, dataToDecrypt.Length);
                    cs.Close();
                }
                decryptedBytes = ms.ToArray();

            }

            return Encoding.UTF8.GetString(decryptedBytes);
        }

        public static byte[] XOR(byte[] a, byte[] b)
        {
            byte[] xored = (a.Length >= b.Length) ? new byte[a.Length] : new byte[b.Length];
            for (int i = 0; i < xored.Length; i++)
                xored[i] = (byte)(a[i] ^ b[i]);
            if (a.Length > b.Length)
            {
                for (int i = b.Length; i < a.Length; i++)
                {
                    xored[i] = a[i];
                }
            }
            if (b.Length > a.Length)
            {
                for (int i = a.Length; i < b.Length; i++)
                {
                    xored[i] = b[i];
                }
            }

            return xored;
        }

        public static String CalculateMD5Hash(String input)
        {
            // step 1, calculate MD5 hash from input
            MD5 md5 = MD5.Create();
            byte[] inputBytes = Encoding.ASCII.GetBytes(input);
            byte[] hash = md5.ComputeHash(inputBytes);
            // step 2, convert byte array to hex string
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < hash.Length; i++)
            {
                sb.Append(hash[i].ToString("X2"));
            }
            return sb.ToString();
        }
    }
}
