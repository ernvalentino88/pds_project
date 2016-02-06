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

        public MainWindow()
        {
            InitializeComponent();
            
        }

        private void Connect_button_Click(object sender, RoutedEventArgs e)
        {
            ConnectDelegate cd = new ConnectDelegate(connect);
            cd.BeginInvoke(this.Text_ip.Text, this.Text_port.Text,null, null);
        }

        private void connect(String address, String port)
        {
            TcpClient tcpclnt = new TcpClient();
            try
            {
                tcpclnt.Connect(address,Int32.Parse(port));
                String msg = "Connection estabilished with: " + this.Text_ip.Text + ":" + this.Text_port.Text;
                this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new UpdateDelegate(update),msg);
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
                String msg = "Error while trying to connect: " + se.Message;
                //this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new UpdateDelegate(update), msg);
            }
            catch (Exception exc)
            {
                String msg = "Error while trying to connect: " + exc.Message;
                //this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new UpdateDelegate(update), msg);
            }
            finally
            {
                tcpclnt.Close();
            }
        }

        public void update(String msg)
        {  
            this.Text_ip.Visibility = Visibility.Collapsed;
            this.Text_port.Visibility = Visibility.Collapsed;
            this.Message_label.Content = msg;
            this.Message_label.Visibility = Visibility.Visible;
            this.Connect_button.Content = "Disconnect";
        }



    }
}
