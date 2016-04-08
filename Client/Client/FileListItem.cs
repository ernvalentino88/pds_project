using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Drawing;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utility;

namespace ClientApp
{
    class FileListItem : ListItem
    {
        private String btnContent;

        public String ButtonContent
        {
            get
            {
                return btnContent;
            }
            set
            {
                btnContent = value;
            }
        }

        public Boolean EnableButton
        {
            get
            {
                if (base.Directory && !base.Deleted)
                    return false;
                return true;
            }
        }

        public FileListItem() : base() { }

        public FileListItem(DirectoryFile file) : base(file)
        {
            btnContent = (base.Directory || base.Deleted) ? "Restore" : "See older versions";
        }
    }

}
