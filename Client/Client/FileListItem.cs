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
    class FileListItem
    {
        private ImageSource icon;
        private Int64 id;
        private String filename;
        private String path;
        private String checksum;
        private Boolean directory;
        private Boolean deleted;
        private String btnContent;
        private Boolean enabled;
        private static ImageSource dir_img = new BitmapImage(new Uri("dir.ico", UriKind.Relative));
        private static ImageSource file_img = new BitmapImage(new Uri("file.ico", UriKind.Relative));

        public ImageSource Icon
        {
            get
            {
                icon = (directory == true) ? dir_img : file_img;
                return icon;
            }
            private set
            {
                icon = value;
            }
        }

        public Int64 Id
        {
            get
            {
                return id;
            }
            set
            {
                id = value;
            }
        }

        public String Filename
        {
            get
            {
                return filename;
            }
            set
            {
                filename = value;
            }
        }

        public String Path
        {
            get
            {
                return path;
            }
            set
            {
                path = value;
            }
        }

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

        public Boolean Directory
        {
            get
            {
                return directory;
            }
            set
            {
                directory = value;
            }
        }

        public Boolean Deleted
        {
            get
            {
                return deleted;
            }
            set
            {
                deleted = value;
            }
        }

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
                if (directory && !deleted)
                    return false;
                return true;
            }
            set
            {
                deleted = value;
            }
        }

        public FileListItem()
        {
            this.deleted = false;
            this.directory = false;
        }

        public FileListItem(DirectoryFile file)
        {
            directory = file.Directory;
            filename = file.Filename;
            path = file.Path;
            checksum = file.Checksum;
            deleted = file.Deleted;
            id = file.Id;
            btnContent = (directory || deleted) ? "Restore" : "See older versions";
        }
    }

}
