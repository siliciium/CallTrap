/*
Copyright 2026 Silicium

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0
*/
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;


/*
Service Name: Handsfree Audio Gateway
Service Description: Simulated Hands-Free Phone
Service Provider: PhoneSim
Service RecHandle: 0x10177
Service Class ID List:
  UUID 128: 3b631bd9-158c-4e91-9c45-b4439a4ad4d8
Protocol Descriptor List:
  "L2CAP" (0x0100)
  "RFCOMM" (0x0003)
    Channel: 4
Profile Descriptor List:
  "Handsfree" (0x111e)
    Version: 0x0107
*/

namespace PhoneSim
{
    public class CindIndicator
    {
        public string Name { get; }
        public int Index { get; }
        public int Min { get; }
        public int Max { get; }
        public int Value { get; set; }
        public int Format { get; set; }
        public CindIndicator(string name, int index, int min, int max, int format)
        {
            Name = name;
            Index = index;
            Min = min;
            Max = max;
            Value = min;
            Format = format; // "," or "-"
        }
    }

    public enum Ton : int
    {
        National = 129,              
        International = 145,    
        Network_specific = 161,       
    }


    public partial class MainWindow : Window
    {

        [DllImport("Dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, DWMWINDOWATTRIBUTE attribute, [In] ref bool pvAttribute, int cbAttribute);
        private enum DWMWINDOWATTRIBUTE
        {
            DWMWA_USE_IMMERSIVE_DARK_MODE = 20,
        }

        private static string PTitle = "Bluetooth PhoneSim";

        private RfcommServiceProvider provider_HFAG;
        private StreamSocketListener listener_HFAG;
        private static string local_btaddr;
        private static string local_btport;
        private static string remote_btaddr;

        private static Dictionary<string, CindIndicator> CIND = new Dictionary<string, CindIndicator>()
        {
            { "call",      new CindIndicator("call",      1, 0, 1, 0) },
            { "callsetup", new CindIndicator("callsetup", 2, 0, 3, 1) },
            { "service",   new CindIndicator("service",   3, 0, 1, 1) },
            { "signal",    new CindIndicator("signal",    4, 0, 5, 1) },
            { "roam",      new CindIndicator("roam",      5, 0, 1, 0) },
            { "battchg",   new CindIndicator("battchg",   6, 0, 5, 1) },
            { "callheld",  new CindIndicator("callheld",  7, 0, 2, 1) }
        };
        private static int BRSF = 3943;
        private static bool eventsEnabled = false;
        private static bool clipEnabled = false;
        private static bool answered = false;

        private static BlockingCollection<string> eventQueue = new BlockingCollection<string>();
        private static string AT_OK = "\r\nOK\r\n";
        private static string AT_ERROR = "\r\nERROR\r\n";
        private static Brush brush_Rx;
        private static Brush brush_Tx;

        private static PBAP_service pBAP_Service;
        private static CancellationTokenSource _cts;


        public MainWindow()
        {

            InitializeComponent();

            brush_Rx = new SolidColorBrush(Color.FromRgb(62, 118, 192)); // (51, 98, 160)
            brush_Tx = new SolidColorBrush(Color.FromRgb(250, 253, 0)); // (173, 127, 168),(237, 0, 255)

            IntPtr hWnd = new WindowInteropHelper(this).EnsureHandle();
            bool value = true;
            int result = DwmSetWindowAttribute(
                hWnd,
                DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE,
                ref value,
                Marshal.SizeOf<bool>()
            );

            PbapFileSystem.Init();          
            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                PBAP_service.AddRootPlanToRichTextBox(tb_pbap, PbapFileSystem.RootPlan());

                var adapter = await BluetoothAdapter.GetDefaultAsync().AsTask();

                if (adapter == null)
                {
                    _AppendText(tb_debug, "❌ No Bluetooth adapter found.", Brushes.IndianRed);
                    return;
                }

                pBAP_Service = new PBAP_service(adapter, tb_pbap);
                pBAP_Service.TitleRequested += OnTitleRequested;
                pBAP_Service.brush_Rx = brush_Rx;
                pBAP_Service.brush_Tx = brush_Tx;

                Debug.WriteLine(adapter.BluetoothAddress);
                Debug.WriteLine(adapter.IsClassicSupported);

                // 2. Create RFCOMM serevice (Hands-Free)
                //Guid serviceUuid = new Guid("0000111f-0000-1000-8000-00805f9b34fb"); // Windows block this profile
                Guid serviceUuid = Guid.NewGuid();

                provider_HFAG = await RfcommServiceProvider
                    .CreateAsync(RfcommServiceId.FromUuid(serviceUuid))
                    .AsTask();

                Debug.WriteLine(provider_HFAG);

                // 3.Prepare to listen to RFCOMM
                listener_HFAG = new StreamSocketListener();
                listener_HFAG.ConnectionReceived += OnConnectionReceived;

                await listener_HFAG.BindServiceNameAsync(provider_HFAG.ServiceId.AsString(), SocketProtectionLevel.PlainSocket).AsTask();
                Debug.WriteLine(listener_HFAG.Information.LocalPort);
                setCustomAttributes(provider_HFAG);

                local_btaddr = FormatBluetoothAddress(adapter.BluetoothAddress);
                local_btport = listener_HFAG.Information.LocalPort;                

                Debug.WriteLine(BuildCindSdpString());

                // 4. SDP Publish
                provider_HFAG.StartAdvertising(listener_HFAG, true);

                // Load second PBAP service
                await pBAP_Service.LoadAsync();

                OnTitleRequested();

            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex); ;
                _AppendText(tb_debug, $"❌ Error : {ex.Message}", Brushes.IndianRed);
            }
        }




        private static void AddSdpString(RfcommServiceProvider provider, ushort id, string value)
        {
            var writer = new DataWriter();
            writer.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8;

            writer.WriteByte(0x25); // TextString
            writer.WriteByte((byte)value.Length);
            writer.WriteString(value);

            provider.SdpRawAttributes.Add(id, writer.DetachBuffer());
        }

        private static void AddSdpUInt16(RfcommServiceProvider provider, ushort id, ushort value)
        {
            var writer = new DataWriter();
            writer.WriteByte(0x09); // UInt16
            writer.WriteUInt16(value);

            provider.SdpRawAttributes.Add(id, writer.DetachBuffer());
        }

        private static string BuildCindSdpString()
        {
            var parts = new List<string>();

            foreach (var kv in CIND.OrderBy(k => k.Value.Index))
            {
                var ind = kv.Value;
                var format = ind.Format;
                var f = ",";
                if (format == 1)
                {
                    f = "-";
                }

                parts.Add($"(\"{ind.Name}\",({ind.Min}{f}{ind.Max}))");
            }

            return string.Join(",", parts);
        }

        private static void setCustomAttributes(RfcommServiceProvider provider)
        {
            // --- Full SDP of an HFP phone ---

            // Name of the service
            AddSdpString(provider, 0x0100, "Handsfree Audio Gateway");

            // Description
            AddSdpString(provider, 0x0101, "Simulated Hands-Free Phone");

            // Provider
            AddSdpString(provider, 0x0102, "PhoneSim");

            // Supported Features (BRSF)
            AddSdpUInt16(provider, 0x0311, 0x002F);
            // 0x002F = EC/NR + 3-way calling + CLI + voice recognition + volume control

            // Network (0x01 = Cellular)
            AddSdpUInt16(provider, 0x0301, 0x0001);

            // Supported Indicators (CIND)
            AddSdpString(provider, 0x0302, BuildCindSdpString());

            // Call Hold (CHLD)
            AddSdpString(provider, 0x0303, "0,1,1x,2");

            // Remote Audio Volume Control
            AddSdpUInt16(provider, 0x0304, 1);

            // Profile Descriptor List (HFP 1.7)
            {
                var writer = new DataWriter();
                writer.WriteByte(0x35); // Sequence
                writer.WriteByte(0x08); // Length

                writer.WriteByte(0x35); writer.WriteByte(0x06); // Sequence
                writer.WriteByte(0x19); writer.WriteUInt16(0x111E); // HFP UUID
                writer.WriteByte(0x09); writer.WriteUInt16(0x0107); // Version 1.7

                provider.SdpRawAttributes.Add(0x0009, writer.DetachBuffer());
            }
        }

        private async void OnConnectionReceived(StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs args)
        {
            remote_btaddr = args.Socket.Information.RemoteAddress.RawName.Replace("(","").Replace(")","");

            Debug.WriteLine("Client connected");
            _ClearText(tb_debug);
            OnTitleRequested();


            var socket = args.Socket;

            var reader = new DataReader(socket.InputStream)
            {
                InputStreamOptions = InputStreamOptions.Partial
            };

            var writer = new DataWriter(socket.OutputStream);
            var sb = new StringBuilder();

            while (true)
            {
                try
                {
                    uint loaded = await reader.LoadAsync(64).AsTask(); // read in blocks

                    if (loaded == 0)
                    {
                        Debug.WriteLine("Client disconnected.");
                        remote_btaddr = string.Empty;
                        OnTitleRequested();
                        CleanAfterCall(true, true);
                        break;
                    }

                    string chunk = reader.ReadString(loaded);
                    sb.Append(chunk);

                    while (true)
                    {
                        string buffer = sb.ToString();
                        int idx = buffer.IndexOf('\r');
                        if (idx < 0)
                            break; // no complete command yet

                        string line = buffer.Substring(0, idx);
                        sb.Remove(0, idx + 1); // remove the line + '\r'

                        line = line.Trim();
                        if (line.Length == 0)
                            continue;

                        Debug.WriteLine("> Data RX: " + line);                        
                        _AppendText(tb_debug, $"> Rx : {line}", brush_Rx);

                        var responses = HandleAt(tb_debug, bt_call, line, writer);

                        foreach (var resp in responses)
                        {
                            Debug.WriteLine("< Data TX: " + resp.Trim());
                            _AppendText(tb_debug, $"< Tx : {resp.Trim()}", brush_Tx);

                            writer.WriteString(resp);
                            await writer.StoreAsync().AsTask();
                        }

                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Error socket : " + ex.Message);
                    break;
                }
            }
        }

        private static string AT(string result)
        {
            return $"\r\n{result}\r\n";
        }

        private static List<string> HandleAt(RichTextBox tb_debug, Button bt_call, string line, DataWriter writer)
        {
            var responses = new List<string>();

            // Bluetooth Remote Supported Features
            if (line.StartsWith("AT+BRSF="))
            {
                var val = int.Parse(line.Substring("AT+BRSF=".Length));
                Debug.WriteLine(val);
                //append(tb_debug, val.ToString());

                responses.Add(AT($"+BRSF: {BRSF}"));
                responses.Add(AT_OK);
            }
            // Bluetooth Audio Codecs
            /*else if (line.StartsWith("AT+BAC="))
            {
                var val = line.Substring("AT+BAC=".Length);
                Debug.WriteLine(val);
                append(tb_debug, val.ToString());
                return "\r\nOK\r\n";
            }
            // Call Hold and Multiparty Handling
            else if (line.StartsWith("AT+CHLD=?"))
            {
                // 0 - Reject the call on hold
                // 1 - End the active call and answer the call on hold
                // 2 - Put the active call on hold and answer the other call
                // 3 - Merge the calls (conference)
                return "\r\n+CHLD: (0,1,2,3)\r\nOK\r\n";
            }
            else if (line.StartsWith("AT+CHLD="))
            {
                return "\r\nOK\r\n";
            }
            // List Current Calls
            else if (line.StartsWith("AT+CLCC"))
            {
                // Aucun appel en cours
                return "\r\nOK\r\n";
            }
            // Extended Error Reporting
            else if (line.StartsWith("AT+CMEE=?"))
            {
                return "\r\n+CMEE: (0-2)\r\nOK\r\n";
            }
            else if (line.StartsWith("AT+CMEE="))
            {
                return "\r\nOK\r\n";
            }*/
            // Call Indicator
            else if (line.StartsWith("AT+CIND=?"))
            {
                responses.Add(AT($"+CIND: {BuildCindSdpString()}"));
                responses.Add(AT_OK);
            }
            else if (line.StartsWith("AT+CIND?"))
            {
                responses.Add(AT($"+CIND: 0,0,0,0,0,{Rand_battchg()},0"));
                responses.Add(AT_OK);
            }
            // Call MEr Reporting
            else if (line.StartsWith("AT+CMER="))
            {
                var val = line.Substring("AT+CMER=".Length);
                eventsEnabled = true;
                // 3 - mode: Notifications enabled
                // 0 - keyp: No keypress
                // 0 - disp: No display
                // 1 - ind : Send +CIEV
                responses.Add(AT_OK);

                _cts = new CancellationTokenSource();
                Task.Run(() => EventLoop(tb_debug, writer, _cts.Token));
                Task.Run(() => CIEVLoop(_cts.Token));
            }
            // Calling Line Identification Presentation
            else if (line.StartsWith("AT+CLIP=1"))
            {
                clipEnabled = true;
                responses.Add(AT_OK);
            }
            // Volume Gain Speaker (phone mic)
            else if (line.StartsWith("AT+VGS="))
            {
                responses.Add(AT_OK);
            }
            // Answer
            else if (line.StartsWith("ATA"))
            {
                answered = true;
                responses.Add(AT_OK);

                _AppendText(tb_debug, "== ANSWER ==", Brushes.DarkSlateGray);
                SetBtnCall_ON(bt_call);

                SendCiev("call", 1);
                SendCiev("callsetup", 0);

            }
            // Call Hang UP
            else if (line.StartsWith("AT+CHUP"))
            {
                responses.Add(AT_OK);
                CleanAfterCall();

                _AppendText(tb_debug, "== HANGUP ==", Brushes.DarkSlateGray);
                SetBtnCall_OFF(bt_call);
            }
            // ..). UnImplemented
            else
            {
                // AT*ECAM=1...
                // AT+CMGF=1...
                responses.Add(AT_ERROR);
            }

            return responses;

        }

        private static async Task EventLoop(RichTextBox tb_debug, DataWriter writer, CancellationToken token)
        {
            Random rnd = new Random();

            while (!token.IsCancellationRequested)
            {
                if (eventQueue.TryTake(out var evt))
                {
                    _AppendText(tb_debug, $"< Tx : {evt}", brush_Tx);
                    writer.WriteString(AT(evt));
                    await writer.StoreAsync().AsTask();
                }

                await Task.Delay(20);
            }
        }

        private static async Task CIEVLoop(CancellationToken token)
        {
            Random rnd = new Random();

            while (!token.IsCancellationRequested)
            {
                if (eventsEnabled)
                {
                    /*int[] values = { 4, 6 };
                    int i = values[new Random().Next(values.Length)];

                    if (i == 4)
                    {*/
                    // Example: simulating a varying signal
                    SendCiev("signal", rnd.Next(1, 6));
                    //}

                    /*if (i == 6)
                    {
                        // Example : simulate battery level
                        SendCiev("battchg", rnd.Next(1, 6));
                    }*/
                }

                await Task.Delay(rnd.Next(5000, 20001)); // 5 to 20 seconds
            }
        }


        private static void SendCiev(string name, int value)
        {
            if (!eventsEnabled) return; // activate after AT+CMER

            if (CIND.TryGetValue(name, out var ind))
            {
                ind.Value = Clamp(value, ind.Min, ind.Max);
                SendEvent($"+CIEV: {ind.Index},{ind.Value}");
            }
        }

        private static void SendEvent(string at)
        {
            if (eventsEnabled)
            {
                eventQueue.Add(at);
            }
        }

        private static int Rand_battchg()
        {
            Random rnd = new Random();
            return rnd.Next(1, 6);
        }

        private static int Rand_signal()
        {
            Random rnd = new Random();
            return rnd.Next(1, 6);
        }

        private static int Clamp(int v, int min, int max) => v < min ? min : (v > max ? max : v);

        private void OnTitleRequested()
        {
            string pbap_cli = String.Empty;
            if (!string.IsNullOrEmpty(pBAP_Service.remote_btaddr)) {
                pbap_cli = $" [ {pBAP_Service.remote_btaddr} ]";
            }

            string hfag_cli = String.Empty;
            if (!string.IsNullOrEmpty(remote_btaddr))
            {
                hfag_cli = $" [ {remote_btaddr} ]";
            }

            update_title(this, $"{PTitle} - {local_btaddr} - HFP port {local_btport}{hfag_cli} - PBAP port {pBAP_Service.local_btport}{pbap_cli}");

        }

        private static void update_title(Window win, string title)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                win.Title = $"{title}";                               
            });
        }

        private static void _ClearText(RichTextBox rtb)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                rtb.Document.Blocks.Clear();
            });
        }

        public static void _AppendText(RichTextBox rtb, string text, Brush brush)
        {

            Application.Current.Dispatcher.Invoke(() =>
            {
                Paragraph p = new Paragraph
                {
                    Margin = new Thickness(0),
                    LineHeight = double.NaN,
                    TextAlignment = TextAlignment.Left
                };
                Run r = new Run(text)
                {
                    Foreground = brush
                };

                p.Inlines.Add(r);
                rtb.Document.Blocks.Add(p);

                rtb.ScrollToEnd();

            });
        }

        private static string FormatBluetoothAddress(ulong address)
        {
            return string.Join(":",
                Enumerable.Range(0, 6)
                    .Select(i => ((address >> (8 * (5 - i))) & 0xFF).ToString("X2"))
            );
        }




        private static void SetBtnCall_ON(Button bt_call)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                //bt_call.Background = (Brush)new BrushConverter().ConvertFromString("#FF10C972");
                bt_call.Foreground = (Brush)new BrushConverter().ConvertFromString("#FFFF0000");
                bt_call.Content = "📞HANGUP";
            });
        }
        private static void SetBtnCall_OFF(Button bt_call)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                //bt_call.Background = (Brush)new BrushConverter().ConvertFromString("#FFC9AA10");
                bt_call.Foreground = (Brush)new BrushConverter().ConvertFromString("#FFFAFD00");
                bt_call.Content = "📞CALL";
            });
        }

        private static void CleanAfterCall(bool restore_eventsEnabled=false, bool restore_clipEnabled=false)
        {
            if (restore_eventsEnabled)
            {
                if (eventsEnabled)
                {
                    _cts.Cancel();
                    _cts.Dispose();
                    _cts = null;

                    eventsEnabled = false;
                }
            }

            answered = false;

            if (restore_clipEnabled)
            {
                clipEnabled = false;
            }                        
        }

        private static void NewCall(Button bt_call, string phone_number, int TON)
        {
            if (!answered)
            {
                SendCiev("callsetup", 1);
                SendEvent("RING");
                SendEvent($"+CLIP: \"{phone_number}\",{TON},,,\"\"");
            }
            else
            {
                SendCiev("call", 0);
                CleanAfterCall();
                SetBtnCall_OFF(bt_call);
            }
        }

        public static bool IsValidPhoneFlexible(string input)
        {
            /*
                - National numbers(0XXXXXXXXX)
                - International numbers(+XX…)
                - Numbers sent via modem(TON 129 / 145)
            */

            if (string.IsNullOrWhiteSpace(input))
                return false;

            string pattern = @"^(0\d{9}|\+[1-9]\d{7,14})$";
            return Regex.IsMatch(input.Trim(), pattern);
        }

        private void PhoneTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var tb = (TextBox)sender;

            // Prevent two +
            if (e.Text == "+" && tb.Text.Contains("+"))
            {
                e.Handled = true;
                return;
            }

            // Allow digit and +
            e.Handled = !Regex.IsMatch(e.Text, @"^[0-9+]$");
        }

        private void PhoneTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                string text = (string)e.DataObject.GetData(typeof(string));

                // Allow digits and +
                if (!Regex.IsMatch(text, @"^[0-9+]+$"))
                    e.CancelCommand();
            }
            else
            {
                e.CancelCommand();
            }
        }

        private void bt_call_Click(object sender, RoutedEventArgs e)
        {

            Debug.WriteLine($"eventsEnabled: {eventsEnabled}");
            Debug.WriteLine($"answered: {answered}");
            Debug.WriteLine($"clipEnabled: {clipEnabled}");            


            if (clipEnabled)
            {
                string phone_number = tb_num.Text;

                bool ok = IsValidPhoneFlexible(phone_number);

                if (ok)
                {
                    if (phone_number.Length > 0)
                    {
                        if (phone_number.StartsWith("+"))
                        {
                            if (phone_number.Length == 12)
                            {
                                NewCall(bt_call, phone_number, (int)Ton.International);
                            }
                        }
                        else
                        {
                            if (phone_number.Length == 10)
                            {
                                NewCall(bt_call, phone_number, (int)Ton.National);
                            }
                        }
                    }
                }
                else
                {
                    DarkMessageBox.Show(PTitle, "⚠ Invalid phone number format.");
                }
            }
            else
            {
                DarkMessageBox.Show(PTitle, "⚠ Not ready : no client connected.");
            }
        }
    }
}
