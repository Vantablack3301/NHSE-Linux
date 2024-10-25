using System;
using System.Collections.Generic;
using System.Linq;
using Eto.Forms;
using NHSE.Core;

namespace NHSE.WinForms
{
    public partial class RestrictedItemSelect : Panel
    {
        private IList<ComboItem> DataSource = Array.Empty<ComboItem>();

        public RestrictedItemSelect() => InitializeComponent();

        public void Initialize(IList<ComboItem> items, bool canType = false)
        {
            CB_ItemID.DataStore = DataSource = items;

            if (!canType)
                CB_ItemID.ReadOnly = true;
        }

        public ushort Value
        {
            get => CHK_CustomItem.Checked ? (ushort) NUD_CustomItem.Value : (ushort) WinFormsUtil.GetIndex(CB_ItemID);
            set
            {
                if (DataSource.Any(z => z.Value == value))
                {
                    CHK_CustomItem.Checked = false;
                    CB_ItemID.SelectedValue = (int)value;
                }
                else
                {
                    CHK_CustomItem.Checked = true;
                    NUD_CustomItem.Value = value;
                }
            }
        }

        private void CHK_Custom_CheckedChanged(object sender, EventArgs e)
        {
            CB_ItemID.Enabled = !CHK_CustomItem.Checked;
            NUD_CustomItem.Enabled = CHK_CustomItem.Checked;
        }

        private void CB_ItemID_SelectedValueChanged(object sender, EventArgs e)
        {
            NUD_CustomItem.Value = WinFormsUtil.GetIndex(CB_ItemID);
        }

        private void InitializeComponent()
        {
            CB_ItemID = new DropDown();
            NUD_CustomItem = new NumericUpDown();
            CHK_CustomItem = new CheckBox();

            var layout = new DynamicLayout();
            layout.BeginVertical();
            layout.Add(CB_ItemID);
            layout.Add(NUD_CustomItem);
            layout.Add(CHK_CustomItem);
            layout.EndVertical();

            Content = layout;
        }

        private DropDown CB_ItemID;
        private NumericUpDown NUD_CustomItem;
        private CheckBox CHK_CustomItem;
    }
}
