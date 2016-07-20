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

namespace ServerApp
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
        private Server server;
        private static object syncLock = new object();
        //private List<Socket> all_sockets = new List<Socket>();
        private ManualResetEvent evt=new ManualResetEvent(false) ;
       
        public MainWindow()
        {
            InitializeComponent();
            connected = false;
            server = new Server();
        }

        private void launch_button_Click(object sender, RoutedEventArgs e)
        {
            
            start_server_delegate sd = new start_server_delegate(start_server);
            sd.BeginInvoke(this.text_port.Text,null, null);
        }

        public void start_server(String port) 
        {
            Socket s = null;
            if (connected)
            {
                connected = false;
                this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new update_ui_delegate(updateUI), "Insert port number for listening to ingress connection");
                //close_all();
                if (myList != null)
                {
                    myList.Stop();
                    myList.Server.Close();
                }
                evt.Set();
                //http://stackoverflow.com/questions/5074996/stopping-all-thread-in-net-threadpool
                //MAybe need to notify all threads to exit on disconnect using MAnualResetEvent
               
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
                        s = myList.AcceptSocket(); 
                        //all_sockets.Add(s);
                        ThreadPool.QueueUserWorkItem(new WaitCallback(clientHandler), s);
                        msg = "Connection accpeted from " + s.RemoteEndPoint;
                        this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new update_ui_delegate(updateUI_msg), msg);
                    }
                }
                catch (SocketException se)
                {
                    connected = false;
                    if (se.SocketErrorCode != SocketError.Interrupted)
                    {
                        String msg = "Insert port number for listening to ingress connection";
                        StreamWriter sw = new StreamWriter("server_log.txt", true);
                        sw.Write(DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
                        sw.WriteLine(" ***Fatal Error***  " + se.Message);
                        sw.WriteLine(se.StackTrace);
                        sw.Close();
                        this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new UpdateDelegateAsync(updateUI_banner), msg, "Network Error", se.Message);
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
                finally
                {
                    if (s != null && s.Connected)
                    {
                        //all_sockets.Remove(s);
                        s.Close();
                    }
                }
            }
        }

        private void clientHandler(Object state)
        {
            Socket s = (Socket)state;
            ClientSession cs = null;
            bool exit = false;
            try
            {
                while (!exit)
                {
                    if (evt.WaitOne(0))
                       break; // or otherwise exit the thread
                    s.ReceiveTimeout = Networking.TIME_OUT_LONG;//TODO:MAKE LONG
                    s.SendTimeout = Networking.TIME_OUT_SHORT;
                    byte[] buffer_command = Utility.Networking.my_recv(4, s);
                    if (buffer_command != null)
                    {
                        s.ReceiveTimeout = Networking.TIME_OUT_SHORT;
                        Networking.CONNECTION_CODES code = (Networking.CONNECTION_CODES)BitConverter.ToUInt32(buffer_command, 0);
                        switch (code)
                        {
                            case Networking.CONNECTION_CODES.KEY_EXC:
                                cs = server.keyExchangeTcpServer(s);
                                if (cs == null)
                                {
                                    exit = true;
                                }
                                break;
                            case Networking.CONNECTION_CODES.AUTH:
                                if (cs != null)
                                {
                                    Int64 sessionId = server.authenticationTcpServer(cs);
                                    if (sessionId <= 0)
                                    {
                                        exit = true;
                                    }
                                }
                                break;
                            case Networking.CONNECTION_CODES.NEW_REG:
                                if (cs != null)
                                {
                                    server.registrationTcpServer(cs);
                                }
                                exit = true;
                                break;
                            case Networking.CONNECTION_CODES.SESSION:
                                if (!server.resumeSession(ref cs, s))
                                    exit = true;
                                break;
                            case Networking.CONNECTION_CODES.SESSION_WATCH:
                                if (!server.resumeSession(ref cs, s, true))
                                    exit = true;
                                break;
                            case Networking.CONNECTION_CODES.DIR:
                                if (!server.getDirectoryInfo(cs))
                                    exit = true;
                                break;
                            case Networking.CONNECTION_CODES.PREV:
                                if (!server.getPreviousVersions(cs))
                                    exit = true;
                                break;
                            case Networking.CONNECTION_CODES.INIT_SYNCH:
                                if (!server.beginSynchronization(cs))
                                    exit = true;
                                break;
                            case Networking.CONNECTION_CODES.START_SYNCH:
                                if (cs != null)
                                {
                                    if (!server.synchronizationSession(cs))
                                        exit = true;
                                }
                                break;
                            case Networking.CONNECTION_CODES.FS_SYNCH:
                                
                                    server.synchronizationSession(cs, s);
                                
                                exit = true;
                                break;
                                
                            case Networking.CONNECTION_CODES.RESTORE_DIR:
                                if (!server.restoreDirectory(cs))
                                    exit = true;
                                break;
                            case Networking.CONNECTION_CODES.RESTORE_FILE:
                                if (!server.restoreFile(cs))
                                    exit = true;
                                break;
                            case Networking.CONNECTION_CODES.GET_SNAP:
                                if (!server.getAllSnapshots(cs))
                                    exit = true;
                                break;
                            case Networking.CONNECTION_CODES.SNAP:
                                if (!server.getSnapshotInfo(cs))
                                    exit = true;
                                break;
                            case Networking.CONNECTION_CODES.DOWN:
                                if (!server.DownloadDirectory(cs))
                                    exit = true;
                                break;
                            case Networking.CONNECTION_CODES.HELLO:
                                server.Hello(s);
                                break;
                            case Networking.CONNECTION_CODES.EXIT:
                                exit = true;
                                break;
                            default:
                                exit = true;
                                break;
                        }
                    }
                    else
                        exit = true;
                }
            }
            catch (SocketException) { return; }
            //all_sockets.Remove(s);
            finally
            {
                if(s!=null)
                s.Close();
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

        //private void close_all() {
        //    foreach (Socket s in all_sockets) {
        //        if (s.Connected)
        //        {
        //            s.Close();
        //        }
        //        all_sockets.Remove(s);
        //    }
        //}
  

        

    }
}
