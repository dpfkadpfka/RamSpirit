# 진짜 Claude 데스크톱 앱으로 「닫았다 다시 열면 먹통」 점검
$ErrorActionPreference = 'Continue'
Add-Type -ReferencedAssemblies System.Drawing,System.Windows.Forms -TypeDefinition @'
using System; using System.Text; using System.Collections.Generic; using System.Runtime.InteropServices; using System.Diagnostics; using System.Drawing;
public static class WT {
  delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc p, IntPtr l);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] static extern int GetWindowThreadProcessId(IntPtr h, out int pid);
  [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr h, int c);
  [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int i);
  [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
  [DllImport("user32.dll")] public static extern uint RegisterWindowMessage(string s);
  [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
  public static IntPtr MainWnd() { foreach (var p in Process.GetProcessesByName("RamSpirit")) if (p.MainWindowHandle != IntPtr.Zero) return p.MainWindowHandle; return IntPtr.Zero; }
  public static void Cmd(int c) { IntPtr h = MainWnd(); if (h != IntPtr.Zero) PostMessage(h, RegisterWindowMessage("RamSpirit_Command"), new IntPtr(c), IntPtr.Zero); }
  public static void CloseMain() { IntPtr h = MainWnd(); if (h != IntPtr.Zero) PostMessage(h, 0x0010, IntPtr.Zero, IntPtr.Zero); }
  public static int VisIn;
  public static string Report(out int stray, out int naked) {
    stray = 0; naked = 0; int s2 = 0, n2 = 0; int vin = 0;
    IntPtr main = MainWnd();
    var sb = new StringBuilder();
    sb.AppendLine("RamSpirit main=" + main + (main != IntPtr.Zero && IsIconic(main) ? " (내려 둠)" : ""));
    EnumWindows(delegate (IntPtr h, IntPtr l) {
      int pid; GetWindowThreadProcessId(h, out pid);
      string pn = ""; try { pn = Process.GetProcessById(pid).ProcessName.ToLower(); } catch { }
      if (pn != "chrome" && pn != "claude" && pn != "ramspirit" && pn != "grok bot") return true;
      bool vis = IsWindowVisible(h);
      var t = new StringBuilder(256); GetWindowText(h, t, 256);
      if (t.Length == 0) return true;
      IntPtr owner = GetWindow(h, 4);
      int st = GetWindowLong(h, -16); bool caption = (st & 0x00C00000) == 0x00C00000;
      RECT r; GetWindowRect(h, out r);
      bool attached = main != IntPtr.Zero && owner == main;
      if (attached && vis) vin++;
      string kind = pn == "ramspirit" ? "ram" : attached ? "칸안" : "밖";
      if (pn != "ramspirit" && vis && !attached && (r.R - r.L) > 50) { s2++; if (!caption) n2++; }
      if (!vis && !attached && pn != "ramspirit" && (r.R - r.L) > 300) sb.AppendLine(string.Format("  [숨은밖] {0} '{1}' {2}x{3}", pn, t, r.R - r.L, r.B - r.T));
      if (vis || attached) sb.AppendLine(string.Format("  [{0}] {1} '{2}' 보임={3} 제목줄={4} 위치={5},{6} {7}x{8}", kind, pn, t, vis, caption, r.L, r.T, r.R - r.L, r.B - r.T));
      return true; }, IntPtr.Zero);
    stray = s2; naked = n2; VisIn = vin;
    sb.AppendLine("  => 밖에 떠 있는 앱 창 " + s2 + "개, 그중 제목줄 없는 창 " + n2 + "개, 칸 안에 보이는 창 " + vin + "개");
    return sb.ToString();
  }
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] static extern void mouse_event(uint f, int x, int y, uint d, UIntPtr e);
  public static void Click(int x, int y) { SetCursorPos(x, y); System.Threading.Thread.Sleep(150); mouse_event(2, 0, 0, 0, UIntPtr.Zero); mouse_event(4, 0, 0, 0, UIntPtr.Zero); }
  public static int[] FindPop() {
    int[] res = null;
    EnumWindows(delegate (IntPtr h, IntPtr l) {
      int pid; GetWindowThreadProcessId(h, out pid);
      string pn = ""; try { pn = Process.GetProcessById(pid).ProcessName.ToLower(); } catch { }
      if (pn != "ramspirit" || !IsWindowVisible(h)) return true;
      RECT r; GetWindowRect(h, out r);
      if (r.R - r.L == 68 && r.B - r.T == 30) { res = new int[] { r.L, r.T }; return false; }
      return true; }, IntPtr.Zero);
    return res;
  }
  public static string FindTitle(string want) {
    string res = null;
    EnumWindows(delegate (IntPtr h, IntPtr l) {
      if (!IsWindowVisible(h)) return true;
      var t = new StringBuilder(256); GetWindowText(h, t, 256);
      if (t.ToString() != want) return true;
      RECT r; GetWindowRect(h, out r); res = r.L + "," + r.T; return false; }, IntPtr.Zero);
    return res;
  }
  [DllImport("user32.dll")] public static extern bool IsWindowEnabled(IntPtr h);
  public static string EnabledTitle(string want) {
    string res = "없음";
    EnumWindows(delegate (IntPtr h, IntPtr l) {
      if (!IsWindowVisible(h)) return true;
      var t = new StringBuilder(256); GetWindowText(h, t, 256);
      if (t.ToString() != want) return true;
      res = IsWindowEnabled(h) ? "풀림" : "잠김"; return false; }, IntPtr.Zero);
    return res;
  }
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  public static bool FocusTitle(string want) {
    bool ok = false;
    EnumWindows(delegate (IntPtr h, IntPtr l) {
      if (!IsWindowVisible(h)) return true;
      var t = new StringBuilder(256); GetWindowText(h, t, 256);
      if (t.ToString() != want) return true;
      ok = SetForegroundWindow(h); return false; }, IntPtr.Zero);
    return ok;
  }
  public static void CloseApp(string title, string proc) {
    EnumWindows(delegate (IntPtr h, IntPtr l) {
      int pid; GetWindowThreadProcessId(h, out pid);
      string pn = ""; try { pn = Process.GetProcessById(pid).ProcessName.ToLower(); } catch { }
      var t = new StringBuilder(256); GetWindowText(h, t, 256);
      if (pn == proc && t.ToString() == title && IsWindowVisible(h)) { PostMessage(h, 0x0010, IntPtr.Zero, IntPtr.Zero); return false; }
      return true; }, IntPtr.Zero);
  }
  public static void Shot(string path) {
    var b = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
    using (var bmp = new Bitmap(b.Width, b.Height)) { using (var g = Graphics.FromImage(bmp)) g.CopyFromScreen(b.X, b.Y, 0, 0, b.Size); bmp.Save(path); }
  }
}
'@

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms
$out = "C:\t\out"; New-Item -ItemType Directory -Force $out | Out-Null
$log = "$out\report.txt"
function L($s) { $s | Out-File $log -Append -Encoding utf8; Write-Host $s }
function Shot($n) { try { [WT]::Shot("$out\$n.png") } catch { L "찍기 실패 $_" } }
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System; using System.Text; using System.Collections.Generic; using System.Runtime.InteropServices; using System.Diagnostics; using System.Drawing;
public static class CW {
  delegate bool EP(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] static extern bool EnumWindows(EP p, IntPtr l);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool IsWindowEnabled(IntPtr h);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] static extern int GetWindowThreadProcessId(IntPtr h, out int pid);
  [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr h, int c);
  [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int i);
  [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(int f, int x, int y, int d, UIntPtr e);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  public static List<IntPtr> Wins(string proc) { var l = new List<IntPtr>(); EnumWindows(delegate (IntPtr h, IntPtr x) { int pid; GetWindowThreadProcessId(h, out pid); string pn=""; try { pn = Process.GetProcessById(pid).ProcessName.ToLower(); } catch {} var c=new StringBuilder(256); GetClassName(h,c,256); var t=new StringBuilder(256); GetWindowText(h,t,256); if (pn==proc && c.ToString()=="Chrome_WidgetWin_1" && t.Length>0) l.Add(h); return true; }, IntPtr.Zero); return l; }
  public static string Desc(IntPtr h) { var t=new StringBuilder(256); GetWindowText(h,t,256); RECT r; GetWindowRect(h,out r); return h+" '"+t+"' vis="+IsWindowVisible(h)+" en="+IsWindowEnabled(h)+" owner="+GetWindow(h,4)+" style="+GetWindowLong(h,-16).ToString("X8")+" ex="+GetWindowLong(h,-20).ToString("X8")+" "+r.L+","+r.T+" "+(r.R-r.L)+"x"+(r.B-r.T); }
  public static void Click(int x, int y) { SetCursorPos(x, y); System.Threading.Thread.Sleep(200); mouse_event(2,0,0,0,UIntPtr.Zero); System.Threading.Thread.Sleep(60); mouse_event(4,0,0,0,UIntPtr.Zero); }
  public static Bitmap Grab(Rectangle r) { var b = new Bitmap(r.Width, r.Height); using (var g = Graphics.FromImage(b)) g.CopyFromScreen(r.Location, Point.Empty, r.Size); return b; }
  public static int Diff(Bitmap a, Bitmap b) { int n = 0; for (int y = 0; y < a.Height; y += 2) for (int x = 0; x < a.Width; x += 2) { Color p = a.GetPixel(x, y), q = b.GetPixel(x, y); if (Math.Abs(p.R - q.R) + Math.Abs(p.G - q.G) + Math.Abs(p.B - q.B) > 40) n++; } return n; }
}
"@
function ClaudeWins { return [CW]::Wins("claude") }
# 눌러서 화면이 바뀌는지로 「먹통」인지 확인: UIA로 메뉴 단추를 찾아 진짜 마우스로 누름
function Probe($tag) {
  $h = (ClaudeWins | Where-Object { [CW]::IsWindowVisible($_) } | Select-Object -First 1)
  if (-not $h) { L "$tag : 보이는 Claude 창 없음"; return "없음" }
  $el = [System.Windows.Automation.AutomationElement]::FromHandle($h)
  $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, "Menu")
  $btn = $el.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
  if (-not $btn) { L "$tag : Menu 단추 못 찾음"; return "단추없음" }
  $r = $btn.Current.BoundingRectangle
  $cx = [int]($r.X + $r.Width / 2); $cy = [int]($r.Y + $r.Height / 2)
  $area = New-Object System.Drawing.Rectangle ([int]$r.X), ([int]$r.Y), 300, 300
  $before = [CW]::Grab($area)
  [CW]::Click($cx, $cy); Start-Sleep 2
  $after = [CW]::Grab($area)
  $d = [CW]::Diff($before, $after)
  Shot "$tag-누른뒤"
  [System.Windows.Forms.SendKeys]::SendWait("{ESC}"); Start-Sleep 1
  $d2 = [CW]::Diff($before, [CW]::Grab($area))
  if ($d2 -gt 50) { [CW]::Click($cx, $cy); Start-Sleep 1 }
  L "$tag : Menu ($cx,$cy) 누르니 바뀐 점 $d"
  if ($d -gt 50) { return "반응" } else { return "먹통" }
}
function DumpWins($tag) { L "--- 창 $tag"; foreach ($h in (ClaudeWins)) { L ("  " + [CW]::Desc($h)) } }
function Uia($h, $max) {
  $el = [System.Windows.Automation.AutomationElement]::FromHandle($h)
  $all = $el.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
  $i = 0
  foreach ($e in $all) { if ($i -ge $max) { break }; $c = $e.Current; if ($c.Name -or $c.ControlType.ProgrammaticName -match 'Edit|Button|Hyperlink') { L ("  [" + $c.ControlType.ProgrammaticName + "] '" + $c.Name + "' " + $c.BoundingRectangle + " focusable=" + $c.IsKeyboardFocusable); $i++ } }
}


Add-Type -TypeDefinition @"
using System; using System.Runtime.InteropServices;
public static class EX {
  [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int i);
  [DllImport("user32.dll")] public static extern int SetWindowLong(IntPtr h, int i, int v);
  [DllImport("user32.dll", EntryPoint="SetWindowLongPtrW")] public static extern IntPtr SetWindowLongPtr(IntPtr h, int i, IntPtr v);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int w, int hh, uint f);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
  [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr h, int c);
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
  [DllImport("user32.dll")] public static extern int SetWindowRgn(IntPtr h, IntPtr r, bool redraw);
  [DllImport("gdi32.dll")] public static extern IntPtr CreateRoundRectRgn(int l, int t, int r, int b, int w, int h);
  public static int Orig;
  public static void Style(IntPtr h) { int old = GetWindowLong(h, -16); int s = (old & ~(0x00C00000 | 0x00040000 | 0x00080000 | 0x00020000 | 0x00010000 | 0x40000000)) | unchecked((int)0x80000000); SetWindowLong(h, -16, s); SetWindowPos(h, IntPtr.Zero, 0,0,0,0, 0x1|0x2|0x4|0x10|0x20); }
  public static void Owner(IntPtr h, IntPtr o) { SetWindowLongPtr(h, -8, o); }
  public static void Place(IntPtr h) { SetWindowPos(h, IntPtr.Zero, 20, 120, 600, 560, 0x4|0x10|0x4000|0x0400); }
  public static void Clip(IntPtr h) { IntPtr r = CreateRoundRectRgn(0, 0, 601, 561, 16, 16); SetWindowRgn(h, r, true); }
  public static void Restore(IntPtr h) { SetWindowRgn(h, IntPtr.Zero, true); SetWindowLongPtr(h, -8, IntPtr.Zero); SetWindowLong(h, -16, Orig & ~0x10000000 | (GetWindowLong(h, -16) & 0x10000000)); SetWindowPos(h, IntPtr.Zero, 20, 120, 600, 560, 0x4|0x10|0x20); }
}
"@
Add-Type -AssemblyName System.Windows.Forms
$own = New-Object System.Windows.Forms.Form; $own.Text = "Owner"; $own.StartPosition = "Manual"; $own.Location = New-Object System.Drawing.Point(700, 500); $own.Size = New-Object System.Drawing.Size(300, 200); $own.Show(); [System.Windows.Forms.Application]::DoEvents()
$O = $own.Handle
$cl = Get-StartApps | Where-Object Name -eq "Claude"
function Launch { Start-Process "shell:AppsFolder\$($cl.AppID)" }
function W { return (ClaudeWins | Select-Object -First 1) }
function Pump($s) { $end = (Get-Date).AddSeconds($s); while ((Get-Date) -lt $end) { [System.Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 100 } }
Launch; Pump 25
$h = W; [EX]::Orig = [EX]::GetWindowLong($h, -16); L ("처음 " + [CW]::Desc($h))
[EX]::SetWindowPos($h, [IntPtr]::Zero, 20, 120, 600, 560, 0x4) | Out-Null; Pump 2
L ("기본: " + (Probe "X0-기본"))

function Variant($name, [scriptblock]$attach, [scriptblock]$reopen) {
  $h = W
  & $attach $h; Pump 2
  $a = Probe "$name-붙임"
  [EX]::PostMessage($h, 0x10, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null; Pump 4
  $vis = [CW]::IsWindowVisible($h)
  & $reopen $h; Pump 6
  $h2 = W
  $b = Probe "$name-다시엶"
  L ("$name : 붙임=$a / 닫은뒤보임=$vis / 다시연뒤=$b / 같은창=" + ($h -eq $h2) + " " + [CW]::Desc($h2))
  [EX]::Restore($h2); [EX]::ShowWindow($h2, 5) | Out-Null; Pump 3
  L ("   되돌린 뒤: " + (Probe "$name-되돌림"))
}
$full = { param($h) [EX]::Style($h); [EX]::Owner($h, $O); [EX]::Place($h); [EX]::Clip($h) }
# 탭 바꿀 때처럼 밖에서 숨겼다가 보이기
function V2($name, [scriptblock]$act) {
  $h = W
  & $full $h; Pump 2
  $a = Probe "$name-붙임"
  & $act $h
  $h2 = W
  $b = Probe "$name-뒤"
  L ("$name : 붙임=$a / 뒤=$b / 같은창=" + ($h -eq $h2) + " " + [CW]::Desc($h2))
  # 다음 실험을 위해 앱이 스스로 숨기고 다시 띄우게 해서 정상으로
  [EX]::Restore($h2); [EX]::PostMessage($h2, 0x10, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null; Pump 3; Launch; Pump 6
  L ("   되돌린 뒤: " + (Probe "$name-되돌림"))
}
V2 "I-밖에서숨기고보이기" { param($h) [EX]::ShowWindow($h, 0) | Out-Null; Pump 3; [EX]::ShowWindow($h, 5) | Out-Null; Pump 3 }
V2 "I2-SWP로숨기고보이기" { param($h) [EX]::SetWindowPos($h, [IntPtr]::Zero, 0,0,0,0, 0x97) | Out-Null; Pump 3; [EX]::SetWindowPos($h, [IntPtr]::Zero, 0,0,0,0, 0x57) | Out-Null; Pump 3 }
V2 "I3-최소화했다복원" { param($h) [EX]::ShowWindow($h, 6) | Out-Null; Pump 3; [EX]::ShowWindow($h, 9) | Out-Null; Pump 3 }
V2 "I4-화면밖으로옮겼다되돌림" { param($h) [EX]::SetWindowPos($h, [IntPtr]::Zero, -32000, -32000, 0, 0, 0x4015) | Out-Null; Pump 3; [EX]::Place($h); Pump 3 }
V2 "I5-숨기고보이기_10초" { param($h) [EX]::ShowWindow($h, 0) | Out-Null; Pump 10; [EX]::ShowWindow($h, 5) | Out-Null; Pump 3 }
V2 "K-닫힘후밖에서보이고_앱다시켜기" { param($h) [EX]::PostMessage($h, 0x10, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null; Pump 4; [EX]::ShowWindow($h, 5) | Out-Null; Pump 2; Launch; Pump 6 }
V2 "L-닫힘후밖에서보이고_숨기고_앱다시켜기" { param($h) [EX]::PostMessage($h, 0x10, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null; Pump 4; [EX]::ShowWindow($h, 5) | Out-Null; Pump 2; [EX]::ShowWindow($h, 0) | Out-Null; Pump 1; Launch; Pump 6 }
V2 "M-닫힘후_앱다시켜기만" { param($h) [EX]::PostMessage($h, 0x10, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null; Pump 4; Launch; Pump 6 }
