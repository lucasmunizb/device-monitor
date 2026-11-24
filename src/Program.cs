using System;
using System.Threading;
using System.Windows.Forms;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--desinstalar") { Installer.Uninstall(); return; }
        if (!Installer.IsInstalledCopy(Application.ExecutablePath))
        {
            try { Installer.Install(); }
            catch (Exception e) { MessageBox.Show("Falha ao instalar: " + e.Message, "Monitor de Bateria"); }
            return;
        }
        bool first;
        using (new Mutex(true, "MonitorDeBateria", out first))
        {
            if (!first) return;
            Application.EnableVisualStyles();
            var devices = new Devices();
            using (new Tray(devices))
            {
                devices.Start();
                Application.Run();
            }
        }
    }
}
