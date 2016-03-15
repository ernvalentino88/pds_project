using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

//for watcher
using System.Security;
using System.Security.Permissions;
using System.IO;

using Utility;


namespace ClientApp
{
    class Watcher
    {
        public FileSystemWatcher watcher { get; set; }
        public String path { get; set; }
        public Client client { get; set; }

        public Watcher(String path, Client client)
        {
            this.client = client;
            this.path = path;
            this.Run();
        }
        
        public void Stop()
        {
            lock (this)
            {
                if (watcher != null)
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.Dispose();
                }
            }
        }

        [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
        private void Run()
        {
            // Create a new FileSystemWatcher and set its properties.
            watcher = new FileSystemWatcher();

            try
            { 
                watcher.Path = path;

                /* Watch for changes in LastAccess and LastWrite times, and
                   the renaming of files or directories. */
                watcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite
                   | NotifyFilters.FileName | NotifyFilters.DirectoryName;
                // Watch all files
                watcher.Filter = "*.*";

                // Add event handlers.
                watcher.Changed += new FileSystemEventHandler(OnChanged);
                watcher.Created += new FileSystemEventHandler(OnCreated);
                watcher.Deleted += new FileSystemEventHandler(OnDeleted);
                watcher.Renamed += new RenamedEventHandler(OnRenamed);
                //set to watch subfolders
                watcher.IncludeSubdirectories = true;
                // Begin watching.
                watcher.EnableRaisingEvents = true;

            }
            catch (SystemException) 
            { 
                //TODO:wrong folder
            }
        }
    
        // Define the event handlers.

        private void OnDeleted(object source, FileSystemEventArgs e) 
        {
            DirectoryFile file = new DirectoryFile();
            file.Fullname = e.FullPath;
            file.Filename = e.Name;
            file.Path = Path.GetPathRoot(e.FullPath);
            file.UserId = client.UserId;

           
            if (!Path.HasExtension(e.FullPath))
            {
                file.Directory = true;
                //directory: a file with no extension is treated like a directory
                Console.WriteLine("Directory: " + e.FullPath + " Deleted");
            }
            else
            {
                //file
                file.Directory = false;
                if ( !Path.GetFileName(e.FullPath)[0].Equals('~') && !Path.GetFileName(e.FullPath)[0].Equals('$') &&
                        !Path.GetFileName(e.FullPath)[0].Equals('~') && !Path.GetExtension(e.FullPath).Equals(".tmp") &&
                        !Path.GetExtension(e.FullPath).Equals(".TMP") )
                {   
                    Console.WriteLine("File: " + e.FullPath + " Deleted");
                }
            }
        }

        private void OnCreated(object source, FileSystemEventArgs e)
        {
            try
            {
                if (Directory.Exists(e.FullPath))
                {
                    //directory
                    //DirectoryInfo di = new DirectoryInfo(e.FullPath);
                    Console.WriteLine("Directory: " + e.FullPath + " Created");
                    lock (this)
                    {
                        //TODO
                    }
                }
                if (File.Exists(e.FullPath))
                {
                    //exclude files started from '~','$' and '.'(hidden files) and without extensions
                    FileInfo fi = new FileInfo(e.FullPath);
                    if (!fi.Name[0].Equals('~') && !fi.Name[0].Equals('$') && !fi.Name[0].Equals('.') &&
                            !fi.Extension.Equals(".tmp") && !fi.Extension.Equals(".TMP") && !fi.Extension.Equals(""))
                    {
                        //file
                        Console.WriteLine("File: " + e.FullPath + " Created");
                        lock (this)
                        {
                            //TODO
                        }
                    }
                }
            }
            catch (Exception) { }
        }

        private void OnChanged(object source, FileSystemEventArgs e)
        {
            try
            {
                //to avoid double changes
                lock (this)
                {
                    watcher.EnableRaisingEvents = false;
                }
                if (File.Exists(e.FullPath))
                {
                    //exclude files started from '~','$' and '.'(hidden files) and without extensions
                    FileInfo fi = new FileInfo(e.FullPath);
                    if (!fi.Name[0].Equals('~') && !fi.Name[0].Equals('$') && !fi.Name[0].Equals('.') &&
                            !fi.Extension.Equals(".tmp") && !fi.Extension.Equals(".TMP") && !fi.Extension.Equals(""))
                    {
                        //file
                        Console.WriteLine("File: " + e.FullPath + " Changed");
                    } 
                }
            }
            catch (Exception) { }
            finally
            {
                watcher.EnableRaisingEvents = true;
            }
        }

        private void OnRenamed(object source, RenamedEventArgs e)
        {
            try
            {
                //if from .doc -> ~ ignore
                //if from ~->.doc changed
                if (Directory.Exists(e.FullPath))
                {
                    // a directory is renamed
                    Console.WriteLine("Directory: {0} renamed to {1}", e.OldFullPath, e.FullPath);
                    lock (this)
                    {
                        //TODO
                    }
                }
                if (File.Exists(e.FullPath))
                {
                    // a file is renamed
                    FileInfo fi_old = new FileInfo(e.OldFullPath);
                    FileInfo fi_new = new FileInfo(e.FullPath);

                    if ((fi_old.Name[0].Equals('~') && !fi_new.Name[0].Equals('~')) ||
                          (fi_old.Extension.Equals(".tmp") && !fi_new.Name[0].Equals('~')) ||
                          (fi_old.Extension.Equals("") && !fi_new.Name[0].Equals('~')) ||
                          (fi_old.Name[0].Equals('~') && !fi_new.Extension.Equals("tmp")) ||
                          (fi_old.Extension.Equals(".tmp") && !fi_new.Extension.Equals("tmp")) ||
                          (fi_old.Extension.Equals("") && !fi_new.Extension.Equals("tmp")) ||
                          (fi_old.Name[0].Equals('~') && !fi_new.Extension.Equals("")) ||
                          (fi_old.Extension.Equals(".tmp") && !fi_new.Extension.Equals("")) ||
                          (fi_old.Extension.Equals("") && !fi_new.Extension.Equals("")))
                    {
                        //file Microsoft changed
                        Console.WriteLine("File MICROSOFT: " + e.FullPath + " Changed");
                        lock (this)
                        {
                            //TODO
                        }
                    }
                    else
                    {
                        if (!fi_new.Name[0].Equals('~') && !fi_new.Name[0].Equals('$') && !fi_new.Name[0].Equals('.') &&
                                    !fi_new.Extension.Equals(".tmp") && !fi_new.Extension.Equals(".TMP") && !fi_new.Extension.Equals(""))
                        {
                            Console.WriteLine("File: {0} renamed to {1}", e.OldFullPath, e.FullPath);
                            lock (this)
                            {
                                //TODO
                            }
                        }
                    }
                }
            }
            catch (Exception) { }
        }
    }
}
