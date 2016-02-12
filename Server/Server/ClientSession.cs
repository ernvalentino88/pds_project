using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using Utility;

namespace Server
{
    class ClientSession
    {
        private Int64 sessionId
        {
            get;
            set;
        }
        private Socket socket
        {
            get;
            set;
        }
        private User user
        {
            get;
            set;
        }
        private AesCryptoServiceProvider aesKey
        {
            get;
            set;
        }

        

    }
}
