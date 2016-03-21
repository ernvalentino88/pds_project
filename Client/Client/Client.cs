using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using Utility;
using System.Windows;

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

        public Int64 getRemoteStatus(DirectoryStatus remoteStatus, String directory)
        {
            try
            {
                Socket s = tcpClient.Client;
                byte[] command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.INIT_SYNCH);
                s.Send(command);
                byte[] buf = Security.AESEncrypt(aesKey, Encoding.UTF8.GetBytes(directory));
                s.Send(BitConverter.GetBytes(buf.Length));
                s.Send(buf);
                command = Networking.my_recv(4, tcpClient.Client);
                if (command != null && (
                        ((Networking.CONNECTION_CODES)BitConverter.ToUInt32(command, 0) == Networking.CONNECTION_CODES.OK)))
                {
                    byte[] recvBuf = Networking.my_recv(4, s);
                    if (recvBuf == null)
                        return -1;
                    Int32 filesInfoToRecv = BitConverter.ToInt32(recvBuf, 0);
                    for (int i = 0; i < filesInfoToRecv; i++)
                    {
                        DirectoryFile file = new DirectoryFile();
                        recvBuf = Networking.my_recv(4, s);
                        if (recvBuf == null)
                            return -1;
                        int len = BitConverter.ToInt32(recvBuf, 0);
                        byte[] encryptedData = Networking.my_recv(len, s);
                        if (encryptedData == null)
                            return -1;
                        String path = Encoding.UTF8.GetString(Security.AESDecrypt(aesKey, encryptedData));
                        recvBuf = Networking.my_recv(4, s);
                        if (recvBuf == null)
                            return -1;
                        len = BitConverter.ToInt32(recvBuf, 0);
                        encryptedData = Networking.my_recv(len, s);
                        if (encryptedData == null)
                            return -1;
                        String filename = Encoding.UTF8.GetString(Security.AESDecrypt(aesKey, encryptedData));
                        command = Networking.my_recv(4, s);
                        if (command == null)
                            return -1;
                        if ((Networking.CONNECTION_CODES)BitConverter.ToUInt32(command, 0) == Networking.CONNECTION_CODES.DEL)
                        {
                            file.Deleted = true;
                        }
                        command = Networking.my_recv(4, s);
                        if (command == null)
                            return -1;
                        if ((Networking.CONNECTION_CODES)BitConverter.ToUInt32(command, 0) == Networking.CONNECTION_CODES.DIR)
                        {
                            file.Directory = true;
                        }
                        if ((Networking.CONNECTION_CODES)BitConverter.ToUInt32(command, 0) == Networking.CONNECTION_CODES.FILE)
                        {
                            recvBuf = Networking.my_recv(8, s);
                            if (recvBuf == null)
                                return -1;
                            Int64 id = BitConverter.ToInt64(recvBuf, 0);
                            encryptedData = Networking.my_recv(48, s);
                            if (encryptedData == null)
                                return -1;
                            String checksum = Encoding.UTF8.GetString(Security.AESDecrypt(aesKey, encryptedData));
                            file.Checksum = checksum;
                            file.Id = id;
                        }
                        file.UserId = userId;
                        file.Filename = filename;
                        file.Path = path;
                        file.Fullname = Path.Combine(path, filename);
                        remoteStatus.Files.Add(file.Fullname, file);
                    }
                    return filesInfoToRecv;
                }
            }
            catch (SocketException se) { Console.WriteLine(se.Message); }
            return -1;
        }

        public Int64 getDirectoryInfo(DirectoryStatus current, String directory)
        {
            try
            {
                Socket s = tcpClient.Client;
                byte[] command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.DIR);
                s.Send(command);
                byte[] buf = Security.AESEncrypt(aesKey, Encoding.UTF8.GetBytes(directory));
                s.Send(BitConverter.GetBytes(buf.Length));
                s.Send(buf);
                command = Networking.my_recv(4, tcpClient.Client);
                if (command != null && (
                        ((Networking.CONNECTION_CODES)BitConverter.ToUInt32(command, 0) == Networking.CONNECTION_CODES.OK)))
                {
                    byte[] recvBuf = Networking.my_recv(4, s);
                    if (recvBuf == null)
                        return -1;
                    Int32 filesInfoToRecv = BitConverter.ToInt32(recvBuf, 0);
                    for (int i = 0; i < filesInfoToRecv; i++)
                    {
                        DirectoryFile file = new DirectoryFile();
                        recvBuf = Networking.my_recv(4, s);
                        if (recvBuf == null)
                            return -1;
                        int len = BitConverter.ToInt32(recvBuf, 0);
                        byte[] encryptedData = Networking.my_recv(len, s);
                        if (encryptedData == null)
                            return -1;
                        String path = Encoding.UTF8.GetString(Security.AESDecrypt(aesKey, encryptedData));
                        recvBuf = Networking.my_recv(4, s);
                        if (recvBuf == null)
                            return -1;
                        len = BitConverter.ToInt32(recvBuf, 0);
                        encryptedData = Networking.my_recv(len, s);
                        if (encryptedData == null)
                            return -1;
                        String filename = Encoding.UTF8.GetString(Security.AESDecrypt(aesKey, encryptedData));
                        command = Networking.my_recv(4, s);
                        if (command == null)
                            return -1;
                        if ((Networking.CONNECTION_CODES)BitConverter.ToUInt32(command, 0) == Networking.CONNECTION_CODES.DEL)
                        {
                            file.Deleted = true;
                        }
                        command = Networking.my_recv(4, s);
                        if (command == null)
                            return -1;
                        if ((Networking.CONNECTION_CODES)BitConverter.ToUInt32(command, 0) == Networking.CONNECTION_CODES.DIR)
                        {
                            file.Directory = true;
                        }
                        if ((Networking.CONNECTION_CODES)BitConverter.ToUInt32(command, 0) == Networking.CONNECTION_CODES.FILE)
                        {
                            recvBuf = Networking.my_recv(8, s);
                            if (recvBuf == null)
                                return -1;
                            Int64 id = BitConverter.ToInt64(recvBuf, 0);
                            encryptedData = Networking.my_recv(48, s);
                            if (encryptedData == null)
                                return -1;
                            String checksum = Encoding.UTF8.GetString(Security.AESDecrypt(aesKey, encryptedData));
                            file.Checksum = checksum;
                            file.Id = id;
                        }
                        file.UserId = userId;
                        file.Filename = filename;
                        file.Path = path;
                        file.Fullname = Path.Combine(path, filename);
                        current.Files.Add(file.Fullname, file);
                    }
                    return filesInfoToRecv;
                }
            }
            catch (SocketException) {  }
            return -1;
        }

        public void firstSynch(DirectoryStatus local, DirectoryStatus remote, String directory)
        {
            try
            {
                if (getRemoteStatus(remote, directory) >= 0)
                {
                    synchronize(local, remote);
                }
            }
            catch (SocketException) { }
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
                   file.Filename = info.Name;
                   file.Path = info.DirectoryName;
                   file.Fullname = info.FullName;
                   file.Checksum = getChecksum(info);
                   local.Files.Add(file.Fullname, file);
               }
               if (Directory.Exists(path))
               {
                   //entry is a directory
                   DirectoryFile file = new DirectoryFile();
                   DirectoryInfo info = new DirectoryInfo(path);
                   file.UserId = userId;
                   file.Directory = true;
                   file.Filename = info.Name;
                   file.Path = info.Parent.FullName;
                   file.Fullname = info.FullName;
                   local.Files.Add(file.Fullname, file);
                   fillDirectoryStatus(local, info.FullName);
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

        public void synchronize(DirectoryStatus local, DirectoryStatus remote, MainWindow mainWindow)
        {
            try
            {
                if (local.Equals(remote))
                    return;
                byte[] command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.START_SYNCH);
                tcpClient.Client.Send(command);
                var fileAdded = local.getDifference(remote);
                var fileDeleted = remote.getDifference(local);
                var fileUpdated = remote.getIntersect(local);
                long tot = fileAdded.Count + fileDeleted.Count + fileUpdated.Count;
                long i = 0;
                foreach (var item in fileAdded)
                {
                    addFile(item.Value);
                    double percentage = (double)i / tot;
                    mainWindow.BackgroundWorker.ReportProgress((int)(percentage * 100));
                    i++;
                }
                
                foreach (var item in fileDeleted)
                {
                    if (!fileDeleted.ContainsKey(item.Value.Path))
                        deleteFile(item.Value);
                    double percentage = (double)i / tot;
                    mainWindow.BackgroundWorker.ReportProgress((int)(percentage * 100));
                    i++;
                }
                
                foreach (var item in fileUpdated)
                {
                    DirectoryFile remoteFile = item.Value;
                    DirectoryFile localFile = local.Files[item.Key];
                    if (!localFile.Equals(remoteFile))
                    {
                        updateFile(localFile);
                        double percentage = (double)i / tot;
                        mainWindow.BackgroundWorker.ReportProgress((int)(percentage * 100));
                        i++;
                    }
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
                this.resumeSession();
                Socket s = tcpClient.Client;
                byte[] command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.UPD);
                s.Send(command);
                byte[] encryptedData = Security.AESEncrypt(aesKey, file.Filename);
                byte[] buf = BitConverter.GetBytes(encryptedData.Length);
                s.Send(buf);
                s.Send(encryptedData);
                encryptedData = Security.AESEncrypt(aesKey, file.Path);
                buf = BitConverter.GetBytes(encryptedData.Length);
                s.Send(buf);
                s.Send(encryptedData);
                
                buf = BitConverter.GetBytes(file.LastModificationTime.ToBinary());
                s.Send(buf);
                command = Networking.my_recv(4, tcpClient.Client);
                if (command != null && (
                        ((Networking.CONNECTION_CODES)BitConverter.ToUInt32(command, 0) == Networking.CONNECTION_CODES.OK)))
                {
                    buf = BitConverter.GetBytes(file.Length);
                    s.Send(buf);
                    using (FileStream reader = File.OpenRead(file.Fullname))
                    {
                        long left = file.Length;
                        while (left > 0)
                        {
                            int dim = (left > 4096) ? 4096 : (int)left;
                            buf = new byte[dim];
                            reader.Read(buf, 0, dim);
                            encryptedData = Security.AESEncrypt(aesKey, buf);
                            s.Send(encryptedData);
                            //s.Send(buf);
                            left -= dim;
                        }
                    }
                }
                
            }
            catch (SocketException) { }
        }

        public void updateFile(DirectoryFile file, bool openSocket)
        {
            try
            {
                TcpClient client = new TcpClient();
                client.Connect(this.server);
                Socket s = client.Client;
                s.ReceiveTimeout = Networking.TIME_OUT_SHORT;
                s.SendTimeout = Networking.TIME_OUT_SHORT;
                //using client credential with a new socket
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
                    command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.START_SYNCH);
                    s.Send(command);
                    command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.UPD);
                    s.Send(command);
                    byte[] encryptedData = Security.AESEncrypt(aesKey, file.Filename);
                    byte[] buf = BitConverter.GetBytes(encryptedData.Length);
                    s.Send(buf);
                    s.Send(encryptedData);
                    encryptedData = Security.AESEncrypt(aesKey, file.Path);
                    buf = BitConverter.GetBytes(encryptedData.Length);
                    s.Send(buf);
                    s.Send(encryptedData);

                    buf = BitConverter.GetBytes(file.LastModificationTime.ToBinary());
                    s.Send(buf);
                    command = Networking.my_recv(4, tcpClient.Client);
                    if (command != null && (
                            ((Networking.CONNECTION_CODES)BitConverter.ToUInt32(command, 0) == Networking.CONNECTION_CODES.OK)))
                    {
                        buf = BitConverter.GetBytes(file.Length);
                        s.Send(buf);
                        using (FileStream reader = File.OpenRead(file.Fullname))
                        {
                            long left = file.Length;
                            while (left > 0)
                            {
                                int dim = (left > 4096) ? 4096 : (int)left;
                                buf = new byte[dim];
                                reader.Read(buf, 0, dim);
                                encryptedData = Security.AESEncrypt(aesKey, buf);
                                s.Send(encryptedData);
                                //s.Send(buf);
                                left -= dim;
                            }
                        }
                        command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.END_SYNCH);
                        s.Send(command);
                    }
                }
            }
            catch (SocketException) { }
        }

        private void deleteFile(DirectoryFile file)
        {
            try
            {
                this.resumeSession();
                Socket s = tcpClient.Client;
                byte[] command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.DEL);
                s.Send(command);
                byte[] encryptedData = Security.AESEncrypt(aesKey, file.Filename);
                byte[] buf = BitConverter.GetBytes(encryptedData.Length);
                s.Send(buf);
                s.Send(encryptedData);
                encryptedData = Security.AESEncrypt(aesKey, file.Path);
                buf = BitConverter.GetBytes(encryptedData.Length);
                s.Send(buf);
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
                }
            }
            catch (SocketException) { }
        }

        public void deleteFile(DirectoryFile file, bool openSocket)
        {
            try
            {
                TcpClient client = new TcpClient();
                client.Connect(this.server);
                Socket s = client.Client;
                s.ReceiveTimeout = Networking.TIME_OUT_SHORT;
                s.SendTimeout = Networking.TIME_OUT_SHORT;
                //using client credential with a new socket
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
                    command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.START_SYNCH);
                    s.Send(command);
                    command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.DEL);
                    s.Send(command);
                    byte[] encryptedData = Security.AESEncrypt(aesKey, file.Filename);
                    byte[] buf = BitConverter.GetBytes(encryptedData.Length);
                    s.Send(buf);
                    s.Send(encryptedData);
                    encryptedData = Security.AESEncrypt(aesKey, file.Path);
                    buf = BitConverter.GetBytes(encryptedData.Length);
                    s.Send(buf);
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
                    }
                    command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.END_SYNCH);
                    s.Send(command);
                }
            }
            catch (SocketException) { }
        }

        public void addFile(DirectoryFile file, bool openSocket)
        {
            try
            {
                TcpClient client = new TcpClient();
                client.Connect(this.server);
                Socket s = client.Client;
                s.ReceiveTimeout = Networking.TIME_OUT_SHORT;
                s.SendTimeout = Networking.TIME_OUT_SHORT;
                //using client credential with a new socket
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
                    command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.START_SYNCH);
                    s.Send(command);
                    command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.ADD);
                    s.Send(command);
                    byte[] encryptedData = Security.AESEncrypt(aesKey, file.Filename);
                    byte[] buf = BitConverter.GetBytes(encryptedData.Length);
                    s.Send(buf);
                    s.Send(encryptedData);
                    encryptedData = Security.AESEncrypt(aesKey, file.Path);
                    buf = BitConverter.GetBytes(encryptedData.Length);
                    s.Send(buf);
                    s.Send(encryptedData);
                    if (file.Directory)
                    {
                        command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.DIR);
                        s.Send(command);
                        command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.END_SYNCH);
                        s.Send(command);
                    }
                    else
                    {
                        command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.FILE);
                        s.Send(command);
                        buf = BitConverter.GetBytes(file.LastModificationTime.ToBinary());
                        s.Send(buf);
                        command = Networking.my_recv(4, tcpClient.Client);
                        if (command != null && (
                                ((Networking.CONNECTION_CODES)BitConverter.ToUInt32(command, 0) == Networking.CONNECTION_CODES.OK)))
                        {
                            buf = BitConverter.GetBytes(file.Length);
                            s.Send(buf);
                            using (FileStream reader = File.OpenRead(file.Fullname))
                            {
                                long left = file.Length;
                                while (left > 0)
                                {
                                    int dim = (left > 4096) ? 4096 : (int)left;
                                    buf = new byte[dim];
                                    reader.Read(buf, 0, dim);
                                    encryptedData = Security.AESEncrypt(aesKey, buf);
                                    s.Send(encryptedData);
                                    //s.Send(buf);
                                    left -= dim;
                                }
                            }
                            command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.END_SYNCH);
                            s.Send(command);
                        }
                    }
                }
            }
            catch (SocketException) { }
        }

        private void addFile(DirectoryFile file)
        {
            try
            {
                this.resumeSession();
                Socket s = tcpClient.Client;
                byte[] command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.ADD);
                s.Send(command);
                byte[] encryptedData = Security.AESEncrypt(aesKey, file.Filename);
                byte[] buf = BitConverter.GetBytes(encryptedData.Length);
                s.Send(buf);
                s.Send(encryptedData);
                encryptedData = Security.AESEncrypt(aesKey, file.Path);
                buf = BitConverter.GetBytes(encryptedData.Length);
                s.Send(buf);
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
                    buf = BitConverter.GetBytes(file.LastModificationTime.ToBinary());
                    s.Send(buf);
                    command = Networking.my_recv(4, tcpClient.Client);
                    if (command != null && (
                            ((Networking.CONNECTION_CODES)BitConverter.ToUInt32(command, 0) == Networking.CONNECTION_CODES.OK)))
                    {
                        buf = BitConverter.GetBytes(file.Length);
                        s.Send(buf);
                        using (FileStream reader = File.OpenRead(file.Fullname))
                        {
                            long left = file.Length;
                            while (left > 0)
                            {
                                int dim = (left > 4096) ? 4096 : (int)left;
                                buf = new byte[dim];
                                reader.Read(buf, 0, dim);
                                encryptedData = Security.AESEncrypt(aesKey, buf);
                                s.Send(encryptedData);
                                //s.Send(buf);
                                left -= dim;
                            }
                        }
                    }
                }
            }
            catch (SocketException) { }
        }

        public Int64 getRemoteStatus(DirectoryStatus remoteStatus, String rootDirectory, MainWindow mainWindow)
        {
            try
            {
                Socket s = tcpClient.Client;
                byte[] command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.INIT_SYNCH);
                s.Send(command);
                byte[] buf = Security.AESEncrypt(aesKey, Encoding.UTF8.GetBytes(rootDirectory));
                s.Send(BitConverter.GetBytes(buf.Length));
                s.Send(buf);
                command = Networking.my_recv(4, tcpClient.Client);
                if (command != null && (
                        ((Networking.CONNECTION_CODES)BitConverter.ToUInt32(command, 0) == Networking.CONNECTION_CODES.OK)))
                {
                    byte[] recvBuf = Networking.my_recv(4, s);
                    if (recvBuf == null)
                        return -1;
                    Int32 filesInfoToRecv = BitConverter.ToInt32(recvBuf, 0);
                    for (int i = 0; i < filesInfoToRecv; i++)
                    {
                        DirectoryFile file = new DirectoryFile();
                        recvBuf = Networking.my_recv(4, s);
                        if (recvBuf == null)
                            return -1;
                        int len = BitConverter.ToInt32(recvBuf, 0);
                        byte[] encryptedData = Networking.my_recv(len, s);
                        if (encryptedData == null)
                            return -1;
                        String path = Encoding.UTF8.GetString(Security.AESDecrypt(aesKey, encryptedData));
                        recvBuf = Networking.my_recv(4, s);
                        if (recvBuf == null)
                            return -1;
                        len = BitConverter.ToInt32(recvBuf, 0);
                        encryptedData = Networking.my_recv(len, s);
                        if (encryptedData == null)
                            return -1;
                        String filename = Encoding.UTF8.GetString(Security.AESDecrypt(aesKey, encryptedData));
                        command = Networking.my_recv(4, s);
                        if (command == null)
                            return -1;
                        if ((Networking.CONNECTION_CODES)BitConverter.ToUInt32(command, 0) == Networking.CONNECTION_CODES.DEL)
                        {
                            file.Deleted = true;
                        }
                        command = Networking.my_recv(4, s);
                        if (command == null)
                            return -1;
                        if ((Networking.CONNECTION_CODES)BitConverter.ToUInt32(command, 0) == Networking.CONNECTION_CODES.DIR)
                        {
                            file.Directory = true;
                        }
                        if ((Networking.CONNECTION_CODES)BitConverter.ToUInt32(command, 0) == Networking.CONNECTION_CODES.FILE)
                        {
                            recvBuf = Networking.my_recv(8, s);
                            if (recvBuf == null)
                                return -1;
                            Int64 id = BitConverter.ToInt64(recvBuf, 0);
                            encryptedData = Networking.my_recv(48, s);
                            if (encryptedData == null)
                                return -1;
                            String checksum = Encoding.UTF8.GetString(Security.AESDecrypt(aesKey, encryptedData));
                            file.Checksum = checksum;
                            file.Id = id;
                        }
                        file.UserId = userId;
                        file.Filename = filename;
                        file.Path = path;
                        file.Fullname = Path.Combine(path, filename);
                        remoteStatus.Files.Add(file.Fullname, file);
                    }
                    return filesInfoToRecv;
                }
            }
            catch (SocketException) { }
            return -1;
        }

        internal void firstSynch(DirectoryStatus local, DirectoryStatus remote, String RootDirectory, MainWindow mainWindow)
        {
            try
            {
                if (getRemoteStatus(remote, RootDirectory, mainWindow) >= 0)
                {
                    synchronize(local, remote, mainWindow);
                }
            }
            catch (SocketException) { }
        }

        public int getPreviousVersions(DirectoryStatus dir, String path)
        {
            try
            {
                Socket s = tcpClient.Client;
                byte[] command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.PREV);
                s.Send(command);
                byte[] buf = Security.AESEncrypt(aesKey, Encoding.UTF8.GetBytes(path));
                s.Send(BitConverter.GetBytes(buf.Length));
                s.Send(buf);
                long date = File.GetLastWriteTime(path).ToBinary();
                buf = BitConverter.GetBytes(date);
                s.Send(buf);
                command = Networking.my_recv(4, tcpClient.Client);
                if (command != null && (
                        ((Networking.CONNECTION_CODES)BitConverter.ToUInt32(command, 0) == Networking.CONNECTION_CODES.OK)))
                {
                    byte[] recvBuf = Networking.my_recv(4, s);
                    if (recvBuf == null)
                        return -1;
                    Int32 filesInfoToRecv = BitConverter.ToInt32(recvBuf, 0);
                    for (int i = 0; i < filesInfoToRecv; i++)
                    {
                        DirectoryFile file = new DirectoryFile();
                        recvBuf = Networking.my_recv(8, s);
                        if (recvBuf == null)
                            return -1;
                        DateTime lastModTime = DateTime.FromBinary(BitConverter.ToInt64(recvBuf, 0));
                        recvBuf = Networking.my_recv(8, s);
                        if (recvBuf == null)
                            return -1;
                        Int64 id = BitConverter.ToInt64(recvBuf, 0);
                        byte[] encryptedData = Networking.my_recv(48, s);
                        if (encryptedData == null)
                            return -1;
                        String checksum = Encoding.UTF8.GetString(Security.AESDecrypt(aesKey, encryptedData));
                        file.Checksum = checksum;
                        file.Id = id;
                        file.Deleted = true;
                        file.UserId = userId;
                        file.Filename = Path.GetFileName(path);
                        file.Path = Path.GetDirectoryName(path);
                        file.Fullname = path;
                        file.LastModificationTime = lastModTime;
                        String fname = file.Fullname + "_" + lastModTime;
                        dir.Files.Add(fname, file);
                    }
                    return filesInfoToRecv;
                }
            }
            catch (SocketException) { }
            return -1;
        }
    }
}
