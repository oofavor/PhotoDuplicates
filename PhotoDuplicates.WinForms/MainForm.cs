using System.Diagnostics;
using System.Text;
using PhotoDuplicates.Core;

namespace PhotoDuplicates.WinForms
{
    // главное окно: каждая группа дубликатов это группа в ListView, галочка у файла
    // значит удалить или переместить, поиск идёт в фоне через Task.Run
    public class MainForm : Form
    {
        // данные
        private readonly AppSettings settings;
        private readonly CommandLineOptions startOptions;
        private readonly DuplicateSearch search = new DuplicateSearch(new HashCache());
        private SearchResult? result;
        private readonly Dictionary<string, Bitmap> thumbnails = new Dictionary<string, Bitmap>(); // путь -> миниатюра
        private CancellationTokenSource? cancelSource;
        private bool isSearching;
        private bool closeAfterSearch;

        // элементы окна
        private readonly MenuStrip menu = new MenuStrip();
        private readonly TextBox folderBox = new TextBox();
        private readonly CheckBox subfoldersBox = new CheckBox();
        private readonly NumericUpDown thresholdBox = new NumericUpDown();
        private readonly ComboBox modeBox = new ComboBox();
        private readonly Button searchButton = new Button();
        private readonly Button cancelButton = new Button();
        private readonly ListView listView = new ListView();
        private readonly ImageList imageList = new ImageList();
        private readonly PictureBox previewBox = new PictureBox();
        private readonly TextBox infoBox = new TextBox();
        private readonly StatusStrip statusStrip = new StatusStrip();
        private readonly ToolStripStatusLabel statusLabel = new ToolStripStatusLabel();
        private readonly ToolStripProgressBar progressBar = new ToolStripProgressBar();

        public MainForm(AppSettings settings, CommandLineOptions startOptions)
        {
            this.settings = settings;
            this.startOptions = startOptions;

            Text = "Поиск похожих и дублирующихся фотографий";
            Size = new Size(1300, 850);
            MinimumSize = new Size(900, 600);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9.5f);
            KeyPreview = true; // чтобы Esc ловился раньше элементов окна

            CreateControls();
            ShowSettingsInControls();
            Theme.Apply(this, settings.DarkTheme);

            search.ProgressChanged += Search_ProgressChanged;
        }

        // ---- Создание элементов окна ----

        private void CreateControls()
        {
            // порядок важен: сначала то, что заполняет середину (Fill), потом края
            Controls.Add(CreateMainArea());
            Controls.Add(CreateActionsPanel());
            Controls.Add(CreateSearchPanel());
            Controls.Add(CreateStatusStrip());
            Controls.Add(CreateMenu());
            MainMenuStrip = menu;
        }

        private MenuStrip CreateMenu()
        {
            var fileMenu = new ToolStripMenuItem("&Файл");
            fileMenu.DropDownItems.Add(CreateMenuItem("Выбрать папку...", Keys.Control | Keys.O, BrowseFolder));
            fileMenu.DropDownItems.Add(CreateMenuItem("Найти дубликаты", Keys.F5, StartSearch));
            fileMenu.DropDownItems.Add(CreateMenuItem("Сохранить отчёт...", Keys.Control | Keys.S, SaveReport));
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add(CreateMenuItem("Выход", Keys.Alt | Keys.F4, Close));

            var editMenu = new ToolStripMenuItem("&Правка");
            editMenu.DropDownItems.Add(CreateMenuItem("Отметить все дубликаты, кроме лучших", Keys.F6, MarkAllExceptBest));
            editMenu.DropDownItems.Add(CreateMenuItem("Снять все отметки", Keys.F7, UnmarkAll));
            editMenu.DropDownItems.Add(new ToolStripSeparator());
            // Del ловим только в списке, иначе он мешал бы стирать текст в поле папки
            var deleteItem = CreateMenuItem("Удалить отмеченные в корзину", Keys.None, DeleteMarked);
            deleteItem.ShortcutKeyDisplayString = "Del";
            editMenu.DropDownItems.Add(deleteItem);
            editMenu.DropDownItems.Add(CreateMenuItem("Переместить отмеченные...", Keys.Control | Keys.M, MoveMarked));

            var toolsMenu = new ToolStripMenuItem("&Сервис");
            toolsMenu.DropDownItems.Add(CreateMenuItem("Настройки...", Keys.Control | Keys.P, OpenSettings));

            var helpMenu = new ToolStripMenuItem("&Справка");
            helpMenu.DropDownItems.Add(CreateMenuItem("Справка", Keys.F1, ShowHelp));

            menu.Items.Add(fileMenu);
            menu.Items.Add(editMenu);
            menu.Items.Add(toolsMenu);
            menu.Items.Add(helpMenu);
            return menu;
        }

        private static ToolStripMenuItem CreateMenuItem(string text, Keys shortcut, Action action)
        {
            var item = new ToolStripMenuItem(text);
            item.ShortcutKeys = shortcut;
            item.Click += (sender, e) => action();
            return item;
        }

        private Control CreateSearchPanel()
        {
            var panel = new FlowLayoutPanel();
            panel.Dock = DockStyle.Top;
            panel.AutoSize = true;
            panel.Padding = new Padding(6);
            panel.WrapContents = true;

            folderBox.Width = 420;
            var browseButton = new Button { Text = "Обзор...", AutoSize = true };
            browseButton.Click += (sender, e) => BrowseFolder();

            subfoldersBox.Text = "Вложенные папки";
            subfoldersBox.AutoSize = true;

            thresholdBox.Minimum = 0;
            thresholdBox.Maximum = DuplicateFinder.MaxThreshold;
            thresholdBox.Width = 60;

            modeBox.DropDownStyle = ComboBoxStyle.DropDownList;
            modeBox.Items.Add("Точные копии и похожие");  // индекс 0 = SearchMode.ExactAndSimilar
            modeBox.Items.Add("Только точные копии");     // индекс 1 = SearchMode.ExactOnly
            modeBox.Width = 200;
            modeBox.SelectedIndexChanged += (sender, e) => thresholdBox.Enabled = modeBox.SelectedIndex == 0;

            searchButton.Text = "Найти (F5)";
            searchButton.AutoSize = true;
            searchButton.Font = new Font(Font, FontStyle.Bold);
            searchButton.Click += (sender, e) => StartSearch();

            cancelButton.Text = "Отмена (Esc)";
            cancelButton.AutoSize = true;
            cancelButton.Enabled = false;
            cancelButton.Click += (sender, e) => CancelSearch();

            panel.Controls.Add(CreateLabel("Папка:"));
            panel.Controls.Add(folderBox);
            panel.Controls.Add(browseButton);
            panel.Controls.Add(subfoldersBox);
            panel.Controls.Add(CreateLabel("Порог (бит):"));
            panel.Controls.Add(thresholdBox);
            panel.Controls.Add(modeBox);
            panel.Controls.Add(searchButton);
            panel.Controls.Add(cancelButton);
            return panel;
        }

        private static Label CreateLabel(string text)
        {
            // отступ, чтобы подпись стояла на одной линии с полями
            return new Label { Text = text, AutoSize = true, Margin = new Padding(3, 7, 3, 0) };
        }

        private Control CreateMainArea()
        {
            var split = new SplitContainer();
            split.Dock = DockStyle.Fill;
            split.FixedPanel = FixedPanel.Panel2; // при растягивании окна растёт список, а не просмотр
            split.SplitterWidth = 6;

            // список с миниатюрами
            imageList.ColorDepth = ColorDepth.Depth32Bit;
            imageList.ImageSize = new Size(settings.ThumbnailSize, settings.ThumbnailSize);

            listView.Dock = DockStyle.Fill;
            listView.View = View.LargeIcon;
            listView.LargeImageList = imageList;
            listView.CheckBoxes = true;      // галочка = удалить/переместить
            listView.ShowGroups = true;
            listView.ShowItemToolTips = true;
            listView.MultiSelect = false;
            listView.HideSelection = false;
            listView.ItemChecked += ListView_ItemChecked;
            listView.SelectedIndexChanged += ListView_SelectedIndexChanged;
            listView.DoubleClick += (sender, e) => OpenSelectedFile();
            listView.KeyDown += ListView_KeyDown;
            split.Panel1.Controls.Add(listView);

            // просмотр выбранного файла
            previewBox.Dock = DockStyle.Fill;
            previewBox.SizeMode = PictureBoxSizeMode.Zoom;

            infoBox.Dock = DockStyle.Bottom;
            infoBox.Multiline = true;
            infoBox.ReadOnly = true;
            infoBox.Height = 210;
            infoBox.Font = new Font("Consolas", 9);
            infoBox.Text = "Выберите папку и нажмите «Найти» (F5).\r\nСправка - F1.";

            split.Panel2.Controls.Add(previewBox);
            split.Panel2.Controls.Add(infoBox);

            // ширину задаём, когда окно уже получило размер
            Load += (sender, e) => split.SplitterDistance = Math.Max(300, split.Width - 420);
            return split;
        }

        private Control CreateActionsPanel()
        {
            var panel = new FlowLayoutPanel();
            panel.Dock = DockStyle.Bottom;
            panel.AutoSize = true;
            panel.Padding = new Padding(6);

            panel.Controls.Add(CreateButton("Отметить дубликаты (F6)", MarkAllExceptBest));
            panel.Controls.Add(CreateButton("Снять отметки (F7)", UnmarkAll));
            panel.Controls.Add(CreateButton("Удалить отмеченные в корзину (Del)", DeleteMarked));
            panel.Controls.Add(CreateButton("Переместить отмеченные... (Ctrl+M)", MoveMarked));
            panel.Controls.Add(CreateButton("Сохранить отчёт... (Ctrl+S)", SaveReport));
            return panel;
        }

        private static Button CreateButton(string text, Action action)
        {
            var button = new Button { Text = text, AutoSize = true };
            button.Click += (sender, e) => action();
            return button;
        }

        private StatusStrip CreateStatusStrip()
        {
            statusLabel.Spring = true;
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            statusLabel.Text = "Готово";
            progressBar.Width = 250;
            progressBar.Visible = false;

            statusStrip.Items.Add(statusLabel);
            statusStrip.Items.Add(progressBar);
            return statusStrip;
        }

        // ---- Настройки <-> элементы окна ----

        private void ShowSettingsInControls()
        {
            folderBox.Text = settings.Folder;
            subfoldersBox.Checked = settings.IncludeSubfolders;
            thresholdBox.Value = settings.Threshold;
            modeBox.SelectedIndex = settings.Mode == SearchMode.ExactOnly ? 1 : 0;
        }

        private void ReadSettingsFromControls()
        {
            settings.Folder = folderBox.Text.Trim();
            settings.IncludeSubfolders = subfoldersBox.Checked;
            settings.Threshold = (int)thresholdBox.Value;
            settings.Mode = modeBox.SelectedIndex == 1 ? SearchMode.ExactOnly : SearchMode.ExactAndSimilar;
        }

        private void SaveSettings()
        {
            try
            {
                settings.Save(AppSettings.DefaultFilePath);
            }
            catch (Exception)
            {
                // не получилось сохранить, ничего страшного, будут настройки по умолчанию
            }
        }

        // ---- Поиск ----

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            // папка передана в командной строке, сразу ищем
            if (startOptions.Folder != null)
            {
                StartSearch();
            }
        }

        private async void StartSearch()
        {
            if (isSearching)
            {
                return;
            }

            ReadSettingsFromControls();
            if (!Directory.Exists(settings.Folder))
            {
                MessageBox.Show(this, "Папка не найдена: " + settings.Folder, "Поиск",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            SaveSettings();

            SetSearching(true);
            ClearResults();
            cancelSource = new CancellationTokenSource();
            CancellationToken token = cancelSource.Token;

            // копия для фонового потока: пользователь может менять поля, пока идёт поиск
            AppSettings searchSettings = settings.Clone();
            bool clearCache = startOptions.ClearCache;
            startOptions.ClearCache = false; // очищаем кеш только при первом поиске

            try
            {
                // тяжёлая работа в фоновом потоке, чтобы окно не зависало
                // await ждёт результат, не блокируя окно, и дальше код идёт уже в главном потоке
                SearchResult searchResult = await Task.Run(() => search.Run(searchSettings, clearCache, token));

                statusLabel.Text = "Создание миниатюр...";
                int size = settings.ThumbnailSize;
                Dictionary<string, Bitmap> newThumbnails = await Task.Run(() => CreateThumbnails(searchResult.Groups, size));
                foreach (KeyValuePair<string, Bitmap> pair in newThumbnails)
                {
                    thumbnails[pair.Key] = pair.Value;
                }

                result = searchResult;
                ShowResults();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Ошибка во время поиска: " + ex.Message, "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            SetSearching(false);
            ShowSearchSummary();

            // --report из командной строки: сохраняем отчёт после первого поиска
            if (startOptions.ReportFile != null && result != null)
            {
                File.WriteAllText(startOptions.ReportFile, result.BuildReport(), Encoding.UTF8);
                startOptions.ReportFile = null;
            }

            if (closeAfterSearch)
            {
                Close();
            }
        }

        private void CancelSearch()
        {
            if (cancelSource != null)
            {
                cancelSource.Cancel();
                statusLabel.Text = "Прерывание поиска...";
            }
        }

        // тоже выполняется в фоновом потоке
        private static Dictionary<string, Bitmap> CreateThumbnails(List<DuplicateGroup> groups, int size)
        {
            var result = new Dictionary<string, Bitmap>();
            foreach (DuplicateGroup group in groups)
            {
                foreach (PhotoInfo photo in group.Files)
                {
                    result[photo.FilePath] = CreateThumbnail(photo.FilePath, size);
                }
            }
            return result;
        }

        private static Bitmap CreateThumbnail(string filePath, int size)
        {
            try
            {
                using (Bitmap image = ImageLoader.Load(filePath))
                {
                    // прозрачный фон подходит к любой теме
                    return ImageLoader.CreateThumbnail(image, size, Color.Transparent);
                }
            }
            catch (Exception)
            {
                // не открылась, рисуем серый квадрат с вопросом
                var placeholder = new Bitmap(size, size);
                using (Graphics g = Graphics.FromImage(placeholder))
                using (var font = new Font("Segoe UI", size / 3f))
                {
                    g.Clear(Color.Gray);
                    g.DrawString("?", font, Brushes.White, size / 3f, size / 8f);
                }
                return placeholder;
            }
        }

        private void Search_ProgressChanged(object? sender, ScanProgressEventArgs e)
        {
            // событие приходит из фонового потока, а элементы окна можно трогать только из главного
            // BeginInvoke отправляет этот код в главный поток
            BeginInvoke(() =>
            {
                progressBar.Maximum = e.Total;
                progressBar.Value = e.Done;
                statusLabel.Text = $"Обработано {e.Done} из {e.Total}: {Path.GetFileName(e.FilePath)}";
            });
        }

        private void SetSearching(bool searching)
        {
            isSearching = searching;
            searchButton.Enabled = !searching;
            cancelButton.Enabled = searching;
            progressBar.Visible = searching;
            progressBar.Value = 0;
            Cursor = searching ? Cursors.AppStarting : Cursors.Default;
        }

        private void ShowSearchSummary()
        {
            if (result == null)
            {
                return;
            }

            ScanResult scan = result.Scan;
            string text = $"Проверено файлов: {scan.Photos.Count} (из кеша: {scan.FromCacheCount}) за {scan.Elapsed.TotalSeconds:0.0} сек. "
                + $"Найдено групп: {result.Groups.Count}.";
            if (scan.WasCancelled)
            {
                text = "Поиск прерван. " + text;
            }
            if (scan.Errors.Count > 0)
            {
                text = text + $" Проблемных файлов: {scan.Errors.Count} (подробности - в отчёте).";
            }
            statusLabel.Text = text;

            if (result.Groups.Count == 0 && !scan.WasCancelled)
            {
                infoBox.Text = "Дубликаты не найдены.";
            }
        }

        // ---- Показ результатов ----

        private void ClearResults()
        {
            result = null;
            listView.Items.Clear();
            listView.Groups.Clear();
            imageList.Images.Clear();
            SetPreview(null);
            infoBox.Text = "";

            foreach (Bitmap bitmap in thumbnails.Values)
            {
                bitmap.Dispose();
            }
            thumbnails.Clear();
        }

        private void ShowResults()
        {
            listView.BeginUpdate(); // не перерисовываем, пока заполняем
            listView.Items.Clear();
            listView.Groups.Clear();
            imageList.Images.Clear();
            imageList.ImageSize = new Size(settings.ThumbnailSize, settings.ThumbnailSize);

            if (result != null)
            {
                foreach (DuplicateGroup group in result.Groups)
                {
                    string header = $"Группа {group.Number} - {group.KindTitle}, файлов: {group.Files.Count}, "
                        + $"можно освободить: {FileOperations.FormatSize(group.BytesToFree)}";
                    var listGroup = new ListViewGroup(header);
                    listGroup.Tag = group;
                    listView.Groups.Add(listGroup);

                    foreach (PhotoInfo photo in group.Files)
                    {
                        listView.Items.Add(CreateListItem(photo, group, listGroup));
                    }
                }
            }

            listView.EndUpdate();
            UpdateMarkedInfo();
        }

        private ListViewItem CreateListItem(PhotoInfo photo, DuplicateGroup group, ListViewGroup listGroup)
        {
            if (thumbnails.TryGetValue(photo.FilePath, out Bitmap? thumbnail))
            {
                imageList.Images.Add(photo.FilePath, thumbnail);
            }

            bool isBest = photo == group.BestFile;
            string text = (isBest ? "★ " : "") + photo.FileName + "\n" + photo.ResolutionText + ", " + FileOperations.FormatSize(photo.FileSize);

            var item = new ListViewItem(text, photo.FilePath, listGroup);
            item.Tag = photo;
            item.ToolTipText = photo.FilePath;
            item.ForeColor = isBest ? Theme.BestText : Theme.NormalText;
            return item;
        }

        // ---- Выбор и просмотр файла ----

        private PhotoInfo? GetSelectedPhoto()
        {
            if (listView.SelectedItems.Count == 0)
            {
                return null;
            }
            return listView.SelectedItems[0].Tag as PhotoInfo;
        }

        private void ListView_SelectedIndexChanged(object? sender, EventArgs e)
        {
            PhotoInfo? photo = GetSelectedPhoto();
            if (photo == null)
            {
                return;
            }

            var group = (DuplicateGroup)listView.SelectedItems[0].Group!.Tag!;
            ShowPhotoInfo(photo, group);

            try
            {
                SetPreview(ImageLoader.Load(photo.FilePath));
            }
            catch (Exception)
            {
                SetPreview(null);
            }
        }

        private void SetPreview(Image? image)
        {
            Image? old = previewBox.Image;
            previewBox.Image = image;
            old?.Dispose();
        }

        private void ShowPhotoInfo(PhotoInfo photo, DuplicateGroup group)
        {
            string comparison;
            if (photo == group.BestFile)
            {
                comparison = "лучший файл группы (★) - предлагается оставить";
            }
            else if (photo.Sha256 == group.BestFile.Sha256)
            {
                comparison = "точная копия лучшего файла";
            }
            else
            {
                int distance = group.DistanceToBest(photo);
                comparison = distance >= 0 ? $"отличается от лучшего на {distance} бит из 64" : "нет данных";
            }

            var text = new StringBuilder();
            text.AppendLine("Файл:       " + photo.FileName);
            text.AppendLine("Папка:      " + Path.GetDirectoryName(photo.FilePath));
            text.AppendLine("Разрешение: " + photo.ResolutionText);
            text.AppendLine("Размер:     " + FileOperations.FormatSize(photo.FileSize));
            text.AppendLine("Изменён:    " + photo.LastWriteTimeUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm"));
            text.AppendLine("SHA-256:    " + photo.Sha256.Substring(0, 32));
            text.AppendLine("            " + photo.Sha256.Substring(32));
            text.AppendLine("dHash:      " + (photo.HasDHash ? PerceptualHash.ToHex(photo.DHash) : "-"));
            text.AppendLine("Сравнение:  " + comparison);
            text.AppendLine();
            text.Append("Enter или двойной щелчок - открыть файл");
            infoBox.Text = text.ToString();
        }

        private void OpenSelectedFile()
        {
            PhotoInfo? photo = GetSelectedPhoto();
            if (photo == null)
            {
                return;
            }

            try
            {
                // открыть программой, которая назначена в Windows для картинок
                Process.Start(new ProcessStartInfo(photo.FilePath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Не удалось открыть файл: " + ex.Message, "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // ---- Отметки (галочки) ----

        private void ListView_ItemChecked(object? sender, ItemCheckedEventArgs e)
        {
            var photo = (PhotoInfo)e.Item.Tag!;
            var group = (DuplicateGroup)e.Item.Group!.Tag!;

            if (e.Item.Checked)
            {
                e.Item.ForeColor = Theme.MarkedText;
            }
            else if (photo == group.BestFile)
            {
                e.Item.ForeColor = Theme.BestText;
            }
            else
            {
                e.Item.ForeColor = Theme.NormalText;
            }

            UpdateMarkedInfo();
        }

        private void MarkAllExceptBest()
        {
            listView.BeginUpdate();
            foreach (ListViewItem item in listView.Items)
            {
                var photo = (PhotoInfo)item.Tag!;
                var group = (DuplicateGroup)item.Group!.Tag!;
                item.Checked = photo != group.BestFile;
            }
            listView.EndUpdate();
        }

        private void UnmarkAll()
        {
            listView.BeginUpdate();
            foreach (ListViewItem item in listView.Items)
            {
                item.Checked = false;
            }
            listView.EndUpdate();
        }

        private List<ListViewItem> GetMarkedItems()
        {
            var marked = new List<ListViewItem>();
            foreach (ListViewItem item in listView.CheckedItems)
            {
                marked.Add(item);
            }
            return marked;
        }

        private void UpdateMarkedInfo()
        {
            if (isSearching)
            {
                return;
            }

            long bytes = 0;
            foreach (ListViewItem item in listView.CheckedItems)
            {
                bytes = bytes + ((PhotoInfo)item.Tag!).FileSize;
            }
            Text = $"Поиск похожих и дублирующихся фотографий - отмечено файлов: {listView.CheckedItems.Count} ({FileOperations.FormatSize(bytes)})";
        }

        // предупреждаем, если в какой-то группе отмечены все файлы (не останется ни одной копии)
        private bool ConfirmNoGroupFullyMarked()
        {
            foreach (ListViewGroup listGroup in listView.Groups)
            {
                bool allMarked = listGroup.Items.Count > 0;
                foreach (ListViewItem item in listGroup.Items)
                {
                    if (!item.Checked)
                    {
                        allMarked = false;
                    }
                }

                if (allMarked)
                {
                    var group = (DuplicateGroup)listGroup.Tag!;
                    DialogResult answer = MessageBox.Show(this,
                        $"В группе {group.Number} отмечены все файлы - не останется ни одной копии этой фотографии.\n\nПродолжить?",
                        "Внимание", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
                    return answer == DialogResult.Yes;
                }
            }
            return true;
        }

        // ---- Удаление и перемещение ----

        private void DeleteMarked()
        {
            List<ListViewItem> marked = GetMarkedItems();
            if (marked.Count == 0)
            {
                MessageBox.Show(this, "Нет отмеченных файлов. Поставьте галочки или нажмите F6.", "Удаление",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!ConfirmNoGroupFullyMarked())
            {
                return;
            }

            DialogResult answer = MessageBox.Show(this, $"Удалить отмеченные файлы ({marked.Count} шт.) в корзину?",
                "Удаление", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer != DialogResult.Yes)
            {
                return;
            }

            var errors = new List<string>();
            foreach (ListViewItem item in marked)
            {
                var photo = (PhotoInfo)item.Tag!;
                try
                {
                    RecycleBin.DeleteFile(photo.FilePath);
                    RemoveFromResult(photo.FilePath);
                }
                catch (Exception ex)
                {
                    errors.Add(photo.FilePath + ": " + ex.Message);
                }
            }

            FinishFileOperation("Удалено в корзину", marked.Count - errors.Count, errors);
        }

        private void MoveMarked()
        {
            List<ListViewItem> marked = GetMarkedItems();
            if (marked.Count == 0)
            {
                MessageBox.Show(this, "Нет отмеченных файлов. Поставьте галочки или нажмите F6.", "Перемещение",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!ConfirmNoGroupFullyMarked())
            {
                return;
            }

            string targetFolder;
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = $"Куда переместить отмеченные файлы ({marked.Count} шт.)?";
                dialog.UseDescriptionForTitle = true;
                dialog.SelectedPath = settings.MoveFolder;
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                targetFolder = dialog.SelectedPath;
            }

            settings.MoveFolder = targetFolder;
            SaveSettings();

            var errors = new List<string>();
            foreach (ListViewItem item in marked)
            {
                var photo = (PhotoInfo)item.Tag!;
                try
                {
                    FileOperations.MoveToFolder(photo.FilePath, targetFolder);
                    RemoveFromResult(photo.FilePath);
                }
                catch (Exception ex)
                {
                    errors.Add(photo.FilePath + ": " + ex.Message);
                }
            }

            FinishFileOperation("Перемещено в " + targetFolder, marked.Count - errors.Count, errors);
        }

        private void RemoveFromResult(string filePath)
        {
            result?.RemoveFile(filePath);
            search.Cache.Remove(filePath);
        }

        private void FinishFileOperation(string action, int successCount, List<string> errors)
        {
            SetPreview(null);
            infoBox.Text = "";
            ShowResults();
            statusLabel.Text = $"{action}: {successCount} файлов. Осталось групп: {result?.Groups.Count ?? 0}.";

            if (errors.Count > 0)
            {
                MessageBox.Show(this, "Не удалось обработать файлы:\n\n" + string.Join("\n", errors),
                    "Ошибки", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // ---- Прочие команды ----

        private void BrowseFolder()
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Выберите папку с фотографиями";
                dialog.UseDescriptionForTitle = true;
                dialog.SelectedPath = folderBox.Text;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    folderBox.Text = dialog.SelectedPath;
                }
            }
        }

        private void SaveReport()
        {
            if (result == null)
            {
                MessageBox.Show(this, "Сначала выполните поиск (F5).", "Отчёт",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "Текстовый файл (*.txt)|*.txt";
                dialog.FileName = "Отчёт о дубликатах.txt";
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    File.WriteAllText(dialog.FileName, result.BuildReport(), Encoding.UTF8);
                    statusLabel.Text = "Отчёт сохранён: " + dialog.FileName;
                }
            }
        }

        private void OpenSettings()
        {
            using (var form = new SettingsForm(settings, search.Cache))
            {
                Theme.Apply(form, settings.DarkTheme);
                if (form.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                settings.ThumbnailSize = form.Result.ThumbnailSize;
                settings.DarkTheme = form.Result.DarkTheme;
                settings.UseCache = form.Result.UseCache;
                SaveSettings();

                Theme.Apply(this, settings.DarkTheme);
                if (result != null)
                {
                    // размер миниатюр поменялся, проще повторить поиск (с кешем это быстро)
                    if (imageList.ImageSize.Width != settings.ThumbnailSize)
                    {
                        StartSearch();
                    }
                    else
                    {
                        ShowResults();
                    }
                }
            }
        }

        private void ShowHelp()
        {
            using (var form = new HelpForm())
            {
                Theme.Apply(form, settings.DarkTheme);
                form.ShowDialog(this);
            }
        }

        // ---- Клавиатура и закрытие окна ----

        private void ListView_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                DeleteMarked();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                OpenSelectedFile();
                e.Handled = true;
            }
            // пробел ListView обрабатывает сам (ставит/снимает галочку)
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape && isSearching)
            {
                CancelSearch();
                e.Handled = true;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // если идёт поиск, сначала прерываем его, окно закроется, когда он остановится
            if (isSearching)
            {
                closeAfterSearch = true;
                CancelSearch();
                e.Cancel = true;
                return;
            }

            ReadSettingsFromControls();
            SaveSettings();
            base.OnFormClosing(e);
        }
    }
}
