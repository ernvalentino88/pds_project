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
            START_SYNCH = 11,
            END_SYNCH = 12,
            ADD = 13,
            UPD = 14,
            DEL = 15,
            INIT_SYNCH = 16,
            DIR = 17,
            FILE = 18
        };

        public static byte[] my_recv(int size, Socket s)
        {
            int left = size;
            int b;
            byte[] received = new byte[size];
            try
            {
                using (MemoryStream ms = new MemoryStream(received))
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
            }
            catch (SocketException)
            {
                return null;
            }
            return received;
        }

        public static byte[] my_recv(Int64 size, Socket s)
        {
            Int64 left = size;
            int b;
            byte[] received = new byte[size];
            try
            {
                using (MemoryStream ms = new MemoryStream(received))
                {
                    while (left > 0)
                    {
                        int dim = (left > 4096) ? 4096 : (int)left;
                        byte[] buffer = new byte[dim];
                        b = s.Receive(buffer);
                        if (b <= 0)
                        {
                            return null;
                        }
                        ms.Write(buffer, 0, b);
                        left -= b;
                    }
                }
            }
            catch (SocketException)
            {
                return null;
            }
            return received;
        }

        public static byte[] recvEncryptedFile(Int64 size, Socket s,AesCryptoServiceProvider key)
        {
            Int64 left = size;
            byte[] received = new byte[size];
            //adjust size for receive correct number of bytes after encrypt
            //left = (left % 16 == 0) ? left + 16 : left + (16 - (left % 16));
            
            try
            {
                using (MemoryStream ms = new MemoryStream(received))
                {
                    while (left > 0)
                    {
                        int dim = (left > 4096) ? 4096 : (int)left;
                        dim = (dim % 16 == 0) ? dim + 16 : dim + (16 - (dim % 16));
                        byte[] buffer = my_recv(dim, s);
                        if (buffer == null)
                            return null;
                        byte[] decryptedData = Security.AESDecrypt(key, buffer);
                        if (decryptedData == null)
                            return null;
                        ms.Write(decryptedData, 0, decryptedData.Length);
                        //ms.Write(buffer, 0, buffer.Length);
                        left -= decryptedData.Length;
                    }
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
