using System;
using System.Collections.Generic;
using System.Linq;
using Eto.Drawing;
using Eto.Forms;
using NHSE.Core;
using NHSE.Sprites;

namespace NHSE.WinForms
{
    public partial class ItemGridEditor : Panel
    {
        private static readonly GridSize Sprites = new();
        private readonly ItemEditor Editor;
        private readonly IReadOnlyList<Item> Items;

        private IList<ImageView> SlotImageViews = Array.Empty<ImageView>();
        private int Count => Items.Count;
        private int Page;
        private int ItemsPerPage;
        public Action? ItemChanged { private get; set; }
        private void ItemUpdated() => ItemChanged?.Invoke();

        public ItemGridEditor(ItemEditor editor, IReadOnlyList<Item> items)
        {
            Editor = editor;
            Items = items;
            InitializeComponent();

            L_ItemName.Text = string.Empty;
        }

        public void InitializeGrid(int width, int height, int itemWidth, int itemHeight)
        {
            Sprites.Width = itemWidth;
            Sprites.Height = itemHeight;
            ItemsPerPage = width * height;
            ItemGrid.InitializeGrid(width, height, Sprites);
            InitializeSlots();
        }

        private void InitializeSlots()
        {
            SlotImageViews = ItemGrid.Entries;
            foreach (var iv in SlotImageViews)
            {
                iv.MouseEnter += Slot_MouseEnter;
                iv.MouseLeave += Slot_MouseLeave;
                iv.MouseDown += Slot_MouseClick;
                iv.MouseWheel += Slot_MouseWheel;
                iv.ContextMenu = CM_Hand;
            }
            ChangePage();
        }

        private void Slot_MouseWheel(object? sender, MouseEventArgs e)
        {
            var delta = e.Delta.Height < 0 ? 1 : -1; // scrolling down increases page #
            var newpage = Math.Min(PageCount - 1, Math.Max(0, Page + delta));
            if (newpage == Page)
                return;
            Page = newpage;
            ChangePage();
        }

        public void Slot_MouseEnter(object? sender, EventArgs e)
        {
            if (sender is not ImageView iv)
                return;
            var index = SlotImageViews.IndexOf(iv);
            var item = GetItem(index);

            var text = GetItemText(item);
            HoverTip.SetToolTip(iv, text);
            L_ItemName.Text = text;
        }

        public void Slot_MouseLeave(object? sender, EventArgs e)
        {
            if (sender is not ImageView)
                return;
            L_ItemName.Text = string.Empty;
            HoverTip.RemoveAll();
        }

        public static string GetItemText(Item item) => GameInfo.Strings.GetItemName(item);

        public void Slot_MouseClick(object? sender, MouseEventArgs e)
        {
            if (sender == null)
                return;
            switch (Keyboard.Modifiers)
            {
                case Keys.Control | Keys.Alt: ClickClone(sender, e); break;
                case Keys.Control: ClickView(sender, e); break;
                case Keys.Shift: ClickSet(sender, e); break;
                case Keys.Alt: ClickDelete(sender, e); break;
                default:
                    return;
            }
            // restart hovering since the mouse event isn't fired
            Slot_MouseEnter(sender, e);
        }

        public Item LoadItem(int index) => Editor.LoadItem(GetItem(index));
        public Item SetItem(int index) => Editor.SetItem(GetItem(index));

        private Item GetItem(int index)
        {
            index += Page * ItemsPerPage;
            return Items[index];
        }

        private void ClickView(object sender, EventArgs e)
        {
            var iv = WinFormsUtil.GetUnderlyingControl<ImageView>(sender);
            if (iv == null)
                return;
            var index = SlotImageViews.IndexOf(iv);
            LoadItem(index);
        }

        private void ClickSet(object sender, EventArgs e)
        {
            var iv = WinFormsUtil.GetUnderlyingControl<ImageView>(sender);
            if (iv == null)
                return;
            var index = SlotImageViews.IndexOf(iv);
            var item = SetItem(index);
            SetItemSprite(item, iv);
            ItemUpdated();
        }

        private void ClickDelete(object sender, EventArgs e)
        {
            var iv = WinFormsUtil.GetUnderlyingControl<ImageView>(sender);
            if (iv == null)
                return;
            var index = SlotImageViews.IndexOf(iv);
            var item = GetItem(index);
            item.Delete();
            SetItemSprite(item, iv);
            ItemUpdated();
        }

        private void ClickClone(object sender, EventArgs e)
        {
            var iv = WinFormsUtil.GetUnderlyingControl<ImageView>(sender);
            if (iv == null)
                return;
            var index = SlotImageViews.IndexOf(iv);
            var item = GetItem(index);
            for (int i = 0; i < SlotImageViews.Count; i++)
            {
                if (i == index)
                    continue;
                var dest = GetItem(i);
                dest.CopyFrom(item);
                SetItemSprite(item, SlotImageViews[i]);
                ItemUpdated();
            }
            System.Media.SystemSounds.Asterisk.Play();
        }

        private void SetItemSprite(Item item, ImageView iv)
        {
            var dw = Sprites.Width;
            var dh = Sprites.Height;
            var font = L_ItemName.Font;
            iv.BackgroundColor = ItemColor.GetItemColor(item);
            iv.Image = ItemSprite.GetItemSprite(item);
            var backing = new Bitmap(dw, dh);
            iv.Image = ItemSprite.GetItemMarkup(item, font, dw, dh, backing);
        }

        private static int GetPageJump()
        {
            return Keyboard.Modifiers switch
            {
                Keys.Control => 10,
                Keys.Alt => 25,
                Keys.Shift => 1000,
                _ => 1
            };
        }

        private void B_Up_Click(object sender, EventArgs e)
        {
            if (Page == 0)
                return;
            Page = Math.Max(0, Page - GetPageJump());
            ChangePage();
        }

        private void B_Down_Click(object sender, EventArgs e)
        {
            if (ItemsPerPage * (Page + 1) == Count)
                return;
            Page = Math.Min(PageCount - 1, Page + GetPageJump());
            ChangePage();
        }

        private int PageCount => Count / ItemsPerPage;

        private void ChangePage()
        {
            bool hasPages = Count > ItemsPerPage;
            B_Up.Visible = hasPages && Page > 0;
            B_Down.Visible = hasPages && Page < PageCount;
            L_Page.Visible = hasPages;

            L_Page.Text = $"{Page + 1}/{Count / ItemsPerPage}";
            LoadItems();
        }

        public void LoadItems()
        {
            for (int i = 0; i < SlotImageViews.Count; i++)
            {
                var item = GetItem(i);
                SetItemSprite(item, SlotImageViews[i]);
            }
            ItemUpdated();
        }

        private static void ShowContextMenuBelow(ContextMenu c, Control n) => c.Show(n.PointToScreen(new Point(0, n.Height)));
        private void B_Clear_Click(object sender, EventArgs e) => ShowContextMenuBelow(CM_Remove, B_Clear);

        private void ClearItemIf(Func<Item, bool> criteria)
        {
            bool all = Keyboard.Modifiers == Keys.Shift;
            int start = 0, end = Items.Count - 1;
            if (!all)
            {
                start = ItemsPerPage * Page;
                end = start + ItemsPerPage;
            }
            for (int i = start; i < end; i++)
            {
                var item = Items[i];
                if (criteria(item))
                    item.Delete();
            }
            LoadItems();
            System.Media.SystemSounds.Asterisk.Play();
        }

        private void B_Sort_Click(object sender, EventArgs e) => ShowContextMenuBelow(CM_Sort, B_Sort);
        private void B_SortAlpha_Click(object sender, EventArgs e)
        {
            var sortedItems = Items.Where(item => item.ItemId != Item.NONE)
                .OrderBy(item => GetItemText(item).ToLower());
            var sortedItemsCopy = new List<Item>(); // to prevent object reference issues

            foreach(var item in sortedItems)
            {
                var itemCopy = new Item();
                itemCopy.CopyFrom(item);
                sortedItemsCopy.Add(itemCopy);
            }

            SetEditorItems(sortedItemsCopy);
        }

        private void B_SortType_Click(object sender, EventArgs e)
        {
            var sortedItems = Items.Where(item => item.ItemId != Item.NONE)
                .OrderBy(ItemInfo.GetItemKind)
                .ThenBy(item => GetItemText(item).ToLower());
            var sortedItemsCopy = new List<Item>(); // to prevent object reference issues

            foreach (var item in sortedItems)
            {
                var itemCopy = new Item();
                itemCopy.CopyFrom(item);
                sortedItemsCopy.Add(itemCopy);
            }

            SetEditorItems(sortedItemsCopy);
        }

        private void SetEditorItems(IReadOnlyList<Item> items)
        {
            if (items.Count > Items.Count)
                return;

            for (int i = 0; i < Items.Count; i++)
            {
                var src = i < items.Count ? items[i] : Item.NO_ITEM;
                GetItem(i).CopyFrom(src);
                ItemUpdated();
            }

            LoadItems();
            Editor.LoadItem(Item.NO_ITEM);
            System.Media.SystemSounds.Asterisk.Play();
        }

        private void B_ClearAll_Click(object sender, EventArgs e) => ClearItemIf(_ => true);
        private void B_ClearClothing_Click(object sender, EventArgs e) => ClearItemIf(z => ItemInfo.GetItemKind(z).IsClothing());
        private void B_ClearCrafting_Click(object sender, EventArgs e) => ClearItemIf(z => ItemInfo.GetItemKind(z).IsCrafting());
        private void B_ClearFurniture_Click(object sender, EventArgs e) => ClearItemIf(z => ItemInfo.GetItemKind(z).IsFurniture());
        private void B_ClearBugs_Click(object sender, EventArgs e) => ClearItemIf(z => GameLists.Bugs.Contains(z.ItemId));
        private void B_ClearFish_Click(object sender, EventArgs e) => ClearItemIf(z => GameLists.Fish.Contains(z.ItemId));
        private void B_ClearDive_Click(object sender, EventArgs e) => ClearItemIf(z => GameLists.Dive.Contains(z.ItemId));

        private class GridSize : IGridItem
        {
            public int Width { get; set; } = 64;
            public int Height { get; set; } = 64;
        }

        private Label L_ItemName;
        private ItemGrid ItemGrid;
        private Button B_Up;
        private Button B_Down;
        private Label L_Page;
        private ContextMenu CM_Hand;
        private ContextMenu CM_Sort;
        private Button B_Clear;
        private Button B_Sort;
        private ContextMenu CM_Remove;
        private ToolTip HoverTip;

        private void InitializeComponent()
        {
            L_ItemName = new Label();
            ItemGrid = new ItemGrid();
            B_Up = new Button();
            B_Down = new Button();
            L_Page = new Label();
            CM_Hand = new ContextMenu();
            CM_Sort = new ContextMenu();
            B_Clear = new Button();
            B_Sort = new Button();
            CM_Remove = new ContextMenu();
            HoverTip = new ToolTip();

            var layout = new DynamicLayout();
            layout.BeginVertical();
            layout.Add(L_ItemName);
            layout.Add(ItemGrid);
            layout.Add(B_Up);
            layout.Add(L_Page);
            layout.Add(B_Down);
            layout.Add(B_Clear);
            layout.Add(B_Sort);
            layout.EndVertical();

            Content = layout;
        }
    }
}
