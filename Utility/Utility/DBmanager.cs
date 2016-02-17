using System;
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
        private static String con_string = @"Data Source=C:\Users\John\Desktop\SQLiteStudio\PDS.db;Version=3;";
        //pc ernesto
        //private static String con_string = @"Data Source=C:\Users\Ernesto\Documents\SQLiteStudio\pds.db;Version=3;";

        public static String find_user(String id)
        {
            try
            {
                using ( var con = new SQLiteConnection(con_string) )
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
                using (var con = new SQLiteConnection(con_string))
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

        public static int getMaxFileId(SQLiteConnection connection) {
            int maxId=-1;
            try{
                string selectMaxId = "select max(fileId) from files";
                SQLiteCommand selectMaxCmd = new SQLiteCommand(selectMaxId, connection);
                object val = selectMaxCmd.ExecuteScalar();
                maxId = int.Parse(val.ToString());
            }catch(SQLiteException ){
                return -1;
            }
            return maxId;
            }

    }
}
