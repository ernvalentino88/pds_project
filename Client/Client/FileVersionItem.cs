using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utility;

namespace ClientApp
{
    class FileVersionItem : ListItem
    {
        private String checksum;

        public String Checksum
        {
            get
            {
                return checksum;
            }
            set
            {
                checksum = value;
            }
        }

        public FileVersionItem() : base() { }

        public FileVersionItem(DirectoryFile file) : base(file)
        {
            checksum = file.Checksum;
        }
    }
}
