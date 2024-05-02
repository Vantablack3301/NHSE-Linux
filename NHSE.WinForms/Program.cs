using System;
using Gtk;

namespace NHSE.WinForms
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        private static void Main()
        {
            Application.Init();
            var app = new Application("org.NHSE.WinForms.NHSE", GLib.ApplicationFlags.None);
            app.Register(GLib.Cancellable.Current);
            var win = new MainWindow();
            app.AddWindow(win);
            win.Show();
            Application.Run();
        }
    }
}
