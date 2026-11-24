class Reading
{
    public int? Percent;
    public bool Charging;
    public bool Full;
    public string Status; // null quando nada a dizer
}

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

    public const int ChargeRiseMv = 15;

    // Resposta do headset a 66 89: 66 89 HI LO PCT (HI<<8|LO = mV). null = resposta que não serve.
    public static Reading Headset(byte[] r, int n, int? lastMv, out int mv)
    {
        mv = 0;
        if (r == null || n < 5 || r.Length < 5 || r[0] != 0x66 || r[1] != 0x89 || r[4] > 100) return null;
        mv = r[2] << 8 | r[3];
        // sem indicador de carga no protocolo: tensão subindo = no cabo
        return new Reading
        {
            Percent = r[4],
            Charging = lastMv != null && mv - lastMv.Value >= ChargeRiseMv,
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
}
