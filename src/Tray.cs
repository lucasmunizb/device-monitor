using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

class Tray : IDisposable
{
    readonly Devices devices;
    readonly Control ui = new Control();
    readonly Slot mouse, headset;

    public Tray(Devices devices)
    {
        this.devices = devices;
        var forceHandle = ui.Handle; // BeginInvoke precisa do handle criado na thread da UI
        mouse = new Slot("Mouse", this);
        headset = new Slot("Headset", this);
        devices.MouseChanged += OnMouse;
        devices.HeadsetChanged += OnHeadset;

        // Explorer cria a entrada do ícone só depois de ele aparecer; espera um pouco antes de promover
        var promote = new Timer { Interval = 5000 };
        promote.Tick += delegate
        {
            promote.Dispose();
            try { Installer.PromoteTrayIcons(); } catch (Exception) { } // ícone escondido não vale derrubar o monitor
        };
        promote.Start();
    }

    void OnMouse(Reading r) { Post(() => mouse.Show(r)); }
    void OnHeadset(Reading r) { Post(() => headset.Show(r)); }

    // leitura que chega durante o Sair (handle já destruído) é descartada, não derruba o processo
    void Post(Action a)
    {
        try { ui.BeginInvoke(a); }
        catch (InvalidOperationException) { } // inclui ObjectDisposedException
    }

    ContextMenuStrip Menu(ToolStripMenuItem status)
    {
        var m = new ContextMenuStrip();
        m.Items.Add(status);
        m.Items.Add("Atualizar", null, delegate { devices.RefreshNow(); });
        var auto = new ToolStripMenuItem("Iniciar com o Windows", null, delegate { Installer.SetAutoStart(!Installer.IsAutoStart()); });
        m.Items.Add(auto);
        m.Items.Add("Sair", null, delegate { Application.Exit(); });
        m.Opening += delegate { auto.Checked = Installer.IsAutoStart(); }; // o registro é a verdade
        return m;
    }

    public void Dispose()
    {
        devices.MouseChanged -= OnMouse;
        devices.HeadsetChanged -= OnHeadset;
        devices.Stop();
        mouse.Dispose();
        headset.Dispose();
    }

    class Slot : IDisposable
    {
        readonly string name;
        readonly NotifyIcon icon = new NotifyIcon();
        readonly ToolStripMenuItem status = new ToolStripMenuItem { Enabled = false, Visible = false };

        public Slot(string name, Tray tray)
        {
            this.name = name;
            icon.ContextMenuStrip = tray.Menu(status);
            Show(null);
            icon.Visible = true;
        }

        public void Show(Reading r)
        {
            string text = name + " " + (r != null && r.Percent != null ? r.Percent + "%" : "—");
            if (r != null && r.Charging) text += " (carregando)";
            if (r != null && r.Status != null) text += " — " + r.Status;
            icon.Text = text.Length > 63 ? text.Substring(0, 63) : text;
            status.Text = r == null ? "" : r.Status;
            status.Visible = r != null && r.Status != null;

            var old = icon.Icon;
            icon.Icon = Draw(Battery.Label(r), Battery.Low(r), r != null && r.Charging);
            if (old != null) { DestroyIcon(old.Handle); old.Dispose(); }
        }

        public void Dispose()
        {
            icon.Visible = false;
            if (icon.Icon != null) DestroyIcon(icon.Icon.Handle);
            icon.Dispose();
        }
    }

    static Icon Draw(string label, bool low, bool charging)
    {
        using (var bmp = new Bitmap(16, 16))
        using (var g = Graphics.FromImage(bmp))
        using (var path = new GraphicsPath())
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;
            float size = label.Length >= 3 ? 8.5f : 11f; // "100" precisa caber em 16 px
            using (var f = new FontFamily("Segoe UI"))
            using (var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                path.AddString(label, f, (int)FontStyle.Bold, size, new RectangleF(-2, 0, 20, 17), fmt);
            // raio por baixo do número: o contorno do texto mantém os dígitos legíveis
            if (charging)
            {
                var bolt = new[] { new PointF(12, 0), new PointF(8, 7), new PointF(11, 7), new PointF(9, 13), new PointF(15, 5), new PointF(12, 5), new PointF(14, 0) };
                using (var edge = new Pen(Color.FromArgb(20, 20, 20), 1f)) g.DrawPolygon(edge, bolt);
                g.FillPolygon(Brushes.Gold, bolt);
            }
            using (var outline = new Pen(Color.FromArgb(220, 20, 20, 20), 2.5f) { LineJoin = LineJoin.Round })
                g.DrawPath(outline, path);
            using (var fill = new SolidBrush(low ? Color.FromArgb(255, 70, 70) : Color.White))
                g.FillPath(fill, path);
            return Icon.FromHandle(bmp.GetHicon());
        }
    }

    [DllImport("user32.dll")]
    static extern bool DestroyIcon(IntPtr handle);
}
