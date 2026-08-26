using System.Runtime.InteropServices;

namespace SchoolBell;

// Voicemeeter Remote API 封装：登录/登出、参数读写、运行状态监测
// 通过 LoadLibrary 显式按绝对路径加载 VoicemeeterRemote64.dll，
// 绕开 DllImport 的默认搜索机制（对非系统目录的 DLL 不可靠）
public static class VoicemeeterRemote
{
    public enum VoicemeeterType
    {
        Standard = 1,
        Banana = 2,
        Potato = 3
    }

    // ===== Win32 加载函数 =====
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadLibrary(string lpFileName);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeLibrary(IntPtr hModule);

    // ===== Voicemeeter API 委托签名 =====
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int DLogin();

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int DLogout();

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    private delegate int DGetParameterFloat([MarshalAs(UnmanagedType.LPStr)] string name, ref float value);

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    private delegate int DSetParameterFloat([MarshalAs(UnmanagedType.LPStr)] string name, float value);

    private static IntPtr hModule = IntPtr.Zero;
    private static DLogin? login;
    private static DLogout? logout;
    private static DGetParameterFloat? getParam;
    private static DSetParameterFloat? setParam;

    private static readonly string[] VmInstallDirs =
    {
        @"C:\Program Files (x86)\VB\Voicemeeter",
        @"C:\Program Files\VB\Voicemeeter"
    };

    public static bool IsLoggedIn { get; private set; }

    // 显式加载 DLL 并解析函数地址，成功返回 true
    private static bool EnsureLoaded()
    {
        if (hModule != IntPtr.Zero) return true;

        string? dllPath = null;
        foreach (var dir in VmInstallDirs)
        {
            var p = Path.Combine(dir, "VoicemeeterRemote64.dll");
            if (File.Exists(p)) { dllPath = p; break; }
        }

        if (dllPath == null)
        {
            Console.WriteLine("[VM] 未在 Voicemeeter 安装目录找到 VoicemeeterRemote64.dll");
            return false;
        }

        hModule = LoadLibrary(dllPath);
        if (hModule == IntPtr.Zero)
        {
            Console.WriteLine($"[VM] LoadLibrary 失败，错误码 {Marshal.GetLastWin32Error()}");
            return false;
        }

        login = GetDelegate<DLogin>("VBVMR_Login");
        logout = GetDelegate<DLogout>("VBVMR_Logout");
        getParam = GetDelegate<DGetParameterFloat>("VBVMR_GetParameterFloat");
        setParam = GetDelegate<DSetParameterFloat>("VBVMR_SetParameterFloat");

        if (login == null || logout == null || getParam == null || setParam == null)
        {
            Console.WriteLine("[VM] 解析函数地址失败");
            FreeLibrary(hModule);
            hModule = IntPtr.Zero;
            return false;
        }

        Console.WriteLine("[VM] DLL 加载成功：" + dllPath);
        return true;
    }

    private static T? GetDelegate<T>(string procName) where T : class
    {
        var addr = GetProcAddress(hModule, procName);
        if (addr == IntPtr.Zero) return null;
        return Marshal.GetDelegateForFunctionPointer<T>(addr);
    }

    // 登录：返回 true 表示成功连上 Voicemeeter
    public static bool Login()
    {
        if (!EnsureLoaded() || login == null) { IsLoggedIn = false; return false; }
        try
        {
            var result = login();
            // 0=成功；1=成功但 Voicemeeter 未运行；-2=已登录
            IsLoggedIn = result == 0 || result == 1 || result == -2;
            Console.WriteLine($"[VM] Login 返回 {result}，IsLoggedIn={IsLoggedIn}");
            return IsLoggedIn;
        }
        catch (Exception ex)
        {
            Console.WriteLine("[VM] 登录异常：" + ex.Message);
            IsLoggedIn = false;
            return false;
        }
    }

    public static void Logout()
    {
        if (!IsLoggedIn || logout == null) { IsLoggedIn = false; return; }
        try { logout(); } catch { /* 忽略 */ }
        IsLoggedIn = false;
    }

    // 检测 Voicemeeter 是否在运行：读一个必定存在的参数
    public static bool IsRunning()
    {
        if (!IsLoggedIn || getParam == null) return false;
        try
        {
            float v = 0;
            return getParam("Strip[0].Mute", ref v) == 0;
        }
        catch
        {
            return false;
        }
    }

    // 设置 Strip 静音（Strip[0] = Hardware Input 1）
    public static bool SetStripMute(int stripIndex, bool mute)
    {
        if (!IsLoggedIn || setParam == null) return false;
        try
        {
            return setParam($"Strip[{stripIndex}].Mute", mute ? 1f : 0f) == 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine("[VM] 设置静音失败：" + ex.Message);
            return false;
        }
    }

    // 读取 Strip 静音状态
    public static bool GetStripMute(int stripIndex)
    {
        if (!IsLoggedIn || getParam == null) return false;
        try
        {
            float v = 0;
            return getParam($"Strip[{stripIndex}].Mute", ref v) == 0 && v > 0.5f;
        }
        catch
        {
            return false;
        }
    }
}
