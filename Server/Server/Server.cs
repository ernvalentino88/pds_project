using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using Utility;
using System.Threading;
using System.Data.SQLite;

namespace ServerApp
{
    class Server
    {
        private ConcurrentDictionary<Int64, ClientSession> id2client;
        private Int64 sessionIdCounter;
        private static String con_string = @"Data Source=C:\Users\John\Desktop\SQLiteStudio\PDS.db;Version=3;";

        public Int64 SessionIdCounter
        {
            get
            {
                return Interlocked.Read(ref this.sessionIdCounter);
            }
        }

        public Server()
        {
            this.sessionIdCounter = 0;
            this.id2client = new ConcurrentDictionary<long, ClientSession>();
        }

        public ClientSession keyExchangeTcpServer(Socket s)
        {
            try
            {
                byte[] recvBuf = new byte[259];
                byte[] command = new byte[4];

                recvBuf = Networking.my_recv(259, s);
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
                        s.Send(encrypted);
                        command = Networking.my_recv(4, s);
                        if (command != null && (
                             ((Networking.CONNECTION_CODES)BitConverter.ToUInt32(command, 0) ==
                                    Networking.CONNECTION_CODES.OK)))
                        {
                            ClientSession clientSession = new ClientSession();
                            clientSession.AESKey = aes;
                            clientSession.Socket = s;
                            return clientSession;
                        }
                    }
                }
            }
            catch (SocketException) { }
            return null;
        }

        public Int64 authenticationTcpServer(ClientSession clientSession)
        {
            try
            {
                byte[] command = new byte[4];
                Socket s = clientSession.Socket;
                AesCryptoServiceProvider aes = clientSession.AESKey;
                byte[] recvBuf = Networking.my_recv(16, s);
                if (recvBuf != null)
                {
                    byte[] decryptedData = Security.AESDecrypt(aes, recvBuf);
                    String userId = Encoding.UTF8.GetString(decryptedData);
                    String pwd = DBmanager.find_user(userId);
                    if (pwd != null)
                    {
                        command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.OK);
                        s.Send(command);
                        Random r = new Random();
                        byte[] challenge = new byte[8];
                        r.NextBytes(challenge);
                        byte[] encryptedData = Security.AESEncrypt(aes, challenge);
                        s.Send(encryptedData);
                        SHA1 sha = new SHA1CryptoServiceProvider();
                        byte[] p = Encoding.UTF8.GetBytes(pwd);
                        byte[] hash = sha.ComputeHash(Security.XOR(p, challenge));
                        byte[] hashClient = Networking.my_recv(20, s);
                        if (hashClient != null)
                        {
                            if (hash.SequenceEqual(hashClient))
                            {
                                command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.OK);
                                s.Send(command);
                                Int64 sessionID = incrementAndGetIdCounter();
                                encryptedData = Security.AESEncrypt(aes, BitConverter.GetBytes(sessionID));
                                s.Send(encryptedData);
                                command = Networking.my_recv(4, s);
                                if (command != null && (
                                    ((Networking.CONNECTION_CODES)BitConverter.ToUInt32(command, 0) ==
                                            Networking.CONNECTION_CODES.OK)))
                                {
                                    clientSession.SessionId = sessionID;
                                    clientSession.User = new User(userId, pwd);
                                    clientSession.LastActivationTime = DateTime.Now;
                                    id2client.GetOrAdd(sessionID, clientSession);
                                    return sessionID;
                                }
                                else
                                {
                                    return -1; //error on last ack
                                }
                            }
                            else
                            {
                                command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.AUTH_FAILURE);
                                s.Send(command);
                                return -2;
                            }
                        }
                    }
                    else
                    {
                        //auth failed: user not existent
                        command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.AUTH_FAILURE);
                        s.Send(command);
                        return -3;
                    }
                }
            }
            catch (SocketException) { }
            return -1;
        }

        public void registrationTcpServer(ClientSession clientSession)
        {
            try
            {
                byte[] command = new byte[4];
                Socket s = clientSession.Socket;
                AesCryptoServiceProvider aes = clientSession.AESKey;
                byte[] encryptedData = Networking.my_recv(16, s);
                if (encryptedData != null)
                {
                    byte[] decryptedData = Security.AESDecrypt(aes, encryptedData);
                    String userid = Encoding.UTF8.GetString(decryptedData);
                    String pwd = DBmanager.find_user(userid);
                    if (pwd != null)
                    {
                        //user yet present
                        command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.REG_FAILURE);
                        s.Send(command);
                        return;
                    }
                    command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.OK);
                    s.Send(command);
                    byte[] pwdSize = Networking.my_recv(4, s);
                    if (pwdSize != null)
                    {
                        int size = BitConverter.ToInt32(pwdSize, 0);
                        encryptedData = Networking.my_recv(size, s);
                        decryptedData = Security.AESDecrypt(aes, encryptedData);
                        if (DBmanager.register(userid, Encoding.UTF8.GetString(decryptedData)))
                        {
                            command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.OK);
                            s.Send(command);
                            return;
                        }
                        command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.REG_FAILURE);
                        s.Send(command);
                    }
                }
            }
            catch (SocketException) { }
        }

        public void Hello(Socket s)
        {
            try
            {
                byte[] command = new byte[4];
                command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.HELLO);
                s.Send(command);
            }
            catch (SocketException) { }
        }

        public bool resumeSession(ref ClientSession clientSession, Socket s)
        {
            try
            {
                byte[] command = new byte[4];
                Random r = new Random();
                byte[] rand = new byte[8];
                r.NextBytes(rand);
                s.Send(rand);
                byte[] hashClient = Networking.my_recv(20, s);
                if (hashClient != null)
                {
                    if (clientSession != null)
                    {
                        SHA1CryptoServiceProvider sha = new SHA1CryptoServiceProvider();
                        byte[] hash = null;
                        hash = sha.ComputeHash(Security.XOR(rand, BitConverter.GetBytes(clientSession.SessionId)));
                        if (hash.SequenceEqual(hashClient))
                        {
                            command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.OK);
                            s.Send(command);
                            clientSession.LastActivationTime = DateTime.Now;
                            return true;
                        }
                        else
                        {
                            command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.AUTH);
                            s.Send(command);
                            return false;
                        }
                    }
                    else
                    {
                        clientSession = getClientSessionFromHash(hashClient, rand);
                        if (clientSession != null)
                        {
                            command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.OK);
                            s.Send(command);
                            clientSession.LastActivationTime = DateTime.Now;
                            return true;
                        }
                        else
                        {
                            command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.AUTH);
                            s.Send(command);
                            return false;
                        }
                    }
                }
            }
            catch (SocketException) { }
            return false;
        }

        private ClientSession getClientSessionFromHash(byte[] hashClient, byte[] rand)
        {
            ClientSession cs = null;
            foreach (var item in id2client)
            {
                SHA1CryptoServiceProvider sha = new SHA1CryptoServiceProvider();
                byte[] hash = null;
                hash = sha.ComputeHash(Security.XOR(rand, BitConverter.GetBytes(item.Key)));
                if (hash.SequenceEqual(hashClient))
                {
                    return item.Value;
                }
                TimeSpan diff = DateTime.Now - item.Value.LastActivationTime;
                int mins = (int)diff.TotalMinutes;
                if (mins > 60)
                    id2client.TryRemove(item.Key, out cs);
            }

            return null;
        }

        public Int64 incrementAndGetIdCounter()
        {
            lock (this)
            {
                if (this.sessionIdCounter == Int64.MaxValue)
                    this.sessionIdCounter = 0;
            }
            return Interlocked.Increment(ref this.sessionIdCounter);
        }

        public bool synchronizationSession(ClientSession clientSession, DateTime creationTime)
        {
            bool exit = false;
            SQLiteConnection con = new SQLiteConnection(DBmanager.connectionString);
            con.Open();
            SQLiteTransaction transaction = con.BeginTransaction();
            DirectoryStatus newStatus = new DirectoryStatus();
            newStatus.FolderPath = clientSession.CurrentStatus.FolderPath;
            newStatus.Username = clientSession.CurrentStatus.Username;

            while (!exit)
            {
                byte[] command = Utility.Networking.my_recv(4, clientSession.Socket);
                if (command != null)
                {
                    Networking.CONNECTION_CODES code = (Networking.CONNECTION_CODES)BitConverter.ToUInt32(command, 0);
                    switch (code)
                    {
                        case Networking.CONNECTION_CODES.ADD :
                            if (!this.addFile(con, clientSession, newStatus))
                                exit = true;
                            break;
                        case Networking.CONNECTION_CODES.UPD :
                            if (!this.updateFile(con, clientSession, newStatus))
                                exit = true;
                            break;
                        case Networking.CONNECTION_CODES.DEL :
                            if (!this.deleteFile(con, clientSession, newStatus))
                                exit = true;
                            break;
                        case Networking.CONNECTION_CODES.END_SYNCH:
                            if (!DBmanager.insertUnchanged(con, clientSession.CurrentStatus, newStatus))
                                exit = true;
                            else
                            {
                                //success: commit and dispose objects
                                transaction.Commit();
                                transaction.Dispose();
                                con.Dispose();
                                clientSession.CurrentStatus = null;
                                clientSession.CurrentStatus = newStatus;
                                return true;
                            }
                            break;
                        default :
                            exit = true;
                            break;
                    }
                }
            }
            //failure: rollback and dispose objects
            transaction.Rollback();
            transaction.Dispose();
            con.Dispose();
            newStatus = null;
            return false;
        }

        private bool deleteFile(SQLiteConnection conn, ClientSession clientSession, DirectoryStatus newStatus)
        {
            Socket s = clientSession.Socket;
            AesCryptoServiceProvider aes = clientSession.AESKey;
            try
            {
                byte[] recvBuf = Networking.my_recv(4, s);
                if (recvBuf == null)
                    return false;
                byte[] encryptedData = Networking.my_recv(BitConverter.ToInt32(recvBuf, 0), s);
                if (encryptedData == null)
                    return false;
                String filename = Encoding.UTF8.GetString(Security.AESDecrypt(aes, encryptedData));
                recvBuf = Networking.my_recv(4, s);
                if (recvBuf == null)
                    return false;
                encryptedData = Networking.my_recv(BitConverter.ToInt32(recvBuf, 0), s);
                if (encryptedData == null)
                    return false;
                String path = Encoding.UTF8.GetString(Security.AESDecrypt(aes, encryptedData));
                recvBuf = Networking.my_recv(4, s);
                if (recvBuf == null)
                    return false;
                if ((Networking.CONNECTION_CODES)BitConverter.ToUInt32(recvBuf, 0) == Networking.CONNECTION_CODES.DIR)
                {
                    //directory
                    return DBmanager.deleteDirectory(conn, newStatus, filename, path);
                }
                if ((Networking.CONNECTION_CODES)BitConverter.ToUInt32(recvBuf, 0) == Networking.CONNECTION_CODES.FILE)
                {
                    //file
                    String fullname = Path.Combine(path, filename);
                    DirectoryFile file = clientSession.CurrentStatus.Files[fullname];
                    return DBmanager.deleteFile(conn, file, newStatus);
                }
                
            }
            catch (SocketException) { }
            return false;
        }

        private bool addFile(SQLiteConnection conn, ClientSession clientSession, DirectoryStatus newStatus)
        {
            Socket s = clientSession.Socket;
            AesCryptoServiceProvider aes = clientSession.AESKey;
            try
            {
                byte[] recvBuf = Networking.my_recv(4, s);
                if (recvBuf == null)
                    return false;
                byte[] encryptedData = Networking.my_recv(BitConverter.ToInt32(recvBuf, 0), s);
                if (encryptedData == null)
                    return false;
                String filename = Encoding.UTF8.GetString(Security.AESDecrypt(aes, encryptedData));
                recvBuf = Networking.my_recv(4, s);
                if (recvBuf == null)
                    return false;
                encryptedData = Networking.my_recv(BitConverter.ToInt32(recvBuf, 0), s);
                if (encryptedData == null)
                    return false;
                String path = Encoding.UTF8.GetString(Security.AESDecrypt(aes, encryptedData));
                
                recvBuf = Networking.my_recv(4, s);
                if (recvBuf == null)
                    return false;
                if ((Networking.CONNECTION_CODES)BitConverter.ToUInt32(recvBuf, 0) == Networking.CONNECTION_CODES.DIR)
                {
                    //directory
                    return DBmanager.insertDirectory(conn, newStatus, filename, path);
                }
                if ((Networking.CONNECTION_CODES)BitConverter.ToUInt32(recvBuf, 0) == Networking.CONNECTION_CODES.FILE)
                {
                    //file
                    recvBuf = Networking.my_recv(8, s);
                    if (recvBuf == null)
                        return false;
                    DateTime lastModTime = DateTime.FromBinary(BitConverter.ToInt64(recvBuf, 0));
                    byte[] command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.OK);
                    s.Send(command);
                    recvBuf = Networking.my_recv(8, s);
                    if (recvBuf == null)
                        return false;
                    Int64 fileLen = BitConverter.ToInt64(recvBuf, 0);
                    byte[] file = Networking.recvEncryptedFile(fileLen, s, aes);
                    if (file == null)
                        return false;
                    return DBmanager.insertFile(conn, newStatus, filename, path, file, lastModTime);
                }
            }
            catch (SocketException) { }
            return false;
        }

        private bool updateFile(SQLiteConnection conn, ClientSession clientSession, DirectoryStatus newStatus)
        {
            Socket s = clientSession.Socket;
            AesCryptoServiceProvider aes = clientSession.AESKey;
            
            try
            {
                byte[] recvBuf = Networking.my_recv(4, s);
                if (recvBuf == null)
                    return false;
                byte[] encryptedData = Networking.my_recv(BitConverter.ToInt32(recvBuf, 0), s);
                if (encryptedData == null)
                    return false;
                String filename = Encoding.UTF8.GetString(Security.AESDecrypt(aes, encryptedData));
                recvBuf = Networking.my_recv(4, s);
                if (recvBuf == null)
                    return false;
                encryptedData = Networking.my_recv(BitConverter.ToInt32(recvBuf, 0), s);
                if (encryptedData == null)
                    return false;
                String path = Encoding.UTF8.GetString(Security.AESDecrypt(aes, encryptedData));
                recvBuf = Networking.my_recv(8, s);
                if (recvBuf == null)
                    return false;
                DateTime lastModTime = DateTime.FromBinary(BitConverter.ToInt64(recvBuf, 0));
                byte[] command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.OK);
                s.Send(command);
                recvBuf = Networking.my_recv(8, s);
                if (recvBuf == null)
                    return false;
                Int64 fileLen = BitConverter.ToInt64(recvBuf, 0);
                byte[] file = Networking.recvEncryptedFile(fileLen, s, aes);
                if (file == null)
                    return false;

                return DBmanager.insertFile(conn, newStatus, filename, path, file, lastModTime);
            }
            catch (SocketException) { }
            return false;
        }

        public bool getDirectoryName(ClientSession clientSession)
        {
            try
            {
                Socket s = clientSession.Socket;
                AesCryptoServiceProvider aes = clientSession.AESKey;
                byte[] recvBuf = Networking.my_recv(4, s);
                if (recvBuf != null)
                {
                    int pathLen = BitConverter.ToInt32(recvBuf, 0);
                    recvBuf = Networking.my_recv(pathLen, s);
                    if (recvBuf != null)
                    {
                        byte[] command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.OK);
                        s.Send(command);
                        return true;
                    }
                }
            }
            catch (SocketException) { }
            return false;
        }

        public bool beginSynchronization(ClientSession clientSession)
        {
            try
            {
                Socket s = clientSession.Socket;
                AesCryptoServiceProvider aes = clientSession.AESKey;
                byte[] recvBuf = Networking.my_recv(4, s);
                if (recvBuf == null)
                    return false;
                int pathLen = BitConverter.ToInt32(recvBuf, 0);
                recvBuf = Networking.my_recv(pathLen, s);
                if (recvBuf == null)
                    return false;
                byte[] command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.OK);
                s.Send(command);

                String dir = Encoding.UTF8.GetString(Security.AESDecrypt(aes, recvBuf));
                DirectoryStatus status = DBmanager.getLastSnapshot(dir, clientSession.User.UserId);
                clientSession.CurrentStatus = status;
                int count = (status == null) ? 0 : status.Files.Count;
                byte[] buf = BitConverter.GetBytes(count);
                s.Send(buf);
                if (status != null)
                {
                    foreach (var item in status.Files)
                    {
                        DirectoryFile file = item.Value;
                        buf = Encoding.UTF8.GetBytes(file.Path);
                        byte[] encryptedData = Security.AESEncrypt(aes, buf);
                        s.Send(BitConverter.GetBytes(encryptedData.Length));
                        s.Send(encryptedData);
                        buf = Encoding.UTF8.GetBytes(file.Filename);
                        encryptedData = Security.AESEncrypt(aes, buf);
                        s.Send(BitConverter.GetBytes(encryptedData.Length));
                        s.Send(encryptedData);
                        if (file.Directory)
                        {
                            command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.DIR);
                            s.Send(command);
                        }
                        else
                        {
                            command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.FILE);
                            s.Send(command);
                            buf = BitConverter.GetBytes(file.Id);
                            s.Send(buf);
                            buf = Encoding.UTF8.GetBytes(file.Checksum);
                            encryptedData = Security.AESEncrypt(aes, buf);
                            s.Send(encryptedData);
                        }
                    }
                }
                return true;
            }
            catch (SocketException) { }
            return false;
        }
    }
}
