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

namespace ClientApp
{
    class Client
    {
        private TcpClient tcpClient;
        private AesCryptoServiceProvider aesKey;
        private Int64 sessionId;
        private IPEndPoint server;

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

        public IPEndPoint Server {
            get
            {
                return server;
            }
            set
            {
                server = value;
            }
        }

        public Int64 SessionId {
            get
            {
                return sessionId;
            }
            set
            {
                sessionId = value;
            }
        }


        public bool keyExchangeTcpClient()
        {
            try
            {
                Stream stm = tcpClient.GetStream();
                Socket s = tcpClient.Client;
                byte[] command = new byte[4];
                command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.KEY_EXC);
                s.Send(command);

                RSACryptoServiceProvider rsa = Security.generateRSAKey();
                byte[] modulus = Security.getModulus(rsa);
                byte[] exp = Security.getExponent(rsa);
                s.Send(modulus);
                s.Send(exp);

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
                        s.Send(command);
                        return true;
                    }
                    else
                    {
                        command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.ERR);
                        s.Send(command);
                    }
                }
                return false;
            }
            catch (SocketException)
            {
                return false;
            }
        }

        public Int64 authenticationTcpClient(String username, String pwd)
        {
            try
            {
                Socket s = tcpClient.Client;
                byte[] command = new byte[4];
                command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.AUTH);
                s.Send(command);

                byte[] encrypted = Security.AESEncrypt(aesKey, username);
                s.Send(encrypted);
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
                        s.Send(hash);
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
                                Int64 sessionID = BitConverter.ToInt64(id, 0);
                                command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.OK);
                                s.Send(command);
                                this.sessionId = sessionID;
                                return sessionId;
                            }
                        }
                        if (((Networking.CONNECTION_CODES)BitConverter.ToUInt32(command, 0) ==
                                    Networking.CONNECTION_CODES.AUTH_FAILURE))
                        {
                            //password not correct
                            return -3;
                        }
                    }
                }
                if (((Networking.CONNECTION_CODES)BitConverter.ToUInt32(command, 0) ==
                            Networking.CONNECTION_CODES.AUTH_FAILURE))
                {
                    //Error: username is not valid
                    return -2;
                }
            }
            catch (SocketException)
            {
                return -1;
            }
            return -1;
        }

        public int registrationTcpClient(String username, String pwd)
        {
            try
            {
                Socket s = tcpClient.Client;
                byte[] command = new byte[4];
                command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.NEW_REG);
                s.Send(command);

                byte[] encrypted = Security.AESEncrypt(aesKey, username);
                s.Send(encrypted);
                command = Networking.my_recv(4, tcpClient.Client);
                if (command != null && (
                        ((Networking.CONNECTION_CODES)BitConverter.ToUInt32(command, 0) == Networking.CONNECTION_CODES.OK)))
                {
                    encrypted = Security.AESEncrypt(aesKey, pwd);
                    Int32 pwdSize = encrypted.Length;
                    s.Send(BitConverter.GetBytes(pwdSize));
                    s.Send(encrypted);
                    command = Networking.my_recv(4, tcpClient.Client);
                    if (command != null && (
                            ((Networking.CONNECTION_CODES)BitConverter.ToUInt32(command, 0) == Networking.CONNECTION_CODES.OK)))
                    {
                        return 1; //ok
                    }
                    else
                        return -1; //db or network failure
                }
                if (command != null && (
                    ((Networking.CONNECTION_CODES)BitConverter.ToUInt32(command, 0) == Networking.CONNECTION_CODES.REG_FAILURE)))
                    return 0;  //userid is yet present

                return -1;  //db or network failure
            }
            catch (SocketException)
            {
                return -1;
            }
        }

        public bool resumeSession()
        {
            try
            {
                if (keepAlive())
                {
                    tcpClient.ReceiveTimeout = Networking.TIME_OUT_SHORT;
                    tcpClient.SendTimeout = Networking.TIME_OUT_SHORT;
                    Socket s = tcpClient.Client;
                    byte[] command = new byte[4];
                    command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.SESSION);
                    s.Send(command);
                    byte[] session = BitConverter.GetBytes(this.sessionId);
                    byte[] rand = Networking.my_recv(8, tcpClient.Client);
                    SHA1CryptoServiceProvider sha = new SHA1CryptoServiceProvider();
                    byte[] hash = sha.ComputeHash(Security.XOR(rand, session));
                    s.Send(hash);
                    command = Networking.my_recv(4, tcpClient.Client);
                    if (command != null && (
                            ((Networking.CONNECTION_CODES)BitConverter.ToUInt32(command, 0) == Networking.CONNECTION_CODES.OK)))
                    {
                        //session is not expired: ok
                        return true;
                    }
                    if (command != null && (
                            ((Networking.CONNECTION_CODES)BitConverter.ToUInt32(command, 0) == Networking.CONNECTION_CODES.AUTH)))
                    {
                        //session expired: need to re-authenthicate
                        return false;
                    }
                }
            }
            catch (SocketException)
            {
                return false;
            }
            return false;
        }

        public bool keepAlive()
        {
            bool isAlive = false;
            int retry = 0;

            while (!isAlive && retry < 3)
            {
                try
                {
                    tcpClient.ReceiveTimeout = 5 * 1000;
                    Stream stm = tcpClient.GetStream();
                    byte[] command = new byte[4];
                    command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.HELLO);
                    stm.Write(command, 0, command.Length);
                    command = Networking.my_recv(4, tcpClient.Client);
                    if (command != null && (
                            ((Networking.CONNECTION_CODES)BitConverter.ToUInt32(command, 0) == Networking.CONNECTION_CODES.HELLO)))
                        isAlive = true;
                    else
                    {

                        tcpClient = new TcpClient();
                        tcpClient.Connect(server);
                        tcpClient.ReceiveTimeout = 5 * 1000;
                        retry++;
                    }
                }
                catch (SocketException se)
                {
                    if (se.SocketErrorCode == SocketError.TimedOut)
                    {
                        retry++;
                    }
                    else
                    {
                        //server unreachable
                        return false;
                    }
                }
                catch (Exception)
                {
                    //server died
                    return false;
                }
            }
            return isAlive;
        }

        public void closeConnectionTcpClient()
        {
            try
            {
                Stream stm = tcpClient.GetStream();
                byte[] command = new byte[4];
                command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.EXIT);
                stm.Write(command, 0, command.Length);
                tcpClient.Close();
            }
            catch (Exception)
            {
                //socket is not connected
                return;
            }
        }
    }
}
