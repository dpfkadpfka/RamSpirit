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
  public static List<IntPtr> Wins(string proc) { var l = new List<IntPtr>(); EnumWindows(delegate (IntPtr h, IntPtr x) { int pid; GetWindowThreadProcessId(h, out pid); string pn=""; try { pn = Process.GetProcessById(pid).ProcessName.ToLower(); } catch {} var c=new StringBuilder(256); GetClassName(h,c,256); var t=new StringBuilder(256); GetWindowText(h,t,256); if (pn==proc && t.Length>0 && GetWindow(h,4)==IntPtr.Zero || pn==proc && t.Length>0 && c.ToString()=="Chrome_WidgetWin_1") l.Add(h); return true; }, IntPtr.Zero); return l; }
  public static string Desc(IntPtr h) { var t=new StringBuilder(256); GetWindowText(h,t,256); RECT r; GetWindowRect(h,out r); return h+" '"+t+"' vis="+IsWindowVisible(h)+" en="+IsWindowEnabled(h)+" owner="+GetWindow(h,4)+" style="+GetWindowLong(h,-16).ToString("X8")+" ex="+GetWindowLong(h,-20).ToString("X8")+" "+r.L+","+r.T+" "+(r.R-r.L)+"x"+(r.B-r.T); }
  public static void Click(int x, int y) { SetCursorPos(x, y); System.Threading.Thread.Sleep(200); mouse_event(2,0,0,0,UIntPtr.Zero); System.Threading.Thread.Sleep(60); mouse_event(4,0,0,0,UIntPtr.Zero); }
  public static Bitmap Grab(Rectangle r) { var b = new Bitmap(r.Width, r.Height); using (var g = Graphics.FromImage(b)) g.CopyFromScreen(r.Location, Point.Empty, r.Size); return b; }
  public static int Diff(Bitmap a, Bitmap b) { int n = 0; for (int y = 0; y < a.Height; y += 2) for (int x = 0; x < a.Width; x += 2) { Color p = a.GetPixel(x, y), q = b.GetPixel(x, y); if (Math.Abs(p.R - q.R) + Math.Abs(p.G - q.G) + Math.Abs(p.B - q.B) > 40) n++; } return n; }
}
"@
$global:PROC = "claude"
function ClaudeWins { return [CW]::Wins($global:PROC) }
function Vis { return (ClaudeWins | Where-Object { [CW]::IsWindowVisible($_) } | Select-Object -First 1) }
# 눌러서 화면이 바뀌는지로 「먹통」인지 확인 (진짜 마우스로 누름)
function Probe($tag) {
  $h = Vis
  if (-not $h) { L "$tag : 보이는 창 없음"; return "없음" }
  $el = [System.Windows.Automation.AutomationElement]::FromHandle($h)
  $all = $el.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
  $target = $null; $blank = $null
  foreach ($e in $all) { $c = $e.Current; if (-not $target -and $c.Name -eq "Menu" -and $c.ControlType.ProgrammaticName -match "Button") { $target = $e }; if (-not $target -and $c.ControlType.ProgrammaticName -match "Edit" -and $c.BoundingRectangle.Width -gt 50) { $target = $e } }
  if (-not $target) { L "$tag : 누를 곳 못 찾음"; return "단추없음" }
  $r = $target.Current.BoundingRectangle
  $isEdit = $target.Current.ControlType.ProgrammaticName -match "Edit"
  if ($isEdit) { [System.Windows.Forms.SendKeys]::SendWait("{ESC}"); $wr = $el.Current.BoundingRectangle; [CW]::Click([int]($wr.X + $wr.Width / 2), [int]($wr.Y + 40)); Start-Sleep 1 }
  $cx = [int]($r.X + [Math]::Min(40, $r.Width / 2)); $cy = [int]($r.Y + $r.Height / 2)
  $area = New-Object System.Drawing.Rectangle ([int]$r.X - 4), ([int]$r.Y - 4), ([int][Math]::Min(320, $r.Width + 8)), 300
  $before = [CW]::Grab($area)
  [CW]::Click($cx, $cy); Start-Sleep 2
  $d = [CW]::Diff($before, [CW]::Grab($area))
  Shot "$tag-누른뒤"
  [System.Windows.Forms.SendKeys]::SendWait("{ESC}"); Start-Sleep 1
  if (-not $isEdit) { $d2 = [CW]::Diff($before, [CW]::Grab($area)); if ($d2 -gt 50) { [CW]::Click($cx, $cy); Start-Sleep 1 } }
  L "$tag : '$($target.Current.Name)' ($cx,$cy) 누르니 바뀐 점 $d"
  if ($d -gt 20) { return "반응" } else { return "먹통" }
}
function DumpWins($tag) { L "--- 창 $tag"; foreach ($h in (ClaudeWins)) { L ("  " + [CW]::Desc($h)) } }
function Uia($h, $max) {
  $el = [System.Windows.Automation.AutomationElement]::FromHandle($h)
  $all = $el.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
  $i = 0
  foreach ($e in $all) { if ($i -ge $max) { break }; $c = $e.Current; if ($c.Name -or $c.ControlType.ProgrammaticName -match 'Edit|Button|Hyperlink') { L ("  [" + $c.ControlType.ProgrammaticName + "] '" + $c.Name + "' " + $c.BoundingRectangle + " focusable=" + $c.IsKeyboardFocusable); $i++ } }
}

function Chk($tag) { $x = Probe $tag; $script:res += "$tag=$x"; DumpWins $tag; Get-Content "$env:APPDATA\RamSpirit\attach.log" -Encoding UTF8 -Tail 3 | ForEach-Object { L "    log: $_" } }
function CloseApp1 { $h = Vis; if ($h) { [WT]::PostMessage($h, 0x10, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null }; Start-Sleep 5 }

function Scenario($name, $entry, $appid, $proc) {
  $global:PROC = $proc
  L "######## $name"
  Start-Process "shell:AppsFolder\$appid"; Start-Sleep 25
  DumpWins "처음"
  New-Item -ItemType Directory -Force "$env:APPDATA\RamSpirit" | Out-Null
  $cfg = "#tab`tReal`r`n$entry`r`nExample 웹`tExample`thttps://example.com/`t#0ea5e9`t0`tweb`r`n#ratio`t1,1`t1`r`n#tab`tTwo`r`n$entry`r`nExample 웹`tExample`thttps://example.com/`t#0ea5e9`t0`tweb`r`n#ratio`t1,1`t1`r`n#active`tReal`r`n#composer`t0`r`n#ver`t9.9.9`r`n#closeapps`t1`r`n#startnew`t0`r`n"
  [IO.File]::WriteAllText("$env:APPDATA\RamSpirit\apps.txt", $cfg, (New-Object Text.UTF8Encoding $true))
  $env:RAMSPIRIT_TEST = "1"; Start-Process C:\t\RamSpirit.exe; Start-Sleep 30
  Chk "$name-P1-붙인뒤"
  CloseApp1; Shot "$name-c1-닫음"; [WT]::Cmd(113); Start-Sleep 12; Chk "$name-P2-닫고다시열기"
  [WT]::Cmd(101); Start-Sleep 8; [WT]::Cmd(100); Start-Sleep 8; Chk "$name-P3-탭왕복"
  CloseApp1; [WT]::Cmd(101); Start-Sleep 8; [WT]::Cmd(113); Start-Sleep 12; Chk "$name-P4-다른탭에서다시열기"
  [WT]::Cmd(100); Start-Sleep 8; Chk "$name-P4b-원래탭으로"
  [WT]::Cmd(110); Start-Sleep 4; [WT]::Cmd(111); Start-Sleep 6; Chk "$name-P5-내렸다올리기"
  CloseApp1; [WT]::Cmd(112); Start-Sleep 35; [WT]::Cmd(113); Start-Sleep 12; Chk "$name-P6-닫은채다시켜기"
  [WT]::Cmd(112); Start-Sleep 35; Chk "$name-P7-다시켜기"
  CloseApp1; [WT]::Cmd(113); Start-Sleep 12; Chk "$name-P8-다시켠뒤닫고열기"
  $n = 0; $s = 0; [void][WT]::Report([ref]$s, [ref]$n); L "$name 밖에 떠 있는 창 $s"; $script:res += "$name-밖에뜬창=$s"
  [WT]::CloseMain(); Start-Sleep 8
  Get-Process RamSpirit -ErrorAction SilentlyContinue | Stop-Process -Force
}
$res = @()
$cl = (Get-StartApps | Where-Object Name -eq "Claude").AppID
Scenario "Claude" "Claude`tClaude`thttps://claude.ai/new`t#d97757`t1" $cl "claude"
$g = Get-StartApps | Where-Object Name -match "ChatGPT" | Select-Object -First 1
L ("ChatGPT 앱: " + $g.Name + " " + $g.AppID)
Start-Process "shell:AppsFolder\$($g.AppID)"; Start-Sleep 25
$gp = (Get-Process | Where-Object { $_.MainWindowTitle -match "ChatGPT" } | Select-Object -First 1).ProcessName
L "ChatGPT 프로세스: $gp"
Scenario "ChatGPT" "ChatGPT`tChatGPT`tapp:$($g.AppID)`t#10a37f`t1" $g.AppID $gp.ToLower()
L ("== 결과: " + ($res -join " / "))
