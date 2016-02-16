using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerApp
{
    class CurrentStatus
    {
        private String username;
        private String folderPath;
        private Dictionary<String, String> file2checksum;
        private DateTime creationDate;

        public String Username
        {
            get
            {
                return username;
            }
            set
            {
                username = value;
            }
        }

        public String FolderPath
        {
            get
            {
                return folderPath;
            }
            set
            {
                folderPath = value;
            }
        }

        public Dictionary<String, String> File2Checksum
        {
            get
            {
                return file2checksum;
            }
            set
            {
                file2checksum = value;
            }
        }

        public DateTime CreationDate
        {
            get
            {
                return creationDate;
            }
            set
            {
                creationDate = value;
            }
        }

    }
}
