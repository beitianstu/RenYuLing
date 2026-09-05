namespace SchoolBell;
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // WinExe 双击/开机自启时没有控制台句柄，设置 OutputEncoding 会抛“句柄无效”；只有 IDE/终端下输出被重定向时才生效
        try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { /* 无控制台宿主，忽略 */ }
        var bellDir = Path.Combine(AppContext.BaseDirectory, "bells");
        if (!Directory.Exists(bellDir)) Directory.CreateDirectory(bellDir);

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
