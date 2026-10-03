using PhotoDuplicates.Core;

namespace PhotoDuplicates.WinForms
{
    public class SettingsForm : Form
    {
        private static readonly int[] ThumbnailSizes = { 96, 128, 192, 256 };

        private readonly ComboBox thumbnailBox = new ComboBox();
        private readonly CheckBox darkThemeBox = new CheckBox();
        private readonly CheckBox useCacheBox = new CheckBox();
        private readonly Label cacheInfoLabel = new Label();
        private readonly HashCache cache;

        public AppSettings Result { get; private set; }

        public SettingsForm(AppSettings settings, HashCache cache)
        {
            this.cache = cache;
            Result = settings.Clone();

            Text = "Настройки";
            Font = new Font("Segoe UI", 10);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;

            var table = new TableLayoutPanel();
            table.ColumnCount = 2;
            table.AutoSize = true;
            table.Padding = new Padding(12);

            // размер миниатюр
            thumbnailBox.DropDownStyle = ComboBoxStyle.DropDownList;
            foreach (int size in ThumbnailSizes)
            {
                thumbnailBox.Items.Add(size + " пикселей");
            }
            thumbnailBox.SelectedIndex = FindSizeIndex(settings.ThumbnailSize);
            AddRow(table, "Размер миниатюр:", thumbnailBox);

            // тема
            darkThemeBox.Text = "Тёмная тема";
            darkThemeBox.AutoSize = true;
            darkThemeBox.Checked = settings.DarkTheme;
            AddRow(table, "Оформление:", darkThemeBox);

            // кеш
            useCacheBox.Text = "Запоминать хеши (повторный поиск будет быстрым)";
            useCacheBox.AutoSize = true;
            useCacheBox.Checked = settings.UseCache;
            AddRow(table, "Кеш:", useCacheBox);

            cacheInfoLabel.AutoSize = true;
            cacheInfoLabel.MaximumSize = new Size(520, 0); // длинный путь переносим
            UpdateCacheInfo();
            AddRow(table, "", cacheInfoLabel);

            var clearCacheButton = new Button { Text = "Очистить кеш", AutoSize = true };
            clearCacheButton.Click += ClearCacheButton_Click;
            AddRow(table, "", clearCacheButton);

            // кнопки
            var okButton = new Button { Text = "OK", AutoSize = true, MinimumSize = new Size(90, 0) };
            okButton.Click += OkButton_Click;
            var cancelButton = new Button { Text = "Отмена", AutoSize = true, MinimumSize = new Size(90, 0) };
            cancelButton.DialogResult = DialogResult.Cancel;

            var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill };
            buttons.Controls.Add(cancelButton);
            buttons.Controls.Add(okButton);
            int lastRow = table.RowCount;
            table.RowCount = lastRow + 1;
            table.Controls.Add(buttons, 0, lastRow);
            table.SetColumnSpan(buttons, 2);

            AcceptButton = okButton;
            CancelButton = cancelButton;
            Controls.Add(table);
        }

        private static void AddRow(TableLayoutPanel table, string labelText, Control control)
        {
            int row = table.RowCount;
            table.RowCount = row + 1;
            table.Controls.Add(new Label { Text = labelText, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
            table.Controls.Add(control, 1, row);
        }

        private static int FindSizeIndex(int size)
        {
            for (int i = 0; i < ThumbnailSizes.Length; i++)
            {
                if (ThumbnailSizes[i] == size)
                {
                    return i;
                }
            }
            return 1; // 128 по умолчанию
        }

        private void UpdateCacheInfo()
        {
            cache.Load();
            cacheInfoLabel.Text = $"В кеше файлов: {cache.Count}\nФайл кеша: {cache.FilePath}";
        }

        private void ClearCacheButton_Click(object? sender, EventArgs e)
        {
            cache.Clear();
            cache.Save();
            UpdateCacheInfo();
        }

        private void OkButton_Click(object? sender, EventArgs e)
        {
            Result.ThumbnailSize = ThumbnailSizes[thumbnailBox.SelectedIndex];
            Result.DarkTheme = darkThemeBox.Checked;
            Result.UseCache = useCacheBox.Checked;
            DialogResult = DialogResult.OK;
        }
    }
}
