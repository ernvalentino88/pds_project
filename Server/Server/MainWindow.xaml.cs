using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
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
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using System.Data.SQLite;
using System.Security.Cryptography;
using Utility;

namespace Server
{
    /// <summary>
    /// Logica di interazione per MainWindow.xaml
    /// </summary>
    /// 
    public partial class MainWindow : MetroWindow
    {
        public static int receive_timeout_long = 1000 * 5*60;
        public static int send_timeout_short = 1000 * 10;
        public static int receive_timeout_short = 1000 * 10;

        public delegate void start_server_delegate(String port);
        public delegate void update_ui_delegate(String msg);
        public delegate Task UpdateDelegateAsync(String msg, String bannerTitle, String bannerMsg);
        private TcpListener myList;
        private Boolean connected;
        private ServerUtility server;
       
        public MainWindow()
        {
            InitializeComponent();
            connected = false;
            server = new ServerUtility();
        }

        private void launch_button_Click(object sender, RoutedEventArgs e)
        {
            
            start_server_delegate sd = new start_server_delegate(start_server);
            sd.BeginInvoke(this.text_port.Text,null, null);
        }

        public void start_server(String port) 
        {
            if (connected)
            {
                connected = false;
                this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new update_ui_delegate(updateUI), "Insert port number for listening to ingress connection");
                myList.Stop();
            }
            else
            {
                Socket s = null;
                try
                {
             
                    myList = new TcpListener(IPAddress.Any, Int32.Parse(port));
                    myList.Start();
                    connected = true;
                    String msg = "The server is running at : " + myList.LocalEndpoint;
                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new update_ui_delegate(updateUI), msg);

                    while (true)
                    {
                        //TODO uscire correttamente dal while
                        s = myList.AcceptSocket();
                        msg = "Connection accpeted from " + s.RemoteEndPoint;
                        this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new update_ui_delegate(updateUI_msg), msg);
                        ClientSession cs = null;
                        //connection done
                        bool exit = false;
                        while (!exit)
                        {
                            s.ReceiveTimeout = receive_timeout_long;
                            s.SendTimeout =send_timeout_short;
                            //byte[] buffer_command = new byte[4];
                            //int b = s.Receive(buffer_command);
                            byte[] buffer_command = Utility.Networking.my_recv(4,s);
                            s.ReceiveTimeout = receive_timeout_short;
                            if (buffer_command!=null)
                            {
                                Networking.CONNECTION_CODES code = (Networking.CONNECTION_CODES)BitConverter.ToUInt32(buffer_command, 0);
                                switch (code)
                                {
                                    case Networking.CONNECTION_CODES.KEY_EXC:
                                        cs = server.keyExchangeTcpServer(s);
                                        if (cs == null) { 
                                            exit = true; 
                                        }
                                        break;
                                    case Networking.CONNECTION_CODES.AUTH:
                                        if (cs != null)
                                        {
                                            Int64 sessionId = server.authenticationTcpServer(cs);                         
                                            if (sessionId <= 0) { 
                                                exit = true; 
                                            }
                                        }
                                        break;
                                    case Networking.CONNECTION_CODES.EXIT:
                                        exit = true;
                                        break;
                                    case Networking.CONNECTION_CODES.NEW_REG:
                                        if (cs != null) {
                                            Console.Write("ok");
                                            server.registrationTcpServer(cs);
                                        }
                                        exit = true;
                                        break;
                                    default: break;
                                }
                            }
                        }
                        s.Close();
                        
                    }
                }
                catch (SocketException se)
                {
                    connected = false;
                    String msg = "Insert port number for listening to ingress connection";
                    StreamWriter sw = new StreamWriter("server_log.txt", true);
                    sw.Write(DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
                    sw.WriteLine(" ***Fatal Error***  " + se.Message);
                    sw.WriteLine(se.StackTrace);
                    sw.Close();
                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new UpdateDelegateAsync(updateUI_banner), msg, "Network Error", se.Message);
                    if (s != null) {
                        s.Close();
                    }
                }
                catch (Exception ex)
                {
                    connected = false;
                    String msg = "Insert port number for listening to ingress connection";
                    StreamWriter sw = new StreamWriter("server_log.txt", true);
                    sw.Write(DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
                    sw.WriteLine(" ***Fatal Error***  " + ex.Message);
                    sw.WriteLine(ex.StackTrace);
                    sw.Close();
                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new UpdateDelegateAsync(updateUI_banner), msg, "Unexpected Error", ex.Message);
                }
            }
        }

        private void updateUI_msg(String msg)
        {
            this.text_label.Content = msg;
        }

        private void updateUI(String msg)
        {
            if (connected)
            {
                this.launch_button.Content = "disconnect";
                this.text_port.Visibility = Visibility.Hidden;
            }
            else
            {
                this.launch_button.Content = "launch";
                this.text_port.Visibility = Visibility.Visible;
            }
            this.text_label.Content = msg;
        }

        private async Task updateUI_banner(String msg, String bannerTitle, String bannerMsg)
        {
            await this.ShowMessageAsync(bannerTitle, bannerMsg);
            this.launch_button.Content = "launch";
            this.text_port.Visibility = Visibility.Visible;
            this.text_label.Content = msg;
        }

      /*  private bool get_cmd(Socket s) {
            byte[] buffer_command = new byte[4];
            int b = s.Receive(buffer_command);
            if (b != 4) { throw new System.Exception("Wrong command bytes"); }
            Networking.CONNECTION_CODES code = (Networking.CONNECTION_CODES)BitConverter.ToUInt32(buffer_command, 0);
            switch (code) {
                case Networking.CONNECTION_CODES.EXIT: return false;
                case Networking.CONNECTION_CODES.NEW_REG:  break;
                case Networking.CONNECTION_CODES.AUTH: 
                    long sid = Networking.authenticationTcpServer(aes, s); 
                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new update_ui_delegate(updateUI_msg), "SESSION ID:"+sid);
                    break;
                

            }

            return true;
        }*/

     /*   private void register(Socket s) {
            byte[] buffer_name = new byte[256];
            byte[] buffer_pwd = new byte[256];
            int b=s.Receive(buffer_name);
            if (b != 256) { throw new System.Exception("Wrong size"); }
            b = s.Receive(buffer_pwd);
            if (b != 256) { throw new System.Exception("Wrong size"); }
            byte[] name = aes.AES_Decrypt(buffer_name);
            byte[] pwd = aes.AES_Decrypt(buffer_pwd);
            string id = System.Text.Encoding.Default.GetString(name);
            string pass = System.Text.Encoding.Default.GetString(pwd);

            if (!DBmanager.register(id, pass)) {
                s.Send(BitConverter.GetBytes((UInt32)Networking.CONNECTION_CODES.ERR));
                throw new Exception("Error during register");   
            }
        }
        */
  

        

    }
}
