using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace Client
{
    class RSA
    {
        private RSACryptoServiceProvider rsa;

        public RSA()
        {
            this.rsa = new RSACryptoServiceProvider(2048);
        }

        public RSAParameters getPublicKey()
        {
            return this.rsa.ExportParameters(false);
        }

        public RSAParameters getPrivateKey()
        {
            return this.rsa.ExportParameters(true);
        }

        public byte[] getModulus()
        {
            return this.rsa.ExportParameters(false).Modulus;
        }

        public byte[] getExponent()
        {
            return this.rsa.ExportParameters(false).Exponent;
        }

        public byte[] RSADecrypt(byte[] DataToDecrypt, bool DoOAEPPadding)
        {
            try
            {
                byte[] decryptedData;
                decryptedData = this.rsa.Decrypt(DataToDecrypt, DoOAEPPadding);
                return decryptedData;
            }
            catch (CryptographicException e)
            {
                return null;
            }

        }
    }
}
