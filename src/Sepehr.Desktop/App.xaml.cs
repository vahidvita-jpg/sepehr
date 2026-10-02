using System;
using System.Windows;
using Sepehr.Core;

namespace Sepehr.Desktop
{
    public partial class App : Application
    {
        public static void ShowError(string text)
        {
            MessageBox.Show(text, "سپهر", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK,
                MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            DispatcherUnhandledException += (s, a) =>
            {
                Log.Error("UI", a.Exception);
                ShowError("خطای غیرمنتظره‌ای رخ داد. جزئیات فنی در فایل گزارش ذخیره شد.");
                a.Handled = true;
            };

            try
            {
                AppPaths.EnsureAll();
                Migrator.Run();
            }
            catch (Exception ex)
            {
                Log.Error("Startup", ex);
                ShowError("راه‌اندازی پایگاه داده انجام نشد. گزارش فنی در پوشه‌ی logs ذخیره شد:\n" + AppPaths.Logs);
                Shutdown(1);
                return;
            }

            if (SetupService.IsFirstRun())
            {
                var setup = new SetupWindow();
                if (setup.ShowDialog() != true) { Shutdown(); return; }
            }

            var login = new LoginWindow();
            if (login.ShowDialog() != true) { Shutdown(); return; }

            var shell = new ShellWindow(login.User);
            MainWindow = shell;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            shell.Show();
        }
    }
}
