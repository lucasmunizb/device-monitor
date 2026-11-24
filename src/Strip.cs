using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

static class StripRules
{
    public const int Gap = 4;

    // Retângulo da faixa no cliente da barra (ela é filha da barra); null = não cabe/barra estranha.
    public static Rectangle? Place(Rectangle bar, Rectangle tray, Size strip)
    {
        if (bar.Height > bar.Width || tray.IsEmpty || !bar.Contains(tray)) return null;
        int x = tray.Left - bar.Left - strip.Width - Gap;
        if (x < 0) return null;
        return new Rectangle(x, (bar.Height - strip.Height) / 2, strip.Width, strip.Height);
    }

    // Barra = quase uniforme de cima a baixo e escura no tema escuro (clara no claro).
    // Dica, menu ou janela passando por cima do ponto copiado não vira fundo da faixa.
    public static bool LooksLikeBar(Color[] column, bool light)
    {
        if (column == null || column.Length == 0) return false;
        double min = 255, max = 0, sum = 0;
        foreach (var c in column)
        {
            double y = 0.299 * c.R + 0.587 * c.G + 0.114 * c.B;
            min = Math.Min(min, y); max = Math.Max(max, y); sum += y;
        }
        double mean = sum / column.Length;
        return max - min <= 24 && (light ? mean >= 150 : mean <= 100);
    }

    // Explorer reiniciando some com a barra por poucos segundos: só volta aos ícones após 10 s
    public static bool IconsVisible(DateTime? missingSince, DateTime now)
    {
        return missingSince != null && now - missingSince.Value >= TimeSpan.FromSeconds(10);
    }
}

// Faixa "🎧 70%  🖱 86%" filha da barra de tarefas, à esquerda da bandeja.
// Janela em camadas com alfa por pixel (UpdateLayeredWindow); filha em camadas exige manifest Windows 8+.
class Strip : Form
{
    const string Headphone = "\uE7F6", MouseGlyph = "\uE962", Bolt = "\uE945";
    readonly ContextMenuStrip menu;
    readonly ToolTip tip = new ToolTip();
    readonly Timer timer = new Timer { Interval = 1000 };
    Reading headset, mouse;
    string headsetTip = "Headset —", mouseTip = "Mouse —";
    IntPtr parent;
    Size size = new Size(1, 1);
    bool attached;
    Bitmap background;

    public bool Attached { get { return attached; } }

    public Strip(ContextMenuStrip menu)
    {
        this.menu = menu;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        timer.Tick += delegate { Attach(); };
        timer.Start();
        Render();
        Attach();
    }

    protected override bool ShowWithoutActivation { get { return true; } }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            return cp;
        }
    }

    public void SetReading(string device, Reading r, string tooltip)
    {
        if (device == "Headset") { headset = r; headsetTip = tooltip; } else { mouse = r; mouseTip = tooltip; }
        tip.SetToolTip(this, headsetTip + "\n" + mouseTip);
        Render();
        Attach();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Right) menu.Show(Cursor.Position);
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        parent = IntPtr.Zero; // Explorer morreu e levou a janela: o próximo tick recria e reanexa
        base.OnHandleDestroyed(e);
    }

    void Attach()
    {
        if (IsDisposed) return;
        IntPtr bar = FindWindow("Shell_TrayWnd", null);
        IntPtr tray = bar == IntPtr.Zero ? IntPtr.Zero : FindWindowEx(bar, IntPtr.Zero, "TrayNotifyWnd", null);
        if (tray == IntPtr.Zero) { SetAttached(false); return; }
        if (!IsHandleCreated) { CreateHandle(); Render(); }
        if (parent != bar)
        {
            // estilo antes do SetParent (pede a doc); falhou = não diz que anexou, ícones voltam após 10 s
            SetWindowLong(Handle, GWL_STYLE, (GetWindowLong(Handle, GWL_STYLE) & ~WS_POPUP) | WS_CHILD);
            if (SetParent(Handle, bar) == IntPtr.Zero) { SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0, SWP_HIDE); SetAttached(false); return; }
            parent = bar;
        }
        RECT b, t;
        GetWindowRect(bar, out b);
        GetWindowRect(tray, out t);
        var place = StripRules.Place(b.ToRectangle(), t.ToRectangle(), size);
        if (place == null) { SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0, SWP_HIDE); SetAttached(false); return; }
        var p = place.Value;
        // HWND_TOP: acima do conteúdo XAML da barra, que também é filho dela
        if (SampleBackground(t.ToRectangle(), b.T + p.Y, p.Height)) Render(); // largura não muda: posição continua valendo
        if (!Visible) Show();
        // SHOWWINDOW pela janela real: após o Explorer recriar a barra, Visible do WinForms pode mentir
        SetWindowPos(Handle, IntPtr.Zero, p.X, p.Y, p.Width, p.Height, SWP_NOACTIVATE | SWP_SHOWWINDOW);
        SetAttached(true);
    }

    void SetAttached(bool on)
    {
        attached = on;
    }

    // Texto pelo GDI do Windows (TextRenderer) com hinting, como o relógio e os ícones da bandeja.
    // Suavização do GDI mistura com o que está por baixo: fundo opaco, a faixa pinta por baixo a coluna da barra que fica entre ela e a bandeja.
    void Render()
    {
        if (!IsHandleCreated) return;
        bool light = LightTheme();
        Color fg = light ? Color.Black : Color.White;
        const TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
        using (var glyphFont = new Font("Segoe Fluent Icons", 16f, GraphicsUnit.Pixel))
        using (var textFont = new Font("Segoe UI Variable Text Semibold", 12f, GraphicsUnit.Pixel))
        {
            var parts = new[] { Tuple.Create(Headphone, headset), Tuple.Create(MouseGlyph, mouse) };
            const int height = 40;
            int width;
            // mede com o mesmo Graphics/hinting do desenho: medir sem ele sobrava ~30 px no fim da faixa
            using (var probe = new Bitmap(1, 1, PixelFormat.Format24bppRgb))
            using (var mg = Graphics.FromImage(probe))
            {
                mg.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                width = LayoutParts(mg, parts, glyphFont, textFont, light, fg, height, flags, false);
            }
            size = new Size(width, height);

            using (var bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb))
            using (var g = Graphics.FromImage(bmp))
            {
                if (background != null)
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                    g.DrawImage(background, new Rectangle(0, 0, width, height));
                }
                else g.Clear(light ? Color.FromArgb(0xEE, 0xEE, 0xEE) : Color.FromArgb(0x1C, 0x1C, 0x1C));
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit; // TextRenderer vira GDI cinza com hinting, como a barra do Windows 11 (sem franja colorida)
                LayoutParts(g, parts, glyphFont, textFont, light, fg, height, flags, true);
                Push(bmp);
            }
        }
    }

    // Percorre "símbolo [raio] texto" de cada aparelho; devolve a largura total. draw=false só mede.
    static int LayoutParts(Graphics g, Tuple<string, Reading>[] parts, Font glyphFont, Font textFont, bool light, Color fg, int height, TextFormatFlags flags, bool draw)
    {
        const int pad = 4, inner = 4, between = 10;
        int x = pad;
        for (int i = 0; i < parts.Length; i++)
        {
            var r = parts[i].Item2;
            if (i > 0) x += between;
            x = DrawPart(g, parts[i].Item1, glyphFont, fg, x, height, flags, draw) + inner;
            if (r != null && r.Charging)
                x = DrawPart(g, Bolt, glyphFont, light ? Color.FromArgb(0x9D, 0x5D, 0x00) : Color.FromArgb(0xFF, 0xD2, 0x3F), x, height, flags, draw) + 1;
            x = DrawPart(g, Battery.PercentText(r), textFont, LevelColor(Battery.LevelOf(r), light), x, height, flags, draw);
        }
        return x + pad;
    }

    static int DrawPart(Graphics g, string s, Font f, Color c, int x, int height, TextFormatFlags flags, bool draw)
    {
        var sz = TextRenderer.MeasureText(g, s, f, Size.Empty, flags);
        if (draw) TextRenderer.DrawText(g, s, f, new Point(x, (height - sz.Height) / 2), c, flags);
        return x + sz.Width;
    }

    // Copia 1 px de largura da barra no vão entre a faixa e a bandeja; true se mudou (tema, papel de parede).
    bool SampleBackground(Rectangle trayScreen, int top, int height)
    {
        var col = new Bitmap(1, height, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(col))
            g.CopyFromScreen(trayScreen.Left - StripRules.Gap / 2, top, 0, 0, col.Size);
        var pixels = new Color[height];
        for (int y = 0; y < height; y++) pixels[y] = col.GetPixel(0, y);
        if (!StripRules.LooksLikeBar(pixels, LightTheme())) { col.Dispose(); return false; } // mantém o fundo anterior
        if (background != null && background.Height == height)
        {
            bool same = true;
            for (int y = 0; y < height && same; y++) same = background.GetPixel(0, y) == pixels[y];
            if (same) { col.Dispose(); return false; }
        }
        if (background != null) background.Dispose();
        background = col;
        return true;
    }

    static Color LevelColor(Level level, bool light)
    {
        switch (level)
        {
            case Level.Low:  return light ? Color.FromArgb(0xC4, 0x2B, 0x1C) : Color.FromArgb(0xFF, 0x5A, 0x5A);
            case Level.Mid:  return light ? Color.FromArgb(0x9D, 0x5D, 0x00) : Color.FromArgb(0xFF, 0xD2, 0x3F);
            case Level.High: return light ? Color.FromArgb(0x0F, 0x7B, 0x0F) : Color.FromArgb(0x5B, 0xD1, 0x5B);
            default:         return light ? Color.FromArgb(0x6E, 0x6E, 0x6E) : Color.FromArgb(0x9A, 0x9A, 0x9A);
        }
    }

    static bool LightTheme()
    {
        using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
            return k != null && (k.GetValue("SystemUsesLightTheme") as int? ?? 0) == 1;
    }

    void Push(Bitmap bmp)
    {
        IntPtr screen = GetDC(IntPtr.Zero), mem = CreateCompatibleDC(screen);
        IntPtr hbmp = bmp.GetHbitmap(Color.FromArgb(0)), old = SelectObject(mem, hbmp);
        try
        {
            var sz = new SIZE { cx = bmp.Width, cy = bmp.Height };
            var src = new POINT();
            var blend = new BLENDFUNCTION { BlendOp = 0, SourceConstantAlpha = 255, AlphaFormat = 0 }; // opaco
            UpdateLayeredWindow(Handle, screen, IntPtr.Zero, ref sz, mem, ref src, 0, ref blend, ULW_ALPHA);
        }
        finally
        {
            SelectObject(mem, old);
            DeleteObject(hbmp);
            DeleteDC(mem);
            ReleaseDC(IntPtr.Zero, screen);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            timer.Dispose();
            if (background != null) background.Dispose();
            tip.Dispose();
            // não deixar filho órfão na barra do Explorer
            if (IsHandleCreated) SetParent(Handle, IntPtr.Zero);
        }
        base.Dispose(disposing);
    }

    const int WS_EX_LAYERED = 0x80000, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x8000000;
    const int GWL_STYLE = -16, WS_CHILD = 0x40000000;
    const int WS_POPUP = unchecked((int)0x80000000);
    const uint SWP_NOACTIVATE = 0x10, SWP_SHOWWINDOW = 0x40;
    const uint SWP_HIDE = 0x80 | 0x1 | 0x2 | 0x4 | SWP_NOACTIVATE; // HIDEWINDOW | NOSIZE | NOMOVE | NOZORDER
    const int ULW_ALPHA = 2;

    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; public Rectangle ToRectangle() { return Rectangle.FromLTRB(L, T, R, B); } }
    [StructLayout(LayoutKind.Sequential)] struct SIZE { public int cx, cy; }
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int x, y; }
    [StructLayout(LayoutKind.Sequential, Pack = 1)] struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }

    [DllImport("user32.dll")] static extern IntPtr FindWindow(string cls, string name);
    [DllImport("user32.dll")] static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string name);
    [DllImport("user32.dll")] static extern IntPtr SetParent(IntPtr child, IntPtr parent);
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int index);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr h, int index, int value);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] static extern bool UpdateLayeredWindow(IntPtr h, IntPtr dst, IntPtr pptDst, ref SIZE size, IntPtr src, ref POINT pptSrc, int key, ref BLENDFUNCTION blend, int flags);
    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h, IntPtr dc);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
}
