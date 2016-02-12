using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using Utility;

namespace Client
{
    class ClientApp
    {
        private TcpClient tcpClient;
        private AesCryptoServiceProvider aesKey;
        private RSACryptoServiceProvider rsaKey;
        private UInt64 sessionId;

        public TcpClient TcpClient {
            get 
            { 
                return tcpClient; 
            }
            set 
            { 
                tcpClient = value; 
            }
        }

        public AesCryptoServiceProvider AESKey {
            get
            {
                return aesKey;
            }
            set
            {
                aesKey = value;
            }
        }

        public RSACryptoServiceProvider RSAKey {
            get
            {
                return rsaKey;
            }
            set
            {
                rsaKey = value;
            }
        }

        public UInt64 SessionId {
            get
            {
                return sessionId;
            }
            set
            {
                sessionId = value;
            }
        }

        public ClientApp()
        {
            tcpClient = new TcpClient();
            aesKey = new AesCryptoServiceProvider();
        }

        public void keyExchangeTcpClient(String ipAddress, Int32 port)
        {
            tcpClient.Connect(ipAddress, port);
            tcpClient.ReceiveTimeout = Networking.TIME_OUT;
            tcpClient.SendTimeout = Networking.TIME_OUT;
            Stream stm = tcpClient.GetStream();
            byte[] command = new byte[4];
            command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.KEY_EXC);
            stm.Write(command, 0, command.Length);

            RSACryptoServiceProvider rsa = Security.generateRSAKey();
            byte[] modulus = Security.getModulus(rsa);
            byte[] exp = Security.getExponent(rsa);
            stm.Write(modulus, 0, modulus.Length);
            stm.Write(exp, 0, exp.Length);

            byte[] recvBuf = Networking.my_recv(256, tcpClient.Client);
            if (recvBuf != null)
            {
                byte[] decryptedData = Security.RSADecrypt(rsa, recvBuf, false);
                MemoryStream ms = new MemoryStream(decryptedData);
                byte[] Key = new byte[32];
                ms.Read(Key, 0, 32);
                byte[] IV = new byte[16];
                ms.Read(IV, 0, 16);

                if (Key != null && IV != null)
                {
                    aesKey = Security.getAESKey(Key, IV);
                    command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.OK);
                    stm.Write(command, 0, command.Length);
                    return;
                }
                else
                {
                    command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.ERR);
                    stm.Write(command, 0, command.Length);
                }
            }
            tcpClient.Close();
        }

        public int authenticationTcpClient(String username, String pwd)
        {
            Stream stm = tcpClient.GetStream();
            byte[] command = new byte[4];
            command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.AUTH);
            stm.Write(command, 0, command.Length);

            byte[] encrypted = Security.AESEncrypt(aesKey, username);
            stm.Write(encrypted, 0, encrypted.Length);
            command = Networking.my_recv(4, tcpClient.Client);
            if (command != null && (
                    ((Networking.CONNECTION_CODES)BitConverter.ToUInt32(command, 0) == 
                            Networking.CONNECTION_CODES.OK)))
            {
                byte[] recvBuf = new byte[16];
                recvBuf = Networking.my_recv(16, tcpClient.Client);
                if (recvBuf != null)
                {
                    byte[] challenge = Security.AESDecrypt(aesKey, recvBuf);
                    SHA1 sha = new SHA1CryptoServiceProvider();
                    byte[] p = Encoding.UTF8.GetBytes(Security.CalculateMD5Hash(pwd));
                    byte[] hash = sha.ComputeHash(Security.XOR(p, challenge));
                    stm.Write(hash, 0, 20);
                    command = Networking.my_recv(4, tcpClient.Client);
                    if (command != null && (
                            ((Networking.CONNECTION_CODES)BitConverter.ToUInt32(command, 0) == 
                                    Networking.CONNECTION_CODES.OK)))
                    {
                        recvBuf = new byte[16];
                        recvBuf = Networking.my_recv(16, tcpClient.Client);
                        if (recvBuf != null)
                        {
                            byte[] id = Security.AESDecrypt(aesKey, recvBuf);
                            UInt64 sessionID = BitConverter.ToUInt64(id, 0);
                            command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.OK);
                            stm.Write(command, 0, command.Length);
                            sessionId = sessionID;
                            return 1;
                        }
                    }
                    if (((Networking.CONNECTION_CODES)BitConverter.ToUInt32(command, 0) == 
                                Networking.CONNECTION_CODES.AUTH_FAILURE))
                    {
                        //password not correct
                        return -2;
                    }
                }
            }
            if (((Networking.CONNECTION_CODES)BitConverter.ToUInt32(command, 0) == 
                        Networking.CONNECTION_CODES.AUTH_FAILURE))
            {
                //Error: username is not valid
                return -2;
            }
            return -1;
        }

        public int registrationTcpClient(String username, String pwd)
        {
            Stream stm = tcpClient.GetStream();
            byte[] command = new byte[4];
            command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.NEW_REG);
            stm.Write(command, 0, command.Length);

            byte[] encrypted = Security.AESEncrypt(aesKey, username);
            stm.Write(encrypted, 0, encrypted.Length);
            command = Networking.my_recv(4, tcpClient.Client);
            if (command != null && (
                    ((Networking.CONNECTION_CODES)BitConverter.ToUInt32(command, 0) == Networking.CONNECTION_CODES.OK)))
            {
                encrypted = Security.AESEncrypt(aesKey, username);
                Int32 pwdSize = encrypted.Length;
                stm.Write(BitConverter.GetBytes(pwdSize), 0, 4);
                stm.Write(encrypted, 0, encrypted.Length);
                command = Networking.my_recv(4, tcpClient.Client);
                if (command != null && (
                        ((Networking.CONNECTION_CODES)BitConverter.ToUInt32(command, 0) == Networking.CONNECTION_CODES.OK)))
                {
                    return 1; //ok
                }
                else
                    return -1; //db or network failure
            }
            if ((Networking.CONNECTION_CODES)BitConverter.ToUInt32(command, 0) == Networking.CONNECTION_CODES.AUTH_FAILURE)
                return 0;  //userid is yet present

            return -1;  //db or network failure
        }
    }
}
