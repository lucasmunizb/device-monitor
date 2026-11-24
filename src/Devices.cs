using System;
using System.Linq;
using System.Threading;

class Devices
{
    const string MouseCable = "vid_1d57&pid_fa73&", MouseReceiver = "vid_1d57&pid_fa65&";
    const string HeadsetId = "vid_03f0&pid_0995&";

    public event Action<Reading> MouseChanged, HeadsetChanged;

    volatile bool running;
    Timer headsetTimer;
    readonly object headsetLock = new object();
    Reading lastMouse;
    int? lastMv;

    public void Start()
    {
        running = true;
        new Thread(MouseLoop) { IsBackground = true, Name = "mouse" }.Start();
        headsetTimer = new Timer(delegate { PollHeadset(); }, null, 0, 30000);
    }

    // o mouse empurra os reports sozinho; só o headset dá para consultar na hora
    public void RefreshNow() { ThreadPool.QueueUserWorkItem(delegate { PollHeadset(); }); }

    public void Stop()
    {
        running = false;
        if (headsetTimer != null) headsetTimer.Dispose();
    }

    void MouseLoop()
    {
        while (running)
        {
            try
            {
                // no cabo o receptor fica mudo: ouvir o cabo primeiro
                var d = Hid.Find(MouseCable).Concat(Hid.Find(MouseReceiver)).FirstOrDefault(x => x.Caps.UsagePage == 0x000A);
                if (d == null) { Fire(MouseChanged, Battery.Missing()); Thread.Sleep(5000); continue; }
                using (var h = Hid.Open(d, false))
                {
                    var r = new byte[d.Caps.Input];
                    int n;
                    // timeout de 5 s: volta a procurar, para trocar de receptor para cabo e vice-versa
                    while (running && (n = Hid.Transfer(h, r, false, 5000)) > 0)
                    {
                        var m = Battery.Mouse(r, n, lastMouse);
                        if (m == null) continue;
                        if (!m.Charging) lastMouse = m;
                        Fire(MouseChanged, m);
                    }
                }
            }
            catch (Exception) { Fire(MouseChanged, Battery.Missing()); Thread.Sleep(5000); }
        }
    }

    void PollHeadset()
    {
        if (!running) return;
        lock (headsetLock)
        {
            Reading result = null;
            try
            {
                var d = Hid.Find(HeadsetId).FirstOrDefault(x => x.Caps.UsagePage == 0xFF13 && x.Caps.Output > 0);
                if (d != null)
                    using (var h = Hid.Open(d, true))
                    {
                        var q = new byte[d.Caps.Output];
                        q[0] = 0x66; q[1] = 0x89;
                        Hid.Transfer(h, q, true, 500);
                        var r = new byte[d.Caps.Input];
                        int n, mv;
                        // pode chegar outro report antes da resposta
                        for (int i = 0; i < 5 && result == null && (n = Hid.Transfer(h, r, false, 1500)) > 0; i++)
                        {
                            result = Battery.Headset(r, n, lastMv, out mv);
                            if (result != null) lastMv = mv;
                        }
                        if (result == null) result = new Reading { Status = "Sem leitura" };
                    }
            }
            catch (Exception) { }
            if (result == null) { result = Battery.Missing(); lastMv = null; }
            Fire(HeadsetChanged, result);
        }
    }

    static void Fire(Action<Reading> e, Reading r) { if (e != null) e(r); }
}
