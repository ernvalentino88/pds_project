using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        private Boolean connected;
        private Client client;

        public MainWindow()
        {
            InitializeComponent();
            connected = false;
        }

        private void Connect_button_Click(object sender, RoutedEventArgs e)
        {
            ConnectDelegate cd = new ConnectDelegate(connect);
            cd.BeginInvoke(this.Text_ip.Text, this.Text_port.Text,this.Text_user.Text,this.Box_pwd.Password,null, null);
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
                    TcpClient tcpClient = new TcpClient();
                    tcpClient.Connect(address, portInt);
                    tcpClient.ReceiveTimeout = Networking.TIME_OUT_SHORT;
                    tcpClient.SendTimeout = Networking.TIME_OUT_SHORT;
                    client.TcpClient = tcpClient;

                    if (client.keyExchangeTcpClient())
                    {
                        Int64 sessionId = client.authenticationTcpClient(username, pwd);
                        msg = "" + sessionId;
                        this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new UpdateDelegate(updateUI), msg);
                        client.Server = new IPEndPoint(IPAddress.Parse(address), portInt);
                        client.UserId = username;
                        DispatcherOperation result = this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new LoginDelegate(updateUI_logged));
                        result.Wait();
                        String directory = (String)result.Result;

                        if (!client.resumeSession())
                        {
                            connected = false;
                            tcpClient.Close();
                            msg = "Log in to the remote server";
                            String title = "You were disconncted";
                            String bannerMsg = "Your session is expired, please login again";
                            this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new UpdateDelegateAsync(updateUI_banner), msg, title, bannerMsg);
                        }
                        msg = "resumed: " + sessionId;
                        this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new UpdateDelegate(updateUI), msg);
                        //if (!client.sendDirectory(directory))
                        //{
                        //    connected = false;
                        //    tcpClient.Close();
                        //    msg = "Log in to the remote server";
                        //    String title = "You were disconncted";
                        //    String bannerMsg = "Your session is expired, please login again";
                        //    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new UpdateDelegateAsync(updateUI_banner), msg, title, bannerMsg);
                        //}
                        DirectoryStatus local = new DirectoryStatus();
                        local.FolderPath = directory;
                        local.Username = username;
                        client.fillDirectoryStatus(local, directory);
                        this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new FillGrid(fill_grid), local);
                        DirectoryStatus remote = new DirectoryStatus();
                        //remote.Username = username;
                        //remote.FolderPath = directory;
                        //if (client.startSynch(remote) >= 0)
                        //{

                        //    client.synchronize(local, remote);
                        //}

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

        private void fill_grid(DirectoryStatus status)
        {
            ObservableCollection<DirectoryFile> obs = new ObservableCollection<DirectoryFile>(status.Files.Values);
            this.fileGrid.ItemsSource = obs;
        }

        private String updateUI_logged()
        {
            this.Grid_initial.Visibility = Visibility.Collapsed;
            this.Grid_logged.Visibility = Visibility.Visible;
            FolderBrowserDialog dlg = new FolderBrowserDialog();
            dlg.Description = "Choose the folder you want to start synchronize";
            dlg.ShowDialog();
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
                connected = false;
                client.closeConnectionTcpClient();
            }
        }

    }
}
