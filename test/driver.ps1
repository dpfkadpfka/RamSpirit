# RamSpirit 자동 점검: 시나리오마다 창 상태를 기록하고 화면을 찍음
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
  public static IntPtr Main() { foreach (var p in Process.GetProcessesByName("RamSpirit")) if (p.MainWindowHandle != IntPtr.Zero) return p.MainWindowHandle; return IntPtr.Zero; }
  public static void Cmd(int c) { IntPtr h = Main(); if (h != IntPtr.Zero) PostMessage(h, RegisterWindowMessage("RamSpirit_Command"), new IntPtr(c), IntPtr.Zero); }
  public static void CloseMain() { IntPtr h = Main(); if (h != IntPtr.Zero) PostMessage(h, 0x0010, IntPtr.Zero, IntPtr.Zero); }
  public static string Report(out int stray, out int naked) {
    stray = 0; naked = 0; int s2 = 0, n2 = 0;
    IntPtr main = Main();
    var sb = new StringBuilder();
    sb.AppendLine("RamSpirit main=" + main + (main != IntPtr.Zero && IsIconic(main) ? " (내려 둠)" : ""));
    EnumWindows(delegate (IntPtr h, IntPtr l) {
      int pid; GetWindowThreadProcessId(h, out pid);
      string pn = ""; try { pn = Process.GetProcessById(pid).ProcessName.ToLower(); } catch { }
      if (pn != "chrome" && pn != "claude" && pn != "ramspirit") return true;
      bool vis = IsWindowVisible(h);
      var t = new StringBuilder(256); GetWindowText(h, t, 256);
      if (t.Length == 0) return true;
      IntPtr owner = GetWindow(h, 4);
      int st = GetWindowLong(h, -16); bool caption = (st & 0x00C00000) == 0x00C00000;
      RECT r; GetWindowRect(h, out r);
      bool attached = main != IntPtr.Zero && owner == main;
      string kind = pn == "ramspirit" ? "ram" : attached ? "칸안" : "밖";
      if (pn != "ramspirit" && vis && !attached && (r.R - r.L) > 50) { s2++; if (!caption) n2++; }
      if (vis || attached) sb.AppendLine(string.Format("  [{0}] {1} '{2}' 보임={3} 제목줄={4} 위치={5},{6} {7}x{8}", kind, pn, t, vis, caption, r.L, r.T, r.R - r.L, r.B - r.T));
      return true; }, IntPtr.Zero);
    stray = s2; naked = n2;
    sb.AppendLine("  => 밖에 떠 있는 앱 창 " + s2 + "개, 그중 제목줄 없는 창 " + n2 + "개");
    return sb.ToString();
  }
  public static void Shot(string path) {
    var b = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
    using (var bmp = new Bitmap(b.Width, b.Height)) { using (var g = Graphics.FromImage(bmp)) g.CopyFromScreen(b.X, b.Y, 0, 0, b.Size); bmp.Save(path); }
  }
}
'@
$out = "C:\t\out"; New-Item -ItemType Directory -Force $out | Out-Null
$log = "$out\report.txt"
$script:fails = @()
function Snap($name, [bool]$expectClean = $true) {
  $s = 0; $n = 0
  $r = [WT]::Report([ref]$s, [ref]$n)
  "===== $name =====" | Out-File $log -Append -Encoding utf8
  $r | Out-File $log -Append -Encoding utf8
  try { [WT]::Shot("$out\$name.png") } catch { "찍기 실패: $_" | Out-File $log -Append -Encoding utf8 }
  if ($expectClean -and $s -gt 0) { $script:fails += "$name : 밖에 뜬 창 $s (제목줄 없음 $n)" }
}
function Start-Ram { $env:RAMSPIRIT_TEST = "1"; Start-Process "C:\t\RamSpirit.exe" }

Start-Ram; Start-Sleep 25; Snap "01-켜기"
[WT]::Cmd(101); Start-Sleep 12; Snap "02-탭2"
[WT]::Cmd(102); Start-Sleep 15; Snap "03-탭3"
[WT]::Cmd(100); Start-Sleep 10; Snap "04-탭1로"
[WT]::Cmd(3);   Start-Sleep 15; Snap "05-모두새대화"
[WT]::Cmd(110); Start-Sleep 4;  Snap "06-내리기"
if ((Get-Content $log -Raw) -match "(?s)===== 06-내리기 =====.*?\[칸안\][^\n]*보임=True") { $script:fails += "06-내리기 : 내렸는데 칸 안 앱 창이 보임" }
[WT]::Cmd(111); Start-Sleep 6;  Snap "07-올리기"
[WT]::Cmd(112); Start-Sleep 25; Snap "08-업데이트처럼다시켜기"
[WT]::Cmd(112); Start-Sleep 1; [WT]::Cmd(113); Start-Sleep 1; [WT]::Cmd(113); Start-Sleep 25; Snap "09-다시켜며바로누르기"
[WT]::Cmd(101); Start-Sleep 12; [WT]::Cmd(100); Start-Sleep 10; Snap "10-탭왕복"
[WT]::CloseMain(); Start-Sleep 10; Snap "11-끄기" $true
Start-Ram; Start-Sleep 25; Snap "12-다시켜기"
[WT]::CloseMain(); Start-Sleep 10; Snap "13-끄기2" $true

if (Test-Path "$env:APPDATA\RamSpirit\error.log") { "===== error.log =====" | Out-File $log -Append -Encoding utf8; Get-Content "$env:APPDATA\RamSpirit\error.log" | Out-File $log -Append -Encoding utf8 }
if (Test-Path "$env:APPDATA\RamSpirit\attach.log") { "===== attach.log =====" | Out-File $log -Append -Encoding utf8; Get-Content "$env:APPDATA\RamSpirit\attach.log" | Out-File $log -Append -Encoding utf8 }
"===== 결과 =====" | Out-File $log -Append -Encoding utf8
if ($script:fails.Count -eq 0) { "통과" | Out-File $log -Append -Encoding utf8 } else { $script:fails | Out-File $log -Append -Encoding utf8 }
Get-Content $log
