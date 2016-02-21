using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using Utility;

namespace ServerApp
{
    class ClientSession
    {
        private Int64 sessionId;
        private Socket socket;
        private User user;
        private AesCryptoServiceProvider aesKey;
        private DateTime lastActivationTime;
        private DirectoryStatus currentStatus;

        public Int64 SessionId
        {
            get
            {
                return sessionId;
            }
            set
            {
                sessionId = value;
            }
        }

        public Socket Socket
        {
            get
            {
                return socket;
            }
            set
            {
                socket = value;
            }
        }

        public User User
        {
            get
            {
                return user;
            }
            set
            {
                user = value;
            }
        }

        public AesCryptoServiceProvider AESKey
        {
            get
            {
                return aesKey;
            }
            set
            {
                aesKey = value;
            }
        }

        public DateTime LastActivationTime
        {
            get
            {
                return lastActivationTime;
            }
            set
            {
                lastActivationTime = value;
            }
        }

        public DirectoryStatus CurrentStatus
        {
            get
            {
                return currentStatus;
            }
            set
            {
                currentStatus = value;
            }
        }
        

    }
}
