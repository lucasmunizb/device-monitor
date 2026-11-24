using System;
using System.Collections.Generic;

static class Tests
{
    static int total;
    static List<string> falhas = new List<string>();

    static void Eq(object esperado, object real, string nome)
    {
        total++;
        if (!object.Equals(esperado, real))
            falhas.Add(nome + ": esperado " + (esperado ?? "null") + ", veio " + (real ?? "null"));
    }

    static byte[] B(params byte[] b) { return b; }

    static int Main()
    {
        Mouse();
        Headset();
        Rotulo();
        Instalador();
        if (falhas.Count > 0)
        {
            foreach (var f in falhas) Console.WriteLine("FALHA " + f);
            return 1;
        }
        Console.WriteLine("OK " + total + " testes");
        return 0;
    }

    static void Mouse()
    {
        var a = Battery.Mouse(B(0x03,0x30,0x40,0x01,0x61), 5, null);
        Eq(97, a.Percent, "mouse 97"); Eq(false, a.Charging, "mouse 97 sem raio");
        var c = Battery.Mouse(B(0x03,0x30,0x40,0x03,0x64), 5, a);
        Eq(97, c.Percent, "carregando mantém último %"); Eq(true, c.Charging, "raio");
        var c0 = Battery.Mouse(B(0x03,0x30,0x40,0x03,0x64), 5, null);
        Eq(null, c0.Percent, "carregando sem anterior"); Eq(true, c0.Charging, "raio sem anterior");
        var f = Battery.Mouse(B(0x03,0x30,0x40,0x02,0x64), 5, a);
        Eq(100, f.Percent, "cheio"); Eq("Carga completa", f.Status, "cheio texto"); Eq(false, f.Charging, "cheio sem raio");
        Eq(null, Battery.Mouse(B(0x03,0x30,0x40,0x01), 4, null), "curto");
        Eq(null, Battery.Mouse(B(0x01,0x30,0x40,0x01,0x61), 5, null), "outro report");
        Eq(null, Battery.Mouse(B(0x03,0x30,0x40,0x04,0x61), 5, null), "estado desconhecido");
    }

    static void Headset()
    {
        int mv;
        var h = Battery.Headset(B(0x66,0x89,0x0F,0xA0,0x50), 62, null, out mv);
        Eq(80, h.Percent, "headset 80"); Eq(4000, mv, "mV"); Eq(false, h.Charging, "1a leitura sem raio");
        Eq(true,  Battery.Headset(B(0x66,0x89,0x0F,0xAF,0x50), 62, 4000, out mv).Charging, "+15 mV raio");
        Eq(false, Battery.Headset(B(0x66,0x89,0x0F,0xAE,0x50), 62, 4000, out mv).Charging, "+14 mV sem raio");
        Eq(false, Battery.Headset(B(0x66,0x89,0x0F,0x90,0x50), 62, 4000, out mv).Charging, "caiu sem raio");
        Eq(null, Battery.Headset(B(0x66,0x89,0x0F,0xA0,0x65), 62, null, out mv), "PCT > 100");
        Eq(null, Battery.Headset(B(0x66,0x8A,0x0F,0xA0,0x50), 62, null, out mv), "outra resposta");
        Eq(null, Battery.Headset(B(0x66,0x89,0x0F), 3, null, out mv), "curto");
        Eq("Carga completa", Battery.Headset(B(0x66,0x89,0x0F,0xA0,0x64), 62, null, out mv).Status, "headset 100 texto");
    }

    static Reading P(int? pct) { return new Reading { Percent = pct }; }

    static void Rotulo()
    {
        Eq("—", Battery.Label(Battery.Missing()), "ausente traço");
        Eq("Receptor não encontrado", Battery.Missing().Status, "ausente texto");
        Eq("—", Battery.Label(null), "null traço");
        Eq("97", Battery.Label(P(97)), "rótulo 97");
        Eq(true, Battery.Low(P(20)), "20 baixo");
        Eq(false, Battery.Low(P(21)), "21 ok");
        Eq(false, Battery.Low(Battery.Missing()), "ausente não é baixo");
        Eq(false, Battery.Low(null), "null não é baixo");
    }

    static void Instalador()
    {
        Eq(true, Installer.IsInstalledCopy(Installer.Target), "cópia instalada");
        Eq(true, Installer.IsInstalledCopy(Installer.Target.ToUpper()), "sem caixa");
        Eq(false, Installer.IsInstalledCopy(@"C:\Users\x\Downloads\Instalar-Monitor-de-Bateria.exe"), "downloads");

        // valores reais de HKCU\Control Panel\NotifyIconSettings\<id> no Windows 11 (2026-11-24)
        Eq(true, Installer.ShouldPromote(@"C:\Users\Muniz\AppData\Local\MonitorDeBateria\MonitorDeBateria.exe", null), "ícone escondido vira visível");
        Eq(true, Installer.ShouldPromote(@"{F1B32785-6FBA-4FCF-9D55-7B8E7F157091}\MonitorDeBateria\monitordebateria.exe", null), "caminho com GUID de pasta");
        Eq(false, Installer.ShouldPromote(@"C:\Users\Muniz\AppData\Local\MonitorDeBateria\MonitorDeBateria.exe", 0), "usuário escondeu: respeita");
        Eq(false, Installer.ShouldPromote(@"C:\Users\Muniz\AppData\Local\Temp\devmon-app\Instalar-Monitor-de-Bateria.exe", null), "instalador não");
        Eq(false, Installer.ShouldPromote(@"C:\Users\Muniz\AppData\Roaming\Telegram Desktop\Telegram.exe", null), "outro app não");
        Eq(false, Installer.ShouldPromote(null, null), "entrada sem caminho");
    }
}
