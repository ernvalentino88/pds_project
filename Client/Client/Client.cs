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
        private String userId;
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

        public String UserId
        {
            get
            {
                return userId;
            }
            set
            {
                userId = value;
            }
        }


        public bool keyExchangeTcpClient()
        {
            try
            {
                Stream stm = tcpClient.GetStream();
                Socket s = tcpClient.Client;
                byte[] command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.KEY_EXC);
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
                byte[] command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.AUTH);
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
                byte[] command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.NEW_REG);
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
                    byte[] command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.SESSION);
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
                    byte[] command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.HELLO);
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
                    if (se.SocketErrorCode == SocketError.ConnectionAborted)
                    {
                        tcpClient = new TcpClient();
                        tcpClient.Connect(server);
                        tcpClient.ReceiveTimeout = 5 * 1000;
                        retry++;
                    }
                    else
                    {
                        //server unreachable
                        StreamWriter sw = new StreamWriter("client_log.txt", true);
                        sw.Write(DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
                        sw.WriteLine(" ***Fatal Error***  " + se.Message + " code: " + se.SocketErrorCode);
                        sw.WriteLine(se.StackTrace);
                        sw.Close();
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
                byte[] command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.EXIT);
                stm.Write(command, 0, command.Length);
                tcpClient.Close();
            }
            catch (Exception)
            {
                //socket is not connected
                return;
            }
        }

        public bool sendDirectory(String directory)
        {
            try
            {
                Socket s = tcpClient.Client;
                byte[] command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.DIR);
                s.Send(command);
                byte[] pathLen = BitConverter.GetBytes(directory.Length);
                s.Send(pathLen);
                byte[] dir = Encoding.UTF8.GetBytes(directory);
                s.Send(dir);
                command = Networking.my_recv(4, tcpClient.Client);
                if (command != null && (
                        ((Networking.CONNECTION_CODES)BitConverter.ToUInt32(command, 0) == Networking.CONNECTION_CODES.OK)))
                    return true;
            }
            catch (SocketException)  { }
            return false; 
        }

        public DirectoryStatus startSynch()
        {
            try
            {
                Socket s = tcpClient.Client;
                DirectoryStatus remoteStat = new DirectoryStatus();
                byte[] command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.INIT_SYNCH);
                s.Send(command);
                byte[] recvBuf = Networking.my_recv(8, s);
                if (recvBuf == null)
                    return null;
                Int64 filesInfoToRecv = BitConverter.ToInt64(recvBuf, 0);
                for (int i = 0; i < filesInfoToRecv; i++)
                {
                    recvBuf = Networking.my_recv(8, s);
                    if (recvBuf == null)
                        return null;
                    Int64 id = BitConverter.ToInt64(recvBuf, 0);
                    recvBuf = Networking.my_recv(4, s);
                    if (recvBuf == null)
                        return null;
                    Int32 pathLen = BitConverter.ToInt32(recvBuf, 0);
                    byte[] encryptedData = Networking.my_recv(pathLen, s);
                    if (encryptedData == null)
                        return null;
                    String filename = Encoding.UTF8.GetString(Security.AESDecrypt(aesKey, encryptedData));
                    encryptedData = Networking.my_recv(32, s);
                    if (encryptedData == null)
                        return null;
                    String checksum = Encoding.UTF8.GetString(Security.AESDecrypt(aesKey, encryptedData));
                    DirectoryFile file = new DirectoryFile();
                    file.Id = id;
                    file.UserId = userId;
                    file.Filename = filename;
                    file.Checksum = checksum;
                    remoteStat.Files.Add(filename, file);
                }
                return remoteStat;
            }
            catch (SocketException) { }
            return null;
        }

        public void fillDirectoryStatus(DirectoryStatus local, String directory)
        {
           var entries = Directory.EnumerateFileSystemEntries(directory);
           foreach (var path in entries)
           {
               if (File.Exists(path))
               {
                   //entry is a file
                   FileInfo info = new FileInfo(path);
                   DirectoryFile file = new DirectoryFile();
                   file.Length = info.Length;
                   file.UserId = userId;
                   file.LastModificationTime = info.LastWriteTime;
                   file.Filename = info.FullName;
                   file.Checksum = getChecksum(info);
                   local.Files.Add(file.Filename, file);
               }
               if (Directory.Exists(path))
               {
                   //entry is a directory
                   fillDirectoryStatus(local, path);
               }
           }
        }

        public String getChecksum(FileInfo info)
        {
            byte[] fileBytes = new byte[info.Length];
            using (MemoryStream ms = new MemoryStream(fileBytes))
            {
                using (FileStream reader = File.OpenRead(info.FullName))
                {
                    long left = info.Length;
                    int n = 0;
                    while (left > 0)
                    {
                        int dim = (left > 8192) ? 8192 : (int)left;
                        byte[] buf = new byte[dim];
                        n = reader.Read(buf, 0, dim);
                        ms.Write(buf, 0, n);
                        left -= n;
                    }
                }
            }
            return Security.CalculateMD5Hash(fileBytes);
        }


        public void synchronize(DirectoryStatus local, DirectoryStatus remote)
        {
            try
            {
                if (local.Equals(remote))
                    return;
                byte[] command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.START_SYNCH);
                tcpClient.Client.Send(command);
                var fileAdded = local.getDifference(remote);
                foreach (var item in fileAdded)
                {
                    addFile(item.Value);
                }
                var fileDeleted = remote.getDifference(local);
                foreach (var item in fileDeleted)
                {
                    deleteFile(item.Value);
                }
                var fileUpdated = remote.getIntersect(local);
                foreach (var item in fileUpdated)
                {
                    DirectoryFile remoteFile = item.Value;
                    DirectoryFile localFile = local.Files[item.Key];
                    if (!localFile.Equals(remoteFile))
                        updateFile(localFile);
                }
                command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.END_SYNCH);
                tcpClient.Client.Send(command);
            }
            catch (Exception) { }
        }

        private void updateFile(DirectoryFile file)
        {
            try
            {
                Socket s = tcpClient.Client;
                byte[] command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.ADD);
                s.Send(command);
                int len = file.Filename.Length;
                len = (len % 16 == 0) ? len : (len + (16 - (len % 16)));
                byte[] buf = BitConverter.GetBytes(len);
                s.Send(buf);
                byte[] encryptedData = Security.AESEncrypt(aesKey, file.Filename);
                s.Send(encryptedData);
                buf = BitConverter.GetBytes(file.LastModificationTime.ToBinary());
                s.Send(buf);
                command = Networking.my_recv(4, tcpClient.Client);
                if (command != null && (
                        ((Networking.CONNECTION_CODES)BitConverter.ToUInt32(command, 0) == Networking.CONNECTION_CODES.OK)))
                {
                    long fileLen = file.Length;
                    fileLen = (fileLen % 16 == 0) ? fileLen : (fileLen + (16 - (fileLen % 16)));
                    buf = BitConverter.GetBytes(fileLen);
                    s.Send(buf);
                    using (FileStream reader = File.OpenRead(file.Filename))
                    {
                        long left = fileLen;
                        while (left > 0)
                        {
                            int dim = (left > 8192) ? 8192 : (int)left;
                            buf = new byte[dim];
                            reader.Read(buf, 0, dim);
                            s.Send(buf);
                            left -= dim;
                        }
                    }
                }
            }
            catch (SocketException) { }
        }

        private void deleteFile(DirectoryFile file)
        {
            try
            {
                Socket s = tcpClient.Client;
                byte[] command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.DEL);
                s.Send(command);
                int len = file.Filename.Length;
                len = (len % 16 == 0) ? len : (len + (16 - (len % 16)));
                byte[] buf = BitConverter.GetBytes(len);
                s.Send(buf);
                byte[] encryptedData = Security.AESEncrypt(aesKey, file.Filename);
                s.Send(encryptedData);
            }
            catch (SocketException) { }
        }

        private void addFile(DirectoryFile file)
        {
            try
            {
                Socket s = tcpClient.Client;
                byte[] command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.ADD);
                s.Send(command);
                int len = file.Filename.Length;
                len = (len % 16 == 0) ? len : (len + (16 - (len % 16)));
                byte[] buf = BitConverter.GetBytes(len);
                s.Send(buf);
                byte[] encryptedData = Security.AESEncrypt(aesKey, file.Filename);
                s.Send(encryptedData);
                buf = BitConverter.GetBytes(file.LastModificationTime.ToBinary());
                s.Send(buf);
                command = Networking.my_recv(4, tcpClient.Client);
                if (command != null && (
                        ((Networking.CONNECTION_CODES)BitConverter.ToUInt32(command, 0) == Networking.CONNECTION_CODES.OK)))
                {
                    long fileLen = file.Length;
                    fileLen = (fileLen % 16 == 0) ? fileLen : (fileLen + (16 - (fileLen % 16)));
                    buf = BitConverter.GetBytes(fileLen);
                    s.Send(buf);
                    using (FileStream reader = File.OpenRead(file.Filename))
                    {
                        long left = fileLen;
                        while (left > 0)
                        {
                            int dim = (left > 8192) ? 8192 : (int)left;
                            buf = new byte[dim];
                            reader.Read(buf, 0, dim);
                            s.Send(buf);
                            left -= dim;
                        }
                    }
                }
            }
            catch (SocketException) { }
        }
    }
}
