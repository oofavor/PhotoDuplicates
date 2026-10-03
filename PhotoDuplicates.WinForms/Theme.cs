namespace PhotoDuplicates.WinForms
{
    internal static class Theme
    {
        private static readonly Color DarkBack = Color.FromArgb(32, 32, 32);
        private static readonly Color DarkPanel = Color.FromArgb(45, 45, 48);
        private static readonly Color DarkText = Color.FromArgb(230, 230, 230);

        // список всегда светлый: цвет заголовков групп задаёт Windows, на тёмном фоне его не видно

        public static readonly Color MarkedText = Color.Firebrick;

        public static readonly Color BestText = Color.DarkGreen;

        public static readonly Color NormalText = SystemColors.WindowText;

        public static void Apply(Control control, bool dark)
        {
            if (control is ListView)
            {
                control.BackColor = SystemColors.Window;
                control.ForeColor = SystemColors.WindowText;
            }
            else if (control is TextBox || control is NumericUpDown || control is ComboBox)
            {
                control.BackColor = dark ? DarkBack : SystemColors.Window;
                control.ForeColor = dark ? DarkText : SystemColors.WindowText;
            }
            else if (control is Button button)
            {
                button.BackColor = dark ? DarkPanel : SystemColors.Control;
                button.ForeColor = dark ? DarkText : SystemColors.ControlText;
                button.FlatStyle = dark ? FlatStyle.Flat : FlatStyle.Standard;
            }
            else if (control is ToolStrip strip)
            {
                // у меню красим и сами полосы, и все пункты
                strip.BackColor = dark ? DarkPanel : SystemColors.Control;
                strip.ForeColor = dark ? DarkText : SystemColors.ControlText;
                ApplyToItems(strip.Items, dark);
            }
            else
            {
                control.BackColor = dark ? DarkPanel : SystemColors.Control;
                control.ForeColor = dark ? DarkText : SystemColors.ControlText;
            }

            foreach (Control child in control.Controls)
            {
                Apply(child, dark);
            }
        }

        private static void ApplyToItems(ToolStripItemCollection items, bool dark)
        {
            foreach (ToolStripItem item in items)
            {
                item.BackColor = dark ? DarkPanel : SystemColors.Control;
                item.ForeColor = dark ? DarkText : SystemColors.ControlText;

                if (item is ToolStripMenuItem menuItem)
                {
                    ApplyToItems(menuItem.DropDownItems, dark);
                }
            }
        }
    }
}
