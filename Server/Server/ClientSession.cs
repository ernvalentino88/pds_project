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
        public Int64 SessionId
        {
            get;
            set;
        }
        public Socket Socket
        {
            get;
            set;
        }
        public User User
        {
            get;
            set;
        }
        public AesCryptoServiceProvider AESKey
        {
            get;
            set;
        }

        

    }
}
