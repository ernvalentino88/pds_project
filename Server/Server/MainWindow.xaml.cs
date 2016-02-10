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
        public static int receive_timeout = 1000 * 10;
        public static int send_timoute = 1000 * 10;

        public delegate void start_server_delegate(String port);
        public delegate void update_ui_delegate(String msg);
        public delegate Task UpdateDelegateAsync(String msg, String bannerTitle, String bannerMsg);
        private TcpListener myList;
        private Boolean connected;
        AESUtility aes;
        
       
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
                        s.ReceiveTimeout = receive_timeout;
                        s.SendTimeout =send_timoute;
                        msg = "Connection accpeted from " + s.RemoteEndPoint;
                        this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new update_ui_delegate(updateUI_msg), msg);
                        //connection done
                        byte[] buffer_command = new byte[4];
                        int b = s.Receive(buffer_command);
                        if (b != 4) { throw new System.Exception("Wrong command bytes"); }
                        CONNECTION_CODES code = (CONNECTION_CODES) BitConverter.ToUInt32(buffer_command,0);
                        if (code == CONNECTION_CODES.KEY_EXC)
                        {
                            //receive  public key
                            byte[] buffer_modulus = new byte[256];
                            b = s.Receive(buffer_modulus);
                            if (b != 256) { throw new System.Exception("Wrong modulus bytes"); }
                            byte[] buffer_exponent = new byte[4];
                            b = s.Receive(buffer_exponent);
                            if (b<1) { throw new System.Exception("Wrong exponent bytes"); }
                            //adjust exponent size
                            byte[] exponent = new byte[b];
                            if (b != 4){
                                for (int i = 0; i < b; i++){
                                    exponent[i] = buffer_exponent[i];
                                }
                            }
                            else {
                                exponent = buffer_exponent;
                            }
                            //set public key
                            RSAUtility rsa = new RSAUtility();
                            rsa.set_public_key(buffer_modulus,exponent);
                            //encrypt simmetric key and send
                            //TODO change CIAO to pawword for AES
                            ASCIIEncoding asen = new ASCIIEncoding();
                            byte[] sim_key_to_send = rsa.RSAEncrypt(asen.GetBytes("CIAO!"), false);
                            if (sim_key_to_send == null) { throw new System.Exception("Error in ecnryption"); }
                            s.Send(sim_key_to_send);
                            aes = new AESUtility(asen.GetBytes("CIAO!"));//TODO change CIAO to pawword for AES

                            bool exit = false;
                            while (!exit) { exit=get_cmd(s); }
                        }
                        else { throw new Exception("Unexpected command: "+code); }


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

        private bool get_cmd(Socket s) {
            byte[] buffer_command = new byte[4];
            int b = s.Receive(buffer_command);
            if (b != 4) { throw new System.Exception("Wrong command bytes"); }
            CONNECTION_CODES code = (CONNECTION_CODES)BitConverter.ToUInt32(buffer_command, 0);
            switch (code) {
                case CONNECTION_CODES.EXIT: return false ; 
                case CONNECTION_CODES.NEW_REG: register(s); break;
                case CONNECTION_CODES.AUTH: authenticate(s); break;
                

            }

            return true;
        }

        private void register(Socket s) {
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
                s.Send(BitConverter.GetBytes((UInt32)CONNECTION_CODES.ERR));
                throw new Exception("Error during register");   
            }
        }

        private void authenticate(Socket s) {
            byte[] buffer_id = new byte[256];
            int b = s.Receive(buffer_id);
            if (b != 256) { throw new System.Exception("Wrong size"); }
            byte[] id = aes.AES_Decrypt(buffer_id);
            String user = System.Text.Encoding.Default.GetString(id);
            //make RANDOM challange
            String chlg = RandomString(10);
            ASCIIEncoding asen = new ASCIIEncoding();
            byte[] chlg_aes=aes.AES_Encrypt(asen.GetBytes(chlg));
            s.Send(chlg_aes);

            String hash=DBmanager.find_user(user);
            //TODO RECEIVE R & Calcolate R'


        
        }

  

        public static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[random.Next(s.Length)]).ToArray());
        }

    }
}
