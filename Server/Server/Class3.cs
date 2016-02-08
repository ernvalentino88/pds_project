using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;

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
    }
}
