// Offline preview installer. Compiled with the Windows .NET Framework compiler.
// Release distribution should move to a signed, maintained installer toolchain.
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;

internal static class PreviewSetup
{
    const string Identity = "Alpha6OPS-Desktop-Preview-0.9";
    const string RegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Alpha6OPSPreview";
    static readonly string InstallPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Alpha6Designs", "Alpha6OPSPreview");
    static readonly string ShortcutPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Alpha 6 OPS Preview.lnk");

    [STAThread]
    static int Main(string[] args)
    {
#if UNINSTALLER
        if (args.Length == 0) args = new[] { "--uninstall" };
#endif
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        try
        {
            if (args.Length == 2 && args[0] == "--extract-test")
            {
                Extract(args[1]); // Diagnostic: no registry writes or shortcuts.
                return 0;
            }
            if (args.Length == 1 && args[0] == "--uninstall")
            {
                // Windows locks a running executable. Run the small uninstaller from temp.
                var temporary = Path.Combine(Path.GetTempPath(), "Alpha6OPS-Uninstall-" + Guid.NewGuid().ToString("N") + ".exe");
                File.Copy(Assembly.GetExecutingAssembly().Location, temporary);
                Process.Start(new ProcessStartInfo(temporary, "--uninstall-worker") { UseShellExecute = true });
                return 0;
            }
            if (args.Length == 1 && args[0] == "--uninstall-worker") return Uninstall();
#if UNINSTALLER
            MessageBox.Show("Use Windows Installed apps to uninstall Alpha 6 OPS Preview.", "Alpha 6 OPS");
            return 0;
#else
            using (var form = new Form())
            using (var title = new Label())
            using (var details = new Label())
            using (var install = new Button())
            {
                form.Text = "Alpha 6 OPS — Desktop Preview Setup";
                form.ClientSize = new Size(560, 310);
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MaximizeBox = false;
                form.StartPosition = FormStartPosition.CenterScreen;
                form.BackColor = Color.FromArgb(16, 18, 15);
                form.ForeColor = Color.WhiteSmoke;
                title.Text = "ALPHA 6 OPS";
                title.SetBounds(28, 25, 500, 45);
                title.Font = new Font("Segoe UI", 24, FontStyle.Bold);
                title.ForeColor = Color.FromArgb(249, 217, 40);
                details.Text = "Install the Windows desktop connection test for your account.\r\n\r\nIncludes a private .NET runtime and Start menu shortcut.\r\nLive monitoring requires your installed MSFS 2024 SDK.\r\nAn offline flight replay is also included.\r\n\r\nLocation: " + InstallPath;
                details.SetBounds(30, 86, 500, 145);
                details.Font = new Font("Segoe UI", 10);
                install.Text = "Install Alpha 6 OPS";
                install.SetBounds(30, 250, 225, 38);
                install.BackColor = Color.FromArgb(249, 217, 40);
                install.ForeColor = Color.Black;
                install.Click += delegate
                {
                    install.Enabled = false;
                    form.UseWaitCursor = true;
                    try
                    {
                        if (Directory.Exists(InstallPath)) throw new IOException("A preview is already installed. Uninstall it through Windows Installed apps before installing this build.");
                        var staging = InstallPath + ".staging-" + Guid.NewGuid().ToString("N");
                        try
                        {
                            Extract(staging);
                            File.WriteAllText(Path.Combine(staging, ".alpha6-preview"), Identity);
                            Directory.Move(staging, InstallPath);
                        }
                        finally
                        {
                            if (Directory.Exists(staging)) { CheckTree(staging); Directory.Delete(staging, true); }
                        }
                        CreateShortcut();
                        using (var key = Registry.CurrentUser.CreateSubKey(RegistryKey))
                        {
                            key.SetValue("DisplayName", "Alpha 6 OPS Desktop Preview");
                            key.SetValue("DisplayVersion", "0.9.0");
                            key.SetValue("Publisher", "Alpha 6 Designs");
                            key.SetValue("InstallLocation", InstallPath);
                            key.SetValue("DisplayIcon", Path.Combine(InstallPath, "Alpha6OPS.exe"));
                            key.SetValue("UninstallString", "\"" + Path.Combine(InstallPath, "Uninstall.exe") + "\" --uninstall");
                            key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                            key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                        }
                        MessageBox.Show(form, "Installed. Open Alpha 6 OPS Preview from the Start menu.\r\n\r\nClose or minimize to keep it in the tray. Use Exit OPS to stop.", "Alpha 6 OPS", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        form.Close();
                    }
                    catch (Exception error)
                    {
                        MessageBox.Show(form, "Installation could not finish: " + error.Message + "\r\n\r\nIf files were extracted, Uninstall.exe in the installation folder can remove the preview.", "Alpha 6 OPS", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        install.Enabled = true;
                    }
                    finally { form.UseWaitCursor = false; }
                };
                form.Controls.AddRange(new Control[] { title, details, install });
                Application.Run(form);
            }
            return 0;
#endif
        }
        catch (Exception error)
        {
            if (args.Length > 0 && args[0] == "--extract-test") return 1;
            MessageBox.Show(error.Message, "Alpha 6 OPS Setup", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }

    static void Extract(string directory)
    {
        var root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (Directory.Exists(root)) throw new IOException("The destination already exists. Choose an empty, new folder.");
        using (var payload = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip"))
        {
            if (payload == null) throw new IOException("This executable contains no installation payload.");
            using (var archive = new ZipArchive(payload, ZipArchiveMode.Read))
            {
                // Validate the entire archive before writing any file.
                foreach (var entry in archive.Entries)
                {
                    var target = Path.GetFullPath(Path.Combine(root, entry.FullName));
                    if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new IOException("Unsafe package entry.");
                }
                Directory.CreateDirectory(root);
                foreach (var entry in archive.Entries)
                {
                    var target = Path.GetFullPath(Path.Combine(root, entry.FullName));
                    if (string.IsNullOrEmpty(entry.Name)) { Directory.CreateDirectory(target); continue; }
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    using (var input = entry.Open())
                    using (var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write)) input.CopyTo(output);
                }
            }
        }
    }

    static void CreateShortcut()
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell");
        var shell = Activator.CreateInstance(shellType);
        var shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { ShortcutPath });
        var type = shortcut.GetType();
        type.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { Path.Combine(InstallPath, "Alpha6OPS.exe") });
        type.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { InstallPath });
        type.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
    }

    static int Uninstall()
    {
        var marker = Path.Combine(InstallPath, ".alpha6-preview");
        if (!File.Exists(marker) || File.ReadAllText(marker) != Identity) throw new IOException("The installation marker is missing or invalid. No files were removed.");
        if (MessageBox.Show("Remove Alpha 6 OPS Desktop Preview and its bundled runtime?\r\n\r\nClose Alpha 6 OPS using Exit OPS before continuing.\r\n\r\n" + InstallPath, "Uninstall Alpha 6 OPS", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return 0;
        foreach (var process in Process.GetProcessesByName("Alpha6OPS"))
        {
            using (process)
            {
                if (!process.HasExited && string.Equals(process.MainModule.FileName, Path.Combine(InstallPath, "Alpha6OPS.exe"), StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Alpha 6 OPS is still running. Exit it from the tray menu first.");
            }
        }
        // The target is a fixed, product-owned path; refuse junctions/symlinks anywhere inside it.
        CheckTree(InstallPath);
        Directory.Delete(InstallPath, true);
        if (File.Exists(ShortcutPath)) File.Delete(ShortcutPath);
        Registry.CurrentUser.DeleteSubKeyTree(RegistryKey, false);
        MessageBox.Show("Alpha 6 OPS Preview was removed.", "Alpha 6 OPS");
        return 0;
    }

    static void CheckTree(string directory)
    {
        if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) throw new IOException("Installation contains a linked directory. No files were removed.");
        foreach (var child in Directory.GetDirectories(directory)) CheckTree(child);
    }
}
