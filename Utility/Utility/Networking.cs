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
    public class Networking
    {
        public static int TIME_OUT = 10 * 1000;

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
            tcpclnt.ReceiveTimeout = TIME_OUT;
            tcpclnt.SendTimeout = TIME_OUT;
            Stream stm = tcpclnt.GetStream();
            byte[] command = new byte[4];
            command = BitConverter.GetBytes((UInt32)CONNECTION_CODES.KEY_EXC);
            stm.Write(command, 0, command.Length);
            
            RSACryptoServiceProvider rsa = Security.generateRSAKey();
            byte[] modulus = Security.getModulus(rsa);
            byte[] exp = Security.getExponent(rsa);
            stm.Write(modulus, 0, modulus.Length);
            stm.Write(exp, 0, exp.Length);
            
            byte[] recvBuf = my_recv(256, tcpclnt.Client);
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
                    AesCryptoServiceProvider aes = Security.getAESKey(Key, IV);
                    command = BitConverter.GetBytes((UInt32)CONNECTION_CODES.OK);
                    stm.Write(command, 0, command.Length);

                    tcpclnt.Close();
                    return aes;
                }
                else
                {
                    command = BitConverter.GetBytes((UInt32)CONNECTION_CODES.ERR);
                    stm.Write(command, 0, command.Length);
                }
            }
            tcpclnt.Close();

            return null;
        }

        public static AesCryptoServiceProvider keyExchangeTcpServer(Socket client)
        {
            byte[] recvBuf = new byte[259];
            byte[] command = new byte[4];

            recvBuf = my_recv(259, client);
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
                    command = my_recv(4, client);
                    if (command != null && (
                         ((CONNECTION_CODES)BitConverter.ToUInt32(command, 0) == CONNECTION_CODES.OK)))
                    {
                        //client.Close();
                        return aes;
                    }         
                }
            }
            //client.Close();
            return null;
        }

        public static Int64 authenticationTcpClient(Aes aes, String username, String pwd, IPEndPoint server)
        {
            TcpClient tcpclnt = new TcpClient();
            tcpclnt.Connect(server);
            Stream stm = tcpclnt.GetStream();
            byte[] command = new byte[4];
            command = BitConverter.GetBytes((UInt32)CONNECTION_CODES.AUTH);
            stm.Write(command, 0, command.Length);
            
            byte[] encrypted = Security.AESEncrypt(aes, username);
            stm.Write(encrypted, 0, encrypted.Length);
            command = my_recv(4, tcpclnt.Client);
            if (command != null && (
                    ((CONNECTION_CODES)BitConverter.ToUInt32(command, 0) == CONNECTION_CODES.OK)))
            {
                byte[] recvBuf = new byte[16];
                recvBuf = my_recv(16, tcpclnt.Client);
                if (recvBuf != null)
                {
                    byte[] challenge = Security.AESDecrypt(aes, recvBuf);
                    SHA1 sha = new SHA1CryptoServiceProvider();
                    byte[] p = Encoding.UTF8.GetBytes(Security.CalculateMD5Hash(pwd));
                    byte[] hash = sha.ComputeHash(Security.XOR(p, challenge));
                    stm.Write(hash, 0, 20);
                    command = my_recv(4, tcpclnt.Client);
                    if (command != null && (
                            ((CONNECTION_CODES)BitConverter.ToUInt32(command, 0) == CONNECTION_CODES.OK)))
                    {
                        recvBuf = new byte[16];
                        recvBuf = my_recv(16, tcpclnt.Client);
                        if (recvBuf != null)
                        {
                            byte[] id = Security.AESDecrypt(aes, recvBuf);
                            Int64 sessionID = BitConverter.ToInt64(id, 0);
                            command = BitConverter.GetBytes((UInt32)CONNECTION_CODES.OK);
                            stm.Write(command, 0, command.Length);
                            tcpclnt.Close();
                            return sessionID;
                        }
                    }
                    if ( ((CONNECTION_CODES)BitConverter.ToUInt32(command, 0) == CONNECTION_CODES.AUTH_FAILURE) )
                    {
                        //password not correct
                        tcpclnt.Close();
                        return -2;
                    }
                }
            }
            if ( ((CONNECTION_CODES)BitConverter.ToUInt32(command, 0) == CONNECTION_CODES.AUTH_FAILURE) )
            {
                //Error: username is not valid
                tcpclnt.Close();
                return -2;
            }
            tcpclnt.Close();
            return -1;
        }

        public static Int64 authenticationTcpServer(Aes aes, Socket client)
        {
            byte[] command = new byte[4];

            byte[]  recvBuf = my_recv(16, client);
            if (recvBuf != null)
            {
                byte[] decryptedData = Security.AESDecrypt(aes, recvBuf);
                String pwd = DBmanager.find_user(Encoding.UTF8.GetString(decryptedData));
                if (pwd != null)
                {
                    command = BitConverter.GetBytes((UInt32)CONNECTION_CODES.OK);
                    client.Send(command);
                    Random r = new Random();
                    byte[] challenge = new byte[8];
                    r.NextBytes(challenge);
                    byte[] encryptedData = Security.AESEncrypt(aes, challenge);
                    client.Send(encryptedData);
                    SHA1 sha = new SHA1CryptoServiceProvider();
                    byte[] p = Encoding.UTF8.GetBytes(pwd);
                    byte[] hash = sha.ComputeHash(Security.XOR(p, challenge));
                    byte[] hashClient = my_recv(20, client);
                    if (hashClient != null)
                    {
                        if (hash.SequenceEqual(hashClient))
                        {
                            command = BitConverter.GetBytes((UInt32)CONNECTION_CODES.OK);
                            client.Send(command);
                            Int64 sessionID = r.Next();
                            encryptedData = Security.AESEncrypt(aes, BitConverter.GetBytes(sessionID));
                            client.Send(encryptedData);
                            command = my_recv(4, client);
                            if (command != null && (
                                ((CONNECTION_CODES)BitConverter.ToUInt32(command, 0) == CONNECTION_CODES.OK)))
                            {
                                //client.Close();
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
                            command = BitConverter.GetBytes((UInt32)CONNECTION_CODES.AUTH_FAILURE);
                            client.Send(command);
                            return -2;
                        }
                    }
                }
                else
                {
                    //auth failed: user not existent
                    command = BitConverter.GetBytes((UInt32)CONNECTION_CODES.AUTH_FAILURE);
                    client.Send(command);
                    return -3;
                }
            }
            //client.Close();
            return 0;
        }

        private static byte[] my_recv(int size, Socket s)
        {
            int left = size;
            int b;
            byte[] received = new byte[size];
            
            MemoryStream ms = new MemoryStream(received);
            try
            {
                while (left > 0)
                {
                    byte[] buffer = new byte[256];
                    b = s.Receive(buffer);
                    if (b <= 0)
                    {
                        return null;
                    }
                    ms.Write(buffer, 0, b);
                    left -= b;
                }
            }
            catch (SocketException)
            {
                return null;
            }
            return received;
        }

    }
}
