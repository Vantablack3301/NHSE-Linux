using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Eto.Forms;
using NHSE.Core;
using NHSE.Injection;

namespace NHSE.WinForms
{
    public partial class PlayerItemEditor : Form
    {
        private readonly Action LoadItems;
        private readonly ItemGridEditor ItemGrid;
        private readonly ItemArrayEditor<Item> ItemArray;

        public PlayerItemEditor(IReadOnlyList<Item> array, int width, int height, bool sysbot = false)
        {
            InitializeComponent();
            this.TranslateInterface(GameInfo.CurrentLanguage);
            ItemArray = new ItemArrayEditor<Item>(array);

            var Editor = ItemGrid = new ItemGridEditor(ItemEditor, array) {Dock = DockStyle.Fill};
            Editor.InitializeGrid(width, height, 64, 64);
            PAN_Items.Content = Editor;

            ItemEditor.Initialize(GameInfo.Strings.ItemDataSource);
            Editor.LoadItems();
            DialogResult = DialogResult.Cancel;
            LoadItems = () => Editor.LoadItems();
            B_Inject.Visible = sysbot;

            EnableDragDrop(this, ItemEditor_DragEnter, PlayerItemEditor_DragDrop);
            EnableDragDrop(PAN_Items, ItemEditor_DragEnter, PlayerItemEditor_DragDrop);
            EnableDragDrop(ItemEditor, ItemEditor_DragEnter, PlayerItemEditor_DragDrop);
        }

        private void B_Cancel_Click(object sender, EventArgs e) => Close();

        private void B_Save_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Ok;
            Close();
        }

        private void B_Dump_Click(object sender, EventArgs e)
        {
            var sfd = new SaveFileDialog
            {
                Title = "Save Inventory",
                Filters = { new FileDialogFilter("New Horizons Inventory", ".nhi"), new FileDialogFilter("All files", ".*") },
                FileName = "items.nhi",
            };
            if (sfd.ShowDialog(this) != DialogResult.Ok)
                return;
            var bytes = ItemArray.Write();
            File.WriteAllBytes(sfd.FileName, bytes);
        }

        private void B_Load_Click(object sender, EventArgs e)
        {
            bool skipOccupiedSlots = (Keyboard.Modifiers & Keys.Alt) != 0;
            bool importCheatClipboard = (Keyboard.Modifiers & Keys.Control) != 0;
            if (importCheatClipboard && Clipboard.Instance.ContainsText)
            {
                var text = Clipboard.Instance.Text;
                var bytes = ItemCheatCode.ReadCode(text);
                if (bytes.Length % ItemArray.ItemSize == 0)
                {
                    ImportItemData(bytes, skipOccupiedSlots);
                    return;
                }
            }

            var ofd = new OpenFileDialog
            {
                Title = "Open Inventory",
                Filters = { new FileDialogFilter("New Horizons Inventory", ".nhi"), new FileDialogFilter("All files", ".*") },
                FileName = "items.nhi",
            };
            if (ofd.ShowDialog(this) != DialogResult.Ok)
                return;

            var data = File.ReadAllBytes(ofd.FileName);
            ImportItemData(data, skipOccupiedSlots);
        }

        private void ImportItemData(byte[] data, bool skipOccupiedSlots, int start = 0)
        {
            ItemArray.ImportItemDataX(data, skipOccupiedSlots, start);

            LoadItems();
            System.Media.SystemSounds.Asterisk.Play();
        }

        private void B_Inject_Click(object sender, EventArgs e)
        {
            var exist = WinFormsUtil.FirstFormOfType<SysBotUI>();
            if (exist != null)
            {
                exist.Show();
                exist.BringToFront();
                exist.CenterToForm(this);
                return;
            }

            void AfterRead(InjectionResult r)
            {
                if (r == InjectionResult.Success)
                    LoadItems();
            }

            static void AfterWrite(InjectionResult r)
            {
                Debug.WriteLine($"Write result: {r}");
                System.Media.SystemSounds.Asterisk.Play();
            }

            var sb = new SysBotController(InjectionType.Pouch);
            var pockInject = new PocketInjector(ItemArray.Items, sb.Bot);
            var ai = new AutoInjector(pockInject, AfterRead, AfterWrite);
            var ub = new USBBotController();
            var pockInjectUSB = new PocketInjector(ItemArray.Items, ub.Bot);
            var aiUSB = new AutoInjector(pockInjectUSB, AfterRead, AfterWrite);

            ItemGrid.ItemChanged = () => ai.Write();
            var sysbot = new SysBotUI(ai, sb, aiUSB, ub);
            sysbot.Show();
        }

        private void ItemEditor_DragEnter(object? sender, DragEventArgs e)
        {
            if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
                e.Effect = DragDropEffects.Copy;
        }

        private void PlayerItemEditor_DragDrop(object? sender, DragEventArgs e)
        {
            var files = (string[]?)e.Data?.GetData(DataFormats.FileDrop);
            if (files?.Length != 1 || Directory.Exists(files[0]))
                return;

            string path = files[0]; // open first D&D
            var fi = new FileInfo(path);
            if (fi.Length > ItemArray.TotalSize || fi.Length % Item.SIZE != 0)
                return;

            System.Media.SystemSounds.Asterisk.Play();
            var data = File.ReadAllBytes(path);
            if (sender == ItemEditor)
            {
                var item = new Item(BitConverter.ToUInt64(data, 0));
                ItemEditor.LoadItem(item);
            }
            else
            {
                bool skipOccupiedSlots = (Keyboard.Modifiers & Keys.Alt) != 0;
                ImportItemData(data, skipOccupiedSlots);
            }
        }

        public static void EnableDragDrop(Control parent, EventHandler<DragEventArgs> enter, EventHandler<DragEventArgs> drop)
        {
            parent.AllowDrop = true;
            parent.DragEnter += enter;
            parent.DragDrop += drop;
            foreach (var control in parent.Controls.OfType<ImageView>())
            {
                control.AllowDrop = true;
                control.DragEnter += enter;
                control.DragDrop += drop;
            }
        }

        private void InitializeComponent()
        {
            B_Cancel = new Button { Text = "Cancel" };
            B_Save = new Button { Text = "Save" };
            B_Dump = new Button { Text = "Dump" };
            B_Load = new Button { Text = "Load" };
            B_Inject = new Button { Text = "Inject" };
            ItemEditor = new ItemEditor();
            PAN_Items = new Panel();

            B_Cancel.Click += B_Cancel_Click;
            B_Save.Click += B_Save_Click;
            B_Dump.Click += B_Dump_Click;
            B_Load.Click += B_Load_Click;
            B_Inject.Click += B_Inject_Click;

            var buttonLayout = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Items = { B_Cancel, B_Save, B_Dump, B_Load, B_Inject }
            };

            var mainLayout = new StackLayout
            {
                Orientation = Orientation.Vertical,
                Items = { ItemEditor, PAN_Items, buttonLayout }
            };

            Content = mainLayout;
        }

        private Button B_Cancel;
        private Button B_Save;
        private Button B_Dump;
        private Button B_Load;
        private Button B_Inject;
        private ItemEditor ItemEditor;
        private Panel PAN_Items;
    }
}
