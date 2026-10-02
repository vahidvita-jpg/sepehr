using System;
using System.Windows;
using Sepehr.Core;

namespace Sepehr.Desktop
{
    public partial class SetupWindow : Window
    {
        public SetupWindow()
        {
            InitializeComponent();
            YearBox.Text = SetupService.SuggestAcademicYear(DateTime.Now);
        }

        void Create_Click(object sender, RoutedEventArgs e)
        {
            ErrorText.Text = "";
            if (PassBox.Password != Pass2Box.Password) { ErrorText.Text = "رمز عبور و تکرار آن یکسان نیست."; return; }
            try
            {
                var err = SetupService.Create(SchoolBox.Text, PrincipalBox.Text, YearBox.Text, UserBox.Text, PassBox.Password, DisplayBox.Text);
                if (err != null) { ErrorText.Text = err; return; }
                DialogResult = true;
            }
            catch (Exception ex)
            {
                Log.Error("Setup", ex);
                ErrorText.Text = "ذخیره‌ی اطلاعات انجام نشد. جزئیات در فایل گزارش ذخیره شد.";
            }
        }
    }
}
