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
using System.ComponentModel;

namespace MMTSerial
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private CancellationTokenSource cts;
        private bool bFileNameOk, bSettingsChanged, bAppSetupDone;
        private enum Baudrate { b1200=1200, b2400=2400, b4800=4800, b9600=9600, b19200=19200 };   // limit possible baud rates to these
        private enum Databits { d7 = 7, d8 = 8 };                                   // limit number of databits to these values

        public MainWindow()
        {
            // first setup all fields --> this should not cause "changed" events to react
            bAppSetupDone = false;
            InitializeComponent();

            cbPort.ItemsSource = SerialPort.GetPortNames();
            string[] handshakes = Enum.GetNames(typeof(Handshake));
            cbHand.ItemsSource = handshakes;
            string[] stopbits = Enum.GetNames(typeof(StopBits));
            cbStop.ItemsSource = stopbits;
            string[] parities = Enum.GetNames(typeof(Parity));
            cbParity.ItemsSource = parities;
            string[] baudrates = Array.ConvertAll((int[])Enum.GetValues(typeof(Baudrate)), x=> x.ToString());
            cbBaud.ItemsSource = baudrates;
            string[] databits = Array.ConvertAll((int[])Enum.GetValues(typeof(Databits)), x => x.ToString());
            cbData.ItemsSource = databits;

            // starting values for buttons
            bFileNameOk = false;
            SetBtnSendFileEnabled();
            SetBtnCancelEnabled(false);
            SetBtnExitEnabled(true);

            // read default settings
            string s = Properties.Settings.Default.Port;
            if (s.Length >= 4)
            {
                cbPort.SelectedValue = s;
            }
            if (Properties.Settings.Default.Baudrate != 0)
            {
                cbBaud.SelectedValue = Properties.Settings.Default.Baudrate.ToString();
            }
            if (Properties.Settings.Default.Databits != 0)
            {
                cbData.SelectedValue = Properties.Settings.Default.Databits.ToString();
            }
            cbParity.SelectedValue = Properties.Settings.Default.Parity.ToString();
            cbStop.SelectedValue = Properties.Settings.Default.Stopbits.ToString();
            cbHand.SelectedValue = Properties.Settings.Default.Handshake.ToString();
            tbDelay.Text = Properties.Settings.Default.DelayChars.ToString();
            tbChars.Text = Properties.Settings.Default.NumberChars.ToString();

            // set status
            tbStatus.Content = "Idle (ready)";
            bAppSetupDone = true;
        }

        delegate void UpdateTextDelegate(string text);
        delegate void UpdateBoolDelegate(bool b);

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

        void UpdateStatus(string text)
        {
            tbStatus.Content = text;
        }

        void SetBtnCancelEnabled(bool b)
        {
            BtnCancel.IsEnabled = b;
        }

        void SetBtnExitEnabled(bool b)
        {
            BtnExit.IsEnabled = b;
        }

        void SetBtnSendFileEnabled()
        {
            if (BtnSendFile != null)
            {
                BtnSendFile.IsEnabled = bFileNameOk && (cbPort.SelectedItem != null) && (cbBaud.SelectedItem != null)
                                            && (cbData.SelectedItem != null) && (cbHand.SelectedItem != null)
                                            && (cbStop.SelectedItem != null) && (cbParity.SelectedItem != null);
            }
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            bFileNameOk = false;
            OpenFileDialog ofDlg = new OpenFileDialog();
            if (ofDlg.ShowDialog() == System.Windows.Forms.DialogResult.OK  )
            {
                filePathTextBox.Text = ofDlg.FileName;
                bFileNameOk = true;
            }
            SetBtnSendFileEnabled();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            cts.Cancel();
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }

        private void SendFileButton_Click(object sender, RoutedEventArgs e)
        {
            string sPort = cbPort.Text;
            // set status
            tbStatus.Content = "Checking parameters...";
            System.Windows.Forms.Application.DoEvents();

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

            int iDelay;
            if (!int.TryParse(tbDelay.Text, out iDelay))
            {
                iDelay = 0;
            }

            int iChars;
            if (!int.TryParse(tbChars.Text, out iChars))
            {
                iChars = 1;
            }

            // disable application exit because a thread will start
            SetBtnExitEnabled(false);
            if (bSettingsChanged)
            {
                Properties.Settings.Default.Save();
                bSettingsChanged = false;
            }

            // set status
            tbStatus.Content = "Starting thread...";
            System.Windows.Forms.Application.DoEvents();

            cts = new CancellationTokenSource();
            Thread sendThread = new Thread(new ParameterizedThreadStart(SendFileSerial));
            sendThread.Start(new object[] { sp1, filePathTextBox.Text, iDelay, iChars, cts.Token });
        }

        void SendFileSerial(object parameters)
        {
            object[] args = (object[])parameters;
            SerialPort sPort = (SerialPort)args[0];
            string sFileName = (string)args[1];
            int iDelay = (int)args[2];
            int iChars = (int)args[3];
            CancellationToken cToken = (CancellationToken)args[4];
            bool bCancelled = false;

            if (iDelay<0)
            {
                iDelay = 0;
            }

            Dispatcher.Invoke(new UpdateTextDelegate(UpdateStatus), new object[] { "Reading file to send..." });
            byte[] bytesToSend = File.ReadAllBytes(sFileName);
            Dispatcher.Invoke(new UpdateTextDelegate(UpdateTotalBytesToSend), new object[] { bytesToSend.Length.ToString() });
            Dispatcher.Invoke(new UpdateBoolDelegate(SetBtnCancelEnabled), new object[] { true });

            Dispatcher.Invoke(new UpdateTextDelegate(UpdateStatus), new object[] { "File read, opening port..." });
            sPort.Open();

            Dispatcher.Invoke(new UpdateTextDelegate(UpdateStatus), new object[] { "Sending data..." });
            for (int i = 0; i < bytesToSend.Length;)
            {
                if (cToken.IsCancellationRequested)
                {
                    bCancelled = true;
                    break;  // user wants to cancel the transmit
                }
                // Write at most iChars characters to the serial port at once
                // make sure we don't send too much when reaching the end of the file
                int iCharstoSend = Math.Min(iChars, bytesToSend.Length - i);
                sPort.Write(bytesToSend, i, iCharstoSend);

                // update pointer
                i += iCharstoSend;

                // update controls in Main Window
                Dispatcher.BeginInvoke(new UpdateTextDelegate(UpdateTotalBytesSent), new object[] { i.ToString() });
                Dispatcher.BeginInvoke(new UpdateTextDelegate(UpdateBytesToWrite), new object[] { sPort.BytesToWrite.ToString() });
                Dispatcher.BeginInvoke(new UpdateTextDelegate(UpdateBytesToRead), new object[] { sPort.BytesToRead.ToString() });
                
                // wait for the delay
                Thread.Sleep(iDelay);
            }
            Dispatcher.Invoke(new UpdateTextDelegate(UpdateStatus), new object[] { "Closing port..." });
            sPort.Close();
            Dispatcher.Invoke(new UpdateBoolDelegate(SetBtnCancelEnabled), new object[] { false });
            cts.Dispose();
            Dispatcher.Invoke(new UpdateBoolDelegate(SetBtnExitEnabled), new object[] { true });
            Dispatcher.Invoke(new UpdateTextDelegate(UpdateStatus), (bCancelled)? new object[] { "File transmission aborted (idle)" } : new object[] { "File transmitted (idle)" });
        }

        private void cbPort_DropDownOpened(object sender, EventArgs e)
        {
            string[] ports = SerialPort.GetPortNames();
            cbPort.ItemsSource = ports;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = !BtnExit.IsEnabled;
        }

        private void cbPort_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SetBtnSendFileEnabled();
            if (bAppSetupDone)
            {
                Properties.Settings.Default.Port = (string)(cbPort.SelectedItem);
                bSettingsChanged = true;
            }
        }

        private void cbBaud_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SetBtnSendFileEnabled();
            if (bAppSetupDone)
            {
                Properties.Settings.Default.Baudrate = int.Parse((string)(cbBaud.SelectedItem));
                bSettingsChanged = true;

            }
        }

        private void cbData_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SetBtnSendFileEnabled();
            if (bAppSetupDone)
            {
                Properties.Settings.Default.Databits = int.Parse((string)cbData.SelectedItem);
                bSettingsChanged = true;
            }
        }

        private void cbParity_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SetBtnSendFileEnabled();
            if (bAppSetupDone)
            {
                Properties.Settings.Default.Parity = (string)cbParity.SelectedItem;
                bSettingsChanged = true; 
            }
        }

        private void tbDelay_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (bAppSetupDone)
            {
                int iDelay;
                if (!int.TryParse(tbDelay.Text, out iDelay))
                {
                    iDelay = 0;
                }
                if (iDelay != Properties.Settings.Default.DelayChars)
                {
                    Properties.Settings.Default.DelayChars = iDelay;
                    bSettingsChanged = true;
                } 
            }
        }

        private void tbChars_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (bAppSetupDone)
            {
                int iChars;
                if (!int.TryParse(tbChars.Text, out iChars))
                {
                    iChars = 0;
                }
                if (iChars != Properties.Settings.Default.NumberChars)
                {
                    Properties.Settings.Default.NumberChars = iChars;
                    bSettingsChanged = true;
                } 
            }
        }

        private void cbStop_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SetBtnSendFileEnabled();
            if (bAppSetupDone)
            {
                Properties.Settings.Default.Stopbits = (string)cbStop.SelectedItem;
                bSettingsChanged = true; 
            }
        }

        private void cbHand_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SetBtnSendFileEnabled();
            if (bAppSetupDone)
            {
                Properties.Settings.Default.Handshake = (string)cbHand.SelectedItem;
                bSettingsChanged = true; 
            }
        }
    }
}
