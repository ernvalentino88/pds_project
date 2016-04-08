using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utility;

namespace ClientApp
{
    class DirectoryItem : ListItem
    {
        private String fullname;
        private Boolean isOnDisk;

        public String Fullname
        {
            get
            {
                return fullname;
            }
            set
            {
                fullname = value;
            }
        }

        public Boolean IsOnDisk
        {
            get
            {
                return isOnDisk;
            }
            set
            {
                isOnDisk = value;
            }
        }

        public DirectoryItem() : base() { }

        public DirectoryItem(DirectoryFile file) : base(file)
        {
            fullname = System.IO.Path.Combine(base.Path, base.Filename);
            isOnDisk = (base.Deleted) ? false : true;
        }
    }
}
