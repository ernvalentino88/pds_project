using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
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
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace Client
{
    /// <summary>
    /// Logica di interazione per MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        private delegate void ConnectDelegate(String address, String port);
        private delegate void UpdateDelegate(String msg);
        private delegate Task UpdateDelegateAsync(String msg, String bannerTitle, String bannerMsg);
        private Boolean connected;

        public MainWindow()
        {
            InitializeComponent();
            connected = false;
        }

        private void Connect_button_Click(object sender, RoutedEventArgs e)
        {
            ConnectDelegate cd = new ConnectDelegate(connect);
            cd.BeginInvoke(this.Text_ip.Text, this.Text_port.Text,null, null);
        }

        private void connect(String address, String port)
        {
            if (address.Equals(String.Empty) || port.Equals(String.Empty) ||
                 address == null || port == null)
                return;
            if (connected)
            {
                connected = false;
                String msg = "Insert IP address and port of the server to connect";
                this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new UpdateDelegate(updateUI), msg);
            }
            else
            {
                TcpClient tcpclnt = new TcpClient();
                try
                {
                    Int32 portInt = Int32.Parse(port);
                    connected = true;
                    String msg = "Trying to connect to the server . . .";
                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new UpdateDelegate(updateUI_progressBar), msg);
                    tcpclnt.Connect(address, portInt);
                    msg = "Connection estabilished with: " + address + ":" + port;
                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new UpdateDelegate(updateUI), msg);
                    Stream stm = tcpclnt.GetStream();
                    String str = "ar.pdf";

                    Console.WriteLine("Sending file infos...");

                    Console.WriteLine("Transmitting file size.....");
                    FileInfo file = new FileInfo("C:\\Users\\Ernesto\\Documents\\ar.pdf");
                    byte[] fileLen = BitConverter.GetBytes(file.Length);

                    stm.Write(fileLen, 0, fileLen.Length);

                    ASCIIEncoding asen = new ASCIIEncoding();
                    byte[] ba = asen.GetBytes(str);
                    Console.WriteLine("Transmitting file name.....");

                    stm.Write(ba, 0, ba.Length);

                    byte[] bb = new byte[100];
                    int k = stm.Read(bb, 0, 100);

                    if (k > 0)
                    {
                        for (int i = 0; i < k; i++)
                            Console.Write(Convert.ToChar(bb[i]));

                        Console.WriteLine("Sending file.....");
                        FileStream fr = File.OpenRead("C:\\Users\\Ernesto\\Documents\\ar.pdf");

                        byte[] fileBytes = new byte[1024];
                        int byteRead = 0;
                        while ((byteRead = fr.Read(fileBytes, 0, fileBytes.Length)) > 0)
                        {
                            stm.Write(fileBytes, 0, byteRead);
                        }
                        fr.Close();
                        Console.WriteLine("File sent");

                        bb = new byte[100];
                        k = stm.Read(bb, 0, 100);

                        for (int i = 0; i < k; i++)
                            Console.Write(Convert.ToChar(bb[i]));


                    }
                }
                catch (SocketException se)
                {
                    connected = false;
                    String msg = "Insert IP address and port of the server to connect";
                    StreamWriter sw = new StreamWriter("client_log.txt",true);
                    sw.Write(DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
                    sw.WriteLine(" ***Fatal Error***  " + se.Message);
                    sw.WriteLine(se.StackTrace);
                    sw.Close();
                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new UpdateDelegateAsync(updateUI_banner), msg, "Server Unreachable",se.Message);
                }
                catch (Exception exc)
                {
                    connected = false;
                    String msg = "Insert IP address and port of the server to connect";
                    StreamWriter sw = new StreamWriter("client_log.txt",true);
                    sw.Write(DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
                    sw.WriteLine(" ***Fatal Error***  " + exc.Message);
                    sw.WriteLine(exc.StackTrace);
                    sw.Close();
                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new UpdateDelegate(updateUI), msg);
                }
                finally
                {
                    tcpclnt.Close();
                }
            }
        }

        public void updateUI(String msg)
        {
            if (connected)
            {
                this.Text_ip.Visibility = Visibility.Hidden;
                this.Text_port.Visibility = Visibility.Hidden;
                this.Connect_button.Content = "disconnect";
            }
            else
            {
                this.Text_ip.Visibility = Visibility.Visible;
                this.Text_port.Visibility = Visibility.Visible;
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
            this.Connect_button.Content = "connect";
            this.progress_bar.Visibility = Visibility.Hidden;
            this.Message_label.Content = msg;
        }

        public void updateUI_progressBar(String msg)
        {
            this.Text_ip.Visibility = Visibility.Hidden;
            this.Text_port.Visibility = Visibility.Hidden;
            this.Connect_button.Content = "disconnect";
            this.progress_bar.Visibility = Visibility.Visible;
            this.Message_label.Content = msg;
        }

    }
}
