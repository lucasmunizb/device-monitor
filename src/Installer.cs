using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

static class Installer
{
    const string Name = "MonitorDeBateria";
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + Name;
    static readonly string Shortcut = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Monitor de Bateria.lnk");

    public static readonly string Target = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"MonitorDeBateria\MonitorDeBateria.exe");

    public static bool IsInstalledCopy(string exePath)
    {
        return string.Equals(Path.GetFullPath(exePath), Path.GetFullPath(Target), StringComparison.OrdinalIgnoreCase);
    }

    // Duplo clique no exe baixado: copia, registra tudo em HKCU (sem admin) e abre o monitor.
    public static void Install()
    {
        KillOthers(); // instância aberta segura o arquivo; clicar de novo = atualizar
        Directory.CreateDirectory(Path.GetDirectoryName(Target));
        File.Copy(Application.ExecutablePath, Target, true);

        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
        var lnk = shell.CreateShortcut(Shortcut);
        lnk.TargetPath = Target;
        lnk.Save();

        SetAutoStart(true);
        using (var k = Registry.CurrentUser.CreateSubKey(UninstallKey))
        {
            k.SetValue("DisplayName", "Monitor de Bateria");
            k.SetValue("UninstallString", "\"" + Target + "\" --desinstalar");
            k.SetValue("DisplayIcon", Target);
            k.SetValue("InstallLocation", Path.GetDirectoryName(Target));
            k.SetValue("NoModify", 1);
            k.SetValue("NoRepair", 1);
        }
        Process.Start(Target);
        MessageBox.Show("Instalado", "Monitor de Bateria");
    }

    public static void Uninstall()
    {
        KillOthers();
        SetAutoStart(false);
        Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false);
        if (File.Exists(Shortcut)) File.Delete(Shortcut);
        // o exe não apaga a si mesmo: um cmd apaga a pasta depois que este processo sair
        // (ping em vez de timeout: timeout falha sem console de entrada)
        Process.Start(new ProcessStartInfo("cmd.exe",
            "/c ping -n 3 127.0.0.1 >nul & rmdir /s /q \"" + Path.GetDirectoryName(Target) + "\"")
            { CreateNoWindow = true, UseShellExecute = false });
    }

    public static bool IsAutoStart()
    {
        using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
            return k != null && k.GetValue(Name) != null;
    }

    public static void SetAutoStart(bool on)
    {
        using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
            if (on) k.SetValue(Name, "\"" + Target + "\"");
            else k.DeleteValue(Name, false);
    }

    static void KillOthers()
    {
        int self = Process.GetCurrentProcess().Id;
        foreach (var p in Process.GetProcessesByName(Name))
            using (p)
            {
                if (p.Id == self) continue;
                try { p.Kill(); p.WaitForExit(5000); } catch (Exception) { } // já saiu
            }
    }
}
