using PhotoDuplicates.Core;

namespace PhotoDuplicates.WinForms
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            ApplicationConfiguration.Initialize();

            CommandLineOptions options = CommandLineOptions.Parse(args);

            if (options.HasErrors)
            {
                MessageBox.Show(string.Join(Environment.NewLine, options.Errors)
                    + Environment.NewLine + Environment.NewLine + "Справка: PhotoDuplicates.exe --help",
                    "Ошибка в параметрах", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (options.ShowHelp)
            {
                Application.Run(new HelpForm());
                return;
            }

            AppSettings settings = AppSettings.Load(AppSettings.DefaultFilePath);
            options.ApplyTo(settings);
            settings.FixInvalidValues();

            Application.Run(new MainForm(settings, options));
        }
    }
}
