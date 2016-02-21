using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utility
{
    public class DirectoryStatus
    {
        private String username;
        private String folderPath;
        private Dictionary<String, DirectoryFile> files;
        private DateTime creationTime;

        public DirectoryStatus()
        {
            this.files = new Dictionary<String, DirectoryFile>();
            this.creationTime = DateTime.Now;
        }

        public DirectoryStatus(DirectoryStatus other)
        {
            this.files = new Dictionary<String, DirectoryFile>(other.files);
            this.folderPath = String.Copy(other.folderPath);
            this.username = String.Copy(other.username);
            this.creationTime = DateTime.Now;
        }

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

        public Dictionary<String, DirectoryFile> Files
        {
            get
            {
                return files;
            }
            set
            {
                files = value;
            }
        }

        public DateTime CreationTime
        {
            get
            {
                return creationTime;
            }
            set
            {
                creationTime = value;
            }
        }

        /// <summary>
        /// Produce the set difference of this and src. Produce this - src
        /// </summary>
        /// <param name="src">the set from which extract items</param>
        /// <returns>the difference set</returns>
        public Dictionary<String, DirectoryFile> getDifference(DirectoryStatus src)
        {
            Dictionary<String, DirectoryFile> d1 = this.files;
            Dictionary<String, DirectoryFile> d2 = src.files;
            Dictionary<String, DirectoryFile> d3;
            //d1 - d2
            d3 = d1.Where(x => !d2.ContainsKey(x.Key)).ToDictionary(x => x.Key, x => x.Value);
            return d3;
        }

        /// <summary>
        /// Produce the intersection set of this and src 
        /// </summary>
        /// <param name="src">the set with which to perform the intersection </param>
        /// <returns>the intersect set</returns>
        public Dictionary<String, DirectoryFile> getIntersect(DirectoryStatus src)
        {
            Dictionary<String, DirectoryFile> d1 = this.files;
            Dictionary<String, DirectoryFile> d2 = src.files;
            Dictionary<String, DirectoryFile> d3;
            d3 = d1.Where(x => d2.ContainsKey(x.Key)).ToDictionary(x => x.Key, x => x.Value);
            return d3;
        }

        public override int GetHashCode()
        {
            int hash = username.GetHashCode();
            hash += folderPath.GetHashCode();
            foreach (var item in files)
            {
                DirectoryFile file = item.Value;
                hash += file.GetHashCode();
            }
            return hash;
        }

        public override bool Equals(object obj)
        {
            DirectoryStatus other = (DirectoryStatus)obj;
            if (!this.username.Equals(other.username))
                return false;
            if (!this.folderPath.Equals(other.folderPath))
                return false;
            foreach (var item in files)
            {
                DirectoryFile file = item.Value;
                if (!other.files.ContainsKey(file.Filename))
                    return false;
                DirectoryFile otherFile = other.files[file.Filename];
                if (!file.Equals(otherFile))
                    return false;
            }
            return true;
        }

    }
}

