using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using Timer = System.Windows.Forms.Timer;

namespace SchoolBell;

// 无边框毛玻璃模式：显示课表与下一次铃声倒计时（Fluent 设计风格）
public class BorderlessForm : Form
{
    // Fluent 配色
    private static readonly Color Accent = Color.FromArgb(0, 120, 215);        // Windows 主题蓝
    private static readonly Color TextPrimary = Color.FromArgb(250, 250, 250);
    private static readonly Color TextSecondary = Color.FromArgb(200, 200, 200);
    private static readonly Color TextTertiary = Color.FromArgb(140, 140, 140);
    private static readonly Color CardStroke = Color.FromArgb(66, 66, 66);

    private static readonly Font FontTitle = new("Segoe UI Variable Display", 13F, FontStyle.Bold);
    private static readonly Font FontBody = new("Microsoft YaHei UI", 10F);
    private static readonly Font FontCaption = new("Segoe UI Variable Text", 8F);
    private static readonly Font FontEmoji = new("Segoe UI Emoji", 8F);         // 专用 Emoji 字体，修复图钉丢失
    private static readonly Font FontCountdown = new("Segoe UI Variable Display", 26F, FontStyle.Bold);
    private static readonly Font FontNext = new("Segoe UI Variable Text", 10.5F);

    private const int Space = 16;       // Fluent 4px 栅格
    private const int Gap = 12;
    private const int Radius = 8;       // Fluent 圆角
    private const int ItemHeight = 21;  // 课表行高（紧凑行间距）

    private readonly BellScheduler scheduler;
    private readonly Timer refreshTimer;

    // ===== 自适应透明度 =====
    private const int AlphaMin = 0x30;   // 最通透
    private const int AlphaMax = 0xE0;   // 最不透明
    private const double LumaLow = 90;   // 低于此亮度不再加深
    private const double LumaHigh = 170; // 高于此亮度视为完全不可读
    private const double LumaHysteresis = 12; // 亮度回差
    private readonly Timer opacityTimer;
    private int currentAlpha = AlphaMin;
    private int targetAlpha = AlphaMin;
    private double lastSampledLuma = -1;

    // 动态遮罩颜色
    private Color maskColor = Color.FromArgb(AlphaMin, 20, 20, 20);

    // 界面数据缓存
    private struct ScheduleDisplayItem
    {
        public string DisplayText;
        public bool IsPassed;
        public bool IsNext;
    }
    private List<ScheduleDisplayItem> displayItems = new();
    private string nextBellText = "";
    private string countdownText = "";
    private int scrollOffset = 0;

    // 交互状态
    private Rectangle btnTopMostRect => new(Width - Space - 96, Space, 96, 30);
    private bool isBtnHovered = false;
    private bool isBtnPressed = false;

    // 彻底清除边框相关窗口样式
    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            const int WS_BORDER = 0x00800000;
            const int WS_DLGFRAME = 0x00400000;
            const int WS_THICKFRAME = 0x00040000;
            const int WS_CAPTION = WS_BORDER | WS_DLGFRAME;
            cp.Style &= ~(WS_CAPTION | WS_THICKFRAME);
            return cp;
        }
    }

    public BorderlessForm(BellScheduler scheduler)
    {
        this.scheduler = scheduler;

        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(380, 480);
        BackColor = Color.FromArgb(24, 24, 24);
        DoubleBuffered = true;
        ShowInTaskbar = true;
        ShowIcon = false;
        Text = "任禹铃 · 课表";
        TopMost = true;

        refreshTimer = new Timer { Interval = 500 };
        refreshTimer.Tick += (_, _) => RefreshDisplay();
        refreshTimer.Start();

        opacityTimer = new Timer { Interval = 33 };
        opacityTimer.Tick += OpacityTimer_Tick;
        opacityTimer.Start();

        RefreshDisplay();
    }

    private int opacitySampleCounter;

    private void OpacityTimer_Tick(object? sender, EventArgs e)
    {
        var isDragging = (Control.MouseButtons & MouseButtons.Left) == MouseButtons.Left;

        if (!isDragging)
        {
            opacitySampleCounter++;
            if (opacitySampleCounter >= 15)
            {
                opacitySampleCounter = 0;
                targetAlpha = ComputeTargetAlpha();
            }
        }

        if (currentAlpha == targetAlpha) return;

        var delta = targetAlpha - currentAlpha;
        var step = Math.Max(1, Math.Abs(delta) / 20) * Math.Sign(delta);
        currentAlpha = Math.Abs(targetAlpha - currentAlpha) <= Math.Abs(step)
            ? targetAlpha
            : currentAlpha + step;

        ApplyAlpha(currentAlpha);
    }

    private int ComputeTargetAlpha()
    {
        try
        {
            var luma = SampleBehindLuma();
            if (luma < 0) return targetAlpha;

            if (lastSampledLuma >= 0 && Math.Abs(luma - lastSampledLuma) < LumaHysteresis)
                return targetAlpha;
            lastSampledLuma = luma;

            var t = Math.Clamp((luma - LumaLow) / (LumaHigh - LumaLow), 0.0, 1.0);
            return (int)(AlphaMin + t * (AlphaMax - AlphaMin));
        }
        catch
        {
            return targetAlpha;
        }
    }

    private static readonly Point[] SamplePoints =
    {
        new(1, 1), new(3, 1), new(5, 1),
        new(1, 3), new(3, 3), new(5, 3),
        new(1, 5), new(3, 5), new(5, 5)
    };

    private const uint ColorKey = 0x00FF00FF;

    private double SampleBehindLuma()
    {
        var screen = GetDC(IntPtr.Zero);
        try
        {
            double total = 0;
            var count = 0;

            foreach (var p in SamplePoints)
            {
                var x = Left + Width * p.X / 6;
                var y = Top + Height * p.Y / 6;
                var pixel = GetPixel(screen, x, y);
                if (pixel == -1) continue;

                var r = pixel & 0xFF;
                var g = (pixel >> 8) & 0xFF;
                var b = (pixel >> 16) & 0xFF;
                total += 0.299 * r + 0.587 * g + 0.114 * b;
                count++;
            }

            return count > 0 ? total / count : -1;
        }
        finally
        {
            ReleaseDC(IntPtr.Zero, screen);
        }
    }

    private void EnableColorKey()
    {
        var exStyle = GetWindowLong(Handle, GWL_EXSTYLE);
        SetWindowLong(Handle, GWL_EXSTYLE, exStyle | WS_EX_LAYERED);
        SetLayeredWindowAttributes(Handle, ColorKey, 0, LWA_COLORKEY);
    }

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_LAYERED = 0x00080000;
    private const uint LWA_COLORKEY = 0x00000001;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

    // 修改遮罩颜色并在控制台输出当前透明度
    private void ApplyAlpha(int alpha)
    {
        maskColor = Color.FromArgb(alpha, 20, 20, 20);
        Console.WriteLine($"[透明度变化] Alpha: {alpha} / 255 ({(alpha / 255.0 * 100):F1}%)");
        Invalidate();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        EnableAcrylic();
        EnableColorKey();
    }

    // 核心绘制管线
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.TextRenderingHint = TextRenderingHint.AntiAlias;

        // 1. 全局背景遮罩
        using (var bgBrush = new SolidBrush(maskColor))
        {
            e.Graphics.FillRectangle(bgBrush, ClientRectangle);
        }

        // 2. 标题
        using (var titleBrush = new SolidBrush(TextPrimary))
        {
            e.Graphics.DrawString("今日课表", FontTitle, titleBrush, new PointF(Space, Space + 2));
        }

        // 3. 置顶按钮绘制（分离 Emoji 与中文字体，确保正确显示）
        var btnAlpha = Math.Min(255, (int)(currentAlpha * 1.1));
        var btnBg = isBtnPressed
            ? Color.FromArgb(btnAlpha, 40, 40, 40)
            : (isBtnHovered ? Color.FromArgb(btnAlpha, 64, 64, 64) : Color.FromArgb(btnAlpha, 48, 48, 48));

        using (var btnPath = CreateRoundPath(new Rectangle(btnTopMostRect.X, btnTopMostRect.Y, btnTopMostRect.Width - 1, btnTopMostRect.Height - 1), 6))
        using (var btnBrush = new SolidBrush(btnBg))
        using (var btnPen = new Pen(Color.FromArgb(btnAlpha, CardStroke.R, CardStroke.G, CardStroke.B), 1f))
        {
            e.Graphics.FillPath(btnBrush, btnPath);
            e.Graphics.DrawPath(btnPen, btnPath);

            using var btnTextBrush = new SolidBrush(TextSecondary);
            using var sfTypo = new StringFormat(StringFormat.GenericTypographic)
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.NoWrap
            };

            if (TopMost)
            {
                var iconSize = e.Graphics.MeasureString("📌", FontEmoji, PointF.Empty, sfTypo);
                var textSize = e.Graphics.MeasureString("已置顶", FontCaption, PointF.Empty, sfTypo);
                const float gap = 4f;
                var totalW = iconSize.Width + gap + textSize.Width;
                var startX = btnTopMostRect.X + (btnTopMostRect.Width - totalW) / 2f;

                var iconRect = new RectangleF(startX, btnTopMostRect.Y, iconSize.Width, btnTopMostRect.Height);
                var textRect = new RectangleF(startX + iconSize.Width + gap, btnTopMostRect.Y, textSize.Width + 4, btnTopMostRect.Height);

                e.Graphics.DrawString("📌", FontEmoji, btnTextBrush, iconRect, sfTypo);
                e.Graphics.DrawString("已置顶", FontCaption, btnTextBrush, textRect, sfTypo);
            }
            else
            {
                var sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                e.Graphics.DrawString("置顶", FontCaption, btnTextBrush, btnTopMostRect, sfCenter);
            }
        }

        var cardAlpha = Math.Min(255, (int)(currentAlpha * 1.05));
        var cardFillColor = Color.FromArgb(cardAlpha, 42, 42, 42);
        var cardStrokeColor = Color.FromArgb(cardAlpha, CardStroke.R, CardStroke.G, CardStroke.B);

        // 4. 课表卡片背景
        var cardScheduleRect = new Rectangle(Space, 56, Width - Space * 2, 250);
        using (var path = CreateRoundPath(new Rectangle(cardScheduleRect.X, cardScheduleRect.Y, cardScheduleRect.Width - 1, cardScheduleRect.Height - 1), Radius * 2))
        using (var brush = new SolidBrush(cardFillColor))
        using (var pen = new Pen(cardStrokeColor, 1f))
        {
            e.Graphics.FillPath(brush, path);
            e.Graphics.DrawPath(pen, path);
        }

        // 5. 课表列表区域（使用 GDI+ 严格裁剪）
        var innerListRect = new Rectangle(cardScheduleRect.X + Radius, cardScheduleRect.Y + Radius, cardScheduleRect.Width - Radius * 2, cardScheduleRect.Height - Radius * 2);
        
        var oldClip = e.Graphics.Clip;
        e.Graphics.SetClip(innerListRect);

        if (displayItems.Count == 0)
        {
            using var emptyBrush = new SolidBrush(TextSecondary);
            var sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            e.Graphics.DrawString("课表为空", FontBody, emptyBrush, innerListRect, sfCenter);
        }
        else
        {
            var sfItem = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };

            for (var i = 0; i < displayItems.Count; i++)
            {
                var item = displayItems[i];
                var itemY = innerListRect.Y + i * ItemHeight - scrollOffset;
                var itemRect = new Rectangle(innerListRect.X, itemY, innerListRect.Width, ItemHeight);

                // 越界剔除
                if (itemY + ItemHeight < innerListRect.Y || itemY > innerListRect.Bottom) continue;

                if (item.IsNext)
                {
                    using var selBrush = new SolidBrush(Color.FromArgb(Math.Min(255, currentAlpha + 35), 68, 68, 68));
                    using var selPath = CreateRoundPath(new Rectangle(itemRect.X + 2, itemRect.Y + 1, itemRect.Width - 4, itemRect.Height - 2), 6);
                    e.Graphics.FillPath(selBrush, selPath);
                }

                var foreColor = item.IsPassed ? TextTertiary : TextPrimary;
                var textRect = new RectangleF(itemRect.X + 8, itemRect.Y, itemRect.Width - 16, itemRect.Height);
                using var textBrush = new SolidBrush(foreColor);
                e.Graphics.DrawString(item.DisplayText, FontBody, textBrush, textRect, sfItem);
            }
        }
        e.Graphics.Clip = oldClip;

        // 6. 倒计时卡片
        var cardCountdownRect = new Rectangle(Space, 56 + 250 + Gap, Width - Space * 2, 110);
        using (var path = CreateRoundPath(new Rectangle(cardCountdownRect.X, cardCountdownRect.Y, cardCountdownRect.Width - 1, cardCountdownRect.Height - 1), Radius * 2))
        using (var brush = new SolidBrush(cardFillColor))
        using (var pen = new Pen(cardStrokeColor, 1f))
        {
            e.Graphics.FillPath(brush, path);
            e.Graphics.DrawPath(pen, path);
        }

        if (!string.IsNullOrEmpty(nextBellText))
        {
            using var nextBrush = new SolidBrush(TextSecondary);
            e.Graphics.DrawString(nextBellText, FontNext, nextBrush, new PointF(cardCountdownRect.X + Space, cardCountdownRect.Y + 14));
        }

        if (!string.IsNullOrEmpty(countdownText))
        {
            using var countBrush = new SolidBrush(Accent);
            e.Graphics.DrawString(countdownText, FontCountdown, countBrush, new PointF(cardCountdownRect.X + Space, cardCountdownRect.Y + 40));
        }

        // 7. 底部提示文本
        using (var hintBrush = new SolidBrush(TextTertiary))
        {
            e.Graphics.DrawString("双击或右键返回主界面 · 拖动移动", FontCaption, hintBrush, new PointF(Space, Height - Space - 18));
        }

        // 8. 绘制颜色键采样点
        using (var sampleBrush = new SolidBrush(Color.Magenta))
        {
            foreach (var p in SamplePoints)
            {
                var x = Width * p.X / 6;
                var y = Height * p.Y / 6;
                e.Graphics.FillRectangle(sampleBrush, x, y, 1, 1);
            }
        }

        base.OnPaint(e);
    }

    private static GraphicsPath CreateRoundPath(Rectangle rect, int diameter)
    {
        var path = new GraphicsPath();
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        refreshTimer.Stop();
        refreshTimer.Dispose();
        opacityTimer.Stop();
        opacityTimer.Dispose();
        base.OnFormClosed(e);
    }

    private void RefreshDisplay()
    {
        var s = scheduler.schedule;
        if (s == null || s.Count == 0)
        {
            displayItems.Clear();
            nextBellText = "";
            countdownText = "";
            Invalidate();
            return;
        }

        var now = DateTime.Now;
        var next = s
            .Where(c => DateTime.Today.Add(c.Time) >= now)
            .OrderBy(c => c.Time)
            .FirstOrDefault();

        var list = new List<ScheduleDisplayItem>();
        var selectedIdx = -1;
        for (var i = 0; i < s.Count; i++)
        {
            var item = s[i];
            var t = item.Type switch
            {
                "start" => "上课",
                "end" => "下课",
                _ => "默认"
            };
            var passed = DateTime.Today.Add(item.Time) < now;
            var isNext = (next != null && item.Time == next.Time && item.Type == next.Type);
            if (isNext && selectedIdx < 0) selectedIdx = i;

            list.Add(new ScheduleDisplayItem
            {
                DisplayText = $"{item.Time:hh\\:mm}    {t}{(passed ? "  ·  已过" : "")}",
                IsPassed = passed,
                IsNext = isNext
            });
        }
        displayItems = list;

        // 自动将下一节课居中滚动显示
        if (selectedIdx >= 0)
        {
            var maxScroll = Math.Max(0, displayItems.Count * ItemHeight - (250 - Radius * 2));
            scrollOffset = Math.Clamp((selectedIdx - 4) * ItemHeight, 0, maxScroll);
        }

        if (next == null)
        {
            nextBellText = "今天课程已结束";
            countdownText = "—";
        }
        else
        {
            var nextTime = DateTime.Today.Add(next.Time);
            var remain = nextTime - now;
            var typeName = next.Type == "start" ? "上课" : "下课";
            nextBellText = $"距离{typeName} · {nextTime:HH:mm}";
            countdownText = $"{remain.Hours:D2}:{remain.Minutes:D2}:{remain.Seconds:D2}";
        }

        Invalidate();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        var cardRect = new Rectangle(Space, 56, Width - Space * 2, 250);
        if (cardRect.Contains(e.Location) && displayItems.Count * ItemHeight > (250 - Radius * 2))
        {
            var maxScroll = displayItems.Count * ItemHeight - (250 - Radius * 2);
            scrollOffset = Math.Clamp(scrollOffset - (e.Delta / 120) * ItemHeight, 0, maxScroll);
            Invalidate(cardRect);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var hovered = btnTopMostRect.Contains(e.Location);
        if (hovered != isBtnHovered)
        {
            isBtnHovered = hovered;
            Invalidate(btnTopMostRect);
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (isBtnHovered || isBtnPressed)
        {
            isBtnHovered = false;
            isBtnPressed = false;
            Invalidate(btnTopMostRect);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button == MouseButtons.Right || e.Clicks >= 2)
        {
            Close();
            return;
        }

        if (e.Button != MouseButtons.Left) return;

        if (btnTopMostRect.Contains(e.Location))
        {
            isBtnPressed = true;
            Invalidate(btnTopMostRect);
            return;
        }

        ReleaseCapture();
        SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (isBtnPressed)
        {
            isBtnPressed = false;
            if (btnTopMostRect.Contains(e.Location))
            {
                TopMost = !TopMost;
            }
            Invalidate(btnTopMostRect);
        }
    }

    private void EnableAcrylic()
    {
        var accent = new AccentPolicy
        {
            AccentState = AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND,
            AccentFlags = 0x20,
            GradientColor = unchecked((int)0x01141414),
            AnimationId = 0
        };

        var accentSize = Marshal.SizeOf(accent);
        var accentPtr = Marshal.AllocHGlobal(accentSize);
        Marshal.StructureToPtr(accent, accentPtr, false);

        var data = new WindowCompositionAttributeData
        {
            Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
            SizeOfData = accentSize,
            Data = accentPtr
        };

        SetWindowCompositionAttribute(Handle, ref data);
        Marshal.FreeHGlobal(accentPtr);
    }

    private const int WM_NCLBUTTONDOWN = 0xA1;
    private const int HTCAPTION = 0x2;

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern int GetPixel(IntPtr hdc, int x, int y);

    private enum AccentState
    {
        ACCENT_DISABLED = 0,
        ACCENT_ENABLE_GRADIENT = 1,
        ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
        ACCENT_ENABLE_BLURBEHIND = 3,
        ACCENT_ENABLE_ACRYLICBLURBEHIND = 4
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public AccentState AccentState;
        public int AccentFlags;
        public int GradientColor;
        public int AnimationId;
    }

    private enum WindowCompositionAttribute
    {
        WCA_ACCENT_POLICY = 19
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public WindowCompositionAttribute Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }
}