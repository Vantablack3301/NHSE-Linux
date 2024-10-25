using System;
using System.Drawing;
using Eto.Forms;
using NHSE.Core;

namespace NHSE.WinForms
{
    public partial class AchievementRow : Panel
    {
        private Label L_Threshold;
        private CheckBox CHK_Read;
        private DateTimePicker CAL_Date;

        public AchievementRow()
        {
            InitializeComponent();
            CAL_Date.MinDate = DateTime.MinValue;
        }

        public void LoadRow(AchievementList list, in int index, in int row)
        {
            if (!LifeSupportAchievement.List.TryGetValue(index, out var detail))
            {
                L_Threshold.Text = "N/A";
            }
            else
            {
                if (row >= detail.AchievementCount)
                {
                    L_Threshold.Text = string.Empty;
                }
                else
                {
                    var threshold = detail.GetThresholdValue(row);
                    L_Threshold.Text = threshold <= 0 ? "N/A" : threshold.ToString();
                }
            }

            var date = list.Date[index][row];
            if (date < CAL_Date.MinDate)
                date = CAL_Date.MinDate;
            CAL_Date.Value = date;

            CHK_Read.Checked = list.Read[index][row];
        }

        public void SaveRow(AchievementList list, in int index, in int row)
        {
            list.Date[index][row] = CAL_Date.Value;
            list.Read[index][row] = CHK_Read.Checked;
        }

        public void ChangeCount(in int index, in int row, in uint count)
        {
            if (!LifeSupportAchievement.List.TryGetValue(index, out var detail))
                return;

            bool satisfied = detail.GetIsSatisfied(row, count);
            L_Threshold.TextColor = satisfied ? Colors.Red : CHK_Read.TextColor;
        }

        private void CAL_Date_MouseDown(object sender, MouseEventArgs e)
        {
            if ((Keyboard.Modifiers & Keys.Alt) != 0 && e.Buttons == MouseButtons.Primary)
                CAL_Date.Value = CAL_Date.MinDate;
        }

        private void InitializeComponent()
        {
            L_Threshold = new Label
            {
                Location = new Point(3, 0),
                Size = new Size(80, 20),
                Text = "1",
                TextAlignment = TextAlignment.Right
            };

            CHK_Read = new CheckBox
            {
                Location = new Point(295, 3),
                Size = new Size(52, 17),
                Text = "Read"
            };

            CAL_Date = new DateTimePicker
            {
                Location = new Point(89, 0),
                Size = new Size(200, 20)
            };
            CAL_Date.MouseDown += CAL_Date_MouseDown;

            Content = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Items =
                {
                    L_Threshold,
                    CHK_Read,
                    CAL_Date
                }
            };
        }
    }
}
