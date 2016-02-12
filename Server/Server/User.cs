using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    class User
    {
        private String userId;
        private String hashedPassword;

        public String UserId
        {
            get;
            set;
        }

        public String HashedPassword
        {
            get;
            set;
        }

        public User(String userId, String pwdHash)
        {
            this.userId = userId;
            this.hashedPassword = pwdHash;
        }
    }
}
