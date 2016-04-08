using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Utility;

namespace ClientApp
{
    /// <summary>
    /// Logica di interazione per MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        private delegate void ConnectDelegate(String address, String port, String username, String pwd);
        private delegate void RegisterDelegate(String address, String port, String user, String pwd1, String pwd2);
        private delegate void UpdateDelegate(String msg);
        private delegate Task UpdateDelegateAsync(String msg, String bannerTitle, String bannerMsg);
        private delegate String LoginDelegate();
        private delegate void FillGrid(DirectoryStatus status);
        private delegate void UpdateBar(int n);
        private delegate void InitBar();
        private Boolean connected;
        private Client client;
        private String CurrentDirectory;
        private String RootDirectory;
        private ObservableCollection<ListItem> FileList;
        private BackgroundWorker sync_worker;
        private BackgroundWorker restore_worker;
        private Watcher watcher;

        public MainWindow()
        {
            InitializeComponent();
            connected = false;
            FileList = new ObservableCollection<ListItem>();
            sync_worker = new BackgroundWorker();
            sync_worker.WorkerReportsProgress = true;
            sync_worker.WorkerSupportsCancellation = true;
            sync_worker.DoWork += new DoWorkEventHandler(sw_DoWork);
            sync_worker.ProgressChanged += new ProgressChangedEventHandler(sw_ProgressChanged);
            sync_worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(sw_RunWorkerCompleted);
            restore_worker = new BackgroundWorker();
            restore_worker.WorkerReportsProgress = true;
            restore_worker.WorkerSupportsCancellation = true;
            restore_worker.DoWork += new DoWorkEventHandler(rw_DoWork);
            restore_worker.ProgressChanged += new ProgressChangedEventHandler(rw_ProgressChanged);
            restore_worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(rw_RunWorkerCompleted);
        }


        private void sw_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (!connected)
                return;
            if (!client.resumeSession())
            {
                connected = false;
                client.TcpClient.Close();
                String msg = "Log in to the remote server";
                String title = "You were disconncted";
                String bannerMsg = "Your session is expired, please login again";
                this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new UpdateDelegateAsync(updateUI_banner), msg, title, bannerMsg);
            }
            this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)(() =>
            {
                this.progressBar_file.Visibility = Visibility.Hidden;
                this.file_grid.Visibility = Visibility.Visible;
                this.Back_button.Visibility = Visibility.Visible;
                this.Refresh_button.Visibility = Visibility.Visible;
                this.Label_log.Visibility = Visibility.Hidden;
            }));
            DirectoryStatus remote = new DirectoryStatus();
            remote.Username = client.UserId;
            remote.FolderPath = RootDirectory;
            if (client.getDirectoryInfo(remote, RootDirectory) < 0)
            {
                connected = false;
                client.TcpClient.Close();
                String msg = "Log in to the remote server";
                String title = "You were disconncted";
                String bannerMsg = "A Network Error occurred, please try later";
                this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new UpdateDelegateAsync(updateUI_banner), msg, title, bannerMsg);
            }
            else
            {
                this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new FillGrid(fill_grid), remote);
                watcher = new Watcher(RootDirectory, client);
            }
        }

        private void sw_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            this.Dispatcher.BeginInvoke(DispatcherPriority.Background, new UpdateBar(update_bar), e.ProgressPercentage);
        }

        private void sw_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = sender as BackgroundWorker;
            PbUpdater up = (PbUpdater)e.Argument;
            if (worker.CancellationPending)
            {
                e.Cancel = true;
            }
            else
            {
                if (!client.resumeSession())
                {
                    connected = false;
                    client.TcpClient.Close();
                    String msg = "Log in to the remote server";
                    String title = "You were disconncted";
                    String bannerMsg = "Your session is expired, please login again";
                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new UpdateDelegateAsync(updateUI_banner), msg, title, bannerMsg);
                }
                else
                {
                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)(() =>
                    {
                        this.progressBar_file.IsIndeterminate = false;
                        this.Label_log.Content = "Checking last session of the direcroty from server ...";
                    }));
                    if (client.getRemoteStatus(up.remote, RootDirectory) >= 0)
                    {
                        this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)(() =>
                        {
                            this.Label_log.Content = "Checking last session of the direcroty from server ... Done";
                        }));
                        client.synchronize(up.local, up.remote, sync_worker);
                    }
                }
            }
        }

        private void Connect_button_Click(object sender, RoutedEventArgs e)
        {
            ConnectDelegate cd = new ConnectDelegate(connect);
            cd.BeginInvoke(this.Text_ip.Text, this.Text_port.Text, this.Text_user.Text, this.Box_pwd.Password, null, null);
        }

        private void connect(String address, String port, String username, String pwd)
        {
            if (address.Equals(String.Empty) || port.Equals(String.Empty) ||
                 username.Equals(String.Empty) || pwd.Equals(String.Empty) || 
                 address == null || port == null || username == null || pwd == null)
                return;
            if (connected)
            {
                connected = false;
                client.closeConnectionTcpClient();
                if (sync_worker.IsBusy)
                    sync_worker.CancelAsync();
                sync_worker.Dispose();
                if (watcher != null)
                    watcher.Stop();
                String msg = "Log in to the remote server";
                this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new UpdateDelegate(updateUI), msg);
            }
            else
            {
                try
                {
                    client = new Client();
                    Int32 portInt = Int32.Parse(port);
                    connected = true;
                    String msg = "Trying to connect to the server . . .";
                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new UpdateDelegate(updateUI_progressBar), msg);
                     
                    client.TcpClient = new TcpClient();
                    TcpClient tcpClient = client.TcpClient;
                    tcpClient.Connect(address, portInt);
                    tcpClient.ReceiveTimeout = Networking.TIME_OUT_SHORT;
                    tcpClient.SendTimeout = Networking.TIME_OUT_SHORT;
                    

                    if (client.keyExchangeTcpClient())
                    {
                        Int64 sessionId = client.authenticationTcpClient(username, pwd);
                        if (sessionId <= 0)
                        {
                            if (sessionId == -1)
                            {
                                //network or other error
                                msg = "Log in to the remote server";
                                this.Dispatcher.BeginInvoke(DispatcherPriority.Normal,
                                    new UpdateDelegateAsync(updateUI_banner), msg, "Server Unreachable", "A network error occurred");
                            }
                            if (sessionId == -2 || sessionId == -1)
                            {
                                //username or password not correct
                                msg = "Log in to the remote server";
                                this.Dispatcher.BeginInvoke(DispatcherPriority.Normal,
                                    new UpdateDelegateAsync(updateUI_banner), msg, "Log in incorrect", "The combination email/password you provided is incorrect");
                            }
                        }
                        client.Server = new IPEndPoint(IPAddress.Parse(address), portInt);
                        client.UserId = username;
                        DispatcherOperation result = this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new LoginDelegate(updateUI_logged));
                        result.Wait();
                        RootDirectory = (String)result.Result;
                        if (RootDirectory != null && RootDirectory != String.Empty)
                        {

                            DirectoryStatus local = new DirectoryStatus();
                            local.FolderPath = RootDirectory;
                            local.Username = username;
                            CurrentDirectory = String.Copy(RootDirectory);
                            client.fillDirectoryStatus(local, RootDirectory);
                            DirectoryStatus remote = new DirectoryStatus();
                            remote.Username = username;
                            remote.FolderPath = RootDirectory;
                            // for updating the progress bar
                            PbUpdater up = new PbUpdater();
                            up.rootDir = RootDirectory;
                            up.remote = remote;
                            up.local = local;
                            sync_worker.RunWorkerAsync(up);
                            // try to free some memory ...
                            local = null;
                            remote = null;
                            GC.Collect();
                        }
                    }
                }
                catch (SocketException se)
                {
                    connected = false;
                    if (client.TcpClient != null && client.TcpClient.Connected)
                        client.TcpClient.Close();
                    String msg = "Log in to the remote server";
                    StreamWriter sw = new StreamWriter("client_log.txt", true);
                    sw.Write(DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
                    sw.WriteLine(" ***Fatal Error***  " + se.Message);
                    sw.WriteLine(se.StackTrace);
                    sw.Close();
                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, 
                        new UpdateDelegateAsync(updateUI_banner), msg, "Server Unreachable", se.Message);
                    
                }
                catch (Exception exc)
                {
                    connected = false;
                    if (client.TcpClient != null && client.TcpClient.Connected)
                        client.TcpClient.Close();
                    String msg = "Log in to the remote server";
                    StreamWriter sw = new StreamWriter("client_log.txt", true);
                    sw.Write(DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
                    sw.WriteLine(" ***Fatal Error***  " + exc.Message);
                    sw.WriteLine(exc.StackTrace);
                    sw.Close();
                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal,
                        new UpdateDelegateAsync(updateUI_banner), msg, "An error occurred", exc.Message);
                }
            }
        }

        private void update_bar(int n)
        {
            this.progressBar_file.IsIndeterminate = false;
            this.Label_log.Content = "Synchronization progress: " + n + "%";
            this.progressBar_file.Value = n;
        }

        private void fill_grid(DirectoryStatus status)
        {
            FileList.Clear();
            if (CurrentDirectory.Equals(RootDirectory))
            {
                //see turn back botton
                this.Back_button.IsEnabled = false;
            }
            else
            {
                //don't see botton
                this.Back_button.IsEnabled = true;
            }
            foreach (var item in status.Files)
            {
                FileListItem file = new FileListItem(item.Value);
                FileList.Add(file);
            }
            this.Label_log.Visibility = Visibility.Hidden;
            this.filePrev_grid.Visibility = Visibility.Hidden;
            this.file_grid.Visibility = Visibility.Visible;
            this.file_grid.ItemsSource = FileList;        
        }

        private String updateUI_logged()
        {
            this.Grid_initial.Visibility = Visibility.Collapsed;
            this.Grid_logged.Visibility = Visibility.Visible;
            this.file_grid.Visibility = Visibility.Hidden;
            this.Back_button.Visibility = Visibility.Hidden;
            this.Refresh_button.Visibility = Visibility.Hidden;
            FolderBrowserDialog dlg = new FolderBrowserDialog();
            dlg.Description = "Choose the folder you want to start synchronize";
            dlg.ShowDialog();
            this.progressBar_file.Visibility = Visibility.Visible;
            this.progressBar_file.IsIndeterminate = true;
            this.Label_log.Content = "Retrieving directory infromation . . .";
            this.Label_log.Visibility = Visibility.Visible;
            return dlg.SelectedPath;
        }

        private void Register_button_Click(object sender, RoutedEventArgs e)
        {
            RegisterDelegate rd = new RegisterDelegate(register);
            rd.BeginInvoke(this.Text_regIp.Text, 
                this.Text_regPort.Text, 
                this.Text_regUser.Text, 
                this.Box_regPwd1.Password, 
                this.Box_regPwd2.Password, null, null);
        }

        private void register(String address, String port, String user, String pwd1, String pwd2)
        {
            if (address.Equals(String.Empty) || port.Equals(String.Empty) ||
                 user.Equals(String.Empty) || pwd1.Equals(String.Empty) || pwd2.Equals(String.Empty) ||
                 address == null || port == null || user == null || pwd1 == null || pwd2 == null)
                return;
            if (!(pwd1.Equals(pwd2)))
            {
                String msg = "Create account";
                String title = "Registration Failed";
                String msgBanner = "The passwords inserted are different";
                this.Dispatcher.BeginInvoke(DispatcherPriority.Normal,
                    new UpdateDelegateAsync(updateUI_regBanner), msg, title, msgBanner);
                return;
            }
            if (user.Length > 16)
            {
                String msg = "Create account";
                String title = "Registration Failed";
                String msgBanner = "The username must be up to 16 characters";
                this.Dispatcher.BeginInvoke(DispatcherPriority.Normal,
                    new UpdateDelegateAsync(updateUI_regBanner), msg, title, msgBanner);
                return;
            }
            try
            {
                client = new Client();
                Int32 portInt = Int32.Parse(port);
                String msg = "Trying to connect to the server . . .";
                this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new UpdateDelegate(updateUI_progressBarReg), msg);
                TcpClient tcpClient = new TcpClient();
                tcpClient.Connect(address, portInt);
                tcpClient.ReceiveTimeout = Networking.TIME_OUT_SHORT;
                tcpClient.SendTimeout = Networking.TIME_OUT_SHORT;
                client.TcpClient = tcpClient;
                
                if (client.keyExchangeTcpClient())
                {
                    int ret = client.registrationTcpClient(user, pwd1);
                    msg = "Create account";
                    String title = "Registration result";
                    String bannerMsg = "" + ret;
                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, 
                        new UpdateDelegateAsync(updateUI_regBanner), msg, title, bannerMsg);
                }
            }
            catch (SocketException se)
            {
                String msg = "Create Account";
                StreamWriter sw = new StreamWriter("client_log.txt", true);
                sw.Write(DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
                sw.WriteLine(" ***Fatal Error***  " + se.Message);
                sw.WriteLine(se.StackTrace);
                sw.Close();
                this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, 
                    new UpdateDelegateAsync(updateUI_regBanner), msg, 
                    "Server Unreachable", se.Message);
            }
            catch (Exception exc)
            {
                String msg = "Create Account";
                StreamWriter sw = new StreamWriter("client_log.txt", true);
                sw.Write(DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
                sw.WriteLine(" ***Fatal Error***  " + exc.Message);
                sw.WriteLine(exc.StackTrace);
                sw.Close();
                this.Dispatcher.BeginInvoke(DispatcherPriority.Normal,
                    new UpdateDelegateAsync(updateUI_regBanner), msg,
                    "An error occurred", exc.Message);
            }
            finally
            {
                if (client.TcpClient != null)
                {
                    client.TcpClient.Close();
                }
            }
        }

        public void updateUI(String msg)
        {
            if (connected)
            {
                this.Text_ip.Visibility = Visibility.Hidden;
                this.Text_port.Visibility = Visibility.Hidden;
                this.Text_user.Visibility = Visibility.Hidden;
                this.Box_pwd.Visibility = Visibility.Hidden;
                this.Connect_button.Content = "disconnect";
            }
            else
            {
                this.Grid_logged.Visibility = Visibility.Collapsed;
                this.Text_ip.Visibility = Visibility.Visible;
                this.Text_port.Visibility = Visibility.Visible;
                this.Text_user.Visibility = Visibility.Visible;
                this.Box_pwd.Visibility = Visibility.Visible;
                this.Grid_initial.Visibility = Visibility.Visible; 
                this.Connect_button.Content = "connect";
            }
            this.progress_bar.Visibility = Visibility.Hidden;
            this.Message_label.Content = msg;
        }

        public async Task updateUI_banner(String msg, String bannerTitle, String bannerMsg)
        {
            await this.ShowMessageAsync(bannerTitle, bannerMsg);
            this.Grid_initial.Visibility = Visibility.Visible;
            this.Grid_logged.Visibility = Visibility.Collapsed;
            this.Text_ip.Visibility = Visibility.Visible;
            this.Text_port.Visibility = Visibility.Visible;
            this.Text_user.Visibility = Visibility.Visible;
            this.Box_pwd.Visibility = Visibility.Visible;
            this.Connect_button.Content = "connect";
            this.progress_bar.Visibility = Visibility.Hidden;
            this.Message_label.Content = msg;
        }

        public async Task updateUI_regBanner(String msg, String bannerTitle, String bannerMsg)
        {
            await this.ShowMessageAsync(bannerTitle, bannerMsg);
            this.Text_regIp.Visibility = Visibility.Visible;
            this.Text_regPort.Visibility = Visibility.Visible;
            this.Text_regUser.Visibility = Visibility.Visible;
            this.Box_regPwd1.Visibility = Visibility.Visible;
            this.Box_regPwd2.Visibility = Visibility.Visible;
            this.Register_button.Visibility = Visibility.Visible;
            this.progressBar_reg.Visibility = Visibility.Hidden;
            this.label_regMsg.Content = msg;
        }

        public void updateUI_progressBar(String msg)
        {
            this.Text_ip.Visibility = Visibility.Hidden;
            this.Text_port.Visibility = Visibility.Hidden;
            this.Text_user.Visibility = Visibility.Hidden;
            this.Box_pwd.Visibility = Visibility.Hidden;
            this.Connect_button.Content = "disconnect";
            this.progress_bar.Visibility = Visibility.Visible;
            this.Message_label.Content = msg;
        }

        public void updateUI_progressBarReg(String msg)
        {
            this.Text_regIp.Visibility = Visibility.Hidden;
            this.Text_regPort.Visibility = Visibility.Hidden;
            this.Text_regUser.Visibility = Visibility.Hidden;
            this.Box_regPwd1.Visibility = Visibility.Hidden;
            this.Box_regPwd2.Visibility = Visibility.Hidden;
            this.Register_button.Visibility = Visibility.Hidden;
            this.progressBar_reg.Visibility = Visibility.Visible;
            this.label_regMsg.Content = msg;
        }

        private void close(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (connected)
            {
                client.closeConnectionTcpClient();
                connected = false;
            }
        }

        private void file_grid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (this.file_grid.SelectedItem == null)
                return;
            var item = this.file_grid.SelectedItem as FileListItem;
            if (item.Directory)
            {
                //entry is a directory
                DirectoryStatus dir = new DirectoryStatus();
                CurrentDirectory = System.IO.Path.Combine(item.Path, item.Filename);
                if (client.getDirectoryInfo(dir, CurrentDirectory) < 0)
                {
                    connected = false;
                    client.TcpClient.Close();
                    if (watcher != null)
                        watcher.Stop();
                    String msg = "Log in to the remote server";
                    String title = "You were disconnected";
                    String bannerMsg = "A Network Error occurred, please try later";
                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new UpdateDelegateAsync(updateUI_banner), msg, title, bannerMsg);
                }
                this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new FillGrid(fill_grid), dir);
            }
        }

        private void Back_button_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentDirectory != null)
            {
                CurrentDirectory = System.IO.Path.GetDirectoryName(CurrentDirectory);
                DirectoryStatus dir = new DirectoryStatus();
                this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)(() =>
                {
                    this.Back_button.IsEnabled = true;
                    this.Refresh_button.IsEnabled = true;
                    this.Label_filename.Visibility = Visibility.Hidden;
                }));
                if (client.getDirectoryInfo(dir, CurrentDirectory) < 0)
                {
                    connected = false;
                    client.TcpClient.Close();
                    if (watcher != null)
                        watcher.Stop();
                    String msg = "Log in to the remote server";
                    String title = "You were disconnected";
                    String bannerMsg = "A Network Error occurred, please try later";
                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new UpdateDelegateAsync(updateUI_banner), msg, title, bannerMsg);
                }
                else 
                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new FillGrid(fill_grid), dir);
            }
        }

        private void Refresh_button_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentDirectory != null)
            {
                DirectoryStatus dir = new DirectoryStatus();
                if (client.getDirectoryInfo(dir, CurrentDirectory) < 0)
                {
                    connected = false;
                    client.TcpClient.Close();
                    if (watcher != null)
                        watcher.Stop();
                    String msg = "Log in to the remote server";
                    String title = "You were disconnected";
                    String bannerMsg = "A Network Error occurred, please try later";
                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new UpdateDelegateAsync(updateUI_banner), msg, title, bannerMsg);
                }
                else 
                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new FillGrid(fill_grid), dir);
            }
        }

        private void Restore_Button_Click(object sender, RoutedEventArgs e)
        {
            ListItem item = ((FrameworkElement)sender).DataContext as ListItem;
            String path = System.IO.Path.Combine(item.Path, item.Filename);
            if (item.Directory)
            {
                //restore directory
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
                PbUpdater up = new PbUpdater();
                up.path = path;
                watcher.Stop();
                restore_worker.RunWorkerAsync(up);               
            }
            else
            {
                if (item.Deleted)
                {
                    //restore file
                    if (!Directory.Exists(item.Path))
                        Directory.CreateDirectory(item.Path);
                    PbUpdater up = new PbUpdater();
                    up.path = path;
                    up.id = item.Id;
                    watcher.Stop();
                    restore_worker.RunWorkerAsync(up);
                }
                else
                {
                    //for turn back on the directory
                    CurrentDirectory = path;
                    //see older versions of a file
                    DirectoryStatus dir = new DirectoryStatus();
                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)(() =>
                    {
                        this.Back_button.IsEnabled = false;
                        this.Refresh_button.IsEnabled = false;
                    }));
                    int n = client.getPreviousVersions(dir, CurrentDirectory);
                    if (n < 0)
                    {
                        connected = false;
                        client.TcpClient.Close();
                        if (watcher != null)
                            watcher.Stop();
                        String msg = "Log in to the remote server";
                        String title = "You were disconnected";
                        String bannerMsg = "A Network Error occurred, please try later";
                        this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new UpdateDelegateAsync(updateUI_banner), msg, title, bannerMsg);
                    }
                    if (n == 0)
                    {
                        this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)(() =>
                        {
                            FileList.Clear();
                            this.file_grid.ItemsSource = FileList;
                            this.Back_button.IsEnabled = true;
                            this.Label_log.Content = "No previous version for the file selected";
                            this.Label_log.Visibility = Visibility.Visible;
                        }));
                    }
                    else
                        this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new FillGrid(fill_prev_grid), dir);
                }
            }
        }

        private void rw_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)(() =>
            {
                this.progressBar_file.Visibility = Visibility.Visible;
                this.progressBar_file.IsIndeterminate = true;
                this.Label_log.Content = "Resyncing with the server . . .";
                this.Label_log.Visibility = Visibility.Visible;
            }));
            
            DirectoryStatus local = new DirectoryStatus();
            local.FolderPath = RootDirectory;
            local.Username = client.UserId;
            CurrentDirectory = String.Copy(RootDirectory);
            client.fillDirectoryStatus(local, RootDirectory);
            DirectoryStatus remote = new DirectoryStatus();
            remote.Username = client.UserId;
            remote.FolderPath = RootDirectory;
            // for updating the progress bar
            PbUpdater up = new PbUpdater();
            up.rootDir = RootDirectory;
            up.remote = remote;
            up.local = local;
            sync_worker.RunWorkerAsync(up);
            // try to free some memory ...
            local = null;
            remote = null;
            //GC.Collect();
        }

        private void rw_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            this.Dispatcher.BeginInvoke(DispatcherPriority.Background, new UpdateBar(update_bar), e.ProgressPercentage);
        }

        private void rw_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = sender as BackgroundWorker;
            PbUpdater up = (PbUpdater)e.Argument;
            if (worker.CancellationPending)
            {
                e.Cancel = true;
            }
            else
            {
                if (!client.resumeSession())
                {
                    connected = false;
                    client.TcpClient.Close();
                    String msg = "Log in to the remote server";
                    String title = "You were disconncted";
                    String bannerMsg = "Your session is expired, please login again";
                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new UpdateDelegateAsync(updateUI_banner), msg, title, bannerMsg);
                }
                else
                {
                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)(() =>
                    {
                        this.progressBar_file.IsIndeterminate = true;
                        this.progressBar_file.Visibility = Visibility.Visible;
                        this.file_grid.Visibility = Visibility.Hidden;
                        this.Back_button.Visibility = Visibility.Hidden;
                        this.Refresh_button.Visibility = Visibility.Hidden;
                        this.Label_log.Content = "Restoring directory " + up.rootDir + "...";
                        this.Label_log.Visibility = Visibility.Visible;
                    }));
                    //TODO check low disk space
                    if (Directory.Exists(up.path))
                        client.restoreDirectory(up.path, restore_worker);
                    else
                        client.restoreFile(up.path, up.id, restore_worker);
                }
            }
        }

        private void fill_prev_grid(DirectoryStatus status)
        {
            FileList.Clear();
            if (CurrentDirectory.Equals(RootDirectory))
            {
                //see turn back botton
                this.Back_button.IsEnabled = false;
            }
            else
            {
                //don't see botton
                this.Back_button.IsEnabled = true;
            }
            foreach (var item in status.Files)
            {
                FileVersionItem file = new FileVersionItem(item.Value);
                FileList.Add(file);
            }
            this.Label_log.Visibility = Visibility.Hidden;
            this.file_grid.Visibility = Visibility.Hidden;
            this.filePrev_grid.Visibility = Visibility.Visible;
            this.Label_filename.Content = "Previous versions of the file " + FileList[0].Filename;
            this.Label_filename.Visibility = Visibility.Visible;
            this.filePrev_grid.ItemsSource = FileList;
        }

    }

    class PbUpdater
    {
        public int value { get; set; }
        public int max { get; set; }
        public DirectoryStatus remote { get; set; }
        public DirectoryStatus local { get; set; }
        public String rootDir { get; set; }
        public String path { get; set; }
        public Int64 id { get; set; }
    }
}
