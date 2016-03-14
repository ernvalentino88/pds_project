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
        private ObservableCollection<FileListItem> FileList;
        private BackgroundWorker bw = new BackgroundWorker();
        private ManualResetEvent mre;

        public BackgroundWorker BackgroundWorker
        {
            get
            {
                return bw;
            }
            private set
            {
                bw = value;
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            connected = false;
            FileList = new ObservableCollection<FileListItem>();
            bw.WorkerReportsProgress = true;
            bw.WorkerSupportsCancellation = true;
            bw.DoWork += new DoWorkEventHandler(bw_DoWork);
            bw.ProgressChanged += new ProgressChangedEventHandler(bw_ProgressChanged);
            bw.RunWorkerCompleted += new RunWorkerCompletedEventHandler(bw_RunWorkerCompleted);
        }

        private void bw_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
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
                this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new FillGrid(fill_grid), remote);
        }

        private void bw_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            this.Dispatcher.BeginInvoke(DispatcherPriority.Background, new UpdateBar(update_bar2), e.ProgressPercentage);
        }

        private void bw_DoWork(object sender, DoWorkEventArgs e)
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
                    this.Dispatcher.BeginInvoke(DispatcherPriority.Background, new InitBar(update_bar1));
                    client.firstSynch(up.local, up.remote, RootDirectory, this);
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
                if (bw.IsBusy)
                    bw.CancelAsync(); 
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
                            }
                            if (sessionId == -2 || sessionId == -1)
                            {
                                //username or password not correct
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
                            //client.firstSynch(local, remote, RootDirectory, this);
                            
                            PbUpdater up = new PbUpdater();
                            up.rootDir = RootDirectory;
                            up.remote = remote;
                            up.local = local;
                            bw.RunWorkerAsync(up);
                            //this.getRemoteStatus(remote, RootDirectory);
                            //client.synchronize(local, remote);
                            local = null;
                            remote = null;
                            //this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)( () => {
                            //    this.progressBar_file.Visibility = Visibility.Hidden;
                            //    this.file_grid.Visibility = Visibility.Visible;
                            //    this.Back_button.Visibility = Visibility.Visible;
                            //    this.Refresh_button.Visibility = Visibility.Visible;
                            //    this.Label_log.Visibility = Visibility.Hidden;
                            //}) );
                            GC.Collect();
                            //remote = new DirectoryStatus();
                            //remote.Username = username;
                            //remote.FolderPath = RootDirectory;
                            //if (client.getDirectoryInfo(remote, RootDirectory) < 0)
                            //{
                            //    connected = false;
                            //    tcpClient.Close();
                            //    msg = "Log in to the remote server";
                            //    String title = "You were disconncted";
                            //    String bannerMsg = "A Network Error occurred, please try later";
                            //    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new UpdateDelegateAsync(updateUI_banner), msg, title, bannerMsg);
                            //}
                            //else
                            //    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new FillGrid(fill_grid), remote);
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

        public Int64 getRemoteStatus(DirectoryStatus remoteStatus, String rootDirectory)
        {
            try
            {
                Socket s = client.TcpClient.Client;
                AesCryptoServiceProvider aes = client.AESKey;
                byte[] command = BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.INIT_SYNCH);
                s.Send(command);
                byte[] buf = Security.AESEncrypt(aes, Encoding.UTF8.GetBytes(rootDirectory));
                s.Send(BitConverter.GetBytes(buf.Length));
                s.Send(buf);
                command = Networking.my_recv(4, s);
                if (command != null && (
                        ((Networking.CONNECTION_CODES)BitConverter.ToUInt32(command, 0) == Networking.CONNECTION_CODES.OK)))
                {
                    byte[] recvBuf = Networking.my_recv(4, s);
                    if (recvBuf == null)
                        return -1;
                    Int32 filesInfoToRecv = BitConverter.ToInt32(recvBuf, 0);
                    PbUpdater up = new PbUpdater();
                    up.max = filesInfoToRecv;
                    for (int i = 0; i < filesInfoToRecv; i++)
                    {
                        DirectoryFile file = new DirectoryFile();
                        recvBuf = Networking.my_recv(4, s);
                        if (recvBuf == null)
                            return -1;
                        int len = BitConverter.ToInt32(recvBuf, 0);
                        byte[] encryptedData = Networking.my_recv(len, s);
                        if (encryptedData == null)
                            return -1;
                        String path = Encoding.UTF8.GetString(Security.AESDecrypt(aes, encryptedData));
                        recvBuf = Networking.my_recv(4, s);
                        if (recvBuf == null)
                            return -1;
                        len = BitConverter.ToInt32(recvBuf, 0);
                        encryptedData = Networking.my_recv(len, s);
                        if (encryptedData == null)
                            return -1;
                        String filename = Encoding.UTF8.GetString(Security.AESDecrypt(aes, encryptedData));
                        command = Networking.my_recv(4, s);
                        if (command == null)
                            return -1;
                        if ((Networking.CONNECTION_CODES)BitConverter.ToUInt32(command, 0) == Networking.CONNECTION_CODES.DEL)
                        {
                            file.Deleted = true;
                        }
                        command = Networking.my_recv(4, s);
                        if (command == null)
                            return -1;
                        if ((Networking.CONNECTION_CODES)BitConverter.ToUInt32(command, 0) == Networking.CONNECTION_CODES.DIR)
                        {
                            file.Directory = true;
                        }
                        if ((Networking.CONNECTION_CODES)BitConverter.ToUInt32(command, 0) == Networking.CONNECTION_CODES.FILE)
                        {
                            recvBuf = Networking.my_recv(8, s);
                            if (recvBuf == null)
                                return -1;
                            Int64 id = BitConverter.ToInt64(recvBuf, 0);
                            encryptedData = Networking.my_recv(48, s);
                            if (encryptedData == null)
                                return -1;
                            String checksum = Encoding.UTF8.GetString(Security.AESDecrypt(aes, encryptedData));
                            file.Checksum = checksum;
                            file.Id = id;
                        }
                        file.UserId = client.UserId;
                        file.Filename = filename;
                        file.Path = path;
                        file.Fullname = System.IO.Path.Combine(path, filename);
                        remoteStatus.Files.Add(file.Fullname, file);
                        up.value = i;
                        this.progressBar_file.Dispatcher.BeginInvoke(DispatcherPriority.Background, new InitBar(update_bar1));
                    }
                    return filesInfoToRecv;
                }
            }
            catch (SocketException) { }
            return -1;
        }

        private void update_bar1()
        {
            this.progressBar_file.IsIndeterminate = false;
        }

        private void update_bar2(int n)
        {
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
                    String msg = "Log in to the remote server";
                    String title = "You were disconnected";
                    String bannerMsg = "A Network Error occurred, please try later";
                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new UpdateDelegateAsync(updateUI_banner), msg, title, bannerMsg);
                }
                this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new FillGrid(fill_grid), dir);
            }
            else
            {
                //entry is a file
            }
        }

        private void Back_button_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentDirectory != null)
            {
                CurrentDirectory = System.IO.Path.GetDirectoryName(CurrentDirectory);
                DirectoryStatus dir = new DirectoryStatus();
                if (client.getDirectoryInfo(dir, CurrentDirectory) < 0)
                {
                    connected = false;
                    client.TcpClient.Close();
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
                    String msg = "Log in to the remote server";
                    String title = "You were disconnected";
                    String bannerMsg = "A Network Error occurred, please try later";
                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new UpdateDelegateAsync(updateUI_banner), msg, title, bannerMsg);
                }
                else 
                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new FillGrid(fill_grid), dir);
            }
        }

        

    }
    class PbUpdater
    {
        public int value { get; set; }
        public int max { get; set; }
        public DirectoryStatus remote { get; set; }
        public DirectoryStatus local { get; set; }
        public String rootDir { get; set; }
    }
}
