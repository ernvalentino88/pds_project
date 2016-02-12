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
using System.Threading;

namespace Server
{
    class Server
    {
        private ClientSession clientSession;
        private Int64 sessionIdCounter;

        public ClientSession ClientSession
        {
            get;
            set;
        }

        public Int64 SessionIdCounter
        {
            get
            {
                return Interlocked.Read(ref this.sessionIdCounter);
            }
            private set;
        }

        public Server()
        {
            this.sessionIdCounter = 0;
        }

        public ClientSession keyExchangeTcpServer(Socket client)
        {
            byte[] recvBuf = new byte[259];
            byte[] command = new byte[4];

            recvBuf = Networking.my_recv(259, client);
            if (recvBuf != null)
            {
                byte[] modulus = new byte[256];
                byte[] exponent = new byte[3];
                MemoryStream ms = new MemoryStream(recvBuf);
                ms.Read(modulus, 0, 256);
                ms.Read(exponent, 0, 3);
                RSACryptoServiceProvider rsa = Security.getPublicKey(modulus, exponent);
                AesCryptoServiceProvider aes = Security.generateAESKey();
                byte[] dataToEncrypt = new byte[48];
                ms = new MemoryStream(dataToEncrypt);
                ms.Write(aes.Key, 0, aes.Key.Length);
                ms.Write(aes.IV, 0, aes.IV.Length);
                byte[] encrypted = Security.RSAEncrypt(rsa, dataToEncrypt, false);

                if (encrypted != null)
                {
                    client.Send(encrypted);
                    command = Networking.my_recv(4, client);
                    if (command != null && (
                         ((Networking.CONNECTION_CODES)BitConverter.ToUInt32(command, 0) == 
                                Networking.CONNECTION_CODES.OK)))
                    {
                        //client.Close();
                        clientSession = new ClientSession();
                        clientSession.AESKey = aes;
                        clientSession.Client = client;
                        return clientSession;
                    }
                }
            }
            //client.Close();
            return null;
        }

        public Int64 authenticationTcpServer(ClientSession clientSession)
        {
            byte[] command = new byte[4];
            Socket client = clientSession.Client;
            AesCryptoServiceProvider aes = clientSession.AESKey;
            byte[] recvBuf = Networking.my_recv(16, client);
            if (recvBuf != null)
            {
                byte[] decryptedData = Security.AESDecrypt(aes, recvBuf);
                String pwd = DBmanager.find_user(Encoding.UTF8.GetString(decryptedData));
                if (pwd != null)
                {
                    command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.OK);
                    client.Send(command);
                    Random r = new Random();
                    byte[] challenge = new byte[8];
                    r.NextBytes(challenge);
                    byte[] encryptedData = Security.AESEncrypt(aes, challenge);
                    client.Send(encryptedData);
                    SHA1 sha = new SHA1CryptoServiceProvider();
                    byte[] p = Encoding.UTF8.GetBytes(pwd);
                    byte[] hash = sha.ComputeHash(Security.XOR(p, challenge));
                    byte[] hashClient = Networking.my_recv(20, client);
                    if (hashClient != null)
                    {
                        if (hash.SequenceEqual(hashClient))
                        {
                            command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.OK);
                            client.Send(command);
                            Int64 sessionID = r.Next();
                            encryptedData = Security.AESEncrypt(aes, BitConverter.GetBytes(sessionID));
                            client.Send(encryptedData);
                            command = Networking.my_recv(4, client);
                            if (command != null && (
                                ((Networking.CONNECTION_CODES)BitConverter.ToUInt32(command, 0) == 
                                        Networking.CONNECTION_CODES.OK)))
                            {
                                //client.Close();
                                clientSession.SessionId = sessionID;
                                return sessionID;
                            }
                            else
                            {
                                //client.Close();
                                return -1; //error on last ack
                            }
                        }
                        else
                        {
                            //auth failed: pwd error
                            command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.AUTH_FAILURE);
                            client.Send(command);
                            return -2;
                        }
                    }
                }
                else
                {
                    //auth failed: user not existent
                    command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.AUTH_FAILURE);
                    client.Send(command);
                    return -3;
                }
            }
            //client.Close();
            return -1;
        }

        public void registrationTcpServer(ClientSession clientSession)
        {
            Socket client = clientSession.Client;
            AesCryptoServiceProvider aes = clientSession.AESKey;
            byte[] encryptedData = Networking.my_recv(16, client);
            byte[] command = new byte[4];

            if (encryptedData != null)
            {
                byte[] decryptedData = Security.AESDecrypt(aes, encryptedData);
                String userid = Encoding.UTF8.GetString(decryptedData);
                String pwd = DBmanager.find_user(userid);
                if (pwd != null)
                {
                    //user yet present
                    command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.REG_FAILURE);
                    client.Send(command);
                    return;
                }
                byte[] pwdSize = Networking.my_recv(4, client);
                if (pwdSize != null)
                {
                    int size = BitConverter.ToInt32(pwdSize, 0);
                    encryptedData = Networking.my_recv(size, client);
                    decryptedData = Security.AESDecrypt(aes, encryptedData);
                    if (DBmanager.register(userid, Encoding.UTF8.GetString(decryptedData)))
                    {
                        command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.OK);
                        client.Send(command);
                        return;
                    }
                    command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.REG_FAILURE);
                    client.Send(command);
                }
            }
        }

        public Int64 incrementAndGetIdCounter()
        {
            return Interlocked.Increment(ref this.sessionIdCounter);
        }
    }
}
