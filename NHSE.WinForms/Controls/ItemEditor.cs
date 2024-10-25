using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Eto.Forms;
using NHSE.Core;
using NHSE.Sprites;

namespace NHSE.WinForms
{
    public partial class ItemEditor : Panel
    {
        private readonly List<ComboItem> Recipes = GameInfo.Strings.CreateItemDataSource(RecipeList.Recipes, false);
        private readonly List<ComboItem> Fossils = GameInfo.Strings.CreateItemDataSource(GameLists.Fossils, false);
        private readonly CheckBox[] Watered;

        private bool Loading = true;
        private bool CanExtend;

        public ItemEditor()
        {
            InitializeComponent();

            CB_WrapColor.Items.AddRange(Enum.GetNames(typeof(ItemWrappingPaper)));
            CB_WrapType.Items.AddRange(Enum.GetNames(typeof(ItemWrapping)));

            Watered = new[]
            {
                CHK_WV0, CHK_WV1,
                CHK_WV2, CHK_WV3,
                CHK_WV4, CHK_WV5,
                CHK_WV6, CHK_WV7,
                CHK_WV8, CHK_WV9,
            };
        }

        private IReadOnlyList<ComboItem> AllItems = Array.Empty<ComboItem>();

        public void Initialize(IReadOnlyList<ComboItem> items, bool canExtend = false)
        {
            CHK_IsExtension.Visible = CanExtend = canExtend;

            CB_ItemID.DisplayMember = nameof(ComboItem.Text);
            CB_ItemID.ValueMember = nameof(ComboItem.Value);
            CB_ItemID.DataSource = items;

            CB_Recipe.DisplayMember = nameof(ComboItem.Text);
            CB_Recipe.ValueMember = nameof(ComboItem.Value);
            CB_Recipe.DataSource = Recipes;

            CB_Fossil.DisplayMember = nameof(ComboItem.Text);
            CB_Fossil.ValueMember = nameof(ComboItem.Value);
            CB_Fossil.DataSource = Fossils;

            LoadItem(Item.NO_ITEM);

            AllItems = items;
        }

        public Item LoadItem(Item item)
        {
            Loading = true;
            var id = item.ItemId;
            if (CanExtend && id == Item.EXTENSION)
                return LoadExtensionItem(item);

            CHK_IsExtension.Checked = false;
            CB_ItemID.SelectedValue = (int)id;
            var kind = ItemInfo.GetItemKind(id);

            if (kind.IsFlowerGene(id))
            {
                LoadGenes(item.Genes);
                CHK_Gold.Checked = item.IsWateredGold;
                CHK_IsWatered.Checked = item.IsWatered;
                NUD_WaterDays.Value = item.DaysWatered;
                for (int i = 0; i < Watered.Length; i++)
                    Watered[i].Checked = item.GetIsWateredByVisitor(i);
            }
            else
            {
                NUD_Count.Value = item.Count;
                NUD_Uses.Value = item.UseCount;
                NUD_Flag0.Value = item.SystemParam;
            }

            LoadItemTypeValues(kind, id);
            if (kind == ItemKind.Kind_MessageBottle || id >= 60_000)
            {
                NUD_Flag1.Value = item.AdditionalParam;
            }
            else
            {
                CHK_Wrapped.Checked = item.WrappingType != 0;
                CB_WrapType.SelectedIndex = (int)item.WrappingType;
                CB_WrapColor.SelectedIndex = (int)item.WrappingPaper;
                CHK_WrapShowName.Checked = item.WrappingShowItem;
                CHK_Wrap80.Checked = item.Wrapping80;
            }

            Loading = false;
            return item;
        }

        private Item LoadExtensionItem(Item item)
        {
            CB_ItemID.SelectedValue = (int) item.ExtensionItemId;
            CHK_IsExtension.Checked = true;
            NUD_ExtensionX.Value = item.ExtensionX;
            NUD_ExtensionY.Value = item.ExtensionY;
            return item;
        }

        public Item SetItem(Item item)
        {
            if (CHK_IsExtension.Checked)
                return SetExtensionItem(item);

            var id = (ushort)WinFormsUtil.GetIndex(CB_ItemID);
            var kind = ItemInfo.GetItemKind(id);

            item.ItemId = id;
            if (kind.IsFlowerGene(id))
            {
                item.Genes = SaveGenes();
                item.DaysWatered = (int) NUD_WaterDays.Value;
                item.IsWateredGold = CHK_Gold.Checked;
                item.IsWatered = CHK_IsWatered.Checked;
                for (int i = 0; i < Watered.Length; i++)
                    item.SetIsWateredByVisitor(i, Watered[i].Checked);

                item.SystemParam = 0;
                item.AdditionalParam = 0;
            }
            else
            {
                item.Count = (ushort)NUD_Count.Value;
                item.UseCount = (ushort)NUD_Uses.Value;
                item.SystemParam = (byte)NUD_Flag0.Value;
            }

            if (kind == ItemKind.Kind_MessageBottle || id >= 60_000)
            {
                item.AdditionalParam = (byte)NUD_Flag1.Value;
            }
            else
            {
                if (!CHK_Wrapped.Checked)
                {
                    item.SetWrapping(0, 0);
                }
                else
                {
                    var type = (ItemWrapping)CB_WrapType.SelectedIndex;
                    var color = (ItemWrappingPaper)CB_WrapColor.SelectedIndex;
                    var show = CHK_WrapShowName.Checked;
                    var flag = CHK_Wrap80.Checked;
                    item.SetWrapping(type, color, show, flag);
                }
            }
            return item;
        }

        private Item SetExtensionItem(Item item)
        {
            var id = (ushort)WinFormsUtil.GetIndex(CB_ItemID);
            item.ItemId = Item.EXTENSION;
            item.ExtensionItemId = id;
            item.ExtensionX = (byte) NUD_ExtensionX.Value;
            item.ExtensionY = (byte) NUD_ExtensionY.Value;
            return item;
        }

        private void CB_ItemID_SelectedValueChanged(object sender, EventArgs e)
        {
            var itemID = (ushort)WinFormsUtil.GetIndex(CB_ItemID);
            var itemCount = (ushort)NUD_Count.Value;
            ChangeItem(itemID, itemCount);
            var kind = ItemInfo.GetItemKind(itemID);

            ToggleEditorVisibility(kind, itemID);
            if (!Loading)
                LoadItemTypeValues(kind, itemID);

            var remake = ItemRemakeUtil.GetRemakeIndex(itemID);
            if (remake < 0)
            {
                var closeItems = GameInfo.Strings.GetAssociatedItems(itemID, out var bse);
                if (closeItems.Count > 1) // ignore if we are the only parenthesised item
                {
                    L_RemakeBody.Text = $"{bse.Trim()}:\n" + closeItems.ToStringList(false);
                    L_RemakeBody.Visible = true;
                }
                else
                {
                    L_RemakeBody.Visible = false;
                    L_RemakeFabric.Visible = false;
                }
            }
            else
            {
                var info = ItemRemakeInfoData.List[remake];
                var body = info.GetBodySummary(GameInfo.Strings);
                L_RemakeBody.Text = body;
                L_RemakeBody.Visible = body.Length != 0;

                var fabric = info.GetFabricSummary(GameInfo.Strings);
                L_RemakeFabric.Text = fabric;
                L_RemakeFabric.Visible = fabric.Length != 0;
            }
        }

        private void LoadItemTypeValues(ItemKind k, ushort index)
        {
            if (k == ItemKind.Kind_MessageBottle || index >= 60_000)
            {
                CHK_Wrapped.Checked = false;
                CHK_Wrapped.Visible = CHK_Wrapped.Checked = false;
                FLP_Flag1.Visible = true;
                return;
            }

            switch (k)
            {
                case ItemKind.Kind_FossilUnknown:
                    CB_Fossil.SelectedValue = (int) NUD_Count.Value;
                    break;

                case ItemKind.Kind_DIYRecipe:
                    CB_Recipe.SelectedValue = (int)NUD_Count.Value;
                    break;

                case ItemKind.Kind_MessageBottle:
                    CB_Recipe.SelectedValue = (int) NUD_Count.Value;
                    CHK_Wrapped.Visible = CHK_Wrapped.Checked = false;
                    FLP_Flag1.Visible = true;
                    return;
            }

            CHK_Wrapped.Visible  = true;
            FLP_Flag1.Visible = false;
        }

        private void ToggleEditorVisibility(ItemKind k, ushort id)
        {
            if (k.IsFlowerGene(id))
            {
                CB_Recipe.Visible = false;
                FLP_Uses.Visible = FLP_Count.Visible = false;
                FLP_Flower.Visible = true;
                return;
            }

            switch (k)
            {
                case ItemKind.Kind_FossilUnknown:
                    CB_Fossil.Visible = true;

                    CB_Recipe.Visible = false;
                    FLP_Uses.Visible = FLP_Count.Visible = false;
                    FLP_Flower.Visible = false;
                    break;

                case ItemKind.Kind_DIYRecipe:
                    CB_Recipe.Visible = true;

                    CB_Fossil.Visible = false;
                    FLP_Uses.Visible = FLP_Count.Visible = false;
                    FLP_Flower.Visible = false;
                    break;

                case ItemKind.Kind_MessageBottle:
                    CB_Recipe.Visible = true;

                    CB_Fossil.Visible = false;
                    FLP_Uses.Visible = true;
                    FLP_Count.Visible = false;
                    FLP_Flower.Visible = false;
                    break;

                default:
                    CB_Fossil.Visible = false;
                    CB_Recipe.Visible = false;
                    FLP_Uses.Visible = FLP_Count.Visible = true;
                    FLP_Flower.Visible = false;
                    break;
            }
        }

        private void L_Count_DoubleClick(object sender, EventArgs e)
        {
            Item currentItem = SetItem(new Item());
            var result = ItemInfo.TryGetMaxStackCount(currentItem, out var max);
            if (!result)
                return;
            currentItem.Count = (ushort)(max - 1);
            LoadItem(currentItem);
        }

        private void CB_CountAlias_SelectedValueChanged(object sender,EventArgs e)
        {
            var val = WinFormsUtil.GetIndex((ComboBox)sender);
            NUD_Count.Value = Math.Max(0, Math.Min(NUD_Count.Maximum, val));
        }

        private void LoadGenes(FlowerGene genes)
        {
            CHK_R1.Checked = (genes & FlowerGene.R1) != 0;
            CHK_R2.Checked = (genes & FlowerGene.R2) != 0;
            CHK_Y1.Checked = (genes & FlowerGene.Y1) != 0;
            CHK_Y2.Checked = (genes & FlowerGene.Y2) != 0;
            CHK_W1.Checked = (genes & FlowerGene.w1) == 0; // inverted; both bits on = no gene (not white)
            CHK_W2.Checked = (genes & FlowerGene.w2) == 0; // inverted; both bits on = no gene (not white)
            CHK_S1.Checked = (genes & FlowerGene.S1) != 0;
            CHK_S2.Checked = (genes & FlowerGene.S2) != 0;
        }

        private FlowerGene SaveGenes()
        {
            var val = FlowerGene.None;
            if (CHK_R1.Checked) val |= FlowerGene.R1;
            if (CHK_R2.Checked) val |= FlowerGene.R2;
            if (CHK_Y1.Checked) val |= FlowerGene.Y1;
            if (CHK_Y2.Checked) val |= FlowerGene.Y2;
            if (!CHK_W1.Checked) val |= FlowerGene.w1; // inverted; both bits on = no gene (not white)
            if (!CHK_W2.Checked) val |= FlowerGene.w2; // inverted; both bits on = no gene (not white)
            if (CHK_S1.Checked) val |= FlowerGene.S1;
            if (CHK_S2.Checked) val |= FlowerGene.S2;
            return val;
        }

        private void L_WaterDays_Click(object sender, EventArgs e)
        {
            bool value = (ModifierKeys & Keys.Alt) == 0;
            CHK_Gold.Checked = value;
            CHK_IsWatered.Checked = value;
            NUD_WaterDays.Value = value ? 31 : 0;
            foreach (var v in Watered)
                v.Checked = value;
        }

        private void CB_KeyDown(object sender, KeyEventArgs e) => WinFormsUtil.RemoveDropCB(sender, e);

        private void CHK_IsExtension_CheckedChanged(object sender, EventArgs e)
        {
            if (CHK_IsExtension.Checked)
            {
                FLP_Item.Visible = false;
                FLP_Extension.Visible = true;
            }
            else
            {
                FLP_Item.Visible = true;
                FLP_Extension.Visible = false;
            }
        }

        private void ChangeItem(ushort item, ushort count)
        {
            var pb = PB_Item;
            pb.BackColor = ItemColor.GetItemColor(item);
            pb.BackgroundImage = ItemSprite.GetItemSprite(item, count);
        }

        private void CHK_Wrapped_CheckedChanged(object sender, EventArgs e)
        {
            FLP_Wrapped.Visible = CHK_Wrapped.Checked;
            if (CHK_Wrapped.Checked && CB_WrapType.SelectedIndex == 0)
                CB_WrapType.SelectedIndex = (int)ItemWrapping.WrappingPaper;
        }

        private void CB_WrapType_SelectedIndexChanged(object sender, EventArgs e) => CB_WrapColor.Visible = (ItemWrapping)CB_WrapType.SelectedIndex == ItemWrapping.WrappingPaper;

        private void CB_ItemID_TextChanged(object sender, EventArgs e)
        {
            var entered = CB_ItemID.Text;
            var itemNames = AllItems.Where(z => z.Text.Contains(entered)).Take(10).Select(z => z.Text);
            var caption = string.join(Environment.NewLine, itemNames);
            TT_Search.SetToolTip(CB_ItemID, caption);
        }

        private void NUD_Count_ValueChanged(object sender, EventArgs e)
        {
            var itemID = (ushort)WinFormsUtil.GetIndex(CB_ItemID);
            var itemCount = (ushort)NUD_Count.Value;
            ChangeItem(itemID, itemCount);
        }

        private void PB_Item_Click(object sender, EventArgs e)
        {
            // Import if requested
            if (ModifierKeys == Keys.Shift && Clipboard.ContainsText())
            {
                var text = Clipboard.GetText();
                if (!ulong.TryParse(text, NumberStyles.AllowHexSpecifier, CultureInfo.CurrentCulture, out var val))
                    return;
                var import = BitConverter.GetBytes(val).ToClass<Item>();
                LoadItem(import);
                System.Media.SystemSounds.Asterisk.Play();
                return;
            }

            // Otherwise, export
            var item = SetItem(new Item());
            var data = item.ToBytesClass();
            var u64 = BitConverter.ToUInt64(data, 0);
            Clipboard.SetText($"{u64:X16}");
            System.Media.SystemSounds.Asterisk.Play();
        }

        private void InitializeComponent()
        {
            CB_ItemID = new ComboBox();
            NUD_Count = new NumericUpDown();
            L_Count = new Label();
            L_Uses = new Label();
            NUD_Uses = new NumericUpDown();
            L_Flag0 = new Label();
            NUD_Flag0 = new NumericUpDown();
            L_Flag1 = new Label();
            NUD_Flag1 = new NumericUpDown();
            CB_Recipe = new ComboBox();
            FLP_Controls = new StackLayout();
            PB_Item = new PictureBox();
            FLP_Meta = new StackLayout();
            CHK_IsExtension = new CheckBox();
            PAN_DummyExtension = new Panel();
            FLP_Extension = new StackLayout();
            L_ExtensionX = new Label();
            NUD_ExtensionX = new NumericUpDown();
            L_ExtensionY = new Label();
            NUD_ExtensionY = new NumericUpDown();
            CB_Fossil = new ComboBox();
            FLP_Item = new StackLayout();
            FLP_Count = new StackLayout();
            PAN_DummyCount = new Panel();
            L_RemakeBody = new Label();
            L_RemakeFabric = new Label();
            FLP_Uses = new StackLayout();
            FLP_Flag0 = new StackLayout();
            FLP_Flower = new StackLayout();
            FLP_Genetics = new StackLayout();
            CHK_R2 = new CheckBox();
            CHK_R1 = new CheckBox();
            CHK_Y2 = new CheckBox();
            CHK_Y1 = new CheckBox();
            CHK_W2 = new CheckBox();
            CHK_W1 = new CheckBox();
            CHK_S2 = new CheckBox();
            CHK_S1 = new CheckBox();
            PAN_DummyFlower = new Panel();
            FLP_FlowerFlags = new StackLayout();
            CHK_IsWatered = new CheckBox();
            NUD_WaterDays = new NumericUpDown();
            L_WaterDays = new Label();
            CHK_WV2 = new CheckBox();
            CHK_WV1 = new CheckBox();
            CHK_WV0 = new CheckBox();
            CHK_WV5 = new CheckBox();
            CHK_WV4 = new CheckBox();
            CHK_WV3 = new CheckBox();
            CHK_WV8 = new CheckBox();
            CHK_WV7 = new CheckBox();
            CHK_WV6 = new CheckBox();
            CHK_WV9 = new CheckBox();
            CHK_Gold = new CheckBox();
            FLP_Flag1Group = new StackLayout();
            FLP_Flag1 = new StackLayout();
            CHK_Wrapped = new CheckBox();
            FLP_Wrapped = new StackLayout();
            CB_WrapType = new ComboBox();
            CB_WrapColor = new ComboBox();
            CHK_WrapShowName = new CheckBox();
            CHK_Wrap80 = new CheckBox();
            TT_Search = new ToolTip();

            FLP_Controls.Orientation = Orientation.Vertical;
            FLP_Controls.Items.Add(PB_Item);
            FLP_Controls.Items.Add(FLP_Meta);
            FLP_Controls.Items.Add(FLP_Item);
            FLP_Controls.Items.Add(FLP_Flag1Group);

            FLP_Meta.Orientation = Orientation.Vertical;
            FLP_Meta.Items.Add(CB_ItemID);
            FLP_Meta.Items.Add(CHK_IsExtension);
            FLP_Meta.Items.Add(PAN_DummyExtension);
            FLP_Meta.Items.Add(FLP_Extension);
            FLP_Meta.Items.Add(CB_Recipe);
            FLP_Meta.Items.Add(CB_Fossil);

            FLP_Extension.Orientation = Orientation.Horizontal;
            FLP_Extension.Items.Add(L_ExtensionX);
            FLP_Extension.Items.Add(NUD_ExtensionX);
            FLP_Extension.Items.Add(L_ExtensionY);
            FLP_Extension.Items.Add(NUD_ExtensionY);

            FLP_Item.Orientation = Orientation.Vertical;
            FLP_Item.Items.Add(FLP_Count);
            FLP_Item.Items.Add(FLP_Uses);
            FLP_Item.Items.Add(FLP_Flag0);
            FLP_Item.Items.Add(FLP_Flower);

            FLP_Count.Orientation = Orientation.Horizontal;
            FLP_Count.Items.Add(L_Count);
            FLP_Count.Items.Add(NUD_Count);
            FLP_Count.Items.Add(PAN_DummyCount);
            FLP_Count.Items.Add(L_RemakeBody);
            FLP_Count.Items.Add(L_RemakeFabric);

            FLP_Uses.Orientation = Orientation.Horizontal;
            FLP_Uses.Items.Add(L_Uses);
            FLP_Uses.Items.Add(NUD_Uses);

            FLP_Flag0.Orientation = Orientation.Horizontal;
            FLP_Flag0.Items.Add(L_Flag0);
            FLP_Flag0.Items.Add(NUD_Flag0);

            FLP_Flower.Orientation = Orientation.Vertical;
            FLP_Flower.Items.Add(FLP_Genetics);
            FLP_Flower.Items.Add(PAN_DummyFlower);
            FLP_Flower.Items.Add(FLP_FlowerFlags);

            FLP_Genetics.Orientation = Orientation.Horizontal;
            FLP_Genetics.Items.Add(CHK_R2);
            FLP_Genetics.Items.Add(CHK_R1);
            FLP_Genetics.Items.Add(CHK_Y2);
            FLP_Genetics.Items.Add(CHK_Y1);
            FLP_Genetics.Items.Add(CHK_W2);
            FLP_Genetics.Items.Add(CHK_W1);
            FLP_Genetics.Items.Add(CHK_S2);
            FLP_Genetics.Items.Add(CHK_S1);

            FLP_FlowerFlags.Orientation = Orientation.Horizontal;
            FLP_FlowerFlags.Items.Add(CHK_IsWatered);
            FLP_FlowerFlags.Items.Add(NUD_WaterDays);
            FLP_FlowerFlags.Items.Add(L_WaterDays);
            FLP_FlowerFlags.Items.Add(CHK_WV2);
            FLP_FlowerFlags.Items.Add(CHK_WV1);
            FLP_FlowerFlags.Items.Add(CHK_WV0);
            FLP_FlowerFlags.Items.Add(CHK_WV5);
            FLP_FlowerFlags.Items.Add(CHK_WV4);
            FLP_FlowerFlags.Items.Add(CHK_WV3);
            FLP_FlowerFlags.Items.Add(CHK_WV8);
            FLP_FlowerFlags.Items.Add(CHK_WV7);
            FLP_FlowerFlags.Items.Add(CHK_WV6);
            FLP_FlowerFlags.Items.Add(CHK_WV9);
            FLP_FlowerFlags.Items.Add(CHK_Gold);

            FLP_Flag1Group.Orientation = Orientation.Vertical;
            FLP_Flag1Group.Items.Add(FLP_Flag1);
            FLP_Flag1Group.Items.Add(CHK_Wrapped);
            FLP_Flag1Group.Items.Add(FLP_Wrapped);

            FLP_Flag1.Orientation = Orientation.Horizontal;
            FLP_Flag1.Items.Add(L_Flag1);
            FLP_Flag1.Items.Add(NUD_Flag1);

            FLP_Wrapped.Orientation = Orientation.Vertical;
            FLP_Wrapped.Items.Add(CB_WrapType);
            FLP_Wrapped.Items.Add(CB_WrapColor);
            FLP_Wrapped.Items.Add(CHK_WrapShowName);
            FLP_Wrapped.Items.Add(CHK_Wrap80);

            Content = FLP_Controls;
        }

        private ComboBox CB_ItemID;
        private NumericUpDown NUD_Count;
        private Label L_Count;
        private Label L_Uses;
        private NumericUpDown NUD_Uses;
        private Label L_Flag0;
        private NumericUpDown NUD_Flag0;
        private Label L_Flag1;
        private NumericUpDown NUD_Flag1;
        private ComboBox CB_Recipe;
        private StackLayout FLP_Controls;
        private PictureBox PB_Item;
        private StackLayout FLP_Meta;
        private CheckBox CHK_IsExtension;
        private Panel PAN_DummyExtension;
        private StackLayout FLP_Extension;
        private Label L_ExtensionX;
        private NumericUpDown NUD_ExtensionX;
        private Label L_ExtensionY;
        private NumericUpDown NUD_ExtensionY;
        private ComboBox CB_Fossil;
        private StackLayout FLP_Item;
        private StackLayout FLP_Count;
        private Panel PAN_DummyCount;
        private Label L_RemakeBody;
        private Label L_RemakeFabric;
        private StackLayout FLP_Uses;
        private StackLayout FLP_Flag0;
        private StackLayout FLP_Flower;
        private StackLayout FLP_Genetics;
        private CheckBox CHK_R2;
        private CheckBox CHK_R1;
        private CheckBox CHK_Y2;
        private CheckBox CHK_Y1;
        private CheckBox CHK_W2;
        private CheckBox CHK_W1;
        private CheckBox CHK_S2;
        private CheckBox CHK_S1;
        private Panel PAN_DummyFlower;
        private StackLayout FLP_FlowerFlags;
        private CheckBox CHK_IsWatered;
        private NumericUpDown NUD_WaterDays;
        private Label L_WaterDays;
        private CheckBox CHK_WV2;
        private CheckBox CHK_WV1;
        private CheckBox CHK_WV0;
        private CheckBox CHK_WV5;
        private CheckBox CHK_WV4;
        private CheckBox CHK_WV3;
        private CheckBox CHK_WV8;
        private CheckBox CHK_WV7;
        private CheckBox CHK_WV6;
        private CheckBox CHK_WV9;
        private CheckBox CHK_Gold;
        private StackLayout FLP_Flag1Group;
        private StackLayout FLP_Flag1;
        private CheckBox CHK_Wrapped;
        private StackLayout FLP_Wrapped;
        private ComboBox CB_WrapType;
        private ComboBox CB_WrapColor;
        private CheckBox CHK_WrapShowName;
        private CheckBox CHK_Wrap80;
        private ToolTip TT_Search;
    }
}
