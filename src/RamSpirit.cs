// AI 분할 창 — 여러 AI 앱(2·4·6·8개)을 한 창의 칸에 붙여서 보여 주는 윈도우 프로그램
// 앱 창을 테두리 없이 각 칸 위에 정확히 겹쳐 두고 큰 창을 따라다니게 함(앱 안에서 타자 정상 입력)
// C# 5 문법만 사용 — 윈도우 기본 컴파일러(csc v4)로 빌드

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
#if !NO_UIA
using System.Windows.Automation;
#endif

static class W
{
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc f, IntPtr l);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
    [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr h, int c);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int w, int hh, uint f);
    [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int i);
    [DllImport("user32.dll")] public static extern int SetWindowLong(IntPtr h, int i, int v);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr64(IntPtr h, int i, IntPtr v);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);

    // 붙어 있던 앱을 원래 창 모양으로 돌려놓은 뒤 닫기 (X 버튼을 누른 것과 같음)
    public static void CloseApp(IntPtr h, int oldStyle)
    {
        if (!IsWindow(h)) return;
        Unclip(h);
        SetOwner(h, IntPtr.Zero);
        SetWindowLong(h, GWL_STYLE, oldStyle);
        SetWindowPos(h, IntPtr.Zero, 100, 100, 1100, 800, NOZ | NOACT | FRAME);
        PostMessage(h, 0x0010, IntPtr.Zero, IntPtr.Zero);   // WM_CLOSE
    }
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
    [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint a, uint b, bool attach);
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, int dx, int dy, uint data, UIntPtr extra);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }

    // 다른 프로그램 창을 맨 앞으로 (윈도우의 앞으로 가져오기 제한을 피하려고 입력 스레드를 잠시 연결)
    public static void ForceForeground(IntPtr h)
    {
        IntPtr fg = GetForegroundWindow();
        uint pid;
        uint ft = GetWindowThreadProcessId(fg, out pid);
        uint me = GetCurrentThreadId();
        bool att = ft != me && AttachThreadInput(me, ft, true);
        BringWindowToTop(h);
        SetForegroundWindow(h);
        if (att) AttachThreadInput(me, ft, false);
    }

    public static void Key(byte vk, bool ctrl)
    {
        if (ctrl) keybd_event(0x11, 0, 0, UIntPtr.Zero);
        keybd_event(vk, 0, 0, UIntPtr.Zero);
        keybd_event(vk, 0, 2, UIntPtr.Zero);
        if (ctrl) keybd_event(0x11, 0, 2, UIntPtr.Zero);
    }

    public static void Click(int x, int y)
    {
        Point old = Cursor.Position;
        SetCursorPos(x, y);
        mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
        mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
        System.Threading.Thread.Sleep(60);
        SetCursorPos(old.X, old.Y);
    }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);

    const int GWL_STYLE = -16, GWLP_HWNDPARENT = -8;
    const int WS_POPUP = unchecked((int)0x80000000), WS_CAPTION = 0x00C00000, WS_THICKFRAME = 0x00040000,
              WS_SYSMENU = 0x00080000, WS_MINIMIZEBOX = 0x00020000, WS_MAXIMIZEBOX = 0x00010000, WS_CHILD = 0x40000000;
    const uint NOSIZE = 0x0001, NOMOVE = 0x0002, NOZ = 0x0004, NOACT = 0x0010, FRAME = 0x0020;

    static void SetOwner(IntPtr h, IntPtr owner)
    {
        if (IntPtr.Size == 8) SetWindowLongPtr64(h, GWLP_HWNDPARENT, owner);
        else SetWindowLong(h, GWLP_HWNDPARENT, owner.ToInt32());
    }

    public static Dictionary<long, string> All()
    {
        var r = new Dictionary<long, string>();
        EnumWindows(delegate (IntPtr h, IntPtr l)
        {
            if (IsWindowVisible(h))
            {
                var sb = new StringBuilder(512);
                GetWindowText(h, sb, 512);
                if (sb.Length > 0) r[h.ToInt64()] = sb.ToString();
            }
            return true;
        }, IntPtr.Zero);
        return r;
    }

    public static string ClassOf(IntPtr h)
    {
        var sb = new StringBuilder(256);
        GetClassName(h, sb, 256);
        return sb.ToString();
    }

    static readonly Dictionary<uint, string> procCache = new Dictionary<uint, string>();
    public static string ProcessOf(IntPtr h)
    {
        try
        {
            uint pid;
            GetWindowThreadProcessId(h, out pid);
            string n;
            lock (procCache)
            {
                if (procCache.TryGetValue(pid, out n)) return n;
            }
            n = Process.GetProcessById((int)pid).ProcessName.ToLowerInvariant();
            lock (procCache) { procCache[pid] = n; }
            return n;
        }
        catch { return ""; }
    }


    public static int[] Size(IntPtr h)
    {
        RECT r;
        GetWindowRect(h, out r);
        return new int[] { r.R - r.L, r.B - r.T };
    }

    // 앱 창을 테두리 없는 창으로 바꾸고 큰 창에 딸린 창(owner)으로 만듦 — 자식 창이 아니라서 키보드 입력이 정상
    public static int Attach(IntPtr h, IntPtr owner)
    {
        ShowWindow(h, 9);
        int old = GetWindowLong(h, GWL_STYLE);
        int s = (old & ~(WS_CAPTION | WS_THICKFRAME | WS_SYSMENU | WS_MINIMIZEBOX | WS_MAXIMIZEBOX | WS_CHILD)) | WS_POPUP;
        SetWindowLong(h, GWL_STYLE, s);
        SetOwner(h, owner);
        SetWindowPos(h, IntPtr.Zero, 0, 0, 0, 0, NOSIZE | NOMOVE | NOZ | NOACT | FRAME);
        return old;
    }

    [DllImport("user32.dll")] static extern int SetWindowRgn(IntPtr h, IntPtr rgn, bool redraw);
    [DllImport("gdi32.dll")] static extern IntPtr CreateRoundRectRgn(int l, int t, int r, int b, int w, int h);

    // 창에서 (0, top)부터 w×hh 부분만 보이게 자름 — 위쪽 제목 줄은 감추고 모서리는 칸처럼 둥글게
    public static void Clip(IntPtr h, int top, int w, int hh, int rad)
    {
        IntPtr rg = CreateRoundRectRgn(0, top, w + 1, top + hh + 1, rad, rad);
        if (SetWindowRgn(h, rg, true) == 0) DeleteObject(rg);
    }
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr o);

    [DllImport("user32.dll")] static extern int GetWindowRgnBox(IntPtr h, out RECT r);
    // 창에 잘라 둔 모양이 남아 있는지 (앱이 스스로 풀어 버리는 경우가 있음)
    public static bool HasRgn(IntPtr h)
    {
        RECT r;
        return GetWindowRgnBox(h, out r) > 1;   // 0 오류, 1 빈 모양, 2·3 있음
    }

    public static void Unclip(IntPtr h)
    {
        if (IsWindow(h)) SetWindowRgn(h, IntPtr.Zero, true);
    }

    public static void Place(IntPtr h, int x, int y, int w, int hh)
    {
        SetWindowPos(h, IntPtr.Zero, x, y, w, hh, NOZ | NOACT | 0x4000);   // ASYNC: 상대 앱이 바빠도 안 기다림
    }

    public static void Detach(IntPtr h, int oldStyle, int x, int y, int w, int hh)
    {
        if (!IsWindow(h)) return;
        Unclip(h);
        SetOwner(h, IntPtr.Zero);
        SetWindowLong(h, GWL_STYLE, oldStyle);
        SetWindowPos(h, IntPtr.Zero, x, y, w, hh, NOZ | FRAME);
        ShowWindow(h, 5);
    }
}

// 고를 수 있는 앱 목록의 한 항목
class AppInfo
{
    public string Name, Keys, Url, Color;   // Keys: 창 제목에서 찾을 단어들(| 로 구분)
    public AppInfo(string name, string keys, string url, string color) { Name = name; Keys = keys; Url = url; Color = color; }
    public override string ToString() { return Name; }
    public int SendPref = -1;   // 한 번에 보내기 대상 여부 (-1이면 AI 이름이면 켬)
    static readonly System.Text.RegularExpressions.Regex AiRe = new System.Text.RegularExpressions.Regex(
        "chatgpt|gemini|claude|grok|perplexity|copilot|deepseek|뤼튼|wrtn|le chat|mistral|notebooklm|클로드|제미나이|퍼플렉시티",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    public bool IsAI { get { return AiRe.IsMatch(Name); } }
    public bool SameAs(AppInfo o) { return o != null && o.Name == Name && o.Url == Url; }
    public static readonly AppInfo CustomEntry = new AppInfo("직접 입력 (사이트 주소)...", "", null, "#475569");
    public static readonly AppInfo InstalledEntry = new AppInfo("설치된 프로그램에서 고르기...", "", null, "#475569");
    public bool IsInstalled { get { return Url != null && Url.StartsWith("app:"); } }
    public bool IsSpecial { get { return Url == null; } }

    // 시작 메뉴의 프로그램으로 만들기 (창 제목 또는 실행 파일 이름으로 찾음)
    public static AppInfo FromInstalled(string name, string id)
    {
        string keys = name;
        try
        {
            if (id.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                keys += "|" + Path.GetFileNameWithoutExtension(id);
        }
        catch { }
        string c = CustomColors[colorIdx % CustomColors.Length];
        colorIdx++;
        return new AppInfo(name, keys, "app:" + id, c);
    }

    public static readonly AppInfo[] Presets = new AppInfo[] {
        new AppInfo("ChatGPT",    "ChatGPT",            "https://chatgpt.com/",                 "#10a37f"),
        new AppInfo("Gemini",     "Gemini",             "https://gemini.google.com/app",        "#4285f4"),
        new AppInfo("Grok",       "Grok",               "https://grok.com/",                    "#6b7280"),
        new AppInfo("Claude",     "Claude",             "https://claude.ai/new",                "#d97757"),
        new AppInfo("Perplexity", "Perplexity",         "https://www.perplexity.ai/",           "#1f8a99"),
        new AppInfo("Copilot",    "Copilot",            "https://copilot.microsoft.com/",       "#7a5af8"),
        new AppInfo("DeepSeek",   "DeepSeek",           "https://chat.deepseek.com/",           "#4d6bfe"),
        new AppInfo("뤼튼",       "뤼튼|wrtn",          "https://wrtn.ai/",                     "#7c3aed"),
        new AppInfo("NotebookLM", "NotebookLM",         "https://notebooklm.google.com/",       "#3c4043"),
        new AppInfo("Le Chat",    "Le Chat|mistral",    "https://chat.mistral.ai/",             "#fa520f")
    };
    static readonly string[] CustomColors = new string[] { "#0ea5e9", "#e11d48", "#16a34a", "#ca8a04", "#9333ea", "#475569" };
    static int colorIdx = 0;

    public static AppInfo Custom(string name, string url)
    {
        if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) url = "https://" + url;
        string keys = name;
        try
        {
            string[] parts = new Uri(url).Host.Split('.');
            if (parts.Length >= 2) keys += "|" + parts[parts.Length - 2];
        }
        catch { }
        string c = CustomColors[colorIdx % CustomColors.Length];
        colorIdx++;
        return new AppInfo(name, keys, url, c);
    }
}

// ---------- 글래스모피즘 테마 ----------
static class Th
{
    public static readonly Color Bg1 = ColorTranslator.FromHtml("#e9e4ff");
    public static readonly Color Bg2 = ColorTranslator.FromHtml("#d9eeff");
    public static readonly Color Glass = ColorTranslator.FromHtml("#f8f7ff");
    public static readonly Color Card = Color.White;
    public static readonly Color Body = ColorTranslator.FromHtml("#f4f3fa");
    public static readonly Color Text = ColorTranslator.FromHtml("#24242e");
    public static readonly Color Sub = ColorTranslator.FromHtml("#8a879e");
    public static readonly Color ChipBg = ColorTranslator.FromHtml("#efedf8");
    public static readonly Color ChipHover = ColorTranslator.FromHtml("#e4e1f3");
    public static readonly Color ChipText = ColorTranslator.FromHtml("#4a4860");
    public static readonly Color Accent = ColorTranslator.FromHtml("#6d5bf6");
    public static readonly Color AccentSoft = ColorTranslator.FromHtml("#e9e5ff");

    public static GraphicsPath RoundPath(Rectangle r, int rad)
    {
        var p = new GraphicsPath();
        int d = rad * 2;
        if (r.Width < d || r.Height < d) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    // 모서리를 둥글게 (크기가 바뀔 때마다 다시)
    public static void Round(Control c, int rad)
    {
        EventHandler h = delegate
        {
            if (c.Width > 0 && c.Height > 0) c.Region = new Region(RoundPath(new Rectangle(0, 0, c.Width, c.Height), rad));
        };
        c.SizeChanged += h;
        h(null, EventArgs.Empty);
    }

    public static void Chip(Button b, bool primary)
    {
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderSize = 0;
        b.BackColor = primary ? Accent : ChipBg;
        b.ForeColor = primary ? Color.White : ChipText;
        b.FlatAppearance.MouseOverBackColor = primary ? ColorTranslator.FromHtml("#5b49ea") : ChipHover;
        b.Cursor = Cursors.Hand;
        Round(b, 9);
    }

    public static void DoubleBuffer(Control c)
    {
        try
        {
            typeof(Control).GetProperty("DoubleBuffered", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(c, true, null);
        }
        catch { }
    }
}

// 아이콘(+글자) 버튼 — 윈도우 기본 아이콘 글꼴(Segoe Fluent Icons / Segoe MDL2 Assets)로 그림
class IconBtn : Control
{
    public string Glyph = "";
    public bool Primary;     // 보라 바탕 (주요 버튼)
    public bool Ghost;       // 평소엔 바탕 없음, 마우스 올리면 연한 바탕
    public bool Active;      // 켜짐 상태(연보라 바탕 + 보라 아이콘)
    public bool Strike;      // 꺼짐 표시(아이콘에 사선)
    public int Radius = 9;
    bool hover, down;
    static Font iconFont;

    public static Font IconFont(float size)
    {
        foreach (string n in new string[] { "Segoe Fluent Icons", "Segoe MDL2 Assets" })
        {
            try
            {
                var f = new Font(n, size);
                if (f.Name == n) return f;
                f.Dispose();
            }
            catch { }
        }
        return new Font("Segoe UI Symbol", size);
    }

    public IconBtn()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        SetStyle(ControlStyles.Selectable, false);
        Cursor = Cursors.Hand;
        Font = new Font("Malgun Gothic", 9.5f, FontStyle.Bold);
        if (iconFont == null) iconFont = IconFont(10.5f);
        BackColor = Color.Transparent;
    }

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }

    // 글자까지 들어가는 데 필요한 너비
    public int Need()
    {
        if (string.IsNullOrEmpty(Text)) return Height;
        using (Graphics g = CreateGraphics())
            return (int)(14 + 18 + 7 + g.MeasureString(Text, Font).Width + 12);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        Color bg, fg;
        if (Primary) { bg = hover ? ColorTranslator.FromHtml("#5b49ea") : Th.Accent; fg = Color.White; }
        else if (Active) { bg = hover ? ColorTranslator.FromHtml("#ddd6ff") : Th.AccentSoft; fg = Th.Accent; }
        else if (Ghost) { bg = down ? Th.ChipHover : hover ? Th.ChipBg : Color.Empty; fg = hover ? Th.Text : Th.ChipText; }
        else { bg = hover ? Th.ChipHover : Th.ChipBg; fg = Th.ChipText; }
        if (Strike && !Active) fg = Th.Sub;
        if (!Enabled) { fg = Color.FromArgb(150, fg); }
        if (down && !Ghost) bg = ControlPaint.Dark(bg, 0.02f);
        if (bg != Color.Empty)
            using (var br = new SolidBrush(bg))
            using (GraphicsPath path = Th.RoundPath(new Rectangle(0, 0, Width - 1, Height - 1), Math.Min(Radius, Height / 2)))
                g.FillPath(br, path);
        bool hasText = !string.IsNullOrEmpty(Text);
        SizeF gs = g.MeasureString(Glyph, iconFont);
        float gx = hasText ? 14 : (Width - gs.Width) / 2f + 1;
        float gy = (Height - gs.Height) / 2f + 1;
        using (var br = new SolidBrush(fg))
        {
            if (Glyph.Length > 0) g.DrawString(Glyph, iconFont, br, gx, gy);
            if (hasText)
            {
                SizeF ts = g.MeasureString(Text, Font);
                g.DrawString(Text, Font, br, 14 + 18 + 7, (Height - ts.Height) / 2f + 1);
            }
        }
        if (Strike && !Active)
            using (var pen = new Pen(fg, 1.6f))
                g.DrawLine(pen, gx + 1, gy + gs.Height - 4, gx + gs.Width - 3, gy + 3);
    }
}

class Target
{
    public AppInfo App;
    public Panel Cell, Head, Body, Dot;
    public Label Lbl, Wait;
    public IconBtn Btn, Change, SendTg, Fresh;
    public bool SendOn;
    public IntPtr Hwnd = IntPtr.Zero;
    public int OldStyle;
    public bool Hidden, Web, Pending;
    public DateTime CheckAt;
    public bool WantNewChat;
    public bool Lost;      // 칸 안의 앱이 닫힘
    public int Missing;    // 연속으로 안 보인 횟수
    public int Misfit;     // 칸에서 벗어난 횟수
    public bool Grown;     // 최소 크기 때문에 칸을 자동으로 키웠는지
    public DateTime NewChatAt;
    public int CapH;       // 앱 자체 제목 줄 높이(최소화·닫기 버튼 아래끝까지), 0이면 없음
    public bool AutoHide;  // 제목 줄에 다른 버튼이 없어서 자동으로 감추는지
    public int HideTop = -1;   // -1 자동, 0 보이기, 1 감추기 (칸 메뉴에서 고름)
    public string Rgn = "";    // 마지막으로 자른 모양
    public int RgnLost;        // 앱이 잘라 둔 모양을 풀어 버린 횟수
    public int Top { get { return CapH <= 0 ? 0 : (HideTop == 1 || (HideTop < 0 && AutoHide)) ? CapH : 0; } }

    public Target(AppInfo a) { App = a; }

    // 이미 열려 있던 창이 정말 그 앱의 창인지 (탐색기 폴더·문서 제목에 이름만 들어간 창은 빼기)
    public bool IsAppWindow(string title, string proc)
    {
        if (App.IsInstalled) return true;
        string p = (proc ?? "").Replace(" ", "").ToLowerInvariant();
        foreach (string k in App.Keys.Split('|'))
        {
            if (k.Length == 0) continue;
            string kk = k.Replace(" ", "").ToLowerInvariant();
            if (p.Length > 0 && (p.Contains(kk) || kk.Contains(p))) return true;
            if (string.Equals(title.Trim(), k, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    public bool Matches(string title, string proc)
    {
        foreach (string k in App.Keys.Split('|'))
        {
            if (k.Length == 0) continue;
            if (title.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (App.IsInstalled && proc != null && proc.Equals(k, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}

class MainForm : Form
{
    readonly List<Target> T = new List<Target>();
    int solo = -1;
    Target drag = null, dropOn = null;
    Point dragStart;
    Panel area, bar;
    double[] colW = new double[0], rowW = new double[0];   // 칸 크기 비율
    string configFile, browser;
    bool firstRun;
    readonly HashSet<long> used = new HashSet<long>();
    readonly Dictionary<long, int> origStyles = new Dictionary<long, int>();   // 앱 창의 원래 모양
    Timer watch;
    readonly HashSet<long> released = new HashSet<long>();  // 풀어 둔 앱 창(다른 칸에서 바로 다시 쓸 수 있음)
    Dictionary<long, string> before;
    Dictionary<string, string> startApps;
    DateTime start;
    Timer timer;
    Font font = new Font("Malgun Gothic", 10, FontStyle.Bold);
    Font btnFont = new Font("Malgun Gothic", 9);

    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try
        {
            int off = 0, round = 2, mica = 2;
            DwmSetWindowAttribute(Handle, 20, ref off, 4);    // 밝은 제목 표시줄
            DwmSetWindowAttribute(Handle, 33, ref round, 4);  // 둥근 모서리 (윈도우 11)
            DwmSetWindowAttribute(Handle, 38, ref mica, 4);   // 반투명 유리 느낌 제목 표시줄 (윈도우 11)
        }
        catch { }
    }

    public MainForm()
    {
        string cfgDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RamSpirit");
        try { Directory.CreateDirectory(cfgDir); } catch { }
        configFile = Path.Combine(cfgDir, "apps.txt");
        firstRun = !File.Exists(configFile);
        browser = FindBrowser();

        Text = "RamSpirit";
        BackColor = Th.Bg1;
        WindowState = FormWindowState.Maximized;
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

        area = new Panel();
        area.Dock = DockStyle.Fill;
        Th.DoubleBuffer(area);
        // 파스텔 그라데이션 배경
        area.Paint += delegate (object o, PaintEventArgs e)
        {
            Rectangle r = area.ClientRectangle;
            if (r.Width <= 0 || r.Height <= 0) return;
            using (var br = new LinearGradientBrush(r, Th.Bg1, Th.Bg2, LinearGradientMode.ForwardDiagonal)) e.Graphics.FillRectangle(br, r);
        };
        area.Resize += delegate { area.Invalidate(); };
        Controls.Add(area);

        // 위쪽 막대: 왼쪽 앱 이름 · 모두 새 대화 · 칸 크기 같게 / 오른쪽 업데이트 · 버전 · 설정
        bar = new Panel();
        bar.Dock = DockStyle.Top; bar.Height = 52; bar.BackColor = Th.Glass;
        var logo = new PictureBox();
        logo.SizeMode = PictureBoxSizeMode.Zoom; logo.BackColor = Th.Glass;
        try { logo.Image = Icon.ExtractAssociatedIcon(Application.ExecutablePath).ToBitmap(); } catch { }
        logo.SetBounds(16, 15, 22, 22);
        bar.Controls.Add(logo);
        var brand = new Label();
        brand.Text = "RamSpirit"; brand.Font = new Font("Segoe UI", 12f, FontStyle.Bold); brand.ForeColor = Th.Text;
        brand.AutoSize = true; brand.BackColor = Th.Glass; brand.Location = new Point(44, 14);
        bar.Controls.Add(brand);
        var allNew = new IconBtn();
        allNew.Glyph = "\uE70F"; allNew.Text = "모두 새 대화";
        allNew.Click += delegate { foreach (Target x in T.ToArray()) if (x.Cell.Visible) NewChat(x); };
        tip.SetToolTip(allNew, "보이는 칸을 모두 새 대화로 바꿔요");
        bar.Controls.Add(allNew);
        var resetBtn = new IconBtn();
        resetBtn.Glyph = "\uE8A9"; resetBtn.Text = "칸 크기 같게";
        resetBtn.Click += delegate { ResetRatio(); };
        tip.SetToolTip(resetBtn, "끌어서 바꾼 칸 크기를 모두 똑같이 되돌려요");
        bar.Controls.Add(resetBtn);
        var setBtn = new IconBtn();
        setBtn.Glyph = "\uE713"; setBtn.Text = "설정";
        setBtn.Click += delegate { OpenSettings(); };
        tip.SetToolTip(setBtn, "칸 수와 칸마다 열 앱 고르기");
        bar.Controls.Add(setBtn);
        updBtn = new IconBtn();
        updBtn.Primary = true; updBtn.Glyph = "\uE896"; updBtn.Visible = false;
        updBtn.Click += delegate
        {
            try { Process.Start(Application.ExecutablePath); } catch { }
            Close();
        };
        bar.Controls.Add(updBtn);
        verLbl = new Label();
        verLbl.Text = "v" + Updater.Version; verLbl.Font = btnFont; verLbl.ForeColor = Th.Sub; verLbl.BackColor = Th.Glass;
        verLbl.TextAlign = ContentAlignment.MiddleRight;
        bar.Controls.Add(verLbl);
        // 아래쪽 가는 선
        bar.Paint += delegate (object o, PaintEventArgs e)
        {
            using (var pen = new Pen(ColorTranslator.FromHtml("#e7e4f5"))) e.Graphics.DrawLine(pen, 0, bar.Height - 1, bar.Width, bar.Height - 1);
        };
        bar.Resize += delegate { LayoutBar(); };
        barBtns = new IconBtn[] { allNew, resetBtn, setBtn };
        Controls.Add(bar);
        BuildComposer();

        foreach (AppInfo a in LoadConfig()) AddTarget(a);

        area.Resize += delegate { DoLayout(); };
        // 칸 사이 틈(가운데 십자 포함)을 끌어서 칸 크기 조절
        area.MouseMove += delegate (object o, MouseEventArgs e) { SplitMove(e); };
        area.MouseDown += delegate (object o, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || solo >= 0) return;
            dragColAt = HitCol(e.X); dragRowAt = HitRow(e.Y);
        };
        area.MouseUp += delegate
        {
            if (dragColAt >= 0 || dragRowAt >= 0) { dragColAt = -1; dragRowAt = -1; SaveConfig(); }
        };
        Move += delegate { SyncWindows(); };
        Resize += delegate { SyncWindows(); };
        FormClosing += delegate { ReleaseAll(); };
        Shown += delegate { OnFirstShown(); };
    }

    // ---------- 설정 저장/불러오기 (apps.txt: 이름 TAB 찾을단어 TAB 주소 TAB 색) ----------
    List<AppInfo> LoadConfig()
    {
        var list = new List<AppInfo>();
        double[] cw = null, rw = null;
        try
        {
            if (File.Exists(configFile))
            {
                foreach (string line in File.ReadAllLines(configFile, Encoding.UTF8))
                {
                    string[] p = line.Split('\t');
                    if (p.Length == 3 && p[0] == "#ratio") { cw = ParseNums(p[1]); rw = ParseNums(p[2]); continue; }
                    if (line.StartsWith("#")) continue;
                    if ((p.Length == 4 || p.Length == 5) && p[0].Length > 0 && p[2].Length > 0)
                    {
                        var a = new AppInfo(p[0], p[1], p[2], p[3]);
                        if (p.Length == 5) a.SendPref = p[4] == "1" ? 1 : 0;
                        list.Add(a);
                    }
                }
            }
        }
        catch { }
        if (list.Count < 2 || list.Count > 8 || list.Count % 2 != 0)
        {
            list.Clear();
            for (int i = 0; i < 4; i++) list.Add(AppInfo.Presets[i]);
        }
        if (cw != null && rw != null) { colW = cw; rowW = rw; }
        return list;
    }

    static double[] ParseNums(string s)
    {
        try
        {
            string[] p = s.Split(',');
            var r = new double[p.Length];
            for (int i = 0; i < p.Length; i++) r[i] = double.Parse(p[i], System.Globalization.CultureInfo.InvariantCulture);
            return r;
        }
        catch { return null; }
    }

    static string JoinNums(double[] a)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < a.Length; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(a[i].ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }

    void SaveConfig()
    {
        try
        {
            var lines = new List<string>();
            foreach (Target t in T) lines.Add(t.App.Name + "\t" + t.App.Keys + "\t" + t.App.Url + "\t" + t.App.Color + "\t" + (t.SendOn ? "1" : "0"));
            lines.Add("#ratio\t" + JoinNums(colW) + "\t" + JoinNums(rowW));
            File.WriteAllLines(configFile, lines.ToArray(), Encoding.UTF8);
        }
        catch { }
    }

    static string FindBrowser()
    {
        string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string[] c = new string[] {
            Path.Combine(pf, @"Google\Chrome\Application\chrome.exe"),
            Path.Combine(pf86, @"Google\Chrome\Application\chrome.exe"),
            Path.Combine(local, @"Google\Chrome\Application\chrome.exe"),
            Path.Combine(pf86, @"Microsoft\Edge\Application\msedge.exe"),
            Path.Combine(pf, @"Microsoft\Edge\Application\msedge.exe")
        };
        foreach (string p in c) if (File.Exists(p)) return p;
        return null;
    }

    // 시작 메뉴에 등록된 앱 목록 (이름 → 실행 ID)
    static Dictionary<string, string> StartApps()
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            Type st = Type.GetTypeFromProgID("Shell.Application");
            object sh = Activator.CreateInstance(st);
            object folder = st.InvokeMember("NameSpace", BindingFlags.InvokeMethod, null, sh, new object[] { "shell:AppsFolder" });
            object items = folder.GetType().InvokeMember("Items", BindingFlags.InvokeMethod, null, folder, null);
            int n = (int)items.GetType().InvokeMember("Count", BindingFlags.GetProperty, null, items, null);
            for (int i = 0; i < n; i++)
            {
                object it = items.GetType().InvokeMember("Item", BindingFlags.InvokeMethod, null, items, new object[] { i });
                if (it == null) continue;
                string name = it.GetType().InvokeMember("Name", BindingFlags.GetProperty, null, it, null) as string;
                string path = it.GetType().InvokeMember("Path", BindingFlags.GetProperty, null, it, null) as string;
                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(path) && !d.ContainsKey(name)) d[name] = path;
            }
        }
        catch { }
        return d;
    }

    void OpenWeb(Target t)
    {
        if (t.App.IsInstalled || t.App.IsSpecial) return;
        try
        {
            if (browser != null) Process.Start(browser, "--new-window --app=" + t.App.Url);
            else Process.Start(t.App.Url);
        }
        catch { }
    }

    // 설치된 앱이 있으면 그 앱을, 없으면 웹 앱 창을 켬
    void Launch(Target t)
    {
        if (t.App.IsInstalled)
        {
            try { Process.Start("explorer.exe", "shell:AppsFolder\\" + t.App.Url.Substring(4)); } catch { }
            return;
        }
        if (startApps == null) startApps = StartApps();
        string id = null;
        foreach (string n in new string[] { t.App.Name, "Google " + t.App.Name, t.App.Name + " AI" })
            if (startApps.TryGetValue(n, out id)) break;
        if (!string.IsNullOrEmpty(id))
        {
            try { Process.Start("explorer.exe", "shell:AppsFolder\\" + id); }
            catch { OpenWeb(t); t.Web = true; }
        }
        else { OpenWeb(t); t.Web = true; }
    }

    // ---------- 칸 만들기/없애기 ----------
    Target AddTarget(AppInfo a)
    {
        var t = new Target(a);

        var cell = new Panel();
        cell.BackColor = Th.Card;
        cell.Padding = new Padding(8, 0, 8, 8);
        Th.Round(cell, 16);

        var head = new Panel();
        head.Dock = DockStyle.Top; head.Height = 46; head.Padding = new Padding(0, 8, 2, 8);
        // 앱 색 동그라미
        var dot = new Panel();
        dot.Dock = DockStyle.Left; dot.Width = 26; dot.BackColor = Th.Card;
        dot.Paint += delegate (object o, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color c = ColorTranslator.FromHtml((string)(dot.Tag ?? "#6d5bf6"));
            using (var br = new SolidBrush(c)) e.Graphics.FillEllipse(br, 8, (dot.Height - 12) / 2, 12, 12);
        };

        var lbl = new Label();
        lbl.Dock = DockStyle.Fill; lbl.Font = font;
        lbl.ForeColor = Th.Text; lbl.TextAlign = ContentAlignment.MiddleLeft; lbl.BackColor = Th.Card;
        lbl.Cursor = Cursors.SizeAll;
        tip.SetToolTip(lbl, "끌어서 다른 칸과 자리 바꾸기 · 두 번 누르면 크게 보기");
        lbl.DoubleClick += delegate { ToggleSolo(t); };
        lbl.MouseDown += delegate (object s, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && solo < 0) { drag = t; dragStart = Control.MousePosition; dropOn = null; }
        };
        lbl.MouseMove += delegate
        {
            if (drag == null) return;
            Point m = Control.MousePosition;
            if (Math.Abs(m.X - dragStart.X) + Math.Abs(m.Y - dragStart.Y) < 12) return;
            Target k = CellAtMouse();
            if (k == drag) k = null;
            if (k != dropOn) { dropOn = k; Highlight(k); }
        };
        lbl.MouseUp += delegate
        {
            Target from = drag, to = dropOn;
            drag = null; dropOn = null;
            Highlight(null);
            if (from != null && to != null && from != to) SwapCells(from, to);
        };

        // 오른쪽 아이콘 버튼: [보내기 켬/끔] | [새 대화] [앱 바꾸기] [크게 보기]
        var tg = HeadBtn("\uE724");
        tg.Width = 34;
        tg.Click += delegate { t.SendOn = !t.SendOn; PaintToggle(t); SaveConfig(); UpdateHint(); };
        var gap = new Panel();
        gap.Dock = DockStyle.Right; gap.Width = 13; gap.BackColor = Th.Card;
        gap.Paint += delegate (object o, PaintEventArgs e)
        {
            using (var pen = new Pen(ColorTranslator.FromHtml("#e7e4f5"))) e.Graphics.DrawLine(pen, 6, 6, 6, gap.Height - 6);
        };
        var fresh = HeadBtn("\uE70F");
        tip.SetToolTip(fresh, "새 대화");
        fresh.Click += delegate { NewChat(t); };
        var change = HeadBtn("\uE8AB");
        tip.SetToolTip(change, "앱 바꾸기");
        change.Click += delegate { ShowAppMenu(t); };
        var btn = HeadBtn("\uE740");
        btn.Click += delegate { ToggleSolo(t); };

        head.Controls.Add(lbl);
        head.Controls.Add(dot);
        head.Controls.Add(tg);
        head.Controls.Add(gap);
        head.Controls.Add(fresh);
        head.Controls.Add(change);
        head.Controls.Add(btn);

        var body = new Panel();
        body.Dock = DockStyle.Fill; body.BackColor = Th.Body;
        Th.Round(body, 10);

        var wait = new Label();
        wait.Dock = DockStyle.Fill; wait.Text = "불러오는 중..."; wait.ForeColor = Th.Sub;
        wait.TextAlign = ContentAlignment.MiddleCenter; wait.Font = font;
        wait.Click += delegate { if (t.Lost || t.Hwnd == IntPtr.Zero) Reopen(t); };
        body.Controls.Add(wait);

        cell.Controls.Add(body);
        cell.Controls.Add(head);
        area.Controls.Add(cell);

        t.Dot = dot;
        t.Cell = cell; t.Head = head; t.Lbl = lbl; t.Body = body; t.Btn = btn; t.Change = change; t.Wait = wait; t.SendTg = tg; t.Fresh = fresh;
        t.SendOn = a.SendPref >= 0 ? a.SendPref == 1 : a.IsAI;
        PaintToggle(t);
        ApplyLook(t);
        T.Add(t);
        return t;
    }

    // 칸 제목 줄의 아이콘 버튼 (오른쪽에 붙고, 버튼 사이에 2px 틈)
    IconBtn HeadBtn(string glyph)
    {
        var b = new IconBtn();
        b.Glyph = glyph; b.Ghost = true; b.Radius = 8;
        b.Dock = DockStyle.Right; b.Width = 32;
        b.BackColor = Th.Card;
        return b;
    }

    void ApplyLook(Target t)
    {
        t.Head.BackColor = Th.Card;
        t.Lbl.ForeColor = Th.Text;
        if (t.Dot != null) { t.Dot.Tag = t.App.Color; t.Dot.Invalidate(); }
        t.Lbl.Text = "  " + t.App.Name;
    }

    // 붙어 있던 앱 창을 풀어서 최소화 (앱은 꺼지지 않음)
    void LetGo(Target t)
    {
        if (t.Hwnd != IntPtr.Zero)
        {
            W.Detach(t.Hwnd, t.OldStyle, 100, 100, 1100, 800);
            W.ShowWindow(t.Hwnd, 6);
            used.Remove(t.Hwnd.ToInt64());
            released.Add(t.Hwnd.ToInt64());
            t.Hwnd = IntPtr.Zero;
        }
        t.Pending = false; t.Hidden = false; t.Web = false;
    }

    // ---------- 설정 창 ----------
    void OpenSettings()
    {
        List<AppInfo> chosen = SettingsDialog();
        if (chosen != null) ApplySettings(chosen, true);
    }

    List<AppInfo> SettingsDialog()
    {
        var f = new Form();
        f.Text = firstRun ? "RamSpirit — 처음 설정" : "RamSpirit — 설정";
        f.FormBorderStyle = FormBorderStyle.FixedDialog;
        f.StartPosition = FormStartPosition.CenterScreen;
        f.MaximizeBox = false; f.MinimizeBox = false;
        f.ClientSize = new Size(440, 470);
        f.Font = btnFont;

        var intro = new Label();
        intro.Text = firstRun
            ? "처음 한 번만 정하면 다음부터는 아이콘만 누르면 이대로 열려요.\n나중에 위쪽 「설정」에서 언제든 바꿀 수 있어요."
            : "칸 수와 칸마다 열 앱을 고르세요.";
        intro.SetBounds(16, 12, 410, 40);
        f.Controls.Add(intro);

        var lc = new Label(); lc.Text = "칸 수"; lc.SetBounds(16, 62, 60, 22);
        var count = new ComboBox(); count.DropDownStyle = ComboBoxStyle.DropDownList; count.SetBounds(80, 58, 120, 24);
        foreach (int n in new int[] { 2, 4, 6, 8 }) count.Items.Add(n + "칸");
        f.Controls.Add(lc); f.Controls.Add(count);

        var order = new Label();
        order.Text = "칸 번호: 윗줄 왼쪽부터 → 오른쪽, 그다음 아랫줄";
        order.ForeColor = Th.Sub; order.SetBounds(16, 90, 410, 20);
        f.Controls.Add(order);

        var labels = new List<Label>();
        var boxes = new List<ComboBox>();
        var usedNames = new List<string>();
        for (int i = 0; i < 8; i++)
        {
            AppInfo cur = null;
            if (i < T.Count) cur = T[i].App;
            else
            {
                foreach (AppInfo pr in AppInfo.Presets)
                {
                    bool taken = usedNames.Contains(pr.Name);
                    for (int j = 0; j < T.Count && !taken; j++) if (T[j].App.Name == pr.Name) taken = true;
                    if (!taken) { cur = pr; break; }
                }
                if (cur == null) cur = AppInfo.Presets[i % AppInfo.Presets.Length];
            }
            usedNames.Add(cur.Name);

            var l = new Label(); l.Text = (i + 1) + "번 칸"; l.SetBounds(16, 120 + i * 34, 60, 22);
            var cb = new ComboBox(); cb.DropDownStyle = ComboBoxStyle.DropDownList; cb.SetBounds(80, 116 + i * 34, 340, 24);
            bool found = false;
            foreach (AppInfo pr in AppInfo.Presets) { cb.Items.Add(pr); if (pr.SameAs(cur)) { cb.SelectedItem = pr; found = true; } }
            if (!found) { cb.Items.Add(cur); cb.SelectedItem = cur; }
            cb.Items.Add(AppInfo.InstalledEntry);
            cb.Items.Add(AppInfo.CustomEntry);
            cb.Tag = cb.SelectedIndex;
            ComboBox me = cb;
            cb.SelectedIndexChanged += delegate
            {
                if (me.SelectedItem == AppInfo.CustomEntry || me.SelectedItem == AppInfo.InstalledEntry)
                {
                    AppInfo a = (me.SelectedItem == AppInfo.CustomEntry) ? AskCustom(f) : PickInstalled(f);
                    if (a != null) { me.Items.Insert(me.Items.Count - 2, a); me.SelectedItem = a; }
                    else me.SelectedIndex = (int)me.Tag;
                }
                me.Tag = me.SelectedIndex;
            };
            f.Controls.Add(l); f.Controls.Add(cb);
            labels.Add(l); boxes.Add(cb);
        }

        count.SelectedIndexChanged += delegate
        {
            int n = (count.SelectedIndex + 1) * 2;
            for (int i = 0; i < 8; i++) { labels[i].Visible = i < n; boxes[i].Visible = i < n; }
        };
        count.SelectedIndex = Math.Max(0, Math.Min(3, T.Count / 2 - 1));

        var ok = new Button(); ok.Text = firstRun ? "이대로 시작" : "저장하고 적용"; ok.SetBounds(212, 432, 120, 28); ok.DialogResult = DialogResult.OK;
        var cancel = new Button(); cancel.Text = "취소"; cancel.SetBounds(338, 432, 82, 28); cancel.DialogResult = DialogResult.Cancel;
        f.Controls.Add(ok); f.Controls.Add(cancel);
        f.AcceptButton = ok; f.CancelButton = cancel;

        if (f.ShowDialog(this) != DialogResult.OK) return null;
        int total = (count.SelectedIndex + 1) * 2;
        var result = new List<AppInfo>();
        for (int i = 0; i < total; i++) result.Add((AppInfo)boxes[i].SelectedItem);
        return result;
    }

    // 고른 구성대로 칸을 맞춤 (같은 앱인 칸은 그대로 두고 바뀐 칸만 다시 엶)
    void ApplySettings(List<AppInfo> apps, bool launch)
    {
        solo = -1;
        var toOpen = new List<Target>();
        int oldCols = T.Count >= 4 ? T.Count / 2 : T.Count;
        while (T.Count > apps.Count)
        {
            Target t = T[T.Count - 1];
            LetGo(t);
            area.Controls.Remove(t.Cell);
            t.Cell.Dispose();
            T.RemoveAt(T.Count - 1);
        }
        for (int i = 0; i < apps.Count; i++)
        {
            if (i < T.Count)
            {
                if (T[i].App.SameAs(apps[i])) continue;
                LetGo(T[i]);
                T[i].App = apps[i];
                T[i].SendOn = apps[i].SendPref >= 0 ? apps[i].SendPref == 1 : apps[i].IsAI;
                PaintToggle(T[i]);
                ApplyLook(T[i]);
                T[i].Wait.Text = "불러오는 중..."; T[i].Wait.Visible = true;
                toOpen.Add(T[i]);
            }
            else toOpen.Add(AddTarget(apps[i]));
        }
        int newCols = T.Count >= 4 ? T.Count / 2 : T.Count;
        if (newCols != oldCols) { colW = new double[0]; rowW = new double[0]; }
        SaveConfig();
        DoLayout();
        if (launch && toOpen.Count > 0) StartSearch(toOpen);
    }

    // ---------- 칸 크기 비율 ----------
    void EnsureRatio(int cols, int rows)
    {
        if (colW.Length != cols) { colW = new double[cols]; for (int i = 0; i < cols; i++) colW[i] = 1; }
        if (rowW.Length != rows) { rowW = new double[rows]; for (int i = 0; i < rows; i++) rowW[i] = 1; }
    }

    void Grow(Target t, double f)
    {
        if (solo >= 0) return;
        int n = T.Count, rows = n >= 4 ? 2 : 1, cols = n / rows;
        EnsureRatio(cols, rows);
        int i = T.IndexOf(t);
        int c = i % cols, r = i / cols;
        colW[c] = Math.Max(0.3, Math.Min(4, colW[c] * f));
        if (rows > 1) rowW[r] = Math.Max(0.3, Math.Min(4, rowW[r] * f));
        SaveConfig();
        DoLayout();
        RecheckSizes();
    }

    void ResetRatio()
    {
        colW = new double[0]; rowW = new double[0];
        SaveConfig();
        DoLayout();
    }

    // 칸이 작아져서 앱이 넘치는지 다시 확인
    void RecheckSizes()
    {
        bool any = false;
        foreach (Target t in T)
            if (t.Hwnd != IntPtr.Zero && !t.Web) { t.Pending = true; t.CheckAt = DateTime.Now.AddMilliseconds(500); any = true; }
        if (any && timer != null && !timer.Enabled) { start = DateTime.Now; timer.Start(); }
    }

    // ---------- 앱 바꾸기 ----------
    void ShowAppMenu(Target t)
    {
        var menu = new ContextMenuStrip();
        foreach (AppInfo p in AppInfo.Presets)
        {
            AppInfo pick = p;
            var item = new ToolStripMenuItem(p.Name);
            if (p.Name == t.App.Name) item.Checked = true;
            item.Click += delegate { ChangeApp(t, pick); };
            menu.Items.Add(item);
        }
        menu.Items.Add(new ToolStripSeparator());
        var inst = new ToolStripMenuItem("설치된 프로그램에서 고르기...");
        inst.Click += delegate
        {
            AppInfo a = PickInstalled(this);
            if (a != null) ChangeApp(t, a);
        };
        menu.Items.Add(inst);
        var custom = new ToolStripMenuItem("직접 입력 (사이트 주소)...");
        custom.Click += delegate
        {
            AppInfo a = AskCustom(this);
            if (a != null) ChangeApp(t, a);
        };
        menu.Items.Add(custom);
        if (t.CapH > 0)
        {
            menu.Items.Add(new ToolStripSeparator());
            var bar = new ToolStripMenuItem("앱 위쪽 제목 줄 감추기");
            bar.Checked = t.Top > 0;
            bar.Click += delegate { t.HideTop = t.Top > 0 ? 0 : 1; t.Rgn = ""; SyncWindows(); };
            menu.Items.Add(bar);
        }
        menu.Show(t.Change, new Point(0, t.Change.Height));
    }

    // 설치된 프로그램 목록에서 검색해서 고르기
    AppInfo PickInstalled(IWin32Window owner)
    {
        if (startApps == null) startApps = StartApps();
        var names = new List<string>(startApps.Keys);
        names.Sort(StringComparer.CurrentCultureIgnoreCase);

        var f = new Form();
        f.Text = "설치된 프로그램에서 고르기";
        f.FormBorderStyle = FormBorderStyle.FixedDialog;
        f.StartPosition = FormStartPosition.CenterParent;
        f.MaximizeBox = false; f.MinimizeBox = false;
        f.ClientSize = new Size(420, 500);
        f.Font = btnFont;

        var l = new Label(); l.Text = "검색 (예: 카카오톡, 한글, 메모장)"; l.SetBounds(16, 12, 380, 20);
        var search = new TextBox(); search.SetBounds(16, 34, 388, 24);
        var list = new ListBox(); list.SetBounds(16, 66, 388, 380);
        var ok = new Button(); ok.Text = "고르기"; ok.SetBounds(232, 458, 82, 28); ok.DialogResult = DialogResult.OK;
        var cancel = new Button(); cancel.Text = "취소"; cancel.SetBounds(322, 458, 82, 28); cancel.DialogResult = DialogResult.Cancel;
        f.Controls.Add(l); f.Controls.Add(search); f.Controls.Add(list); f.Controls.Add(ok); f.Controls.Add(cancel);
        f.AcceptButton = ok; f.CancelButton = cancel;

        EventHandler fill = delegate
        {
            string q = search.Text.Trim();
            list.BeginUpdate();
            list.Items.Clear();
            foreach (string n in names)
                if (q.Length == 0 || n.IndexOf(q, StringComparison.CurrentCultureIgnoreCase) >= 0) list.Items.Add(n);
            if (list.Items.Count > 0) list.SelectedIndex = 0;
            list.EndUpdate();
        };
        search.TextChanged += fill;
        list.DoubleClick += delegate { if (list.SelectedItem != null) { f.DialogResult = DialogResult.OK; f.Close(); } };
        fill(null, EventArgs.Empty);

        if (f.ShowDialog(owner) != DialogResult.OK || list.SelectedItem == null) return null;
        string pick = (string)list.SelectedItem;
        return AppInfo.FromInstalled(pick, startApps[pick]);
    }

    AppInfo AskCustom(IWin32Window owner)
    {
        var f = new Form();
        f.Text = "직접 입력";
        f.FormBorderStyle = FormBorderStyle.FixedDialog;
        f.StartPosition = FormStartPosition.CenterParent;
        f.MaximizeBox = false; f.MinimizeBox = false;
        f.ClientSize = new Size(420, 170);
        f.Font = btnFont;

        var l1 = new Label(); l1.Text = "이름 (예: 뤼튼, 클로바X)"; l1.SetBounds(16, 14, 380, 20);
        var name = new TextBox(); name.SetBounds(16, 36, 388, 24);
        var l2 = new Label(); l2.Text = "사이트 주소 (예: https://clova-x.naver.com)"; l2.SetBounds(16, 70, 380, 20);
        var url = new TextBox(); url.SetBounds(16, 92, 388, 24);
        var ok = new Button(); ok.Text = "확인"; ok.SetBounds(232, 130, 82, 28); ok.DialogResult = DialogResult.OK;
        var cancel = new Button(); cancel.Text = "취소"; cancel.SetBounds(322, 130, 82, 28); cancel.DialogResult = DialogResult.Cancel;
        f.Controls.Add(l1); f.Controls.Add(name); f.Controls.Add(l2); f.Controls.Add(url); f.Controls.Add(ok); f.Controls.Add(cancel);
        f.AcceptButton = ok; f.CancelButton = cancel;

        if (f.ShowDialog(owner) != DialogResult.OK) return null;
        string n = name.Text.Trim(), u = url.Text.Trim();
        if (n.Length == 0 || u.Length == 0) return null;
        return AppInfo.Custom(n, u);
    }

    void ChangeApp(Target t, AppInfo a)
    {
        LetGo(t);
        t.App = a;
        t.SendOn = a.IsAI;
        PaintToggle(t);
        UpdateHint();
        ApplyLook(t);
        t.Wait.Text = "불러오는 중...";
        t.Wait.Visible = true;
        SaveConfig();
        StartSearch(new List<Target> { t });
    }

    // ---------- 배치 ----------
    void DoLayout()
    {
        if (area == null || T.Count == 0) return;
        int Wd = area.ClientSize.Width, Ht = area.ClientSize.Height, g = Gap;
        int n = T.Count;
        int rows = n >= 4 ? 2 : 1;
        int cols = n / rows;
        EnsureRatio(cols, rows);
        double sc = 0, sr = 0;
        foreach (double v in colW) sc += v;
        foreach (double v in rowW) sr += v;
        int availW = Wd - (cols + 1) * g, availH = Ht - (rows + 1) * g;
        int[] xs = new int[cols], ws = new int[cols], ys = new int[rows], hs = new int[rows];
        int x = g;
        for (int c = 0; c < cols; c++) { xs[c] = x; ws[c] = (c == cols - 1) ? (Wd - g - x) : (int)(availW * colW[c] / sc); x += ws[c] + g; }
        int y = g;
        for (int r = 0; r < rows; r++) { ys[r] = y; hs[r] = (r == rows - 1) ? (Ht - g - y) : (int)(availH * rowW[r] / sr); y += hs[r] + g; }
        Lx = xs; Lw = ws; Ly = ys; Lh = hs;   // 끌어서 크기 조절할 때 씀

        for (int i = 0; i < n; i++)
        {
            Target t = T[i];
            if (solo >= 0)
            {
                if (i == solo) { t.Cell.SetBounds(g, g, Wd - 2 * g, Ht - 2 * g); t.Cell.Visible = true; }
                else t.Cell.Visible = false;
            }
            else
            {
                int c = i % cols, r = i / cols;
                t.Cell.SetBounds(xs[c], ys[r], ws[c], hs[r]);
                t.Cell.Visible = true;
            }
            t.Btn.Glyph = (solo == i) ? "\uE73F" : "\uE740";
            tip.SetToolTip(t.Btn, (solo == i) ? "원래 칸 나누기로 돌아가기" : "이 칸만 크게 보기");
            t.Btn.Invalidate();
        }
        SyncWindows();
        UpdateHint();
    }

    // ---------- 칸 사이 틈 끌기 ----------
    const int Gap = 8;
    int[] Lx = new int[0], Lw = new int[0], Ly = new int[0], Lh = new int[0];
    int dragColAt = -1, dragRowAt = -1;
    DateTime lastSplit = DateTime.MinValue;

    int HitCol(int x)
    {
        for (int c = 0; c + 1 < Lx.Length; c++)
        {
            int gx = Lx[c] + Lw[c];
            if (x >= gx - 3 && x <= gx + Gap + 3) return c;
        }
        return -1;
    }

    int HitRow(int y)
    {
        for (int r = 0; r + 1 < Ly.Length; r++)
        {
            int gy = Ly[r] + Lh[r];
            if (y >= gy - 3 && y <= gy + Gap + 3) return r;
        }
        return -1;
    }

    void SplitMove(MouseEventArgs e)
    {
        if (dragColAt < 0 && dragRowAt < 0)
        {
            if (solo >= 0) { area.Cursor = Cursors.Default; return; }
            bool c = HitCol(e.X) >= 0, r = HitRow(e.Y) >= 0;
            area.Cursor = (c && r) ? Cursors.SizeAll : c ? Cursors.SizeWE : r ? Cursors.SizeNS : Cursors.Default;
            return;
        }
        if ((DateTime.Now - lastSplit).TotalMilliseconds < 30) return;   // 너무 자주 다시 그리지 않게
        lastSplit = DateTime.Now;
        int k = dragColAt;
        if (k >= 0 && k + 1 < Lx.Length && k + 1 < colW.Length)
        {
            int left = Lx[k], span = Lx[k + 1] + Lw[k + 1] - left - Gap;
            int px = Math.Max(160, Math.Min(span - 160, e.X - left));
            double total = colW[k] + colW[k + 1];
            if (span > 320) { colW[k] = total * px / span; colW[k + 1] = total - colW[k]; }
        }
        k = dragRowAt;
        if (k >= 0 && k + 1 < Ly.Length && k + 1 < rowW.Length)
        {
            int top = Ly[k], span = Ly[k + 1] + Lh[k + 1] - top - Gap;
            int py = Math.Max(120, Math.Min(span - 120, e.Y - top));
            double total = rowW[k] + rowW[k + 1];
            if (span > 240) { rowW[k] = total * py / span; rowW[k + 1] = total - rowW[k]; }
        }
        DoLayout();
    }

    void SyncWindows()
    {
        if (WindowState == FormWindowState.Minimized) return;
        foreach (Target t in T)
        {
            if (t.Hwnd == IntPtr.Zero || t.Body == null) continue;
            if (t.Cell.Visible)
            {
                Rectangle r = t.Body.RectangleToScreen(t.Body.ClientRectangle);
                int top = t.Top;
                W.Place(t.Hwnd, r.X, r.Y - top, r.Width, r.Height + top);
                ClipTo(t, top, r.Width, r.Height);
                if (t.Hidden) { W.ShowWindowAsync(t.Hwnd, 8); t.Hidden = false; }
            }
            else if (!t.Hidden)
            {
                W.ShowWindowAsync(t.Hwnd, 0);
                t.Hidden = true;
            }
        }
    }

    // 칸에 보이는 부분만 남기고 잘라 냄(바뀌었을 때만)
    void ClipTo(Target t, int top, int w, int h)
    {
        string key = top + "," + w + "," + h;
        if (key == t.Rgn || t.Hwnd == IntPtr.Zero) return;
        t.Rgn = key;
        W.Clip(t.Hwnd, top, w, h, 20);   // 칸 안쪽(반지름 10)과 같은 둥근 모서리
    }

    // 앱이 스스로 그린 위쪽 제목 줄(아이콘·제목·최소화·닫기)을 찾아서 그 높이만큼 감춤
    void FindTitleBar(Target t, IntPtr h, string proc)
    {
#if !NO_UIA
        bool browser = proc == "chrome" || proc == "msedge";
        RunBackground(delegate
        {
            for (int i = 0; i < 4 && t.Hwnd == h; i++)
            {
                System.Threading.Thread.Sleep(i == 0 ? 900 : 1200);
                int auto;
                int cap = Uia.TitleBarHeight(h, browser, out auto);
                if (cap > 0) { t.CapH = cap; t.AutoHide = auto == 1; break; }
            }
        }, delegate
        {
            if (t.Hwnd == h && t.CapH > 0) { t.Rgn = ""; SyncWindows(); }
        });
#endif
    }

    void ToggleSolo(Target t)
    {
        int i = T.IndexOf(t);
        solo = (solo == i) ? -1 : i;
        DoLayout();
    }

    Target CellAtMouse()
    {
        Point p = area.PointToClient(Control.MousePosition);
        foreach (Target t in T)
            if (t.Cell.Visible && t.Cell.Bounds.Contains(p)) return t;
        return null;
    }

    void Highlight(Target k)
    {
        foreach (Target t in T)
        {
            if (t == k)
            {
                t.Head.BackColor = Th.AccentSoft; t.Lbl.ForeColor = Th.Accent;
                t.Lbl.Text = "  " + t.App.Name + "  ← 여기에 놓으면 자리 바꾸기";
            }
            else ApplyLook(t);
        }
    }

    void SwapCells(Target a, Target b)
    {
        int ia = T.IndexOf(a), ib = T.IndexOf(b);
        T[ia] = b; T[ib] = a;
        SaveConfig();
        DoLayout();
    }

    // ---------- 한 번에 보내기 ----------
    TextBox q;
    IconBtn sendBtn;
    Label qHint;
    Label sendHint;

    void PaintToggle(Target t)
    {
        if (t.SendTg == null) return;
        t.SendTg.Active = t.SendOn;
        t.SendTg.Strike = !t.SendOn;
        t.SendTg.Invalidate();
        tip.SetToolTip(t.SendTg, t.SendOn ? "「모두에게 보내기」가 이 칸에도 들어가요 (누르면 끔)" : "이 칸에는 보내지 않아요 (누르면 켬)");
    }

    readonly List<string> attach = new List<string>();   // 함께 보낼 파일
    IconBtn fileBtn;

    void UpdateHint()
    {
        if (sendHint == null) return;
        int n = 0;
        foreach (Target t in T) if (t.SendOn && t.Cell.Visible) n++;
        string files = "";
        if (attach.Count > 0)
        {
            var names = new List<string>();
            for (int i = 0; i < attach.Count && i < 3; i++) names.Add(Path.GetFileName(attach[i]));
            files = "첨부 " + attach.Count + "개(" + string.Join(", ", names.ToArray()) + (attach.Count > 3 ? " 외" : "") + ") · ";
        }
        sendHint.Text = files + "Enter 보내기 · Shift+Enter 줄바꿈 · 보이는 칸 중 " + n + "개에 보내요";
        if (fileBtn != null) { fileBtn.Text = attach.Count > 0 ? "파일 " + attach.Count + "개" : "파일"; fileBtn.Active = attach.Count > 0; }
    }

    void AddFiles(string[] paths)
    {
        if (paths == null) return;
        foreach (string f in paths)
            if (File.Exists(f) && !attach.Contains(f)) attach.Add(f);
        UpdateHint();
    }

    void FileButtonClick()
    {
        if (attach.Count > 0)
        {
            var menu = new ContextMenuStrip();
            var more = new ToolStripMenuItem("파일 더 넣기...");
            more.Click += delegate { PickFiles(); };
            var clear = new ToolStripMenuItem("첨부 비우기");
            clear.Click += delegate { attach.Clear(); UpdateHint(); };
            menu.Items.Add(more); menu.Items.Add(clear);
            menu.Show(fileBtn, new Point(0, -menu.Height - 4));
            return;
        }
        PickFiles();
    }

    void PickFiles()
    {
        var d = new OpenFileDialog();
        d.Multiselect = true;
        d.Title = "모든 칸에 함께 보낼 파일 고르기";
        if (d.ShowDialog(this) == DialogResult.OK) AddFiles(d.FileNames);
    }

    void BuildComposer()
    {
        var comp = new Panel();
        comp.Dock = DockStyle.Bottom; comp.Height = 96; comp.BackColor = Th.Glass;
        var box = new Panel();
        box.BackColor = Th.Card;
        Th.Round(box, 16);
        q = new TextBox();
        q.Multiline = true; q.Font = new Font("Malgun Gothic", 11); q.BorderStyle = BorderStyle.None;
        q.BackColor = Th.Card; q.ForeColor = Th.Text;
        // 비어 있을 때 보이는 안내 글자
        qHint = new Label();
        qHint.Text = "모든 칸에 보낼 메시지를 입력하세요"; qHint.Font = q.Font; qHint.ForeColor = ColorTranslator.FromHtml("#aaa7bd");
        qHint.BackColor = Th.Card; qHint.Cursor = Cursors.IBeam;
        qHint.Click += delegate { q.Focus(); };
        q.TextChanged += delegate { qHint.Visible = q.Text.Length == 0; };
        box.Controls.Add(q);
        box.Controls.Add(qHint);
        qHint.BringToFront();
        sendBtn = new IconBtn();
        sendBtn.Primary = true; sendBtn.Glyph = "\uE724"; sendBtn.Text = "모두에게 보내기"; sendBtn.Radius = 14;
        sendBtn.Font = new Font("Malgun Gothic", 10.5f, FontStyle.Bold);
        sendHint = new Label();
        sendHint.Font = btnFont; sendHint.ForeColor = Th.Sub; sendHint.TextAlign = ContentAlignment.MiddleLeft;
        fileBtn = new IconBtn();
        fileBtn.Glyph = "\uE723"; fileBtn.Text = "파일"; fileBtn.Radius = 14;
        fileBtn.Font = new Font("Malgun Gothic", 10f, FontStyle.Bold);
        tip.SetToolTip(fileBtn, "모든 칸에 함께 보낼 파일 넣기 (입력창에 끌어다 놓아도 돼요)");
        fileBtn.Click += delegate { FileButtonClick(); };
        comp.Controls.Add(box); comp.Controls.Add(fileBtn); comp.Controls.Add(sendBtn); comp.Controls.Add(sendHint);
        // 파일을 입력창에 끌어다 놓으면 첨부
        foreach (Control dc in new Control[] { comp, box, q })
        {
            dc.AllowDrop = true;
            dc.DragEnter += delegate (object o, DragEventArgs e)
            {
                e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            };
            dc.DragDrop += delegate (object o, DragEventArgs e)
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop)) AddFiles((string[])e.Data.GetData(DataFormats.FileDrop));
            };
        }
        comp.Resize += delegate
        {
            int w = comp.ClientSize.Width;
            int sw = 172, fw = Math.Max(100, fileBtn.Need());
            box.SetBounds(16, 12, Math.Max(120, w - 16 - sw - 8 - fw - 12), 50);
            q.SetBounds(16, 12, Math.Max(80, box.Width - 32), 28);
            qHint.SetBounds(19, 13, Math.Max(80, box.Width - 40), 26);
            fileBtn.SetBounds(w - 16 - sw - 8 - fw, 12, fw, 50);
            sendBtn.SetBounds(w - 16 - sw, 12, sw, 50);
            sendHint.SetBounds(20, 66, Math.Max(100, w - 40), 24);
        };
        box.Click += delegate { q.Focus(); };
        q.KeyDown += delegate (object s, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && !e.Shift)
            {
                e.SuppressKeyPress = true;
                // 한글 입력 중 마지막 글자가 확정된 뒤 보내도록 한 박자 늦춤
                BeginInvoke((System.Windows.Forms.MethodInvoker)delegate { SendAll(); });
            }
        };
        sendBtn.Click += delegate { SendAll(); };
        Controls.Add(comp);
    }

    bool busy;   // 보내기·새 대화 작업 중 (겹치지 않게)
    IconBtn updBtn;
    IconBtn[] barBtns;
    Label verLbl;
    readonly ToolTip tip = new ToolTip();

    void LayoutBar()
    {
        if (barBtns == null) return;
        int h = 34, y = (bar.Height - h) / 2, x = 160;
        // 왼쪽: 모두 새 대화 · 칸 크기 같게
        for (int i = 0; i < 2; i++) { int w = barBtns[i].Need(); barBtns[i].SetBounds(x, y, w, h); x += w + 8; }
        // 오른쪽: [업데이트] 버전 [설정]
        int r = bar.ClientSize.Width - 16;
        int sw = barBtns[2].Need();
        barBtns[2].SetBounds(r - sw, y, sw, h); r -= sw + 10;
        verLbl.SetBounds(r - 60, y, 60, h); r -= 60 + 10;
        if (updBtn.Visible) { int uw = updBtn.Need(); updBtn.SetBounds(r - uw, y, uw, h); }
    }

    void CheckUpdateLater()
    {
        string got = null;
        RunBackground(delegate
        {
            System.Threading.Thread.Sleep(4000);
            got = Updater.CheckAndUpdate();
        }, delegate
        {
            if (got == null) return;
            updBtn.Text = "새 버전 v" + got + " 준비됨 · 다시 시작";
            updBtn.Visible = true;
            tip.SetToolTip(updBtn, "누르면 RamSpirit가 다시 켜지면서 새 버전이 적용돼요");
            LayoutBar();
        });
    }

    // 무거운 작업(다른 앱 조작·입력란 찾기)은 별도 일꾼 스레드에서 → 화면이 멈추지 않음
    void RunBackground(System.Threading.ThreadStart work, System.Windows.Forms.MethodInvoker done)
    {
        var th = new System.Threading.Thread(delegate ()
        {
            try { work(); } catch { }
            try { if (!IsDisposed) BeginInvoke(done); } catch { }
        });
        th.IsBackground = true;
        th.Start();
    }

    void SendAll()
    {
        if (busy) return;
        string text = q.Text.Trim();
        var files = new List<string>(attach);
        if (text.Length == 0 && files.Count == 0) return;
        var targets = new List<IntPtr>();
        foreach (Target t in T)
            if (t.SendOn && t.Cell.Visible && t.Hwnd != IntPtr.Zero && W.IsWindow(t.Hwnd)) targets.Add(t.Hwnd);
        if (targets.Count == 0) { sendHint.Text = "보낼 칸이 없어요. 칸 제목의 「보내기 켬」과 앱이 붙어 있는지 확인해 주세요."; return; }

        string oldClip = null;
        try { if (Clipboard.ContainsText()) oldClip = Clipboard.GetText(); } catch { }

        busy = true;
        q.Enabled = false; sendBtn.Enabled = false; fileBtn.Enabled = false;
        sendHint.Text = "보내는 중... 끝날 때까지 마우스·키보드를 잠시 쓰지 말아 주세요";
        IntPtr me = Handle;
        RunBackground(delegate
        {
            foreach (IntPtr h in targets) { try { PasteInto(h, text, files); } catch { } }
            System.Threading.Thread.Sleep(300);
        }, delegate
        {
            try { if (oldClip != null) Clipboard.SetText(oldClip); } catch { }
            W.ForceForeground(me);
            q.Enabled = true; sendBtn.Enabled = true; fileBtn.Enabled = true;
            q.Clear(); q.Focus();
            attach.Clear();
            busy = false;
            UpdateHint();
            sendHint.Text = targets.Count + "개 칸에 보냈어요" + (files.Count > 0 ? " (파일 " + files.Count + "개 포함)" : "") + ". 안 들어간 칸은 그 칸에 직접 붙여넣기(Ctrl+V)해 주세요.";
        });
    }

    // 클립보드는 화면 쪽 스레드에서만 바꿀 수 있어서 넘겨서 처리
    void SetClipText(string t)
    {
        Invoke((System.Windows.Forms.MethodInvoker)delegate { try { Clipboard.SetText(t); } catch { } });
    }

    void SetClipFiles(List<string> files)
    {
        Invoke((System.Windows.Forms.MethodInvoker)delegate
        {
            try
            {
                var sc = new System.Collections.Specialized.StringCollection();
                foreach (string f in files) sc.Add(f);
                Clipboard.SetFileDropList(sc);
            }
            catch { }
        });
    }

    // 그 앱 창을 앞으로 → 입력란 찾아 클릭(못 찾으면 창 아래쪽 가운데) → 글 붙여넣기 → 파일 붙여넣기 → Enter
    void PasteInto(IntPtr h, string text, List<string> files)
    {
        W.ForceForeground(h);
        System.Threading.Thread.Sleep(180);
        bool focused = false;
#if !NO_UIA
        focused = Uia.FocusInput(h);
#endif
        if (!focused)
        {
            W.RECT r;
            W.GetWindowRect(h, out r);
            W.Click((r.L + r.R) / 2, r.B - 70);
        }
        System.Threading.Thread.Sleep(150);
        if (text.Length > 0)
        {
            SetClipText(text);
            W.Key(0x41, true);   // Ctrl+A (입력란에 남은 글 지우기)
            W.Key(0x56, true);   // Ctrl+V
            System.Threading.Thread.Sleep(300);
        }
        if (files.Count > 0)
        {
            SetClipFiles(files);
            W.Key(0x56, true);   // Ctrl+V (파일 붙여넣기)
            // 파일이 올라갈 시간을 기다림
            System.Threading.Thread.Sleep(Math.Min(7000, 1500 + 1000 * files.Count));
        }
        W.Key(0x0D, false);      // Enter
        System.Threading.Thread.Sleep(250);
    }

    // ---------- 새 대화 ----------
    static readonly string[] NewChatNames = new string[] {
        "New chat", "New Chat", "New conversation", "Start new chat",
        "새 채팅", "새 대화", "새로운 채팅", "새 채팅 시작", "새 대화 시작" };

    // 앱 안의 「새 채팅」 버튼을 찾아 누름(일꾼 스레드). 웹 창은 못 찾으면 창을 새로 열어서 새 대화로
    void NewChat(Target t) { NewChat(t, false); }

    static void keyCombo(byte vk)
    {
        W.keybd_event(0x11, 0, 0, UIntPtr.Zero);
        W.keybd_event(0x10, 0, 0, UIntPtr.Zero);
        W.keybd_event(vk, 0, 0, UIntPtr.Zero);
        W.keybd_event(vk, 0, 2, UIntPtr.Zero);
        W.keybd_event(0x10, 0, 2, UIntPtr.Zero);
        W.keybd_event(0x11, 0, 2, UIntPtr.Zero);
    }
    void NewChat(Target t, bool quiet)
    {
        if (t.Hwnd == IntPtr.Zero) { if (!quiet) Reopen(t); return; }
        IntPtr h = t.Hwnd;
        bool done = false;
        RunBackground(delegate
        {
#if !NO_UIA
            done = Uia.ClickByName(h, NewChatNames);
            if (!done && !t.Web)
            {
                // 설정 화면 같은 게 열려 있으면 새 채팅 버튼이 안 보임 → Esc로 닫고 다시 찾기
                W.ForceForeground(h);
                System.Threading.Thread.Sleep(150);
                W.Key(0x1B, false);
                System.Threading.Thread.Sleep(400);
                done = Uia.ClickByName(h, NewChatNames);
                string nm = t.App.Name.ToLowerInvariant();
                if (!done && (nm.Contains("chatgpt") || nm.Contains("gemini")))
                {
                    // ChatGPT·Gemini 새 채팅 단축키 Ctrl+Shift+O
                    keyCombo(0x4F);
                    done = true;
                }
            }
#endif
        }, delegate
        {
            if (done) { if (!quiet) sendHint.Text = t.App.Name + ": 새 대화로 바꿨어요"; return; }
            if (t.Web && t.Hwnd == h)
            {
                used.Remove(h.ToInt64());
                t.Hwnd = IntPtr.Zero; t.Pending = false; t.WantNewChat = false;
                t.Wait.Text = "새 대화 여는 중..."; t.Wait.Visible = true;
                W.PostMessage(h, 0x0010, IntPtr.Zero, IntPtr.Zero);   // 그 창 닫고
                before = W.All();
                OpenWeb(t);                                            // 첫 화면(새 대화)으로 다시 엶
                start = DateTime.Now;
                if (timer != null) timer.Start();
                return;
            }
            if (!quiet) sendHint.Text = t.App.Name + ": 새 대화 버튼을 찾지 못했어요. 앱에서 직접 눌러 주세요.";
        });
    }

    // 앱이 닫혔으면 칸에 「다시 열기」 안내
    void WatchApps()
    {
        if (WindowState == FormWindowState.Minimized) return;
        foreach (Target t in T)
        {
            if (t.Hwnd == IntPtr.Zero || t.Hidden || !t.Cell.Visible) continue;
            bool alive = W.IsWindow(t.Hwnd) && W.IsWindowVisible(t.Hwnd);
            if (alive) { t.Missing = 0; KeepInCell(t); continue; }
            if (++t.Missing < 2) continue;
            used.Remove(t.Hwnd.ToInt64());
            t.Hwnd = IntPtr.Zero;
            t.Pending = false;
            t.Lost = true;
            t.Wait.Text = t.App.Name + " 앱이 닫혔어요\n여기를 누르면 다시 열어요";
            t.Wait.Cursor = Cursors.Hand;
            t.Wait.Visible = true;
        }
    }

    // 앱이 스스로 움직이거나 커지면 칸 안으로 되돌림.
    // 최소 크기 때문에 칸에 안 들어가면: 웹으로 열 수 있는 앱은 웹 창으로, 설치된 프로그램은 그 줄·열을 자동으로 넓힘
    void KeepInCell(Target t)
    {
        if (busy) return;
        Rectangle r = t.Body.RectangleToScreen(t.Body.ClientRectangle);
        int top = t.Top;
        r = new Rectangle(r.X, r.Y - top, r.Width, r.Height + top);
        W.RECT wr;
        W.GetWindowRect(t.Hwnd, out wr);
        int ww = wr.R - wr.L, wh = wr.B - wr.T;
        bool moved = Math.Abs(wr.L - r.X) > 4 || Math.Abs(wr.T - r.Y) > 4 || Math.Abs(ww - r.Width) > 4 || Math.Abs(wh - r.Height) > 4;
        if (!moved)
        {
            t.Misfit = 0;
            if (t.Rgn.Length > 0 && !W.HasRgn(t.Hwnd))
            {
                t.Rgn = "";
                if (top > 0 && ++t.RgnLost >= 4)
                {
                    // 이 앱은 잘라 둔 모양을 계속 풀어 버림 → 제목 줄을 감추지 않고 칸에 딱 맞춤
                    t.CapH = 0;
                    SyncWindows();
                    return;
                }
            }
            ClipTo(t, top, r.Width, r.Height - top);
            return;
        }
        W.Place(t.Hwnd, r.X, r.Y, r.Width, r.Height);
        ClipTo(t, top, r.Width, r.Height - top);
        bool over = ww > r.Width + 10 || wh > r.Height + 10;
        if (!over) return;
        if (++t.Misfit < 2) return;          // 한 번 더 맞춰 보고도 크면
        if (!t.App.IsInstalled && !t.Web) { SwitchToWeb(t); return; }
        if (t.App.IsInstalled && !t.Grown && solo < 0) { t.Grown = true; AutoGrow(t, ww, wh); return; }
        if (t.Misfit == 4)
            t.Lbl.Text = "  " + t.App.Name + "  (칸이 이 프로그램의 최소 크기보다 작아요 · 칸 수를 줄이거나 「크게 보기」를 써 주세요)";
    }

    // 웹 창으로 바꾸기
    void SwitchToWeb(Target t)
    {
        IntPtr h = t.Hwnd;
        if (h == IntPtr.Zero) return;
        W.Detach(h, t.OldStyle, 80, 80, 1100, 800);
        W.ShowWindow(h, 6);
        used.Remove(h.ToInt64());
        released.Add(h.ToInt64());
        t.Hwnd = IntPtr.Zero; t.Pending = false; t.Misfit = 0;
        t.Wait.Text = t.App.Name + " 앱이 칸보다 커서 웹 창으로 여는 중...";
        t.Wait.Visible = true;
        t.Web = true;
        OpenWeb(t);
        start = DateTime.Now;
        if (timer != null && !timer.Enabled) timer.Start();
    }

    // 그 칸이 있는 줄(높이)과 열(너비)을 프로그램 최소 크기만큼 넓힘
    void AutoGrow(Target t, int needW, int needH)
    {
        int n = T.Count, rows = n >= 4 ? 2 : 1, cols = n / rows;
        EnsureRatio(cols, rows);
        int i = T.IndexOf(t), c = i % cols, r = i / cols, g = 4;
        int availW = area.ClientSize.Width - (cols + 1) * g, availH = area.ClientSize.Height - (rows + 1) * g;
        if (rows > 1 && availH > 0)
        {
            double f = Math.Min(0.8, (needH + 60.0) / availH), other = 0;
            for (int k = 0; k < rows; k++) if (k != r) other += rowW[k];
            if (f > rowW[r] / (other + rowW[r])) rowW[r] = f * other / (1 - f);
        }
        if (cols > 1 && availW > 0)
        {
            double f = Math.Min(0.8, (needW + 24.0) / availW), other = 0;
            for (int k = 0; k < cols; k++) if (k != c) other += colW[k];
            if (f > colW[c] / (other + colW[c])) colW[c] = f * other / (1 - f);
        }
        SaveConfig();
        DoLayout();
        sendHint.Text = t.App.Name + " 프로그램이 들어가도록 칸 크기를 자동으로 넓혔어요. 「비율 초기화」로 되돌릴 수 있어요.";
    }

    // 그 칸의 앱만 다시 열기
    void Reopen(Target t)
    {
        t.Lost = false; t.Missing = 0;
        t.Web = false;
        t.Wait.Text = "다시 여는 중...";
        t.Wait.Cursor = Cursors.Default;
        t.Wait.Visible = true;
        StartSearch(new List<Target> { t });
    }

    bool released_ = false;
    public void SafeRelease() { ReleaseAll(); }

    void ReleaseAll()
    {
        if (released_) return;
        released_ = true;
        if (watch != null) watch.Stop();
        if (timer != null) timer.Stop();
        // 기본: 붙어 있던 앱들도 같이 닫음 / Shift를 누른 채 닫으면 앱은 따로 창으로 남김
        bool keep = (Control.ModifierKeys & Keys.Shift) == Keys.Shift;
        int i = 0;
        foreach (Target t in T)
        {
            if (t.Hwnd != IntPtr.Zero)
            {
                if (keep) W.Detach(t.Hwnd, t.OldStyle, 80 + i * 40, 80 + i * 40, 1100, 800);
                else W.CloseApp(t.Hwnd, t.OldStyle);
            }
            i++;
        }
        // 앱을 바꾸면서 최소화해 둔 예전 창도 같이 닫기
        if (!keep) foreach (long h in released) W.PostMessage(new IntPtr(h), 0x0010, IntPtr.Zero, IntPtr.Zero);
    }


    // ---------- 앱 켜고 붙이기 ----------
    void OnFirstShown()
    {
        DoLayout();
        Application.DoEvents();
        timer = new Timer();
        timer.Interval = 300;
        timer.Tick += delegate { Tick(); };
        // 칸 안의 앱이 닫혔는지 1초마다 확인
        watch = new Timer();
        watch.Interval = 1000;
        watch.Tick += delegate { WatchApps(); };
        watch.Start();
        if (firstRun)
        {
            List<AppInfo> chosen = SettingsDialog();
            if (chosen != null) ApplySettings(chosen, false);
            else SaveConfig();
            firstRun = false;
        }
        StartSearch(new List<Target>(T));
        MakeShortcut();
        CheckUpdateLater();
    }

    // 그 앱 창이 이미 열려 있는지 (브라우저 창·스토어 앱 창은 빼고)
    bool AlreadyOpen(Target t)
    {
        string me = W.ProcessOf(Handle);
        foreach (KeyValuePair<long, string> kv in before)
        {
            IntPtr h = new IntPtr(kv.Key);
            if (h == Handle || used.Contains(kv.Key)) continue;
            string proc = W.ProcessOf(h);
            if (proc == me || proc == "chrome" || proc == "msedge") continue;
            if (!t.Matches(kv.Value, proc) || !t.IsAppWindow(kv.Value, proc)) continue;
            string cls = W.ClassOf(h);
            if (cls == "ApplicationFrameWindow" || cls == "Windows.UI.Core.CoreWindow") continue;
            return true;
        }
        return false;
    }

    void StartSearch(List<Target> list)
    {
        before = W.All();
        foreach (Target t in list)
        {
            t.WantNewChat = true;
            if (AlreadyOpen(t)) continue;   // 이미 켜져 있는 앱은 다시 켜지 않고 그 창을 바로 붙임
            Launch(t);
            System.Threading.Thread.Sleep(250);
        }
        start = DateTime.Now;
        timer.Start();
    }

    void Tick()
    {
        double sec = (DateTime.Now - start).TotalSeconds;

        // 켤 때·앱을 바꿀 때: 설치된 앱은 마지막 대화가 열려 있으니 새 대화로 돌림 (웹 창은 처음부터 새 대화)
        foreach (Target t in T.ToArray())
        {
            if (!t.WantNewChat || t.Hwnd == IntPtr.Zero || t.NewChatAt == DateTime.MinValue || DateTime.Now < t.NewChatAt) continue;
            t.WantNewChat = false;
            t.NewChatAt = DateTime.MinValue;
            if (!t.Web) NewChat(t, true);
        }

        // 붙인 뒤 크기 확인: 앱의 최소 크기 때문에 칸보다 크면(옆 칸 침범) 웹 앱 창으로 교체
        foreach (Target t in T)
        {
            if (!t.Pending || DateTime.Now < t.CheckAt) continue;
            t.Pending = false;
            if (t.Hwnd == IntPtr.Zero) continue;
            SyncWindows();
            int[] sz = W.Size(t.Hwnd);
            Rectangle r = t.Body.ClientRectangle;
            bool over = sz[0] > r.Width + 10 || sz[1] > r.Height + 10;
            if (over && t.App.IsInstalled && t.Cell.Visible)
            {
                // 웹 창으로 바꿀 수 없는 프로그램은 안내만 표시
                t.Lbl.Text = "  " + t.App.Name + "  (칸이 프로그램 최소 크기보다 작아요 · 칸 사이 틈을 끌어 키워 주세요)";
                continue;
            }
            if (!t.Web && t.Cell.Visible && over) SwitchToWeb(t);
        }

        bool anyLeft = false;
        Dictionary<long, string> now = W.All();
        string me = Process.GetCurrentProcess().ProcessName.ToLowerInvariant();
        var procs = new Dictionary<long, string>();
        foreach (Target t in T)
        {
            if (t.Hwnd != IntPtr.Zero) continue;
            anyLeft = true;
            foreach (KeyValuePair<long, string> kv in now)
            {
                if (used.Contains(kv.Key)) continue;
                IntPtr h = new IntPtr(kv.Key);
                if (h == Handle) continue;
                string proc = null;
                if (t.App.IsInstalled)
                {
                    if (!procs.TryGetValue(kv.Key, out proc)) { proc = W.ProcessOf(h); procs[kv.Key] = proc; }
                }
                if (!t.Matches(kv.Value, proc)) continue;
                if (proc == null) { if (!procs.TryGetValue(kv.Key, out proc)) { proc = W.ProcessOf(h); procs[kv.Key] = proc; } }
                if (proc == me) continue;
                if (before.ContainsKey(kv.Key) && !released.Contains(kv.Key))
                {
                    // 원래 켜져 있던 앱 창은 기다리지 않고 바로 붙임 (일반 브라우저 창·이름만 비슷한 다른 창은 제외)
                    if (proc == "chrome" || proc == "msedge" || !t.IsAppWindow(kv.Value, proc)) continue;
                }
                string cls = W.ClassOf(h);
                if (!t.App.IsInstalled && (cls == "ApplicationFrameWindow" || cls == "Windows.UI.Core.CoreWindow"))
                {
                    // 스토어 앱 창은 붙일 수 없어서 웹 앱 창으로 대신 엶
                    used.Add(kv.Key);
                    if (!t.Web) { t.Web = true; OpenWeb(t); W.ShowWindow(h, 6); }
                    continue;
                }
                used.Add(kv.Key);
                released.Remove(kv.Key);
                t.Wait.Visible = false;
                int old = W.Attach(h, Handle);
                if (!origStyles.ContainsKey(kv.Key)) origStyles[kv.Key] = old;   // 다시 붙여도 원래 모양 기억
                t.OldStyle = origStyles[kv.Key];
                t.Lost = false; t.Missing = 0; t.Misfit = 0;
                t.Hwnd = h;
                t.CapH = 0; t.AutoHide = false; t.Rgn = ""; t.RgnLost = 0;
                FindTitleBar(t, h, proc);
                t.Pending = true;
                t.CheckAt = DateTime.Now.AddMilliseconds(800);
                if (t.WantNewChat) t.NewChatAt = DateTime.Now.AddMilliseconds(2500);
                SyncWindows();
                break;
            }
        }

        bool anyPending = false;
        foreach (Target t in T) if (t.Pending || (t.WantNewChat && t.Hwnd != IntPtr.Zero)) anyPending = true;
        if ((!anyLeft && !anyPending) || sec > 40)
        {
            timer.Stop();
            foreach (Target t in T)
                if (t.Hwnd == IntPtr.Zero) { t.Wait.Text = t.App.Name + " 창을 찾지 못했어요\n「바꾸기」에서 다시 골라 보세요"; t.Wait.Visible = true; }
            SyncWindows();
        }
    }

    // 처음 실행 때 바탕화면 바로가기 만들기
    static void MakeShortcut()
    {
        try
        {
            string lnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "RamSpirit.lnk");
            if (File.Exists(lnk)) return;
            Type st = Type.GetTypeFromProgID("WScript.Shell");
            object sh = Activator.CreateInstance(st);
            object sc = st.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, sh, new object[] { lnk });
            Type ct = sc.GetType();
            ct.InvokeMember("TargetPath", BindingFlags.SetProperty, null, sc, new object[] { Application.ExecutablePath });
            ct.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, sc, new object[] { Path.GetDirectoryName(Application.ExecutablePath) });
            ct.InvokeMember("IconLocation", BindingFlags.SetProperty, null, sc, new object[] { Application.ExecutablePath + ",0" });
            ct.InvokeMember("Save", BindingFlags.InvokeMethod, null, sc, null);
        }
        catch { }
    }
}

#if !NO_UIA
// 윈도우 접근성 기능(UI Automation)으로 앱 안의 입력란을 찾아 포커스
static class Uia
{
    public static readonly object Gate = new object();   // 한 번에 하나씩만

    public static bool FocusInput(IntPtr h)
    {
        lock (Gate) { return FocusInputCore(h); }
    }

    static bool FocusInputCore(IntPtr h)
    {
        try
        {
            AutomationElement root = AutomationElement.FromHandle(h);
            Condition c = new AndCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit),
                new PropertyCondition(AutomationElement.IsEnabledProperty, true),
                new PropertyCondition(AutomationElement.IsKeyboardFocusableProperty, true));
            AutomationElementCollection found = null;
            for (int i = 0; i < 3; i++)
            {
                found = root.FindAll(TreeScope.Descendants, c);
                if (found != null && found.Count > 0) break;
                System.Threading.Thread.Sleep(300);
            }
            if (found == null) return false;
            AutomationElement best = null;
            double bestY = double.MinValue;
            foreach (AutomationElement e in found)
            {
                try
                {
                    if (e.Current.IsOffscreen) continue;
                    System.Windows.Rect r = e.Current.BoundingRectangle;
                    if (r.Width < 40) continue;
                    if (r.Bottom > bestY) { bestY = r.Bottom; best = e; }   // 가장 아래쪽 입력란 = 채팅 입력창
                }
                catch { }
            }
            if (best == null) return false;
            best.SetFocus();
            return true;
        }
        catch { return false; }
    }

    static readonly string[] CapNames = { "최소화", "Minimize", "최대화", "Maximize", "이전 크기로 복원", "Restore", "닫기", "Close" };

    // 최소화 버튼 아래끝까지의 높이(창 위쪽 기준). 못 찾으면 0.
    // auto=1: 그 줄에 앱 버튼이 따로 없음(브라우저 앱 창이거나 제목·창 버튼뿐) → 감춰도 됨
    public static int TitleBarHeight(IntPtr h, bool browser, out int auto)
    {
        auto = 0;
        lock (Gate)
        {
            try
            {
                W.RECT wr;
                W.GetWindowRect(h, out wr);
                AutomationElement root = AutomationElement.FromHandle(h);
                Condition c = new AndCondition(
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                    new OrCondition(new PropertyCondition(AutomationElement.NameProperty, "최소화"),
                                    new PropertyCondition(AutomationElement.NameProperty, "Minimize")));
                AutomationElementCollection mins = root.FindAll(TreeScope.Descendants, c);
                double bottom = -1;
                foreach (AutomationElement e in mins)
                {
                    System.Windows.Rect r = e.Current.BoundingRectangle;
                    if (r.IsEmpty || r.Top - wr.T > 30) continue;   // 창 맨 위에 있는 것만
                    if (bottom < 0 || r.Bottom < bottom) bottom = r.Bottom;
                }
                if (bottom < 0) return 0;
                int cap = (int)Math.Round(bottom - wr.T);
                if (cap < 16 || cap > 90) return 0;
                if (browser) { auto = 1; return cap; }
                // 같은 줄에 앱 버튼(메뉴·뒤로 등)이 있으면 자동으로 감추지 않음
                AutomationElementCollection btns = root.FindAll(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));
                int other = 0;
                foreach (AutomationElement e in btns)
                {
                    System.Windows.Rect r = e.Current.BoundingRectangle;
                    if (r.IsEmpty || r.Width < 4) continue;
                    double mid = (r.Top + r.Bottom) / 2;
                    if (mid < wr.T || mid > wr.T + cap) continue;
                    if (Array.IndexOf(CapNames, e.Current.Name) >= 0) continue;
                    other++;
                }
                auto = other == 0 ? 1 : 0;
                return cap;
            }
            catch { return 0; }
        }
    }

    // 이름이 맞는 버튼·링크를 찾아 누르기 (이름 조건으로 첫 번째만 찾아서 빠름)
    public static bool ClickByName(IntPtr h, string[] names)
    {
        lock (Gate)
        {
            try
            {
                AutomationElement root = AutomationElement.FromHandle(h);
                var conds = new Condition[names.Length];
                for (int i = 0; i < names.Length; i++)
                    conds[i] = new PropertyCondition(AutomationElement.NameProperty, names[i]);
                Condition c = new OrCondition(conds);
                for (int tryNo = 0; tryNo < 2; tryNo++)
                {
                    AutomationElement e = root.FindFirst(TreeScope.Descendants, c);
                    if (e != null && !e.Current.IsOffscreen)
                    {
                        object pat;
                        if (e.TryGetCurrentPattern(InvokePattern.Pattern, out pat)) { ((InvokePattern)pat).Invoke(); return true; }
                        System.Windows.Rect r = e.Current.BoundingRectangle;
                        W.ForceForeground(h);
                        W.Click((int)(r.X + r.Width / 2), (int)(r.Y + r.Height / 2));
                        return true;
                    }
                    System.Threading.Thread.Sleep(400);
                }
            }
            catch { }
            return false;
        }
    }
}
#endif

// ---------- 설치·자동 업데이트 ----------
static class Updater
{
    public const string Version = "1.4.0";
    const string Repo = "dpfkadpfka/ramspirit";

    public static string InstallDir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RamSpirit"); } }
    public static string InstalledExe { get { return Path.Combine(InstallDir, "RamSpirit.exe"); } }

    // 처음 실행하면 내 PC의 고정 위치로 복사하고 바로가기를 만든 뒤 그쪽으로 다시 실행 (true면 지금 프로세스는 종료)
    public static bool EnsureInstalled()
    {
        try
        {
            string me = Path.GetFullPath(Application.ExecutablePath);
            if (string.Equals(me, Path.GetFullPath(InstalledExe), StringComparison.OrdinalIgnoreCase)) return false;
            Directory.CreateDirectory(InstallDir);
            File.Copy(me, InstalledExe, true);
            MakeShortcut(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), InstalledExe);
            MakeShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs"), InstalledExe);
            Process.Start(InstalledExe);
            return true;
        }
        catch { return false; }   // 복사가 안 되면 그냥 지금 위치에서 실행
    }

    public static void MakeShortcut(string folder, string target)
    {
        try
        {
            string lnk = Path.Combine(folder, "RamSpirit.lnk");
            Type st = Type.GetTypeFromProgID("WScript.Shell");
            object sh = Activator.CreateInstance(st);
            object sc = st.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, sh, new object[] { lnk });
            Type ct = sc.GetType();
            ct.InvokeMember("TargetPath", BindingFlags.SetProperty, null, sc, new object[] { target });
            ct.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, sc, new object[] { Path.GetDirectoryName(target) });
            ct.InvokeMember("IconLocation", BindingFlags.SetProperty, null, sc, new object[] { target + ",0" });
            ct.InvokeMember("Save", BindingFlags.InvokeMethod, null, sc, null);
        }
        catch { }
    }

    public static void CleanupOld()
    {
        try
        {
            string old = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "RamSpirit.old.exe");
            if (File.Exists(old)) File.Delete(old);
        }
        catch { }
    }

    // 깃허브에 새 버전이 있으면 받아서 실행 파일을 바꿔 둠 (다음에 켤 때부터 새 버전). 바꿨으면 새 버전 번호를 돌려줌
    public static string CheckAndUpdate()
    {
        try
        {
            System.Net.ServicePointManager.SecurityProtocol = (System.Net.SecurityProtocolType)3072;   // TLS 1.2
            var wc = new System.Net.WebClient();
            wc.Headers.Add("User-Agent", "RamSpirit");
            wc.Encoding = Encoding.UTF8;
            string json = wc.DownloadString("https://api.github.com/repos/" + Repo + "/releases/latest");
            var m = System.Text.RegularExpressions.Regex.Match(json, "\"tag_name\"\\s*:\\s*\"v?([0-9]+\\.[0-9]+\\.[0-9]+)\"");
            if (!m.Success) return null;
            string latest = m.Groups[1].Value;
            if (new System.Version(latest) <= new System.Version(Version)) return null;
            var a = System.Text.RegularExpressions.Regex.Match(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]*/RamSpirit\\.exe)\"");
            if (!a.Success) return null;

            string cur = Application.ExecutablePath;
            string dir = Path.GetDirectoryName(cur);
            string tmp = Path.Combine(dir, "RamSpirit.new.exe");
            string old = Path.Combine(dir, "RamSpirit.old.exe");
            var wc2 = new System.Net.WebClient();
            wc2.Headers.Add("User-Agent", "RamSpirit");
            wc2.DownloadFile(a.Groups[1].Value, tmp);
            if (new FileInfo(tmp).Length < 20000) { File.Delete(tmp); return null; }
            if (File.Exists(old)) File.Delete(old);
            File.Move(cur, old);      // 실행 중인 파일도 이름은 바꿀 수 있음
            File.Move(tmp, cur);
            return latest;
        }
        catch { return null; }
    }
}

static class Program
{
    [STAThread]
    static void Main()
    {
        // 이미 켜져 있으면 하나만
        bool first;
        var mutex = new System.Threading.Mutex(true, "RamSpirit_SingleInstance", out first);
        if (!first) return;
        if (Updater.EnsureInstalled()) { mutex.ReleaseMutex(); return; }
        Updater.CleanupOld();
        try { W.SetProcessDPIAware(); } catch { }
        Application.EnableVisualStyles();
        var form = new MainForm();
        // 오류로 갑자기 꺼져도 붙어 있던 앱 창을 원래대로 돌려놓음
        Application.ThreadException += delegate { try { form.SafeRelease(); } catch { } };
        AppDomain.CurrentDomain.UnhandledException += delegate { try { form.SafeRelease(); } catch { } };
        Application.Run(form);
        GC.KeepAlive(mutex);
    }
}
