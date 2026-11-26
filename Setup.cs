using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;

class Setup : Form {
    const string App = "Tixing.exe";
    const string Uninstaller = "卸载提醒助手.exe";
    const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Tixing";
    static readonly string Shortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "提醒助手.lnk");
    readonly TextBox path = new TextBox();
    readonly Button install = new Button();
    readonly CheckBox startup = new CheckBox { Text="登录 Windows 时自动启动提醒助手", AutoSize=true };

    [STAThread] static void Main(string[] args) {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
#if UNINSTALLER
        Remove();
#else
        if (args.Length == 1 && args[0] == "--uninstall") {
            Remove();
            return;
        }
        Application.Run(new Setup());
#endif
    }
    Setup() {
        Text="安装提醒助手";
        Font=new Font("Microsoft YaHei UI",10);
        StartPosition=FormStartPosition.CenterScreen;
        FormBorderStyle=FormBorderStyle.FixedDialog;
        MaximizeBox=false;
        MinimizeBox=false;
        BackColor=Color.FromArgb(242,245,250);
        ClientSize=new Size(520,242);
        var heading=new Label { Text="选择安装位置", Font=new Font(Font.FontFamily,20,FontStyle.Bold), AutoSize=true, Location=new Point(22,20) };
        var description=new Label { Text="可浏览选择 C 盘、D 盘或其他磁盘，也可以输入完整路径。", AutoSize=true, Location=new Point(24,69) };
        path.SetBounds(24,103,378,30);
        using (var key=Registry.CurrentUser.OpenSubKey(RegistryPath))
            path.Text=key==null ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","Tixing") : (string)key.GetValue("InstallLocation");
        var browse=new Button { Text="浏览…" };
        browse.SetBounds(414,101,83,30);
        browse.Click+=delegate {
            using (var picker=new FolderBrowserDialog { Description="选择提醒助手安装文件夹", ShowNewFolderButton=true }) {
                if (Directory.Exists(path.Text)) picker.SelectedPath=path.Text;
                if (picker.ShowDialog(this)==DialogResult.OK) path.Text=picker.SelectedPath;
            }
        };
        install.Text="安装";
        install.BackColor=Color.FromArgb(49,94,235);
        install.ForeColor=Color.White;
        install.FlatStyle=FlatStyle.Flat;
        install.FlatAppearance.BorderSize=0;
        startup.Location=new Point(24,151);
        startup.Checked=StartupEnabled();
        install.SetBounds(391,183,106,36);
        install.Click+=delegate { Install(); };
        Controls.AddRange(new Control[] { heading,description,path,browse,startup,install });
    }
    static bool StartupEnabled() {
        try {
            using (var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
                return key!=null && key.GetValue("Tixing")!=null;
        } catch (System.Security.SecurityException) { return false; }
          catch (UnauthorizedAccessException) { return false; }
    }
    void Install() {
        try {
            var input=path.Text.Trim();
            if (input.Length < 3 || input[1] != ':' || input[2] != '\\')
                throw new ArgumentException("请输入包含盘符的完整路径。");
            string target=Path.GetFullPath(input);
            if (target==Path.GetPathRoot(target)) target=Path.Combine(target,"Tixing");
            string exe=Path.Combine(target,App);
            string setup=Path.Combine(target,Uninstaller);
            string installed=null;
            string previousUninstall=null;
            using (var existing=Registry.CurrentUser.OpenSubKey(RegistryPath))
                if (existing!=null) {
                    installed=existing.GetValue("InstallLocation") as string;
                    previousUninstall=existing.GetValue("UninstallString") as string;
                }
            if (installed!=null && !string.Equals(installed.TrimEnd('\\'),target.TrimEnd('\\'),StringComparison.OrdinalIgnoreCase))
                throw new IOException("请先卸载已有版本，或选择原来的安装位置。");
            bool update=installed!=null;
            if (!update && (File.Exists(exe) || File.Exists(setup)))
                throw new IOException("此位置已存在同名文件，请选择其他文件夹。");
            Directory.CreateDirectory(target);
            using (var payload=Assembly.GetExecutingAssembly().GetManifestResourceStream(App))
            using (var file=new FileStream(exe,update?FileMode.Create:FileMode.CreateNew))
                payload.CopyTo(file);
            using (var payload=Assembly.GetExecutingAssembly().GetManifestResourceStream("Uninstaller.exe"))
            using (var file=new FileStream(setup,update?FileMode.Create:FileMode.CreateNew))
                payload.CopyTo(file);
            var legacy=Path.Combine(target,"TixingSetup.exe");
            if (string.Equals(previousUninstall,Quote(legacy)+" --uninstall",StringComparison.OrdinalIgnoreCase) && !string.Equals(legacy,Application.ExecutablePath,StringComparison.OrdinalIgnoreCase)) File.Delete(legacy);
            var shellType=Type.GetTypeFromProgID("WScript.Shell");
            dynamic shell=Activator.CreateInstance(shellType);
            dynamic shortcut=shell.CreateShortcut(Shortcut);
            shortcut.TargetPath=exe;
            shortcut.WorkingDirectory=target;
            shortcut.Save();
            using (var key=Registry.CurrentUser.CreateSubKey(RegistryPath)) {
                key.SetValue("DisplayName","提醒助手");
                key.SetValue("InstallLocation",target);
                key.SetValue("DisplayIcon",exe);
                key.SetValue("UninstallString",Quote(setup));
                key.SetValue("NoModify",1);
                key.SetValue("NoRepair",1);
            }
            using (var run=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")) {
                if (startup.Checked) run.SetValue("Tixing",Quote(exe)+" --tray");
                else run.DeleteValue("Tixing",false);
            }
            var choice=MessageBox.Show(this,"安装完成。现在打开提醒助手吗？","提醒助手",MessageBoxButtons.YesNo,MessageBoxIcon.Information);
            if (choice==DialogResult.Yes) Process.Start(exe);
            Close();
        } catch (Exception error) {
            MessageBox.Show(this,"安装未完成。"+error.Message,"提醒助手",MessageBoxButtons.OK,MessageBoxIcon.Error);
        }
    }
    static string Quote(string value) { return "\"" + value + "\""; }
    static void Remove() {
        if (MessageBox.Show("卸载提醒助手并删除所有提醒设置？","提醒助手",MessageBoxButtons.YesNo)!=DialogResult.Yes) return;
        try {
            var folder=Path.GetDirectoryName(Application.ExecutablePath);
            File.Delete(Path.Combine(folder,App));
            File.Delete(Shortcut);
            var settings=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Tixing");
            File.Delete(Path.Combine(settings,"settings.xml"));
            File.Delete(Path.Combine(settings,"settings.xml.tmp"));
            if (Directory.Exists(settings) && Directory.GetFileSystemEntries(settings).Length==0)
                Directory.Delete(settings);
            using (var run=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run",true))
                if (run!=null) run.DeleteValue("Tixing",false);
            Registry.CurrentUser.DeleteSubKeyTree(RegistryPath,false);
            var command="ping 127.0.0.1 -n 3 > nul & del /q \"%TIXING_UNINSTALLER%\" & rd \"%TIXING_INSTALL_FOLDER%\"";
            var cleanup=new ProcessStartInfo(Path.Combine(Environment.SystemDirectory,"cmd.exe"),"/d /v:off /c "+command) { UseShellExecute=false, CreateNoWindow=true };
            cleanup.EnvironmentVariables["TIXING_UNINSTALLER"]=Application.ExecutablePath;
            cleanup.EnvironmentVariables["TIXING_INSTALL_FOLDER"]=folder;
            Process.Start(cleanup);
            MessageBox.Show("提醒助手已卸载。","提醒助手");
        } catch (Exception error) {
            MessageBox.Show("卸载未完成。请先从托盘退出提醒助手。\n"+error.Message,"提醒助手");
        }
    }
}
