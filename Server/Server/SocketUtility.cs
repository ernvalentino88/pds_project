using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.IO;

namespace Server
{
    class SocketUtility
    {
        Socket s;
        
        private SocketUtility(Socket s1) {
            s = s1;
        }

        private void get_pub_key(byte[] modulus,byte[] exponent){
           

            byte[] buffer_modulus = new byte[256];
            int b = s.Receive(buffer_modulus);
            if (b != 256) { throw new Exception(""); }
            byte[] buffer_exponent = new byte[4];
        }

        private static byte[] my_recv(int size, Socket s) {
            int left = size;
            int b;
            byte[] received = new byte[size];
            byte[] buffer = new byte[256];
            MemoryStream ms = new MemoryStream(buffer);
            try
            {
                while (left > 0)
                {
                    b = s.Receive(buffer);
                    if (b <= 0) {
                        return null;
                    }
                    ms.Read(received, 0, b);
                    left -= b;
                }
            }
            catch (SocketException) {
                return null;
            }
            return received;
        }
    }
}
