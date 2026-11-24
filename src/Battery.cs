class Reading
{
    public int? Percent;
    public bool Charging;
    public bool Full;
    public string Status; // null quando nada a dizer
}

enum Level { None, Low, Mid, High }

static class Battery
{
    // Report do mouse: 03 30 40 ESTADO PCT. null = report que não serve.
    public static Reading Mouse(byte[] r, int n, Reading last)
    {
        if (r == null || n < 5 || r.Length < 5 || r[0] != 0x03 || r[1] != 0x30 || r[2] != 0x40) return null;
        switch (r[3])
        {
            case 0x01: return new Reading { Percent = r[4] };
            case 0x02: return new Reading { Percent = 100, Full = true, Status = "Carga completa" };
            // no cabo o firmware infla o %: mantém o último lido sem cabo
            case 0x03: return new Reading { Percent = last == null ? null : last.Percent, Charging = true };
            default: return null;
        }
    }

    // Resposta do headset a 66 89: 66 89 HI LO PCT (HI<<8|LO = mV, não usado). null = resposta que não serve.
    // Carga = cabo USB no PC (03F0:0795 presente, medido). A tensão não serve: no fim da carga fica parada
    // e sem cabo cai tão devagar quanto oscila. shortcut: carregador de tomada não mostra raio; medir se for usado.
    public static Reading Headset(byte[] r, int n, bool cable)
    {
        if (r == null || n < 5 || r.Length < 5 || r[0] != 0x66 || r[1] != 0x89 || r[4] > 100) return null;
        return new Reading
        {
            Percent = r[4],
            Charging = cable && r[4] < 100, // com 100% vira "Carga completa", como o mouse
            Full = r[4] == 100,
            Status = r[4] == 100 ? "Carga completa" : null
        };
    }

    public static Reading Missing() { return new Reading { Status = "Receptor não encontrado" }; }

    public static string Label(Reading r)
    {
        return r == null || r.Percent == null ? "—" : r.Percent.Value.ToString();
    }

    public static bool Low(Reading r) { return r != null && r.Percent <= 20; }

    // cor da faixa: vermelho 0–20, amarelo 21–60, verde 61–100
    public static Level LevelOf(Reading r)
    {
        if (r == null || r.Percent == null) return Level.None;
        return r.Percent <= 20 ? Level.Low : r.Percent <= 60 ? Level.Mid : Level.High;
    }

    public static string PercentText(Reading r)
    {
        return r == null || r.Percent == null ? "—" : r.Percent + "%";
    }
}
