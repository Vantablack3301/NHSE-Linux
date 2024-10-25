using System;
using System.IO;
using System.Linq;
using Eto.Forms;
using Eto.Drawing;
using NHSE.Core;
using NHSE.Sprites;
using NHSE.Villagers;

namespace NHSE.WinForms
{
    public partial class VillagerEditor : Panel
    {
        public IVillager[] Villagers;
        public IVillagerOrigin Origin;
        private readonly HorizonSave SAV;
        private int VillagerIndex = -1;
        private bool Loading;

        public VillagerEditor(IVillager[] villagers, IVillagerOrigin origin, HorizonSave sav, bool hasHouses)
        {
            Villagers = villagers;
            Origin = origin;
            SAV = sav;
            InitializeComponent();
            LoadVillagers();

            B_EditHouses.Visible = hasHouses;
        }

        public void Save() => SaveVillager(VillagerIndex);

        private void LoadVillagers()
        {
            CB_Personality.Items.Clear();
            var personalities = Enum.GetNames(typeof(VillagerPersonality));
            foreach (var p in personalities)
                CB_Personality.Items.Add(p);

            VillagerIndex = -1;
            LoadVillager(0);
        }

        private void LoadVillager(object sender, EventArgs e) => LoadVillager((int)NUD_Villager.Value);

        private void LoadVillager(int index)
        {
            if (VillagerIndex >= 0)
                SaveVillager(VillagerIndex);

            if (index < 0)
                return;

            LoadVillager(Villagers[index]);
            VillagerIndex = index;
        }

        private void LoadVillager(IVillager v)
        {
            Loading = true;
            NUD_Species.Value = v.Species;
            NUD_Variant.Value = v.Variant;
            CB_Personality.SelectedIndex = (int)v.Personality;
            TB_Catchphrase.Text = v.CatchPhrase;
            CHK_VillagerMovingOut.Checked = v.MovingOut;
            Loading = false;
        }

        private void SaveVillager(int index)
        {
            var v = Villagers[index];

            v.Species = (byte)NUD_Species.Value;
            v.Variant = (byte)NUD_Variant.Value;
            v.Personality = (VillagerPersonality)CB_Personality.SelectedIndex;
            v.CatchPhrase = TB_Catchphrase.Text;
            v.MovingOut = CHK_VillagerMovingOut.Checked;
        }

        private void CHK_VillagerMovingOut_CheckedChanged(object sender, EventArgs e)
        {
            if (Loading)
                return;

            if (!CHK_VillagerMovingOut.Checked)
                return;

            MessageBox.Show(MessageStrings.MsgMoveOut, MessageStrings.MsgMoveOutSuggest);
        }

        private void B_DumpVillager_Click(object sender, EventArgs e)
        {
            if (Keyboard.Modifiers == Keys.Shift)
            {
                using var fbd = new SelectFolderDialog();
                if (fbd.ShowDialog(this) != DialogResult.Ok)
                    return;

                var dir = Path.GetDirectoryName(fbd.Directory);
                if (dir == null || !Directory.Exists(dir))
                    return;
                Villagers.Dump(fbd.Directory);
                return;
            }

            var name = L_ExternalName.Text;
            using var sfd = new SaveFileDialog
            {
                Filters = { new FileDialogFilter("New Horizons Villager", ".nhv"), new FileDialogFilter("New Horizons Villager", ".nhv2"), new FileDialogFilter("All files", ".*") },
                FileName = $"{name}.{Villagers[VillagerIndex].Extension}",
            };
            if (sfd.ShowDialog(this) != DialogResult.Ok)
                return;

            SaveVillager(VillagerIndex);
            var v = Villagers[VillagerIndex];
            File.WriteAllBytes(sfd.FileName, v.Write());
        }

        private void B_LoadVillager_Click(object sender, EventArgs e)
        {
            var name = L_ExternalName.Text;
            using var ofd = new OpenFileDialog
            {
                Filters = { new FileDialogFilter("New Horizons Villager", ".nhv"), new FileDialogFilter("New Horizons Villager", ".nhv2"), new FileDialogFilter("All files", ".*") },
                FileName = $"{name}.{Villagers[VillagerIndex].Extension}",
            };
            if (ofd.ShowDialog(this) != DialogResult.Ok)
                return;

            var path = ofd.FileName;
            var expectLength = SAV.Main.Offsets.VillagerSize;
            var fi = new FileInfo(path);
            if (!VillagerConverter.IsCompatible((int)fi.Length, expectLength))
            {
                MessageBox.Show(string.Format(MessageStrings.MsgDataSizeMismatchImport, fi.Length, expectLength), path);
                return;
            }

            var data = File.ReadAllBytes(ofd.FileName);
            data = VillagerConverter.GetCompatible(data, expectLength);
            if (data.Length != expectLength)
            {
                MessageBox.Show(string.Format(MessageStrings.MsgDataSizeMismatchImport, fi.Length, expectLength), path);
                return;
            }

            var v = SAV.Main.Offsets.ReadVillager(data);
            var player0 = Origin;
            if (!v.IsOriginatedFrom(player0))
            {
                string msg = string.Format(MessageStrings.MsgDataDidNotOriginateFromHost_0, player0.PlayerName);
                var result = MessageBox.Show(msg, MessageStrings.MsgAskUpdateValues, MessageBoxButtons.YesNoCancel);
                if (result == DialogResult.Cancel)
                    return;
                if (result == DialogResult.Yes)
                    v.ChangeOrigins(player0, v.Write());
            }

            LoadVillager(Villagers[VillagerIndex] = v);
        }

        private void B_EditWear_Click(object sender, EventArgs e)
        {
            var v = Villagers[VillagerIndex];
            var items = v.WearStockList;
            using var editor = new PlayerItemEditor(items, 8, 3);
            if (editor.ShowDialog(this) == DialogResult.Ok)
                v.WearStockList = items;
        }

        private void B_EditFurniture_Click(object sender, EventArgs e)
        {
            var v = Villagers[VillagerIndex];
            var items = v.FtrStockList;
            using var editor = new PlayerItemEditor(items, 8, 4);
            if (editor.ShowDialog(this) == DialogResult.Ok)
                v.FtrStockList = items;
        }

        private void B_EditVillagerFlags_Click(object sender, EventArgs e)
        {
            var v = Villagers[VillagerIndex];
            var flags = v.GetEventFlagsSave();
            using var editor = new VillagerFlagEditor(flags);
            if (editor.ShowDialog(this) == DialogResult.Ok)
                v.SetEventFlagsSave(flags);
        }

        private string GetCurrentVillagerInternalName() => VillagerUtil.GetInternalVillagerName((VillagerSpecies)NUD_Species.Value, (int)NUD_Variant.Value);
        private void ChangeVillager(object sender, EventArgs e) => ChangeVillager();
        private void ChangeVillager()
        {
            var name = GetCurrentVillagerInternalName();
            L_InternalName.Text = name;
            L_ExternalName.Text = GameInfo.Strings.GetVillager(name);
            PB_Villager.Image = VillagerSprite.GetVillagerSprite(name);
        }

        private void B_EditHouse_Click(object sender, EventArgs e)
        {
            SaveVillager(VillagerIndex);
            var villagers = SAV.Main.GetVillagers();
            var houses = SAV.Main.GetVillagerHouses();
            using var editor = new VillagerHouseEditor(houses, villagers, SAV.Main, VillagerIndex);
            if (editor.ShowDialog(this) == DialogResult.Ok)
                SAV.Main.SetVillagerHouses(houses);
        }

        private static void ShowContextMenuBelow(ContextMenu c, Control n) => c.Show(n.PointToScreen(new Point(0, n.Height)));
        private void B_EditVillager_Click(object sender, EventArgs e) => ShowContextMenuBelow(CM_EditVillager, B_EditVillager);

        private void B_EditVillagerRoom_Click(object sender, EventArgs e)
        {
            var v = Villagers[VillagerIndex];
            using var editor = new SaveRoomFloorWallEditor(v.Room);
            if (editor.ShowDialog(this) == DialogResult.Ok)
                v.Room = editor.Entity;
        }

        private void B_EditVillagerDesign_Click(object sender, EventArgs e)
        {
            var playerID = SAV.Players[0].Personal.GetPlayerIdentity(); // fetch ID for overwrite ownership
            var townID = SAV.Players[0].Personal.GetTownIdentity(); // fetch ID for overwrite ownership
            var v = Villagers[VillagerIndex];
            var tmp = new[] {v.Design};
            using var editor = new PatternEditorPRO(tmp);
            playerID.CopyTo(tmp[0].Data, 0x54); // overwrite playerID bytes
            townID.CopyTo(tmp[0].Data, 0x38); // overwrite townID bytes
            if (editor.ShowDialog(this) == DialogResult.Ok)
                v.Design = tmp[0];
        }

        private void B_EditVillagerPlayerMemories_Click(object sender, EventArgs e)
        {
            if (Keyboard.Modifiers == Keys.Shift)
            {
                var prompt = MessageBox.Show(MessageStrings.MsgVillagerFriendshipMax, MessageBoxButtons.YesNo);
                if (prompt != DialogResult.Yes)
                    return;
                foreach (var villager in Villagers)
                    villager.SetFriendshipAll();
                System.Media.SystemSounds.Asterisk.Play();
                return;
            }

            var v = Villagers[VillagerIndex];
            using var editor = new VillagerMemoryEditor(v);
            if (editor.ShowDialog(this) == DialogResult.Ok)
            { } // editor saves our changes
        }

        private void B_EditDIYTimer_Click(object sender, EventArgs e)
        {
            var v = Villagers[VillagerIndex];
            using var editor = new VillagerDIYTimerEditor(v);
            if (editor.ShowDialog(this) == DialogResult.Ok)
            { } // editor saves our changes
        }

        private void B_MoveOutAllVillagers_Click(object sender, EventArgs e)
        {
            if (Loading)
                return;

            var prompt = MessageBox.Show(MessageStrings.MsgMoveOutAll, MessageBoxButtons.OkCancel);
            if (prompt != DialogResult.Ok)
                return;

            foreach (var villager in Villagers)
                villager.MovingOut = true;
            CHK_VillagerMovingOut.Checked = true;

            System.Media.SystemSounds.Asterisk.Play();
        }

        private void B_SetPhraseOriginal_Click(object sender, EventArgs e)
        {
            var internalName = GetCurrentVillagerInternalName();
            TB_Catchphrase.Text = GameInfo.Strings.GetVillagerDefaultPhrase(internalName);
        }

        private void B_ReplaceVillager_Click(object sender, EventArgs e)
        {
            if (!Clipboard.Instance.ContainsText)
            {
                MessageBox.Show(MessageStrings.MsgVillagerReplaceNoText);
                return;
            }

            var internalName = Clipboard.Instance.Text;
            if (!VillagerResources.IsVillagerDataKnown(internalName))
            {
                internalName = GameInfo.Strings.VillagerMap.FirstOrDefault(z => string.Equals(z.Value, internalName, StringComparison.InvariantCultureIgnoreCase)).Key;
                if (internalName == default)
                {
                    MessageBox.Show(string.Format(MessageStrings.MsgVillagerReplaceUnknownName, internalName));
                    return;
                }
            }

            var index = VillagerIndex;
            var villager = Villagers[index];
            if (villager is not Villager2 v2)
            {
                MessageBox.Show(MessageStrings.MsgVillagerReplaceOutdatedSaveFormat);
                return;
            }

            var houses = SAV.Main.GetVillagerHouses();
            var houseIndex = Array.FindIndex(houses, z => z.NPC1 == index);
            var exist = new VillagerInfo(v2, houses[houseIndex]);
            var replace = VillagerSwap.GetReplacementVillager(exist, internalName);

            var nh = new VillagerHouse1(replace.House);
            SAV.Main.SetVillagerHouse(nh, houseIndex);
            var nv = new Villager2(replace.Villager);
            LoadVillager(Villagers[index] = nv);
            System.Media.SystemSounds.Asterisk.Play();
        }

        private void InitializeComponent()
        {
            NUD_Villager = new NumericUpDown();
            NUD_Species = new NumericUpDown();
            NUD_Variant = new NumericUpDown();
            CB_Personality = new DropDown();
            TB_Catchphrase = new TextBox();
            CHK_VillagerMovingOut = new CheckBox();
            B_DumpVillager = new Button();
            B_LoadVillager = new Button();
            B_EditWear = new Button();
            B_EditFurniture = new Button();
            B_EditVillagerFlags = new Button();
            B_EditHouse = new Button();
            B_EditVillager = new Button();
            B_EditVillagerRoom = new Button();
            B_EditVillagerDesign = new Button();
            B_EditVillagerPlayerMemories = new Button();
            B_EditDIYTimer = new Button();
            B_MoveOutAllVillagers = new Button();
            B_SetPhraseOriginal = new Button();
            B_ReplaceVillager = new Button();
            L_InternalName = new Label();
            L_ExternalName = new Label();
            PB_Villager = new ImageView();
            CM_EditVillager = new ContextMenu();

            var layout = new DynamicLayout();
            layout.BeginVertical();
            layout.Add(NUD_Villager);
            layout.Add(NUD_Species);
            layout.Add(NUD_Variant);
            layout.Add(CB_Personality);
            layout.Add(TB_Catchphrase);
            layout.Add(CHK_VillagerMovingOut);
            layout.Add(B_DumpVillager);
            layout.Add(B_LoadVillager);
            layout.Add(B_EditWear);
            layout.Add(B_EditFurniture);
            layout.Add(B_EditVillagerFlags);
            layout.Add(B_EditHouse);
            layout.Add(B_EditVillager);
            layout.Add(B_EditVillagerRoom);
            layout.Add(B_EditVillagerDesign);
            layout.Add(B_EditVillagerPlayerMemories);
            layout.Add(B_EditDIYTimer);
            layout.Add(B_MoveOutAllVillagers);
            layout.Add(B_SetPhraseOriginal);
            layout.Add(B_ReplaceVillager);
            layout.Add(L_InternalName);
            layout.Add(L_ExternalName);
            layout.Add(PB_Villager);
            layout.EndVertical();

            Content = layout;
        }

        private NumericUpDown NUD_Villager;
        private NumericUpDown NUD_Species;
        private NumericUpDown NUD_Variant;
        private DropDown CB_Personality;
        private TextBox TB_Catchphrase;
        private CheckBox CHK_VillagerMovingOut;
        private Button B_DumpVillager;
        private Button B_LoadVillager;
        private Button B_EditWear;
        private Button B_EditFurniture;
        private Button B_EditVillagerFlags;
        private Button B_EditHouse;
        private Button B_EditVillager;
        private Button B_EditVillagerRoom;
        private Button B_EditVillagerDesign;
        private Button B_EditVillagerPlayerMemories;
        private Button B_EditDIYTimer;
        private Button B_MoveOutAllVillagers;
        private Button B_SetPhraseOriginal;
        private Button B_ReplaceVillager;
        private Label L_InternalName;
        private Label L_ExternalName;
        private ImageView PB_Villager;
        private ContextMenu CM_EditVillager;
    }
}
