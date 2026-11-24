using System;
using System.Collections.Generic;
using System.Drawing;

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
        Faixa();
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
        var h = Battery.Headset(B(0x66,0x89,0x0F,0xA0,0x50), 62, false);
        Eq(80, h.Percent, "headset 80"); Eq(false, h.Charging, "sem cabo sem raio");
        // medido 2026-11-24: com o cabo USB no PC o headset aparece como 03F0:0795; sem cabo some.
        // A tensão não serve: no fim da carga fica parada e sem cabo cai tão devagar quanto oscila.
        Eq(true,  Battery.Headset(B(0x66,0x89,0x10,0x5C,0x31), 62, true).Charging, "cabo no PC: raio já na 1a leitura");
        var cheio = Battery.Headset(B(0x66,0x89,0x10,0x5C,0x64), 62, true);
        Eq(false, cheio.Charging, "cabo no PC com 100%: sem raio"); Eq("Carga completa", cheio.Status, "cabo no PC com 100%: carga completa");
        // bug 2026-11-24: sem cabo, descarregando devagar (3848 -> 3846 mV, 54%), o raio ficou preso
        Eq(false, Battery.Headset(B(0x66,0x89,0x0F,0x06,0x36), 62, false).Charging, "sem cabo descarregando devagar: sem raio");
        Eq(null, Battery.Headset(B(0x66,0x89,0x0F,0xA0,0x65), 62, false), "PCT > 100");
        Eq(null, Battery.Headset(B(0x66,0x8A,0x0F,0xA0,0x50), 62, false), "outra resposta");
        Eq(null, Battery.Headset(B(0x66,0x89,0x0F), 3, false), "curto");
        Eq("Carga completa", Battery.Headset(B(0x66,0x89,0x0F,0xA0,0x64), 62, false).Status, "headset 100 texto");
    }

    static Reading P(int? pct) { return new Reading { Percent = pct }; }
    static Color[] Col(params Color[] c) { return c; }

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

    static void Faixa()
    {
        Eq(Level.Low,  Battery.LevelOf(P(0)),   "0 vermelho");
        Eq(Level.Low,  Battery.LevelOf(P(20)),  "20 vermelho");
        Eq(Level.Mid,  Battery.LevelOf(P(21)),  "21 amarelo");
        Eq(Level.Mid,  Battery.LevelOf(P(60)),  "60 amarelo");
        Eq(Level.High, Battery.LevelOf(P(61)),  "61 verde");
        Eq(Level.High, Battery.LevelOf(P(100)), "100 verde");
        Eq(Level.None, Battery.LevelOf(P(null)), "sem % cinza");
        Eq(Level.None, Battery.LevelOf(null),   "sem leitura cinza");
        Eq(Level.None, Battery.LevelOf(Battery.Missing()), "receptor ausente cinza");
        Eq("70%", Battery.PercentText(P(70)), "texto 70%");
        Eq("—",   Battery.PercentText(null),  "texto sem leitura");
        Eq("—",   Battery.PercentText(new Reading { Charging = true }), "carregando sem % traço");

        // barra e bandeja medidas na máquina em 2026-11-24
        var bar = new Rectangle(0, 1032, 2560, 48); var tray = new Rectangle(2206, 1032, 354, 48);
        Eq((Rectangle?)new Rectangle(2206 - 140 - 4, 4, 140, 40), StripRules.Place(bar, tray, new Size(140, 40)), "colada à bandeja");
        Eq(null, StripRules.Place(new Rectangle(0, 0, 48, 1080), new Rectangle(0, 900, 48, 180), new Size(140, 40)), "barra vertical");
        Eq(null, StripRules.Place(bar, new Rectangle(100, 1032, 50, 48), new Size(140, 40)), "sem espaço");
        Eq(null, StripRules.Place(bar, new Rectangle(0, 0, 0, 0), new Size(140, 40)), "bandeja fora da barra");
        var t0 = new DateTime(2026, 11, 24, 12, 0, 0);
        Eq(false, StripRules.IconsVisible(null, t0), "barra presente");
        Eq(false, StripRules.IconsVisible(t0, t0.AddSeconds(9)), "explorer reiniciando não pisca");
        Eq(true,  StripRules.IconsVisible(t0, t0.AddSeconds(10)), "10 s sem barra: ícones");

        // fundo copiado da barra: aceita só o que parece barra (dica/menu por cima não vira fundo)
        Eq(true,  StripRules.LooksLikeBar(Col(Color.FromArgb(28, 32, 44), Color.FromArgb(30, 34, 46)), false), "barra escura");
        Eq(false, StripRules.LooksLikeBar(Col(Color.FromArgb(249, 249, 249), Color.FromArgb(40, 40, 40), Color.FromArgb(249, 249, 249)), false), "dica com texto por cima");
        Eq(false, StripRules.LooksLikeBar(Col(Color.FromArgb(243, 243, 243), Color.FromArgb(243, 243, 243)), false), "janela clara sobre barra escura");
        Eq(true,  StripRules.LooksLikeBar(Col(Color.FromArgb(238, 238, 238), Color.FromArgb(236, 236, 236)), true), "barra clara");
        Eq(false, StripRules.LooksLikeBar(Col(Color.FromArgb(32, 32, 32), Color.FromArgb(32, 32, 32)), true), "janela escura sobre barra clara");
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
