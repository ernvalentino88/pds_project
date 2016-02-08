using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;


namespace Server
{
    class RSAUtility
    {
        RSACryptoServiceProvider rsa;
        public RSAUtility() {
            rsa = new RSACryptoServiceProvider();
       }

        public byte[] RSAEncrypt(byte[] DataToEncrypt,bool DoOAEPPadding)
        {
            try
            {
                byte[] encryptedData;
                encryptedData = rsa.Encrypt(DataToEncrypt, DoOAEPPadding);
                
                return encryptedData;
            }
            //Catch and display a CryptographicException  
            //to the console.
            catch (CryptographicException e)
            {
                return null;
            }
    }
    public void set_public_key(byte[] modulus,byte[] exponent){
                RSAParameters param=new RSAParameters();
                param.Modulus=modulus;
                param.Exponent=exponent;
                rsa.ImportParameters(param);
             }

    }
    
}
