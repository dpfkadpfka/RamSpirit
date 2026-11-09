// 자동 점검용 가짜 「Claude 데스크톱 앱」: 창 하나뿐, X를 누르면 꺼지지 않고 숨음, 두 번째로 켜면 원래 창을 다시 보여 줌
using System; using System.Drawing; using System.Windows.Forms; using System.Runtime.InteropServices;
static class FakeClaude {
  [DllImport("user32.dll")] static extern IntPtr FindWindow(string c, string t);
  [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int n);
  [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
  [STAThread] static void Main() {
    bool first; var m = new System.Threading.Mutex(true, "FakeClaudeOne", out first);
    if (!first) { IntPtr h = FindWindow(null, "Claude"); if (h != IntPtr.Zero) { ShowWindow(h, 9); SetForegroundWindow(h); } return; }
    var f = new Form(); f.Text = "Claude"; f.ClientSize = new Size(900, 700); f.MinimumSize = new Size(380, 300);
    f.BackColor = Color.FromArgb(250, 249, 245);
    var l = new Label(); l.Text = "가짜 Claude 데스크톱 앱"; l.Font = new Font("Segoe UI", 20f); l.ForeColor = Color.FromArgb(217, 119, 87); l.AutoSize = true; l.Location = new Point(30, 30);
    f.Controls.Add(l);
    var b = new Button(); b.Text = "새 채팅"; b.Location = new Point(30, 100); b.AutoSize = true; f.Controls.Add(b);
    f.FormClosing += delegate (object o, FormClosingEventArgs e) { if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; f.Hide(); } };
    Application.Run(f); GC.KeepAlive(m);
  }
}
