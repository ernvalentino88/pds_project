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
            KEY_EXC = 7,
            EXIT = 8,
            AUTH = 9
        };

        public static AesCryptoServiceProvider keyExchangeTcpClient(String ipAddress, Int32 port)
        {
            TcpClient tcpclnt = new TcpClient();
            tcpclnt.Connect(ipAddress, port);
            Stream stm = tcpclnt.GetStream();
            byte[] command = new byte[4];
            command = BitConverter.GetBytes((UInt32)CONNECTION_CODES.KEY_EXC);
            stm.Write(command, 0, command.Length);
            int bytesRead = stm.Read(command, 0, 4);

            if (bytesRead == 4 && (
                ((CONNECTION_CODES)BitConverter.ToUInt32(command, 0) == CONNECTION_CODES.OK)))
            {
                RSACryptoServiceProvider rsa = Security.generateRSAKey();
                byte[] modulus = Security.getModulus(rsa);
                byte[] exp = Security.getExponent(rsa);
                stm.Write(modulus, 0, modulus.Length);
                stm.Write(exp, 0, exp.Length);

                bytesRead = stm.Read(command, 0, 4);
                if (bytesRead == 4 && (
                    ((CONNECTION_CODES)BitConverter.ToUInt32(command, 0) == CONNECTION_CODES.OK)))
                {
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
                    bytesRead = client.Receive(command);
                    if (bytesRead == 4 && (
                         ((CONNECTION_CODES)BitConverter.ToUInt32(command, 0) == CONNECTION_CODES.OK)))
                    {
                        client.Close();
                        return aes;
                    }           
                }
            }
            command = BitConverter.GetBytes((UInt32)CONNECTION_CODES.ERR);
            client.Send(command);
            client.Close();

            return null;
        }

        public static UInt64? authenticationTcpClient(Aes aes, String username, String pwd, IPEndPoint server)
        {
            TcpClient tcpclnt = new TcpClient();
            tcpclnt.Connect(server);
            Stream stm = tcpclnt.GetStream();
            byte[] command = new byte[4];
            command = BitConverter.GetBytes((UInt32)CONNECTION_CODES.AUTH);
            stm.Write(command, 0, command.Length);
            int bytesRead = stm.Read(command, 0, 4);

            if (bytesRead == 4 && (
                ((CONNECTION_CODES)BitConverter.ToUInt32(command, 0) == CONNECTION_CODES.OK)))
            {
                byte[] encrypted = Security.AESEncrypt(aes, username);
                stm.Write(encrypted, 0, encrypted.Length);
                bytesRead = stm.Read(command, 0, 4);
                if (bytesRead == 4 && (
                     ((CONNECTION_CODES)BitConverter.ToUInt32(command, 0) == CONNECTION_CODES.OK)))
                {
                    byte[] recvBuf = new byte[16];
                    bytesRead = stm.Read(recvBuf, 0, recvBuf.Length);
                    if (bytesRead == 16)
                    {
                        byte[] challenge = Security.AESDecrypt(aes, recvBuf);
                        command = BitConverter.GetBytes((UInt32)CONNECTION_CODES.OK);
                        stm.Write(command, 0, 4);
                        byte[] p = Encoding.UTF8.GetBytes(pwd);
                        SHA1 sha = new SHA1CryptoServiceProvider();
                        byte[] hash = sha.ComputeHash(Security.XOR(p, challenge));
                        byte[] sendBuf = Security.AESEncrypt(aes, hash);
                        stm.Write(sendBuf, 0, 32);
                        bytesRead = stm.Read(command, 0, 4);
                        if (bytesRead == 4 && (
                             ((CONNECTION_CODES)BitConverter.ToUInt32(command, 0) == CONNECTION_CODES.OK)))
                        {
                            recvBuf = new byte[16];
                            bytesRead = stm.Read(recvBuf, 0, recvBuf.Length);
                            if (bytesRead == 16)
                            {
                                byte[] id = Security.AESDecrypt(aes, recvBuf);
                                UInt64 sessionID = BitConverter.ToUInt64(id, 0);
                                command = BitConverter.GetBytes((UInt32)CONNECTION_CODES.OK);
                                stm.Write(command, 0, command.Length);
                                tcpclnt.Close();
                                return sessionID;
                            }
                        }

                    }
                    else
                    {
                        command = BitConverter.GetBytes((UInt32)CONNECTION_CODES.ERR);
                        stm.Write(command, 0, command.Length);
                    }
                }
            }
            tcpclnt.Close();
            return null;
        }

    }
}
