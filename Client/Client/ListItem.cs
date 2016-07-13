using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utility;

namespace ClientApp
{
    abstract class ListItem
    {
        private ImageSource icon;
        private Int64 id;
        private String filename;
        private String path;
        private Boolean directory;
        private Boolean deleted;
        private String lastModTime;
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

        public String LastModificationTime
        {
            get
            {
                return lastModTime;
            }
            set
            {
                lastModTime = value;
            }
        }

        public ListItem()
        {
            this.deleted = false;
            this.directory = false;
        }

        public ListItem(DirectoryFile file)
        {
            directory = file.Directory;
            filename = file.Filename;
            path = file.Path;
            deleted = file.Deleted;
            id = file.Id;
            lastModTime = file.LastModificationTime.ToString("ddd, d MMM yyyy HH:mm:ss");
        }
    }
}
