using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
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
using System.IO;
using System.IO.Ports;

namespace MMTSerial
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            string[] handshakes = Enum.GetNames(typeof(Handshake));
            cbHand.ItemsSource = handshakes;
            string[] stopbits = Enum.GetNames(typeof(StopBits));
            cbStop.ItemsSource = stopbits;
            string[] parities = Enum.GetNames(typeof(Parity));
            cbParity.ItemsSource = parities;
        }

        delegate void UpdateTextDelegate(string text);

        void UpdateTotalBytesToSend(string text)
        {
            tbTotalBytesToSend.Text = text;
        }

        void UpdateTotalBytesSent(string text)
        {
            tbTotalBytesSent.Text = text;
        }

        void UpdateBytesToWrite(string text)
        {
            tbBytesToWrite.Text = text;
        }

        void UpdateBytesToRead(string text)
        {
            tbBytesToRead.Text = text;
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofDlg = new OpenFileDialog();
            if (ofDlg.ShowDialog() == System.Windows.Forms.DialogResult.OK  )
            {
                filePathTextBox.Text = ofDlg.FileName;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void SendFileButton_Click(object sender, RoutedEventArgs e)
        {
            string sPort = cbPort.Text;
            if (sPort.Length<4)
            {
                System.Windows.MessageBox.Show("The entered portname must be at least 4 characters long.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SerialPort sp1 = new SerialPort(sPort);
            sp1.BaudRate = int.Parse(cbBaud.Text);
            sp1.DataBits = int.Parse(cbData.Text);
            sp1.Parity = (Parity)Enum.Parse(typeof(Parity), cbParity.Text, true);
            sp1.StopBits = (StopBits)Enum.Parse(typeof(StopBits), cbStop.Text, true);
            sp1.Handshake = (Handshake)Enum.Parse(typeof(Handshake), cbHand.Text, true);

            Thread sendThread = new Thread(new ParameterizedThreadStart(SendFileSerial));
            sendThread.Start(new object[] { sp1, filePathTextBox.Text, int.Parse(tbDelay.Text) });
        }

        void SendFileSerial(object parameters)
        {
            object[] args = (object[])parameters;
            SerialPort sPort = (SerialPort)args[0];
            string sFileName = (string)args[1];
            int iDelay = (int)args[2];

            if (iDelay<0)
            {
                iDelay = 0;
            }

            byte[] bytesToSend = File.ReadAllBytes(sFileName);
            
            Dispatcher.Invoke(new UpdateTextDelegate(UpdateTotalBytesToSend), new object[] { bytesToSend.Length.ToString() });

            sPort.Open();
            for (int i = 0; i < bytesToSend.Length; i++)
            {
                // Write one character to the serial port
                sPort.Write(bytesToSend, i, 1);
                Dispatcher.BeginInvoke(new UpdateTextDelegate(UpdateTotalBytesSent), new object[] { (i+1).ToString() });
                Dispatcher.BeginInvoke(new UpdateTextDelegate(UpdateBytesToWrite), new object[] { sPort.BytesToWrite.ToString() });
                Dispatcher.BeginInvoke(new UpdateTextDelegate(UpdateBytesToRead), new object[] { sPort.BytesToRead.ToString() });  // wait for the delay

                Thread.Sleep(iDelay);
            }
            sPort.Close();
        }

        private void cbPort_DropDownOpened(object sender, EventArgs e)
        {
            string[] ports = SerialPort.GetPortNames();
            cbPort.ItemsSource = ports;
        }
    }
}
