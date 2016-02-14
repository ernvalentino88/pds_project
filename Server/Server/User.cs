using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerApp
{
    class User
    {
        private String userId;
        private String hashedPassword;

        public String UserId
        {
            get
            {
                return userId;
            }
            set
            {
                userId = value;
            }
        }

        public String HashedPassword
        {
            get
            {
                return hashedPassword;
            }
            set
            {
                hashedPassword = value;
            }
        }

        public User(String userId, String pwdHash)
        {
            this.userId = userId;
            this.hashedPassword = pwdHash;
        }
    }
}
