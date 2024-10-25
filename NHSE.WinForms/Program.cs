using System;
using Eto.Forms;

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
            new Application(Eto.Platform.Detect).Run(new MainForm());
        }
    }
}
