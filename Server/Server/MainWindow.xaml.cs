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
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;

namespace Server
{
    /// <summary>
    /// Logica di interazione per MainWindow.xaml
    /// </summary>
    /// 
    public partial class MainWindow : MetroWindow
    {
        TcpListener myList;
        public delegate void start_server_delegate();
        public delegate void update_ui_delegate(String msg);
        //private Thread serverThread;
       
        public MainWindow()
        {
            InitializeComponent();
        }

        private void launch_button_Click(object sender, RoutedEventArgs e)
        {

           myList = new TcpListener(IPAddress.Any, Int32.Parse(this.text_port.Text));
           start_server_delegate sd = new start_server_delegate(start_server);
           sd.BeginInvoke(null,null);
          



        }

        public void start_server() {
           
            try
            {
                
                myList.Start();
              
               String msg="The server is running at : " + myList.LocalEndpoint;
                this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new update_ui_delegate(update_msg), msg);
               
          
               
              
                /* Start Listeneting at the specified port */
            
                while (true)
                {
                    Socket s = myList.AcceptSocket();
                    msg = "Connection accpeted from " + s.RemoteEndPoint;
                   this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new update_ui_delegate(update_msg), msg);
                   
                   

                    s.Close();
                }
                myList.Stop();
            }
            catch (Exception ex)
            {
              String msg = ex.Message;
              this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new update_ui_delegate(update_msg), msg);
            }
        
        }

        private void update_msg(String msg) {
            this.launch_button.Visibility = Visibility.Collapsed;
            this.text_port.Visibility = Visibility.Collapsed;
            this.con_stat.Visibility = Visibility.Visible;
            this.con_stat.Content = msg;
        }


       
    }
}
