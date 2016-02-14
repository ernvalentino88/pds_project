using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerApp
{
    class ServerInstance
    {
        private ClientSession clientSession;

        public ClientSession ClientSession
        {
            get;
            set;
        }

        public ServerInstance(ClientSession cs)
        {
            this.clientSession = cs;
        }
    }
}
