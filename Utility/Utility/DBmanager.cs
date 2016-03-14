using System;
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
        public static String connectionString = @"Data Source=C:\Users\Ernesto\Documents\SQLiteStudio\pds.db;Version=3;";
        public static String date_format = "dd/MM/yyyy-HH:mm:ss";

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
                                    + " and substr(path,1," + len + ") = @dir and creation_time ="
                                    + " @date;";
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
                                    file.Filename = (String)reader["filename"];
                                    file.Deleted = (Boolean)reader["deleted"];
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
            }
            catch (SQLiteException) { }
            return ds;
        }

        public static bool insertDirectory(SQLiteConnection conn, DirectoryStatus newStatus, String filename, String path)
        {
            try
            {
                using (SQLiteCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"insert into snapshots (user_id,creation_time,path,filename,directory) values"
                        + " (@user, @creationTime, @path, @filename, @directory);";
                    cmd.Parameters.AddWithValue("@user", newStatus.Username);
                    cmd.Parameters.AddWithValue("@creationTime", newStatus.CreationTime.ToString(date_format));
                    cmd.Parameters.AddWithValue("@path", path);
                    cmd.Parameters.AddWithValue("@filename", filename);
                    cmd.Parameters.AddWithValue("@directory", true);
                    cmd.ExecuteNonQuery();
                }
                //add directory
                DirectoryFile dirFile = new DirectoryFile(path, filename, newStatus.Username, true);
                dirFile.Directory = true;
                newStatus.Files.Add(dirFile.Fullname, dirFile);
                return true;
            }
            catch (SQLiteException) { }
            return false;
        }

        public static bool insertFile(SQLiteConnection conn, DirectoryStatus newStatus, String filename, String path, byte[] file, DateTime lastModTime)
        {
            try
            {
                using (SQLiteCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"insert into files (user_id,path,filename,content,last_mod_time) values"
                            + " (@user, @path, @filename, @content, @lastModTime);";
                    cmd.Parameters.AddWithValue("@user", newStatus.Username);
                    cmd.Parameters.AddWithValue("@path", path);
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
                    cmd.Parameters.AddWithValue("@path", path);
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
                    cmd.CommandText = @"insert into snapshots (user_id,file_id,creation_time,checksum,path,filename) values"
                        + " (@user, @fileId, @creationTime, @checksum, @path, @filename);";
                    cmd.Parameters.AddWithValue("@user", newStatus.Username);
                    cmd.Parameters.AddWithValue("@fileId", fileId);
                    cmd.Parameters.AddWithValue("@creationTime", newStatus.CreationTime.ToString(date_format));
                    cmd.Parameters.AddWithValue("@checksum", checksum);
                    cmd.Parameters.AddWithValue("@path", path);
                    cmd.Parameters.AddWithValue("@filename", filename);
                    cmd.ExecuteNonQuery();
                }
                //add file
                DirectoryFile dirFile = new DirectoryFile((Int64)fileId, path, filename, newStatus.Username, checksum, lastModTime);
                newStatus.Files.Add(dirFile.Fullname, dirFile);
                return true;
            }
            catch (SQLiteException) { }
            return false;
        }

        public static int getMaxFileId(SQLiteConnection connection) {
            int maxId=-1;
            try
            {
                string selectMaxId = "select max(fileId) from files;";
                SQLiteCommand selectMaxCmd = new SQLiteCommand(selectMaxId, connection);
                object val = selectMaxCmd.ExecuteScalar();
                maxId = int.Parse(val.ToString());
            } catch(SQLiteException)
            {
                return -1;
            }
            return maxId;
        }


        public static bool deleteDirectory(SQLiteConnection conn, DirectoryStatus newStatus, string filename, string path)
        {
            try
            {
                using (SQLiteCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"insert into snapshots (user_id,creation_time,path,filename,directory,deleted) values"
                        + " (@user, @creationTime, @path, @filename, @directory, @deleted);";
                    cmd.Parameters.AddWithValue("@user", newStatus.Username);
                    cmd.Parameters.AddWithValue("@creationTime", newStatus.CreationTime.ToString(date_format));
                    cmd.Parameters.AddWithValue("@path", path);
                    cmd.Parameters.AddWithValue("@filename", filename);
                    cmd.Parameters.AddWithValue("@directory", true);
                    cmd.Parameters.AddWithValue("@deleted", true);
                    cmd.ExecuteNonQuery();
                }
                //add directory
                DirectoryFile dirFile = new DirectoryFile(path, filename, newStatus.Username, true);
                dirFile.Deleted = true;
                newStatus.Files.Add(dirFile.Fullname, dirFile);
                return true;
            }
            catch (SQLiteException) { }
            return false;
        }

        public static bool deleteFile(SQLiteConnection conn, DirectoryFile file, DirectoryStatus newStatus)
        {
            try
            {
                using (SQLiteCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"insert into snapshots (user_id,file_id,creation_time,checksum,path,filename,deleted) values"
                        + " (@user, @fileId, @creationTime, @checksum, @path, @filename, @deleted);";
                    cmd.Parameters.AddWithValue("@user", file.UserId);
                    cmd.Parameters.AddWithValue("@fileId", file.Id);
                    cmd.Parameters.AddWithValue("@creationTime", newStatus.CreationTime.ToString(date_format));
                    cmd.Parameters.AddWithValue("@checksum", file.Checksum);
                    cmd.Parameters.AddWithValue("@path", file.Path);
                    cmd.Parameters.AddWithValue("@filename", file.Filename);
                    cmd.Parameters.AddWithValue("@deleted", true);
                    cmd.ExecuteNonQuery();
                }
                DirectoryFile dirFile = new DirectoryFile(file.Id, file.Path, file.Filename, file.UserId, file.Checksum, file.LastModificationTime);
                file.Deleted = true;
                newStatus.Files.Add(dirFile.Fullname, dirFile);
                return true;
            }
            catch (SQLiteException) { }
            return false;
        }

        public static bool insertUnchanged(SQLiteConnection conn, DirectoryStatus oldStatus, DirectoryStatus newStatus)
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
                        if (file.Directory)
                        {
                            cmd.CommandText = @"insert into snapshots (user_id,creation_time,path,filename,directory,deleted) values"
                                + " (@user, @creationTime, @path, @filename, @directory, @deleted);";
                            cmd.Parameters.AddWithValue("@user", newStatus.Username);
                            cmd.Parameters.AddWithValue("@creationTime", newStatus.CreationTime.ToString(date_format));
                            cmd.Parameters.AddWithValue("@path", file.Path);
                            cmd.Parameters.AddWithValue("@filename", file.Filename);
                            cmd.Parameters.AddWithValue("@directory", file.Directory);
                            cmd.Parameters.AddWithValue("@deleted", file.Deleted);
                            cmd.ExecuteNonQuery();
                            newStatus.Files.Add(file.Fullname, file);
                        }
                        else
                        {
                            cmd.CommandText = @"insert into snapshots (user_id,file_id,creation_time,checksum,path,filename,deleted) values"
                                + " (@user, @fileId, @creationTime, @checksum, @path, @filename, @deleted);";
                            cmd.Parameters.AddWithValue("@user", file.UserId);
                            cmd.Parameters.AddWithValue("@fileId", file.Id);
                            cmd.Parameters.AddWithValue("@creationTime", newStatus.CreationTime.ToString(date_format));
                            cmd.Parameters.AddWithValue("@checksum", file.Checksum);
                            cmd.Parameters.AddWithValue("@path", file.Path);
                            cmd.Parameters.AddWithValue("@filename", file.Filename);
                            cmd.Parameters.AddWithValue("@deleted", file.Deleted);
                            cmd.ExecuteNonQuery();
                            newStatus.Files.Add(file.Fullname, file);
                        }
                    }
                }
                return true;
            }
            catch (SQLiteException) { }
            return false;
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
                        int len = directory.Length;
                        cmd.CommandText = @"select max(creation_time) from snapshots where"
                                + " user_id = @id and path = @dir;";
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
                                    + " and path = @dir and creation_time = @date;";
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
                                    file.Filename = (String)reader["filename"];
                                    file.Deleted = (Boolean)reader["deleted"];
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
            }
            catch (SQLiteException) { }
            return ds;
        }
    }
}
