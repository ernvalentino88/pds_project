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

namespace Server
{
    /// <summary>
    /// Logica di interazione per MainWindow.xaml
    /// </summary>
    /// 
    public partial class MainWindow : MetroWindow
    {
        public delegate void start_server_delegate(String port);
        public delegate void update_ui_delegate(String msg);
        public delegate Task UpdateDelegateAsync(String msg, String bannerTitle, String bannerMsg);
        private TcpListener myList;
        private Boolean connected;
       
        public MainWindow()
        {
            InitializeComponent();
            connected = false;
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
                        Socket s = myList.AcceptSocket();
                        msg = "Connection accpeted from " + s.RemoteEndPoint;
                        this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new update_ui_delegate(updateUI_msg), msg);
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

       

        

       

       



    }
}
