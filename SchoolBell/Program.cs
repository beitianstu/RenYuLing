namespace SchoolBell;
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var bellDir = Path.Combine(AppContext.BaseDirectory, "bells");
        if (!Directory.Exists(bellDir)) Directory.CreateDirectory(bellDir);

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
