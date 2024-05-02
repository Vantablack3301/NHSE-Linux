using System;
using Gtk;

namespace NHSE.WinForms
{
    /// <summary>
    /// Simple launcher for opening a save file.
    /// </summary>
    public partial class Main : Window
    {
        public const string BackupFolderName = "bak";
        public const string ItemFolderName = "items";
        public static readonly string WorkingDirectory = Application.StartupPath;
        public static readonly string BackupPath = System.IO.Path.Combine(WorkingDirectory, BackupFolderName);
        public static readonly string ItemPath = System.IO.Path.Combine(WorkingDirectory, ItemFolderName);

        private VBox vbox = new VBox(false, 2);
        private MenuBar menubar = new MenuBar();
        private MenuItem fileItem = new MenuItem("File");
        private MenuItem openItem = new MenuItem("Open");
        private MenuItem exitItem = new MenuItem("Exit");

        public Main() : base("NHSE")
        {
            SetDefaultSize(250, 200);
            SetPosition(WindowPosition.Center);
            DeleteEvent += delegate { Application.Quit(); };

            var fileMenu = new Menu();
            fileItem.Submenu = fileMenu;
            
            openItem.Activated += Open_Click;
            fileMenu.Append(openItem);
            
            exitItem.Activated += delegate { Application.Quit(); };
            fileMenu.Append(exitItem);
            
            menubar.Append(fileItem);
            vbox.PackStart(menubar, false, false, 0);
            Add(vbox);
            
            ShowAll();
        }

        private static void Open(HorizonSave file)
        {
            bool sized = file.ValidateSizes();
            if (!sized)
            {
                var prompt = WinFormsUtil.Prompt(MessageBoxButtons.YesNo, MessageStrings.MsgSaveDataSizeMismatch, MessageStrings.MsgAskContinue);
                if (prompt != DialogResult.Yes)
                    return;
            }

            new Editor(file).Show();
        }

        private void Open_Click(object sender, EventArgs e)
        {
            using var ofd = new FileChooserDialog(
                "Choose a file to open",
                this,
                FileChooserAction.Open,
                "Cancel", ResponseType.Cancel,
                "Open", ResponseType.Accept);

            if (ofd.Run() == (int)ResponseType.Accept)
            {
                Open(ofd.Filename);
            }
            ofd.Destroy();
        }

        private static void Open(string path)
        {
            #if !DEBUG
            try
            #endif
            {
                OpenFileOrPath(path);
            }
            #if !DEBUG
            catch (Exception ex)
            {
                WinFormsUtil.Error(ex.Message);
            }
            #endif
        }

        private static void OpenFileOrPath(string path)
        {
            if (System.IO.Directory.Exists(path))
            {
                OpenSaveFile(path);
                return;
            }

            var dir = System.IO.Path.GetDirectoryName(path);
            if (dir is null || !System.IO.Directory.Exists(dir)) // ya never know
            {
                WinFormsUtil.Error(MessageStrings.MsgSaveDataImportFail, MessageStrings.MsgSaveDataImportSuggest);
                return;
            }

            OpenSaveFile(dir);
        }

        private static void OpenSaveFile(string path)
        {
            var file = new HorizonSave(path);
            Open(file);

            var settings = Settings.Default;
            settings.LastFilePath = path;

            if (!settings.BackupPrompted)
            {
                settings.BackupPrompted = true;
                var line1 = string.Format(MessageStrings.MsgBackupCreateLocation, BackupFolderName);
                var line2 = MessageStrings.MsgBackupCreateQuestion;
                var prompt = WinFormsUtil.Prompt(MessageBoxButtons.YesNo, line1, line2);
                settings.AutomaticBackup = prompt == DialogResult.Yes;
            }

            if (settings.AutomaticBackup)
                BackupSaveFile(file, path, BackupPath);

            settings.Save();
        }

        private static void BackupSaveFile(HorizonSave file, string path, string bak)
        {
            System.IO.Directory.CreateDirectory(bak);
            var dest = System.IO.Path.Combine(bak, file.GetBackupFolderTitle());
            if (!System.IO.Directory.Exists(dest))
                FileUtil.CopyFolder(path, dest);
        }
    }
}
