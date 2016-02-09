using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace Utility
{
    class Networking
    {

        public enum CONNECTION_CODES
        {
            ERR = 0,
            OK = 1,
            HELLO = 3,
            AUTH_FAILURE = 4,
            REG_FAILURE = 5,
            NEW_REG = 6,
            KEY_EXC = 7
        };

        public static AesCryptoServiceProvider keyExchangeTcpClient(String ipAddress, Int32 port)
        {
            TcpClient tcpclnt = new TcpClient();
            tcpclnt.Connect(ipAddress, port);
            Stream stm = tcpclnt.GetStream();
            byte[] command = new byte[4];
            command = BitConverter.GetBytes((UInt32)CONNECTION_CODES.KEY_EXC);
            stm.Write(command, 0, command.Length);
            RSACryptoServiceProvider rsa = Security.generateRSAKey();
            byte[] modulus = Security.getModulus(rsa);
            byte[] exp = Security.getExponent(rsa);
            stm.Write(modulus, 0, modulus.Length);
            stm.Write(exp, 0, exp.Length);

            int bytesRead = stm.Read(command, 0, 4);
            if (bytesRead < 4)
            {
                tcpclnt.Close();
                return null;
            }


            byte[] recvBuf = new byte[512];
            bytesRead = stm.Read(recvBuf, 0, 512);
            if (bytesRead == 512)
            {
                MemoryStream ms = new MemoryStream(recvBuf);
                byte[] encKey = new byte[256];
                ms.Read(encKey, 0, 256);
                byte[] encIV = new byte[256];
                ms.Read(encIV, 0, 256);
                byte[] key = Security.RSADecrypt(rsa, encKey, false);
                byte[] iv = Security.RSADecrypt(rsa, encIV, false);
                if (key != null && iv != null)
                {
                    AesCryptoServiceProvider aes = Security.getAESKey(key, iv);
                    command = BitConverter.GetBytes((UInt32)CONNECTION_CODES.OK);
                    stm.Write(command, 0, command.Length);

                    tcpclnt.Close();
                    return aes;
                }
                
            }
            command = BitConverter.GetBytes((UInt32)CONNECTION_CODES.ERR);
            stm.Write(command, 0, command.Length);
            tcpclnt.Close();

            return null;
        }

        public static AesCryptoServiceProvider keyExchangeTcpServer(Socket client)
        {
            byte[] recvBuf = new byte[259];
            byte[] command = new byte[4];

            int bytesRead = client.Receive(recvBuf);
            if (bytesRead == 259)
            {
                command = BitConverter.GetBytes((UInt32)CONNECTION_CODES.OK);
                client.Send(command);
                byte[] modulus = new byte[256];
                byte[] exponent = new byte[3];
                MemoryStream ms = new MemoryStream(recvBuf);
                ms.Read(modulus, 0, 256);
                ms.Read(exponent, 0, 3);
                RSACryptoServiceProvider rsa = Security.getPublicKey(modulus, exponent);
                AesCryptoServiceProvider aes = Security.generateAESKey();
                byte[] encKey = Security.RSAEncrypt(rsa, aes.Key, false);
                byte[] encIV = Security.RSAEncrypt(rsa, aes.IV, false);
                if (encKey != null && encIV != null)
                {
                    client.Send(encKey);
                    client.Send(encIV);

                    return aes;
                }
            }
            command = BitConverter.GetBytes((UInt32)CONNECTION_CODES.ERR);
            client.Send(command);

            return null;
        }

    }
}
