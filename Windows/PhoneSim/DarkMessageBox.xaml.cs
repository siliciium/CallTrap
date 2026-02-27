using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace PhoneSim
{

    public partial class DarkMessageBox : Window
    {
        [DllImport("Dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, DWMWINDOWATTRIBUTE attribute, [In] ref bool pvAttribute, int cbAttribute);
        private enum DWMWINDOWATTRIBUTE
        {
            DWMWA_USE_IMMERSIVE_DARK_MODE = 20,
        }
        public DarkMessageBox(string title, string message)
        {
            InitializeComponent();

            IntPtr hWnd = new WindowInteropHelper(this).EnsureHandle();
            bool value = true;
            int result = DwmSetWindowAttribute(
                hWnd,
                DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE,
                ref value,
                Marshal.SizeOf<bool>()
            );

            TextBlockMessage.Text = message;
            this.Title = title;
        }

        public static void Show(string title, string message)
        {
            if(message.Length < 30)
            {
                message = message.PadRight(30, ' ');
            }
            new DarkMessageBox(title, message).ShowDialog();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
