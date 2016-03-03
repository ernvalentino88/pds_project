using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

//for watcher
using System.Security;
using System.Security.Permissions;
using System.IO;

public class WatcherTestFinal
{
    static FileSystemWatcher watcher;

    public static void Main()
    {
        Run();
    }
    //TODO: somwhere turn off watcher on CLIENT
    [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
    public static void Run()
    {
        // Create a new FileSystemWatcher and set its properties.
        watcher = new FileSystemWatcher();

        try
        {
            Console.WriteLine("Directory: ");
            String name;
            name = Console.ReadLine();
            //String name = @"C:\Users\John\Documents\TEST";
            watcher.Path = name;

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

            // Wait for the user to quit the program.
            Console.WriteLine("Press \'q\' to quit the sample.");
            while (Console.Read() != 'q') ;
        }
        catch (SystemException) { Console.WriteLine("Wrong folder"); Console.ReadKey(); return; }
    }
    
    // Define the event handlers.

    private static void OnDeleted(object source, FileSystemEventArgs e) 
    {
        if (!Path.HasExtension(e.FullPath))
        {
            //directory: a file with no extension is treated like a directory
            Console.WriteLine("Directory: " + e.FullPath + " Deleted");
        }
        else
        {
            //file
            if ( !Path.GetFileName(e.FullPath)[0].Equals('~') && !Path.GetFileName(e.FullPath)[0].Equals('$') &&
                    !Path.GetFileName(e.FullPath)[0].Equals('~') && !Path.GetExtension(e.FullPath).Equals(".tmp") &&
                    !Path.GetExtension(e.FullPath).Equals(".TMP") )
            {
                Console.WriteLine("File: " + e.FullPath + " Deleted");
            }
        }
    }

    private static void OnCreated(object source, FileSystemEventArgs e)
    {   
        if (Directory.Exists(e.FullPath))
        {    
            //directory
            //DirectoryInfo di = new DirectoryInfo(e.FullPath);
            Console.WriteLine("Directory: " + e.FullPath + " Created");
        }
        if (File.Exists(e.FullPath))
        {
            //exclude files started from '~','$' and '.'(hidden files) and without extensions
            FileInfo fi = new FileInfo(e.FullPath);
            if ( !fi.Name[0].Equals('~') && !fi.Name[0].Equals('$') && !fi.Name[0].Equals('.') &&
                    !fi.Extension.Equals(".tmp") && !fi.Extension.Equals(".TMP") && !fi.Extension.Equals("") )
            {
                //file
                Console.WriteLine("File: " + e.FullPath + " Created");
            }   
        } 
    }

 
    private static void OnChanged(object source, FileSystemEventArgs e)
    {
        try
        {
            //to avoid double changes
            watcher.EnableRaisingEvents = false;
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
        catch (System.UnauthorizedAccessException)
        {
            //file protetto o non puo essere aperto perche' e' eseguibile
            Console.WriteLine("PROTECTED File: " + e.FullPath + " " + e.ChangeType);
        }

        finally
        {
            watcher.EnableRaisingEvents = true;
        }
    }

    private static void OnRenamed(object source, RenamedEventArgs e)
    {
        //if from .doc -> ~ ignore
        //if from ~->.doc changed
        if (Directory.Exists(e.FullPath))
        {
            // a directory is renamed
            Console.WriteLine("Directory: {0} renamed to {1}", e.OldFullPath, e.FullPath);
        }
        if (File.Exists(e.FullPath))
        {
            // a file is renamed
            FileInfo fi_old = new FileInfo(e.OldFullPath);
            FileInfo fi_new = new FileInfo(e.FullPath);

            if ( (fi_old.Name[0].Equals('~') && !fi_new.Name[0].Equals('~')) ||
                  (fi_old.Extension.Equals(".tmp") && !fi_new.Name[0].Equals('~')) ||
                  (fi_old.Extension.Equals("") && !fi_new.Name[0].Equals('~')) ||
                  (fi_old.Name[0].Equals('~') && !fi_new.Extension.Equals("tmp")) ||
                  (fi_old.Extension.Equals(".tmp") && !fi_new.Extension.Equals("tmp")) ||
                  (fi_old.Extension.Equals("") && !fi_new.Extension.Equals("tmp")) ||
                  (fi_old.Name[0].Equals('~') && !fi_new.Extension.Equals("")) ||
                  (fi_old.Extension.Equals(".tmp") && !fi_new.Extension.Equals("")) ||
                  (fi_old.Extension.Equals("") && !fi_new.Extension.Equals("")) )
            {
                //file Microsoft changed
                Console.WriteLine("File MICROSOFT: " + e.FullPath + " Changed");
            }
            else
            {
                if (!fi_new.Name[0].Equals('~') && !fi_new.Name[0].Equals('$') && !fi_new.Name[0].Equals('.') &&
                            !fi_new.Extension.Equals(".tmp") && !fi_new.Extension.Equals(".TMP") && !fi_new.Extension.Equals(""))
                {
                    Console.WriteLine("File: {0} renamed to {1}", e.OldFullPath, e.FullPath);
                }
            }
        }
    }
}
