using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Eto.Drawing;
using Eto.Forms;
using NHSE.Core;
using NHSE.Injection;
using NHSE.Sprites;
using NHSE.WinForms.Properties;

namespace NHSE.WinForms
{
    public sealed partial class Editor : Form
    {
        private readonly HorizonSave SAV;
        private readonly VillagerEditor Villagers;

        public Editor(HorizonSave file)
        {
            InitializeComponent();

            SAV = file;

            LoadPlayers();
            Villagers = LoadVillagers();

            LoadMain();

            var lang = Settings.Default.Language;
            var index = GameLanguage.GetLanguageIndex(lang);
            Menu_Language.SelectedIndex = index; // triggers translation
            // this.TranslateInterface(GameInfo.CurrentLanguage);

            Title = SAV.GetSaveTitle("NHSE");
        }

        private void Menu_Settings_Click(object sender, EventArgs e)
        {
            using var editor = new SettingsEditor();
            editor.ShowDialog();
        }

        private void Menu_Language_SelectedIndexChanged(object sender, EventArgs e)
        {
            Menu_Options.DropDown.Close();
            if ((uint)Menu_Language.SelectedIndex >= GameLanguage.LanguageCount)
                return;
            var lang = GameInfo.SetLanguage2Char(Menu_Language.SelectedIndex);

            this.TranslateInterface(lang);
            var settings = Settings.Default;
            settings.Language = lang;
            settings.Save();

            Task.Run(() =>
            {
                ItemSprite.Initialize(GameInfo.GetStrings("en").itemlist);
                TranslationUtil.SetLocalization(typeof(MessageStrings), lang);
                TranslationUtil.SetLocalization(GameInfo.Strings.InternalNameTranslation, lang);
            });
        }

        private void Menu_Save_Click(object sender, EventArgs e)
        {
            SaveAll();
            try
            {
                SAV.Save((uint) DateTime.Now.Ticks);
            }
            catch (Exception ex)
            {
                WinFormsUtil.Error(MessageStrings.MsgSaveDataExportFail, ex.Message);
                return;
            }
            WinFormsUtil.Alert(MessageStrings.MsgSaveDataExportSuccess);
        }

        private void Menu_DumpDecrypted_Click(object sender, EventArgs e)
        {
            using var fbd = new SelectFolderDialog();
            if (fbd.ShowDialog(this) != DialogResult.Ok)
                return;
            SAV.Dump(fbd.Directory);
            System.Media.SystemSounds.Asterisk.Play();
        }

        private void Menu_LoadDecrypted_Click(object sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Open main.dat ...",
                Filters = { new FileDialogFilter("New Horizons Save File (main.dat)", "main.dat") },
                FileName = "main.dat",
            };

            if (ofd.ShowDialog(this) == DialogResult.Ok)
                LoadDecryptedFromPath(ofd.FileName);
        }

        private void LoadDecryptedFromPath(string main)
        {
            var dir = Path.GetDirectoryName(main);
            if (dir is null || !Directory.Exists(dir))
            {
                WinFormsUtil.Alert(MessageStrings.MsgImportDirectoryDoesNotExist);
                return;
            }

            SAV.Load(dir);
            ReloadAll(); // reload all fields
            System.Media.SystemSounds.Asterisk.Play();
        }

        private void Menu_VerifyHashes_Click(object sender, EventArgs e)
        {
            var result = SAV.GetInvalidHashes().ToArray();
            if (result.Length == 0)
            {
                WinFormsUtil.Alert(MessageStrings.MsgSaveDataHashesValid);
                return;
            }

            if (WinFormsUtil.Prompt(MessageBoxButtons.YesNo, MessageStrings.MsgAskExportResultToClipboard) != DialogResult.Yes)
                return;

            var lines = result.Select(z => z.ToString());
            Clipboard.Text = string.Join(Environment.NewLine, lines);
        }

        private void Menu_RAMEdit_Click(object sender, EventArgs e)
        {
            var exist = WinFormsUtil.FirstFormOfType<SysBotRAMEdit>();
            if (exist != null)
            {
                exist.Show();
                return;
            }

            var sysbot = new SysBotRAMEdit(InjectionType.Generic);
            sysbot.Show();
        }

        private void Menu_ItemImages_Click(object sender, EventArgs e)
        {
            var exist = WinFormsUtil.FirstFormOfType<ImageFetcher>();
            if (exist != null)
            {
                exist.Show();
                return;
            }

            var imgfetcher = new ImageFetcher();
            imgfetcher.Show();
        }

        private void ReloadAll()
        {
            Villagers.Villagers = SAV.Main.GetVillagers();
            Villagers.Origin = SAV.Players[0].Personal;
            LoadPlayers();
        }

        private VillagerEditor LoadVillagers()
        {
            var p0 = SAV.Players[0].Personal;
            var villagers = SAV.Main.GetVillagers();
            var v = new VillagerEditor(villagers, p0, SAV, true) {Size = new Size(-1, -1)};
            Tab_Villagers.Content = v;
            return v;
        }

        private void SaveAll()
        {
            SavePlayer(PlayerIndex);
            Villagers.Save();
            SAV.Main.SetVillagers(Villagers.Villagers);
            SaveMain();
        }

        private void LoadMain()
        {
            var m = SAV.Main;
            var names = Enum.GetNames(typeof(Hemisphere));
            foreach (var n in names)
                CB_Hemisphere.Items.Add(n);
            CB_Hemisphere.SelectedIndex = (int)m.Hemisphere;

            names = Enum.GetNames(typeof(AirportColor));
            foreach (var n in names)
                CB_AirportColor.Items.Add(n);
            CB_AirportColor.SelectedIndex = (int)m.AirportThemeColor;
            NUD_WeatherSeed.Value = m.WeatherSeed;
        }

        private void SaveMain()
        {
            var m = SAV.Main;
            m.Hemisphere = (Hemisphere)CB_Hemisphere.SelectedIndex;
            m.AirportThemeColor = (AirportColor)CB_AirportColor.SelectedIndex;
            m.WeatherSeed = (uint)NUD_WeatherSeed.Value;
        }

        #region Player Editing
        private void LoadPlayers()
        {
            if (SAV.Players.Length == 0)
                throw new Exception("No players found in the loaded directory.");

            CB_Players.Items.Clear();
            var playerList = SAV.Players.Select(z => z.DirectoryName);
            foreach (var p in playerList)
                CB_Players.Items.Add(p);

            PlayerIndex = -1;
            CB_Players.SelectedIndex = 0;
        }

        private int PlayerIndex = -1;
        private void LoadPlayer(object sender, EventArgs e) => LoadPlayer(CB_Players.SelectedIndex);

        private void B_EditPlayerItems_Click(object sender, EventArgs e)
        {
            var player = SAV.Players[PlayerIndex];
            {
                var pers = player.Personal;
                var bag = pers.Bag;
                var pocket = pers.Pocket;
                var items = pocket.Concat(bag).ToArray();
                using var editor = new PlayerItemEditor(items, 10, 4, true);
                if (editor.ShowDialog(this) != DialogResult.Ok)
                    return;

                pers.Pocket = items.Take(pocket.Count).ToArray();
                pers.Bag = items.Skip(pocket.Count).Take(bag.Count).ToArray();
            }
        }

        private void B_Storage_Click(object sender, EventArgs e)
        {
            var player = SAV.Players[PlayerIndex];
            var pers = player.Personal;
            var p1 = pers.ItemChest;
            using var editor = new PlayerItemEditor(p1, 10, 5);
            if (editor.ShowDialog(this) == DialogResult.Ok)
                pers.ItemChest = p1;
        }

        private void B_RecycleBin_Click(object sender, EventArgs e)
        {
            var items = SAV.Main.RecycleBin;
            using var editor = new PlayerItemEditor(items, 10, 4);
            if (editor.ShowDialog(this) == DialogResult.Ok)
                SAV.Main.RecycleBin = items;
        }

        private void B_EditPlayerRecipes_Click(object sender, EventArgs e)
        {
            var player = SAV.Players[PlayerIndex];
            using var editor = new RecipeListEditor(player);
            editor.ShowDialog(this);
        }

        private void B_EditPlayerReceivedItems_Click(object sender, EventArgs e)
        {
            var player = SAV.Players[PlayerIndex];
            using var editor = new ItemReceivedEditor(player);
            editor.ShowDialog(this);
        }

        private void B_EditPlayerReactions_Click(object sender, EventArgs e)
        {
            var player = SAV.Players[PlayerIndex];
            using var editor = new ReactionEditor(player.Personal);
            editor.ShowDialog(this);
        }

        private void B_EditPlayerMisc_Click(object sender, EventArgs e)
        {
            var player = SAV.Players[PlayerIndex];
            using var editor = new MiscPlayerEditor(player);
            editor.ShowDialog(this);
        }

        private void LoadPlayer(int index)
        {
            if (PlayerIndex >= 0)
                SavePlayer(PlayerIndex);

            if (index < 0)
                return;

            var player = SAV.Players[index];

            var pers = player.Personal;
            TB_Name.Text = pers.PlayerName;
            TB_TownName.Text = pers.TownName;
            NUD_BankBells.Value = Math.Min(int.MaxValue, pers.Bank.Value);
            NUD_NookMiles.Value = Math.Min(int.MaxValue, pers.NookMiles.Value);
            NUD_TotalNookMiles.Value = Math.Min(int.MaxValue, pers.TotalNookMiles.Value);
            NUD_Wallet.Value = Math.Min(int.MaxValue, pers.Wallet.Value);

            // swapped on purpose -- first count is the first two rows of items
            NUD_PocketCount1.Value = Math.Min(int.MaxValue, pers.PocketCount);
            NUD_PocketCount2.Value = Math.Min(int.MaxValue, pers.BagCount);
            NUD_StorageCount.Value = Math.Min(int.MaxValue, pers.ItemChestCount);

            if (player.WhereAreN is not null)
            {
                NUD_Poki.Value = Math.Min(int.MaxValue, player.WhereAreN.Poki.Value);
            }
            else
            {
                L_Poki.Visible = NUD_Poki.Visible = false;
            }

            try
            {
                var photo = pers.GetPhotoData();
                PB_Player.Image = new Bitmap(new MemoryStream(photo));
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            PlayerIndex = index;
        }

        private void SavePlayer(int index)
        {
            if (index < 0)
                return;

            var player = SAV.Players[index];
            var pers = player.Personal;

            if (pers.PlayerName != TB_Name.Text)
            {
                var orig = pers.GetPlayerIdentity();
                pers.PlayerName = TB_Name.Text;
                var updated = pers.GetPlayerIdentity();
                SAV.ChangeIdentity(orig, updated);
            }
            if (pers.TownName != TB_TownName.Text)
            {
                var orig = pers.GetTownIdentity();
                pers.TownName = TB_TownName.Text;
                var updated = pers.GetTownIdentity();
                SAV.ChangeIdentity(orig, updated);
            }

            var bank = pers.Bank;
            bank.Value = (uint)NUD_BankBells.Value;
            pers.Bank = bank;

            var nook = pers.NookMiles;
            nook.Value = (uint)NUD_NookMiles.Value;
            pers.NookMiles = nook;

            var tnook = pers.TotalNookMiles;
            tnook.Value = (uint)NUD_TotalNookMiles.Value;
            pers.TotalNookMiles = tnook;

            var wallet = pers.Wallet;
            wallet.Value = (uint)NUD_Wallet.Value;
            pers.Wallet = wallet;

            // swapped on purpose -- first count is the first two rows of items
            pers.PocketCount = (uint)NUD_PocketCount1.Value;
            pers.BagCount = (uint)NUD_PocketCount2.Value;

            pers.ItemChestCount = (uint)NUD_StorageCount.Value;

            if (player.WhereAreN is { } x)
            {
                var poki = x.Poki;
                poki.Value = (uint)NUD_Poki.Value;
                x.Poki = poki;
            }
        }

        private void B_EditAchievements_Click(object sender, EventArgs e)
        {
            var pers = SAV.Players[PlayerIndex].Personal;
            using var editor = new AchievementEditor(pers);
            editor.ShowDialog(this);
        }

        private void B_EditPlayerFlags_Click(object sender, EventArgs e)
        {
            var pers = SAV.Players[PlayerIndex].Personal;
            var flags = pers.GetEventFlagsPlayer();
            using var editor = new FlagEditor(flags);
            if (editor.ShowDialog(this) == DialogResult.Ok)
                pers.SetEventFlagsPlayer(flags);
        }

        #endregion

        private void Menu_SavePNG_Click(object sender, EventArgs e)
        {
            var pb = WinFormsUtil.GetUnderlyingControl<ImageView>(sender);
            if (pb?.Image == null)
            {
                WinFormsUtil.Alert(MessageStrings.MsgNoPictureLoaded);
                return;
            }

            string name = SAV.Players[PlayerIndex].Personal.PlayerName;
            var bmp = pb.Image;
            using var sfd = new SaveFileDialog
            {
                Filters = { new FileDialogFilter("png file (*.png)", "png") },
                FileName = $"{name}.png",
            };
            if (sfd.ShowDialog(this) != DialogResult.Ok)
                return;

            bmp.Save(sfd.FileName, ImageFormat.Png);
        }

        private void B_EditTurnipExchange_Click(object sender, EventArgs e)
        {
            var turnips = SAV.Main.Turnips;
            using var editor = new SingleObjectEditor<TurnipStonk>(turnips, PropertySort.Categorized, false);
            if (editor.ShowDialog(this) == DialogResult.Ok)
                SAV.Main.Turnips = turnips;
        }

        private void B_EditFieldItems_Click(object sender, EventArgs e)
        {
            using var editor = new FieldItemEditor(SAV.Main);
            editor.ShowDialog(this);
        }

        private void B_EditLandFlags_Click(object sender, EventArgs e)
        {
            var flags = SAV.Main.GetEventFlagLand();
            using var editor = new LandFlagEditor(flags);
            if (editor.ShowDialog(this) == DialogResult.Ok)
                SAV.Main.SetEventFlagLand(flags);
        }

        private void B_EditPatterns_Click(object sender, EventArgs e)
        {
            var playerID = SAV.Players[0].Personal.GetPlayerIdentity(); // fetch ID for overwrite ownership
            var townID = SAV.Players[0].Personal.GetTownIdentity(); // fetch ID for overwrite ownership
            var patterns = SAV.Main.GetDesigns();
            using var editor = new PatternEditor(patterns);
            if (editor.ShowDialog(this) == DialogResult.Ok)
                SAV.Main.SetDesigns(patterns, playerID, townID);
        }

        private void B_EditPRODesigns_Click(object sender, EventArgs e)
        {
            var playerID = SAV.Players[0].Personal.GetPlayerIdentity(); // fetch ID for overwrite ownership
            var townID = SAV.Players[0].Personal.GetTownIdentity(); // fetch ID for overwrite ownership
            var patterns = SAV.Main.GetDesignsPRO();
            using var editor = new PatternEditorPRO(patterns);
            if (editor.ShowDialog(this) == DialogResult.Ok)
                SAV.Main.SetDesignsPRO(patterns, playerID, townID);
        }

        private void B_EditPatternFlag_Click(object sender, EventArgs e)
        {
            var patterns = new[] {SAV.Main.FlagMyDesign};
            using var editor = new PatternEditor(patterns);
            if (editor.ShowDialog(this) == DialogResult.Ok)
                SAV.Main.FlagMyDesign = patterns[0];
        }

        private void B_EditDesignsTailor_Click(object sender, EventArgs e)
        {
            var patterns = SAV.Main.GetDesignsTailor();
            using var editor = new PatternEditorPRO(patterns);
            if (editor.ShowDialog(this) == DialogResult.Ok)
                SAV.Main.SetDesignsTailor(patterns);
        }

        private static void ShowContextMenuBelow(ContextMenu c, Control n) => c.Show(n.PointToScreen(new Point(0, n.Height)));
        private void B_EditPlayer_Click(object sender, EventArgs e) => ShowContextMenuBelow(CM_EditPlayer, B_EditPlayer);
        private void B_EditMap_Click(object sender, EventArgs e) => ShowContextMenuBelow(CM_EditMap, B_EditMap);

        private void B_EditPlayerHouses_Click(object sender, EventArgs e)
        {
            var houses = SAV.Main.GetPlayerHouses();
            using var editor = new PlayerHouseEditor(houses, SAV.Players, SAV.Main, PlayerIndex);
            if (editor.ShowDialog(this) == DialogResult.Ok)
                SAV.Main.SetPlayerHouses(houses);
        }

        private void B_EditBulletin_Click(object sender, EventArgs e)
        {
            var boxed = SAV.Main.Bulletin;
            using var editor = new SingleObjectEditor<object>(boxed, PropertySort.NoSort, false);
            if (editor.ShowDialog(this) == DialogResult.Ok)
                SAV.Main.Bulletin = boxed;
        }

        private void B_EditFieldGoods_Click(object sender, EventArgs e)
        {
            var boxed = SAV.Main.SaveFg;
            using var editor = new SingleObjectEditor<object>(boxed, PropertySort.NoSort, false);
            if (editor.ShowDialog(this) == DialogResult.Ok)
                SAV.Main.SaveFg = boxed;
        }

        private void B_EditMuseum_Click_Click(object sender, EventArgs e)
        {
            var museum = SAV.Main.Museum;
            using var editor = new MuseumEditor(museum);
            if (editor.ShowDialog(this) == DialogResult.Ok)
                SAV.Main.Museum = museum;
        }

        private void B_EditVisitors_Click(object sender, EventArgs e)
        {
            var boxed = SAV.Main.Visitor;
            using var editor = new SingleObjectEditor<object>(boxed, PropertySort.NoSort, false);
            if (editor.ShowDialog(this) == DialogResult.Ok)
                SAV.Main.Visitor = boxed;
        }

        private void NUD_PocketCount_ValueChanged(object sender, EventArgs e) => ((NumericUpDown) sender).BackgroundColor = (uint) ((NumericUpDown) sender).Value > 20 ? Colors.Red : NUD_BankBells.BackgroundColor;
        private void NUD_Wallet_ValueChanged(object sender, EventArgs e) => NUD_Wallet.BackgroundColor = (ulong) NUD_Wallet.Value > 99_999 ? Colors.Red : NUD_BankBells.BackgroundColor;

        private void InitializeComponent()
        {
            Menu_Settings = new ButtonMenuItem { Text = "Settings" };
            Menu_Settings.Click += Menu_Settings_Click;

            Menu_Language = new DropDown { Width = 100 };
            Menu_Language.SelectedIndexChanged += Menu_Language_SelectedIndexChanged;

            Menu_Options = new ButtonMenuItem { Text = "Options" };
            Menu_Options.Items.Add(Menu_Settings);
            Menu_Options.Items.Add(Menu_Language);

            Menu_Save = new Button { Text = "Save" };
            Menu_Save.Click += Menu_Save_Click;

            Menu_DumpDecrypted = new Button { Text = "Dump Decrypted" };
            Menu_DumpDecrypted.Click += Menu_DumpDecrypted_Click;

            Menu_LoadDecrypted = new Button { Text = "Load Decrypted" };
            Menu_LoadDecrypted.Click += Menu_LoadDecrypted_Click;

            Menu_VerifyHashes = new Button { Text = "Verify Hashes" };
            Menu_VerifyHashes.Click += Menu_VerifyHashes_Click;

            Menu_RAMEdit = new Button { Text = "RAM Edit" };
            Menu_RAMEdit.Click += Menu_RAMEdit_Click;

            Menu_ItemImages = new Button { Text = "Item Images" };
            Menu_ItemImages.Click += Menu_ItemImages_Click;

            Menu_SavePNG = new Button { Text = "Save PNG" };
            Menu_SavePNG.Click += Menu_SavePNG_Click;

            B_EditPlayerItems = new Button { Text = "Edit Player Items" };
            B_EditPlayerItems.Click += B_EditPlayerItems_Click;

            B_Storage = new Button { Text = "Storage" };
            B_Storage.Click += B_Storage_Click;

            B_RecycleBin = new Button { Text = "Recycle Bin" };
            B_RecycleBin.Click += B_RecycleBin_Click;

            B_EditPlayerRecipes = new Button { Text = "Edit Player Recipes" };
            B_EditPlayerRecipes.Click += B_EditPlayerRecipes_Click;

            B_EditPlayerReceivedItems = new Button { Text = "Edit Player Received Items" };
            B_EditPlayerReceivedItems.Click += B_EditPlayerReceivedItems_Click;

            B_EditPlayerReactions = new Button { Text = "Edit Player Reactions" };
            B_EditPlayerReactions.Click += B_EditPlayerReactions_Click;

            B_EditPlayerMisc = new Button { Text = "Edit Player Misc" };
            B_EditPlayerMisc.Click += B_EditPlayerMisc_Click;

            B_EditAchievements = new Button { Text = "Edit Achievements" };
            B_EditAchievements.Click += B_EditAchievements_Click;

            B_EditPlayerFlags = new Button { Text = "Edit Player Flags" };
            B_EditPlayerFlags.Click += B_EditPlayerFlags_Click;

            B_EditTurnipExchange = new Button { Text = "Edit Turnip Exchange" };
            B_EditTurnipExchange.Click += B_EditTurnipExchange_Click;

            B_EditFieldItems = new Button { Text = "Edit Field Items" };
            B_EditFieldItems.Click += B_EditFieldItems_Click;

            B_EditLandFlags = new Button { Text = "Edit Land Flags" };
            B_EditLandFlags.Click += B_EditLandFlags_Click;

            B_EditPatterns = new Button { Text = "Edit Patterns" };
            B_EditPatterns.Click += B_EditPatterns_Click;

            B_EditPRODesigns = new Button { Text = "Edit PRO Designs" };
            B_EditPRODesigns.Click += B_EditPRODesigns_Click;

            B_EditPatternFlag = new Button { Text = "Edit Pattern Flag" };
            B_EditPatternFlag.Click += B_EditPatternFlag_Click;

            B_EditDesignsTailor = new Button { Text = "Edit Designs Tailor" };
            B_EditDesignsTailor.Click += B_EditDesignsTailor_Click;

            B_EditPlayer = new Button { Text = "Edit Player" };
            B_EditPlayer.Click += B_EditPlayer_Click;

            B_EditMap = new Button { Text = "Edit Map" };
            B_EditMap.Click += B_EditMap_Click;

            B_EditPlayerHouses = new Button { Text = "Edit Player Houses" };
            B_EditPlayerHouses.Click += B_EditPlayerHouses_Click;

            B_EditBulletin = new Button { Text = "Edit Bulletin" };
            B_EditBulletin.Click += B_EditBulletin_Click;

            B_EditFieldGoods = new Button { Text = "Edit Field Goods" };
            B_EditFieldGoods.Click += B_EditFieldGoods_Click;

            B_EditMuseum = new Button { Text = "Edit Museum" };
            B_EditMuseum.Click += B_EditMuseum_Click_Click;

            B_EditVisitors = new Button { Text = "Edit Visitors" };
            B_EditVisitors.Click += B_EditVisitors_Click;

            CB_Players = new DropDown();
            CB_Players.SelectedIndexChanged += LoadPlayer;

            TB_Name = new TextBox();
            TB_TownName = new TextBox();
            NUD_BankBells = new NumericUpDown();
            NUD_NookMiles = new NumericUpDown();
            NUD_TotalNookMiles = new NumericUpDown();
            NUD_Wallet = new NumericUpDown();
            NUD_PocketCount1 = new NumericUpDown();
            NUD_PocketCount2 = new NumericUpDown();
            NUD_StorageCount = new NumericUpDown();
            NUD_Poki = new NumericUpDown();
            L_Poki = new Label { Text = "Poki" };
            PB_Player = new ImageView();
            CB_Hemisphere = new DropDown();
            CB_AirportColor = new DropDown();
            NUD_WeatherSeed = new NumericUpDown();
            Tab_Villagers = new TabPage { Text = "Villagers" };

            var layout = new DynamicLayout();
            layout.BeginVertical();
            layout.Add(Menu_Options);
            layout.Add(Menu_Save);
            layout.Add(Menu_DumpDecrypted);
            layout.Add(Menu_LoadDecrypted);
            layout.Add(Menu_VerifyHashes);
            layout.Add(Menu_RAMEdit);
            layout.Add(Menu_ItemImages);
            layout.Add(Menu_SavePNG);
            layout.Add(B_EditPlayerItems);
            layout.Add(B_Storage);
            layout.Add(B_RecycleBin);
            layout.Add(B_EditPlayerRecipes);
            layout.Add(B_EditPlayerReceivedItems);
            layout.Add(B_EditPlayerReactions);
            layout.Add(B_EditPlayerMisc);
            layout.Add(B_EditAchievements);
            layout.Add(B_EditPlayerFlags);
            layout.Add(B_EditTurnipExchange);
            layout.Add(B_EditFieldItems);
            layout.Add(B_EditLandFlags);
            layout.Add(B_EditPatterns);
            layout.Add(B_EditPRODesigns);
            layout.Add(B_EditPatternFlag);
            layout.Add(B_EditDesignsTailor);
            layout.Add(B_EditPlayer);
            layout.Add(B_EditMap);
            layout.Add(B_EditPlayerHouses);
            layout.Add(B_EditBulletin);
            layout.Add(B_EditFieldGoods);
            layout.Add(B_EditMuseum);
            layout.Add(B_EditVisitors);
            layout.Add(CB_Players);
            layout.Add(TB_Name);
            layout.Add(TB_TownName);
            layout.Add(NUD_BankBells);
            layout.Add(NUD_NookMiles);
            layout.Add(NUD_TotalNookMiles);
            layout.Add(NUD_Wallet);
            layout.Add(NUD_PocketCount1);
            layout.Add(NUD_PocketCount2);
            layout.Add(NUD_StorageCount);
            layout.Add(NUD_Poki);
            layout.Add(L_Poki);
            layout.Add(PB_Player);
            layout.Add(CB_Hemisphere);
            layout.Add(CB_AirportColor);
            layout.Add(NUD_WeatherSeed);
            layout.Add(Tab_Villagers);
            layout.EndVertical();

            Content = layout;
        }

        private ButtonMenuItem Menu_Settings;
        private DropDown Menu_Language;
        private ButtonMenuItem Menu_Options;
        private Button Menu_Save;
        private Button Menu_DumpDecrypted;
        private Button Menu_LoadDecrypted;
        private Button Menu_VerifyHashes;
        private Button Menu_RAMEdit;
        private Button Menu_ItemImages;
        private Button Menu_SavePNG;
        private Button B_EditPlayerItems;
        private Button B_Storage;
        private Button B_RecycleBin;
        private Button B_EditPlayerRecipes;
        private Button B_EditPlayerReceivedItems;
        private Button B_EditPlayerReactions;
        private Button B_EditPlayerMisc;
        private Button B_EditAchievements;
        private Button B_EditPlayerFlags;
        private Button B_EditTurnipExchange;
        private Button B_EditFieldItems;
        private Button B_EditLandFlags;
        private Button B_EditPatterns;
        private Button B_EditPRODesigns;
        private Button B_EditPatternFlag;
        private Button B_EditDesignsTailor;
        private Button B_EditPlayer;
        private Button B_EditMap;
        private Button B_EditPlayerHouses;
        private Button B_EditBulletin;
        private Button B_EditFieldGoods;
        private Button B_EditMuseum;
        private Button B_EditVisitors;
        private DropDown CB_Players;
        private TextBox TB_Name;
        private TextBox TB_TownName;
        private NumericUpDown NUD_BankBells;
        private NumericUpDown NUD_NookMiles;
        private NumericUpDown NUD_TotalNookMiles;
        private NumericUpDown NUD_Wallet;
        private NumericUpDown NUD_PocketCount1;
        private NumericUpDown NUD_PocketCount2;
        private NumericUpDown NUD_StorageCount;
        private NumericUpDown NUD_Poki;
        private Label L_Poki;
        private ImageView PB_Player;
        private DropDown CB_Hemisphere;
        private DropDown CB_AirportColor;
        private NumericUpDown NUD_WeatherSeed;
        private TabPage Tab_Villagers;
    }
}
