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
        public static String date_format = "dd/MM/yyyy-HH:mm:ss";

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

            while (!exit)
            {
                byte[] command = Utility.Networking.my_recv(4, clientSession.Socket);
                if (command != null)
                {
                    Networking.CONNECTION_CODES code = (Networking.CONNECTION_CODES)BitConverter.ToUInt32(command, 0);
                    DirectoryStatus newStatus = new DirectoryStatus();
                    newStatus.FolderPath = clientSession.CurrentStatus.FolderPath;
                    newStatus.Username = clientSession.CurrentStatus.Username;
                    
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
                            if (!this.insertUnchanged(con, clientSession.CurrentStatus, newStatus))
                                exit = true;
                            else
                            {
                                //success: commit and dispose objects
                                transaction.Commit();
                                transaction.Dispose();
                                con.Dispose();
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
            return false;
        }

        private bool insertUnchanged(SQLiteConnection conn, DirectoryStatus oldStatus, DirectoryStatus newStatus)
        {
            try
            {
                Dictionary<String, DirectoryFile> diff;
                diff = oldStatus.Files.Where(x => !newStatus.Files.ContainsKey(x.Key)).ToDictionary(x => x.Key, x => x.Value);
                foreach (var item in diff)
                {
                    using (SQLiteCommand cmd = conn.CreateCommand())
                    {
                        DirectoryFile file = item.Value;
                        cmd.CommandText = @"insert into snapshots (user_id,file_id,creation_time,checksum,path,deleted) values"
                            + " @user, @fileId, @creationTime, @checksum, @path, @deleted";
                        cmd.Parameters.AddWithValue("@user", file.UserId);
                        cmd.Parameters.AddWithValue("@fileId", file.Id);
                        cmd.Parameters.AddWithValue("@creation_time", newStatus.CreationTime.ToString(date_format));
                        cmd.Parameters.AddWithValue("@checksum", file.Checksum);
                        cmd.Parameters.AddWithValue("@path", file.Filename);
                        cmd.Parameters.AddWithValue("@deleted", file.Deleted);
                        cmd.ExecuteNonQuery();
                    }
                }
                return true;
            }
            catch (SQLiteException) { }
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
                byte[] command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.OK);
                s.Send(command);
                DirectoryFile file = clientSession.CurrentStatus.Files[filename];
                using (SQLiteCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"insert into snapshots (user_id,file_id,creation_time,checksum,path,deleted) values"
                        + " @user, @fileId, @creationTime, @checksum, @path, @deleted";
                    cmd.Parameters.AddWithValue("@user", file.UserId);
                    cmd.Parameters.AddWithValue("@fileId", file.Id);
                    cmd.Parameters.AddWithValue("@creation_time", newStatus.CreationTime.ToString(date_format));
                    cmd.Parameters.AddWithValue("@checksum", file.Checksum);
                    cmd.Parameters.AddWithValue("@path", file.Filename);
                    cmd.Parameters.AddWithValue("@deleted", true);
                    cmd.ExecuteNonQuery();
                }
                DirectoryFile dirFile = new DirectoryFile(file.Id, filename, file.UserId, file.Checksum, file.LastModificationTime);
                file.Deleted = true;
                newStatus.Files[filename] = dirFile;
                return true;
            }
            catch (SocketException) { }
            catch (SQLiteException) { }
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
                recvBuf = Networking.my_recv(8, s);
                if (recvBuf == null)
                    return false;
                DateTime lastModTime = DateTime.FromBinary(BitConverter.ToInt64(recvBuf, 0));
                byte[] command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.OK);
                s.Send(command);
                recvBuf = Networking.my_recv(8, s);
                if (recvBuf == null)
                    return false;
                encryptedData = Networking.my_recv(BitConverter.ToInt64(recvBuf, 0), s);
                if (encryptedData != null)
                    return false;
                byte[] file = Security.AESDecrypt(aes, encryptedData);
                if (file == null)
                    return false;

                using (SQLiteCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"insert into files (user_id,path,content,last_mod_time) values"
                            + " @user, @path, @content, @lastModTime";
                    cmd.Parameters.AddWithValue("@user", newStatus.Username);
                    cmd.Parameters.AddWithValue("@path", filename);
                    cmd.Parameters.AddWithValue("@content", file);
                    cmd.Parameters.AddWithValue("@lastModTime", lastModTime.ToString(date_format));
                    cmd.ExecuteNonQuery();
                }
                Int64? fileId = -1;
                using (SQLiteCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "select file_id from files where user_id = @user and path = @path and"
                            + " last_mod_time = @lastModTime";
                    cmd.Parameters.AddWithValue("@user", newStatus.Username);
                    cmd.Parameters.AddWithValue("@path", filename);
                    cmd.Parameters.AddWithValue("@lastModTime", lastModTime.ToString(date_format));
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            fileId = (Int64?)reader[0];
                        }
                    }
                }
                if (fileId == null || fileId <= 0)
                    return false;
                String checksum = Security.CalculateMD5Hash(file);
                using (SQLiteCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"insert into snapshots (user_id,file_id,creation_time,checksum,path) values"
                        + " @user, @fileId, @creationTime, @checksum, @path";
                    cmd.Parameters.AddWithValue("@user", newStatus.Username);
                    cmd.Parameters.AddWithValue("@fileId", fileId);
                    cmd.Parameters.AddWithValue("@creation_time", newStatus.CreationTime.ToString(date_format));
                    cmd.Parameters.AddWithValue("@checksum", checksum);
                    cmd.Parameters.AddWithValue("@path", filename);
                    cmd.ExecuteNonQuery();
                }
                //update file
                DirectoryFile dirFile = new DirectoryFile((Int64)fileId, filename, newStatus.Username, checksum, lastModTime);
                newStatus.Files[filename] = dirFile;
                return true;
            }
            catch (SocketException) { }
            catch (SQLiteException) { }
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
                recvBuf = Networking.my_recv(8, s);
                if (recvBuf == null)
                    return false;
                DateTime lastModTime = DateTime.FromBinary(BitConverter.ToInt64(recvBuf, 0));
                byte[] command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.OK);
                s.Send(command);
                recvBuf = Networking.my_recv(8, s);
                if (recvBuf == null)
                    return false;
                encryptedData = Networking.my_recv(BitConverter.ToInt64(recvBuf, 0), s);
                if (encryptedData != null)
                    return false;
                byte[] file = Security.AESDecrypt(aes, encryptedData);
                if (file == null)
                    return false;

                using (SQLiteCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"insert into files (user_id,path,content,last_mod_time) values"
                            + " @user, @path, @content, @lastModTime";
                    cmd.Parameters.AddWithValue("@user", newStatus.Username);
                    cmd.Parameters.AddWithValue("@path", filename);
                    cmd.Parameters.AddWithValue("@content", file);
                    cmd.Parameters.AddWithValue("@lastModTime", lastModTime.ToString(date_format));
                    cmd.ExecuteNonQuery();
                }
                Int64? fileId = -1;
                using (SQLiteCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "select file_id from files where user_id = @user and path = @path and"
                            + " last_mod_time = @lastModTime";
                    cmd.Parameters.AddWithValue("@user", newStatus.Username);
                    cmd.Parameters.AddWithValue("@path", filename);
                    cmd.Parameters.AddWithValue("@lastModTime", lastModTime.ToString(date_format));
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            fileId = (Int64?)reader[0];
                        }
                    }
                }
                if (fileId == null || fileId <= 0)
                    return false;
                String checksum = Security.CalculateMD5Hash(file);
                using (SQLiteCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"insert into snapshots (user_id,file_id,creation_time,checksum,path) values"
                        + " @user, @fileId, @creationTime, @checksum, @path";
                    cmd.Parameters.AddWithValue("@user", newStatus.Username);
                    cmd.Parameters.AddWithValue("@fileId", fileId);
                    cmd.Parameters.AddWithValue("@creation_time", newStatus.CreationTime.ToString(date_format));
                    cmd.Parameters.AddWithValue("@checksum", checksum);
                    cmd.Parameters.AddWithValue("@path", filename);
                    cmd.ExecuteNonQuery();
                }
                //add file
                DirectoryFile dirFile = new DirectoryFile((Int64)fileId, filename, newStatus.Username, checksum, lastModTime);
                newStatus.Files[filename] = dirFile;
                return true;
            }
            catch (SocketException) { }
            catch (SQLiteException) { }
            return false;
        }

        public bool StartTransferSession(ClientSession clientSession)
        {
            Socket s = clientSession.Socket;
            AesCryptoServiceProvider aes = clientSession.AESKey;
            byte[] command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.OK);
            s.Send(command);
            //receive number of files(in chiaro)
            byte[] qnt_byte = Networking.my_recv(4, s);
            if(qnt_byte==null){ 
                return false;
            }
            int qnt = BitConverter.ToInt32(qnt_byte,0);
            if (qnt > 0)
            {   SQLiteConnection con=null;
                try
                {
                    con = new SQLiteConnection(con_string);
                    con.Open();
                    SQLiteTransaction tr = con.BeginTransaction();
                    SQLiteCommand cmd = con.CreateCommand();
                    cmd.Transaction = tr;
                    //get last max id from DB
                    int last_id = DBmanager.getMaxFileId(con);
                    if (last_id >= 0)
                    {
                        for (int i = qnt; i > 0; i--, ++last_id)
                        {
                            //get path length(in chiaro)
                            byte[] length_byte = Networking.my_recv(4, s);
                            if (length_byte != null)
                            {
                                int path_len = BitConverter.ToInt32(length_byte, 0);
                                //get path(aes)
                               byte[] encryptedData = Networking.my_recv(path_len, s);
                               if (encryptedData != null)
                               {
                                   byte[] decryptedData = Security.AESDecrypt(aes, encryptedData);
                                   String path = Encoding.UTF8.GetString(decryptedData);
                                   //get mod_Date(in chiaro)
                                   //format dd/MM/yyyy-HH:mm:ss (19byte) type string
                                   byte[] mod_byte = Networking.my_recv(19, s);
                                   if (mod_byte != null)
                                   {
                                       String mod_date = Encoding.UTF8.GetString(mod_byte);
                                       //get file size (in chiaro)
                                        byte[] size_byte = Networking.my_recv(4, s);
                                        if (size_byte != null)
                                        {
                                            int size = BitConverter.ToInt32(size_byte, 0);
                                            //get content(aes)
                                            encryptedData = Networking.my_recv(size, s);
                                            if (encryptedData != null)
                                            {
                                                byte[] FILE= Security.AESDecrypt(aes, encryptedData);
                                                //insert into files
                                                cmd.CommandText = "INSERT INTO files (fileId,path,content,mod_date) values('"+last_id+"','"+path+"','"+FILE+"','"+mod_date+"')";
                                                cmd.ExecuteNonQuery();
                                                //insert into  session (fileId==last_d)
                                                DateTime now = DateTime.Now;
                                                String now_str = now.ToString(date_format);
                                                cmd.CommandText = "INSERT INTO session (user,fileId,creation,checksum,path) values('" + clientSession.User + "','" +last_id + "','" + now_str+ "','" +Security.CalculateMD5Hash(FILE) +"','" +path +"')";
                                                cmd.ExecuteNonQuery();

                                            }
                                            else { con.Close(); return false; }
                                        }
                                        else { con.Close(); return false; }
                                   }
                                   else { con.Close(); return false; }
                               }
                               else { con.Close(); return false; }

                            }
                            else { con.Close(); return false; }
                        }
                    }
                    else { con.Close(); return false; }
                    tr.Commit();
                    con.Close();
                    return true;
                }
                catch (SQLiteException) {
                    if(con_string !=null){ con.Close(); }
                    return false;
                }
            }
                return false;
        }

    }
}
