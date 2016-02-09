using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace Utility
{
    class Security
    {
        public static RSACryptoServiceProvider generateRSAKey()
        {
            RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(2048);
            RSAParameters param = rsa.ExportParameters(false);
            param.Exponent = new byte[] {1, 0, 1};

            return rsa;
        }

        public static byte[] getModulus(RSACryptoServiceProvider rsa)
        {
            return rsa.ExportParameters(false).Modulus;
        }

        public static byte[] getExponent(RSACryptoServiceProvider rsa)
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

        public static ICryptoTransform getEncryptor(AesCryptoServiceProvider aes)
        {
            return aes.CreateEncryptor(aes.Key, aes.IV);
        }

        public static ICryptoTransform getDecryptor(AesCryptoServiceProvider aes)
        {
            return aes.CreateDecryptor(aes.Key, aes.IV);
        }
    }
}
