using PhotoDuplicates.Core;

namespace PhotoDuplicates.WinForms
{
    public class HelpForm : Form
    {
        public HelpForm()
        {
            Text = "Справка - Поиск дубликатов фотографий";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(850, 750);
            KeyPreview = true;

            var textBox = new TextBox();
            textBox.Multiline = true;
            textBox.ReadOnly = true;
            textBox.ScrollBars = ScrollBars.Vertical;
            textBox.Dock = DockStyle.Fill;
            textBox.Font = new Font("Consolas", 11); // моноширинный, чтобы колонки не разъезжались
            textBox.Text = HelpText.Keys + Environment.NewLine + Environment.NewLine + HelpText.CommandLine;

            var closeButton = new Button();
            closeButton.Text = "Закрыть";
            closeButton.Dock = DockStyle.Bottom;
            closeButton.Height = 40;
            closeButton.DialogResult = DialogResult.OK;

            Controls.Add(textBox);
            Controls.Add(closeButton);
            AcceptButton = closeButton;
            CancelButton = closeButton;
            ActiveControl = closeButton;  // иначе весь текст будет выделен
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.F1)
            {
                Close();
            }
        }
    }
}
