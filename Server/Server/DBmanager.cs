using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SQLite;
using System.Security.Cryptography;

namespace Utility
{
    class DBmanager
    {

        private static SQLiteConnection connect_db()
        {
            SQLiteConnection dbCon;
            String con_str = @"Data Source=C:\Users\John\Desktop\SQLiteStudio\PDS.db;Version=3;";
            dbCon = new SQLiteConnection(con_str, true);
            try
            {
                dbCon.Open();
            }
            catch (System.Data.SQLite.SQLiteException ex)
            {
                throw ex;
            }
            return dbCon;
        }

        public static String find_user(String id)
        {
            SQLiteConnection db_con = connect_db();
            string sql = "select user_id from dbo.users where user_id=" + id;
            SQLiteCommand command = new SQLiteCommand(sql, db_con);
            SQLiteDataReader reader = command.ExecuteReader();
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

        public static bool register(String id, String pass)
        {
            try
            {
                SQLiteConnection con = connect_db();
                String pwd_md5 = CalculateMD5Hash(pass);
                string sql = "insert into users (user_id, pwd) values ('" + id + "', " + pwd_md5 + ")";
                SQLiteCommand command = new SQLiteCommand(sql, con);
                command.ExecuteNonQuery();
            }
            catch (System.Data.SQLite.SQLiteException ex)
            {
                return false;
            }
            return true;
        }

        public static string CalculateMD5Hash(string input)
        {
            // step 1, calculate MD5 hash from input
            MD5 md5 = System.Security.Cryptography.MD5.Create();
            byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
            byte[] hash = md5.ComputeHash(inputBytes);
            // step 2, convert byte array to hex string
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < hash.Length; i++)
            {
                sb.Append(hash[i].ToString("X2"));
            }
            return sb.ToString();
        }


    }
}
