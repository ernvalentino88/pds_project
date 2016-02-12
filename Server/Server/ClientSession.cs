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
        private Int64 sessionId;
        private Socket client;
        private User user;
        private AesCryptoServiceProvider aesKey;

        public Int64 SessionId
        {
            get;
            set;
        }

        public Socket Client
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
