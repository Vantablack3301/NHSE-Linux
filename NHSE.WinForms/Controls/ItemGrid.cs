using System.Collections.Generic;
using System.Diagnostics;
using Eto.Drawing;
using Eto.Forms;
using NHSE.Core;

namespace NHSE.WinForms
{
    public partial class ItemGrid : Panel
    {
        public ItemGrid()
        {
            InitializeComponent();
        }

        public readonly List<ImageView> Entries = new();
        public int Slots { get; private set; }

        private int sizeW = 32;
        private int sizeH = 32;

        public void InitializeGrid(int width, int height, IGridItem info)
        {
            sizeW = info.Width;
            sizeH = info.Height;
            Generate(width, height);
            Slots = width * height;
        }

        private const int padEdge = 0; // edges
        private const int border = 1; // between

        private void Generate(int width, int height)
        {
            var layout = new DynamicLayout();
            layout.BeginVertical();
            Entries.Clear();

            int colWidth = sizeW;
            int rowHeight = sizeH;

            for (int row = 0; row < height; row++)
            {
                layout.BeginHorizontal();
                for (int column = 0; column < width; column++)
                {
                    var iv = GetControl(sizeW, sizeH);
                    layout.Add(iv);
                    Entries.Add(iv);
                }
                layout.EndHorizontal();
            }
            layout.EndVertical();

            Content = layout;
            Width = (2 * padEdge) + border + (width * (colWidth + border)) + 2;
            Height = (2 * padEdge) + border + (height * (rowHeight + border)) + 2;
            Debug.WriteLine($"{Name} -- Width: {Width}, Height: {Height}");
        }

        public static ImageView GetControl(int width, int height)
        {
            return new ImageView
            {
                Size = new Size(width + (2 * 1), height + (2 * 1)),
                BackgroundColor = Colors.Transparent,
                ImageInterpolation = ImageInterpolation.High,
                Border = BorderType.Line,
                Padding = Padding.Empty,
                Margin = Padding.Empty,
            };
        }
    }
}
