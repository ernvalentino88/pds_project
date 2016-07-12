using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utility;


namespace ClientApp
{
    class SnapshotItem : ListItem
    {
        public SnapshotItem(DirectoryFile file) : base(file)
        {
        }
    }
}
