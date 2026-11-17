using System.Collections.Generic;
using UnityEngine;

/// <summary>IMGUI overlay: third-person half with stage bar, minimap and captions; first-person camera and process log half.</summary>
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
    static readonly Color RightBg = new Color(0.035f, 0.045f, 0.06f, 1f);

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
            case CtrlStatus.Pending: return "PENDING";
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
            case CtrlStatus.Pending: return DemoDirector.Amber;
            case CtrlStatus.Verified: return DemoDirector.Green;
            case CtrlStatus.InRoutine: return DemoDirector.Cyan;
            default: return new Color(0.5f, 0.55f, 0.62f);
        }
    }

    // ------------------------------------------------------------------ main
    // Layout: the left half is the third-person view (main camera viewport) with a compact stage bar,
    // the minimap and captions; the right half is the robot's first-person camera above the process log.
    float LW { get { return Mathf.Round(W * 0.5f); } }
    const float TopY = 80;          // first row under the header

    void OnGUI()
    {
        if (d == null || robot == null) return;
        S = Screen.height / 1080f;
        GUI.matrix = Matrix4x4.Scale(new Vector3(S, S, 1f));
        W = Screen.width / S; Hh = 1080f;

        if (Event.current.type == EventType.Repaint || Event.current.type == EventType.MouseDown || Event.current.type == EventType.MouseUp || Event.current.type == EventType.Layout)
        {
            Fill(new Rect(LW, 0, W - LW, Hh), RightBg);
            Fill(new Rect(LW - 1, 0, 2, Hh), new Color(1, 1, 1, 0.08f));
            WorldTags();
            Header();
            LeftCards();
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

    // ------------------------------------------------------------------ left half: header, stages, cards
    void Header()
    {
        float x = 20, y = 16;
        Rect chip = new Rect(x, y, 300, 48);
        RR(chip, Panel, 12); RRo(chip, Edge, 12);
        Fill(new Rect(x + 14, y + 12, 3, 24), DemoDirector.Cyan);
        T(new Rect(x + 26, y + 4, 260, 24), robot.Title, 20, Txt, TextAnchor.MiddleLeft, true);
        T(new Rect(x + 26, y + 26, 260, 18), d.missionRunning ? d.missionName + "  ·  " + Clock(Time.time - d.missionT0) : "Legacy control integration demo", 12, d.missionRunning ? DemoDirector.Cyan : Dim, TextAnchor.MiddleLeft);
        float bx = chip.xMax + 12, by = y + 9, bh = 30;
        if (Btn(new Rect(bx, by, 112, bh), d.missionRunning && d.auto ? "Stop" : "Quick demo", DemoDirector.Cyan, d.auto && d.missionRunning))
        { if (d.missionRunning && d.auto) d.CancelMission("Demo stopped"); else if (!d.missionRunning) d.StartQuick(); }
        if (Btn(new Rect(bx + 120, by, 96, bh), "Full demo", DemoDirector.Blue)) { if (!d.missionRunning) d.StartAuto(); }
        if (Btn(new Rect(bx + 224, by, 72, bh), "Reset", DemoDirector.Mute)) d.ResetWorld();
        if (Btn(new Rect(bx + 304, by, 84, bh), "Robot", DemoDirector.Mute)) { if (!d.missionRunning) { d.ResetWorld(); d.introVisible = true; } }
    }

    static string Clock(float t) { return ((int)(t / 60)).ToString("00") + ":" + ((int)(t % 60)).ToString("00"); }

    void LeftCards()
    {
        float x = 20, y = TopY;
        if (d.probeOpen) { ProbeCard(new Rect(x, y, 330, 36 + 4 * 44)); y += 36 + 4 * 44 + 12; }
        if (d.safetyOpen) SafetyCard(new Rect(x, y, 330, 36 + 4 * 38));
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
        Card(r, "SAFETY GUARD", DemoDirector.Amber, "risk low");
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

    // ------------------------------------------------------------------ right column
    void RightColumn()
    {
        float x = LW + 16, w = W - LW - 32, y = 16;
        float fh = (w - 20) * 0.625f;              // robot feed is 1024 x 640
        FeedCard(new Rect(x, y, w, 36 + fh + 10));
        y += 36 + fh + 10 + 12;
        LogCard(new Rect(x, y, w, Hh - y - 16));
    }

    void FeedCard(Rect r)
    {
        Card(r, "FIRST-PERSON  ·  ROBOT CAM  ·  " + robot.UnitId, DemoDirector.Cyan, d.missionRunning ? d.missionName.ToUpper() : "MANUAL");
        Rect fr = new Rect(r.x + 10, r.y + 36, r.width - 20, r.height - 46);
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

    void LogCard(Rect r)
    {
        int cur = -1;
        for (int i = 6; i >= 0; i--) if (d.stageState[i] == 1) { cur = i; break; }
        Card(r, "PROCESS LOG", cur >= 0 ? DemoDirector.StageColor(cur) : DemoDirector.Mute,
            cur >= 0 ? "Now:  S" + (cur + 1) + "  ·  " + DemoDirector.StageName[cur] : "mapping · wireless · physical · verify");
        float y = r.yMax - 12;
        float w = r.width - 28;
        float textX = r.x + 230, textW = r.xMax - 14 - textX;
        for (int i = d.log.Count - 1; i >= 0 && y > r.y + 40; i--)
        {
            var l = d.log[i];
            float h = Mathf.Max(22, TH(l.text, 15, textW));
            y -= h + 6;
            if (y < r.y + 38) break;
            Color tc = l.col;
            Rect tag = new Rect(r.x + 14, y + 1, 150, 20);
            Color sc = TagColor(l.tag);
            RR(tag, new Color(sc.r, sc.g, sc.b, 0.22f), 6);
            T(new Rect(tag.x + 8, tag.y, tag.width - 12, tag.height), TagLabel(l.tag), 11, sc, TextAnchor.MiddleLeft, true);
            T(new Rect(r.x + 172, y, 56, 22), l.time, 12, Dim, TextAnchor.MiddleLeft);
            float alpha = i == d.log.Count - 1 ? 1f : 0.88f;
            if (i == d.log.Count - 1) RR(new Rect(r.x + 8, y - 3, r.width - 16, h + 6), new Color(1, 1, 1, 0.05f), 8);
            T(new Rect(textX, y, textW, h), l.text, 15, new Color(tc.r, tc.g, tc.b, alpha) * (tc == DemoDirector.Mute ? 1.15f : 1f), TextAnchor.UpperLeft, false, true);
        }
    }

    // "S5" -> "S5  Wireless", "SAFE" -> "Safety Guard"
    static string TagLabel(string tag)
    {
        switch (tag)
        {
            case "S1": return "S1  Recognize";
            case "S2": return "S2  Map & verify";
            case "S3": return "S3  Routine";
            case "S4": return "S4  Trigger";
            case "S5": return "S5  Wireless";
            case "S6": return "S6  Physical";
            case "S7": return "S7  Learn";
            case "SAFE": return "Safety Guard";
            default: return tag;
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
            if (r.xMax > LW - 8 || r.x < 0 || r.y < TopY) continue;
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
        float x = 20 + 360 + 16, w = LW - 20 - x;
        float bh = TH(d.capBody, 15, w - 56);
        float h = 46 + bh + 14;
        Rect r = new Rect(x, Hh - 44 - h + (1f - a) * 14, w, h);
        int cur = -1;
        for (int i = 6; i >= 0; i--) if (d.stageState[i] == 1) { cur = i; break; }
        Color acc = cur >= 0 ? DemoDirector.StageColor(cur) : DemoDirector.Cyan;
        var og = GUI.color; GUI.color = new Color(1, 1, 1, a);
        RR(r, new Color(0.03f, 0.045f, 0.07f, 0.86f), 16);
        RRo(r, new Color(acc.r, acc.g, acc.b, 0.55f), 16, 1.5f);
        Fill(new Rect(r.x + 18, r.y + 16, 4, h - 32), acc);
        T(new Rect(r.x + 34, r.y + 10, w - 50, 32), d.capHead, 22, Txt, TextAnchor.MiddleLeft, true);
        T(new Rect(r.x + 34, r.y + 44, w - 56, bh + 4), d.capBody, 15, Dim, TextAnchor.UpperLeft, false, true);
        GUI.color = og;
    }

    void Toasts()
    {
        float w0 = 420, cx = LW - 20 - w0 / 2;
        float y = TopY;
        foreach (var t in d.toasts)
        {
            float age = Time.time - t.t0;
            float a = Mathf.Clamp01(age / 0.25f) * Mathf.Clamp01((t.dur - age) / 0.4f);
            float w = w0, h = 62;
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
        string s = "WASD drive   C camera   SPACE quick demo   L full demo   H hazard   M robot   T reset";
        float x = 20 + 360 + 16, cx = (x + LW - 20) * 0.5f;
        Rect r = new Rect(x, Hh - 36, LW - 20 - x, 22);
        T(r, s, 11, new Color(1, 1, 1, 0.55f), TextAnchor.MiddleCenter, true);
        if (d.hazard) { RR(new Rect(cx - 90, Hh - 64, 180, 22), new Color(1f, 0.42f, 0.42f, 0.25f), 11); T(new Rect(cx - 90, Hh - 64, 180, 22), "HAZARD INJECTED", 11, DemoDirector.Red, TextAnchor.MiddleCenter, true); }
    }

    void ProposalCard()
    {
        var p = d.proposal;
        if (!p.open) return;
        float a = Mathf.Clamp01((Time.time - p.openT) / 0.35f);
        var og = GUI.color; GUI.color = new Color(1, 1, 1, a);
        float w = Mathf.Min(640, LW - 40), h = 130 + p.lines.Count * 32 + 70;
        float cx = LW * 0.5f;
        Rect r = new Rect(cx - w / 2, 220 + (1f - a) * 20, w, h);
        Fill(new Rect(0, 0, LW, Hh), new Color(0, 0, 0, 0.38f * a));
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

    // start screen: choose the robot, then start the auto demo or drive manually
    void Intro()
    {
        Fill(new Rect(0, 0, W, Hh), new Color(0.02f, 0.03f, 0.05f, 0.84f));
        float cx = W * 0.5f, cy = Hh * 0.5f;
        T(new Rect(cx - 500, cy - 300, 1000, 30), "LG CLOiD  ·  SOFTWARE ENGINEERING PHASE 2", 16, DemoDirector.Cyan, TextAnchor.MiddleCenter, true);
        T(new Rect(cx - 600, cy - 268, 1200, 56), "Bringing legacy controls into proactive routines", 42, Txt, TextAnchor.MiddleCenter, true);
        T(new Rect(cx - 480, cy - 206, 960, 26), "Choose the robot for this run. Both run the same seven stages; only the body and the way it presses differ.", 16, Dim, TextAnchor.MiddleCenter);

        float cw = 420, ch = 250, gap = 28, top = cy - 160;
        RobotCard(new Rect(cx - gap * 0.5f - cw, top, cw, ch), RobotModel.Cloid, "1",
            new[] { "Two articulated arms with IK reach", "Round wheeled base, pan / tilt head camera", "The arm presses switches and turns dials" });
        RobotCard(new Rect(cx + gap * 0.5f, top, cw, ch), RobotModel.Tracked, "2",
            new[] { "Two tracks, one motor per track", "Carriage rides a vertical rail to switch height", "Piston rod extends straight ahead to press" });

        float by = top + ch + 30;
        if (Btn(new Rect(cx - 290, by, 180, 48), "Quick demo  ·  30 s", DemoDirector.Cyan, true)) { d.introVisible = false; d.StartQuick(); }
        if (Btn(new Rect(cx - 90, by, 180, 48), "Full demo", DemoDirector.Blue)) { d.introVisible = false; d.StartAuto(); }
        if (Btn(new Rect(cx + 110, by, 180, 48), "Drive manually", DemoDirector.Mute)) d.introVisible = false;
        T(new Rect(cx - 450, by + 64, 900, 24), "1 / 2 choose robot     SPACE quick demo     L full demo     ENTER drive manually     M back to this screen", 13, new Color(1, 1, 1, 0.5f), TextAnchor.MiddleCenter, true);
    }

    void RobotCard(Rect r, RobotModel m, string key, string[] lines)
    {
        bool sel = robot.model == m;
        bool hover = r.Contains(Event.current.mousePosition);
        Color acc = DemoDirector.Cyan;
        RR(r, sel ? new Color(acc.r, acc.g, acc.b, 0.16f) : new Color(1, 1, 1, hover ? 0.08f : 0.04f), 18);
        RRo(r, sel ? new Color(acc.r, acc.g, acc.b, 0.95f) : new Color(1, 1, 1, hover ? 0.35f : 0.18f), 18, sel ? 2f : 1f);
        Rect kr = new Rect(r.x + 22, r.y + 22, 30, 30);
        RR(kr, sel ? acc : new Color(1, 1, 1, 0.12f), 8);
        T(kr, key, 15, sel ? new Color(0.02f, 0.05f, 0.1f) : Txt, TextAnchor.MiddleCenter, true);
        T(new Rect(r.x + 64, r.y + 18, r.width - 90, 36), RobotRig.ModelName(m), 26, Txt, TextAnchor.MiddleLeft, true);
        T(new Rect(r.x + 24, r.y + 62, r.width - 48, 22), RobotRig.ModelTagline(m), 14, sel ? acc : Dim, TextAnchor.MiddleLeft, true);
        float y = r.y + 100;
        foreach (var l in lines)
        {
            Fill(new Rect(r.x + 26, y + 9, 5, 5), sel ? acc : Dim);
            T(new Rect(r.x + 40, y, r.width - 64, 24), l, 15, Txt, TextAnchor.MiddleLeft);
            y += 30;
        }
        T(new Rect(r.x + 24, r.yMax - 40, r.width - 48, 22), sel ? "SELECTED" : "Click to select", 12, sel ? acc : new Color(1, 1, 1, 0.45f), TextAnchor.MiddleLeft, true);
        if (!sel && GUI.Button(r, GUIContent.none, GUIStyle.none)) { Sfx.Play("ui"); d.ChooseRobot(m); }
    }

    void EndCard()
    {
        float cx = LW * 0.5f;
        string[] l = { "Result: " + (d.cancelled > 0 ? "cancelled" : d.wirelessOk + d.physicalOk > 0 ? "success" : "failure"), "WIFI / IR / BT: " + d.wirelessOk + " success, " + d.wirelessFail + " failure", "ARM (physical press): " + d.physicalOk + " success", "Safety Guard cancels: " + d.cancelled };
        Rect r = new Rect(20, TopY, 420, 40 + l.Length * 26 + 14);   // top left, clear of the toasts
        RR(r, new Color(0.04f, 0.055f, 0.085f, 0.92f), 18); RRo(r, Edge, 18);
        T(new Rect(r.x + 20, r.y + 8, 380, 28), "RUN SUMMARY", 13, DemoDirector.Green, TextAnchor.MiddleLeft, true);
        for (int i = 0; i < l.Length; i++) T(new Rect(r.x + 20, r.y + 40 + i * 26, 380, 24), l[i], 15, Txt, TextAnchor.MiddleLeft);
    }
}
