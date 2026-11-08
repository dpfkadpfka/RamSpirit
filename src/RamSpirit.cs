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
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern uint RegisterWindowMessage(string s);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
    public delegate void WinEventProc(IntPtr hook, uint ev, IntPtr h, int idObject, int idChild, uint thread, uint time);
    [DllImport("user32.dll")] public static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr mod, WinEventProc cb, uint pid, uint tid, uint flags);
    [DllImport("user32.dll")] public static extern bool UnhookWinEvent(IntPtr hook);

    // 붙어 있던 앱을 원래 창 모양으로 돌려놓은 뒤 닫기 (X 버튼을 누른 것과 같음)
    public static void CloseApp(IntPtr h, int oldStyle)
    {
        if (!IsWindow(h)) return;
        Unclip(h);
        SetOwner(h, IntPtr.Zero);
        SetWindowLong(h, GWL_STYLE, oldStyle);
        SetWindowPos(h, IntPtr.Zero, 100, 100, 1100, 800, NOZ | NOACT | FRAME | 0x4000);
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
        SetWindowPos(h, IntPtr.Zero, x, y, w, hh, NOZ | NOACT | 0x4000 | 0x0400);   // ASYNC: 상대 앱이 바빠도 안 기다림 · NOSENDCHANGING: 앱이 정한 최소 크기에 막히지 않고 칸 크기 그대로
    }

    public static void Detach(IntPtr h, int oldStyle, int x, int y, int w, int hh)
    {
        if (!IsWindow(h)) return;
        Unclip(h);
        SetOwner(h, IntPtr.Zero);
        SetWindowLong(h, GWL_STYLE, oldStyle);
        SetWindowPos(h, IntPtr.Zero, x, y, w, hh, NOZ | FRAME | 0x4000);
        ShowWindowAsync(h, 5);
    }
}

// 고를 수 있는 앱 목록의 한 항목
class AppInfo
{
    public string Name, Keys, Url, Color;   // Keys: 창 제목에서 찾을 단어들(| 로 구분)
    public AppInfo(string name, string keys, string url, string color) { Name = name; Keys = keys; Url = url; Color = color; }
    public override string ToString() { return Name; }
    public int SendPref = -1;   // 한 번에 보내기 대상 여부 (-1이면 AI 이름이면 켬)
    public bool PreferWeb;      // 컴퓨터에 앱이 있어도 웹 창으로 열기
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
    public static readonly Color Card = Color.White;
    public static Color Bg1, Bg2, Glass, Body, Text, Sub, ChipBg, ChipHover, ChipText, Accent, AccentSoft,
        Line, Border, Frame, AccentDark, HoverBg, ActiveHover, Mute, BorderHover, NumGray, Faint;
    public static string Name = "흰색";

    // 테마: 이름, 그리고 Bg1 Bg2 Glass Body Text Sub ChipBg ChipHover ChipText Accent AccentSoft Line Border Frame AccentDark HoverBg ActiveHover Mute BorderHover NumGray Faint
    public static readonly string[][] Themes = new string[][] {
        new string[] { "흰색", "eef0f3 e8ebef fafafb f4f5f7 202327 868a92 eff0f2 e4e6ea 40444c 2f343c e8eaee e6e8ec e2e4e8 d9dce1 1f2329 fbfbfc dcdfe4 cfd3d9 c3c8cf b0b4bb a8acb3" },
        new string[] { "보라", "e9e4ff d9eeff f8f7ff f4f3fa 24242e 8a879e efedf8 e4e1f3 4a4860 6d5bf6 e9e5ff e7e4f5 e3dff4 dcd7f2 5b49ea fbfaff ddd6ff cfcae6 c9c2f3 b3afc8 aaa7bd" },
        new string[] { "하늘", "e3eeff e4f6ff f6f9ff f2f6fb 1f2733 7e8aa0 ecf2fa e0e8f5 3b4a62 3b82f6 e2edff e3e9f3 dde5f1 d3ddec 2563eb fafcff d2e2fd c8d6ec b9cdf0 a9b6ca a3afc2" },
        new string[] { "민트", "e2f5ee e5f2fa f6fbf9 f1f6f4 1f2a27 7b8f89 e9f3ef dcebe5 35504a 12a07d dbf3eb e0ece8 d9e7e2 cfdfd9 0e8a6b fafdfc c9ebdf c3d8d1 b3dccf a7b9b3 a0b2ac" },
        new string[] { "분홍", "fde7ef efe8ff fff8fb faf3f6 2b2228 9a8691 f8ecf1 f0dfe7 5a4350 e2557c fde4ec f1e4ea ecdde4 e5d3dc c9406a fffbfd fbd3e0 e6d0da f3bfd0 c4b2bb bba9b2" }
    };

    static Th() { Set("흰색"); }

    public static Color[] Snapshot()
    {
        return new Color[] { Bg1, Bg2, Glass, Body, Text, Sub, ChipBg, ChipHover, ChipText, Accent, AccentSoft,
            Line, Border, Frame, AccentDark, HoverBg, ActiveHover, Mute, BorderHover, NumGray, Faint };
    }

    public static string[] Palette(string name)
    {
        foreach (string[] t in Themes) if (t[0] == name) return t[1].Split(' ');
        return null;
    }

    public static void Set(string name)
    {
        string[] v = Palette(name);
        if (v == null) return;
        Name = name;
        Func<int, Color> c = delegate (int i) { return ColorTranslator.FromHtml("#" + v[i]); };
        Bg1 = c(0); Bg2 = c(1); Glass = c(2); Body = c(3); Text = c(4); Sub = c(5); ChipBg = c(6); ChipHover = c(7); ChipText = c(8);
        Accent = c(9); AccentSoft = c(10); Line = c(11); Border = c(12); Frame = c(13); AccentDark = c(14); HoverBg = c(15);
        ActiveHover = c(16); Mute = c(17); BorderHover = c(18); NumGray = c(19); Faint = c(20);
    }

    // 이미 만들어진 화면의 색을 새 테마 색으로 바꿈
    public static void Recolor(Control root, Color[] from, Color[] to)
    {
        var map = new Dictionary<int, Color>();
        for (int i = 0; i < from.Length; i++) if (!map.ContainsKey(from[i].ToArgb())) map[from[i].ToArgb()] = to[i];
        RecolorCore(root, map);
    }

    static void RecolorCore(Control c, Dictionary<int, Color> map)
    {
        Color n;
        if (c.BackColor.A == 255 && map.TryGetValue(c.BackColor.ToArgb(), out n)) c.BackColor = n;
        if (map.TryGetValue(c.ForeColor.ToArgb(), out n)) c.ForeColor = n;
        foreach (Control k in c.Controls) RecolorCore(k, map);
        c.Invalidate();
    }

    // 설정 파일에서 테마 이름만 미리 읽음 (화면 만들기 전에)
    public static void LoadFrom(string file)
    {
        try
        {
            if (!File.Exists(file)) return;
            foreach (string line in File.ReadAllLines(file, Encoding.UTF8))
                if (line.StartsWith("#theme\t")) Set(line.Substring(7).Trim());
        }
        catch { }
    }

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
        b.FlatAppearance.MouseOverBackColor = primary ? Th.AccentDark : ChipHover;
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
// 버튼 여러 개를 하나로 묶는 둥근 바탕 (탭 묶음 · 위쪽 도구 묶음)
class SegBox : Panel
{
    public SegBox()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var br = new SolidBrush(Th.ChipBg))
        using (GraphicsPath p = Th.RoundPath(new Rectangle(0, 0, Width - 1, Height - 1), 9))
            g.FillPath(br, p);
    }
}

class IconBtn : Control
{
    public string Glyph = "";
    public bool Primary;     // 보라 바탕 (주요 버튼)
    public bool Ghost;       // 평소엔 바탕 없음, 마우스 올리면 연한 바탕
    public bool Active;      // 켜짐 상태(연보라 바탕 + 보라 아이콘)
    public bool Strike;      // 꺼짐 표시(아이콘에 사선)
    public bool Seg;         // 묶음(SegBox) 안의 버튼: 평소 바탕 없음, 켜지면 흰 바탕 + 테두리
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

    public void PerformClick() { OnClick(EventArgs.Empty); }
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
            return (int)((Glyph.Length > 0 ? 14 + 18 + 7 : 18) + g.MeasureString(Text, Font).Width + (Glyph.Length > 0 ? 12 : 18));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        Color bg, fg;
        if (Seg) { bg = Active ? Color.White : (down || hover) ? Th.ChipHover : Color.Empty; fg = (Active || hover) ? Th.Text : Th.Sub; }
        else if (Primary) { bg = hover ? Th.AccentDark : Th.Accent; fg = Color.White; }
        else if (Active) { bg = hover ? Th.ActiveHover : Th.AccentSoft; fg = Th.Accent; }
        else if (Ghost) { bg = down ? Th.ChipHover : hover ? Th.ChipBg : Color.Empty; fg = hover ? Th.Text : Th.ChipText; }
        else { bg = hover ? Th.ChipHover : Th.ChipBg; fg = Th.ChipText; }
        if (Strike && !Active) fg = Th.Sub;
        if (!Enabled) { fg = Color.FromArgb(150, fg); }
        if (down && !Ghost && !Seg) bg = ControlPaint.Dark(bg, 0.02f);
        if (bg != Color.Empty)
            using (var br = new SolidBrush(bg))
            using (GraphicsPath path = Th.RoundPath(new Rectangle(0, 0, Width - 1, Height - 1), Math.Min(Radius, Height / 2)))
            {
                g.FillPath(br, path);
                if (Seg && Active) using (var pen = new Pen(Th.Border)) g.DrawPath(pen, path);
            }
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
                float tx = Glyph.Length > 0 ? 14 + 18 + 7 : (Width - ts.Width) / 2f;
                g.DrawString(Text, Font, br, tx, (Height - ts.Height) / 2f + 1);
            }
        }
        if (Strike && !Active)
            using (var pen = new Pen(fg, 1.6f))
                g.DrawLine(pen, gx + 1, gy + gs.Height - 4, gx + gs.Width - 3, gy + 3);
    }
}

// 앱 아이콘: 컴퓨터에 설치된 앱이면 그 앱의 아이콘을 윈도우에서 받아 오고, 없으면 사이트의 작은 아이콘(파비콘)을 받아 옴
static class AppIcons
{
    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellItemImageFactory { [PreserveSig] int GetImage(SIZE size, int flags, out IntPtr phbm); }
    [StructLayout(LayoutKind.Sequential)] struct SIZE { public int cx, cy; }
    [StructLayout(LayoutKind.Sequential)] struct BITMAP { public int bmType, bmWidth, bmHeight, bmWidthBytes; public short bmPlanes, bmBitsPixel; public IntPtr bmBits; }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    static extern void SHCreateItemFromParsingName(string path, IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory ppv);
    [DllImport("gdi32.dll")] static extern int GetObject(IntPtr h, int size, ref BITMAP bm);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr o);

    static readonly Dictionary<string, Image> cache = new Dictionary<string, Image>();
    static readonly List<string> pending = new List<string>();
    static readonly Queue<KeyValuePair<string, string>> work = new Queue<KeyValuePair<string, string>>();
    static System.Threading.Thread worker;
    public static Dictionary<string, string> Installed;   // 설치된 앱 이름 → 실행 ID
    public static Action Changed;                         // 아이콘을 새로 받았을 때 (화면 다시 그리기)

    static string Key(AppInfo a) { return a.Name + "|" + a.Url; }

    public static Image Get(AppInfo a)
    {
        if (a == null) return null;
        string k = Key(a);
        lock (cache)
        {
            Image img;
            if (cache.TryGetValue(k, out img)) return img;
            if (pending.Contains(k)) return null;
            pending.Add(k);
            // 설치된 앱 ID 찾기 (없으면 사이트 주소로)
            string src = null;
            if (a.Url.StartsWith("app:")) src = "app:" + a.Url.Substring(4);
            else if (Installed != null)
            {
                string id;
                foreach (string n in new string[] { a.Name, "Google " + a.Name, a.Name + " AI" })
                    if (Installed.TryGetValue(n, out id)) { src = "app:" + id; break; }
            }
            if (src == null) src = a.Url;
            work.Enqueue(new KeyValuePair<string, string>(k, src));
            if (worker == null || !worker.IsAlive)
            {
                worker = new System.Threading.Thread(Run);
                worker.IsBackground = true;
                worker.SetApartmentState(System.Threading.ApartmentState.STA);
                worker.Start();
            }
        }
        return null;
    }

    static void Run()
    {
        while (true)
        {
            KeyValuePair<string, string> job;
            lock (cache) { if (work.Count == 0) return; job = work.Dequeue(); }
            Image img = null;
            try { img = job.Value.StartsWith("app:") ? ShellIcon("shell:AppsFolder\\" + job.Value.Substring(4), 64) : Favicon(job.Value); }
            catch { }
            lock (cache) { cache[job.Key] = img; pending.Remove(job.Key); }
            if (img != null && Changed != null) try { Changed(); } catch { }
        }
    }

    static Image ShellIcon(string path, int size)
    {
        IShellItemImageFactory f;
        SHCreateItemFromParsingName(path, IntPtr.Zero, typeof(IShellItemImageFactory).GUID, out f);
        IntPtr hbm;
        var sz = new SIZE { cx = size, cy = size };
        if (f.GetImage(sz, 0x4 /* 아이콘만 */, out hbm) != 0 || hbm == IntPtr.Zero) return null;
        try
        {
            var bm = new BITMAP();
            GetObject(hbm, Marshal.SizeOf(typeof(BITMAP)), ref bm);
            if (bm.bmBits == IntPtr.Zero || bm.bmBitsPixel != 32) return Image.FromHbitmap(hbm);
            // 투명한 부분을 살려서 복사 (위아래가 뒤집혀 있음)
            var tmp = new Bitmap(bm.bmWidth, bm.bmHeight, bm.bmWidthBytes, System.Drawing.Imaging.PixelFormat.Format32bppPArgb, bm.bmBits);
            var copy = new Bitmap(bm.bmWidth, bm.bmHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(copy)) g.DrawImage(tmp, 0, 0);
            tmp.Dispose();
            copy.RotateFlip(RotateFlipType.RotateNoneFlipY);
            return copy;
        }
        finally { DeleteObject(hbm); }
    }

    static Image Favicon(string url)
    {
        string host;
        try { host = new Uri(url).Host; } catch { return null; }
        string dir = Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RamSpirit"), "icons");
        string file = Path.Combine(dir, host + ".png");
        if (!File.Exists(file))
        {
            Directory.CreateDirectory(dir);
            System.Net.ServicePointManager.SecurityProtocol = (System.Net.SecurityProtocolType)3072;
            var wc = new System.Net.WebClient();
            wc.Headers.Add("User-Agent", "RamSpirit");
            wc.DownloadFile("https://www.google.com/s2/favicons?domain=" + host + "&sz=64", file);
        }
        using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
        using (var img = Image.FromStream(fs))
            return new Bitmap(img);
    }

    // 메뉴용 작은 아이콘 (20px)
    public static Image Small(AppInfo a)
    {
        Image img = Get(a);
        if (img == null) return null;
        var b = new Bitmap(20, 20);
        using (Graphics g = Graphics.FromImage(b))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(img, 0, 0, 20, 20);
        }
        return b;
    }
}

// 작업 표시줄의 RamSpirit를 오른쪽 클릭하면 나오는 메뉴(점프 목록)
static class JumpList
{
    [ComImport, Guid("6332debf-87b5-4670-90c0-5e57b408a49e"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface ICustomDestinationList
    {
        void SetAppID([MarshalAs(UnmanagedType.LPWStr)] string id);
        void BeginList(out uint maxSlots, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object removed);
        void AppendCategory([MarshalAs(UnmanagedType.LPWStr)] string category, [MarshalAs(UnmanagedType.Interface)] IObjectArray items);
        void AppendKnownCategory(int category);
        void AddUserTasks([MarshalAs(UnmanagedType.Interface)] IObjectArray tasks);
        void CommitList();
        void GetRemovedDestinations(ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object removed);
        void DeleteList([MarshalAs(UnmanagedType.LPWStr)] string id);
        void AbortList();
    }
    [ComImport, Guid("92CA9DCD-5622-4bba-A805-5E9F541BD8C9"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IObjectArray
    {
        void GetCount(out uint n);
        void GetAt(uint i, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object o);
    }
    [ComImport, Guid("5632b1a4-e38a-400a-928a-d4cd63230295"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IObjectCollection
    {
        void GetCount(out uint n);
        void GetAt(uint i, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object o);
        void AddObject([MarshalAs(UnmanagedType.Interface)] object o);
        void AddFromArray([MarshalAs(UnmanagedType.Interface)] IObjectArray a);
        void RemoveObjectAt(uint i);
        void Clear();
    }
    [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellLinkW
    {
        void GetPath(IntPtr a, int b, IntPtr c, uint d);
        void GetIDList(out IntPtr p);
        void SetIDList(IntPtr p);
        void GetDescription(IntPtr a, int b);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string s);
        void GetWorkingDirectory(IntPtr a, int b);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string s);
        void GetArguments(IntPtr a, int b);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string s);
        void GetHotkey(out short h);
        void SetHotkey(short h);
        void GetShowCmd(out int c);
        void SetShowCmd(int c);
        void GetIconLocation(IntPtr a, int b, out int i);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string s, int i);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string s, uint r);
        void Resolve(IntPtr h, uint f);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string s);
    }
    [StructLayout(LayoutKind.Sequential)] struct PROPERTYKEY { public Guid fmtid; public uint pid; }
    [StructLayout(LayoutKind.Explicit)] struct PROPVARIANT { [FieldOffset(0)] public ushort vt; [FieldOffset(8)] public IntPtr p; }
    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IPropertyStore
    {
        void GetCount(out uint n);
        void GetAt(uint i, out PROPERTYKEY k);
        void GetValue(ref PROPERTYKEY k, out PROPVARIANT v);
        void SetValue(ref PROPERTYKEY k, ref PROPVARIANT v);
        void Commit();
    }

    static object Create(string clsid) { return Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid(clsid))); }

    static object Task(string exe, string title, string args, int icon)
    {
        var link = (IShellLinkW)Create("00021401-0000-0000-C000-000000000046");
        link.SetPath(exe);
        link.SetArguments(args);
        link.SetIconLocation(exe, 0);
        link.SetDescription(title);
        var store = (IPropertyStore)link;
        var key = new PROPERTYKEY { fmtid = new Guid("F29F85E0-4FF9-1068-AB91-08002B27B3D9"), pid = 2 };   // 제목
        var v = new PROPVARIANT { vt = 31, p = Marshal.StringToCoTaskMemUni(title) };
        store.SetValue(ref key, ref v);
        store.Commit();
        Marshal.FreeCoTaskMem(v.p);
        return link;
    }

    public static void Make()
    {
        string exe = Application.ExecutablePath;
        if (File.Exists(Updater.InstalledExe)) exe = Updater.InstalledExe;
        var list = (ICustomDestinationList)Create("77f10cf0-3db5-4966-b520-b7c54fd35ed6");
        uint slots; object removed;
        Guid iid = typeof(IObjectArray).GUID;
        list.BeginList(out slots, ref iid, out removed);
        var tasks = (IObjectCollection)Create("2d3468c1-36a7-43b6-ac24-d3f02fd9607a");
        tasks.AddObject(Task(exe, "업데이트 확인", "--cmd update", 0));
        list.AddUserTasks((IObjectArray)tasks);
        list.CommitList();
    }
}

// 둥근 상자(흰 바탕 + 연한 테두리) — 입력칸·목록 바탕
class RoundBox : Panel
{
    public Color Fill = Color.White, Border = Th.Border;
    public int Radius = 12;
    public RoundBox()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(Parent != null ? Parent.BackColor : Th.Glass);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (GraphicsPath path = Th.RoundPath(new Rectangle(0, 0, Width - 1, Height - 1), Radius))
        using (var br = new SolidBrush(Fill))
        using (var pen = new Pen(Border))
        { g.FillPath(br, path); g.DrawPath(pen, path); }
    }
}

// 칸 수 고르기 타일 (작은 칸 모양 그림 + 「4칸」)
class ChoiceTile : Control
{
    public int Cols, Rows;
    public bool Selected;
    bool hover;
    public ChoiceTile()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Cursor = Cursors.Hand;
        Font = new Font("Malgun Gothic", 10f, FontStyle.Bold);
    }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(Parent != null ? Parent.BackColor : Th.Glass);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        Color fill = Selected ? Th.AccentSoft : Color.White;
        Color border = Selected ? Th.Accent : hover ? Th.BorderHover : Th.Border;
        using (GraphicsPath path = Th.RoundPath(new Rectangle(1, 1, Width - 3, Height - 3), 12))
        using (var br = new SolidBrush(fill))
        using (var pen = new Pen(border, Selected ? 2f : 1f))
        { g.FillPath(br, path); g.DrawPath(pen, path); }
        // 작은 칸 모양
        int gw = 46, gh = 28, gx = (Width - gw) / 2, gy = 14, gap = 3;
        int cw = (gw - gap * (Cols - 1)) / Cols, ch = (gh - gap * (Rows - 1)) / Rows;
        using (var br = new SolidBrush(Selected ? Th.Accent : Th.Mute))
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                    using (GraphicsPath p = Th.RoundPath(new Rectangle(gx + c * (cw + gap), gy + r * (ch + gap), cw, ch), 2))
                        g.FillPath(br, p);
        using (var br = new SolidBrush(Selected ? Th.Accent : Th.Text))
        {
            SizeF s = g.MeasureString(Text, Font);
            g.DrawString(Text, Font, br, (Width - s.Width) / 2f, gy + gh + 8);
        }
    }
}

// 앱 아이콘 (실행 파일 안에 넣어 둔 7가지 중 고름)
static class AppIcon
{
    public const int Count = 7;
    public static int Current = 1;
    public static Icon Load(int n)
    {
        try
        {
            using (Stream st = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("icon" + n + ".ico"))
                if (st != null) return new Icon(st);
        }
        catch { }
        return null;
    }
    public static Bitmap Picture(int n, int size)
    {
        try
        {
            using (Stream st = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("icon" + n + ".png"))
                if (st != null) using (var b = new Bitmap(st)) return new Bitmap(b, size, size);
        }
        catch { }
        return null;
    }
    // 바로가기 아이콘으로 쓸 .ico 파일 (설치 폴더 안 icons 폴더)
    public static string IcoFile(int n)
    {
        try
        {
            string dir = Path.Combine(Updater.InstallDir, "icons");
            Directory.CreateDirectory(dir);
            string f = Path.Combine(dir, "icon" + n + ".ico");
            using (Stream st = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("icon" + n + ".ico"))
            {
                if (st == null) return null;
                using (FileStream fs = File.Create(f)) st.CopyTo(fs);
            }
            return f;
        }
        catch { return null; }
    }
    // 바탕화면·시작 메뉴 바로가기 아이콘 바꾸기
    public static void UpdateShortcuts(int n)
    {
        string ico = n == 1 ? null : IcoFile(n);
        string exe = File.Exists(Updater.InstalledExe) ? Updater.InstalledExe : Application.ExecutablePath;
        foreach (string folder in new string[] {
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs") })
        {
            string lnk = Path.Combine(folder, "RamSpirit.lnk");
            if (!File.Exists(lnk)) continue;
            try
            {
                Type t = Type.GetTypeFromProgID("WScript.Shell");
                object sh = Activator.CreateInstance(t);
                object sc = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, sh, new object[] { lnk });
                Type ct = sc.GetType();
                ct.InvokeMember("IconLocation", BindingFlags.SetProperty, null, sc, new object[] { (ico ?? exe) + ",0" });
                ct.InvokeMember("Save", BindingFlags.InvokeMethod, null, sc, null);
            }
            catch { }
        }
    }
}

// 앱 아이콘 고르기 타일
class IconTile : Control
{
    public Image Img;
    public bool Selected;
    bool hover;
    public IconTile()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Cursor = Cursors.Hand;
    }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(Parent != null ? Parent.BackColor : Th.Glass);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        using (GraphicsPath path = Th.RoundPath(new Rectangle(1, 1, Width - 3, Height - 3), 12))
        using (var br = new SolidBrush(Color.White))
        using (var pen = new Pen(Selected ? Th.Accent : hover ? Th.BorderHover : Th.Border, Selected ? 2f : 1f))
        { g.FillPath(br, path); g.DrawPath(pen, path); }
        if (Img != null)
        {
            int d = Math.Min(Width, Height) - 22;
            g.DrawImage(Img, new Rectangle((Width - d) / 2, (Height - d) / 2, d, d));
        }
    }
}

// 테마 색 고르기 타일 (색 동그라미 + 이름)
class SwatchTile : Control
{
    public Color A, B;
    public bool Selected;
    bool hover;
    public SwatchTile()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Cursor = Cursors.Hand;
        Font = new Font("Malgun Gothic", 10f, FontStyle.Bold);
    }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(Parent != null ? Parent.BackColor : Th.Glass);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        using (GraphicsPath path = Th.RoundPath(new Rectangle(1, 1, Width - 3, Height - 3), 12))
        using (var br = new SolidBrush(Color.White))
        using (var pen = new Pen(Selected ? Th.Accent : hover ? Th.BorderHover : Th.Border, Selected ? 2f : 1f))
        { g.FillPath(br, path); g.DrawPath(pen, path); }
        int d = 24, y = (Height - d) / 2;
        using (var br = new SolidBrush(A)) g.FillEllipse(br, 14, y, d, d);
        using (var br = new SolidBrush(B)) g.FillPie(br, 14, y, d, d, -45, 180);
        using (var br = new SolidBrush(Th.Text))
        {
            SizeF sz = g.MeasureString(Text, Font);
            g.DrawString(Text, Font, br, 14 + d + 10, (Height - sz.Height) / 2f + 1);
        }
    }
}

// 켬/끔 스위치가 있는 설정 한 줄 (제목 + 설명)
class SwitchRow : Control
{
    public bool Checked;
    string title, desc;
    bool hover;
    public SwitchRow(string t, string d, bool on)
    {
        title = t; desc = d; Checked = on;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Cursor = Cursors.Hand;
    }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnClick(EventArgs e) { Checked = !Checked; Invalidate(); base.OnClick(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(Parent != null ? Parent.BackColor : Th.Glass);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        using (GraphicsPath path = Th.RoundPath(new Rectangle(0, 0, Width - 1, Height - 1), 12))
        using (var br = new SolidBrush(Color.White))
        using (var pen = new Pen(hover ? Th.BorderHover : Th.Border))
        { g.FillPath(br, path); g.DrawPath(pen, path); }
        using (var f = new Font("Malgun Gothic", 10.5f, FontStyle.Bold))
        using (var br = new SolidBrush(Th.Text)) g.DrawString(title, f, br, 16, 10);
        using (var f = new Font("Malgun Gothic", 8.8f))
        using (var br = new SolidBrush(Th.Sub))
            g.DrawString(desc, f, br, new RectangleF(16, 34, Width - 100, 22));
        // 스위치
        int sw = 44, sh = 24, x = Width - sw - 16, y = (Height - sh) / 2;
        using (GraphicsPath p = Th.RoundPath(new Rectangle(x, y, sw, sh), sh / 2))
        using (var br = new SolidBrush(Checked ? Th.Accent : Th.Mute)) g.FillPath(br, p);
        int k = sh - 6;
        using (var br = new SolidBrush(Color.White)) g.FillEllipse(br, Checked ? x + sw - k - 3 : x + 3, y + 3, k, k);
    }
}

// 윈도우 시작할 때 자동 실행 (내 계정의 시작 프로그램 목록)
static class AutoStart
{
    const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public static bool Enabled
    {
        get
        {
            try { using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(Key)) return k != null && k.GetValue("RamSpirit") != null; }
            catch { return false; }
        }
    }
    public static void Set(bool on)
    {
        try
        {
            using (var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(Key))
            {
                string exe = File.Exists(Updater.InstalledExe) ? Updater.InstalledExe : Application.ExecutablePath;
                if (on) k.SetValue("RamSpirit", "\"" + exe + "\"");
                else k.DeleteValue("RamSpirit", false);
            }
        }
        catch { }
    }
}

// 칸 하나를 나타내는 카드 (번호 · 앱 색 · 앱 이름) — 누르면 앱 고르기
class SlotCard : Control
{
    public int Number;
    public AppInfo App;
    bool hover;
    static Font nameFont = new Font("Malgun Gothic", 10.5f, FontStyle.Bold);
    static Font smallFont = new Font("Malgun Gothic", 8.5f);
    static Font numFont = new Font("Segoe UI", 10f, FontStyle.Bold);
    public SlotCard()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Cursor = Cursors.Hand;
    }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(Parent != null ? Parent.BackColor : Th.Glass);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        Color c = Th.Accent;
        try { c = ColorTranslator.FromHtml(App.Color); } catch { }
        using (GraphicsPath path = Th.RoundPath(new Rectangle(1, 1, Width - 3, Height - 3), 14))
        using (var br = new SolidBrush(hover ? Th.HoverBg : Color.White))
        using (var pen = new Pen(hover ? Th.Accent : Th.Border, hover ? 1.6f : 1f))
        { g.FillPath(br, path); g.DrawPath(pen, path); }
        // 칸 번호 (왼쪽 위, 작게)
        using (var br = new SolidBrush(Th.NumGray))
            g.DrawString(Number.ToString(), numFont, br, 12, 9);
        // 앱 첫 글자 동그라미 + 이름 (가운데)
        string nm = App != null ? App.Name : "";
        int d = Height >= 110 ? 40 : 32;
        int cx = Width / 2, top = (Height - d - 30) / 2;
        Image ic = AppIcons.Get(App);
        if (ic != null)
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(ic, cx - d / 2, top, d, d);
        }
        else
        {
            using (var br = new SolidBrush(c)) g.FillEllipse(br, cx - d / 2, top, d, d);
            using (var f = new Font("Segoe UI", d >= 40 ? 15f : 12f, FontStyle.Bold))
            using (var br = new SolidBrush(Color.White))
            {
                string ini = nm.Length > 0 ? nm.Substring(0, 1).ToUpper() : "?";
                SizeF s = g.MeasureString(ini, f);
                g.DrawString(ini, f, br, cx - s.Width / 2f + 1, top + (d - s.Height) / 2f + 1);
            }
        }
        using (var sf = new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
        using (var br = new SolidBrush(Th.Text))
            g.DrawString(nm, nameFont, br, new RectangleF(8, top + d + 6, Width - 16, 24), sf);
        // 마우스를 올리면 오른쪽 위에 「바꾸기」
        if (hover)
        {
            string tx = "바꾸기 ▾";
            SizeF s = g.MeasureString(tx, smallFont);
            var r = new RectangleF(Width - s.Width - 22, 8, s.Width + 12, s.Height + 4);
            using (GraphicsPath p = Th.RoundPath(Rectangle.Round(r), 8))
            using (var br = new SolidBrush(Th.AccentSoft)) g.FillPath(br, p);
            using (var br = new SolidBrush(Th.Accent)) g.DrawString(tx, smallFont, br, r.X + 6, r.Y + 2);
        }
    }
}

// 연보라 메뉴 색
class SoftColors : ProfessionalColorTable
{
    public override Color MenuItemSelected { get { return Th.AccentSoft; } }
    public override Color MenuItemBorder { get { return Th.AccentSoft; } }
    public override Color MenuBorder { get { return Th.Frame; } }
    public override Color ToolStripDropDownBackground { get { return Color.White; } }
    public override Color ImageMarginGradientBegin { get { return Color.White; } }
    public override Color ImageMarginGradientMiddle { get { return Color.White; } }
    public override Color ImageMarginGradientEnd { get { return Color.White; } }
    public override Color SeparatorDark { get { return Th.Line; } }
    public override Color SeparatorLight { get { return Color.White; } }
    public override Color CheckBackground { get { return Th.AccentSoft; } }
    public override Color CheckSelectedBackground { get { return Th.AccentSoft; } }
    public override Color CheckPressedBackground { get { return Th.AccentSoft; } }
}

static class Ui
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);
    [DllImport("user32.dll")] static extern bool ReleaseCapture();
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
    public static Font Body = new Font("Malgun Gothic", 9.5f);

    public static ContextMenuStrip Menu()
    {
        var m = new ContextMenuStrip();
        m.Renderer = new ToolStripProfessionalRenderer(new SoftColors()) { RoundedEdges = false };
        m.Font = new Font("Malgun Gothic", 10f);
        m.Padding = new Padding(4);
        return m;
    }

    // 끌어서 창 옮기기 (제목 줄이 없는 창)
    public static void Draggable(Control c, Form f)
    {
        c.MouseDown += delegate (object o, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(f.Handle, 0xA1, new IntPtr(2), IntPtr.Zero);
        };
    }

    // 요즘 스타일 창 틀: 위 제목·설명·닫기 / 가운데 내용 / 아래 취소·확인
    public static Form Shell(string title, string sub, int w, int h, string okText, out Panel body, out IconBtn ok, out IconBtn cancel)
    {
        var f = new Form();
        f.FormBorderStyle = FormBorderStyle.None;
        f.StartPosition = FormStartPosition.CenterParent;
        f.ShowInTaskbar = false;
        f.ClientSize = new Size(w, h);
        f.BackColor = Th.Glass;
        f.Font = Body;
        f.KeyPreview = true;
        f.Text = title;
        f.TopMost = true;   // 칸에 붙은 앱 창보다 위에
        f.HandleCreated += delegate
        {
            int round = 2, border = Th.Frame.B << 16 | Th.Frame.G << 8 | Th.Frame.R;   // 둥근 모서리, 테마 테두리(BGR)
            try { DwmSetWindowAttribute(f.Handle, 33, ref round, 4); DwmSetWindowAttribute(f.Handle, 34, ref border, 4); } catch { }
        };
        f.Paint += delegate (object o, PaintEventArgs e)
        {
            using (var pen = new Pen(Th.Frame)) e.Graphics.DrawRectangle(pen, 0, 0, f.ClientSize.Width - 1, f.ClientSize.Height - 1);
        };

        var head = new Panel();
        head.Dock = DockStyle.Top; head.Height = 84; head.BackColor = Th.Glass;
        var t = new Label();
        t.Text = title; t.Font = new Font("Malgun Gothic", 15f, FontStyle.Bold); t.ForeColor = Th.Text;
        t.AutoSize = true; t.Location = new Point(24, 20); t.BackColor = Th.Glass;
        var st = new Label();
        st.Text = sub; st.Font = Body; st.ForeColor = Th.Sub;
        st.AutoSize = true; st.Location = new Point(26, 54); st.BackColor = Th.Glass;
        var x = new IconBtn();
        x.Ghost = true; x.Glyph = "\uE711"; x.BackColor = Th.Glass; x.Radius = 8;
        x.SetBounds(w - 20 - 34, 18, 34, 34);
        x.Click += delegate { f.DialogResult = DialogResult.Cancel; f.Close(); };
        head.Controls.Add(t); head.Controls.Add(st); head.Controls.Add(x);
        Draggable(head, f); Draggable(t, f); Draggable(st, f);

        var foot = new Panel();
        foot.Dock = DockStyle.Bottom; foot.Height = 72; foot.BackColor = Th.Glass;
        foot.Paint += delegate (object o, PaintEventArgs e)
        {
            using (var pen = new Pen(Th.Line)) e.Graphics.DrawLine(pen, 24, 0, foot.Width - 24, 0);
        };
        ok = new IconBtn();
        ok.Primary = true; ok.Glyph = "\uE73E"; ok.Text = okText; ok.Radius = 12;
        ok.Font = new Font("Malgun Gothic", 10f, FontStyle.Bold);
        cancel = new IconBtn();
        cancel.Text = "취소"; cancel.Radius = 12;
        cancel.Font = new Font("Malgun Gothic", 10f, FontStyle.Bold);
        foot.Controls.Add(ok); foot.Controls.Add(cancel);
        IconBtn okB = ok, caB = cancel;
        EventHandler lay = delegate
        {
            int ow = Math.Max(120, okB.Need()), cw = Math.Max(84, caB.Need());
            okB.SetBounds(foot.Width - 24 - ow, 16, ow, 40);
            caB.SetBounds(foot.Width - 24 - ow - 8 - cw, 16, cw, 40);
        };
        foot.Resize += lay;
        f.Load += lay;
        cancel.Click += delegate { f.DialogResult = DialogResult.Cancel; f.Close(); };
        f.KeyDown += delegate (object o, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) { f.DialogResult = DialogResult.Cancel; f.Close(); }
        };

        body = new Panel();
        body.Dock = DockStyle.Fill; body.BackColor = Th.Glass;
        f.Controls.Add(body);
        f.Controls.Add(foot);
        f.Controls.Add(head);
        return f;
    }

    // 이름표 + 흰 둥근 입력칸
    public static TextBox Field(Control parent, string label, string hint, int x, int y, int w)
    {
        var l = new Label();
        l.Text = label; l.Font = new Font("Malgun Gothic", 10f, FontStyle.Bold); l.ForeColor = Th.Text;
        l.AutoSize = true; l.Location = new Point(x, y); l.BackColor = parent.BackColor;
        var box = new RoundBox();
        box.SetBounds(x, y + 26, w, 44);
        var tb = new TextBox();
        tb.BorderStyle = BorderStyle.None; tb.Font = new Font("Malgun Gothic", 10.5f); tb.BackColor = Color.White; tb.ForeColor = Th.Text;
        tb.SetBounds(14, 12, w - 28, 22);
        box.Controls.Add(tb);
        box.Click += delegate { tb.Focus(); };
        parent.Controls.Add(l); parent.Controls.Add(box);
        if (hint != null)
        {
            var h = new Label();
            h.Text = hint; h.Font = new Font("Malgun Gothic", 8.5f); h.ForeColor = Th.Sub;
            h.AutoSize = true; h.Location = new Point(x + 2, y + 76); h.BackColor = parent.BackColor;
            parent.Controls.Add(h);
        }
        return tb;
    }
}

// 앱 창의 닫기 버튼 등을 덮는 작은 가림판 (눌러도 아무 일 없음)
class GuardForm : Form
{
    Bitmap strip;
    public GuardForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.White;
        DoubleBuffered = true;
    }

    // 가림판 바로 왼쪽의 좁은 세로 띠(3px)를 화면에서 떠 와서, 가림판 전체로 늘려 그림 → 제목 줄과 자연스럽게 이어짐
    public void Stamp(Rectangle r)
    {
        try
        {
            var b = new Bitmap(3, Math.Max(1, r.Height));
            using (Graphics g = Graphics.FromImage(b))
                g.CopyFromScreen(r.X - 7, r.Y, 0, 0, b.Size);
            if (strip != null) strip.Dispose();
            strip = b;
            Invalidate();
        }
        catch { }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (strip == null) { base.OnPaint(e); return; }
        e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
        e.Graphics.DrawImage(strip, new Rectangle(0, 0, Width, Height), new Rectangle(1, 0, 1, strip.Height), GraphicsUnit.Pixel);
    }
    protected override bool ShowWithoutActivation { get { return true; } }
    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= 0x08000000 | 0x00000080;   // NOACTIVATE · TOOLWINDOW
            return cp;
        }
    }
}

// 칸 사이 선을 끄는 동안 보이는 보라색 안내 선 (앱 창보다 위에)
class GuideLine : Form
{
    public GuideLine()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Th.Accent;
        Opacity = 0.75;
    }
    protected override bool ShowWithoutActivation { get { return true; } }
    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= 0x08000000 | 0x00000080 | 0x00000020;   // NOACTIVATE · TOOLWINDOW · 마우스 통과
            return cp;
        }
    }
}

// 앱을 켜서 칸에 넣는 동안 잠깐 화면을 덮는 가림막 (앱 창이 커졌다 작아지는 모습이 안 보이게)
class LoadCover : Form
{
    public string Status = "";

    public LoadCover()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Th.Bg1;
        DoubleBuffered = true;
    }

    protected override bool ShowWithoutActivation { get { return true; } }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= 0x08000000 | 0x00000080;   // 눌러도 앞으로 안 오고(NOACTIVATE), 작업 전환 목록에 안 보임(TOOLWINDOW)
            return cp;
        }
    }

    int angle;
    Timer spin;

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (spin == null)
        {
            spin = new Timer();
            spin.Interval = 30;
            spin.Tick += delegate
            {
                angle = (angle + 9) % 360;
                int cx = ClientSize.Width / 2, cy = ClientSize.Height / 2;
                Invalidate(new Rectangle(cx - 40, cy - 110, 80, 80));
            };
        }
        if (Visible) spin.Start(); else spin.Stop();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Rectangle r = ClientRectangle;
        if (r.Width <= 0 || r.Height <= 0) return;
        using (var br = new LinearGradientBrush(r, Th.Bg1, Th.Bg2, LinearGradientMode.ForwardDiagonal)) g.FillRectangle(br, r);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        int cx = r.Width / 2, cy = r.Height / 2;
        // 빙글빙글 도는 로딩 고리
        var ring = new Rectangle(cx - 26, cy - 96, 52, 52);
        using (var pen = new Pen(Th.Line, 5f)) g.DrawEllipse(pen, ring);
        using (var pen = new Pen(Th.Accent, 5f))
        {
            pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round;
            g.DrawArc(pen, ring, angle, 100);
        }
        using (var f = new Font("Malgun Gothic", 15f, FontStyle.Bold))
        using (var br = new SolidBrush(Th.Text))
        {
            string t = "앱을 칸에 넣고 있어요";
            SizeF s = g.MeasureString(t, f);
            g.DrawString(t, f, br, cx - s.Width / 2, cy - 18);
        }
        using (var f = new Font("Malgun Gothic", 10.5f))
        using (var br = new SolidBrush(Th.Sub))
        {
            SizeF s = g.MeasureString(Status, f);
            g.DrawString(Status, f, br, cx - s.Width / 2, cy + 22);
        }
    }
}

// 탭 하나 = 칸 구성 하나 (예: 「AI」 탭 4칸, 「이미지」 탭에 포토샵 등)
class Workspace
{
    public string Name;
    public List<Target> T = new List<Target>();
    public List<AppInfo> Apps = new List<AppInfo>();   // 아직 한 번도 안 연 탭의 앱 목록
    public double[] ColW = new double[0], RowW = new double[0];
    public int Solo = -1;
    public bool Started;
    public Workspace(string n) { Name = n; }
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
    public bool Lent;      // 이 앱 창을 다른 탭이 빌려 감 (그 탭에서 쓰는 중)
    public bool TooSmall;  // 창이 작아서 이 앱이 칸에 안 들어감 → 잠시 숨김
    public bool Lost;      // 칸 안의 앱이 닫힘
    public int Missing;    // 연속으로 안 보인 횟수
    public int Misfit;     // 칸에서 벗어난 횟수
    public bool Grown;     // 최소 크기 때문에 칸을 자동으로 키웠는지
    public bool NoBrowser; // 같은 앱의 웹 창 칸이 따로 있어서, 이 칸은 브라우저 창을 안 붙임 (예: Claude 앱 칸 + Claude 웹 칸)
    public DateTime NewChatAt;
    public int CapH;       // 앱 자체 제목 줄 높이(최소화·닫기 버튼 아래끝까지), 0이면 없음
    public bool AutoHide;  // 제목 줄에 다른 버튼이 없어서 자동으로 감추는지
    public int HideTop = -1;   // -1 자동, 0 보이기, 1 감추기 (칸 메뉴에서 고름)
    public string Rgn = "";    // 마지막으로 자른 모양
    public int RgnLost;        // 앱이 잘라 둔 모양을 풀어 버린 횟수
    public int Top { get { return 0; } }
    public int MinW, MinH;   // 이 앱이 더 줄어들지 않는 최소 크기 (실제로 본 값)
    public Rectangle CapBtns = Rectangle.Empty;   // 앱의 최소화·최대화·닫기 버튼 자리 (창 기준)
    public Form Guard;                           // 그 버튼들을 덮는 가림판
    public Color GuardColor = Color.White;

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

    // AI 앱 칸은 그 앱 자신의 창이나 브라우저(웹 창)만 붙임 — 제목에 이름만 들어간 다른 프로그램(Codex·VS Code 등)은 제외
    public bool ProcOk(string proc)
    {
        if (App.IsInstalled || string.IsNullOrEmpty(proc)) return true;
        string p = proc.Replace(" ", "").ToLowerInvariant();
        if (p == "chrome" || p == "msedge") return !NoBrowser;
        if (p == "applicationframehost") return true;
        if (App.PreferWeb) return false;   // 웹으로 열기로 했으면 데스크톱 앱 창은 안 붙임
        foreach (string k in App.Keys.Split('|'))
        {
            string kk = k.Replace(" ", "").ToLowerInvariant();
            if (kk.Length > 0 && (p.Contains(kk) || kk.Contains(p))) return true;
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
    List<Target> T = new List<Target>();
    readonly List<Workspace> spaces = new List<Workspace>();
    Workspace cur;

    // 모든 탭의 칸
    List<Target> AllTargets()
    {
        var all = new List<Target>();
        foreach (Workspace w in spaces) all.AddRange(w.T);
        if (!spaces.Contains(cur)) all.AddRange(T);
        return all;
    }
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
        MinimumSize = new Size(720, 480);
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
        bar.Dock = DockStyle.Top; bar.Height = 40; bar.BackColor = Th.Glass;
        logo = new PictureBox();
        logo.SizeMode = PictureBoxSizeMode.Zoom; logo.BackColor = Th.Glass;
        try { logo.Image = Icon.ExtractAssociatedIcon(Application.ExecutablePath).ToBitmap(); } catch { }
        logo.SetBounds(14, 11, 18, 18);
        bar.Controls.Add(logo);
        var brand = new Label();
        brand.Text = "RamSpirit"; brand.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold); brand.ForeColor = Th.Text;
        brand.AutoSize = true; brand.BackColor = Th.Glass; brand.Location = new Point(36, 10);
        brand.Cursor = Cursors.Hand; logo.Cursor = Cursors.Hand;
        EventHandler home = delegate { if (solo >= 0 && solo < T.Count) ToggleSolo(T[solo]); };
        brand.Click += home; logo.Click += home;
        Tip(brand, "처음 화면(칸 나누기)으로 돌아가기");
        bar.Controls.Add(brand);
        toolSeg = new SegBox();
        toolSeg.BackColor = Th.Glass;
        bar.Controls.Add(toolSeg);
        var allNew = new IconBtn();
        allNew.Seg = true; allNew.Glyph = "\uE70F"; allNew.Text = "모두 새 대화";
        allNew.Click += delegate { foreach (Target x in T.ToArray()) if (x.Cell.Visible) NewChat(x); };
        Tip(allNew, "보이는 칸을 모두 새 대화로 바꿔요");
        toolSeg.Controls.Add(allNew);
        var resetBtn = new IconBtn();
        resetBtn.Seg = true; resetBtn.Glyph = "\uE8A9"; resetBtn.Text = "칸 크기 같게";
        resetBtn.Click += delegate { foreach (Target x in T) { x.MinW = 0; x.MinH = 0; x.Misfit = 0; } ResetRatio(); };
        Tip(resetBtn, "끌어서 바꾼 칸 크기를 모두 똑같이 되돌려요");
        toolSeg.Controls.Add(resetBtn);
        compToggle = new IconBtn();
        compToggle.Seg = true; compToggle.Glyph = "\uE724"; compToggle.Text = "한 번에 보내기";
        compToggle.Click += delegate { SetComposer(!compOpen); SaveConfig(); };
        toolSeg.Controls.Add(compToggle);
        foreach (IconBtn b in new IconBtn[] { allNew, resetBtn, compToggle }) { b.Font = new Font("Malgun Gothic", 9f); b.Radius = 7; }
        sendHint = new Label();
        sendHint.Font = btnFont; sendHint.ForeColor = Th.Sub; sendHint.BackColor = Th.Glass;
        sendHint.TextAlign = ContentAlignment.MiddleLeft; sendHint.AutoEllipsis = true;
        sendHint.TextChanged += delegate { HintChanged(); };
        bar.Controls.Add(sendHint);
        var setBtn = new IconBtn();
        setBtn.Ghost = true; setBtn.Glyph = "\uE713"; setBtn.Radius = 8;
        setBtn.Click += delegate { OpenSettings(); };
        Tip(setBtn, "설정 · 칸 수와 칸마다 열 앱 고르기");
        bar.Controls.Add(setBtn);
        updBtn = new IconBtn();
        updBtn.Primary = true; updBtn.Glyph = "\uE896"; updBtn.Visible = false;
        updBtn.Click += delegate
        {
            string exe = File.Exists(Updater.InstalledExe) ? Updater.InstalledExe : Application.ExecutablePath;
            Close();                       // 붙어 있던 앱들을 먼저 정리하고
            Program.ReleaseLock();
            try { Process.Start(exe, "--wait " + Process.GetCurrentProcess().Id); } catch { }
            Environment.Exit(0);
        };
        bar.Controls.Add(updBtn);
        verLbl = new Label();
        verLbl.Text = "v" + Updater.Version; verLbl.Font = btnFont; verLbl.ForeColor = Th.Sub; verLbl.BackColor = Th.Glass;
        verLbl.TextAlign = ContentAlignment.MiddleRight;
        verLbl.Cursor = Cursors.Hand;
        verLbl.Click += delegate { CheckUpdate(true, 0); };
        Tip(verLbl, "누르면 새 버전이 있는지 확인해요");
        bar.Controls.Add(verLbl);
        bar.Paint += delegate (object o, PaintEventArgs e)
        {
            using (var pen = new Pen(Th.Line)) e.Graphics.DrawLine(pen, 0, bar.Height - 1, bar.Width, bar.Height - 1);
        };
        bar.Resize += delegate { LayoutBar(); };
        pinBtn = new IconBtn();
        pinBtn.Ghost = true; pinBtn.Glyph = "\uE718"; pinBtn.Radius = 8;
        pinBtn.Click += delegate { SetOnTop(!onTop); SaveConfig(); };
        bar.Controls.Add(pinBtn);
        tabSeg = new SegBox();
        tabSeg.BackColor = Th.Glass;
        bar.Controls.Add(tabSeg);
        tabAdd = new IconBtn();
        tabAdd.Ghost = true; tabAdd.Glyph = "\uE710"; tabAdd.Radius = 8;
        tabAdd.Click += delegate { AddSpace(); };
        Tip(tabAdd, "새 탭 만들기 (예: 이미지 탭에 포토샵)");
        bar.Controls.Add(tabAdd);
        barBtns = new IconBtn[] { allNew, resetBtn, compToggle, setBtn };
        Controls.Add(bar);
        BuildComposer();

        LoadSpaces();
        ApplyIcon(AppIcon.Current, false);
        T = cur.T; colW = cur.ColW; rowW = cur.RowW;
        foreach (AppInfo a in cur.Apps) AddTarget(a);
        cur.Started = true;

        area.Resize += delegate { DoLayout(); };
        // 칸 사이 틈(가운데 십자 포함)을 끌어서 칸 크기 조절
        SplitForward(area);
        Move += delegate { SyncWindows(); };
        Resize += delegate { SyncWindows(); };
        FormClosing += delegate
        {
            dragColAt = -1; dragRowAt = -1; if (dragTimer != null) dragTimer.Stop(); HideGuides();
            if (tray != null) { tray.Visible = false; tray.Dispose(); tray = null; }
            if (watch != null) watch.Stop();
            if (timer != null) timer.Stop();
            if (fixTimer != null) fixTimer.Stop();
            try { if (hookMove != IntPtr.Zero) W.UnhookWinEvent(hookMove); if (hookLoc != IntPtr.Zero) W.UnhookWinEvent(hookLoc); if (hookFg != IntPtr.Zero) W.UnhookWinEvent(hookFg); } catch { }
            bool shift = (Control.ModifierKeys & Keys.Shift) == Keys.Shift;
            keepOnClose = closeApps ? shift : !shift;
            // 응답 없는 앱이 있어도 RamSpirit가 꺼지지 못하고 남지 않도록 따로 처리하고 최대 3초만 기다림
            var th = new System.Threading.Thread(ReleaseAll);
            th.IsBackground = true;
            th.Start();
            th.Join(3000);
        };
        Shown += delegate { OnFirstShown(); };
    }

    // ---------- 설정 저장/불러오기 (apps.txt: 이름 TAB 찾을단어 TAB 주소 TAB 색) ----------
    void LoadSpaces()
    {
        string active = null;
        Workspace w = null;
        try
        {
            if (File.Exists(configFile))
            {
                foreach (string line in File.ReadAllLines(configFile, Encoding.UTF8))
                {
                    string[] p = line.Split('\t');
                    if (p.Length == 2 && p[0] == "#tab") { w = new Workspace(p[1].Trim()); spaces.Add(w); continue; }
                    if (p.Length == 2 && p[0] == "#active") { active = p[1].Trim(); continue; }
                    if (p.Length == 2 && p[0] == "#composer") { compOpen = p[1] != "0"; continue; }
                    if (p.Length == 2 && p[0] == "#ontop") { onTop = p[1] == "1"; continue; }
                    if (p.Length == 2 && p[0] == "#ctrlenter") { ctrlEnterSend = p[1] == "1"; continue; }
                    if (p.Length == 2 && p[0] == "#startnew") { startNewChat = p[1] != "0"; continue; }
                    if (p.Length == 2 && p[0] == "#closeapps") { closeApps = p[1] != "0"; continue; }
                    if (p.Length == 2 && p[0] == "#icon") { int n; if (int.TryParse(p[1], out n) && n >= 1 && n <= AppIcon.Count) AppIcon.Current = n; continue; }
                    if (line.StartsWith("#") && !line.StartsWith("#ratio")) continue;
                    if (w == null) { w = new Workspace("AI"); spaces.Add(w); }   // 예전 설정 파일(탭 없음)
                    if (p.Length == 3 && p[0] == "#ratio")
                    {
                        double[] cw = ParseNums(p[1]), rw = ParseNums(p[2]);
                        if (cw != null && rw != null) { w.ColW = cw; w.RowW = rw; }
                        continue;
                    }
                    if (p.Length >= 4 && p.Length <= 6 && p[0].Length > 0 && p[2].Length > 0)
                    {
                        var a = new AppInfo(p[0], p[1], p[2], p[3]);
                        if (p.Length >= 5) a.SendPref = p[4] == "1" ? 1 : 0;
                        if (p.Length == 6) a.PreferWeb = p[5] == "web";
                        w.Apps.Add(a);
                    }
                }
            }
        }
        catch { }
        if (spaces.Count == 0) spaces.Add(new Workspace("AI"));
        foreach (Workspace x in spaces)
            if (x.Apps.Count < 2 || x.Apps.Count > 8 || x.Apps.Count % 2 != 0)
            {
                x.Apps.Clear();
                for (int i = 0; i < 4; i++) x.Apps.Add(AppInfo.Presets[i]);
                x.ColW = new double[0]; x.RowW = new double[0];
            }
        cur = spaces[0];
        foreach (Workspace x in spaces) if (x.Name == active) cur = x;
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
            if (cur != null) { cur.ColW = colW; cur.RowW = rowW; cur.Solo = solo; }
            foreach (Workspace w in spaces)
            {
                lines.Add("#tab\t" + w.Name);
                if (w.Started)
                    foreach (Target t in w.T) lines.Add(t.App.Name + "\t" + t.App.Keys + "\t" + t.App.Url + "\t" + t.App.Color + "\t" + (t.SendOn ? "1" : "0") + (t.App.PreferWeb ? "\tweb" : ""));
                else
                    foreach (AppInfo a in w.Apps) lines.Add(a.Name + "\t" + a.Keys + "\t" + a.Url + "\t" + a.Color + "\t" + (a.SendPref >= 0 ? a.SendPref.ToString() : (a.IsAI ? "1" : "0")));
                lines.Add("#ratio\t" + JoinNums(w.ColW) + "\t" + JoinNums(w.RowW));
            }
            if (cur != null) lines.Add("#active\t" + cur.Name);
            lines.Add("#composer\t" + (compOpen ? "1" : "0"));
            lines.Add("#theme\t" + Th.Name);
            lines.Add("#icon\t" + AppIcon.Current);
            lines.Add("#ontop\t" + (onTop ? "1" : "0"));
            lines.Add("#ctrlenter\t" + (ctrlEnterSend ? "1" : "0"));
            lines.Add("#startnew\t" + (startNewChat ? "1" : "0"));
            lines.Add("#closeapps\t" + (closeApps ? "1" : "0"));
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
        if (t.App.PreferWeb) { OpenWeb(t); t.Web = true; return; }
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
        cell.Padding = new Padding(5, 0, 5, 5);
        Th.Round(cell, 12);

        var head = new Panel();
        head.Dock = DockStyle.Top; head.Height = 36; head.Padding = new Padding(0, 4, 0, 4);
        // 앱 색 동그라미
        var dot = new Panel();
        dot.Dock = DockStyle.Left; dot.BackColor = Th.Card;
        dot.Width = 26;
        dot.Paint += delegate (object o, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            Image ic = AppIcons.Get(t.App);
            if (ic != null) { e.Graphics.DrawImage(ic, 5, (dot.Height - 18) / 2, 18, 18); return; }
            Color c = ColorTranslator.FromHtml((string)(dot.Tag ?? "#6d5bf6"));
            using (var br = new SolidBrush(c)) e.Graphics.FillEllipse(br, 9, (dot.Height - 10) / 2, 10, 10);
        };

        var lbl = new Label();
        lbl.Dock = DockStyle.Fill; lbl.Font = font;
        lbl.ForeColor = Th.Text; lbl.TextAlign = ContentAlignment.MiddleLeft; lbl.BackColor = Th.Card; lbl.AutoEllipsis = true; lbl.UseMnemonic = false;
        lbl.Cursor = Cursors.SizeAll;
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
        tg.Width = 30;
        tg.Click += delegate { t.SendOn = !t.SendOn; PaintToggle(t); SaveConfig(); UpdateHint(); };
        var gap = new Panel();
        gap.Dock = DockStyle.Right; gap.Width = 9; gap.BackColor = Th.Card;
        gap.Paint += delegate (object o, PaintEventArgs e)
        {
            using (var pen = new Pen(Th.Line)) e.Graphics.DrawLine(pen, 4, 5, 4, gap.Height - 5);
        };
        var fresh = HeadBtn("\uE70F");
        Tip(fresh, "새 대화");
        fresh.Click += delegate { NewChat(t); };
        var change = HeadBtn("\uE8AB");
        Tip(change, "앱 바꾸기");
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
        Th.Round(body, 8);

        var wait = new Label();
        wait.Dock = DockStyle.Fill; wait.Text = "불러오는 중..."; wait.ForeColor = Th.Sub;
        wait.TextAlign = ContentAlignment.MiddleCenter; wait.Font = font;
        wait.Click += delegate { if (t.TooSmall) { ToggleSolo(t); return; } if (t.Lost || t.Hwnd == IntPtr.Zero) Reopen(t); };
        body.Controls.Add(wait);

        cell.Controls.Add(body);
        cell.Controls.Add(head);
        area.Controls.Add(cell);
        SplitForward(cell); SplitForward(head); SplitForward(dot);

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
        b.Dock = DockStyle.Right; b.Width = 28;
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
        t.Pending = false; t.Hidden = false; t.Web = false; t.TooSmall = false; t.Lent = false;
    }

    // ---------- 설정 창 ----------
    void OpenSettings()
    {
        List<AppInfo> chosen = SettingsDialog();
        if (chosen != null) ApplySettings(chosen, true);
    }

    // 설정 창: 왼쪽 메뉴(칸 구성 · 화면 · 보내기 · 시작과 닫기 · 정보) + 오른쪽 내용
    List<AppInfo> SettingsDialog()
    {
        Panel body; IconBtn ok, cancel;
        var f = Ui.Shell(firstRun ? "처음 설정" : "설정",
            firstRun ? "한 번만 정해 두면 다음부터는 아이콘만 눌러도 이대로 열려요" : "「" + (cur != null ? cur.Name : "") + "」 탭과 RamSpirit 전체 설정",
            860, 640, firstRun ? "이대로 시작" : "저장하고 적용", out body, out ok, out cancel);

        // 왼쪽 메뉴
        var nav = new Panel();
        nav.BackColor = Th.Glass;
        nav.SetBounds(16, 0, 180, body.Height);
        nav.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom;
        body.Controls.Add(nav);
        var host = new Panel();
        host.BackColor = Th.Glass;
        host.SetBounds(212, 0, 860 - 212 - 24, 640 - 84 - 72);
        body.Controls.Add(host);
        body.Paint += delegate (object o, PaintEventArgs e)
        {
            using (var pen = new Pen(Th.Line)) e.Graphics.DrawLine(pen, 202, 4, 202, body.Height - 8);
        };
        int PW = host.Width;
        var pages = new List<Panel>();
        var navBtns = new List<IconBtn>();
        Func<string, string, Panel> addPage = delegate (string glyph, string title)
        {
            var pg = new Panel();
            pg.BackColor = Th.Glass;
            pg.SetBounds(0, 0, host.Width, host.Height);
            pg.Visible = false;
            host.Controls.Add(pg);
            var h = new Label();
            h.Text = title; h.Font = new Font("Malgun Gothic", 13f, FontStyle.Bold); h.ForeColor = Th.Text;
            h.AutoSize = true; h.Location = new Point(0, 2); h.BackColor = Th.Glass;
            pg.Controls.Add(h);
            var nb = new IconBtn();
            nb.Glyph = glyph; nb.Text = title; nb.Ghost = true; nb.Radius = 10;
            nb.Font = new Font("Malgun Gothic", 10f, FontStyle.Bold);
            nb.SetBounds(0, navBtns.Count * 44, 180, 38);
            int idx = pages.Count;
            nb.Click += delegate
            {
                for (int k = 0; k < pages.Count; k++)
                {
                    pages[k].Visible = k == idx;
                    navBtns[k].Active = k == idx; navBtns[k].Ghost = k != idx; navBtns[k].Invalidate();
                }
            };
            nav.Controls.Add(nb);
            navBtns.Add(nb);
            pages.Add(pg);
            return pg;
        };
        Func<Control, string, int, Label> section = delegate (Control parent, string text, int y)
        {
            var l = new Label();
            l.Text = text; l.Font = new Font("Malgun Gothic", 10f, FontStyle.Bold); l.ForeColor = Th.Sub;
            l.AutoSize = true; l.Location = new Point(0, y); l.BackColor = Th.Glass;
            parent.Controls.Add(l);
            return l;
        };

        // ===== 1. 칸 구성 =====
        Panel p1 = addPage("\uE8A9", "칸 구성");
        var slots = new AppInfo[8];
        var usedNames = new List<string>();
        for (int i = 0; i < 8; i++)
        {
            AppInfo cand = null;
            if (i < T.Count) cand = T[i].App;
            else
            {
                foreach (AppInfo pr in AppInfo.Presets)
                {
                    bool taken = usedNames.Contains(pr.Name);
                    for (int j = 0; j < T.Count && !taken; j++) if (T[j].App.Name == pr.Name) taken = true;
                    if (!taken) { cand = pr; break; }
                }
                if (cand == null) cand = AppInfo.Presets[i % AppInfo.Presets.Length];
            }
            usedNames.Add(cand.Name);
            slots[i] = cand;
        }
        int count = T.Count == 0 ? 4 : Math.Max(2, Math.Min(8, T.Count / 2 * 2));
        section(p1, "칸 수", 44);
        var tiles = new List<ChoiceTile>();
        int tw = (PW - 3 * 10) / 4;
        int[] ns = { 2, 4, 6, 8 };
        for (int i = 0; i < 4; i++)
        {
            int n = ns[i];
            var tile = new ChoiceTile();
            tile.Text = n + "칸"; tile.Rows = n >= 4 ? 2 : 1; tile.Cols = n / tile.Rows;
            tile.SetBounds(i * (tw + 10), 70, tw, 72);
            tiles.Add(tile);
            p1.Controls.Add(tile);
        }
        section(p1, "칸마다 열 앱 · 화면과 같은 자리예요 (카드를 누르면 바꿔요)", 162);
        var grid = new Panel();
        grid.BackColor = Th.Glass;
        grid.SetBounds(0, 188, PW, host.Height - 188 - 4);
        p1.Controls.Add(grid);
        Action rebuild = null;
        rebuild = delegate
        {
            foreach (ChoiceTile tl in tiles) { tl.Selected = tl.Text == count + "칸"; tl.Invalidate(); }
            grid.SuspendLayout();
            while (grid.Controls.Count > 0) { Control c = grid.Controls[0]; grid.Controls.RemoveAt(0); c.Dispose(); }
            int rows = count >= 4 ? 2 : 1, cols = count / rows, gap = 10;
            int cw = (grid.Width - gap * (cols - 1)) / cols;
            int ch = Math.Min(130, (grid.Height - gap * (rows - 1)) / rows);
            for (int i = 0; i < count; i++)
            {
                int idx = i;
                var card = new SlotCard();
                card.Number = i + 1; card.App = slots[i];
                card.SetBounds((i % cols) * (cw + gap), (i / cols) * (ch + gap), cw, ch);
                card.Click += delegate
                {
                    var m = Ui.Menu();
                    foreach (AppInfo pr in AppInfo.Presets)
                    {
                        AppInfo pick = pr;
                        var it = new ToolStripMenuItem(pr.Name, AppIcons.Small(pr));
                        it.Checked = pr.SameAs(slots[idx]);
                        it.Click += delegate { slots[idx] = pick; card.App = pick; card.Invalidate(); };
                        m.Items.Add(it);
                    }
                    m.Items.Add(new ToolStripSeparator());
                    var inst = new ToolStripMenuItem("설치된 프로그램에서 고르기...");
                    inst.Click += delegate { AppInfo a = PickInstalled(f); if (a != null) { slots[idx] = a; card.App = a; card.Invalidate(); } };
                    var cus = new ToolStripMenuItem("직접 입력 (사이트 주소)...");
                    cus.Click += delegate { AppInfo a = AskCustom(f); if (a != null) { slots[idx] = a; card.App = a; card.Invalidate(); } };
                    m.Items.Add(inst); m.Items.Add(cus);
                    m.Show(card, new Point(0, card.Height + 2));
                };
                grid.Controls.Add(card);
            }
            grid.ResumeLayout();
        };
        foreach (ChoiceTile tl in tiles)
        {
            ChoiceTile me = tl;
            tl.Click += delegate { count = int.Parse(me.Text.Replace("칸", "")); rebuild(); };
        }
        rebuild();

        // ===== 2. 화면 =====
        Panel p2 = addPage("\uE790", "화면");
        section(p2, "테마 색", 44);
        string pickTheme = Th.Name;
        var swatches = new List<SwatchTile>();
        int sw = (PW - 4 * 10) / 5;
        for (int i = 0; i < Th.Themes.Length; i++)
        {
            var st = new SwatchTile();
            st.Text = Th.Themes[i][0];
            string[] pv = Th.Themes[i][1].Split(' ');
            st.A = ColorTranslator.FromHtml("#" + pv[0]); st.B = ColorTranslator.FromHtml("#" + pv[9]);
            st.Selected = st.Text == pickTheme;
            st.SetBounds(i * (sw + 10), 70, sw, 50);
            SwatchTile meS = st;
            st.Click += delegate
            {
                pickTheme = meS.Text;
                foreach (SwatchTile x in swatches) { x.Selected = x == meS; x.Invalidate(); }
            };
            swatches.Add(st);
            p2.Controls.Add(st);
        }
        section(p2, "창", 142);
        var swTop = new SwitchRow("항상 위에 두기", "다른 프로그램을 눌러도 RamSpirit가 항상 위에 떠 있어요", onTop);
        swTop.SetBounds(0, 168, PW, 62); p2.Controls.Add(swTop);
        var swComp = new SwitchRow("아래 입력창 열어 두기", "끄면 입력창이 숨겨져서 앱 칸이 더 커져요 (위쪽 「한 번에 보내기」로 언제든 열기)", compOpen);
        swComp.SetBounds(0, 236, PW, 62); p2.Controls.Add(swComp);
        section(p2, "앱 아이콘", 316);
        int pickIcon = AppIcon.Current;
        var iconTiles = new List<IconTile>();
        int iw = (PW - (AppIcon.Count - 1) * 8) / AppIcon.Count;
        for (int i = 1; i <= AppIcon.Count; i++)
        {
            var it = new IconTile();
            it.Img = AppIcon.Picture(i, 48);
            it.Selected = i == pickIcon;
            it.SetBounds((i - 1) * (iw + 8), 342, iw, Math.Min(iw, 70));
            int num = i;
            IconTile meI = it;
            it.Click += delegate
            {
                pickIcon = num;
                foreach (IconTile x in iconTiles) { x.Selected = x == meI; x.Invalidate(); }
            };
            Tip(it, num == 1 ? "기본 아이콘" : "아이콘 " + num);
            iconTiles.Add(it);
            p2.Controls.Add(it);
        }

        // ===== 3. 보내기 =====
        Panel p3 = addPage("\uE724", "보내기");
        section(p3, "보내는 키", 44);
        var swCtrl = new SwitchRow("Ctrl+Enter로 보내기", "켜면 Enter는 줄바꿈이 되고 Ctrl+Enter로 보내요 (긴 글을 쓸 때 편해요)", ctrlEnterSend);
        swCtrl.SetBounds(0, 70, PW, 62); p3.Controls.Add(swCtrl);
        section(p3, "새 대화", 152);
        var swNew = new SwitchRow("켤 때 모든 칸을 새 대화로", "RamSpirit를 켜면 칸마다 지난 대화 대신 새 대화로 시작해요", startNewChat);
        swNew.SetBounds(0, 178, PW, 62); p3.Controls.Add(swNew);

        // ===== 4. 시작과 닫기 =====
        Panel p4 = addPage("\uE7E8", "시작과 닫기");
        section(p4, "시작", 44);
        var swAuto = new SwitchRow("윈도우를 켜면 RamSpirit도 켜기", "컴퓨터를 켜고 로그인하면 바로 칸에 앱이 들어간 화면이 열려요", AutoStart.Enabled);
        swAuto.SetBounds(0, 70, PW, 62); p4.Controls.Add(swAuto);
        section(p4, "닫기", 152);
        var swClose = new SwitchRow("닫을 때 칸 안의 앱도 같이 닫기", "끄면 RamSpirit만 닫히고 앱들은 따로 창으로 남아요 (Shift를 누른 채 닫으면 반대로)", closeApps);
        swClose.SetBounds(0, 178, PW, 62); p4.Controls.Add(swClose);

        // ===== 5. 정보 =====
        Panel p5 = addPage("\uE946", "정보");
        var vl = new Label();
        vl.Text = "RamSpirit v" + Updater.Version; vl.Font = new Font("Segoe UI", 15f, FontStyle.Bold); vl.ForeColor = Th.Text;
        vl.AutoSize = true; vl.Location = new Point(0, 48); vl.BackColor = Th.Glass;
        p5.Controls.Add(vl);
        var vs = new Label();
        vs.Text = "여러 AI 앱과 프로그램을 한 창에 모아 쓰는 윈도우 앱 · 만든 사람 김예람"; vs.Font = Ui.Body; vs.ForeColor = Th.Sub;
        vs.AutoSize = true; vs.Location = new Point(2, 86); vs.BackColor = Th.Glass;
        p5.Controls.Add(vs);
        var bUpd = new IconBtn();
        bUpd.Glyph = "\uE895"; bUpd.Text = Updater.Ready != null ? "새 버전 v" + Updater.Ready + " 적용하고 다시 시작" : "업데이트 확인"; bUpd.Radius = 10;
        bUpd.SetBounds(0, 128, Math.Max(150, bUpd.Need()), 38);
        bUpd.Click += delegate
        {
            if (Updater.Ready != null) { f.Close(); updBtn.PerformClick(); return; }
            CheckUpdate(true, 0);
            bUpd.Text = "확인 중... (결과는 위쪽 안내 줄에)"; bUpd.Width = bUpd.Need(); bUpd.Invalidate();
        };
        p5.Controls.Add(bUpd);
        var bDir = new IconBtn();
        bDir.Glyph = "\uE838"; bDir.Text = "설정 폴더 열기"; bDir.Radius = 10;
        bDir.SetBounds(0, 176, Math.Max(150, bDir.Need()), 38);
        bDir.Click += delegate { try { Process.Start("explorer.exe", Path.GetDirectoryName(configFile)); } catch { } };
        p5.Controls.Add(bDir);
        var bRel = new IconBtn();
        bRel.Glyph = "\uE774"; bRel.Text = "다운로드 페이지 열기"; bRel.Radius = 10;
        bRel.SetBounds(0, 224, Math.Max(150, bRel.Need()), 38);
        bRel.Click += delegate { try { Process.Start("https://github.com/dpfkadpfka/ramspirit/releases/latest"); } catch { } };
        p5.Controls.Add(bRel);

        // 처음 페이지
        navBtns[0].PerformClick();
        if (firstRun) for (int k = 1; k < navBtns.Count; k++) navBtns[k].Visible = false;

        bool chosen = false;
        ok.Click += delegate { chosen = true; f.DialogResult = DialogResult.OK; f.Close(); };
        f.KeyDown += delegate (object o, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { chosen = true; f.DialogResult = DialogResult.OK; f.Close(); } };
        f.ShowDialog(this);
        if (!chosen) return null;

        // 화면·보내기·시작 설정은 바로 적용
        if (pickTheme != Th.Name) SetTheme(pickTheme);
        if (pickIcon != AppIcon.Current) { ApplyIcon(pickIcon, true); SaveConfig(); }
        if (swTop.Checked != onTop) SetOnTop(swTop.Checked);
        if (swComp.Checked != compOpen) SetComposer(swComp.Checked);
        ctrlEnterSend = swCtrl.Checked;
        startNewChat = swNew.Checked;
        closeApps = swClose.Checked;
        if (swAuto.Checked != AutoStart.Enabled) AutoStart.Set(swAuto.Checked);
        UpdateHint();
        SaveConfig();

        var result = new List<AppInfo>();
        for (int i = 0; i < count; i++) result.Add(slots[i]);
        return result;
    }

    // ---------- 탭 ----------
    PictureBox logo;
    readonly List<IconBtn> tabBtns = new List<IconBtn>();
    IconBtn tabAdd;
    SegBox tabSeg, toolSeg;
    // 탭 끌어서 순서 바꾸기
    int tabDownX = -1; bool tabDragging;

    void SwitchTo(Workspace w)
    {
        if (w == cur || w == null) return;
        cur.ColW = colW; cur.RowW = rowW; cur.Solo = solo;
        foreach (Target t in T)
        {
            t.Cell.Visible = false;
            if (t.Hwnd != IntPtr.Zero && !t.Hidden) { W.ShowWindowAsync(t.Hwnd, 0); t.Hidden = true; }
            if (t.Guard != null) t.Guard.Hide();
        }
        cur = w; T = w.T; colW = w.ColW; rowW = w.RowW; solo = w.Solo;
        if (!w.Started)
        {
            w.Started = true;
            var list = new List<Target>();
            foreach (AppInfo a in w.Apps) list.Add(AddTarget(a));
            foreach (Target t in list) ClaimFromOtherTabs(t);
            list.RemoveAll(delegate (Target t) { return t.Hwnd != IntPtr.Zero; });
            foreach (Target t in list) t.Lent = false;
            DoLayout();
            if (list.Count > 0) StartSearch(list);
        }
        else
        {
            DoLayout();
            ClaimAll(new List<Target>(T));
        }
        foreach (Target t in T) { t.Rgn = ""; }
        SyncWindows();
        ReshowSoon();
        UpdateTabs();
        UpdateHint();
        SaveConfig();
    }

    // 같은 앱(Claude·ChatGPT처럼 창이 하나뿐인 앱)이 다른 탭에도 들어 있으면 그 창을 지금 탭으로 데려옴
    // → 두 탭이 한 창을 서로 끌어당기지 않음. 원래 탭으로 돌아가면 다시 그쪽으로 감
    bool ClaimFromOtherTabs(Target t)
    {
        if (t.Hwnd != IntPtr.Zero) return true;
        foreach (Workspace w in spaces)
        {
            if (w.T == T) continue;
            foreach (Target o in w.T)
            {
                if (o.Hwnd == IntPtr.Zero || o.Web || !o.App.SameAs(t.App)) continue;
                if (!W.IsWindow(o.Hwnd)) continue;
                HandOver(o, t, cur != null ? cur.Name : "");
                Log(t.App.Name + " 창을 「" + w.Name + "」 탭에서 지금 탭으로 옮김");
                return true;
            }
        }
        return false;
    }

    // 앱 창을 칸 o에서 칸 t로 넘김 (o는 「다른 탭에서 쓰는 중」으로)
    void HandOver(Target o, Target t, string toTab)
    {
        if (!T.Contains(t)) { W.ShowWindowAsync(o.Hwnd, 0); }
        t.Hwnd = o.Hwnd; t.OldStyle = o.OldStyle; t.Web = false;
        t.Hidden = true; t.Pending = false; t.Lost = false; t.Lent = false; t.Missing = 0; t.Misfit = 0;
        t.CapH = o.CapH; t.AutoHide = o.AutoHide; t.Rgn = ""; t.TooSmall = false; t.MinW = 0; t.MinH = 0;
        t.WantNewChat = false;
        t.Wait.Visible = false; t.Wait.Cursor = Cursors.Default;
        o.Hwnd = IntPtr.Zero; o.Hidden = false; o.Pending = false; o.TooSmall = false; o.Lent = true; o.WantNewChat = false;
        if (o.Guard != null) { o.Guard.Dispose(); o.Guard = null; }
        o.Wait.Text = o.App.Name + "은(는) 「" + toTab + "」 탭에서 쓰는 중이에요\n이 탭으로 돌아오면 다시 여기로 와요";
        o.Wait.Cursor = Cursors.Default;
        o.Wait.Visible = true;
    }

    // 지금 탭의 칸들: 다른 탭에 있는 같은 앱 창을 데려오고, 그래도 비어 있는(빌려준 탭이 사라진) 칸은 새로 찾음
    void ClaimAll(List<Target> list)
    {
        var left = new List<Target>();
        foreach (Target t in list)
        {
            if (t.Hwnd != IntPtr.Zero) continue;
            if (ClaimFromOtherTabs(t)) continue;
            if (t.Lent) { t.Lent = false; t.Wait.Text = "불러오는 중..."; t.Wait.Visible = true; left.Add(t); }
        }
        if (left.Count > 0) StartSearch(left);
    }

    // 탭을 바꾼 직후, 숨겼다 다시 보이게 한 앱 창이 늦게 반응해서 안 보이는 일이 있어 잠시 뒤 한 번 더 보이게 함
    Timer reshow;
    int reshowLeft;
    void ReshowSoon()
    {
        if (reshow == null)
        {
            reshow = new Timer(); reshow.Interval = 350;
            reshow.Tick += delegate
            {
                if (--reshowLeft <= 0) reshow.Stop();
                if (WindowState == FormWindowState.Minimized) return;
                foreach (Target t in T)
                {
                    if (t.Hwnd == IntPtr.Zero || !t.Cell.Visible || t.TooSmall) continue;
                    if (W.IsIconic(t.Hwnd) || !W.IsWindowVisible(t.Hwnd)) W.ShowWindowAsync(t.Hwnd, 4);
                    t.Hidden = false; t.Rgn = "";
                }
                SyncWindows();
            };
        }
        reshowLeft = 4;
        reshow.Stop(); reshow.Start();
    }

    void UpdateTabs()
    {
        if (bar == null || tabSeg == null) return;
        foreach (IconBtn b in tabBtns) { tabSeg.Controls.Remove(b); b.Dispose(); }
        tabBtns.Clear();
        foreach (Workspace w in spaces)
        {
            Workspace me = w;
            var b = new IconBtn();
            b.Text = w.Name; b.Seg = true; b.Active = w == cur; b.Radius = 7;
            b.Font = new Font("Malgun Gothic", 9.5f, w == cur ? FontStyle.Bold : FontStyle.Regular);
            b.MouseDown += delegate (object o, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left) { tabDownX = Cursor.Position.X; tabDragging = false; }
            };
            b.MouseMove += delegate (object o, MouseEventArgs e)
            {
                if (tabDownX < 0 || (e.Button & MouseButtons.Left) == 0) return;
                if (!tabDragging && Math.Abs(Cursor.Position.X - tabDownX) < 6) return;
                tabDragging = true;
                b.Cursor = Cursors.SizeWE;
                // 마우스가 있는 자리의 탭과 자리 바꾸기
                int mx = tabSeg.PointToClient(Cursor.Position).X;
                int from = tabBtns.IndexOf(b), to = from;
                for (int i = 0; i < tabBtns.Count; i++)
                {
                    Rectangle r = tabBtns[i].Bounds;
                    if (i != from && mx >= r.Left + r.Width / 3 && mx <= r.Right - r.Width / 3) to = i;
                }
                if (to != from && from >= 0)
                {
                    Workspace ws = spaces[from]; spaces.RemoveAt(from); spaces.Insert(to, ws);
                    tabBtns.RemoveAt(from); tabBtns.Insert(to, b);
                    LayoutBar();
                }
            };
            b.MouseUp += delegate (object o, MouseEventArgs e)
            {
                bool dragged = tabDragging;
                tabDownX = -1; tabDragging = false; b.Cursor = Cursors.Hand;
                if (dragged) { SaveConfig(); return; }
                if (e.Button == MouseButtons.Right) { TabMenu(me, b); return; }
                if (e.Button == MouseButtons.Left) BeginInvoke((System.Windows.Forms.MethodInvoker)delegate { SwitchTo(me); });
            };
            Tip(b, (w == cur ? "지금 보고 있는 탭" : "「" + w.Name + "」 탭으로 바꾸기") + " · 끌어서 순서 바꾸기 · 오른쪽 클릭: 이름 바꾸기·지우기");
            tabBtns.Add(b);
            tabSeg.Controls.Add(b);
        }
        LayoutBar();
    }

    void TabMenu(Workspace w, Control at)
    {
        var m = Ui.Menu();
        var ren = new ToolStripMenuItem("이름 바꾸기...");
        ren.Click += delegate
        {
            string n = AskName("탭 이름 바꾸기", w.Name);
            if (n == null) return;
            w.Name = n; UpdateTabs(); SaveConfig();
        };
        var apps = new ToolStripMenuItem("이 탭의 칸·앱 고르기...");
        apps.Click += delegate { SwitchTo(w); OpenSettings(); };
        var del = new ToolStripMenuItem("탭 지우기 (안의 앱도 닫혀요)");
        del.Enabled = spaces.Count > 1;
        del.Click += delegate { RemoveSpace(w); };
        m.Items.Add(ren); m.Items.Add(apps); m.Items.Add(new ToolStripSeparator()); m.Items.Add(del);
        m.Show(at, new Point(0, at.Height + 2));
    }

    void AddSpace()
    {
        string n = AskName("새 탭 만들기", "");
        if (n == null) return;
        var w = new Workspace(n);
        w.Started = true;                      // 칸은 아래 설정에서 고름
        spaces.Add(w);
        Workspace back = cur;
        SwitchTo(w);
        List<AppInfo> chosen = SettingsDialog();
        if (chosen == null) { RemoveSpace(w); SwitchTo(back); return; }
        ApplySettings(chosen, true);
        UpdateTabs();
    }

    void RemoveSpace(Workspace w)
    {
        if (spaces.Count <= 1 && w == cur && T.Count > 0) return;
        if (w == cur)
        {
            Workspace other = null;
            foreach (Workspace x in spaces) if (x != w) { other = x; break; }
            if (other != null) SwitchTo(other);
        }
        foreach (Target t in w.T)
        {
            if (t.Hwnd != IntPtr.Zero)
            {
                // 이 창을 빌려준 다른 탭이 있으면 닫지 않고 돌려줌
                Target back = null;
                foreach (Workspace x in spaces)
                    if (x != w) foreach (Target o in x.T) if (back == null && o.Lent && o.Hwnd == IntPtr.Zero && o.App.SameAs(t.App)) back = o;
                if (back != null) { W.ShowWindowAsync(t.Hwnd, 0); HandOver(t, back, ""); back.Hidden = true; back.Wait.Visible = false; }
                else { W.CloseApp(t.Hwnd, t.OldStyle); used.Remove(t.Hwnd.ToInt64()); }
            }
            area.Controls.Remove(t.Cell); t.Cell.Dispose();
        }
        w.T.Clear();
        spaces.Remove(w);
        UpdateTabs();
        SaveConfig();
    }

    // 이름 하나 묻는 작은 창
    string AskName(string title, string start)
    {
        Panel body; IconBtn ok, cancel;
        var f = Ui.Shell(title, "예: AI, 이미지, 문서, 수업", 440, 290, "확인", out body, out ok, out cancel);
        TextBox name = Ui.Field(body, "탭 이름", null, 24, 4, 440 - 48);
        name.Text = start;
        bool done = false;
        Action go = delegate { if (name.Text.Trim().Length > 0) { done = true; f.DialogResult = DialogResult.OK; f.Close(); } };
        ok.Click += delegate { go(); };
        f.KeyDown += delegate (object o, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; go(); } };
        f.Shown += delegate { name.Focus(); name.SelectAll(); };
        f.ShowDialog(this);
        if (!done) return null;
        string n = name.Text.Trim().Replace("\t", " ");
        return n.Length > 12 ? n.Substring(0, 12) : n;
    }

    // 앱 아이콘 바꾸기 (창·작업 표시줄·알림 영역·위쪽 로고, 바꿀 때는 바로가기도)
    void ApplyIcon(int n, bool shortcuts)
    {
        Icon ic = AppIcon.Load(n);
        if (ic == null) return;
        AppIcon.Current = n;
        try { Icon = ic; } catch { }
        try { if (logo != null) { Bitmap b = AppIcon.Picture(n, 32); if (b != null) logo.Image = b; } } catch { }
        try { if (tray != null) tray.Icon = ic; } catch { }
        if (shortcuts) RunBackground(delegate { AppIcon.UpdateShortcuts(n); }, delegate { });
    }

    // 테마를 바꾸면 지금 화면 색도 바로 바뀜
    void SetTheme(string name)
    {
        Color[] old = Th.Snapshot();
        Th.Set(name);
        Th.Recolor(this, old, Th.Snapshot());
        BackColor = Th.Bg1;
        foreach (Target t in T) PaintToggle(t);
        area.Invalidate(true);
        SaveConfig();
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
        foreach (Target t in toOpen) { t.Lent = false; if (ClaimFromOtherTabs(t)) t.Wait.Visible = false; }
        toOpen.RemoveAll(delegate (Target t) { return t.Hwnd != IntPtr.Zero; });
        SyncWindows();
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
        var menu = Ui.Menu();
        foreach (AppInfo p in AppInfo.Presets)
        {
            AppInfo pick = p;
            var item = new ToolStripMenuItem(p.Name, AppIcons.Small(p));
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
        if (!t.App.IsInstalled && t.App.Url != null && t.App.Url.StartsWith("http"))
        {
            menu.Items.Add(new ToolStripSeparator());
            var webItem = new ToolStripMenuItem(t.App.PreferWeb ? "컴퓨터에 설치된 앱으로 열기" : "웹 창으로 열기 (앱이 칸에 안 맞을 때)");
            webItem.Click += delegate
            {
                var a = new AppInfo(t.App.Name, t.App.Keys, t.App.Url, t.App.Color);
                a.SendPref = t.SendOn ? 1 : 0;
                a.PreferWeb = !t.App.PreferWeb;
                ChangeApp(t, a);
            };
            menu.Items.Add(webItem);
        }
        menu.Show(t.Change, new Point(0, t.Change.Height));
    }

    // 설치된 프로그램 목록에서 검색해서 고르기
    AppInfo PickInstalled(IWin32Window owner)
    {
        if (startApps == null) startApps = StartApps();
        var names = new List<string>(startApps.Keys);
        names.Sort(StringComparer.CurrentCultureIgnoreCase);

        Panel body; IconBtn ok, cancel;
        var f = Ui.Shell("설치된 프로그램에서 고르기", "이름 일부만 써도 찾아져요 (예: 카카오톡, 한글, 메모장)", 480, 600, "고르기", out body, out ok, out cancel);

        var sbox = new RoundBox();
        sbox.SetBounds(24, 4, 480 - 48, 44);
        var icon = new Label();
        icon.Text = "\uE721"; icon.Font = IconBtn.IconFont(11f); icon.ForeColor = Th.Sub; icon.BackColor = Color.White;
        icon.SetBounds(14, 13, 20, 20);
        var search = new TextBox();
        search.BorderStyle = BorderStyle.None; search.Font = new Font("Malgun Gothic", 10.5f); search.BackColor = Color.White;
        search.SetBounds(40, 12, 480 - 48 - 56, 22);
        sbox.Controls.Add(icon); sbox.Controls.Add(search);
        sbox.Click += delegate { search.Focus(); };
        body.Controls.Add(sbox);

        var lbox = new RoundBox();
        lbox.SetBounds(24, 60, 480 - 48, 600 - 84 - 72 - 60 - 12);
        var list = new ListBox();
        list.BorderStyle = BorderStyle.None; list.DrawMode = DrawMode.OwnerDrawFixed; list.ItemHeight = 36;
        list.Font = new Font("Malgun Gothic", 10.5f); list.BackColor = Color.White; list.IntegralHeight = false;
        list.SetBounds(6, 6, lbox.Width - 12, lbox.Height - 12);
        list.DrawItem += delegate (object o, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.FillRectangle(Brushes.White, e.Bounds);
            bool sel = (e.State & DrawItemState.Selected) != 0;
            if (sel)
                using (var br = new SolidBrush(Th.AccentSoft))
                using (GraphicsPath p = Th.RoundPath(new Rectangle(e.Bounds.X + 2, e.Bounds.Y + 2, e.Bounds.Width - 4, e.Bounds.Height - 4), 8))
                    g.FillPath(br, p);
            TextRenderer.DrawText(g, list.Items[e.Index].ToString(), list.Font, new Rectangle(e.Bounds.X + 14, e.Bounds.Y, e.Bounds.Width - 20, e.Bounds.Height),
                sel ? Th.Accent : Th.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        };
        lbox.Controls.Add(list);
        body.Controls.Add(lbox);

        EventHandler fill = delegate
        {
            string q = search.Text.Trim();
            list.BeginUpdate();
            list.Items.Clear();
            foreach (string n in names)
                if (q.Length == 0 || n.IndexOf(q, StringComparison.CurrentCultureIgnoreCase) >= 0) list.Items.Add(n);
            if (list.Items.Count > 0) list.SelectedIndex = 0;
            list.EndUpdate();
            ok.Enabled = list.Items.Count > 0;
        };
        search.TextChanged += fill;
        search.KeyDown += delegate (object o, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Down && list.SelectedIndex < list.Items.Count - 1) { list.SelectedIndex++; e.Handled = true; }
            if (e.KeyCode == Keys.Up && list.SelectedIndex > 0) { list.SelectedIndex--; e.Handled = true; }
        };
        bool chosen = false;
        Action pickNow = delegate { if (list.SelectedItem != null) { chosen = true; f.DialogResult = DialogResult.OK; f.Close(); } };
        ok.Click += delegate { pickNow(); };
        list.DoubleClick += delegate { pickNow(); };
        f.KeyDown += delegate (object o, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; pickNow(); } };
        f.Shown += delegate { search.Focus(); };
        fill(null, EventArgs.Empty);

        f.ShowDialog(owner);
        if (!chosen || list.SelectedItem == null) return null;
        string pick = (string)list.SelectedItem;
        return AppInfo.FromInstalled(pick, startApps[pick]);
    }

    AppInfo AskCustom(IWin32Window owner)
    {
        Panel body; IconBtn ok, cancel;
        var f = Ui.Shell("사이트 직접 넣기", "목록에 없는 AI나 웹사이트를 칸에 넣어요", 480, 380, "넣기", out body, out ok, out cancel);
        TextBox name = Ui.Field(body, "이름", "칸 제목에 보일 이름이에요 (예: 클로바X)", 24, 4, 480 - 48);
        TextBox url = Ui.Field(body, "사이트 주소", "https:// 는 빼고 써도 돼요 (예: clova-x.naver.com)", 24, 110, 480 - 48);
        var err = new Label();
        err.ForeColor = ColorTranslator.FromHtml("#d6335e"); err.Font = Ui.Body; err.AutoSize = true;
        err.Location = new Point(26, 206); err.BackColor = Th.Glass;
        body.Controls.Add(err);
        bool chosen = false;
        Action done = delegate
        {
            if (name.Text.Trim().Length == 0) { err.Text = "이름을 써 주세요"; name.Focus(); return; }
            if (url.Text.Trim().Length == 0) { err.Text = "사이트 주소를 써 주세요"; url.Focus(); return; }
            chosen = true; f.DialogResult = DialogResult.OK; f.Close();
        };
        ok.Click += delegate { done(); };
        f.KeyDown += delegate (object o, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; done(); } };
        f.Shown += delegate { name.Focus(); };
        f.ShowDialog(owner);
        if (!chosen) return null;
        return AppInfo.Custom(name.Text.Trim(), url.Text.Trim());
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
        t.Lent = false;
        SaveConfig();
        if (ClaimFromOtherTabs(t)) { SyncWindows(); return; }
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
            Tip(t.Btn, (solo == i) ? "원래 칸 나누기로 돌아가기" : "이 칸만 크게 보기");
            t.Btn.Invalidate();
        }
        SyncWindows();
        UpdateHint();
    }

    // ---------- 칸 사이 틈 끌기 ----------
    const int Gap = 6;
    int[] Lx = new int[0], Lw = new int[0], Ly = new int[0], Lh = new int[0];
    int dragColAt = -1, dragRowAt = -1;

    // 칸 사이 틈 + 양쪽 칸의 가장자리 여백까지 잡히게 (틈이 좁아도 쉽게 잡힘)
    const int Grab = 7;

    int HitCol(int x)
    {
        for (int c = 0; c + 1 < Lx.Length; c++)
        {
            int gx = Lx[c] + Lw[c];
            if (x >= gx - Grab && x <= gx + Gap + Grab) return c;
        }
        return -1;
    }

    int HitRow(int y)
    {
        for (int r = 0; r + 1 < Ly.Length; r++)
        {
            int gy = Ly[r] + Lh[r];
            if (y >= gy - Grab && y <= gy + Gap + Grab) return r;
        }
        return -1;
    }

    // 바탕·칸 테두리·칸 제목 줄 어디서든 칸 사이 선을 잡을 수 있게 마우스 신호를 넘겨받음
    void SplitForward(Control c)
    {
        Func<Point, Point> toArea = delegate (Point p) { return c == area ? p : area.PointToClient(c.PointToScreen(p)); };
        c.MouseMove += delegate (object o, MouseEventArgs e)
        {
            Point p = toArea(e.Location);
            if (dragColAt < 0 && dragRowAt < 0)
            {
                if (solo >= 0) { c.Cursor = Cursors.Default; return; }
                bool hc = HitCol(p.X) >= 0, hr = HitRow(p.Y) >= 0;
                c.Cursor = (hc && hr) ? Cursors.SizeAll : hc ? Cursors.SizeWE : hr ? Cursors.SizeNS : Cursors.Default;
                return;
            }
            dragPt = p;
            MoveGuides();
        };
        c.MouseDown += delegate (object o, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || solo >= 0) return;
            Point p = toArea(e.Location);
            dragColAt = HitCol(p.X); dragRowAt = HitRow(p.Y);
            if (dragColAt >= 0 || dragRowAt >= 0) { dragPt = p; ShowGuides(); }
        };
        c.MouseUp += delegate { EndDrag(); };
    }

    // 끌기를 끝냄 (마우스를 놓은 신호를 놓쳐도 타이머가 대신 끝냄 → 선이 화면에 남거나 칸이 안 움직이는 일 없게)
    void EndDrag()
    {
            if (dragTimer != null) dragTimer.Stop();
            if (dragColAt < 0 && dragRowAt < 0) { HideGuides(); return; }
            HideGuides();
            ApplySplit(dragPt);          // 놓았을 때 한 번만 칸 크기를 바꿈 → 앱 창이 들썩이지 않음
            dragColAt = -1; dragRowAt = -1;
            foreach (Target t in T) t.Grown = true;   // 직접 정한 크기를 자동으로 되돌리지 않음
            DoLayout();
            SaveConfig();
    }

    Timer dragTimer;
    Point dragPt;
    GuideLine guideV, guideH;

    // 끄는 위치를 칸 경계 안으로 제한 (한 칸이 너무 작아지지 않게)
    int ClampX(int x)
    {
        int k = dragColAt;
        if (k < 0 || k + 1 >= Lx.Length) return x;
        int left = Lx[k], right = Lx[k + 1] + Lw[k + 1];
        int n = T.Count, rows = n >= 4 ? 2 : 1, cols = Math.Max(1, n / rows);
        int minL = 220, minR = 220;
        for (int i = 0; i < n; i++)
        {
            if (T[i].MinW <= 0) continue;
            if (i % cols == k) minL = Math.Max(minL, T[i].MinW + 12);
            if (i % cols == k + 1) minR = Math.Max(minR, T[i].MinW + 12);
        }
        if (minL + minR > right - left) { minL = 220; minR = 220; }
        return Math.Max(left + minL, Math.Min(right - minR, x));
    }

    int ClampY(int y)
    {
        int k = dragRowAt;
        if (k < 0 || k + 1 >= Ly.Length) return y;
        int top = Ly[k], bottom = Ly[k + 1] + Lh[k + 1];
        int n = T.Count, rows = n >= 4 ? 2 : 1, cols = Math.Max(1, n / rows);
        int minT = 160, minB = 160;
        for (int i = 0; i < n; i++)
        {
            if (T[i].MinH <= 0) continue;
            if (i / cols == k) minT = Math.Max(minT, T[i].MinH + 52);
            if (i / cols == k + 1) minB = Math.Max(minB, T[i].MinH + 52);
        }
        if (minT + minB > bottom - top) { minT = 160; minB = 160; }
        return Math.Max(top + minT, Math.Min(bottom - minB, y));
    }

    void ShowGuides()
    {
        if (dragColAt >= 0) { if (guideV == null) { guideV = new GuideLine(); guideV.Owner = this; } }
        if (dragRowAt >= 0) { if (guideH == null) { guideH = new GuideLine(); guideH.Owner = this; } }
        MoveGuides();
        if (dragColAt >= 0) guideV.Show();
        if (dragRowAt >= 0) guideH.Show();
        if (dragTimer == null)
        {
            dragTimer = new Timer();
            dragTimer.Interval = 30;
            dragTimer.Tick += delegate
            {
                if (dragColAt < 0 && dragRowAt < 0) { dragTimer.Stop(); HideGuides(); return; }
                dragPt = area.PointToClient(Cursor.Position);
                if ((Control.MouseButtons & MouseButtons.Left) == 0) EndDrag(); else MoveGuides();
            };
        }
        dragTimer.Start();
    }

    void MoveGuides()
    {
        Rectangle a = area.RectangleToScreen(area.ClientRectangle);
        if (dragColAt >= 0 && guideV != null)
        {
            int x = ClampX(dragPt.X);
            guideV.Bounds = new Rectangle(a.X + x - 3, a.Y + 4, 6, a.Height - 8);
        }
        if (dragRowAt >= 0 && guideH != null)
        {
            int y = ClampY(dragPt.Y);
            guideH.Bounds = new Rectangle(a.X + 4, a.Y + y - 3, a.Width - 8, 6);
        }
    }

    void HideGuides()
    {
        if (guideV != null) guideV.Hide();
        if (guideH != null) guideH.Hide();
    }

    void ApplySplit(Point pt)
    {
        int k = dragColAt;
        if (k >= 0 && k + 1 < Lx.Length && k + 1 < colW.Length)
        {
            int left = Lx[k], span = Lx[k + 1] + Lw[k + 1] - left - Gap;
            int px = ClampX(pt.X) - left - Gap / 2;
            double total = colW[k] + colW[k + 1];
            if (span > 100) { colW[k] = total * px / span; colW[k + 1] = total - colW[k]; }
        }
        k = dragRowAt;
        if (k >= 0 && k + 1 < Ly.Length && k + 1 < rowW.Length)
        {
            int top = Ly[k], span = Ly[k + 1] + Lh[k + 1] - top - Gap;
            int py = ClampY(pt.Y) - top - Gap / 2;
            double total = rowW[k] + rowW[k + 1];
            if (span > 100) { rowW[k] = total * py / span; rowW[k + 1] = total - rowW[k]; }
        }
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
                if (t.TooSmall)
                {
                    // 칸이 다시 앱 최소 크기만큼 커졌으면 다시 보여 줌, 아니면 계속 숨겨 둠
                    if (r.Width + 10 >= t.MinW && r.Height + 10 >= t.MinH) { t.TooSmall = false; t.Misfit = 0; t.Wait.Visible = false; t.Wait.Cursor = Cursors.Default; }
                    else continue;
                }
                int top = t.Top;
                W.Place(t.Hwnd, r.X, r.Y - top, r.Width, r.Height + top);
                ClipTo(t, top, r.Width, r.Height);
                PlaceGuard(t);
                if (t.Hidden) { W.ShowWindowAsync(t.Hwnd, 8); t.Hidden = false; }
            }
            else if (!t.Hidden)
            {
                W.ShowWindowAsync(t.Hwnd, 0);
                t.Hidden = true;
                if (t.Guard != null) t.Guard.Hide();
            }
        }
    }

    // 칸에 보이는 부분만 남기고 잘라 냄(바뀌었을 때만)
    void ClipTo(Target t, int top, int w, int h)
    {
        string key = top + "," + w + "," + h;
        if (key == t.Rgn || t.Hwnd == IntPtr.Zero) return;
        t.Rgn = key;
        W.Clip(t.Hwnd, top, w, h, 16);   // 칸 안쪽(반지름 8)과 같은 둥근 모서리
    }

    // 앱이 스스로 그린 위쪽 제목 줄(아이콘·제목·최소화·닫기)을 찾아서 그 높이만큼 감춤
    void FindTitleBar(Target t, IntPtr h, string proc)
    {
#if !NO_UIA
        Rectangle got = Rectangle.Empty;
        RunBackground(delegate
        {
            for (int i = 0; i < 4 && t.Hwnd == h; i++)
            {
                System.Threading.Thread.Sleep(i == 0 ? 1000 : 1200);
                got = Uia.CaptionButtons(h);
                if (!got.IsEmpty) break;
            }
        }, delegate
        {
            if (t.Hwnd != h || got.IsEmpty) return;
            W.RECT wr0; W.GetWindowRect(h, out wr0);
            int winW = wr0.R - wr0.L;
            // X 자리에 「창 오른쪽 끝에서 버튼 왼쪽 끝까지 거리」를 넣어 둠
            t.CapBtns = new Rectangle(winW - got.X, got.Y, got.Width, got.Height);
            // 가림판 색: 버튼 바로 왼쪽의 앱 바탕색
            try
            {
                W.RECT wr; W.GetWindowRect(h, out wr);
                using (var bmp = new Bitmap(1, 1))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                        g.CopyFromScreen(wr.L + got.X - 6, wr.T + got.Y + Math.Min(6, got.Height / 3), 0, 0, new Size(1, 1));
                    t.GuardColor = bmp.GetPixel(0, 0);
                }
            }
            catch { }
            PlaceGuard(t);
        });
#endif
    }

    // 앱 창의 닫기·최소화·최대화 버튼 위에 작은 가림판을 덮어 눌리지 않게 (실수로 앱이 꺼지지 않게)
    void PlaceGuard(Target t)
    {
        // 닫기 버튼 가림판은 쓰지 않음 (2.0.7부터)
        if (t.Guard != null) { t.Guard.Hide(); t.Guard.Dispose(); t.Guard = null; }
        return;
        if (t.CapBtns.IsEmpty || t.Hwnd == IntPtr.Zero) { if (t.Guard != null) t.Guard.Hide(); return; }
        if (t.Hidden || !t.Cell.Visible) { if (t.Guard != null) t.Guard.Hide(); return; }
        if (WindowState == FormWindowState.Minimized || !Visible || !W.IsWindowVisible(t.Hwnd) || W.IsIconic(t.Hwnd))
        { if (t.Guard != null) t.Guard.Hide(); return; }
        Rectangle b = t.Body.RectangleToScreen(t.Body.ClientRectangle);
        // 실제 앱 창이 있는 자리 기준으로 (앱이 아직 다른 곳에 있으면 가림판도 띄우지 않음)
        W.RECT aw; W.GetWindowRect(t.Hwnd, out aw);
        var app = Rectangle.FromLTRB(aw.L, aw.T, aw.R, aw.B);
        var r = new Rectangle(app.Right - t.CapBtns.X, app.Y + t.CapBtns.Y, t.CapBtns.Width, t.CapBtns.Height);
        r.Intersect(b);
        r.Intersect(app);
        r.Intersect(RectangleToScreen(ClientRectangle));   // RamSpirit 창 바깥으로는 절대 안 나감
        if (r.Width <= 0 || r.Height <= 0 || r.Width > 400 || r.Height > 80) { if (t.Guard != null) t.Guard.Hide(); return; }
        if (t.Guard == null || t.Guard.IsDisposed)
        {
            var g = new GuardForm();
            g.Owner = this;
            t.Guard = g;
        }
        if (!t.Guard.Visible && !OursInFront()) return;    // 다른 프로그램을 쓰는 중엔 새로 띄우지 않음 (그 위로 튀어나오지 않게)
        t.GuardColor = SampleLeftOf(r, t.GuardColor);
        t.Guard.BackColor = t.GuardColor;
        t.Guard.Bounds = r;
        ((GuardForm)t.Guard).Stamp(r);
        if (!t.Guard.Visible) t.Guard.Show();
        // 앱 창을 가림판 바로 아래로 (다른 프로그램과의 앞뒤 순서는 건드리지 않음)
        W.SetWindowPos(t.Hwnd, t.Guard.Handle, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010 | 0x4000);
    }

    void RaiseGuards()
    {
        return;
        if (!OursInFront()) return;          // 다른 프로그램을 쓰는 중이면 아무것도 안 함
        foreach (Target t in T)
        {
            if (t.Hwnd == IntPtr.Zero || t.Hidden) continue;
            if (t.Guard == null || t.Guard.IsDisposed || !t.Guard.Visible) { PlaceGuard(t); continue; }
            W.SetWindowPos(t.Hwnd, t.Guard.Handle, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010 | 0x4000);
        }
        // 앞에 오는 창이 바뀌면 제목 줄 색이 바뀌므로 잠깐 뒤 가림판 색도 다시 맞춤
        if (guardTimer == null)
        {
            guardTimer = new Timer();
            guardTimer.Interval = 250;
            guardTimer.Tick += delegate
            {
                guardTimer.Stop();
                foreach (Target t in T)
                    if (t.Guard != null && !t.Guard.IsDisposed && t.Guard.Visible)
                    {
                        Color c = SampleLeftOf(t.Guard.Bounds, t.GuardColor);
                        if (c != t.GuardColor) { t.GuardColor = c; t.Guard.BackColor = c; }
                        ((GuardForm)t.Guard).Stamp(t.Guard.Bounds);
                    }
            };
        }
        guardTimer.Stop(); guardTimer.Start();
    }

    Timer guardTimer;

    protected override void WndProc(ref Message m)
    {
        if (Program.CmdMsg != 0 && (uint)m.Msg == Program.CmdMsg) { RunCmd(m.WParam.ToInt32()); return; }
        base.WndProc(ref m);
    }

    // 작업 표시줄 메뉴 명령: 1 업데이트 확인 · 2 칸 나누기 화면 · 3 모두 새 대화 · 4 설정
    void RunCmd(int c)
    {
        BringUp();
        if (c == 1) { if (Updater.Ready != null) updBtn.PerformClick(); else CheckUpdate(true, 0); }
        else if (c == 2) { if (solo >= 0 && solo < T.Count) ToggleSolo(T[solo]); }
        else if (c == 3) { foreach (Target x in T.ToArray()) if (x.Cell.Visible) NewChat(x); }
        else if (c == 4) OpenSettings();
    }

    // 지금 맨 앞 창이 RamSpirit나 칸 안의 앱인지
    bool OursInFront()
    {
        IntPtr fg = W.GetForegroundWindow();
        if (fg == IntPtr.Zero) return false;
        uint pid; W.GetWindowThreadProcessId(fg, out pid);
        if (pid == (uint)Process.GetCurrentProcess().Id) return true;
        foreach (Target t in T) if (t.Hwnd == fg) return true;
        return false;
    }

    // ---------- 항상 위 ----------
    bool onTop;
    IconBtn pinBtn;
    bool ctrlEnterSend;          // Ctrl+Enter로 보내기 (Enter는 줄바꿈)
    bool startNewChat = true;    // 켤 때 모든 칸을 새 대화로
    bool closeApps = true;       // 닫을 때 칸 안 앱도 같이 닫기
    bool startupSearch;

    void SetOnTop(bool on)
    {
        onTop = on;
        TopMost = on;
        if (pinBtn != null)
        {
            pinBtn.Active = on; pinBtn.Ghost = !on;
            pinBtn.Glyph = on ? "\uE841" : "\uE718";
            pinBtn.Invalidate();
            Tip(pinBtn, on ? "항상 위: 켜짐 (누르면 끔)" : "항상 위: 꺼짐 — 누르면 다른 창보다 항상 위에 떠 있어요");
        }
    }

    // 화면에서 그 사각형 바로 왼쪽 점의 색
    static Color SampleLeftOf(Rectangle r, Color fallback)
    {
        try
        {
            using (var bmp = new Bitmap(1, 1))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                    g.CopyFromScreen(r.X - 4, r.Y + Math.Min(4, r.Height / 3), 0, 0, new Size(1, 1));
                return bmp.GetPixel(0, 0);
            }
        }
        catch { return fallback; }
    }

    void ToggleSolo(Target t)
    {
        int i = T.IndexOf(t);
        dragColAt = -1; dragRowAt = -1; HideGuides();
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
    IconBtn sendBtn, compToggle;
    Panel comp;
    bool compOpen = true;

    // 아래 입력창을 접으면 얇은 줄만 남고, 그만큼 칸이 커짐
    void SetComposer(bool open)
    {
        compOpen = open;
        comp.Visible = open;
        compToggle.Active = open;
        compToggle.Invalidate();
        Tip(compToggle, open ? "아래 입력창 닫기 (칸이 더 커져요)" : "아래에 입력창을 열어서, 종이비행기가 켜진 칸에 같은 글을 한 번에 보내요");
        if (open) q.Focus();
    }
    Label qHint;
    Label sendHint;

    void PaintToggle(Target t)
    {
        if (t.SendTg == null) return;
        t.SendTg.Active = t.SendOn;
        t.SendTg.Strike = !t.SendOn;
        t.SendTg.Invalidate();
        Tip(t.SendTg, t.SendOn ? "보내기 켜짐 — 아래 입력창에 쓴 글이 이 칸에도 들어가요 (누르면 끔)" : "보내기 꺼짐 — 이 칸에는 보내지 않아요 (누르면 켬)");
    }

    readonly List<string> attach = new List<string>();   // 함께 보낼 파일
    IconBtn fileBtn;

    bool hintBusy;
    Timer hintTimer;
    // 알림 글자는 잠깐만 보이고 사라짐
    void HintChanged()
    {
        if (sendHint.Text.Length == 0 || attach.Count > 0 && sendHint.Text.StartsWith("첨부")) return;
        if (hintTimer == null)
        {
            hintTimer = new Timer(); hintTimer.Interval = 7000;
            hintTimer.Tick += delegate { hintTimer.Stop(); if (busy) { hintTimer.Start(); return; } hintBusy = false; UpdateHint(); };
        }
        hintBusy = true;
        hintTimer.Stop(); hintTimer.Start();
    }

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
        if (!hintBusy) sendHint.Text = files.Length > 0 ? files.TrimEnd(' ', '·') : "";
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
            var menu = Ui.Menu();
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

    // 대화상자를 띄우는 동안 칸의 앱 창을 잠시 숨김 (SyncWindows가 다시 보여 줌)
    void HideApps()
    {
        foreach (Target t in T)
            if (t.Hwnd != IntPtr.Zero && !t.Hidden) { W.ShowWindowAsync(t.Hwnd, 0); t.Hidden = true; }
    }

    void PickFiles()
    {
        var d = new OpenFileDialog();
        d.Multiselect = true;
        d.Title = "모든 칸에 함께 보낼 파일 고르기";
        HideApps();
        if (d.ShowDialog(this) == DialogResult.OK) AddFiles(d.FileNames);
        SyncWindows();
    }

    void BuildComposer()
    {
        comp = new Panel();
        comp.Dock = DockStyle.Bottom; comp.Height = 64; comp.BackColor = Th.Glass;
        comp.Paint += delegate (object o, PaintEventArgs e)
        {
            using (var pen = new Pen(Th.Line)) e.Graphics.DrawLine(pen, 0, 0, comp.Width, 0);
        };
        var box = new Panel();
        box.BackColor = Th.Card;
        Th.Round(box, 16);
        q = new TextBox();
        q.Multiline = true; q.Font = new Font("Malgun Gothic", 11); q.BorderStyle = BorderStyle.None;
        q.BackColor = Th.Card; q.ForeColor = Th.Text;
        // 비어 있을 때 보이는 안내 글자
        qHint = new Label();
        qHint.Text = "여기에 쓰면 종이비행기가 켜진 칸에 한 번에 보내요"; qHint.Font = q.Font; qHint.ForeColor = Th.Faint;
        qHint.BackColor = Th.Card; qHint.Cursor = Cursors.IBeam;
        qHint.Click += delegate { q.Focus(); };
        q.TextChanged += delegate { qHint.Visible = q.Text.Length == 0; };
        box.Controls.Add(q);
        box.Controls.Add(qHint);
        qHint.BringToFront();
        sendBtn = new IconBtn();
        sendBtn.Primary = true; sendBtn.Glyph = "\uE724"; sendBtn.Text = "한 번에 보내기"; sendBtn.Radius = 14;
        sendBtn.Font = new Font("Malgun Gothic", 10.5f, FontStyle.Bold);
        fileBtn = new IconBtn();
        fileBtn.Glyph = "\uE723"; fileBtn.Text = "파일"; fileBtn.Radius = 14;
        fileBtn.Font = new Font("Malgun Gothic", 10f, FontStyle.Bold);
        Tip(fileBtn, "모든 칸에 함께 보낼 파일 넣기 (입력창에 끌어다 놓아도 돼요)");
        fileBtn.Click += delegate { FileButtonClick(); };
        comp.Controls.Add(box); comp.Controls.Add(fileBtn); comp.Controls.Add(sendBtn);
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
            int sw = 160, fw = Math.Max(96, fileBtn.Need());
            box.SetBounds(10, 10, Math.Max(120, w - 10 - sw - 6 - fw - 8), 44);
            q.SetBounds(14, 11, Math.Max(80, box.Width - 28), 24);
            qHint.SetBounds(17, 12, Math.Max(80, box.Width - 34), 22);
            fileBtn.SetBounds(w - 10 - sw - 6 - fw, 10, fw, 44);
            sendBtn.SetBounds(w - 10 - sw, 10, sw, 44);
        };
        box.Click += delegate { q.Focus(); };
        q.KeyDown += delegate (object s, KeyEventArgs e)
        {
            bool go = ctrlEnterSend ? (e.KeyCode == Keys.Enter && e.Control) : (e.KeyCode == Keys.Enter && !e.Shift && !e.Control);
            if (go)
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
    readonly Dictionary<Control, string> tipText = new Dictionary<Control, string>();
    Control tipOn;
    Timer tipTimer;

    // 마우스를 올리면 설명을 아이콘 "위쪽"에 띄움 (아래에 뜨면 마우스 화살표에 가려서)
    void Tip(Control c, string text)
    {
        bool hooked = tipText.ContainsKey(c);
        tipText[c] = text;
        if (tipOn == c) ShowTip();
        if (hooked) return;
        c.MouseEnter += delegate
        {
            tipOn = c;
            if (tipTimer == null)
            {
                tipTimer = new Timer();
                tipTimer.Interval = 350;
                tipTimer.Tick += delegate { tipTimer.Stop(); ShowTip(); };
            }
            tipTimer.Stop(); tipTimer.Start();
        };
        c.MouseLeave += delegate
        {
            if (tipTimer != null) tipTimer.Stop();
            if (tipOn == c) { tip.Hide(c); tipOn = null; }
        };
        c.MouseDown += delegate { if (tipTimer != null) tipTimer.Stop(); tip.Hide(c); };
    }

    void ShowTip()
    {
        Control c = tipOn;
        string text;
        if (c == null || !c.IsHandleCreated || !tipText.TryGetValue(c, out text)) return;
        Size sz = TextRenderer.MeasureText(text, SystemFonts.StatusFont);
        int x = Math.Max(-c.Left, (c.Width - sz.Width - 10) / 2);
        tip.Show(text, c, x, -(sz.Height + 14), 4000);
    }

    void LayoutBar()
    {
        if (barBtns == null) return;
        int h = 28, y = (bar.Height - h) / 2, x = 132;
        // 왼쪽 묶음: 모두 새 대화 · 칸 크기 같게 · 한 번에 보내기
        int ix = 3;
        for (int i = 0; i < 3; i++) { int w = barBtns[i].Need() - 8; barBtns[i].SetBounds(ix, 3, w, h - 6); ix += w + 2; }
        toolSeg.SetBounds(x, y, ix + 1, h);
        x += ix + 1;
        // 오른쪽: [업데이트] [핀] 버전 [톱니]
        int r = bar.ClientSize.Width - 10;
        barBtns[3].SetBounds(r - h, y, h, h); r -= h + 4;
        verLbl.Visible = false;   // 버전은 설정 → 정보에서
        if (pinBtn != null) { pinBtn.SetBounds(r - h, y, h, h); r -= h + 6; }
        if (updBtn.Visible) { int uw = updBtn.Need(); updBtn.SetBounds(r - uw, y, uw, h); r -= uw + 8; }
        // 탭 묶음 [AI][이미지] + [+]
        if (tabAdd != null && tabSeg != null)
        {
            r -= 6;
            tabAdd.SetBounds(r - h, y, h, h); r -= h + 4;
            int tx = 3;
            foreach (IconBtn b in tabBtns) { int tw = Math.Max(48, b.Need() - 4); b.SetBounds(tx, 3, tw, h - 6); tx += tw + 2; }
            int segW = tx + 1;
            tabSeg.SetBounds(r - segW, y, segW, h); r -= segW + 8;
            tabSeg.Visible = tabBtns.Count > 0;
        }
        // 가운데: 잠깐 보이는 알림 글자 (평소엔 비어 있음)
        if (sendHint != null) sendHint.SetBounds(x + 14, y, Math.Max(10, r - x - 14), h);
    }

    void CheckUpdateLater() { CheckUpdate(false, 4000); }

    bool checking;

    // 업데이트 확인 (manual = 직접 눌렀을 때 → 결과를 알려 줌)
    void CheckUpdate(bool manual, int delay)
    {
        if (checking) return;
        if (Updater.Ready != null) { ShowReady(Updater.Ready, manual); return; }
        checking = true;
        if (manual) sendHint.Text = "새 버전이 있는지 확인하는 중...";
        string got = null;
        RunBackground(delegate
        {
            if (delay > 0) System.Threading.Thread.Sleep(delay);
            got = Updater.CheckAndUpdate();
        }, delegate
        {
            checking = false;
            if (got != null) { ShowReady(got, manual); return; }
            if (manual)
            {
                string msg = "지금 쓰는 v" + Updater.Version + "이 최신 버전이에요.";
                sendHint.Text = msg;
                if (tray != null) tray.ShowBalloonTip(3000, "RamSpirit", msg, ToolTipIcon.Info);
            }
        });
    }

    void ShowReady(string ver, bool manual)
    {
        updBtn.Text = "새 버전 v" + ver + " 준비됨 · 다시 시작";
        updBtn.Visible = true;
        Tip(updBtn, "누르면 RamSpirit가 다시 켜지면서 새 버전이 적용돼요");
        LayoutBar();
        if (trayUpdate != null) { trayUpdate.Text = "새 버전 v" + ver + " 적용하고 다시 시작"; }
        if (tray != null && manual) tray.ShowBalloonTip(3000, "RamSpirit", "새 버전 v" + ver + "을 받아 두었어요. 위쪽 보라색 버튼이나 이 메뉴로 다시 시작하세요.", ToolTipIcon.Info);
    }

    // ---------- 작업 표시줄 오른쪽 아래 알림 영역(트레이) 아이콘 ----------
    NotifyIcon tray;
    ToolStripMenuItem trayUpdate;

    void MakeTray()
    {
        tray = new NotifyIcon();
        try { tray.Icon = Icon ?? Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        tray.Text = "RamSpirit v" + Updater.Version;
        var m = Ui.Menu();
        var open = new ToolStripMenuItem("RamSpirit 열기");
        open.Font = new Font(m.Font, FontStyle.Bold);
        open.Click += delegate { BringUp(); };
        var home = new ToolStripMenuItem("칸 나누기 화면으로");
        home.Click += delegate { BringUp(); if (solo >= 0 && solo < T.Count) ToggleSolo(T[solo]); };
        var allNew = new ToolStripMenuItem("모두 새 대화");
        allNew.Click += delegate { foreach (Target x in T.ToArray()) if (x.Cell.Visible) NewChat(x); };
        var set = new ToolStripMenuItem("설정...");
        set.Click += delegate { BringUp(); OpenSettings(); };
        trayUpdate = new ToolStripMenuItem("업데이트 확인");
        trayUpdate.Click += delegate
        {
            if (Updater.Ready != null) { updBtn.PerformClick(); return; }
            CheckUpdate(true, 0);
        };
        var ver = new ToolStripMenuItem("지금 버전 v" + Updater.Version);
        ver.Enabled = false;
        var quit = new ToolStripMenuItem("끝내기");
        quit.Click += delegate { Close(); };
        m.Items.Add(open); m.Items.Add(home); m.Items.Add(allNew); m.Items.Add(set);
        m.Items.Add(new ToolStripSeparator());
        m.Items.Add(trayUpdate); m.Items.Add(ver);
        m.Items.Add(new ToolStripSeparator());
        m.Items.Add(quit);
        tray.ContextMenuStrip = m;
        tray.DoubleClick += delegate { BringUp(); };
        tray.MouseClick += delegate (object o, MouseEventArgs e) { if (e.Button == MouseButtons.Left) BringUp(); };
        tray.Visible = true;
    }

    void BringUp()
    {
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Maximized;
        Show();
        Activate();
        W.ForceForeground(Handle);
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
        "새 채팅", "새 대화", "새로운 채팅", "새 채팅 시작", "새 대화 시작",
        "New session", "새 세션", "새로 만들기" };

    // 앱 안의 「새 채팅」 버튼을 찾아 누름(일꾼 스레드). 웹 창은 못 찾으면 창을 새로 열어서 새 대화로
    void NewChat(Target t) { NewChat(t, false); }

    // 새 대화 버튼을 누른 뒤 그 앱이 대화상자(예: Claude 「로컬 세션용 폴더 선택」)를 띄우면 닫음
    bool CloseSurprise(IntPtr h, Dictionary<long, string> before0)
    {
        string proc = W.ProcessOf(h);
        for (int i = 0; i < 16; i++)
        {
            System.Threading.Thread.Sleep(200);
            foreach (KeyValuePair<long, string> kv in W.All())
            {
                if (before0.ContainsKey(kv.Key)) continue;
                IntPtr d = new IntPtr(kv.Key);
                if (W.ClassOf(d) != "#32770" || W.ProcessOf(d) != proc) continue;
                W.PostMessage(d, 0x0010, IntPtr.Zero, IntPtr.Zero);   // 닫기(취소)
                return true;
            }
        }
        return false;
    }

    static void keyCombo(byte vk)
    {
        W.keybd_event(0x11, 0, 0, UIntPtr.Zero);
        W.keybd_event(0x10, 0, 0, UIntPtr.Zero);
        W.keybd_event(vk, 0, 0, UIntPtr.Zero);
        W.keybd_event(vk, 0, 2, UIntPtr.Zero);
        W.keybd_event(0x10, 0, 2, UIntPtr.Zero);
        W.keybd_event(0x11, 0, 2, UIntPtr.Zero);
    }
    readonly object newChatLock = new object();

    // 새 대화: 칸마다 차례대로 하나씩 (동시에 하면 서로 엉켜서 엉뚱한 곳이 눌림). 마우스는 움직이지 않음.
    void NewChat(Target t, bool quiet)
    {
        if (t.Hwnd == IntPtr.Zero) { if (!quiet) Reopen(t); return; }
        IntPtr h = t.Hwnd;
        bool done = false, popupClosed = false;
        RunBackground(delegate
        {
            lock (newChatLock)
            {
#if !NO_UIA
                // 버튼을 누른 뒤 그 앱이 띄우는 대화상자(예: Claude 「로컬 세션용 폴더 선택」)는 바로 닫음
                var watcher = new System.Threading.Thread(delegate () { popupClosed = CloseSurprise(h, W.All()); });
                watcher.IsBackground = true;
                watcher.Start();
                // 키보드·마우스는 쓰지 않고 앱 안의 「새 채팅」 버튼에 직접 눌러 달라고 알림
                // 그래도 앱이 자기 창을 앞으로 가져오면, 쓰고 있던 창으로 바로 되돌림 (입력하던 커서가 안 뺏기게)
                IntPtr fg = W.GetForegroundWindow();
                done = Uia.ClickByName(h, NewChatNames);
                for (int i = 0; i < 6; i++)
                {
                    System.Threading.Thread.Sleep(60);
                    if (fg != IntPtr.Zero && W.GetForegroundWindow() != fg) { W.ForceForeground(fg); break; }
                }
                watcher.Join(done ? 3200 : 100);
#endif
                System.Threading.Thread.Sleep(150);
            }
        }, delegate
        {
            if (popupClosed)
            {
#if !NO_UIA
                RunBackground(delegate { try { Log(t.App.Name + " 폴더 창이 뜸 · 버튼 후보: " + Uia.ButtonNames(h, 40)); } catch { } }, delegate { });
#endif
                if (!quiet) sendHint.Text = t.App.Name + ": 새 대화 대신 폴더 고르는 창이 떠서 닫았어요 (작업 모드). 그 앱에서 새 채팅을 직접 눌러 주세요.";
                return;
            }
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
#if !NO_UIA
            RunBackground(delegate { try { Log(t.App.Name + " 새 대화 버튼 후보: " + Uia.ButtonNames(h, 40)); } catch { } }, delegate { });
#endif
        });
    }

    // 앱이 닫혔으면 칸에 「다시 열기」 안내
    void WatchApps()
    {
        if (WindowState == FormWindowState.Minimized) return;
        foreach (Target t in T)
        {
            if (t.Hwnd == IntPtr.Zero || t.Hidden || !t.Cell.Visible) continue;
            bool exists = W.IsWindow(t.Hwnd);
            bool alive = exists && W.IsWindowVisible(t.Hwnd);
            if (alive && W.IsIconic(t.Hwnd)) { W.ShowWindowAsync(t.Hwnd, 4); t.Rgn = ""; SyncWindows(); continue; }   // 최소화돼 있으면 칸 안에 다시 펼침
            if (alive) { t.Missing = 0; KeepInCell(t); continue; }
            if (exists)
            {
                // 앱이 창을 스스로 숨김(트레이로 숨기기 등) → 다시 칸에 보이게
                W.ShowWindowAsync(t.Hwnd, 8);
                t.Rgn = "";
                Log(t.App.Name + " 창이 숨겨져서 다시 보이게 함");
                continue;
            }
            if (++t.Missing < 2) continue;
            if (t.Guard != null) t.Guard.Hide();
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
        // RamSpirit가 최소화됐거나 칸이 접혀 있을 때(0x0)는 잴 수 없음 → 아무것도 기억하지 않음
        if (t.TooSmall || WindowState == FormWindowState.Minimized || !Visible || !t.Cell.Visible || dragColAt >= 0 || dragRowAt >= 0) return;
        if (t.Body.ClientSize.Width < 120 || t.Body.ClientSize.Height < 120) return;
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
            // 지금 칸 크기에 잘 들어가 있으면, 예전에 잘못 기억한 최소 크기는 버림
            if (t.MinW > ww + 10) t.MinW = 0;
            if (t.MinH > wh + 10) t.MinH = 0;
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
        // 데스크톱 앱은 웹 창으로 바꾸지 않음 → 앱이 들어가도록 칸을 넓히고, 그 크기를 기억해 선을 더 좁히지 못하게
        if (ww > r.Width + 10) t.MinW = Math.Max(t.MinW, ww);
        if (wh > r.Height + 10) t.MinH = Math.Max(t.MinH, wh);
        Log(t.App.Name + " 앱 최소 크기 " + ww + "x" + wh + " > 칸 " + r.Width + "x" + r.Height);
        if (solo >= 0) return;
        // 화면을 꽉 채운 상태에서는 그 줄·열을 넓혀 봄. 창을 줄여 둔 상태이거나 넓혀도 안 되면 → 창 밖으로 튀어나오지 않게 잠시 숨김
        bool canGrow = WindowState == FormWindowState.Maximized
            && (wh <= r.Height + 10 || wh + 60 <= area.ClientSize.Height * 0.7)
            && (ww <= r.Width + 10 || ww + 30 <= area.ClientSize.Width * 0.7);
        if (canGrow && t.Misfit <= 3) { AutoGrow(t, ww > r.Width + 10 ? ww : 0, wh > r.Height + 10 ? wh : 0); return; }
        HideTooSmall(t);
    }

    void HideTooSmall(Target t)
    {
        t.TooSmall = true;
        if (t.Guard != null) t.Guard.Hide();
        W.ShowWindowAsync(t.Hwnd, 0);
        t.Hidden = true;
        t.Wait.Text = "창이 작아서 " + t.App.Name + "을(를) 잠시 숨겼어요\n창을 키우면 다시 나타나요 · 여기를 누르면 크게 보기";
        t.Wait.Cursor = Cursors.Hand;
        t.Wait.Visible = true;
        t.Wait.BringToFront();
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
        if (rows > 1 && availH > 0 && needH > 0)
        {
            double f = Math.Min(0.7, (needH + 56.0) / availH), other = 0;
            for (int k = 0; k < rows; k++) if (k != r) other += rowW[k];
            if (f > rowW[r] / (other + rowW[r])) rowW[r] = f * other / (1 - f);
        }
        if (cols > 1 && availW > 0 && needW > 0)
        {
            double f = Math.Min(0.7, (needW + 24.0) / availW), other = 0;
            for (int k = 0; k < cols; k++) if (k != c) other += colW[k];
            if (f > colW[c] / (other + colW[c])) colW[c] = f * other / (1 - f);
        }
        SaveConfig();
        DoLayout();
        sendHint.Text = t.App.Name + " 앱이 " + (needH > 0 ? "높이 " + needH : "너비 " + needW) + "보다 작아지지 않아서 그 칸을 넓혔어요 · 칸 크기를 똑같이 쓰려면 「바꾸기」에서 「웹 창으로 열기」";
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
    bool keepOnClose;
    public void SafeRelease() { ReleaseAll(); }

    void ReleaseAll()
    {
        if (released_) return;
        released_ = true;
        // 기본: 붙어 있던 앱들도 같이 닫음 / Shift를 누른 채 닫으면 앱은 따로 창으로 남김
        bool keep = keepOnClose;
        int i = 0;
        foreach (Target t in AllTargets())
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
    // 다른 앱 창이 옮겨지기 시작하거나 위치가 바뀌는 순간을 알려 받음
    W.WinEventProc winEvt;
    IntPtr hookMove, hookLoc, hookFg;
    readonly List<Target> needFix = new List<Target>();
    Timer fixTimer;

    void HookWindows()
    {
        winEvt = OnWinEvent;
        hookMove = W.SetWinEventHook(0x000A, 0x000A, IntPtr.Zero, winEvt, 0, 0, 2);   // 끌어서 옮기기·크기 바꾸기 시작
        hookLoc = W.SetWinEventHook(0x800B, 0x800B, IntPtr.Zero, winEvt, 0, 0, 2);    // 위치·크기 바뀜
        hookFg = W.SetWinEventHook(0x0003, 0x0003, IntPtr.Zero, winEvt, 0, 0, 0);     // 맨 앞 창 바뀜
        fixTimer = new Timer();
        fixTimer.Interval = 120;
        fixTimer.Tick += delegate
        {
            fixTimer.Stop();
            var list = new List<Target>(needFix);
            needFix.Clear();
            foreach (Target t in list)
                if (t.Hwnd != IntPtr.Zero && !t.Hidden && t.Cell.Visible && dragColAt < 0 && dragRowAt < 0) KeepInCell(t);
        };
    }

    void OnWinEvent(IntPtr hook, uint ev, IntPtr h, int idObject, int idChild, uint thread, uint time)
    {
        if (idObject != 0 || h == IntPtr.Zero) return;
        if (ev == 0x0003) { RaiseGuards(); return; }
        Target t = null;
        foreach (Target x in T) if (x.Hwnd == h) { t = x; break; }
        if (t == null) return;
        if (ev == 0x000A)
        {
            // 칸 안의 앱을 끌어서 옮기거나 크기를 바꾸려 하면 바로 취소
            W.PostMessage(h, 0x001F, IntPtr.Zero, IntPtr.Zero);   // WM_CANCELMODE
            return;
        }
        if (!needFix.Contains(t)) needFix.Add(t);
        if (fixTimer != null && !fixTimer.Enabled) fixTimer.Start();
    }

    void OnFirstShown()
    {
        try { HookWindows(); } catch { }
        SetComposer(compOpen);
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
        startupSearch = true;
        StartSearch(new List<Target>(T));
        startupSearch = false;
        MakeShortcut();
        try { MakeTray(); } catch { }
        AppIcons.Changed = delegate
        {
            try
            {
                BeginInvoke((System.Windows.Forms.MethodInvoker)delegate
                {
                    foreach (Target x in AllTargets()) if (x.Dot != null) x.Dot.Invalidate();
                });
            }
            catch { }
        };
        try { if (startApps == null) startApps = StartApps(); AppIcons.Installed = startApps; } catch { }
        foreach (AppInfo a in AppInfo.Presets) AppIcons.Get(a);
        foreach (Target x in AllTargets()) AppIcons.Get(x.App);
        UpdateTabs();
        SetOnTop(onTop);
        try { JumpList.Make(); } catch { }
        if (Program.StartCmd == 1) CheckUpdate(true, 1500);
        CheckUpdateLater();
    }

    // 어떤 창을 어느 칸에 붙였는지 기록 (켤 때마다 새로 씀)
    bool logStarted;
    void Log(string line)
    {
        try
        {
            string f = Path.Combine(Path.GetDirectoryName(configFile), "attach.log");
            string text = DateTime.Now.ToString("HH:mm:ss") + "  " + line + "\r\n";
            if (!logStarted) { File.WriteAllText(f, "RamSpirit v" + Updater.Version + "\r\n" + text, Encoding.UTF8); logStarted = true; }
            else File.AppendAllText(f, text, Encoding.UTF8);
        }
        catch { }
    }

    // 같은 앱을 한 칸은 설치된 앱, 다른 칸은 웹 창으로 쓰면 → 앱 칸은 브라우저 창을 잡지 않게 표시
    void MarkSiblings()
    {
        List<Target> all = AllTargets();
        foreach (Target t in all)
        {
            bool sib = false;
            if (!t.App.IsInstalled && !t.App.PreferWeb && !t.Web)
                foreach (Target o in all)
                    if (o != t && o.App.PreferWeb && o.App.Name == t.App.Name) { sib = true; break; }
            t.NoBrowser = sib;
        }
    }

    // 그 앱 창이 이미 열려 있는지 (브라우저 창·스토어 앱 창은 빼고)
    bool AlreadyOpen(Target t)
    {
        MarkSiblings();
        string me = W.ProcessOf(Handle);
        foreach (KeyValuePair<long, string> kv in before)
        {
            IntPtr h = new IntPtr(kv.Key);
            if (h == Handle || used.Contains(kv.Key)) continue;
            string proc = W.ProcessOf(h);
            if (proc == me || proc == "chrome" || proc == "msedge") continue;
            if (!t.Matches(kv.Value, proc) || !t.IsAppWindow(kv.Value, proc) || !t.ProcOk(proc)) continue;
            string cls = W.ClassOf(h);
            if (cls == "ApplicationFrameWindow" || cls == "Windows.UI.Core.CoreWindow") continue;
            return true;
        }
        return false;
    }

    LoadCover cover;
    DateTime coverUntil, coverReadyAt;

    void ShowCover()
    {
        if (WindowState == FormWindowState.Minimized || !Visible) return;
        if (cover == null || cover.IsDisposed) cover = new LoadCover();
        Rectangle b = RectangleToScreen(ClientRectangle);
        cover.Bounds = b;
        coverUntil = DateTime.Now.AddSeconds(20);   // 무슨 일이 있어도 20초 뒤엔 걷음
        coverReadyAt = DateTime.MinValue;
        UpdateCover();
        if (!cover.Visible) cover.Show();
    }

    void UpdateCover()
    {
        if (cover == null || cover.IsDisposed || !cover.Visible && coverUntil < DateTime.Now) return;
        var parts = new List<string>();
        foreach (Target t in T) parts.Add(t.App.Name + (t.Hwnd != IntPtr.Zero ? " ✓" : " …"));
        string st = string.Join("   ", parts.ToArray());
        if (st != cover.Status) { cover.Status = st; cover.Invalidate(); }
    }

    void HideCover()
    {
        if (cover != null && !cover.IsDisposed && cover.Visible) cover.Hide();
    }

    // 모든 칸에 앱이 붙고 크기·제목 줄 정리까지 끝나면 가림막을 걷음
    void CheckCover()
    {
        if (cover == null || cover.IsDisposed || !cover.Visible) return;
        UpdateCover();
        if (DateTime.Now > coverUntil) { HideCover(); return; }
        bool all = true;
        foreach (Target t in T) if (t.Cell.Visible && (t.Hwnd == IntPtr.Zero || t.Pending)) all = false;
        if (!all) { coverReadyAt = DateTime.MinValue; return; }
        if (coverReadyAt == DateTime.MinValue) coverReadyAt = DateTime.Now.AddMilliseconds(1300);
        else if (DateTime.Now >= coverReadyAt) { SyncWindows(); HideCover(); }
    }

    void StartSearch(List<Target> list)
    {
        if (list.Count > 0) ShowCover();
        before = W.All();
        foreach (Target t in list)
        {
            t.WantNewChat = !startupSearch || startNewChat;
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
            if (over && t.Cell.Visible && solo < 0)
            {
                // 앱이 칸보다 크면 칸을 넓힘 (웹 창으로 바꾸지 않음)
                t.Misfit = 0;   // 넓힐지는 1초마다 다시 재 보고 정함 (KeepInCell)
            }
        }

        bool anyLeft = false;
        MarkSiblings();
        Dictionary<long, string> now = W.All();
        string me = Process.GetCurrentProcess().ProcessName.ToLowerInvariant();
        var procs = new Dictionary<long, string>();
        foreach (Target t in AllTargets())
        {
            if (t.Hwnd != IntPtr.Zero || t.Lent) continue;
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
                if (proc == me || !t.ProcOk(proc)) continue;
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
                Log(t.App.Name + " ← 창 \"" + kv.Value + "\" (" + proc + ", " + cls + ")");
                int old = W.Attach(h, Handle);
                if (!origStyles.ContainsKey(kv.Key)) origStyles[kv.Key] = old;   // 다시 붙여도 원래 모양 기억
                t.OldStyle = origStyles[kv.Key];
                t.Lost = false; t.Missing = 0; t.Misfit = 0;
                t.Hwnd = h;
                t.CapH = 0; t.AutoHide = false; t.Rgn = ""; t.RgnLost = 0; t.MinW = 0; t.MinH = 0; t.CapBtns = Rectangle.Empty;
                FindTitleBar(t, h, proc);
                if (!T.Contains(t)) { W.ShowWindowAsync(h, 0); t.Hidden = true; t.Wait.Visible = false; break; }   // 다른 탭의 칸이면 숨겨 둠
                t.Pending = true;
                t.CheckAt = DateTime.Now.AddMilliseconds(800);
                if (t.WantNewChat) t.NewChatAt = DateTime.Now.AddMilliseconds(2500);
                SyncWindows();
                break;
            }
        }

        CheckCover();
        bool anyPending = false;
        foreach (Target t in T) if (t.Pending || (t.WantNewChat && t.Hwnd != IntPtr.Zero)) anyPending = true;
        if ((!anyLeft && !anyPending) || sec > 40)
        {
            if (cover != null && cover.Visible && coverReadyAt != DateTime.MinValue && DateTime.Now < coverReadyAt) return;   // 가림막 걷을 때까지 조금 더
            HideCover();
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

    // 문제 확인용: 창 안 버튼·링크 이름들 (앞에서부터 n개)
    public static string ButtonNames(IntPtr h, int n)
    {
        lock (Gate)
        {
            var sb = new StringBuilder();
            try
            {
                AutomationElement root = AutomationElement.FromHandle(h);
                AutomationElementCollection all = root.FindAll(TreeScope.Descendants, new OrCondition(
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Hyperlink),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuItem)));
                int k = 0;
                foreach (AutomationElement e in all)
                {
                    string nm = e.Current.Name;
                    if (string.IsNullOrEmpty(nm) || nm.Length > 30) continue;
                    sb.Append(nm).Append(" | ");
                    if (++k >= n) break;
                }
            }
            catch { }
            return sb.ToString();
        }
    }

    // 앱 창 맨 위의 최소화·최대화·닫기 버튼을 모두 감싸는 사각형 (창 왼쪽 위 기준). 없으면 Empty
    public static Rectangle CaptionButtons(IntPtr h)
    {
        lock (Gate)
        {
            try
            {
                W.RECT wr;
                W.GetWindowRect(h, out wr);
                AutomationElement root = AutomationElement.FromHandle(h);
                var conds = new Condition[CapNames.Length];
                for (int i = 0; i < CapNames.Length; i++) conds[i] = new PropertyCondition(AutomationElement.NameProperty, CapNames[i]);
                AutomationElementCollection found = root.FindAll(TreeScope.Descendants, new AndCondition(
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button), new OrCondition(conds)));
                Rectangle u = Rectangle.Empty;
                foreach (AutomationElement e in found)
                {
                    System.Windows.Rect r = e.Current.BoundingRectangle;
                    if (r.IsEmpty || r.Top - wr.T > 60 || r.Width > 80) continue;          // 창 맨 위의 작은 버튼만
                    if (r.Right < wr.R - 260) continue;                                    // 오른쪽 끝 근처만
                    var rr = new Rectangle((int)r.X - wr.L, (int)r.Y - wr.T, (int)r.Width, (int)r.Height);
                    u = u.IsEmpty ? rr : Rectangle.Union(u, rr);
                }
                if (u.IsEmpty) return u;
                u.Inflate(2, 2);
                return u;
            }
            catch { return Rectangle.Empty; }
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
                        if (!e.TryGetCurrentPattern(InvokePattern.Pattern, out pat)) return false;
                        var inv = (InvokePattern)pat;
                        var th = new System.Threading.Thread(delegate () { try { inv.Invoke(); } catch { } });
                        th.IsBackground = true;
                        th.Start();
                        th.Join(2500);
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
    public const string Version = "2.2.0";
    public static string Ready;   // 이번에 받아 둔 새 버전 (다시 시작하면 적용)
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
            Ready = latest;
            return latest;
        }
        catch { return null; }
    }
}

public static class Program
{
    [STAThread]
    static void Main()
    {
        // 「다시 시작」으로 켜졌으면 이전 RamSpirit가 완전히 꺼질 때까지 기다림
        string[] args = Environment.GetCommandLineArgs();
        bool restart = false;
        int oldPid = 0;
        for (int i = 0; i + 1 < args.Length; i++)
        {
            if (args[i] != "--wait") continue;
            restart = true;
            int.TryParse(args[i + 1], out oldPid);
            try { Process.GetProcessById(oldPid).WaitForExit(8000); } catch { }
        }
        // 이미 켜져 있으면 하나만
        bool first;
        var mutex = new System.Threading.Mutex(true, "RamSpirit_SingleInstance", out first);
        if (!first && restart)
        {
            // 이전 RamSpirit가 아직 안 꺼졌으면 끝내고 넘겨받음
            try { Process.GetProcessById(oldPid).Kill(); } catch { }
            try { first = mutex.WaitOne(10000); } catch (System.Threading.AbandonedMutexException) { first = true; }
        }
        try { CmdMsg = W.RegisterWindowMessage("RamSpirit_Command"); } catch { }
        // 작업 표시줄 메뉴에서 온 명령 (--cmd update 등)
        int cmd = 0;
        for (int i = 0; i + 1 < args.Length; i++)
            if (args[i] == "--cmd") cmd = Array.IndexOf(new string[] { "", "update", "home", "newchat", "settings" }, args[i + 1]);
        if (cmd < 0) cmd = 0;
        StartCmd = cmd;
        if (!first && !restart)
        {
            Process me = Process.GetCurrentProcess();
            bool shown = false;
            foreach (Process p in Process.GetProcessesByName(me.ProcessName))
            {
                if (p.Id == me.Id) continue;
                IntPtr hw = IntPtr.Zero;
                try { hw = p.MainWindowHandle; } catch { }
                if (hw != IntPtr.Zero && W.IsWindowVisible(hw))
                {
                    if (W.IsIconic(hw)) W.ShowWindow(hw, 9);
                    W.SetForegroundWindow(hw);
                    if (cmd > 0) W.PostMessage(hw, CmdMsg, new IntPtr(cmd), IntPtr.Zero);   // 켜져 있는 RamSpirit가 그 일을 함
                    shown = true;
                }
            }
            if (shown) return;                       // 정말 켜져 있으면 그 창을 보여 줌
            foreach (Process p in Process.GetProcessesByName(me.ProcessName))
            {
                if (p.Id == me.Id) continue;
                try { p.Kill(); p.WaitForExit(3000); } catch { }   // 화면 없이 남은 RamSpirit는 끝냄
            }
            try { first = mutex.WaitOne(5000); } catch (System.Threading.AbandonedMutexException) { first = true; }
        }
        if (!first) return;
        Lock = mutex;
        if (Updater.EnsureInstalled()) { mutex.ReleaseMutex(); return; }
        Updater.CleanupOld();
        try { W.SetProcessDPIAware(); } catch { }
        Application.EnableVisualStyles();
        Th.LoadFrom(Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RamSpirit"), "apps.txt"));
        MainForm form;
        try { form = new MainForm(); }
        catch (Exception ex)
        {
            // 켜지다가 오류가 나면 조용히 꺼지지 않고 알려 줌 (오류 내용은 설정 폴더 error.log에 남김)
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RamSpirit");
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "error.log"), DateTime.Now + " v" + Updater.Version + "\r\n" + ex + "\r\n\r\n");
            }
            catch { }
            MessageBox.Show("RamSpirit를 켜는 중에 문제가 생겼어요.\n잠시 뒤 다시 켜 보시고, 계속 안 되면 알려 주세요.", "RamSpirit");
            ReleaseLock();
            return;
        }
        // 오류로 갑자기 꺼져도 붙어 있던 앱 창을 원래대로 돌려놓음
        Application.ThreadException += delegate { try { form.SafeRelease(); } catch { } };
        AppDomain.CurrentDomain.UnhandledException += delegate { try { form.SafeRelease(); } catch { } };
        Application.Run(form);
        GC.KeepAlive(mutex);
        ReleaseLock();
        Environment.Exit(0);   // 남아 있는 작업이 있어도 확실히 끝냄 (다시 시작이 막히지 않게)
    }

    static System.Threading.Mutex Lock;
    public static int StartCmd;
    public static uint CmdMsg;

    // 「다시 시작」 때 새 RamSpirit가 바로 켜질 수 있게 잠금을 먼저 풂
    public static void ReleaseLock()
    {
        try { if (Lock != null) { Lock.ReleaseMutex(); Lock = null; } } catch { }
    }
}
