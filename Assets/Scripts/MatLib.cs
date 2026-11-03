using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Named materials shared by the Blender models and the procedural house. Values mirror the Blender
/// Principled settings (linear colour, roughness, metallic). Baked copies in Resources/Mats are preferred so
/// shader variants (emission) survive player builds.
/// </summary>
public static class MatLib
{
    struct Def { public Color c; public float smooth, metal; public bool emit; public Color e; }

    static Dictionary<string, Def> defs;
    static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

    static Color L(float r, float g, float b) { return new Color(r, g, b).gamma; }

    static void D(string n, Color c, float rough, float metal = 0f)
    {
        defs[n] = new Def { c = c, smooth = 1f - rough, metal = metal };
    }

    static void E(string n, Color c, float rough, Color e, float k)
    {
        defs[n] = new Def { c = c, smooth = 1f - rough, emit = true, e = e * k };
    }

    static void Init()
    {
        if (defs != null) return;
        defs = new Dictionary<string, Def>();
        // robot
        D("m_white", L(0.94f, 0.945f, 0.95f), 0.18f);
        D("m_pearl", L(0.84f, 0.86f, 0.89f), 0.28f, 0.2f);
        D("m_grey", L(0.50f, 0.53f, 0.57f), 0.30f, 0.7f);
        D("m_dark", L(0.025f, 0.03f, 0.04f), 0.06f);
        D("m_base", L(0.09f, 0.10f, 0.115f), 0.45f);
        E("m_led", L(0.10f, 0.80f, 1.00f), 0.3f, new Color(0.2f, 0.9f, 1f), 2.4f);
        // furniture
        D("m_fabric_blue", L(0.38f, 0.44f, 0.52f), 0.92f);
        D("m_fabric_sand", L(0.80f, 0.74f, 0.64f), 0.92f);
        D("m_fabric_grey", L(0.64f, 0.65f, 0.67f), 0.92f);
        D("m_walnut", L(0.30f, 0.19f, 0.12f), 0.42f);
        D("m_oak", L(0.74f, 0.58f, 0.40f), 0.5f);
        D("m_blackmetal", L(0.045f, 0.047f, 0.055f), 0.35f, 0.8f);
        D("m_brass", L(0.80f, 0.62f, 0.32f), 0.28f, 1f);
        D("m_white_matte", L(0.94f, 0.94f, 0.925f), 0.55f);
        D("m_stone", L(0.11f, 0.115f, 0.13f), 0.5f);
        D("m_steel", L(0.72f, 0.74f, 0.77f), 0.3f, 0.9f);
        D("m_ceramic", L(0.96f, 0.95f, 0.93f), 0.18f);
        D("m_leaf", L(0.16f, 0.40f, 0.20f), 0.42f);
        D("m_leaf2", L(0.10f, 0.30f, 0.16f), 0.42f);
        D("m_soil", L(0.07f, 0.055f, 0.045f), 0.9f);
        D("m_rug", L(0.90f, 0.87f, 0.82f), 0.97f);
        E("m_glow", L(1f, 0.85f, 0.6f), 0.3f, new Color(1f, 0.78f, 0.45f), 0.05f);
        D("m_screen", L(0.008f, 0.010f, 0.016f), 0.04f);
        D("m_bedding", L(0.93f, 0.94f, 0.95f), 0.92f);
        D("m_throw", L(0.50f, 0.58f, 0.67f), 0.92f);
        D("m_plate", L(0.96f, 0.96f, 0.95f), 0.4f);
        D("m_teak", L(0.55f, 0.38f, 0.22f), 0.45f);
        // house
        D("wall_white", L(0.84f, 0.83f, 0.81f), 0.88f);
        D("wall_dark", L(0.16f, 0.17f, 0.19f), 0.82f);
        D("wall_blue", L(0.26f, 0.33f, 0.42f), 0.86f);
        D("ceiling", L(0.90f, 0.90f, 0.89f), 0.92f);
        D("base_white", L(0.88f, 0.88f, 0.87f), 0.55f);
        D("frame", L(0.03f, 0.032f, 0.038f), 0.35f, 0.6f);
        D("door_dark", L(0.12f, 0.075f, 0.05f), 0.4f);
        D("ground", L(0.045f, 0.075f, 0.05f), 0.96f);
        D("deck", L(0.30f, 0.21f, 0.14f), 0.6f);
        D("foliage", L(0.06f, 0.18f, 0.09f), 0.85f);
        D("trunk", L(0.09f, 0.065f, 0.05f), 0.9f);
        D("concrete", L(0.30f, 0.30f, 0.31f), 0.75f);
        D("art_terra", L(0.70f, 0.34f, 0.22f), 0.8f);
        D("art_navy", L(0.10f, 0.18f, 0.30f), 0.8f);
        D("art_sand", L(0.80f, 0.72f, 0.58f), 0.8f);
        D("art_teal", L(0.12f, 0.38f, 0.36f), 0.8f);
        D("thermo", L(0.9f, 0.9f, 0.9f), 0.35f);
        E("thermo_screen", L(0.02f, 0.03f, 0.04f), 0.1f, new Color(0.3f, 0.7f, 1f), 0.6f);
        E("lamp_disc", L(0.95f, 0.93f, 0.88f), 0.4f, new Color(1f, 0.82f, 0.55f), 0.05f);
    }

    public static IEnumerable<string> Names { get { Init(); return defs.Keys; } }

    public static bool Has(string n) { Init(); return defs.ContainsKey(n); }

    public static Material Create(string n)
    {
        Init();
        Def d = defs[n];
        var m = Util.Mat(d.c, d.smooth, d.metal, d.emit ? (Color?)d.e : null);
        m.name = n;
        return m;
    }

    public static Material Get(string n)
    {
        Material m;
        if (cache.TryGetValue(n, out m) && m != null) return m;
        m = Resources.Load<Material>("Mats/" + n);
        if (m == null) m = Create(n);
        cache[n] = m;
        return m;
    }
}
