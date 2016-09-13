using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
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
        private DateTime lastRead;

        public Watcher(String path, Client client)
        {
            this.client = client;
            this.path = path;
            lastRead = DateTime.MinValue;
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
              //wrong folder
                return;
            }
        }

        public void Pause()
        {
            watcher.EnableRaisingEvents = false;
        }

        public void Resume()
        {
            watcher.EnableRaisingEvents = true;
        }
    
        // Define the event handlers.

        private void OnDeleted(object source, FileSystemEventArgs e) 
        {
            DirectoryFile file = new DirectoryFile();
            file.Fullname = e.FullPath;
            file.Filename = Path.GetFileName(e.FullPath);
            file.Path = Path.GetDirectoryName(e.FullPath);
            file.UserId = client.UserId;
       
            if (!Path.HasExtension(e.FullPath))
            {
                file.Directory = true;
                //directory: a file with no extension is treated like a directory
                client.deleteFile(file, true);
            }
            else
            {
                //file
                file.Directory = false;
                if ( !Path.GetFileName(e.FullPath)[0].Equals('~') && !Path.GetFileName(e.FullPath)[0].Equals('$') &&
                        !Path.GetFileName(e.FullPath)[0].Equals('.') && !Path.GetExtension(e.FullPath).Equals(".tmp") &&
                        !Path.GetExtension(e.FullPath).Equals(".TMP") )
                {
                    client.deleteFile(file, true);
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
                    lock (this)
                    {
                        DirectoryFile file = new DirectoryFile();
                        file.Directory = true;
                        file.Filename = Path.GetFileName(e.FullPath);
                        file.Fullname = e.FullPath;
                        file.LastModificationTime = Directory.GetCreationTime(e.FullPath);
                        file.Path = Path.GetDirectoryName(e.FullPath);
                        file.UserId = client.UserId;
                        client.addFile(file, true);
                    }
                }
                else
                {
                    //file
                    lock (this)
                    {
                        FileInfo info = new FileInfo(e.FullPath);
                        if (!info.Name[0].Equals('~') && !info.Name[0].Equals('$') &&
                                 !info.Name[0].Equals('.') && !info.Extension.Equals(".tmp") &&
                                 !info.Extension.Equals(".TMP") && !info.Extension.Equals(""))
                        {
                            DirectoryFile file = new DirectoryFile();
                            file.Directory = false;
                            file.Filename = info.Name;
                            file.Fullname = info.FullName;
                            file.LastModificationTime = info.LastWriteTime;
                            file.Path = info.DirectoryName;
                            file.UserId = client.UserId;
                            client.addFile(file, true);
                        }
                    }
                }
            }
            catch (Exception) { return; }
        }

        private void OnChanged(object source, FileSystemEventArgs e)
        {
            try
            {
                lock (this)
                {
                    if (File.Exists(e.FullPath))
                    {
                        DateTime lastWriteTime = File.GetLastWriteTime(e.FullPath);
                        if (lastWriteTime != lastRead)
                        {
                            //exclude files started from '~','$' and '.'(hidden files) and without extensions
                            FileInfo info = new FileInfo(e.FullPath);
                            if (!info.Name[0].Equals('~') && !info.Name[0].Equals('$') &&
                                 !info.Name[0].Equals('.') && !info.Extension.Equals(".tmp") &&
                                 !info.Extension.Equals(".TMP") && !info.Extension.Equals(""))
                            {
                                //file
                                DirectoryFile file = new DirectoryFile();
                                file.Filename = info.Name;
                                file.Fullname = info.FullName;
                                file.Path = info.DirectoryName;
                                file.UserId = client.UserId;
                                file.LastModificationTime = info.LastWriteTime;
                                file.Length = info.Length;
                                client.updateFile(file, true);
                            }
                            lastRead = lastWriteTime;
                        }
                    }
                }
            }
            catch (Exception) { return; }
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
                    lock (this)
                    {
                        client.renameDirectory(e.OldFullPath, e.FullPath);
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
                        lock (this)
                        {
                            DirectoryFile file = new DirectoryFile();
                            file.Filename = fi_new.Name;
                            file.Fullname = fi_new.FullName;
                            file.Path = fi_new.DirectoryName;
                            file.UserId = client.UserId;
                            file.LastModificationTime = fi_new.LastWriteTime;
                            file.Length = fi_new.Length;
                            client.updateFile(file, true);
                        }
                    }
                    else
                    {
                        if (!fi_new.Name[0].Equals('~') && !fi_new.Name[0].Equals('$') && !fi_new.Name[0].Equals('.') &&
                                    !fi_new.Extension.Equals(".tmp") && !fi_new.Extension.Equals(".TMP") && !fi_new.Extension.Equals(""))
                        {
                            lock (this)
                            {
                                client.renameFile(fi_old.Name, fi_new.Name, fi_old.DirectoryName);
                            }
                        }
                    }
                }
            }
            catch (Exception) { return; }
        }
    }
}
