using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utility
{
    public class DirectoryFile
    {
        private Int64 id;
        private String filename;
        private String path;
        private String userId;
        private String checksum;
        private DateTime lastModificationTime;
        private Boolean deleted;
        private Boolean directory;
        private Int64 length;
        private String fullname;
        private DateTime lastModTime;

        public DirectoryFile() 
        {
            this.deleted = false;
            this.directory = false;
        }

        public DirectoryFile(Int64 id, String path, String fname, String userId, String checksum, DateTime lasModtime)
        {
            this.id = id;
            this.path = path;
            this.filename = fname;
            this.fullname = System.IO.Path.Combine(path, fname);
            this.userId = userId;
            this.checksum = checksum;
            this.lastModificationTime = lasModtime;
            this.deleted = false;
            this.directory = false;
        }

        public DirectoryFile(String path, String filename, String userId, Boolean isDirectory)
        {
            this.path = path;
            this.filename = filename;
            this.fullname = path;
            this.userId = userId;
            this.Directory = isDirectory;
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

        public DateTime LastModificationTime
        {
            get
            {
                return lastModificationTime;
            }
            set
            {
                lastModificationTime = value;
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

        public Int64 Length
        {
            get
            {
                return length;
            }
            set
            {
                length = value;
            }
        }
        public string Fullname
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

        public override int GetHashCode()
        {
            return ( filename.GetHashCode() +
                userId.GetHashCode() + checksum.GetHashCode() );
        }

        public override bool Equals(object obj)
        {
            DirectoryFile other = (DirectoryFile)obj;
            return ( userId.Equals(other.userId) && 
                filename.Equals(other.filename) && checksum.Equals(other.checksum) );
        }
    }
}

