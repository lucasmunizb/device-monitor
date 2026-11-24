class Reading
{
    public int? Percent;
    public bool Charging;
    public bool Full;
    public bool Cable; // headset: carga vista pelo cabo no PC (03F0:0795), não deduzida pela tensão
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

    // medido 2026-11-24: no cabo a tensão sobe até parar em ~4,19 V (oscila ±1 mV) e a % continua subindo;
    // sem cabo a tensão cai ~24 mV/10 s e a % só desce
    public const int ChargeStartMv = 3, ChargeNoiseMv = 3;

    // Resposta do headset a 66 89: 66 89 HI LO PCT (HI<<8|LO = mV). null = resposta que não serve.
    public static Reading Headset(byte[] r, int n, int? lastMv, Reading last, bool cable, out int mv)
    {
        mv = 0;
        if (r == null || n < 5 || r.Length < 5 || r[0] != 0x66 || r[1] != 0x89 || r[4] > 100) return null;
        mv = r[2] << 8 | r[3];
        // sem indicador de carga no protocolo: tensão ou % subindo acende o raio; aceso, só apaga quando a tensão cai de verdade
        bool was = last != null && last.Charging && !last.Cable; // tolerância de tensão só para carga deduzida
        int delta = lastMv == null ? 0 : mv - lastMv.Value;
        return new Reading
        {
            Percent = r[4],
            // cabo no PC = certeza; senão (carregador de tomada) deduz pela tensão/%. Com 100% vira "Carga completa"
            Charging = r[4] < 100 && (cable || lastMv != null && (delta >= (was ? -ChargeNoiseMv : ChargeStartMv)
                || (last != null && last.Percent != null && r[4] > last.Percent))),
            Full = r[4] == 100,
            Cable = cable,
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
