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
  public static void Shot(string path) {
    var b = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
    using (var bmp = new Bitmap(b.Width, b.Height)) { using (var g = Graphics.FromImage(bmp)) g.CopyFromScreen(b.X, b.Y, 0, 0, b.Size); bmp.Save(path); }
  }
}
'@
$out = "C:\t\out"; New-Item -ItemType Directory -Force $out | Out-Null
$log = "$out\report.txt"
$script:fails = @()
function Snap($name, [bool]$expectClean = $true, [int]$expectVis = -1) {
  $s = 0; $n = 0
  $r = [WT]::Report([ref]$s, [ref]$n)
  "===== $name =====" | Out-File $log -Append -Encoding utf8
  $r | Out-File $log -Append -Encoding utf8
  try { [WT]::Shot("$out\$name.png") } catch { "찍기 실패: $_" | Out-File $log -Append -Encoding utf8 }
  if ($expectClean -and $s -gt 0) { $script:fails += "$name : 밖에 뜬 창 $s (제목줄 없음 $n)" }
  if ($expectVis -ge 0 -and [WT]::VisIn -ne $expectVis) { $script:fails += "$name : 칸 안에 보이는 창이 " + [WT]::VisIn + "개 (기대 $expectVis)" }
}
function Start-Ram { $env:RAMSPIRIT_TEST = "1"; Start-Process "C:\t\RamSpirit.exe" }

function Grok($c) { Set-Content -Path "C:\t\cmd_GrokBot.txt" -Value $c }
Start-Ram; Start-Sleep 3; Snap "00-켜는중" $false; Start-Sleep 22; Snap "01-켜기" $true 2
[WT]::Cmd(101); Start-Sleep 12; Snap "02-탭2" $true 2
[WT]::Cmd(102); Start-Sleep 15; Snap "03-탭3" $true 4
[WT]::Cmd(100); Start-Sleep 10; Snap "04-탭1로"
[WT]::Cmd(3);   Start-Sleep 15; Snap "05-모두새대화"
# 탭에 마우스를 올리면 위쪽에 수정·삭제가 뜨는지, 수정을 누르면 창이 뜨는지
[WT]::SetCursorPos(650, 45); Start-Sleep 2; Snap "H1-탭에마우스"
$pp = [WT]::FindPop(); if ($pp -eq $null) { $script:fails += "H1 : 탭 위 수정·삭제 단추가 안 뜸" } else { "팝업 위치 $($pp[0]),$($pp[1])" | Out-File $log -Append -Encoding utf8; [WT]::Click($pp[0] + 18, $pp[1] + 15); Start-Sleep 2; Snap "H2-수정창"; Add-Type -AssemblyName System.Windows.Forms; [System.Windows.Forms.SendKeys]::SendWait("{ESC}"); Start-Sleep 1 }
[WT]::SetCursorPos(500, 500); Start-Sleep 1
[WT]::Cmd(110); Start-Sleep 4;  Snap "06-내리기"
if ((Get-Content $log -Raw) -match "(?s)===== 06-내리기 =====.*?\[칸안\][^\n]*보임=True") { $script:fails += "06-내리기 : 내렸는데 칸 안 앱 창이 보임" }
[WT]::Cmd(111); Start-Sleep 6;  Snap "07-올리기"
# 앱이 스스로 창을 숨겼다가 다른 탭에 있을 때 다시 띄움
[WT]::Cmd(102); Start-Sleep 10; Grok "hide"; Start-Sleep 5
[WT]::Cmd(100); Start-Sleep 4; Grok "show"; Start-Sleep 5; Snap "G1-다른탭에서앱이다시뜸" $true 2
[WT]::Cmd(102); Start-Sleep 8; Snap "G2-돌아오면제자리" $true 4
# 앱이 창을 없앴다가(앱이 닫혔어요) 다른 탭에 있을 때 새 창을 만듦
Grok "close"; Start-Sleep 5
[WT]::Cmd(100); Start-Sleep 4; Grok "recreate"; Start-Sleep 8; Snap "G3-다른탭에서앱창새로생김" $true 2
[WT]::Cmd(102); Start-Sleep 10; Snap "G4-돌아오면새창이칸에" $true 4
# 지금 탭에서 앱이 숨겼다가 다시 띄움
Grok "hide"; Start-Sleep 5; Grok "show"; Start-Sleep 6; Snap "G5-지금탭에서다시뜸" $true 4
[WT]::Cmd(100); Start-Sleep 8
# 앱이 띄운 대화상자가 화면 밖에 숨어 앱이 먹통처럼 보일 때 → 칸으로 꺼내야 함
[WT]::Cmd(102); Start-Sleep 8; Grok "dialog"; Start-Sleep 6
$dp = [WT]::FindTitle("다른 이름으로 저장"); "대화상자 위치 $dp" | Out-File $log -Append -Encoding utf8
if ($dp -eq $null -or $dp.StartsWith("-")) { $script:fails += "S1 : 숨은 대화상자를 꺼내지 못함 ($dp)" }
Snap "S1-숨은대화상자꺼냄"
Grok "closedialog"; Start-Sleep 3
# 앱이 멈췄을 때 → 칸에 안내
Grok "hang"; Start-Sleep 16; Snap "S2-앱멈춤안내" $true 3
Start-Sleep 14; [WT]::Cmd(100); Start-Sleep 6
[WT]::Cmd(112); Start-Sleep 25; Snap "08-업데이트처럼다시켜기"
[WT]::Cmd(101); Start-Sleep 12; Snap "08b-다시켠뒤탭2"
[WT]::Cmd(100); Start-Sleep 8
[WT]::Cmd(112); Start-Sleep 25; Snap "08c-두번째다시켜기"
[WT]::Cmd(102); Start-Sleep 15; Snap "08d-다시켠뒤탭3"
[WT]::Cmd(100); Start-Sleep 8
[WT]::Cmd(112); Start-Sleep 1; [WT]::Cmd(113); Start-Sleep 1; [WT]::Cmd(113); Start-Sleep 25; Snap "09-다시켜며바로누르기"
[WT]::Cmd(101); Start-Sleep 12; [WT]::Cmd(100); Start-Sleep 10; Snap "10-탭왕복"
[WT]::CloseMain(); Start-Sleep 10; Snap "11-끄기" $true
Start-Ram; Start-Sleep 25; Snap "12-다시켜기"
[WT]::CloseMain(); Start-Sleep 10; Snap "13-끄기2" $true

$ie = "$env:LOCALAPPDATA\RamSpirit\RamSpirit.exe"
"===== 설치된 파일 버전 =====" | Out-File $log -Append -Encoding utf8
if (Test-Path $ie) { ([System.Diagnostics.FileVersionInfo]::GetVersionInfo($ie).FileVersion) | Out-File $log -Append -Encoding utf8 } else { $script:fails += "설치된 RamSpirit.exe가 없음" }
if (Test-Path "$env:APPDATA\RamSpirit\error.log") { "===== error.log =====" | Out-File $log -Append -Encoding utf8; Get-Content "$env:APPDATA\RamSpirit\error.log" | Out-File $log -Append -Encoding utf8 }
if (Test-Path "$env:APPDATA\RamSpirit\attach.log") { "===== attach.log =====" | Out-File $log -Append -Encoding utf8; Get-Content "$env:APPDATA\RamSpirit\attach.log" | Out-File $log -Append -Encoding utf8 }
"===== 결과 =====" | Out-File $log -Append -Encoding utf8
if ($script:fails.Count -eq 0) { "통과" | Out-File $log -Append -Encoding utf8 } else { $script:fails | Out-File $log -Append -Encoding utf8 }
Get-Content $log
