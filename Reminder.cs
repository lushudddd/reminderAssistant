using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Serialization;
using Microsoft.Win32;

public class Reminder {
    public string Name, Message, Accent;
    public int Minutes;
    public bool Enabled, Custom;
    [XmlIgnore] public DateTime Due;
}
public class Settings {
    public bool Enabled, Initialized;
    public List<Reminder> Items = new List<Reminder>();
}
class Assistant : Form {
    static readonly string FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tixing", "settings.xml");
    static readonly Color Ink = ColorTranslator.FromHtml("#192437");
    static readonly Color Muted = ColorTranslator.FromHtml("#53637A");
    static readonly Color Background = ColorTranslator.FromHtml("#F4F4F5");
    static readonly Color Blue = ColorTranslator.FromHtml("#315EEB");
    readonly FlowLayoutPanel rows = new FlowLayoutPanel();
    readonly CheckBox master = new CheckBox();
    readonly Button preferences = new Button();
    readonly Panel header = new Panel { Dock=DockStyle.Top, BackColor=ColorTranslator.FromHtml("#25272C") };
    readonly Label heading = new Label { Text="提醒助手", ForeColor=Color.White, TextAlign=ContentAlignment.MiddleCenter };
    readonly Label status = new Label { ForeColor=ColorTranslator.FromHtml("#BBC0CC"), TextAlign=ContentAlignment.MiddleCenter };
    Font largeHeading, smallHeading;
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
    protected override void OnHandleCreated(EventArgs e) {
        base.OnHandleCreated(e);
        int dark=1;
        if (DwmSetWindowAttribute(Handle,20,ref dark,4)!=0) DwmSetWindowAttribute(Handle,19,ref dark,4);
    }
    readonly NotifyIcon tray = new NotifyIcon();
    readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
    readonly Settings settings;
    bool quitting;
    Popup popup;

    [STAThread] static void Main() {
        bool first;
        using (var instance = new Mutex(true, "Local\\TixingReminderAssistant", out first)) {
            if (!first) return;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Assistant());
        }
    }
    static Reminder BuiltIn(string name, string message, string accent, int minutes) {
        return new Reminder { Name=name, Message=message, Accent=accent, Minutes=minutes, Enabled=true };
    }
    static Settings LoadSettings() {
        try {
            using (var stream = File.OpenRead(FilePath))
                return (Settings)new XmlSerializer(typeof(Settings)).Deserialize(stream);
        } catch (IOException) {} catch (InvalidOperationException) {}
        var value = new Settings();
        value.Items.Add(BuiltIn("喝水", "多喝水能加快血液循环，稀释血液中的代谢废物（如尿酸、尿素等），并通过尿液和汗液将其排出体外，从而减轻肝脏和肾脏的代谢负担。充足饮水还能缓解久坐带来的血液粘稠，预防下肢静脉曲张。同时，喝水能短暂增加血容量，帮助大脑保持清醒，缓解精神萎靡哦！", "#176BC2", 59));
        value.Items.Add(BuiltIn("眼睛休息", "盯屏太久，眼睛可能干涩、视线模糊，甚至头痛。放下屏幕，看一会儿远处。", "#087D6B", 20));
        value.Items.Add(BuiltIn("站立", "长期久坐与心血管疾病和 2 型糖尿病风险升高有关。现在起身走几步。", "#A25512", 53));
        value.Items.Add(BuiltIn("活动肩颈", "保持一个姿势太久，脖子可能僵硬到转头都疼。放松肩膀，轻轻活动一下。", "#7243B5", 67));
        value.Items.Add(BuiltIn("提肛", "哥们，提会儿肛吧！不仅能预防痔疮，还能增强勃起硬度，提高对射精的控制力，有效延长性生活时间。", "#B33B69", 239));
        return value;
    }
    void Save() {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
        string next = FilePath + ".tmp";
        using (var stream = File.Create(next)) new XmlSerializer(typeof(Settings)).Serialize(stream, settings);
        if (File.Exists(FilePath)) File.Replace(next, FilePath, null);
        else File.Move(next, FilePath);
    }
    public Assistant() {
        settings = LoadSettings();
        Text = "提醒助手";
        Icon = SystemIcons.Information;
        Font = new Font("Microsoft YaHei UI", 10);
        ForeColor = Ink;
        BackColor = Background;
        ClientSize = new Size(680, 595);
        MinimumSize = new Size(480, 470);
        StartPosition = FormStartPosition.CenterScreen;

        BackColor = header.BackColor;
        largeHeading = new Font(Font.FontFamily, 32, FontStyle.Bold);
        smallHeading = new Font(Font.FontFamily, 23, FontStyle.Bold);
        heading.Font = largeHeading;
        master.Appearance = Appearance.Button;
        master.FlatStyle = FlatStyle.Flat;
        master.FlatAppearance.BorderSize = 0;
        master.ForeColor = Color.White;
        master.TextAlign = ContentAlignment.MiddleCenter;
        master.Font = new Font(Font.FontFamily, 13, FontStyle.Bold);
        master.Cursor = Cursors.Hand;
        master.Checked = settings.Enabled;
        preferences.Text = "设置";
        preferences.ForeColor = Color.White;
        preferences.BackColor = ColorTranslator.FromHtml("#434956");
        preferences.FlatStyle = FlatStyle.Flat;
        preferences.FlatAppearance.BorderSize = 0;
        preferences.Click += delegate { ShowPreferences(); };
        header.Controls.AddRange(new Control[] { heading, master, status, preferences });
        header.Resize += delegate { LayoutHeader(); };
        ClientSizeChanged += delegate { LayoutHeader(); };
        master.CheckedChanged += delegate {
            settings.Enabled = master.Checked;
            if (settings.Enabled && !settings.Initialized) {
                settings.Initialized = true;
                foreach (var item in settings.Items) if (!item.Custom) item.Enabled = true;
            }
            if (!settings.Enabled && popup != null) popup.Close();
            ResetSchedule();
            Save();
            RefreshRows();
        };
        rows.Dock = DockStyle.Fill;
        rows.FlowDirection = FlowDirection.TopDown;
        rows.WrapContents = false;
        rows.AutoScroll = true;
        rows.Padding = new Padding(24, 15, 24, 16);
        rows.Resize += delegate { ResizeRows(); };
        Controls.Add(rows);
        Controls.Add(header);

        var menu = new ContextMenuStrip();
        menu.Items.Add("打开提醒助手", null, delegate { ShowMain(); });
        var enabled = menu.Items.Add("暂停提醒", null, delegate { master.Checked = !master.Checked; });
        var startup = new ToolStripMenuItem("登录 Windows 时启动") { CheckOnClick = true };
        startup.Click += delegate { SetStartup(startup.Checked); };
        menu.Items.Add(startup);
        menu.Items.Add("退出", null, delegate { quitting = true; Close(); });
        menu.Opening += delegate {
            enabled.Text = settings.Enabled ? "暂停提醒" : "开启提醒";
            startup.Checked = StartupEnabled();
        };
        tray.Icon = Icon;
        tray.Text = Text;
        tray.ContextMenuStrip = menu;
        tray.Visible = true;
        tray.DoubleClick += delegate { ShowMain(); };
        FormClosing += delegate(object sender, FormClosingEventArgs e) {
            if (!quitting && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); return; }
            tray.Dispose();
        };
        timer.Interval = 1000;
        timer.Tick += delegate { CheckDue(); };
        ResetSchedule();
        RefreshRows();
        timer.Start();
        var args = Environment.GetCommandLineArgs();
        if (Array.IndexOf(args, "--tray") >= 0) { master.Checked = true; Shown += delegate { Hide(); }; }
    }
    static bool StartupEnabled() {
        try {
            using (var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
                return key!=null && key.GetValue("Tixing")!=null;
        } catch (System.Security.SecurityException) { return false; }
          catch (UnauthorizedAccessException) { return false; }
    }
    void ShowPreferences() {
        using (var dialog=new Form { Text="设置", Font=Font, ClientSize=new Size(370,130), StartPosition=FormStartPosition.CenterParent, FormBorderStyle=FormBorderStyle.FixedDialog, MaximizeBox=false, MinimizeBox=false }) {
            var setting=new CheckBox { Text="登录 Windows 时自动启动", AutoSize=true, Location=new Point(20,22) };
            setting.Checked=StartupEnabled();
            dialog.Controls.Add(setting);
            dialog.Controls.Add(new Label { Text="自动启动时，在托盘开启提醒助手。", AutoSize=true, Location=new Point(40,57) });
            setting.CheckedChanged+=delegate { SetStartup(setting.Checked); };
            dialog.ShowDialog(this);
        }
    }
    void SetStartup(bool enabled) {
        using (var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")) {
            if (enabled) key.SetValue("Tixing", "\"" + Application.ExecutablePath + "\" --tray");
            else key.DeleteValue("Tixing", false);
        }
    }
    void ShowMain() { Show(); WindowState = FormWindowState.Normal; Activate(); }
    void ResetSchedule() {
        var now = DateTime.UtcNow;
        foreach (var item in settings.Items) item.Due = now.AddMinutes(item.Minutes);
    }
    void ResizeRows() {
        foreach (Control row in rows.Controls) row.Width = Math.Max(100, rows.ClientSize.Width-69);
    }
    Label Section(string title) {
        return new Label { Text=title, ForeColor=ColorTranslator.FromHtml("#BBC0CC"), Height=24, TextAlign=ContentAlignment.MiddleLeft, Margin=new Padding(0, 0, 0, 4) };
    }
    void LayoutHeader() {
        bool active = settings.Enabled;
        header.Height = active ? 128 : ClientSize.Height;
        preferences.SetBounds(Math.Max(0,header.ClientSize.Width-76),14,60,30);
        int center = header.ClientSize.Width / 2;
        int top = active ? 6 : Math.Max(20,(header.Height-196)/2);
        int buttonWidth=active?130:206;
        int buttonHeight=active?36:48;
        heading.Font=active?smallHeading:largeHeading;
        heading.SetBounds(0,top,header.ClientSize.Width,active?48:65);
        master.SetBounds(center-buttonWidth/2,top+(active?52:96),buttonWidth,buttonHeight);
        status.SetBounds(0,master.Bottom+(active?3:13),header.ClientSize.Width,29);
        var previous = master.Region;
        using (var shape=new GraphicsPath()) {
            int diameter=buttonHeight;
            shape.AddArc(0,0,diameter,diameter,90,180);
            shape.AddArc(buttonWidth-diameter-1,0,diameter,diameter,270,180);
            shape.CloseFigure();
            master.Region=new Region(shape);
        }
        if (previous!=null) previous.Dispose();
    }
    void RefreshRows() {
        master.Text = settings.Enabled ? "暂停" : "开启";
        status.Text = settings.Enabled ? "当前：运行中" : "当前：未启动";
        LayoutHeader();
        master.BackColor = settings.Enabled ? ColorTranslator.FromHtml("#434956") : ColorTranslator.FromHtml("#216FCE");
        rows.SuspendLayout();
        while (rows.Controls.Count > 0) rows.Controls[0].Dispose();
        rows.Visible = settings.Enabled;
        if (settings.Enabled) {
            rows.Controls.Add(Section("内置提醒"));
            foreach (var item in settings.Items) if (!item.Custom) AddRow(item);
            rows.Controls.Add(Section("自定义提醒"));
            foreach (var item in settings.Items) if (item.Custom) AddRow(item);
            var add = MakeButton("＋  添加提醒", Blue);
            add.Height = 37;
            add.Click += delegate { Edit(null); };
            rows.Controls.Add(add);
        }
        ResizeRows();
        rows.ResumeLayout();
    }
    static Button MakeButton(string text, Color color) {
        var button = new Button { Text=text, BackColor=color, ForeColor=Color.White, FlatStyle=FlatStyle.Flat, Cursor=Cursors.Hand };
        button.FlatAppearance.BorderSize = 0;
        return button;
    }
    void AddRow(Reminder item) {
        var accent = ColorTranslator.FromHtml(item.Accent);
        var card = new Panel { Height=55, BackColor=Color.White, Margin=new Padding(0,0,0,7) };
        card.Controls.Add(new Panel { BackColor=accent, Dock=DockStyle.Left, Width=5 });
        var open = new Button { Text=item.Name, TextAlign=ContentAlignment.MiddleLeft, Location=new Point(15, 9), Size=new Size(210,36), ForeColor=accent, FlatStyle=FlatStyle.Flat, Cursor=Cursors.Hand, AutoEllipsis=true, BackColor=Color.White };
        open.FlatAppearance.BorderSize = 0;
        open.Click += delegate { Edit(item); };
        var interval = new Label { Text="每 " + item.Minutes + " 分钟", ForeColor=Muted, TextAlign=ContentAlignment.MiddleRight, Size=new Size(116,34) };
        var toggle = new CheckBox { Appearance=Appearance.Button, Checked=item.Enabled, Text=item.Enabled?"开启":"关闭", TextAlign=ContentAlignment.MiddleCenter, ForeColor=Color.White, BackColor=item.Enabled?accent:Muted, Size=new Size(61,31), FlatStyle=FlatStyle.Flat, Cursor=Cursors.Hand, AccessibleName=item.Name+"开关" };
        toggle.FlatAppearance.BorderSize = 0;
        toggle.CheckedChanged += delegate {
            item.Enabled=toggle.Checked;
            item.Due=DateTime.UtcNow.AddMinutes(item.Minutes);
            toggle.Text=item.Enabled?"开启":"关闭";
            toggle.BackColor=item.Enabled?accent:Muted;
            Save();
        };
        card.Resize += delegate {
            toggle.Location=new Point(card.Width-73,12);
            interval.Location=new Point(card.Width-201,10);
            open.Width=Math.Max(70, card.Width-221);
        };
        card.Controls.AddRange(new Control[] { open, interval, toggle });
        rows.Controls.Add(card);
    }
    void Edit(Reminder item) {
        bool custom = item == null || item.Custom;
        using (var dialog = new Form { Text=item==null?"添加提醒":item.Name, Font=Font, ForeColor=Ink, BackColor=Background, ClientSize=new Size(405,custom?295:150), FormBorderStyle=FormBorderStyle.FixedDialog, StartPosition=FormStartPosition.CenterParent, MaximizeBox=false, MinimizeBox=false }) {
            int y = 19;
            var name = new TextBox { Text=item==null?"":item.Name, Width=360 };
            var message = new TextBox { Text=item==null?"":item.Message, Width=360, Height=59, Multiline=true, ScrollBars=ScrollBars.Vertical };
            if (custom) { Field(dialog, "名称", name, ref y); Field(dialog, "提示文案", message, ref y); }
            var minutes = new NumericUpDown { Minimum=1, Maximum=10080, Value=item==null?60:item.Minutes, Width=110 };
            Field(dialog, "提醒间隔（分钟）", minutes, ref y);
            var save = MakeButton("保存", Blue);
            save.SetBounds(282,y+5,100,35);
            dialog.Controls.Add(save);
            dialog.AcceptButton = save;
            save.Click += delegate {
                if (custom && (string.IsNullOrWhiteSpace(name.Text) || string.IsNullOrWhiteSpace(message.Text))) {
                    MessageBox.Show(dialog,"请填写名称和提示文案。","提醒助手"); return;
                }
                if (item==null) {
                    item=new Reminder { Custom=true, Accent="#465B88" };
                    settings.Items.Add(item);
                }
                if (custom) { item.Name=name.Text.Trim(); item.Message=message.Text.Trim(); }
                item.Minutes=(int)minutes.Value;
                item.Due=DateTime.UtcNow.AddMinutes(item.Minutes);
                Save();
                dialog.DialogResult=DialogResult.OK;
            };
            if (dialog.ShowDialog(this)==DialogResult.OK) RefreshRows();
        }
    }
    static void Field(Form dialog, string title, Control field, ref int y) {
        dialog.Controls.Add(new Label { Text=title, AutoSize=true, Location=new Point(22,y) });
        field.Location = new Point(22,y+25);
        dialog.Controls.Add(field);
        y+=field.Height+36;
    }
    void CheckDue() {
        if (!settings.Enabled || popup!=null) return;
        var now=DateTime.UtcNow;
        var due=settings.Items.FindAll(item => item.Enabled && item.Due <= now);
        if (due.Count==0) return;
        foreach (var item in due) item.Due=now.AddMinutes(item.Minutes);
        var featured=due.Find(item => !item.Custom && item.Name=="提肛") ?? due[0];
        string title=featured.Name;
        string message=featured.Message;
        foreach (var item in due) {
            if (item != featured) message+="\n\n"+item.Name+"\n"+item.Message;
        }
        popup=new Popup(title,message,
            delegate { foreach (var item in due) if (item.Enabled) item.Due=DateTime.UtcNow.AddMinutes(5); },
            delegate { ShowMain(); MessageBox.Show(this,string.Join("\n\n",due.ConvertAll(item => item.Name+"\n"+item.Message).ToArray()),title); });
        popup.FormClosed+=delegate { popup=null; };
        popup.Show();
    }
    class Popup : Form {
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams {
            get { var value=base.CreateParams; value.ExStyle|=0x08000000; return value; }
        }
        public Popup(string title,string message,Action delay,Action details) {
            FormBorderStyle=FormBorderStyle.None;
            ShowInTaskbar=false;
            TopMost=true;
            BackColor=Ink;
            Font=new Font("Microsoft YaHei UI",10);

            var work=Screen.FromPoint(Cursor.Position).WorkingArea;
            var body = new Label { Text=message, ForeColor=Color.WhiteSmoke, AutoSize=true, MaximumSize=new Size(345,0) };
            int bodyHeight=Math.Min(body.PreferredHeight+12,Math.Max(70,work.Height-115));
            var bodyPanel=new Panel { AutoScroll=true };
            bodyPanel.SetBounds(15,43,370,bodyHeight);
            bodyPanel.Controls.Add(body);
            int buttonTop=bodyPanel.Bottom+8;
            ClientSize=new Size(400,buttonTop+43);
            StartPosition=FormStartPosition.Manual;
            Location=new Point(work.Right-Width-18,work.Bottom-Height-18);
            Controls.Add(new Label { Text=title, ForeColor=Color.White, Font=new Font(Font.FontFamily,14,FontStyle.Bold), Location=new Point(15,10), Size=new Size(345,28), AutoEllipsis=true });
            Controls.Add(bodyPanel);
            var close=MakeButton("×",Ink);
            close.SetBounds(353,7,32,28);
            close.Click+=delegate { Close(); };
            var later=MakeButton("稍后 5 分钟",Blue);
            later.SetBounds(15,buttonTop,117,30);
            later.Click+=delegate { delay(); Close(); };
            var open=MakeButton("查看详情",Ink);
            open.SetBounds(140,buttonTop,100,30);
            open.Click+=delegate { Close(); details(); };
            Controls.AddRange(new Control[] { close,later,open });
            var expiry=new System.Windows.Forms.Timer { Interval=11000 };
            expiry.Tick+=delegate { Close(); };
            FormClosed+=delegate { expiry.Dispose(); };
            expiry.Start();
        }
    }
}
