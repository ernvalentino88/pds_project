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
        public delegate void start_server_delegate();
        //private Thread serverThread;
        TcpListener myList;
        public MainWindow()
        {
            InitializeComponent();
        }

        private void launch_button_Click(object sender, RoutedEventArgs e)
        {
            myList = new TcpListener(IPAddress.Any, Int32.Parse(this.text_port.Text));
            this.launch_button.Visibility = Visibility.Collapsed;
            this.text_port.Visibility = Visibility.Collapsed;


            this.con_stat.Visibility = Visibility.Visible;
            this.launch_button.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new start_server_delegate(start_server));
           



        }

        public void start_server() {

            try
            {
               
                //TcpListener myList = new TcpListener(IPAddress.Any, Int32.Parse(this.text_port.Text));
                //System.Threading.Thread.Sleep(1000);
                myList.Start();
                //this.launch_button.Visibility = Visibility.Collapsed;
               // this.text_port.Visibility = Visibility.Collapsed;


               // this.con_stat.Visibility = Visibility.Visible;
                this.con_stat.Content = "The server is running at : " + myList.LocalEndpoint;
                //System.Threading.Thread.Sleep(5000);
                /* Start Listeneting at the specified port */
                

                Socket s = myList.AcceptSocket();



                this.con_stat.Content = "Connection accpeted from "+s.RemoteEndPoint;


                s.Close();
                 myList.Stop();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine("Error..... " + ex.StackTrace);
            }
        
        }


       
    }
}
