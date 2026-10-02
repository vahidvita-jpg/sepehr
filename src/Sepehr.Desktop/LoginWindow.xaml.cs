using System;
using System.Windows;
using Sepehr.Core;

namespace Sepehr.Desktop
{
    public partial class LoginWindow : Window
    {
        public AuthResult User { get; private set; }

        public LoginWindow()
        {
            InitializeComponent();
            try { SchoolText.Text = AuthService.SchoolName(); } catch (Exception ex) { Log.Error("Login", ex); }
            Loaded += (s, e) => UserBox.Focus();
        }

        void Login_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var r = AuthService.Login(UserBox.Text, PassBox.Password);
                if (!r.Success) { ErrorText.Text = r.Error; PassBox.Clear(); return; }
                User = r;
                DialogResult = true;
            }
            catch (Exception ex)
            {
                Log.Error("Login", ex);
                ErrorText.Text = "ورود انجام نشد. جزئیات در فایل گزارش ذخیره شد.";
            }
        }
    }
}
