﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SQLite;
using System.Security.Cryptography;



namespace Utility
{
    public class DBmanager
    {   
        //pc alex
        //public static String connectionString = @"Data Source=C:\Users\John\Desktop\SQLiteStudio\PDS.db;Version=3;";
        //pc ernesto
        public static String connectionString = @"Data Source=C:\Users\Ernesto\Documents\SQLiteStudio\PDS.db;Version=3;";
        public static String date_format = "yyyy-MM-dd HH:mm:ss";
        public static Int64 max_versions=3;
        public static int days_limit = 30;//espresso in giorni

        public static String find_user(String id)
        {
            try
            {
                using ( var con = new SQLiteConnection(connectionString) )
                {
                    con.Open();
                    using ( var cmd = con.CreateCommand() )
                    {
                        cmd.CommandText = @"select * from users where user_id = @id";
                        cmd.Parameters.AddWithValue("@id", id);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                if (reader["user_id"].Equals(id))
                                {
                                    String pwd = "" + reader["pwd"];
                                    return pwd;
                                }
                            }
                            return null;
                        }
                    }
                }
            }
            catch (SQLiteException)
            {
                return null;
            }
        }

        public static bool register(String id, String pass)
        {
            try
            {
                using (var con = new SQLiteConnection(connectionString))
                {
                    con.Open();
                    using (var cmd = con.CreateCommand())
                    {
                        String pwd_md5 = Security.CalculateMD5Hash(pass);
                        cmd.CommandText = @"insert into users (user_id, pwd) values (@id, @pwd);";
                        cmd.Parameters.AddWithValue("@id", id);
                        cmd.Parameters.AddWithValue("@pwd", pwd_md5);
                        if (cmd.ExecuteNonQuery() == 1)
                            return true;
                        return false;
                    }
                }
            }
            catch (SQLiteException)
            {
                return false;
            }
        }

        public static DirectoryStatus getLastSnapshot(String directory, String username)
        {
            DirectoryStatus ds = new DirectoryStatus();
            ds.FolderPath = directory;
            ds.Username = username;
            directory += "\\";
            String date = null;
            try
            {
                using (var con = new SQLiteConnection(connectionString))
                {
                    con.Open();
                    if (!cleanDBsnapshots(con, username, directory)) return ds;
                    using (var cmd = con.CreateCommand())
                    {
                        int len = directory.Length;
                        cmd.CommandText = @"select max(creation_time) from snapshots where"
                                + " user_id = @id and substr(path,1," + len + ") = @dir;";
                        cmd.Parameters.AddWithValue("@id", username);
                        cmd.Parameters.AddWithValue("@dir", directory);
                        object val = cmd.ExecuteScalar();
                        date = (val == System.DBNull.Value) ? null : (String)val;
                    }
                    if (date != null)
                    {
                        ds.CreationTime = DateTime.ParseExact(date, date_format, null);
                        using (var cmd = con.CreateCommand())
                        {
                            int len = directory.Length;
                            cmd.CommandText = @"select * from snapshots where user_id = @id"
                                    + " and substr(path,1," + len + ") = @dir"
                                    + " and creation_time = @date;";
                            cmd.Parameters.AddWithValue("@id", username);
                            cmd.Parameters.AddWithValue("@dir", directory);
                            cmd.Parameters.AddWithValue("@date", date);
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    DirectoryFile file = new DirectoryFile();
                                    file.UserId = username;
                                    file.Directory = (Boolean)reader["directory"];
                                    file.Path = (String)reader["path"];
                                    file.Path = file.Path.Substring(0,file.Path.LastIndexOf('\\'));
                                    file.Filename = (String)reader["filename"];
                                    file.Deleted = (Boolean)reader["deleted"];
                                    file.LastModificationTime = (DateTime)reader["last_mod_time"];
                                    if (!file.Directory)
                                    {
                                        file.Id = (Int64)reader["file_id"];
                                        file.Checksum = (String)reader["checksum"];
                                    }
                                    file.Fullname = Path.Combine(file.Path, file.Filename);
                                    ds.Files.Add(file.Fullname, file);
                                }
                            }
                        }
                    }
                } 
                return ds;
            }
            catch (SQLiteException) { return null; }
           
        }

        public static bool insertDirectory(SQLiteConnection conn, DirectoryStatus newStatus, String filename, String path, DateTime lastModTime)
        {
            try
            {
                using (SQLiteCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"insert into snapshots (user_id,creation_time,path,filename,last_mod_time,directory) values"
                        + " (@user, @creationTime, @path, @filename, @lastModTime, @directory);";
                    cmd.Parameters.AddWithValue("@user", newStatus.Username);
                    cmd.Parameters.AddWithValue("@creationTime", newStatus.CreationTime.ToString(date_format));
                    cmd.Parameters.AddWithValue("@path", path + "\\");
                    cmd.Parameters.AddWithValue("@filename", filename);
                    cmd.Parameters.AddWithValue("@lastModTime", lastModTime.ToString(date_format));
                    cmd.Parameters.AddWithValue("@directory", true);
                    cmd.ExecuteNonQuery();
                }
                //add directory
                DirectoryFile dirFile = new DirectoryFile(path, filename, newStatus.Username, lastModTime, true);
                newStatus.Files.Add(dirFile.Fullname, dirFile);
                return true;
            }
            catch (SQLiteException) {  return false;}
           
        }
        /***** Clean DB functions ******/

        public static bool cleanDBsnapshots(SQLiteConnection conn, DirectoryStatus newStatus, String directory)
        {
            return cleanDBsnapshots(conn, newStatus.Username, directory);
        }

        public static bool cleanDBsnapshots(SQLiteConnection conn, String user, String directory)
        {
            try
            {
                using (var transaction = conn.BeginTransaction())
                {
                    Int64? ver_count = -1;
                    DateTime? oldest = null;//? per farlo nullable
                    int len = directory.Length;

                    using (SQLiteCommand cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "select count(distinct(creation_time)),min(creation_time) from snapshots " + 
                                           "where user_id = @user and substr(path,1," + len + ") = @dir;";
                        cmd.Parameters.AddWithValue("@user", user);
                        cmd.Parameters.AddWithValue("@dir", directory);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                ver_count = (Int64?)reader[0];
                                if (ver_count > 0)
                                {
                                    //oldest = (DateTime?)reader[1];
                                    oldest = Convert.ToDateTime(reader[1]);
                                }
                            }
                        }
                    }
                    //se count=0 quindi file non esiste ancora->ok(oldest ==null)
                    //se ver_count >0 oldest non puo valere null
                    if (ver_count < 0 || (ver_count > 0 && oldest == null))
                    {
                        return false;
                    }
                    if (ver_count >= max_versions)
                    {
                        using (SQLiteCommand cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = @"DELETE from snapshots where user_id = @user and " +
                                            "substr(path,1," + len + ") = @dir and creation_time = @creationTime;";
                            cmd.Parameters.AddWithValue("@user", user);
                            cmd.Parameters.AddWithValue("@dir", directory);
                            cmd.Parameters.AddWithValue("@creationTime", ((DateTime)oldest).ToString(date_format));
                            cmd.ExecuteNonQuery();
                            transaction.Commit();
                        }
                    }
                }
                return true;
            }
            catch (SQLiteException) { return false; }
            catch (Exception) { return false; }
            
        }
        
        //n.b ho 2 funzioni per pulire files una con tipo DirectoryStatus per user e altro con String
        public static bool cleanDBfiles(SQLiteConnection conn, String user, String filename, String path)
        {
            try
            {/*//clean i dati vecchi
                List<Int64> idsToDelete = new List<Int64>();
                DateTime now = DateTime.Now;
                DateTime limit = now.Subtract(new TimeSpan(days_limit, 0, 0, 0));
                Boolean cleaned = false;
                using (SQLiteCommand cmd = conn.CreateCommand()) 
                {
                    cmd.CommandText = "select file_id from files where user_id = @user and path = @path and"
                        + " filename = @filename and last_mod_time < @lastModTime;";
                    cmd.Parameters.AddWithValue("@user", user);
                    cmd.Parameters.AddWithValue("@path", path + "\\");
                    cmd.Parameters.AddWithValue("@filename", filename);
                    cmd.Parameters.AddWithValue("@lastModTime", ((DateTime)limit).ToString(date_format));
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            idsToDelete.Add((Int64)reader[0]);
                        }
                    }
                }
                if (idsToDelete.Count > 0) 
                { 
                    cleaned = true; 
                }
                foreach (Int64 id in idsToDelete)
                {
                    using (SQLiteCommand cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"DELETE from files where user_id=@user and file_id=@id;";
                        cmd.Parameters.AddWithValue("@user", user);
                        cmd.Parameters.AddWithValue("@id", id);
                        cmd.ExecuteNonQuery();
                    }
                    using (SQLiteCommand cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"DELETE from snapshots where user_id=@user and file_id=@id;";
                        cmd.Parameters.AddWithValue("@user", user);
                        cmd.Parameters.AddWithValue("@id", id);
                        cmd.ExecuteNonQuery();
                    }
                }
                

                //se ho cancellato almeno una riga sono sicuro che sto sotto limite
                if (!cleaned)
                {*/
                    Int64? ver_count = -1;
                    DateTime? oldest = null;//? per farlo nullable
                    Int64 fileid = -1;

                    using (SQLiteCommand cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "select count(*),min(last_mod_time) from files where user_id = @user and path = @path and"
                                + " filename = @filename;";
                        cmd.Parameters.AddWithValue("@user", user);
                        cmd.Parameters.AddWithValue("@path", path + "\\");
                        cmd.Parameters.AddWithValue("@filename", filename);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                ver_count = (Int64?)reader[0];
                                if (ver_count > 0)
                                {
                                    //oldest = (DateTime?)reader[1];
                                    oldest = Convert.ToDateTime(reader[1]);
                                }
                            }
                        }

                    }
                    //se count=0 quindi file non esiste ancora->ok(oldest ==null)
                    //se ver_count >0 oldest non puo valere null
                    if (ver_count < 0 || (ver_count > 0 && oldest == null))
                    {
                        return false;
                    }

                    if (ver_count >= max_versions)
                    {
                        using (SQLiteCommand cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = "select file_id from files where user_id = @user and path = @path and"
                               + " filename = @filename and last_mod_time= @lastModTime;";
                            cmd.Parameters.AddWithValue("@user", user);
                            cmd.Parameters.AddWithValue("@path", path + "\\");
                            cmd.Parameters.AddWithValue("@filename", filename);
                            cmd.Parameters.AddWithValue("@lastModTime", ((DateTime)oldest).ToString(date_format));
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    fileid = (Int64)reader[0];
                                }
                            }
                            if (fileid < 0) return false;

                        }
                        using (SQLiteCommand cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = @"DELETE from files where user_id=@user and path=@path and filename=@filename and last_mod_time= @lastModTime;";
                            cmd.Parameters.AddWithValue("@user", user);
                            cmd.Parameters.AddWithValue("@path", path + "\\");
                            cmd.Parameters.AddWithValue("@filename", filename);
                            cmd.Parameters.AddWithValue("@lastModTime", ((DateTime)oldest).ToString(date_format));
                            cmd.ExecuteNonQuery();
                        }
                        using (SQLiteCommand cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = @"DELETE from snapshots where user_id=@user and file_id=@id ;";
                            cmd.Parameters.AddWithValue("@user", user);
                            cmd.Parameters.AddWithValue("@id", fileid);
                            cmd.ExecuteNonQuery();
                        }
                    }
               // }
                return true;
            }
            catch (SQLiteException) { return false; }
            catch (Exception) { return false; }
           
        
        }

        public static bool cleanDBfiles(SQLiteConnection conn, DirectoryStatus newStatus, String filename, String path)
        {
          return cleanDBfiles(conn, newStatus.Username, filename, path);
        }
        /***** END CLEAN DB ******/
        public static bool insertFile(SQLiteConnection conn, DirectoryStatus newStatus, String filename, String path, byte[] file, DateTime lastModTime)
        {
            try
            {   /*****DB cleaning******/
                if (!cleanDBfiles(conn, newStatus, filename, path)) return false;
                /*****END CLEANING******/
                using (SQLiteCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"insert into files (user_id,path,filename,content,last_mod_time) values"
                            + " (@user, @path, @filename, @content, @lastModTime);";
                    cmd.Parameters.AddWithValue("@user", newStatus.Username);
                    cmd.Parameters.AddWithValue("@path", path + "\\");
                    cmd.Parameters.AddWithValue("@filename", filename);
                    cmd.Parameters.AddWithValue("@content", file);
                    cmd.Parameters.AddWithValue("@lastModTime", lastModTime.ToString(date_format));
                    cmd.ExecuteNonQuery();
                }
                Int64? fileId = -1;
                using (SQLiteCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "select file_id from files where user_id = @user and path = @path and"
                            + " filename = @filename and last_mod_time = @lastModTime;";
                    cmd.Parameters.AddWithValue("@user", newStatus.Username);
                    cmd.Parameters.AddWithValue("@path", path + "\\");
                    cmd.Parameters.AddWithValue("@filename", filename);
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
                    cmd.CommandText = @"insert into snapshots (user_id,file_id,creation_time,checksum,path,last_mod_time,filename) values"
                        + " (@user, @fileId, @creationTime, @checksum, @path, @lastModTime, @filename);";
                    cmd.Parameters.AddWithValue("@user", newStatus.Username);
                    cmd.Parameters.AddWithValue("@fileId", fileId);
                    cmd.Parameters.AddWithValue("@creationTime", newStatus.CreationTime.ToString(date_format));
                    cmd.Parameters.AddWithValue("@checksum", checksum);
                    cmd.Parameters.AddWithValue("@lastModTime", lastModTime.ToString(date_format));
                    cmd.Parameters.AddWithValue("@path", path + "\\");
                    cmd.Parameters.AddWithValue("@filename", filename);
                    cmd.ExecuteNonQuery();
                }
                //add file
                DirectoryFile dirFile = new DirectoryFile((Int64)fileId, path, filename, newStatus.Username, checksum, lastModTime);
                newStatus.Files.Add(dirFile.Fullname, dirFile);
                return true;
            }
            catch (SQLiteException) {  return false;}
           
        }


        public static bool deleteDirectory(SQLiteConnection conn, DirectoryStatus newStatus, DirectoryStatus oldStatus, String path, DateTime lastModTime)
        {
            try
            {
                path += "\\";
                Dictionary<String, DirectoryFile> del = new Dictionary<String,DirectoryFile>();
                foreach (var item in oldStatus.Files)
                {
                    String s = item.Key + "\\";
                    if (s.Length >= path.Length)
                    {
                        if (s.Substring(0, path.Length).Equals(path))
                        {
                            // file or directory contained in the directory to delete
                            del.Add(item.Key, item.Value);
                        }
                    }
                }

                foreach (var item in del)
                {

                    using (SQLiteCommand cmd = conn.CreateCommand())
                    {
                        DirectoryFile file = item.Value;
                        if (file.Directory)
                        {
                            cmd.CommandText = @"insert into snapshots (user_id,creation_time,path,filename,"
                                + " last_mod_time,directory,deleted) values (@user, @creationTime, @path,"
                                + " @filename, @lastModTime, @directory, @deleted);";
                            cmd.Parameters.AddWithValue("@user", newStatus.Username);
                            cmd.Parameters.AddWithValue("@creationTime", newStatus.CreationTime.ToString(date_format));
                            cmd.Parameters.AddWithValue("@path", file.Path + "\\");
                            cmd.Parameters.AddWithValue("@filename", file.Filename);
                            cmd.Parameters.AddWithValue("@lastModTime", file.LastModificationTime.ToString(date_format));
                            cmd.Parameters.AddWithValue("@directory", file.Directory);
                            cmd.Parameters.AddWithValue("@deleted", true);
                            cmd.ExecuteNonQuery();
                            file.Deleted = true;
                            DirectoryFile newFile = file.clone();
                            newStatus.Files.Add(newFile.Fullname, newFile);
                        }
                        else
                        {
                            cmd.CommandText = @"insert into snapshots (user_id,file_id,creation_time,checksum,path,filename,"
                                + " last_mod_time,deleted) values (@user, @fileId, @creationTime, @checksum, @path,"
                                + " @filename, @lastModTime, @deleted);";
                            cmd.Parameters.AddWithValue("@user", file.UserId);
                            cmd.Parameters.AddWithValue("@fileId", file.Id);
                            cmd.Parameters.AddWithValue("@creationTime", newStatus.CreationTime.ToString(date_format));
                            cmd.Parameters.AddWithValue("@checksum", file.Checksum);
                            cmd.Parameters.AddWithValue("@path", file.Path + "\\");
                            cmd.Parameters.AddWithValue("@filename", file.Filename);
                            cmd.Parameters.AddWithValue("@lastModTime", file.LastModificationTime.ToString(date_format));
                            cmd.Parameters.AddWithValue("@deleted", true);
                            cmd.ExecuteNonQuery();
                            file.Deleted = true;
                            DirectoryFile newFile = file.clone();
                            newStatus.Files.Add(newFile.Fullname, newFile);
                        }
                    }
                }
                return true;
            }
            catch (SQLiteException) { return false;}
            
        }

        public static bool deleteFile(SQLiteConnection conn, DirectoryFile file, DirectoryStatus newStatus)
        {
            try
            {
                using (SQLiteCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"insert into snapshots (user_id,file_id,creation_time,checksum,path,"
                        + " filename,last_mod_time,deleted) values (@user, @fileId, @creationTime, @checksum, @path,"
                        + " @filename, @lastModTime, @deleted);";
                    cmd.Parameters.AddWithValue("@user", file.UserId);
                    cmd.Parameters.AddWithValue("@fileId", file.Id);
                    cmd.Parameters.AddWithValue("@creationTime", newStatus.CreationTime.ToString(date_format));
                    cmd.Parameters.AddWithValue("@checksum", file.Checksum);
                    cmd.Parameters.AddWithValue("@path", file.Path + "\\");
                    cmd.Parameters.AddWithValue("@filename", file.Filename);
                    cmd.Parameters.AddWithValue("@lastModTime", file.LastModificationTime.ToString(date_format));
                    cmd.Parameters.AddWithValue("@deleted", true);
                    cmd.ExecuteNonQuery();
                }
                DirectoryFile dirFile = new DirectoryFile(file.Id, file.Path, file.Filename, file.UserId, file.Checksum, file.LastModificationTime);
                file.Deleted = true;
                newStatus.Files.Add(dirFile.Fullname, dirFile);
                return true;
            }
            catch (SQLiteException) {   return false; }
         
        }

        public static bool insertUnchanged(SQLiteConnection conn, DirectoryStatus oldStatus, DirectoryStatus newStatus)
        {
            try
            {
                if (oldStatus.Equals(newStatus))
                    return true;
                Dictionary<String, DirectoryFile> diff;
                diff = oldStatus.Files.Where(x => !newStatus.Files.ContainsKey(x.Key)).ToDictionary(x => x.Key, x => x.Value);
                
                foreach (var item in diff)
                {

                    using (SQLiteCommand cmd = conn.CreateCommand())
                    {
                        DirectoryFile file = item.Value;
                        if (file.Directory)
                        {
                            cmd.CommandText = @"insert into snapshots (user_id,creation_time,path,filename,last_mod_time,directory,deleted) values"
                                + " (@user, @creationTime, @path, @filename, @lastModTime, @directory, @deleted);";
                            cmd.Parameters.AddWithValue("@user", newStatus.Username);
                            cmd.Parameters.AddWithValue("@creationTime", newStatus.CreationTime.ToString(date_format));
                            cmd.Parameters.AddWithValue("@path", file.Path + "\\");
                            cmd.Parameters.AddWithValue("@filename", file.Filename);
                            cmd.Parameters.AddWithValue("@lastModTime", file.LastModificationTime.ToString(date_format));
                            cmd.Parameters.AddWithValue("@directory", file.Directory);
                            cmd.Parameters.AddWithValue("@deleted", file.Deleted);
                            cmd.ExecuteNonQuery();
                            DirectoryFile newFile = file.clone();
                            newStatus.Files.Add(newFile.Fullname, newFile);
                        }
                        else
                        {
                            cmd.CommandText = @"insert into snapshots (user_id,file_id,creation_time,checksum,path,filename,last_mod_time,deleted) values"
                                + " (@user, @fileId, @creationTime, @checksum, @path, @filename, @lastModTime, @deleted);";
                            cmd.Parameters.AddWithValue("@user", file.UserId);
                            cmd.Parameters.AddWithValue("@fileId", file.Id);
                            cmd.Parameters.AddWithValue("@creationTime", newStatus.CreationTime.ToString(date_format));
                            cmd.Parameters.AddWithValue("@checksum", file.Checksum);
                            cmd.Parameters.AddWithValue("@path", file.Path + "\\");
                            cmd.Parameters.AddWithValue("@filename", file.Filename);
                            cmd.Parameters.AddWithValue("@lastModTime", file.LastModificationTime.ToString(date_format));
                            cmd.Parameters.AddWithValue("@deleted", file.Deleted);
                            cmd.ExecuteNonQuery();
                            DirectoryFile newFile = file.clone();
                            newStatus.Files.Add(newFile.Fullname, newFile);
                        }
                    }
                }
                return true;
            }
            catch (SQLiteException) { return false;}
            
        }


        public static DirectoryStatus getRequestedDirectory(String directory, String username)
        {
            DirectoryStatus ds = new DirectoryStatus();
            ds.FolderPath = directory;
            ds.Username = username;
            String date = null;
            try
            {
                using (var con = new SQLiteConnection(connectionString))
                {
                    con.Open();
                    using (var cmd = con.CreateCommand())
                    {
                        int len = directory.Length + 1;
                        cmd.CommandText = @"select max(creation_time) from snapshots where"
                                + " user_id = @id and path = @dir;";
                        cmd.Parameters.AddWithValue("@id", username);
                        cmd.Parameters.AddWithValue("@dir", directory + "\\");
                        object val = cmd.ExecuteScalar();
                        date = (val == System.DBNull.Value) ? null : (String)val;
                    }
                    if (date != null)
                    {
                        ds.CreationTime = DateTime.ParseExact(date, date_format, null);
                        using (var cmd = con.CreateCommand())
                        {
                            int len = directory.Length;
                            cmd.CommandText = @"select * from snapshots where user_id = @id"
                                    + " and path = @dir and creation_time = @date;";
                            cmd.Parameters.AddWithValue("@id", username);
                            cmd.Parameters.AddWithValue("@dir", directory + "\\");
                            cmd.Parameters.AddWithValue("@date", date);
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    DirectoryFile file = new DirectoryFile();
                                    file.UserId = username;
                                    file.Directory = (Boolean)reader["directory"];
                                    file.Path = (String)reader["path"];
                                    file.Path = file.Path.Substring(0, file.Path.LastIndexOf('\\'));
                                    file.Filename = (String)reader["filename"];
                                    file.Deleted = (Boolean)reader["deleted"];
                                    file.LastModificationTime = (DateTime)reader["last_mod_time"];
                                    if (!file.Directory)
                                    {
                                        file.Id = (Int64)reader["file_id"];
                                        file.Checksum = (String)reader["checksum"];
                                    }
                                    file.Fullname = Path.Combine(file.Path, file.Filename);
                                    ds.Files.Add(file.Fullname, file);
                                }
                            }
                        }
                    }
                }
                return ds;
            }
            catch (SQLiteException) { return ds; }
            
        }

        public static DirectoryStatus getPreviousVersion(String path, String username, DateTime date)
        {
            DirectoryStatus ds = new DirectoryStatus();
            ds.FolderPath = path;
            ds.Username = username;
            try
            {
                using (var con = new SQLiteConnection(connectionString))
                {
                    con.Open();
                    using (var cmd = con.CreateCommand())
                    {
                        cmd.CommandText = @"select path,filename,file_id,last_mod_time,content"
                                + " from files where user_id = @id"
                                + " and path = @dir and filename = @filename"
                                + " and last_mod_time < @date;";
                        cmd.Parameters.AddWithValue("@id", username);
                        cmd.Parameters.AddWithValue("@dir", Path.GetDirectoryName(path) + "\\");
                        cmd.Parameters.AddWithValue("@filename", Path.GetFileName(path));
                        cmd.Parameters.AddWithValue("@date", date.ToString(date_format));
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                DirectoryFile file = new DirectoryFile();
                                file.UserId = username;
                                file.Path = (String)reader["path"];
                                file.Path = file.Path.Substring(0, file.Path.LastIndexOf('\\'));
                                file.Filename = (String)reader["filename"];
                                file.Id = (Int64)reader["file_id"];
                                file.Checksum = Security.CalculateMD5Hash((byte[])reader["content"]);
                                file.LastModificationTime = (DateTime)reader["last_mod_time"];
                                file.Deleted = true;
                                file.Fullname = Path.Combine(file.Path, file.Filename);
                                ds.Files.Add(file.NameVersion, file);
                            }
                        }
                    }
                    
                }
                return ds;
            }
            catch (SQLiteException) { return ds; }
            
        }

        public static bool deleteDirectory(SQLiteConnection con, String path, String user, DateTime creationTime)
        {
            try
            {
                using (var cmd = con.CreateCommand())
                {
                    cmd.CommandText = @"update snapshots set deleted = @del where"
                            + " user_id = @user and creation_time = @creationTime"
                            + " and path = @dir and filename = @name;";
                    cmd.Parameters.AddWithValue("@del", true);
                    cmd.Parameters.AddWithValue("@user", user);
                    cmd.Parameters.AddWithValue("@creationTime", creationTime.ToString(date_format));
                    cmd.Parameters.AddWithValue("@dir", Path.GetDirectoryName(path) + "\\");
                    cmd.Parameters.AddWithValue("@name", Path.GetFileName(path));
                    cmd.ExecuteNonQuery();
                }
                using (var cmd = con.CreateCommand())
                {
                    int len = path.Length + 1;
                    cmd.CommandText = @"update snapshots set deleted = @del where"
                            + " user_id = @user and creation_time = @creationTime"
                            + " and substr(path,1," + len + ") = @dir;";
                    cmd.Parameters.AddWithValue("@del", true);
                    cmd.Parameters.AddWithValue("@user", user);
                    cmd.Parameters.AddWithValue("@creationTime", creationTime.ToString(date_format));
                    cmd.Parameters.AddWithValue("@dir", path + "\\");
                    cmd.ExecuteNonQuery();
                }
                return true;
            }
            catch (SQLiteException) {  return false;}
           
        }

        public static bool deleteFile(SQLiteConnection con, String path, String filename, String user, DateTime creationTime)
        {
            try
            {
                using (var cmd = con.CreateCommand())
                {
                    cmd.CommandText = @"update snapshots set deleted = @del where"
                            + " user_id = @user and creation_time = @creationTime"
                            + " and path = @dir and filename = @filename;";
                    cmd.Parameters.AddWithValue("@del", true);
                    cmd.Parameters.AddWithValue("@user", user);
                    cmd.Parameters.AddWithValue("@creationTime", creationTime.ToString(date_format));
                    cmd.Parameters.AddWithValue("@dir", path + "\\");
                    cmd.Parameters.AddWithValue("@filename", filename);
                    cmd.ExecuteNonQuery();
                }
                return true;
            }
            catch (SQLiteException) { return false;}
            
        }

        public static bool insertDirectory(SQLiteConnection conn, String path, String filename, String user, DateTime creationTime, DateTime lastModTime)
        {
            try
            {
                int n = -1;
                using (SQLiteCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"select count(*) from snapshots where user_id = @user"
                        + " and creation_time = @creationTime and path = @path and filename = @filename;";
                    cmd.Parameters.AddWithValue("@user", user);
                    cmd.Parameters.AddWithValue("@creationTime", creationTime.ToString(date_format));
                    cmd.Parameters.AddWithValue("@path", path + "\\");
                    cmd.Parameters.AddWithValue("@filename", filename);
                    object val = cmd.ExecuteScalar();
              
                    n = (val.ToString().Equals("0")) ? 0 : 1;
                }
                if (n > 0)
                {
                    using (SQLiteCommand cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"update snapshots set deleted = @del where user_id = @user"
                            + " and creation_time = @creationTime and path = @path and filename = @filename;";
                        cmd.Parameters.AddWithValue("@del", false);
                        cmd.Parameters.AddWithValue("@user", user);
                        cmd.Parameters.AddWithValue("@creationTime", creationTime.ToString(date_format));
                        cmd.Parameters.AddWithValue("@path", path + "\\");
                        cmd.Parameters.AddWithValue("@filename", filename);
                        cmd.ExecuteNonQuery();
                    }
                    return true;
                }
                else
                {
                    using (SQLiteCommand cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"insert into snapshots (user_id,creation_time,path,filename,last_mod_time,directory) values"
                            + " (@user, @creationTime, @path, @filename, @lastModTime, @directory);";
                        cmd.Parameters.AddWithValue("@user", user);
                        cmd.Parameters.AddWithValue("@creationTime", creationTime.ToString(date_format));
                        cmd.Parameters.AddWithValue("@path", path + "\\");
                        cmd.Parameters.AddWithValue("@filename", filename);
                        cmd.Parameters.AddWithValue("@lastModTime", lastModTime.ToString(date_format));
                        cmd.Parameters.AddWithValue("@directory", true);
                        cmd.ExecuteNonQuery();
                    }
                    return true;
                }
            }
            catch (SQLiteException) { return false;}
            
        }

        public static bool insertFile(SQLiteConnection conn, String path, String filename, byte[] file, String user, DateTime creationTime, DateTime lastModTime)
        {
            try
            {   /*****DB cleaning******/
                if (!cleanDBfiles(conn, user, filename, path)) return false;
                /*****END CLEANING******/
                using (SQLiteCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"insert into files (user_id,path,filename,content,last_mod_time) values"
                            + " (@user, @path, @filename, @content, @lastModTime);";
                    cmd.Parameters.AddWithValue("@user", user);
                    cmd.Parameters.AddWithValue("@path", path + "\\");
                    cmd.Parameters.AddWithValue("@filename", filename);
                    cmd.Parameters.AddWithValue("@content", file);
                    cmd.Parameters.AddWithValue("@lastModTime", lastModTime.ToString(date_format));
                    cmd.ExecuteNonQuery();
                }
                Int64? fileId = -1;
                using (SQLiteCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "select file_id from files where user_id = @user and path = @path and"
                            + " filename = @filename and last_mod_time = @lastModTime;";
                    cmd.Parameters.AddWithValue("@user", user);
                    cmd.Parameters.AddWithValue("@path", path + "\\");
                    cmd.Parameters.AddWithValue("@filename", filename);
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
                    cmd.CommandText = @"insert into snapshots (user_id,file_id,creation_time,checksum,path,last_mod_time,filename) values"
                        + " (@user, @fileId, @creationTime, @checksum, @path, @lastModTime, @filename);";
                    cmd.Parameters.AddWithValue("@user", user);
                    cmd.Parameters.AddWithValue("@fileId", fileId);
                    cmd.Parameters.AddWithValue("@creationTime", creationTime.ToString(date_format));
                    cmd.Parameters.AddWithValue("@checksum", checksum);
                    cmd.Parameters.AddWithValue("@lastModTime", lastModTime.ToString(date_format));
                    cmd.Parameters.AddWithValue("@path", path + "\\");
                    cmd.Parameters.AddWithValue("@filename", filename);
                    cmd.ExecuteNonQuery();
                }
                return true;
            }
            catch (SQLiteException) { return false; }
           
        }

        public static bool renameFile(SQLiteConnection conn, String user, String path, String oldName, String newName) {

            try {
                using (SQLiteCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"update  snapshots set filename=@newName where user_id=@user and filename=@oldName"
                           + " and path=@dir;";
                    cmd.Parameters.AddWithValue("@user", user);
                    cmd.Parameters.AddWithValue("@newName", newName);
                    cmd.Parameters.AddWithValue("@oldName",oldName);
                    cmd.Parameters.AddWithValue("@dir", path + "\\");
                    cmd.ExecuteNonQuery();
                }
                using (SQLiteCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"update  files set filename=@newName where user_id=@user and filename=@oldName"
                           + " and path=@dir;";
                    cmd.Parameters.AddWithValue("@user", user);
                    cmd.Parameters.AddWithValue("@newName", newName);
                    cmd.Parameters.AddWithValue("@oldName", oldName);
                    cmd.Parameters.AddWithValue("@dir", path + "\\");
                    cmd.ExecuteNonQuery();
                }
                return true;
            }
            catch (SQLiteException) { return false; }
        
        }

        public static bool renameDirectory(SQLiteConnection conn, String user, String oldPath, String newPath, DateTime creation_time) {
            try
            {
             
                using (SQLiteCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"update  snapshots set filename=@newName where user_id=@user and filename=@oldName"
                        + " and path=@dir and creation_time=@creation_time;";
                    cmd.Parameters.AddWithValue("@user", user);
                    cmd.Parameters.AddWithValue("@newName", Path.GetFileName(newPath));
                    cmd.Parameters.AddWithValue("@oldName", Path.GetFileName(oldPath));
                    cmd.Parameters.AddWithValue("@dir", Path.GetDirectoryName(oldPath) + "\\");
                    cmd.Parameters.AddWithValue("@creation_time", creation_time.ToString(date_format));
                    if (cmd.ExecuteNonQuery() < 1)
                        return false;
                }
                using (SQLiteCommand cmd = conn.CreateCommand())
                {

                    cmd.CommandText = @"update  files set path=replace(path,@oldPath,@newPath) where file_id IN (select file_id from snapshots where user_id=@user"
                        + " and substr(path,1," + (oldPath.Length+1) + ")=@oldPath and creation_time=@creation_time);";
                    cmd.Parameters.AddWithValue("@user", user);
                    cmd.Parameters.AddWithValue("@newPath", newPath + "\\");
                    cmd.Parameters.AddWithValue("@oldPath", oldPath + "\\");
                    cmd.Parameters.AddWithValue("@creation_time", creation_time.ToString(date_format));
                    cmd.ExecuteNonQuery();
                }
                using (SQLiteCommand cmd = conn.CreateCommand())
                {

                    cmd.CommandText = @"update  snapshots set path=replace(path,@oldPath,@newPath)  where user_id=@user"
                        + " and substr(path,1," + (oldPath.Length+1) + ")=@oldPath and creation_time=@creation_time;";
                    cmd.Parameters.AddWithValue("@user", user);
                    cmd.Parameters.AddWithValue("@newPath", newPath + "\\");
                    cmd.Parameters.AddWithValue("@oldPath", oldPath + "\\");
                    cmd.Parameters.AddWithValue("@creation_time", creation_time.ToString(date_format));
                    cmd.ExecuteNonQuery();
                }
                  
                return true;  
            }
            catch (SQLiteException) { 
                return false; 
            }
        }


        public static bool updateFile(SQLiteConnection conn, String path, String filename, String user, byte[] file, DateTime creationTime, DateTime lastModTime)
        {
            try
            {   /*****DB cleaning******/
                if (!cleanDBfiles(conn, user, filename, path)) return false;
                /*****END CLEANING******/
                using (SQLiteCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"insert into files (user_id,path,filename,content,last_mod_time) values"
                            + " (@user, @path, @filename, @content, @lastModTime);";
                    cmd.Parameters.AddWithValue("@user", user);
                    cmd.Parameters.AddWithValue("@path", path + "\\");
                    cmd.Parameters.AddWithValue("@filename", filename);
                    cmd.Parameters.AddWithValue("@content", file);
                    cmd.Parameters.AddWithValue("@lastModTime", lastModTime.ToString(date_format));
                    cmd.ExecuteNonQuery();
                }
                Int64? fileId = -1;
                using (SQLiteCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "select file_id from files where user_id = @user and path = @path and"
                            + " filename = @filename and last_mod_time = @lastModTime;";
                    cmd.Parameters.AddWithValue("@user", user);
                    cmd.Parameters.AddWithValue("@path", path + "\\");
                    cmd.Parameters.AddWithValue("@filename", filename);
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
                    cmd.CommandText = @"update snapshots set file_id = @id, checksum = @checksum,"
                            + " last_mod_time = @lastModTime where user_id = @user and"
                            + " creation_time = @creationTime and path = @dir and filename = @filename;";
                    cmd.Parameters.AddWithValue("@id", fileId);
                    cmd.Parameters.AddWithValue("@checksum", checksum);
                    cmd.Parameters.AddWithValue("@lastModTime", lastModTime.ToString(date_format));
                    cmd.Parameters.AddWithValue("@user", user);
                    cmd.Parameters.AddWithValue("@creationTime", creationTime.ToString(date_format));
                    cmd.Parameters.AddWithValue("@dir", path + "\\");
                    cmd.Parameters.AddWithValue("@filename", filename);
                    cmd.ExecuteNonQuery();
                }
                return true;
            }
            catch (SQLiteException) {  return false;}
           
        }

        public static List<Int64> getFilesDeletedIDs(String directory, String user, ref DateTime creationTime)
        {
            List<Int64> ids = new List<Int64>();
            directory += "\\";
            String date = null;
            try
            {
                using (var con = new SQLiteConnection(connectionString))
                {
                    con.Open();
                    using (var cmd = con.CreateCommand())
                    {
                        int len = directory.Length;
                        cmd.CommandText = @"select max(creation_time) from snapshots where"
                                + " user_id = @id and substr(path,1," + len + ") = @dir;";
                        cmd.Parameters.AddWithValue("@id", user);
                        cmd.Parameters.AddWithValue("@dir", directory);
                        object val = cmd.ExecuteScalar();
                        date = (val == System.DBNull.Value) ? null : (String)val;
                    }
                    if (date != null)
                    {
                        creationTime = DateTime.ParseExact(date, date_format, null);
                        using (var cmd = con.CreateCommand())
                        {
                            int len = directory.Length;
                            cmd.CommandText = @"select file_id, directory from snapshots where user_id = @id"
                                    + " and substr(path,1," + len + ") = @dir  and creation_time = @date;";
                            cmd.Parameters.AddWithValue("@id", user);
                            cmd.Parameters.AddWithValue("@dir", directory);
                            cmd.Parameters.AddWithValue("@date", date);
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    Boolean isDir = (Boolean)reader["directory"];
                                    if (!isDir)
                                    {
                                        Int64 id = (Int64)reader["file_id"];
                                        ids.Add(id);
                                    }
                                }
                            }
                        }
                    }
                }
                return ids;
            }
            catch (SQLiteException) {  return ids;}
           
        }

        public static List<Int64> getFilesIdToDownload(String directory, String user, DateTime creationTime)
        {
            List<Int64> ids = new List<Int64>();
            directory += "\\";
            
            try
            {
                using (var con = new SQLiteConnection(connectionString))
                {
                    con.Open();
                 
                        using (var cmd = con.CreateCommand())
                        {
                            int len = directory.Length;
                            cmd.CommandText = @"select file_id, directory from snapshots where user_id = @id"
                                    + " and substr(path,1," + len + ") = @dir  and creation_time = @date;";
                            cmd.Parameters.AddWithValue("@id", user);
                            cmd.Parameters.AddWithValue("@dir", directory);
                            cmd.Parameters.AddWithValue("@date", creationTime.ToString(date_format));
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    Boolean isDir = (Boolean)reader["directory"];
                                    if (!isDir)
                                    {
                                        Int64 id = (Int64)reader["file_id"];
                                        ids.Add(id);
                                    }
                                }
                            }
                        }
                        return ids;             
                }
            }
            catch (SQLiteException) {  return ids;}
           
        }


        public static void getFileFromID(Int64 id, ref String path, ref String filename, ref byte[] file)
        {
            try
            {
                using (var con = new SQLiteConnection(DBmanager.connectionString))
                {
                    con.Open();
                    using (var cmd = con.CreateCommand())
                    {
                        cmd.CommandText = "select * from files where file_id = @id;";
                        cmd.Parameters.AddWithValue("@id", id);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                path = (String)reader["path"];
                                path = path.Substring(0, path.LastIndexOf('\\'));
                                filename = (String)reader["filename"];
                                file = (byte[])reader["content"];
                            }
                        }
                    }
                }
            }
            catch (SQLiteException) { return; }
        }

        public static bool restoreFile(Int64 id, String path, String name, String user, DateTime lastModTime, DateTime creationTime)
        {
            try
            {
                using (var con = new SQLiteConnection(DBmanager.connectionString))
                {
                    con.Open();
                    using (var tr = con.BeginTransaction())
                    {
                        using (var cmd = con.CreateCommand())
                        {
                            //update the file in the snapshot 
                            cmd.CommandText = @"update snapshots set deleted = @del, last_mod_time = @lastModTime"
                            + " where user_id = @user and creation_time = @creationTime and path = @dir and filename = @filename;";
                            cmd.Parameters.AddWithValue("@del", false);
                            cmd.Parameters.AddWithValue("@lastModTime", lastModTime.ToString(date_format));
                            cmd.Parameters.AddWithValue("@user", user);
                            cmd.Parameters.AddWithValue("@creationTime", creationTime.ToString(date_format));
                            cmd.Parameters.AddWithValue("@dir", path + "\\");
                            cmd.Parameters.AddWithValue("@filename", name);
                            cmd.ExecuteNonQuery();
                        }

                        using (var cmd = con.CreateCommand())
                        {
                            //update the last modification time in files 
                            cmd.CommandText = @"update files set last_mod_time = @lastModTime"
                            + " where file_id = @id;";
                            cmd.Parameters.AddWithValue("@lastModTime", lastModTime.ToString(date_format));
                            cmd.Parameters.AddWithValue("@id", id);
                            cmd.ExecuteNonQuery();
                        }
                        tr.Commit();
                        return true;
                    }
                }
            }
            catch (SQLiteException) {return false; }
            
        }

        public static bool restoreFile(Int64 id, String path, String name, String user, String checksum, DateTime lastModTime)
        {
            String date = null;
            path += "\\";
            try
            {
                using (var con = new SQLiteConnection(DBmanager.connectionString))
                {
                    con.Open();
                    using (var tr = con.BeginTransaction())
                    {
                        using (var cmd = con.CreateCommand())
                        {
                            int len = path.Length;
                            cmd.CommandText = @"select max(creation_time) from snapshots where"
                                    + " user_id = @id and path = @dir and filename = @name;";
                            cmd.Parameters.AddWithValue("@id", user);
                            cmd.Parameters.AddWithValue("@dir", path);
                            cmd.Parameters.AddWithValue("@name", name);
                            object val = cmd.ExecuteScalar();
                            date = (val == System.DBNull.Value) ? null : (String)val;
                        }
                        if (date != null)
                        {
                            using (var cmd = con.CreateCommand())
                            {
                                cmd.CommandText = @"update snapshots set file_id = @id, checksum = @checksum,"
                                    + " last_mod_time = @lastModTime, deleted = @del where user_id = @user and"
                                    + " creation_time = @creationTime and path = @dir and filename = @filename;";
                                cmd.Parameters.AddWithValue("@id", id);
                                cmd.Parameters.AddWithValue("@checksum", checksum);
                                cmd.Parameters.AddWithValue("@lastModTime", lastModTime.ToString(date_format));
                                cmd.Parameters.AddWithValue("@del", false);
                                cmd.Parameters.AddWithValue("@user", user);
                                cmd.Parameters.AddWithValue("@creationTime", date);
                                cmd.Parameters.AddWithValue("@dir", path);
                                cmd.Parameters.AddWithValue("@filename", name);
                                cmd.ExecuteNonQuery();
                            }

                            using (var cmd = con.CreateCommand())
                            {
                                //update the last modification time in files 
                                cmd.CommandText = @"update files set last_mod_time = @lastModTime"
                                + " where file_id = @id;";
                                cmd.Parameters.AddWithValue("@lastModTime", lastModTime.ToString(date_format));
                                cmd.Parameters.AddWithValue("@id", id);
                                cmd.ExecuteNonQuery();
                            }
                            tr.Commit();
                            return true;
                        }
                    }
                }
                return false;
            }
            catch (SQLiteException) {  return false;}
           
        }


        public static DirectoryStatus  getAllSnapshots(String username) {
            DirectoryStatus ds = new DirectoryStatus();
            try
            {
                using (var con = new SQLiteConnection(DBmanager.connectionString))
                {
                    con.Open();
                    using (var cmd = con.CreateCommand())
                    {
                        cmd.CommandText = @"select distinct path,creation_time from snapshots where user_id=@id";
                        cmd.Parameters.AddWithValue("@id", username);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                DirectoryFile file = new DirectoryFile();
                                file.UserId = username;
                                file.Directory = true;
                                file.Path = (String)reader["path"];
                                file.Path = file.Path.Substring(0, file.Path.LastIndexOf('\\'));
                                DateTime prefix = (DateTime)reader["creation_time"];
                                file.Fullname = (String)prefix.ToString("dd/MM/yyyyTHH:ss") + "_" + (String)reader["path"];
                                file.LastModificationTime = (DateTime)reader["creation_time"];
                                ds.Files.Add(file.Fullname, file);
                            }
                        }
                    }
                }
                return ds;
            }
            catch (SQLiteException) { return null; }

           
        }

        public static DirectoryStatus getSnapshotFilesOfDirectory(String directory, String username, DateTime creationTime)
        {
            DirectoryStatus ds = new DirectoryStatus();
            ds.FolderPath = directory;
            ds.Username = username;
            try
            {
                using (var con = new SQLiteConnection(connectionString))
                {
                    con.Open();
                        using (var cmd = con.CreateCommand())
                        {
                            int len = directory.Length;
                            cmd.CommandText = @"select * from snapshots where user_id = @id"
                                    + " and path = @dir and creation_time = @date;";
                            cmd.Parameters.AddWithValue("@id", username);
                            cmd.Parameters.AddWithValue("@dir", directory + "\\");
                            cmd.Parameters.AddWithValue("@date", creationTime.ToString(date_format));
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    DirectoryFile file = new DirectoryFile();
                                    file.UserId = username;
                                    file.Directory = (Boolean)reader["directory"];
                                    file.Path = (String)reader["path"];
                                    file.Path = file.Path.Substring(0, file.Path.LastIndexOf('\\'));
                                    file.Filename = (String)reader["filename"];
                                    file.Deleted = (Boolean)reader["deleted"];
                                    file.LastModificationTime = (DateTime)reader["last_mod_time"];
                                    if (!file.Directory)
                                    {
                                        file.Id = (Int64)reader["file_id"];
                                        file.Checksum = (String)reader["checksum"];
                                    }
                                    file.Fullname = Path.Combine(file.Path, file.Filename);
                                    ds.Files.Add(file.Fullname, file);
                                }   
                        }
                    }
                }
                return ds;
            }
            catch (SQLiteException) { return ds; }
            
        }


    }     
}
