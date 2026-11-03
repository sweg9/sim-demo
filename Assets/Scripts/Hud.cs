using System.Collections.Generic;
using UnityEngine;

/// <summary>IMGUI overlay: stage tracker, robot camera with detection overlay, registry, log, minimap, captions.</summary>
public class Hud : MonoBehaviour
{
    public DemoDirector d;
    public RobotRig robot;
    public House house;
    public Camera mainCam;
    public Camera mapCam;
    public RenderTexture mapRT;

    static readonly Color Panel = new Color(0.04f, 0.055f, 0.08f, 0.80f);
    static readonly Color Edge = new Color(0.55f, 0.72f, 0.95f, 0.22f);
    static readonly Color Txt = new Color(0.93f, 0.96f, 1f);
    static readonly Color Dim = new Color(0.60f, 0.68f, 0.78f);

    Font font;
    GUIStyle ts;
    Texture2D white, scanTex, vignette, wedge, glowTex;
    float W, Hh, S;
    readonly Dictionary<string, float> shown = new Dictionary<string, float>();

    void Start()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        white = Texture2D.whiteTexture;
        scanTex = GradientTex(1, 64, new Color(1, 1, 1, 0), new Color(1, 1, 1, 0.9f), true);
        vignette = Vignette();
        wedge = Wedge();
        glowTex = Radial();
    }

    // ------------------------------------------------------------------ textures
    Texture2D GradientTex(int w, int h, Color a, Color b, bool mirror)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int y = 0; y < h; y++)
        {
            float k = y / (float)(h - 1);
            if (mirror) k = 1f - Mathf.Abs(k * 2f - 1f);
            for (int x = 0; x < w; x++) t.SetPixel(x, y, Color.Lerp(a, b, k * k));
        }
        t.wrapMode = TextureWrapMode.Clamp; t.Apply();
        return t;
    }

    Texture2D Vignette()
    {
        int n = 128;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x / (float)(n - 1) - 0.5f) * 2f, dy = (y / (float)(n - 1) - 0.5f) * 2f;
                float r = Mathf.Clamp01((Mathf.Sqrt(dx * dx * 0.75f + dy * dy) - 0.55f) / 0.75f);
                t.SetPixel(x, y, new Color(0, 0.03f, 0.06f, r * r * 0.62f));
            }
        t.wrapMode = TextureWrapMode.Clamp; t.Apply();
        return t;
    }

    Texture2D Wedge()
    {
        int n = 128;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                // wedge pointing up from the bottom centre, +-40 degrees
                float dx = (x - n / 2f) / (n / 2f), dy = (y) / (float)n;
                float ang = Mathf.Atan2(Mathf.Abs(dx), dy) * Mathf.Rad2Deg;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = ang < 40f && r < 0.98f ? (1f - r) * 0.8f : 0f;
                t.SetPixel(x, y, new Color(0.3f, 0.9f, 1f, a * 0.65f));
            }
        t.wrapMode = TextureWrapMode.Clamp; t.Apply();
        return t;
    }

    Texture2D Radial()
    {
        int n = 64;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x / (float)(n - 1) - 0.5f) * 2f, dy = (y / (float)(n - 1) - 0.5f) * 2f;
                float r = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                t.SetPixel(x, y, new Color(1, 1, 1, r * r));
            }
        t.wrapMode = TextureWrapMode.Clamp; t.Apply();
        return t;
    }

    // ------------------------------------------------------------------ drawing helpers
    void RR(Rect r, Color c, float rad)
    {
        GUI.DrawTexture(r, white, ScaleMode.StretchToFill, true, 0, c, Vector4.zero, new Vector4(rad, rad, rad, rad));
    }

    void RRo(Rect r, Color c, float rad, float w = 1f)
    {
        GUI.DrawTexture(r, white, ScaleMode.StretchToFill, true, 0, c, new Vector4(w, w, w, w), new Vector4(rad, rad, rad, rad));
    }

    void Fill(Rect r, Color c)
    {
        var o = GUI.color; GUI.color = c; GUI.DrawTexture(r, white); GUI.color = o;
    }

    void Card(Rect r, string title, Color accent, string right = null)
    {
        RR(r, Panel, 14);
        RRo(r, Edge, 14);
        if (title != null)
        {
            Fill(new Rect(r.x + 16, r.y + 14, 3, 14), accent);
            T(new Rect(r.x + 26, r.y + 9, r.width - 40, 24), title, 13, Dim, TextAnchor.MiddleLeft, true);
            if (right != null) T(new Rect(r.x + 16, r.y + 9, r.width - 32, 24), right, 13, accent, TextAnchor.MiddleRight, true);
        }
    }

    void T(Rect r, string s, int size, Color c, TextAnchor a = TextAnchor.UpperLeft, bool bold = false, bool wrap = false)
    {
        if (ts == null) ts = new GUIStyle(GUI.skin.label);
        ts.font = font; ts.fontSize = size; ts.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        ts.alignment = a; ts.wordWrap = wrap; ts.clipping = TextClipping.Clip;
        ts.normal.textColor = c;
        GUI.Label(r, s, ts);
    }

    float TH(string s, int size, float width, bool bold = false)
    {
        if (ts == null) ts = new GUIStyle(GUI.skin.label);
        ts.font = font; ts.fontSize = size; ts.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal; ts.wordWrap = true;
        return ts.CalcHeight(new GUIContent(s), width);
    }

    bool Btn(Rect r, string label, Color accent, bool on = false)
    {
        bool hover = r.Contains(Event.current.mousePosition);
        RR(r, on ? new Color(accent.r, accent.g, accent.b, 0.30f) : hover ? new Color(1, 1, 1, 0.14f) : new Color(1, 1, 1, 0.07f), r.height * 0.5f);
        RRo(r, new Color(accent.r, accent.g, accent.b, hover || on ? 0.9f : 0.35f), r.height * 0.5f);
        T(r, label, 13, Txt, TextAnchor.MiddleCenter, true);
        if (GUI.Button(r, GUIContent.none, GUIStyle.none)) { Sfx.Play("ui"); return true; }
        return false;
    }

    float Smooth(string key, float target, float speed = 6f)
    {
        float v;
        if (!shown.TryGetValue(key, out v)) v = target;
        v = Mathf.MoveTowards(v, target, speed * Time.unscaledDeltaTime);
        shown[key] = v;
        return v;
    }

    static string StatusText(CtrlStatus s)
    {
        switch (s)
        {
            case CtrlStatus.Detected: return "DETECTED";
            case CtrlStatus.Candidate: return "MAPPING";
            case CtrlStatus.Verified: return "VERIFIED";
            case CtrlStatus.InRoutine: return "IN ROUTINE";
            default: return "UNSEEN";
        }
    }

    static Color StatusColor(CtrlStatus s)
    {
        switch (s)
        {
            case CtrlStatus.Detected: return DemoDirector.Blue;
            case CtrlStatus.Candidate: return DemoDirector.Amber;
            case CtrlStatus.Verified: return DemoDirector.Green;
            case CtrlStatus.InRoutine: return DemoDirector.Cyan;
            default: return new Color(0.5f, 0.55f, 0.62f);
        }
    }

    // ------------------------------------------------------------------ main
    void OnGUI()
    {
        if (d == null || robot == null) return;
        S = Screen.height / 1080f;
        GUI.matrix = Matrix4x4.Scale(new Vector3(S, S, 1f));
        W = Screen.width / S; Hh = 1080f;

        if (Event.current.type == EventType.Repaint || Event.current.type == EventType.MouseDown || Event.current.type == EventType.MouseUp || Event.current.type == EventType.Layout)
        {
            WorldTags();
            TopBar();
            LeftColumn();
            RightColumn();
            Minimap();
            Caption();
            Toasts();
            ProposalCard();
            Hints();
            if (d.introVisible) Intro();
            if (d.endCard) EndCard();
        }
    }

    // ------------------------------------------------------------------ pieces
    void TopBar()
    {
        float pw = 150, gap = 8, total = 7 * pw + 6 * gap;
        float x0 = (W - total) * 0.5f - 70, y = 18;
        string[] phase = { "SETUP", "RUNTIME", "LEARNING" };
        int[] pf = { 0, 3, 6 }, pl = { 2, 5, 6 };
        for (int p = 0; p < 3; p++)
        {
            float a = x0 + pf[p] * (pw + gap), b = x0 + pl[p] * (pw + gap) + pw;
            Color c = DemoDirector.StageColor(pf[p]);
            T(new Rect(a, y - 4, b - a, 16), phase[p], 11, new Color(c.r, c.g, c.b, 0.85f), TextAnchor.MiddleCenter, true);
            Fill(new Rect(a + 6, y + 13, b - a - 12, 2), new Color(c.r, c.g, c.b, 0.35f));
        }
        for (int i = 0; i < 7; i++)
        {
            Rect r = new Rect(x0 + i * (pw + gap), y + 20, pw, 52);
            Color c = DemoDirector.StageColor(i);
            int st = d.stageState[i];
            float act = Smooth("st" + i, st == 1 ? 1f : 0f, 5f);
            float done = Smooth("sd" + i, st == 2 ? 1f : 0f, 5f);
            RR(r, Color.Lerp(new Color(0.04f, 0.055f, 0.08f, 0.72f), new Color(c.r * 0.35f, c.g * 0.35f, c.b * 0.35f, 0.92f), Mathf.Max(act, done * 0.6f)), 12);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
            RRo(r, new Color(c.r, c.g, c.b, Mathf.Lerp(0.18f, 0.4f + 0.5f * pulse, act) * (st == 2 ? 1.6f : 1f)), 12, act > 0.5f ? 2f : 1f);
            if (act > 0.5f) GUI.DrawTexture(new Rect(r.x - 30, r.y - 22, r.width + 60, r.height + 44), glowTex, ScaleMode.StretchToFill, true, 0, new Color(c.r, c.g, c.b, 0.12f + 0.10f * pulse), 0, 0);
            T(new Rect(r.x + 12, r.y + 6, 40, 24), "S" + (i + 1), 20, st == 0 ? Dim : Color.Lerp(Txt, c, 0.5f), TextAnchor.MiddleLeft, true);
            if (st == 2) T(new Rect(r.xMax - 36, r.y + 6, 26, 24), "OK", 12, c, TextAnchor.MiddleRight, true);
            T(new Rect(r.x + 12, r.y + 29, r.width - 16, 20), DemoDirector.StageName[i], 13, st == 0 ? Dim : Txt, TextAnchor.MiddleLeft, st == 1);
        }
        // mission chip
        Rect mc = new Rect(x0 + total + 18, y + 20, 150, 52);
        RR(mc, Panel, 12); RRo(mc, Edge, 12);
        T(new Rect(mc.x + 12, mc.y + 6, mc.width - 20, 20), d.missionRunning ? (d.auto ? "AUTO DEMO" : "RUNNING") : "MANUAL", 13, d.missionRunning ? DemoDirector.Cyan : Dim, TextAnchor.MiddleLeft, true);
        float t = d.missionRunning ? Time.time - d.missionT0 : 0f;
        T(new Rect(mc.x + 12, mc.y + 26, mc.width - 20, 22), d.missionRunning ? ((int)(t / 60)).ToString("00") + ":" + (t % 60).ToString("00") + "  " + d.missionName : "Drive with WASD", 12, Txt, TextAnchor.MiddleLeft);
    }

    void LeftColumn()
    {
        float x = 20, y = 18;
        RR(new Rect(x, y, 330, 52), Panel, 14); RRo(new Rect(x, y, 330, 52), Edge, 14);
        Fill(new Rect(x + 16, y + 14, 3, 24), DemoDirector.Cyan);
        T(new Rect(x + 28, y + 6, 290, 26), "CLOiD", 22, Txt, TextAnchor.MiddleLeft, true);
        T(new Rect(x + 28, y + 28, 290, 20), "Legacy control integration demo", 12, Dim, TextAnchor.MiddleLeft);
        // buttons
        y += 62;
        float bw = 76, bh = 30;
        if (Btn(new Rect(x, y, 98, bh), d.missionRunning && d.auto ? "Stop" : "Auto demo", DemoDirector.Cyan, d.auto && d.missionRunning))
        { if (d.missionRunning && d.auto) d.CancelMission("Auto demo stopped"); else if (!d.missionRunning) d.StartAuto(); }
        if (Btn(new Rect(x + 104, y, bw + 8, bh), "Routine", DemoDirector.Amber)) { if (!d.missionRunning) d.RunRoutineKey("evening"); }
        if (Btn(new Rect(x + 104 + bw + 14, y, bw + 18, bh), "Goodnight", DemoDirector.Amber)) { if (!d.missionRunning) d.RunRoutineKey("goodnight"); }
        if (Btn(new Rect(x + 104 + bw + bw + 40, y, 48, bh), "Reset", DemoDirector.Mute)) d.ResetWorld();
        y += 42;

        if (d.probeOpen) { ProbeCard(new Rect(x, y, 330, 36 + 4 * 44)); y += 36 + 4 * 44 + 12; }
        if (d.safetyOpen) { SafetyCard(new Rect(x, y, 330, 36 + 4 * 38)); y += 36 + 4 * 38 + 12; }
        if (d.focus != null) { ChannelCard(new Rect(x, y, 330, 214), d.focus); y += 226; }
    }

    void ProbeCard(Rect r)
    {
        Card(r, "CAPABILITY PROBE", DemoDirector.Cyan, d.levelText);
        for (int i = 0; i < d.probe.Length; i++)
        {
            var p = d.probe[i];
            Rect row = new Rect(r.x + 14, r.y + 40 + i * 44, r.width - 28, 40);
            RR(row, new Color(1, 1, 1, 0.04f), 10);
            Color c = p.state == 2 ? DemoDirector.Green : p.state == 1 ? DemoDirector.Cyan : Dim;
            T(new Rect(row.x + 10, row.y, 36, row.height), p.level, 17, c, TextAnchor.MiddleLeft, true);
            T(new Rect(row.x + 50, row.y + 3, 190, 20), p.name, 14, Txt, TextAnchor.MiddleLeft, true);
            T(new Rect(row.x + 50, row.y + 21, 220, 16), p.detail, 11, Dim, TextAnchor.MiddleLeft);
            Rect ic = new Rect(row.xMax - 34, row.y + 8, 24, 24);
            if (p.state == 2) { RR(ic, new Color(c.r, c.g, c.b, 0.25f), 12); T(ic, "OK", 15, c, TextAnchor.MiddleCenter, true); }
            else if (p.state == 1)
            {
                float a = 0.4f + 0.6f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6f));
                RR(new Rect(ic.x + 6, ic.y + 6, 12, 12), new Color(c.r, c.g, c.b, a), 6);
            }
            else RRo(ic, new Color(1, 1, 1, 0.2f), 12);
        }
    }

    void SafetyCard(Rect r)
    {
        Card(r, "SAFETY GUARD", DemoDirector.Amber, "R1");
        for (int i = 0; i < d.safety.Length; i++)
        {
            var s = d.safety[i];
            Rect row = new Rect(r.x + 14, r.y + 40 + i * 38, r.width - 28, 34);
            RR(row, new Color(1, 1, 1, 0.04f), 9);
            Color c = s.state == 2 ? DemoDirector.Green : s.state == 3 ? DemoDirector.Red : s.state == 1 ? DemoDirector.Amber : Dim;
            T(new Rect(row.x + 12, row.y, row.width - 50, row.height), s.text, 13, s.state == 0 ? Dim : Txt, TextAnchor.MiddleLeft);
            Rect ic = new Rect(row.xMax - 30, row.y + 5, 24, 24);
            if (s.state == 2) T(ic, "OK", 16, c, TextAnchor.MiddleCenter, true);
            else if (s.state == 3) T(ic, "X", 16, c, TextAnchor.MiddleCenter, true);
            else if (s.state == 1) RR(new Rect(ic.x + 6, ic.y + 6, 12, 12), new Color(c.r, c.g, c.b, 0.4f + 0.6f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 7f))), 6);
        }
    }

    void ChannelCard(Rect r, ControlPoint cp)
    {
        Card(r, "CHANNEL SCORES", DemoDirector.Green, cp.id);
        T(new Rect(r.x + 16, r.y + 36, r.width - 32, 20), cp.lampName + "  ·  map conf " + cp.mapConf.ToString("0.00"), 12, Txt, TextAnchor.MiddleLeft);
        var rows = new List<KeyValuePair<string, float>>();
        if (cp.hasWifi) rows.Add(new KeyValuePair<string, float>("Wi-Fi", cp.pWifi));
        if (cp.hasIr) rows.Add(new KeyValuePair<string, float>("IR", cp.pIr));
        if (cp.hasBt) rows.Add(new KeyValuePair<string, float>("BT", cp.pBt));
        rows.Add(new KeyValuePair<string, float>("PHYS", cp.pPhys));
        float y = r.y + 64;
        foreach (var kv in rows)
        {
            float v = Smooth(cp.id + kv.Key, kv.Value, 0.35f);
            Color c = kv.Key == "PHYS" ? DemoDirector.Amber : DemoDirector.Blue;
            if (v < 0.40f) c = DemoDirector.Red;
            T(new Rect(r.x + 16, y, 52, 22), kv.Key, 12, Txt, TextAnchor.MiddleLeft, true);
            Rect bar = new Rect(r.x + 70, y + 6, r.width - 140, 10);
            RR(bar, new Color(1, 1, 1, 0.08f), 5);
            RR(new Rect(bar.x, bar.y, Mathf.Max(8, bar.width * v), bar.height), new Color(c.r, c.g, c.b, 0.9f), 5);
            Fill(new Rect(bar.x + bar.width * 0.4f, bar.y - 3, 1, 16), new Color(1, 1, 1, 0.35f));
            T(new Rect(r.xMax - 62, y, 48, 22), v.ToString("0.00"), 12, c, TextAnchor.MiddleRight, true);
            y += 28;
        }
        if (!string.IsNullOrEmpty(d.lastLearn))
        {
            Rect lr = new Rect(r.x + 14, r.yMax - 40, r.width - 28, 28);
            RR(lr, new Color(DemoDirector.Green.r, DemoDirector.Green.g, DemoDirector.Green.b, 0.12f), 9);
            T(lr, "S7  " + d.lastLearn, 12, DemoDirector.Green, TextAnchor.MiddleCenter, true);
        }
        T(new Rect(r.x + 70 + (r.width - 140) * 0.4f - 24, r.y + 36, 48, 18), "", 10, Dim, TextAnchor.MiddleCenter);
    }

    // ------------------------------------------------------------------ right column
    void RightColumn()
    {
        float w = 520, x = W - w - 20, y = 18;
        FeedCard(new Rect(x, y, w, 36 + 325 + 12));
        y += 36 + 325 + 12 + 12;
        RegistryCard(new Rect(x, y, w, 36 + 5 * 34 + 12));
        y += 36 + 5 * 34 + 12 + 12;
        LogCard(new Rect(x, y, w, Hh - y - 18));
    }

    void FeedCard(Rect r)
    {
        Card(r, "ROBOT CAM  ·  CLOiD-01", DemoDirector.Cyan);
        Rect fr = new Rect(r.x + 10, r.y + 36, r.width - 20, 325);
        GUI.DrawTexture(fr, robot.feed, ScaleMode.StretchToFill, false);
        GUI.DrawTexture(fr, vignette, ScaleMode.StretchToFill, true);
        // scan line
        float sy = Mathf.Repeat(Time.unscaledTime * 0.35f, 1f);
        GUI.DrawTexture(new Rect(fr.x, fr.y + sy * fr.height - 24, fr.width, 48), scanTex, ScaleMode.StretchToFill, true, 0, new Color(0.3f, 0.9f, 1f, 0.10f), 0, 0);
        // header chips
        bool rec = ((int)(Time.unscaledTime * 1.6f)) % 2 == 0;
        RR(new Rect(fr.x + 10, fr.y + 10, 8, 8), rec ? DemoDirector.Red : new Color(1, 0.4f, 0.4f, 0.3f), 4);
        T(new Rect(fr.x + 24, fr.y + 4, 80, 20), "LIVE", 12, Txt, TextAnchor.MiddleLeft, true);
        RR(new Rect(fr.xMax - 74, fr.y + 6, 64, 22), new Color(0, 0, 0, 0.45f), 11);
        T(new Rect(fr.xMax - 74, fr.y + 6, 64, 22), "CAP " + d.levelText, 12, DemoDirector.Cyan, TextAnchor.MiddleCenter, true);
        // frame brackets
        Color bc = new Color(1, 1, 1, 0.5f);
        float L = 20, th = 2, m = 8;
        Fill(new Rect(fr.x + m, fr.y + m, L, th), bc); Fill(new Rect(fr.x + m, fr.y + m, th, L), bc);
        Fill(new Rect(fr.xMax - m - L, fr.y + m, L, th), bc); Fill(new Rect(fr.xMax - m - th, fr.y + m, th, L), bc);
        Fill(new Rect(fr.x + m, fr.yMax - m - th, L, th), bc); Fill(new Rect(fr.x + m, fr.yMax - m - L, th, L), bc);
        Fill(new Rect(fr.xMax - m - L, fr.yMax - m - th, L, th), bc); Fill(new Rect(fr.xMax - m - th, fr.yMax - m - L, th, L), bc);
        // crosshair
        Fill(new Rect(fr.center.x - 9, fr.center.y, 18, 1), new Color(1, 1, 1, 0.35f));
        Fill(new Rect(fr.center.x, fr.center.y - 9, 1, 18), new Color(1, 1, 1, 0.35f));

        // detections
        GUI.BeginClip(fr);
        foreach (var cp in house.controls)
        {
            if (!cp.visibleNow && Time.time - cp.seenT > 0.25f) continue;
            float conf = cp.visConf;
            if (conf < (cp.IsDecoy ? 0.22f : 0.40f)) continue;
            float x0 = cp.screenMin.x * fr.width, x1 = cp.screenMax.x * fr.width;
            float y0 = (1f - cp.screenMax.y) * fr.height, y1 = (1f - cp.screenMin.y) * fr.height;
            float pad = 6;
            Rect b = new Rect(x0 - pad, y0 - pad, x1 - x0 + pad * 2, y1 - y0 + pad * 2);
            Color c; string label;
            if (cp.IsDecoy) { c = new Color(1f, 0.55f, 0.5f); label = "WALL PANEL  " + conf.ToString("0.00") + "  REJECT"; }
            else if (cp.status == CtrlStatus.Unseen) { c = Color.white; label = "ANALYZING...  " + conf.ToString("0.00"); float s = 1f + 0.3f * (1f - Mathf.Clamp01(cp.visTime / 0.55f)); b = Scale(b, s); }
            else { c = StatusColor(cp.status); label = cp.typeName.ToUpper() + "  " + cp.conf.ToString("0.00"); }
            Brackets(b, c, cp.IsDecoy ? 1.5f : 2.5f);
            float lw = label.Length * 7.2f + 14;
            Rect lr = new Rect(Mathf.Clamp(b.x, 4, fr.width - lw - 4), Mathf.Max(4, b.y - 24), lw, 20);
            RR(lr, new Color(c.r * 0.25f, c.g * 0.25f, c.b * 0.25f, 0.88f), 6);
            T(lr, label, 11, c, TextAnchor.MiddleCenter, true);
            if (!cp.IsDecoy && cp.status != CtrlStatus.Unseen)
                T(new Rect(lr.x, b.yMax + 2, 200, 16), cp.id + " · " + cp.room, 11, new Color(c.r, c.g, c.b, 0.9f), TextAnchor.MiddleLeft, true);
        }
        GUI.EndClip();
        // footer
        T(new Rect(fr.x + 12, fr.yMax - 24, 300, 20), System.DateTime.Now.ToString("HH:mm:ss"), 11, new Color(1, 1, 1, 0.75f), TextAnchor.MiddleLeft, true);
        T(new Rect(fr.xMax - 250, fr.yMax - 24, 240, 20), "FOV 62°   MODE: " + (robot.speed > 0.05f ? "NAV" : "HOLD"), 11, new Color(1, 1, 1, 0.75f), TextAnchor.MiddleRight, true);
    }

    static Rect Scale(Rect r, float s)
    {
        Vector2 c = r.center; float w = r.width * s, h = r.height * s;
        return new Rect(c.x - w / 2, c.y - h / 2, w, h);
    }

    void Brackets(Rect b, Color c, float th)
    {
        float L = Mathf.Min(16, Mathf.Min(b.width, b.height) * 0.35f);
        Fill(new Rect(b.x, b.y, L, th), c); Fill(new Rect(b.x, b.y, th, L), c);
        Fill(new Rect(b.xMax - L, b.y, L, th), c); Fill(new Rect(b.xMax - th, b.y, th, L), c);
        Fill(new Rect(b.x, b.yMax - th, L, th), c); Fill(new Rect(b.x, b.yMax - L, th, L), c);
        Fill(new Rect(b.xMax - L, b.yMax - th, L, th), c); Fill(new Rect(b.xMax - th, b.yMax - L, th, L), c);
    }

    void RegistryCard(Rect r)
    {
        int n = 0; foreach (var c in house.controls) if (!c.IsDecoy && c.status != CtrlStatus.Unseen) n++;
        Card(r, "CONTROL REGISTRY", DemoDirector.Blue, n + " / 5");
        int row = 0;
        foreach (var cp in house.controls)
        {
            if (cp.IsDecoy) continue;
            Rect rr = new Rect(r.x + 10, r.y + 38 + row * 34, r.width - 20, 30);
            bool foc = d.focus == cp;
            RR(rr, foc ? new Color(1, 1, 1, 0.10f) : new Color(1, 1, 1, row % 2 == 0 ? 0.035f : 0.0f), 8);
            bool seen = cp.status != CtrlStatus.Unseen;
            T(new Rect(rr.x + 10, rr.y, 56, rr.height), cp.id, 13, seen ? Txt : Dim, TextAnchor.MiddleLeft, true);
            T(new Rect(rr.x + 68, rr.y, 120, rr.height), seen ? cp.typeName : "-", 12, seen ? Txt : Dim, TextAnchor.MiddleLeft);
            T(new Rect(rr.x + 190, rr.y, 120, rr.height), seen ? cp.room : "", 12, Dim, TextAnchor.MiddleLeft);
            Color sc = StatusColor(cp.status);
            Rect pill = new Rect(rr.xMax - 150, rr.y + 5, 96, 20);
            RR(pill, new Color(sc.r, sc.g, sc.b, 0.20f), 10);
            T(pill, StatusText(cp.status), 10, sc, TextAnchor.MiddleCenter, true);
            T(new Rect(rr.xMax - 50, rr.y, 42, rr.height), seen ? cp.mapConf > 0 ? cp.mapConf.ToString("0.00") : cp.conf.ToString("0.00") : "", 12, Txt, TextAnchor.MiddleRight);
            row++;
        }
    }

    void LogCard(Rect r)
    {
        Card(r, "PROCESS LOG", DemoDirector.Mute, "end-to-end trace");
        float y = r.yMax - 12;
        float w = r.width - 28;
        for (int i = d.log.Count - 1; i >= 0 && y > r.y + 40; i--)
        {
            var l = d.log[i];
            float h = Mathf.Max(18, TH(l.text, 12, w - 92));
            y -= h + 4;
            if (y < r.y + 38) break;
            float age = Time.time - 0f;
            Color tc = l.col;
            Rect tag = new Rect(r.x + 14, y + 1, 40, 17);
            Color sc = TagColor(l.tag);
            RR(tag, new Color(sc.r, sc.g, sc.b, 0.22f), 6);
            T(tag, l.tag, 10, sc, TextAnchor.MiddleCenter, true);
            T(new Rect(r.x + 60, y, 50, 18), l.time, 10, Dim, TextAnchor.MiddleLeft);
            float alpha = i == d.log.Count - 1 ? 1f : 0.88f;
            T(new Rect(r.x + 106, y, w - 92, h), l.text, 12, new Color(tc.r, tc.g, tc.b, alpha) * (tc == DemoDirector.Mute ? 1.15f : 1f), TextAnchor.UpperLeft, false, true);
        }
    }

    static Color TagColor(string tag)
    {
        switch (tag)
        {
            case "S1": case "S2": case "S3": return DemoDirector.Blue;
            case "S4": case "S5": case "S6": return DemoDirector.Amber;
            case "S7": return DemoDirector.Green;
            case "SAFE": return DemoDirector.Red;
            default: return DemoDirector.Cyan;
        }
    }

    // ------------------------------------------------------------------ minimap
    void Minimap()
    {
        float w = 360, h = 270 + 36;
        Rect r = new Rect(20, Hh - h - 18, w, h);
        Card(r, "FLOOR PLAN", DemoDirector.Cyan);
        Rect mr = new Rect(r.x + 10, r.y + 36, w - 20, 270 - 10);
        GUI.DrawTexture(mr, mapRT, ScaleMode.StretchToFill, false);
        float s = mapCam.orthographicSize, hw = s * mapCam.aspect;
        System.Func<Vector3, Vector2> P = wp => new Vector2(
            mr.x + (wp.x - (mapCam.transform.position.x - hw)) / (2 * hw) * mr.width,
            mr.y + (1f - (wp.z - (mapCam.transform.position.z - s)) / (2 * s)) * mr.height);
        foreach (var cp in house.controls)
        {
            if (cp.IsDecoy) continue;
            Vector2 p = P(cp.transform.position);
            Color c = StatusColor(cp.status);
            RR(new Rect(p.x - 5, p.y - 5, 10, 10), new Color(c.r, c.g, c.b, 0.95f), 5);
            RRo(new Rect(p.x - 7, p.y - 7, 14, 14), new Color(1, 1, 1, 0.6f), 7);
        }
        Vector2 rp = P(robot.transform.position);
        float yaw = robot.transform.eulerAngles.y;
        var old = GUI.matrix;
        GUIUtility.RotateAroundPivot(yaw, rp);
        GUI.DrawTexture(new Rect(rp.x - 40, rp.y - 80, 80, 80), wedge, ScaleMode.StretchToFill, true);
        GUI.matrix = old;
        GUI.DrawTexture(new Rect(rp.x - 16, rp.y - 16, 32, 32), glowTex, ScaleMode.StretchToFill, true, 0, new Color(0.3f, 0.9f, 1f, 0.55f), 0, 0);
        RR(new Rect(rp.x - 5, rp.y - 5, 10, 10), Color.white, 5);
        RRo(mr, new Color(1, 1, 1, 0.12f), 6);
        T(new Rect(mr.x + 8, mr.yMax - 22, 300, 18), "Living · Kitchen   |   Hall · Bedroom", 10, new Color(1, 1, 1, 0.55f), TextAnchor.MiddleLeft, true);
    }

    // ------------------------------------------------------------------ world tags (AR)
    void WorldTags()
    {
        if (mainCam == null) return;
        foreach (var cp in house.controls)
        {
            if (cp.IsDecoy || cp.status == CtrlStatus.Unseen) continue;
            Vector3 wp = cp.transform.position + Vector3.up * 0.16f + cp.Normal * 0.05f;
            Vector3 sp = mainCam.WorldToScreenPoint(wp);
            if (sp.z < 0.5f || sp.z > 22f) continue;
            float k = Mathf.Clamp01(1f - (sp.z - 5f) / 17f);
            Vector2 p = new Vector2(sp.x / S, (Screen.height - sp.y) / S);
            Color c = StatusColor(cp.status);
            string s = cp.id + "  " + cp.lampName;
            float w = s.Length * 7f + 28;
            Rect r = new Rect(p.x - w / 2, p.y - 36, w, 24);
            if (r.xMax > W - 560 || r.x < 0 || r.y < 90) continue;
            RR(r, new Color(0.03f, 0.05f, 0.08f, 0.82f * (0.5f + 0.5f * k)), 12);
            RRo(r, new Color(c.r, c.g, c.b, 0.8f), 12);
            RR(new Rect(r.x + 8, r.y + 8, 8, 8), c, 4);
            T(new Rect(r.x + 22, r.y, r.width - 26, r.height), s, 11, Txt, TextAnchor.MiddleLeft, true);
            Fill(new Rect(p.x - 0.5f, r.yMax, 1, 10), new Color(c.r, c.g, c.b, 0.8f));
        }
    }

    // ------------------------------------------------------------------ caption, toasts, hints
    void Caption()
    {
        if (string.IsNullOrEmpty(d.capHead)) return;
        float age = Time.time - d.capT;
        float a = Mathf.Clamp01(age / 0.35f);
        float w = 880;
        float cx = (W - 540 - 370) * 0.5f + 370;
        Rect r = new Rect(cx - w / 2, Hh - 150 + (1f - a) * 14, w, 86);
        Color sc = Color.white;
        int cur = -1;
        for (int i = 6; i >= 0; i--) if (d.stageState[i] == 1) { cur = i; break; }
        Color acc = cur >= 0 ? DemoDirector.StageColor(cur) : DemoDirector.Cyan;
        var og = GUI.color; GUI.color = new Color(1, 1, 1, a);
        RR(r, new Color(0.03f, 0.045f, 0.07f, 0.84f), 18);
        RRo(r, new Color(acc.r, acc.g, acc.b, 0.55f), 18, 1.5f);
        Fill(new Rect(r.x + 22, r.y + 22, 4, 42), acc);
        T(new Rect(r.x + 40, r.y + 10, w - 60, 38), d.capHead, 26, Txt, TextAnchor.MiddleLeft, true);
        T(new Rect(r.x + 40, r.y + 48, w - 60, 30), d.capBody, 17, Dim, TextAnchor.MiddleLeft);
        GUI.color = og;
    }

    void Toasts()
    {
        float cx = (W - 540 - 370) * 0.5f + 370 - 40;
        float y = 104;
        foreach (var t in d.toasts)
        {
            float age = Time.time - t.t0;
            float a = Mathf.Clamp01(age / 0.25f) * Mathf.Clamp01((t.dur - age) / 0.4f);
            float w = 470, h = 62;
            Rect r = new Rect(cx - w / 2, y + (1f - Mathf.Clamp01(age / 0.25f)) * -16f, w, h);
            var og = GUI.color; GUI.color = new Color(1, 1, 1, a);
            RR(r, new Color(0.035f, 0.05f, 0.075f, 0.9f), 16);
            RRo(r, new Color(t.col.r, t.col.g, t.col.b, 0.75f), 16, 1.5f);
            RR(new Rect(r.x + 14, r.y + 18, 26, 26), new Color(t.col.r, t.col.g, t.col.b, 0.22f), 13);
            RR(new Rect(r.x + 22, r.y + 26, 10, 10), t.col, 5);
            T(new Rect(r.x + 54, r.y + 8, w - 66, 24), t.title, 16, Txt, TextAnchor.MiddleLeft, true);
            T(new Rect(r.x + 54, r.y + 32, w - 66, 22), t.body, 13, Dim, TextAnchor.MiddleLeft);
            GUI.color = og;
            y += h + 8;
        }
    }

    void Hints()
    {
        string s = "WASD drive / click floor to go    Shift boost    Right-drag orbit    Wheel zoom    C camera    SPACE auto demo    R routine    G goodnight    H hazard    T reset";
        float cx = (W - 540 - 370) * 0.5f + 370;
        Rect r = new Rect(cx - 460, Hh - 50, 920, 24);
        T(r, s, 12, new Color(1, 1, 1, 0.55f), TextAnchor.MiddleCenter, true);
        if (d.hazard) { RR(new Rect(cx - 90, Hh - 78, 180, 22), new Color(1f, 0.42f, 0.42f, 0.25f), 11); T(new Rect(cx - 90, Hh - 78, 180, 22), "HAZARD INJECTED", 11, DemoDirector.Red, TextAnchor.MiddleCenter, true); }
    }

    void ProposalCard()
    {
        var p = d.proposal;
        if (!p.open) return;
        float a = Mathf.Clamp01((Time.time - p.openT) / 0.35f);
        var og = GUI.color; GUI.color = new Color(1, 1, 1, a);
        float w = 640, h = 130 + p.lines.Count * 32 + 70;
        float cx = (W - 540 - 370) * 0.5f + 370;
        Rect r = new Rect(cx - w / 2, 220 + (1f - a) * 20, w, h);
        Fill(new Rect(0, 0, W, Hh), new Color(0, 0, 0, 0.38f * a));
        RR(r, new Color(0.04f, 0.055f, 0.085f, 0.97f), 22);
        RRo(r, new Color(DemoDirector.Blue.r, DemoDirector.Blue.g, DemoDirector.Blue.b, 0.8f), 22, 2);
        T(new Rect(r.x + 28, r.y + 20, w - 56, 20), "S3  ROUTINE PROPOSAL", 13, DemoDirector.Blue, TextAnchor.MiddleLeft, true);
        T(new Rect(r.x + 28, r.y + 42, w - 56, 38), p.title, 30, Txt, TextAnchor.MiddleLeft, true);
        T(new Rect(r.x + 28, r.y + 82, w - 56, 22), "When: " + p.trigger, 14, Dim, TextAnchor.MiddleLeft);
        float y = r.y + 118;
        for (int i = 0; i < p.lines.Count; i++)
        {
            Rect lr = new Rect(r.x + 28, y, w - 56, 28);
            RR(lr, new Color(1, 1, 1, 0.05f), 9);
            T(new Rect(lr.x + 12, lr.y, 30, 28), (i + 1).ToString(), 13, DemoDirector.Blue, TextAnchor.MiddleLeft, true);
            T(new Rect(lr.x + 38, lr.y, lr.width - 50, 28), p.lines[i], 14, Txt, TextAnchor.MiddleLeft);
            y += 32;
        }
        Rect ok = new Rect(r.xMax - 28 - 170, r.yMax - 58, 170, 40);
        Rect no = new Rect(ok.x - 130, ok.y, 120, 40);
        bool clicked = Time.time - p.clickT < 0.5f && p.clickT > 0;
        RR(ok, clicked ? DemoDirector.Green : new Color(DemoDirector.Blue.r, DemoDirector.Blue.g, DemoDirector.Blue.b, 0.9f), 20);
        T(ok, clicked ? "Approved" : "Approve", 16, new Color(0.02f, 0.05f, 0.1f), TextAnchor.MiddleCenter, true);
        RRo(no, new Color(1, 1, 1, 0.3f), 20);
        T(no, "Later", 15, Dim, TextAnchor.MiddleCenter, true);
        if (clicked)
        {
            float k = (Time.time - p.clickT) / 0.5f;
            GUI.DrawTexture(Scale(new Rect(ok.center.x - 10, ok.center.y - 10, 20, 20), 1f + k * 5f), glowTex, ScaleMode.StretchToFill, true, 0, new Color(1, 1, 1, 0.5f * (1 - k)), 0, 0);
        }
        if (GUI.Button(ok, GUIContent.none, GUIStyle.none)) d.ApproveProposal();
        if (GUI.Button(no, GUIContent.none, GUIStyle.none)) d.DeclineProposal();
        GUI.color = og;
    }

    void Intro()
    {
        Fill(new Rect(0, 0, W, Hh), new Color(0.02f, 0.03f, 0.05f, 0.80f));
        float cx = W * 0.5f, cy = Hh * 0.5f;
        T(new Rect(cx - 500, cy - 160, 1000, 40), "LG CLOiD  ·  SOFTWARE ENGINEERING PHASE 2", 16, DemoDirector.Cyan, TextAnchor.MiddleCenter, true);
        T(new Rect(cx - 500, cy - 120, 1000, 90), "Bringing legacy controls\ninto proactive routines", 54, Txt, TextAnchor.MiddleCenter, true);
        T(new Rect(cx - 480, cy + 30, 960, 60), "The robot finds wall switches with its vision module, maps them to lamps, and operates them when a routine fires.\nWireless first, physical fallback second. Seven stages, three phases.", 18, Dim, TextAnchor.MiddleCenter, false, true);
        if (Btn(new Rect(cx - 190, cy + 120, 180, 48), "Start auto demo", DemoDirector.Cyan, true)) { d.introVisible = false; d.StartAuto(); }
        if (Btn(new Rect(cx + 10, cy + 120, 180, 48), "Drive manually", DemoDirector.Mute)) d.introVisible = false;
        T(new Rect(cx - 400, cy + 190, 800, 24), "SPACE auto demo     ENTER drive manually", 13, new Color(1, 1, 1, 0.5f), TextAnchor.MiddleCenter, true);
    }

    void EndCard()
    {
        float cx = (W - 540 - 370) * 0.5f + 370;
        string[] l = { "Wireless first: " + d.wirelessOk + " ok, " + d.wirelessFail + " failed", "Physical fallback: " + d.physicalOk + " ok", "Safety cancels: " + d.cancelled };
        Rect r = new Rect(cx - 210, 210, 420, 40 + l.Length * 26 + 14);
        RR(r, new Color(0.04f, 0.055f, 0.085f, 0.92f), 18); RRo(r, Edge, 18);
        T(new Rect(r.x + 20, r.y + 8, 380, 28), "RUN SUMMARY", 13, DemoDirector.Green, TextAnchor.MiddleLeft, true);
        for (int i = 0; i < l.Length; i++) T(new Rect(r.x + 20, r.y + 40 + i * 26, 380, 24), l[i], 15, Txt, TextAnchor.MiddleLeft);
    }
}
