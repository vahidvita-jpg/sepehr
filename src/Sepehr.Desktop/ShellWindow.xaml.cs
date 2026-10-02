using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;
using Sepehr.Core;

namespace Sepehr.Desktop
{
    public partial class ShellWindow : Window
    {
        readonly AuthResult _user;
        List<BellRow> _bells = new List<BellRow>();
        readonly DispatcherTimer _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };

        public ShellWindow(AuthResult user)
        {
            InitializeComponent();
            _user = user;
            UserText.Text = "کاربر: " + user.DisplayName;
            try
            {
                SchoolNameText.Text = AuthService.SchoolName();
                _bells = BellEngine.Load();
            }
            catch (Exception ex) { Log.Error("Shell", ex); App.ShowError("بارگذاری اطلاعات انجام نشد. جزئیات در فایل گزارش ذخیره شد."); }
            _timer.Tick += (s, e) => Tick();
            _timer.Start();
            Tick();
        }

        static string P(string s) { return PersianDate.ToPersianDigits(s); }
        static string Hm(TimeSpan t) { return P(t.ToString(@"hh\:mm")); }

        void Tick()
        {
            var now = DateTime.Now;
            WeekdayText.Text = PersianDate.WeekdayName(now);
            PersianDateText.Text = P(PersianDate.LongFormat(now));
            GregorianText.Text = now.ToString("yyyy-MM-dd");
            ClockText.Text = now.ToString("HH:mm:ss");

            var st = BellEngine.Compute(_bells, now.TimeOfDay);
            switch (st.Phase)
            {
                case "running":
                    CurrentTitle.Text = st.Current.Label;
                    CurrentDetail.Text = "از " + Hm(st.Current.Start) + " تا " + Hm(st.Current.End);
                    RemainingText.Text = "زمان باقی‌مانده: " + P(((int)st.Remaining.TotalMinutes).ToString("00") + ":" + st.Remaining.Seconds.ToString("00"));
                    break;
                case "before": CurrentTitle.Text = "هنوز مدرسه شروع نشده است"; CurrentDetail.Text = ""; RemainingText.Text = ""; break;
                case "after": CurrentTitle.Text = "زمان مدرسه تمام شده است"; CurrentDetail.Text = ""; RemainingText.Text = ""; break;
                case "gap": CurrentTitle.Text = "بین دو زنگ"; CurrentDetail.Text = ""; RemainingText.Text = ""; break;
                default: CurrentTitle.Text = "زنگی تعریف نشده است"; CurrentDetail.Text = ""; RemainingText.Text = ""; break;
            }
            if (st.NextClass != null)
            {
                NextTitle.Text = st.NextClass.Label;
                NextDetail.Text = "شروع: " + Hm(st.NextClass.Start);
            }
            else { NextTitle.Text = "—"; NextDetail.Text = "کلاس دیگری باقی نمانده است"; }
        }

        void ShowPanel(bool teachers)
        {
            DashboardPanel.Visibility = teachers ? Visibility.Collapsed : Visibility.Visible;
            TeachersPanel.Visibility = teachers ? Visibility.Visible : Visibility.Collapsed;
        }

        void NavDashboard_Click(object sender, RoutedEventArgs e) { ShowPanel(false); }

        void NavTeachers_Click(object sender, RoutedEventArgs e)
        {
            ShowPanel(true);
            LoadTeachers();
        }

        void LoadTeachers()
        {
            try { TeachersGrid.ItemsSource = TeacherService.List(); }
            catch (Exception ex) { Log.Error("Teachers", ex); App.ShowError("بارگذاری لیست معلمان انجام نشد."); }
        }

        void AddTeacher_Click(object sender, RoutedEventArgs e)
        {
            TError.Text = "";
            try
            {
                var err = TeacherService.Add(_user, TFirst.Text, TLast.Text, TNational.Text, TEmployee.Text, TMobile.Text);
                if (err != null) { TError.Text = err; return; }
                TFirst.Clear(); TLast.Clear(); TNational.Clear(); TEmployee.Clear(); TMobile.Clear();
                LoadTeachers();
            }
            catch (Exception ex)
            {
                Log.Error("Teachers", ex);
                TError.Text = "ثبت انجام نشد. جزئیات در فایل گزارش ذخیره شد.";
            }
        }
    }
}
