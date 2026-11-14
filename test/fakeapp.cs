// 자동 점검용 가짜 데스크톱 앱 (예: Grok Bot). C:\t\cmd_<이름>.txt 에 hide / show / close / recreate 를 쓰면 그대로 함
using System; using System.Drawing; using System.IO; using System.Windows.Forms;
static class FakeApp {
  static Form f; static string title; static Rectangle last;
  static Form Make() {
    var x = new Form(); x.Text = title; x.ClientSize = new Size(800, 600); x.MinimumSize = new Size(380, 300); x.BackColor = Color.FromArgb(235, 245, 255);
    var l = new Label(); l.Text = "가짜 " + title + " 앱"; l.Font = new Font("Segoe UI", 20f); l.AutoSize = true; l.Location = new Point(30, 30); x.Controls.Add(l);
    if (last.Width > 0) { x.StartPosition = FormStartPosition.Manual; x.Bounds = last; }
    x.FormClosing += delegate (object o, FormClosingEventArgs e) { if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; x.Hide(); } };
    return x;
  }
  [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool EnableWindow(IntPtr h, bool on);
  [STAThread] static void Main(string[] a) {
    title = a.Length > 0 ? a[0] : "Grok Bot";
    string cmdFile = @"C:\t\cmd_" + title.Replace(" ", "") + ".txt";
    var host = new ApplicationContext();
    f = Make(); f.Show();
    var t = new Timer(); t.Interval = 400;
    t.Tick += delegate {
      if (!File.Exists(cmdFile)) return;
      string c = ""; try { c = File.ReadAllText(cmdFile).Trim(); File.Delete(cmdFile); } catch { return; }
      if (c == "hide") f.Hide();
      else if (c == "show") f.Show();
      else if (c == "close") { last = f.Bounds; f.Dispose(); f = null; }
      else if (c == "dialog") { var d = new Form(); d.Text = "Save As Test"; d.StartPosition = FormStartPosition.Manual; d.Location = new Point(-3000, -3000); d.Size = new Size(400, 250); f.BeginInvoke((MethodInvoker)delegate { d.ShowDialog(f); }); }
      else if (c == "closedialog") { foreach (Form o in Application.OpenForms) if (o != f && o.Text == "Save As Test") { o.Close(); break; } }
      else if (c == "spawn") { last = Rectangle.Empty; var old = f; f = Make(); f.Text = title; f.Show(); old.Text = title; }
      else if (c == "lock") { EnableWindow(f.Handle, false); }
      else if (c == "hang") { System.Threading.Thread.Sleep(25000); }
      else if (c == "recreate") { if (f != null) { last = f.Bounds; f.Dispose(); } f = Make(); f.Show(); }
    };
    t.Start();
    Application.Run(host);
  }
}
