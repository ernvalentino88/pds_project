﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SQLite;
using System.Security.Cryptography;

namespace Server
{
    class DBmanager
    {

        private SQLiteConnection connect_db()
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

        private String find_user(SQLiteConnection db_con, String id)
        {
            string sql = "select user_id from dbo.users where user_id=" + id;
            SQLiteCommand command = new SQLiteCommand(sql, db_con);
            SQLiteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (reader["user_id"].Equals(id))
                {
                    string pwd_hash = reader.GetString(1);
                    // String pwd_hash = new string(reader["pwd"].ToString);
                    return pwd_hash;
                }
            }

            return null;
        }

        public string CalculateMD5Hash(string input) {
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
