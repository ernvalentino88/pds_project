using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;


namespace Utility
{
    public class Networking
    {
        public static int TIME_OUT_SHORT = 30 * 1000;
        public static int TIME_OUT_LONG = 5 * 60 * 1000;
        public static String date_format = "dd/MM/yyyy-HH:mm:ss";
     

        public enum CONNECTION_CODES
        {
            ERR = 0,
            OK = 1,
            HELLO = 3,
            AUTH_FAILURE = 4,
            REG_FAILURE = 5,
            NEW_REG = 6,
            KEY_EXC = 7,
            EXIT = 8,
            AUTH = 9,
            SESSION = 10,
            TRANS=11

        };

        public static byte[] my_recv(int size, Socket s)
        {
            int left = size;
            int b;
            byte[] received = new byte[size];
            
            MemoryStream ms = new MemoryStream(received);
            try
            {
                while (left > 0)
                {
                    byte[] buffer = new byte[left];
                    b = s.Receive(buffer);
                    if (b <= 0)
                    {
                        return null;
                    }
                    ms.Write(buffer, 0, b);
                    left -= b;
                }
            }
            catch (SocketException)
            {
                return null;
            }
            return received;
        }

    }
}
