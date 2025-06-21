using System.Drawing.Drawing2D;

namespace BGMBackend.Injector
{
    internal class LightBlueRenderer : ToolStripProfessionalRenderer
    {
        private readonly Color _colorMenuArrow = Color.FromArgb(0, 122, 204);
        private readonly Color _colorCheckSquare = Color.FromArgb(0, 122, 204);
        private readonly Color _colorCheckMark = Color.FromArgb(19, 69, 99);
        private readonly Color _colorMenuItemText = Color.FromArgb(33, 33, 33);

        public LightBlueRenderer() : base(new MenuStripColorTable()) { }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            if (e is not null)
            {
                e.ArrowColor = _colorMenuArrow;
            }
            base.OnRenderArrow(e!);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            if (e is not null)
            {
                var textRect = e.TextRectangle;
                textRect.Height = e.Item.Height - 4; // 4 is the default difference between the item height and the text rectangle height

                e.TextRectangle = textRect;
                e.TextFormat = TextFormatFlags.VerticalCenter;
                e.TextColor = _colorMenuItemText;
            }
            base.OnRenderItemText(e!);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (e is null || !e.Item.Enabled)
                return;
            base.OnRenderMenuItemBackground(e);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            if (e is not null)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                var rectImage = new Rectangle(e.ImageRectangle.Location, e.ImageRectangle.Size);
                rectImage.Inflate(-1, -1);

                using (var p = new Pen(_colorCheckSquare, 1))
                {
                    g.DrawRectangle(p, rectImage);
                }

                var rectCheck = rectImage;
                rectCheck.Width = rectImage.Width - 6;
                rectCheck.Height = rectImage.Height - 8;
                rectCheck.X += 3;
                rectCheck.Y += 4;

                using (var p = new Pen(_colorCheckMark, 2))
                {
                    g.DrawLines(p,
                    [
                        new Point(rectCheck.Left, rectCheck.Bottom - Convert.ToInt32(rectCheck.Height / (double)2)),
                        new Point(rectCheck.Left + Convert.ToInt32(rectCheck.Width / (double)3), rectCheck.Bottom),
                        new Point(rectCheck.Right, rectCheck.Top)
                    ]);
                }
            }
        }

        private class MenuStripColorTable : ProfessionalColorTable
        {
            private readonly Color _colorMenuBorder = Color.FromArgb(204, 206, 219);
            private readonly Color _colorMenuItemSelected = Color.FromArgb(201, 222, 245);
            private readonly Color _colorBackground = Color.FromArgb(246, 246, 246);
            private readonly Color _colorSeparator = Color.FromArgb(224, 227, 230);
            private readonly Color _colorStatusStripGradient = Color.FromArgb(234, 237, 241);
            private readonly Color _colorButtonSelected = Color.FromArgb(88, 146, 226);
            private readonly Color _colorButtonPressed = Color.FromArgb(110, 160, 230);

            public override Color ToolStripDropDownBackground => _colorBackground;

            public override Color MenuStripGradientBegin => _colorBackground;

            public override Color MenuStripGradientEnd => _colorBackground;

            public override Color CheckBackground => _colorBackground;

            public override Color CheckPressedBackground => _colorBackground;

            public override Color CheckSelectedBackground => _colorBackground;

            public override Color MenuItemSelected => _colorMenuItemSelected;

            public override Color ImageMarginGradientBegin => _colorBackground;

            public override Color ImageMarginGradientMiddle => _colorBackground;

            public override Color ImageMarginGradientEnd => _colorBackground;

            public override Color MenuItemBorder => _colorMenuItemSelected;

            public override Color MenuBorder => _colorMenuBorder;

            public override Color SeparatorDark => _colorSeparator;

            public override Color SeparatorLight => _colorSeparator;

            public override Color StatusStripGradientBegin => _colorStatusStripGradient;

            public override Color StatusStripGradientEnd => _colorStatusStripGradient;

            public override Color ButtonSelectedGradientBegin => _colorButtonSelected;

            public override Color ButtonSelectedGradientMiddle => _colorButtonSelected;

            public override Color ButtonSelectedGradientEnd => _colorButtonSelected;

            public override Color ButtonSelectedBorder => _colorButtonSelected;

            public override Color ButtonPressedGradientBegin => _colorButtonPressed;

            public override Color ButtonPressedGradientMiddle => _colorButtonPressed;

            public override Color ButtonPressedGradientEnd => _colorButtonPressed;

            public override Color ButtonPressedBorder => _colorButtonPressed;
        }
    }


    public class DarkBlueRenderer : ToolStripProfessionalRenderer
    {
        private readonly Color _colorMenuArrow = Color.FromArgb(237, 237, 237);
        private readonly Color _colorCheckSquare = Color.FromArgb(0, 122, 204);
        private readonly Color _colorCheckMark = Color.FromArgb(237, 237, 237);
        private readonly Color _colorMenuItemText = Color.FromArgb(237, 237, 237);

        public DarkBlueRenderer() : base(new MenuStripColorTable()) { }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            if (e != null)
                e.ArrowColor = _colorMenuArrow;
            base.OnRenderArrow(e!);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            if (e != null)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                var rectImage = new Rectangle(e.ImageRectangle.Location, e.ImageRectangle.Size);
                rectImage.Inflate(-1, -1);

                using (var p = new Pen(_colorCheckSquare, 1))
                {
                    g.DrawRectangle(p, rectImage);
                }

                var rectCheck = rectImage;
                rectCheck.Width = rectImage.Width - 6;
                rectCheck.Height = rectImage.Height - 8;
                rectCheck.X += 3;
                rectCheck.Y += 4;

                using (var p = new Pen(_colorCheckMark, 2))
                {
                    g.DrawLines(p,
                    [
                        new Point(rectCheck.Left, rectCheck.Bottom - System.Convert.ToInt32(rectCheck.Height / (double)2)),
                        new Point(rectCheck.Left + System.Convert.ToInt32(rectCheck.Width / (double)3), rectCheck.Bottom),
                        new Point(rectCheck.Right, rectCheck.Top)
                    ]);
                }
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            if (e is not null)
            {
                var textRect = e.TextRectangle;
                textRect.Height = e.Item.Height - 4; // 4 is the default difference between the item height and the text rectangle height

                e.TextRectangle = textRect;
                e.TextFormat = TextFormatFlags.VerticalCenter;
                e.TextColor = _colorMenuItemText;
            }

            base.OnRenderItemText(e!);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (e is null || !e.Item.Enabled)
                return;
            base.OnRenderMenuItemBackground(e);
        }

        private class MenuStripColorTable : ProfessionalColorTable
        {
            private readonly Color _colorMenuBorder = Color.FromArgb(61, 61, 67);
            private readonly Color _colorMenuItemSelected = Color.FromArgb(76, 76, 77);
            private readonly Color _colorBackground = Color.FromArgb(43, 43, 43);
            private readonly Color _colorSeparator = Color.FromArgb(61, 61, 67);
            private readonly Color _colorStatusStripGradient = Color.FromArgb(234, 237, 241);
            private readonly Color _colorButtonSelected = Color.FromArgb(88, 146, 226);
            private readonly Color _colorButtonPressed = Color.FromArgb(110, 160, 230);

            public override Color ToolStripDropDownBackground => _colorBackground;

            public override Color MenuStripGradientBegin => _colorBackground;

            public override Color MenuStripGradientEnd => _colorBackground;

            public override Color CheckBackground => _colorBackground;

            public override Color CheckPressedBackground => _colorBackground;

            public override Color CheckSelectedBackground => _colorBackground;

            public override Color MenuItemSelected => _colorMenuItemSelected;

            public override Color ImageMarginGradientBegin => _colorBackground;

            public override Color ImageMarginGradientMiddle => _colorBackground;

            public override Color ImageMarginGradientEnd => _colorBackground;

            public override Color MenuItemBorder => _colorMenuItemSelected;

            public override Color MenuBorder => _colorMenuBorder;

            public override Color SeparatorDark => _colorSeparator;

            public override Color SeparatorLight => _colorSeparator;

            public override Color StatusStripGradientBegin => _colorStatusStripGradient;

            public override Color StatusStripGradientEnd => _colorStatusStripGradient;

            public override Color ButtonSelectedGradientBegin => _colorButtonSelected;

            public override Color ButtonSelectedGradientMiddle => _colorButtonSelected;

            public override Color ButtonSelectedGradientEnd => _colorButtonSelected;

            public override Color ButtonSelectedBorder => _colorButtonSelected;

            public override Color ButtonPressedGradientBegin => _colorButtonPressed;

            public override Color ButtonPressedGradientMiddle => _colorButtonPressed;

            public override Color ButtonPressedGradientEnd => _colorButtonPressed;

            public override Color ButtonPressedBorder => _colorButtonPressed;
        }
    }
}
