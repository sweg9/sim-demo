using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class PatrolStop
{
    public Vector3 pos;
    public float yaw;
    public string room;
    public PatrolStop(float x, float z, float yaw, string room) { pos = new Vector3(x, 0, z); this.yaw = yaw; this.room = room; }
}

/// <summary>Builds the whole modern house: shell, floors, furniture (Blender models), lights and wall controls.</summary>
public class House : MonoBehaviour
{
    public const float H = 2.7f;

    public List<ControlPoint> controls = new List<ControlPoint>();
    public List<RoomLight> lamps = new List<RoomLight>();
    public List<PatrolStop> patrol = new List<PatrolStop>();
    public List<ReflectionProbe> probes = new List<ReflectionProbe>();
    public Transform dock;
    public Vector3 dockPos;
    public List<GameObject> ceilingObjs = new List<GameObject>();
    public Dictionary<string, Transform> wallGroups = new Dictionary<string, Transform>();
    public Rect footprint = new Rect(0, 0, 13, 9);
    public Light sky;

    Transform root;
    Material mWall, mFrame, mBase;

    struct Opening
    {
        public float s0, s1, y0, y1;
        public int mullions;
        public bool glass, frame;
        public Opening(float s0, float s1, float y0, float y1, int mullions = 0, bool glass = false, bool frame = true)
        {
            this.s0 = s0; this.s1 = s1; this.y0 = y0; this.y1 = y1; this.mullions = mullions; this.glass = glass; this.frame = frame;
        }
    }

    // ------------------------------------------------------------------ helpers
    GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Material m, bool collider = true, bool shadow = true)
    {
        var g = Util.Prim(PrimitiveType.Cube, name, parent, center, size, m, collider);
        if (!shadow) g.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        return g;
    }

    Transform Group(string name)
    {
        var g = new GameObject(name);
        g.transform.SetParent(root, false);
        return g.transform;
    }

    void Solid(Transform grp, bool alongX, float line, float t, float s0, float s1, float y0, float y1, Material m)
    {
        if (s1 - s0 < 0.001f || y1 - y0 < 0.001f) return;
        float c = (s0 + s1) * 0.5f, len = s1 - s0, yc = (y0 + y1) * 0.5f, hy = y1 - y0;
        if (alongX) Box("wall", grp, new Vector3(c, yc, line), new Vector3(len, hy, t), m);
        else Box("wall", grp, new Vector3(line, yc, c), new Vector3(t, hy, len), m);
    }

    void BuildWall(Transform grp, bool alongX, float line, float a0, float a1, float t, Material m, params Opening[] ops)
    {
        System.Array.Sort(ops, (p, q) => p.s0.CompareTo(q.s0));
        float cur = a0;
        foreach (var o in ops)
        {
            Solid(grp, alongX, line, t, cur, o.s0, 0, H, m);
            if (o.y0 > 0.001f) Solid(grp, alongX, line, t, o.s0, o.s1, 0, o.y0, m);
            if (o.y1 < H - 0.001f) Solid(grp, alongX, line, t, o.s0, o.s1, o.y1, H, m);
            if (o.frame) Frame(grp, alongX, line, t, o);
            if (o.glass)
            {
                float c = (o.s0 + o.s1) * 0.5f, len = o.s1 - o.s0, yc = (o.y0 + o.y1) * 0.5f, hy = o.y1 - o.y0;
                var gl = new GameObject("glass");
                gl.transform.SetParent(grp, false);
                var bc = gl.AddComponent<BoxCollider>();
                if (alongX) { gl.transform.localPosition = new Vector3(c, yc, line); bc.size = new Vector3(len, hy, 0.06f); }
                else { gl.transform.localPosition = new Vector3(line, yc, c); bc.size = new Vector3(0.06f, hy, len); }
            }
            cur = o.s1;
        }
        Solid(grp, alongX, line, t, cur, a1, 0, H, m);
    }

    void Frame(Transform grp, bool alongX, float line, float t, Opening o)
    {
        float d = t + 0.02f, w = 0.04f;
        float len = o.s1 - o.s0, hy = o.y1 - o.y0, yc = (o.y0 + o.y1) * 0.5f, c = (o.s0 + o.s1) * 0.5f;
        System.Action<float, float, float, float> add = (cs, cy, sl, sh) =>
        {
            if (alongX) Box("frame", grp, new Vector3(cs, cy, line), new Vector3(sl, sh, d), mFrame, false);
            else Box("frame", grp, new Vector3(line, cy, cs), new Vector3(d, sh, sl), mFrame, false);
        };
        add(o.s0 + w * 0.5f, yc, w, hy);
        add(o.s1 - w * 0.5f, yc, w, hy);
        add(c, o.y1 - w * 0.5f, len, w);
        if (o.y0 > 0.001f || o.glass) add(c, o.y0 + w * 0.5f, len, w);
        for (int i = 1; i <= o.mullions; i++)
        {
            float s = Mathf.Lerp(o.s0, o.s1, i / (float)(o.mullions + 1));
            add(s, yc, 0.03f, hy);
        }
    }

    Light MkLight(string name, Transform parent, Vector3 pos, Color col, float intensity, float range, bool shadow, float spot = 0f)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.position = pos;
        var l = g.AddComponent<Light>();
        l.color = col;
        l.intensity = intensity;
        l.range = range;
        l.renderMode = LightRenderMode.ForcePixel;
        if (spot > 0f)
        {
            l.type = LightType.Spot;
            l.spotAngle = spot;
            g.transform.rotation = Quaternion.Euler(90, 0, 0);
        }
        else l.type = LightType.Point;
        l.shadows = shadow ? LightShadows.Soft : LightShadows.None;
        if (shadow) { l.shadowStrength = 0.85f; l.shadowBias = 0.03f; l.shadowNormalBias = 0.4f; }
        return l;
    }

    static readonly Color Warm = new Color(1f, 0.79f, 0.56f);
    static readonly Color WarmSoft = new Color(1f, 0.85f, 0.68f);

    RoomLight NewLamp(string room, Vector3 anchor)
    {
        var g = new GameObject("Lamp_" + room);
        g.transform.SetParent(root, false);
        var rl = g.AddComponent<RoomLight>();
        rl.roomName = room;
        rl.anchor = anchor;
        lamps.Add(rl);
        return rl;
    }

    GameObject Furn(string part, float x, float z, float yaw, bool collider, float y = 0f, string file = "Furniture")
    {
        var g = ModelLib.Make(file, part, furn, collider);
        g.transform.position = new Vector3(x, y, z);
        g.transform.rotation = Quaternion.Euler(0, yaw, 0);
        return g;
    }

    Transform furn;

    GameObject Glow(string part, GameObject body, RoomLight rl)
    {
        var g = ModelLib.Make("Furniture", part, furn, false, false);
        g.transform.position = body.transform.position;
        g.transform.rotation = body.transform.rotation;
        g.transform.localScale = body.transform.localScale;
        rl.glow.Add(g.GetComponent<Renderer>());
        return g;
    }

    GameObject Strip(RoomLight rl, Vector3 center, float length, float yaw)
    {
        var g = ModelLib.Make("Furniture", "F_LedStrip_Glow", furn, false, false);
        g.transform.position = center;
        g.transform.rotation = Quaternion.Euler(0, yaw, 0);
        g.transform.localScale = new Vector3(length, 1, 1);
        rl.glow.Add(g.GetComponent<Renderer>());
        return g;
    }

    GameObject Disc(RoomLight rl, Vector3 pos)
    {
        var g = Util.Prim(PrimitiveType.Cylinder, "downlight", furn, pos, new Vector3(0.15f, 0.006f, 0.15f), MatLib.Get("lamp_disc"), false);
        g.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        g.layer = Util.MinimapLayer;
        rl.glow.Add(g.GetComponent<Renderer>());
        return g;
    }

    ControlPoint MakeControl(string id, string label, string room, string typeName, ControlKind kind, Vector3 pos, Vector3 normal,
                             RoomLight lamp, string lampName, float onLevel = 1f)
    {
        var g = new GameObject(id);
        g.transform.SetParent(root, false);
        g.transform.position = pos;
        g.transform.rotation = Quaternion.LookRotation(normal, Vector3.up);
        var cp = g.AddComponent<ControlPoint>();
        cp.id = id; cp.label = label; cp.room = room; cp.typeName = typeName; cp.kind = kind;
        cp.lamp = lamp; cp.lampName = lampName; cp.onLevel = onLevel;
        cp.Build();
        controls.Add(cp);
        return cp;
    }

    Texture2D PlankTex()
    {
        int n = 512;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, true);
        var px = new Color32[n * n];
        var rnd = new System.Random(11);
        int rows = 6, rh = n / rows;
        for (int r = 0; r < rows; r++)
        {
            int off = rnd.Next(0, n);
            int seg = n / 2;
            for (int s = 0; s < 2; s++)
            {
                float tone = 0.88f + (float)rnd.NextDouble() * 0.22f;
                float warm = (float)rnd.NextDouble() * 0.05f;
                int x0 = (off + s * seg) % n;
                for (int xx = 0; xx < seg; xx++)
                {
                    int x = (x0 + xx) % n;
                    for (int y = 0; y < rh; y++)
                    {
                        int yy = r * rh + y;
                        float grain = 0.5f + 0.5f * Mathf.Sin((y * 0.9f + Mathf.PerlinNoise(xx * 0.01f + s * 7, r * 3.1f) * 14f) * 1.3f);
                        float v = tone * (0.95f + 0.05f * grain) * (0.97f + 0.06f * Mathf.PerlinNoise(xx * 0.05f + r, y * 0.3f));
                        float edge = (y < 2 || y > rh - 3 || xx < 2) ? 0.62f : 1f;
                        px[yy * n + x] = new Color(0.80f * v * edge + warm, 0.66f * v * edge + warm * 0.6f, 0.47f * v * edge);
                    }
                }
            }
        }
        t.SetPixels32(px);
        t.wrapMode = TextureWrapMode.Repeat;
        t.filterMode = FilterMode.Bilinear;
        t.anisoLevel = 8;
        t.Apply(true);
        return t;
    }

    Texture2D TileTex()
    {
        int n = 256;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, true);
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int tx = x / (n / 2), ty = y / (n / 2);
                float tone = 0.86f + 0.1f * Mathf.PerlinNoise(tx * 3.7f + 1, ty * 2.3f + 2) + 0.03f * Mathf.PerlinNoise(x * 0.07f, y * 0.07f);
                bool grout = (x % (n / 2) < 2) || (y % (n / 2) < 2);
                float v = grout ? 0.42f : tone;
                px[y * n + x] = new Color(v * 0.80f, v * 0.80f, v * 0.79f);
            }
        t.SetPixels32(px);
        t.wrapMode = TextureWrapMode.Repeat;
        t.anisoLevel = 8;
        t.Apply(true);
        return t;
    }

    void Floor(string name, float x0, float z0, float x1, float z1, Material m, float tile)
    {
        var inst = new Material(m);
        inst.mainTextureScale = new Vector2((x1 - x0) / tile, (z1 - z0) / tile);
        var g = Box(name, root, new Vector3((x0 + x1) * 0.5f, -0.05f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, 0.1f, z1 - z0), inst, false);
        g.layer = 2;
    }

    void Art(Vector3 pos, Vector3 normal, float w, float h, string mat, string wall)
    {
        var g = new GameObject("art");
        g.transform.SetParent(wallGroups[wall], false);
        g.transform.position = pos;
        g.transform.rotation = Quaternion.LookRotation(normal, Vector3.up);
        Box("frame", g.transform, new Vector3(0, 0, 0.012f), new Vector3(w + 0.04f, h + 0.04f, 0.024f), mFrame, false);
        Box("canvas", g.transform, new Vector3(0, 0, 0.026f), new Vector3(w, h, 0.006f), MatLib.Get(mat), false);
    }

    // ------------------------------------------------------------------ build
    public void Build()
    {
        root = new GameObject("House").transform;
        root.SetParent(transform, false);
        furn = Group("furniture");
        mWall = MatLib.Get("wall_white");
        mFrame = MatLib.Get("frame");
        mBase = MatLib.Get("base_white");

        BuildShell();
        BuildFloors();
        BuildOutside();
        BuildLiving();
        BuildKitchen();
        BuildHall();
        BuildBedroom();
        BuildPatrol();
        BuildProbes();

        // everything that is not a floor and not a lamp should block path planning: colliders already exist
    }

    void BuildShell()
    {
        float T = 0.22f, t = 0.12f;
        var south = Group("Wall_South"); wallGroups["S"] = south;
        var north = Group("Wall_North"); wallGroups["N"] = north;
        var west = Group("Wall_West"); wallGroups["W"] = west;
        var east = Group("Wall_East"); wallGroups["E"] = east;
        var inner = Group("Walls_Inner");

        // exterior walls (inner face on the footprint edge)
        BuildWall(south, true, -T / 2, -T, 13 + T, T, mWall,
            new Opening(0.9f, 6.3f, 0.0f, 2.45f, 3, true));
        BuildWall(north, true, 9 + T / 2, -T, 13 + T, T, mWall,
            new Opening(1.0f, 4.2f, 1.25f, 2.3f, 2, true));
        BuildWall(west, false, -T / 2, -T, 9 + T, T, mWall,
            new Opening(5.6f, 8.4f, 0.9f, 2.4f, 2, true));
        BuildWall(east, false, 13 + T / 2, -T, 9 + T, T, mWall,
            new Opening(1.5f, 2.5f, 0.0f, 2.2f, 0, false, true), new Opening(5.3f, 8.1f, 0.4f, 2.4f, 1, true));

        // interior walls
        BuildWall(inner, false, 8f, 0f, 9f, t, mWall, new Opening(1.3f, 2.5f, 0f, 2.1f));
        BuildWall(inner, true, 4.2f, 8f + t / 2, 13f, t, mWall, new Opening(9.9f, 11.1f, 0f, 2.1f));

        // entrance door (east wall of the hall)
        Box("door_panel", east, new Vector3(13.06f, 1.08f, 2.0f), new Vector3(0.06f, 2.16f, 0.94f), MatLib.Get("door_dark"), true);
        Box("door_handle", east, new Vector3(12.965f, 1.05f, 2.32f), new Vector3(0.03f, 0.03f, 0.55f), MatLib.Get("m_brass"), false);

        // accent walls
        Box("tv_wall", west, new Vector3(0.012f, 1.35f, 2.6f), new Vector3(0.024f, 2.7f, 3.4f), MatLib.Get("wall_dark"), false, false);
        Box("bed_wall", north, new Vector3(11f, 1.35f, 8.988f), new Vector3(3.6f, 2.7f, 0.024f), MatLib.Get("wall_blue"), false, false);

        // baseboards
        float bh = 0.08f;
        Box("bb", south, new Vector3(6.5f, bh / 2, 0.012f), new Vector3(13f, bh, 0.024f), mBase, false, false);
        Box("bb", north, new Vector3(6.5f, bh / 2, 8.988f), new Vector3(13f, bh, 0.024f), mBase, false, false);
        Box("bb", west, new Vector3(0.012f, bh / 2, 4.5f), new Vector3(0.024f, bh, 9f), mBase, false, false);
        Box("bb", east, new Vector3(12.988f, bh / 2, 4.5f), new Vector3(0.024f, bh, 9f), mBase, false, false);

        // ceiling slab (layer 8: hidden from the map camera and from high orbit cameras)
        var ceil = Box("ceiling", root, new Vector3(6.5f, H + 0.06f, 4.5f), new Vector3(13.6f, 0.12f, 9.6f), MatLib.Get("ceiling"), false);
        ceil.layer = Util.MinimapLayer;
        ceilingObjs.Add(ceil);
    }

    void BuildFloors()
    {
        var oak = Util.Mat(Color.white, 0.38f);
        oak.mainTexture = PlankTex();
        var tile = Util.Mat(Color.white, 0.6f);
        tile.mainTexture = TileTex();
        Floor("floor_living", 0, 0, 8f, 9f, oak, 1.2f);
        Floor("floor_bed", 8f, 4.2f, 13f, 9f, oak, 1.2f);
        Floor("floor_hall", 8f, 0, 13f, 4.2f, tile, 1.2f);
        var found = Box("foundation", root, new Vector3(6.5f, -0.2f, 4.5f), new Vector3(14f, 0.2f, 10f), MatLib.Get("concrete"), false);
        found.layer = 2;
    }

    void BuildOutside()
    {
        var o = Group("outside");
        var ground = Box("ground", o, new Vector3(6.5f, -0.35f, 4.5f), new Vector3(120f, 0.2f, 120f), MatLib.Get("ground"), false, false);
        ground.layer = 2;
        var deck = Box("deck", o, new Vector3(3.6f, -0.12f, -1.9f), new Vector3(9f, 0.24f, 3.5f), MatLib.Get("deck"), false);
        deck.layer = 2;
        var rnd = new System.Random(5);
        for (int i = 0; i < 16; i++)
        {
            float a = i / 16f * Mathf.PI * 2f;
            float rx = 18f + (float)rnd.NextDouble() * 6f, rz = 16f + (float)rnd.NextDouble() * 6f;
            Vector3 p = new Vector3(6.5f + Mathf.Cos(a) * rx, 0, 4.5f + Mathf.Sin(a) * rz);
            float h = 5f + (float)rnd.NextDouble() * 4f;
            var tr = Util.Prim(PrimitiveType.Cylinder, "trunk", o, p + Vector3.up * h * 0.25f, new Vector3(0.35f, h * 0.25f, 0.35f), MatLib.Get("trunk"), false);
            var cr = Util.Prim(PrimitiveType.Sphere, "crown", o, p + Vector3.up * h * 0.7f, new Vector3(h * 0.7f, h * 0.8f, h * 0.7f), MatLib.Get("foliage"), false);
            cr.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            tr.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }
        // a pair of low planters on the deck
        for (int i = 0; i < 2; i++)
            Util.Prim(PrimitiveType.Cylinder, "planter", o, new Vector3(1.0f + i * 5.3f, 0.28f, -1.2f), new Vector3(0.7f, 0.28f, 0.7f), MatLib.Get("m_blackmetal"), false);
    }

    // ------------------------------------------------------------------ living
    void BuildLiving()
    {
        var living = NewLamp("Living room", new Vector3(3.6f, 2.0f, 2.6f));
        var nook = NewLamp("Reading nook", new Vector3(0.9f, 1.5f, 4.2f));
        nook.glowBoost = 2.0f;

        Furn("F_Rug", 3.0f, 2.6f, 90f, false);
        var tvu = Furn("F_TVUnit", 0.236f, 2.6f, 90f, true);
        Furn("F_TV", 0.026f, 2.6f, 90f, false);
        Furn("F_Sofa", 4.75f, 2.6f, 270f, true);
        Furn("F_CoffeeTable", 2.85f, 2.6f, 0f, true);
        Furn("F_Armchair", 2.75f, 4.55f, 180f, true);
        Furn("F_SideTable", 4.75f, 4.35f, 0f, true);
        Furn("F_Vase", 4.75f, 4.35f, 0f, false, 0.5f);
        Furn("F_Vase", 0.236f, 3.35f, 0f, false, 0.57f);
        Furn("F_Plant", 0.55f, 0.55f, 40f, true);
        Furn("F_PlantSmall", 0.30f, 3.95f, 0f, false, 0f);

        // reading nook: armchair + floor lamp
        Furn("F_Armchair", 1.65f, 4.95f, 205f, true);
        var fl = Furn("F_FloorLamp", 0.75f, 4.15f, 0f, true);
        Glow("F_FloorLamp_Glow", fl, nook);
        var nl = MkLight("nook_light", nook.transform, new Vector3(0.78f, 1.5f, 4.15f), Warm, 1.1f, 4.2f, true);
        nook.AddLight(nl);

        // living: TV bias strip + six downlights
        Strip(living, new Vector3(0.05f, 0.88f, 2.6f), 1.6f, 90f);
        var bias = MkLight("tv_bias", living.transform, new Vector3(0.5f, 0.85f, 2.6f), new Color(1f, 0.7f, 0.45f), 0.9f, 2.6f, false);
        living.AddLight(bias);
        Vector3[] dl = {
            new Vector3(2.0f, 2.68f, 1.2f), new Vector3(4.4f, 2.68f, 1.2f), new Vector3(6.4f, 2.68f, 1.4f),
            new Vector3(2.0f, 2.68f, 3.9f), new Vector3(4.4f, 2.68f, 3.9f), new Vector3(6.4f, 2.68f, 3.9f) };
        for (int i = 0; i < dl.Length; i++)
        {
            Disc(living, dl[i]);
            var s = MkLight("dl" + i, living.transform, dl[i] - Vector3.up * 0.05f, Warm, 1.1f, 5.2f, i == 1 || i == 4, 105f);
            living.AddLight(s);
        }

        // art and shelf detail

        // wall controls (S1 targets)
        var cp1 = MakeControl("SW-01", "Living ceiling light", "Living room", "Rocker switch", ControlKind.Rocker,
            new Vector3(7.94f, 1.25f, 3.0f), Vector3.left, living, "Living lights");
        cp1.hasWifi = true; cp1.wifiFails = true; cp1.channelNote = "Wi-Fi smart plug";
        cp1.pWifi = 0.66f; cp1.pPhys = 0.82f;
        var cp5 = MakeControl("SW-05", "Reading lamp dimmer", "Living room", "Rotary dial", ControlKind.Dial,
            new Vector3(0.0f, 1.25f, 4.75f), Vector3.right, nook, "Reading lamp", 0.65f);
        cp5.hasBt = true; cp5.btFails = true; cp5.channelNote = "BT lamp bulb";
        cp5.pBt = 0.55f; cp5.pPhys = 0.78f;
        var dec = MakeControl("PN-X", "Thermostat panel", "Living room", "Wall panel", ControlKind.Decoy,
            new Vector3(7.94f, 1.5f, 3.6f), Vector3.left, null, "");

        // charging dock
        dockPos = new Vector3(6.85f, 0, 0.62f);
        var d = Furn("F_Dock", dockPos.x, dockPos.z, 0f, false);
        d.AddComponent<BoxCollider>().center = new Vector3(0, 0.275f, -0.46f);
        d.GetComponent<BoxCollider>().size = new Vector3(0.24f, 0.55f, 0.07f);
        var dg = ModelLib.Make("Furniture", "F_Dock_Glow", furn, false, false);
        dg.transform.position = d.transform.position;
        dock = d.transform;
    }

    // ------------------------------------------------------------------ kitchen + dining
    void BuildKitchen()
    {
        var kit = NewLamp("Kitchen & dining", new Vector3(3.6f, 2.0f, 7.2f));
        Furn("F_Counter", 2.6f, 8.66f, 180f, true);
        Furn("F_Fridge", 5.25f, 8.615f, 180f, true);
        Furn("F_Island", 3.0f, 6.5f, 0f, true);
        for (int i = 0; i < 3; i++) Furn("F_Stool", 2.2f + i * 0.8f, 5.6f, 0f, true);
        Furn("F_Vase", 3.8f, 6.5f, 0f, false, 0.93f);

        var dt = Furn("F_DiningTable", 6.45f, 7.2f, 90f, true);
        for (int i = 0; i < 2; i++)
        {
            Furn("F_DiningChair", 5.62f, 6.75f + i * 0.9f, 90f, true);
            Furn("F_DiningChair", 7.3f, 6.75f + i * 0.9f, 270f, true);
        }
        Furn("F_RugRound", 6.45f, 7.0f, 0f, false);
        Furn("F_Plant", 7.45f, 8.5f, 0f, true);

        // three pendants over the island, one over the dining table
        Vector3[] pp = { new Vector3(2.2f, H, 6.5f), new Vector3(3.0f, H, 6.5f), new Vector3(3.8f, H, 6.5f), new Vector3(6.45f, H, 7.2f) };
        for (int i = 0; i < pp.Length; i++)
        {
            var p = Furn("F_Pendant", pp[i].x, pp[i].z, 0f, false, H);
            Glow("F_Pendant_Glow", p, kit);
            var l = MkLight("pend" + i, kit.transform, new Vector3(pp[i].x, H - 0.98f, pp[i].z), Warm, 1.5f, 4.4f, false, 125f);
            kit.AddLight(l);
        }
        // backsplash window light
        Strip(kit, new Vector3(2.6f, 1.0f, 8.97f), 3.8f, 0f);
        var uc = MkLight("under", kit.transform, new Vector3(2.6f, 1.05f, 8.55f), WarmSoft, 0.9f, 2.5f, false);
        kit.AddLight(uc);
        Art(new Vector3(6.4f, 1.7f, 9.0f), Vector3.back, 1.1f, 0.8f, "art_teal", "N");

        var cp2 = MakeControl("SW-02", "Kitchen pendants", "Kitchen & dining", "Push button", ControlKind.Push,
            new Vector3(7.94f, 1.25f, 5.55f), Vector3.left, kit, "Kitchen pendants");
        cp2.hasIr = true; cp2.channelNote = "IR hub";
        cp2.pIr = 0.74f; cp2.pPhys = 0.80f;
    }

    // ------------------------------------------------------------------ hall
    void BuildHall()
    {
        var hall = NewLamp("Hall", new Vector3(10.5f, 2.0f, 2.0f));
        Furn("F_Console", 11.2f, 0.2f, 0f, true);
        Furn("F_Vase", 10.8f, 0.2f, 0f, false, 0.815f);
        Furn("F_PlantSmall", 12.45f, 0.55f, 0f, false);
        Furn("F_Stool", 12.35f, 3.4f, 0f, true);
        for (int i = 0; i < 2; i++)
        {
            float x = i == 0 ? 10.25f : 12.15f;
            var sc = Furn("F_Sconce", x, 0.05f, 0f, false, 1.6f);
            Glow("F_Sconce_Glow", sc, hall);
            var l = MkLight("sconce" + i, hall.transform, new Vector3(x, 1.65f, 0.35f), Warm, 0.9f, 2.6f, false);
            hall.AddLight(l);
        }
        Strip(hall, new Vector3(10.5f, 2.64f, 0.06f), 4.6f, 0f);
        Strip(hall, new Vector3(10.5f, 2.64f, 4.1f), 4.6f, 0f);
        for (int i = 0; i < 2; i++)
        {
            var l = MkLight("hall" + i, hall.transform, new Vector3(9.6f + i * 2.4f, 2.45f, 2.1f), WarmSoft, 1.05f, 4.4f, false);
            hall.AddLight(l);
        }
        Art(new Vector3(13.0f, 1.6f, 3.4f), Vector3.left, 0.9f, 0.9f, "art_sand", "E");

        var cp3 = MakeControl("SW-03", "Hall lights dimmer", "Hall", "Rotary dial", ControlKind.Dial,
            new Vector3(9.35f, 1.25f, 0.0f), Vector3.forward, hall, "Hall lights", 0.6f);
        cp3.channelNote = "none (physical only)";
        cp3.pPhys = 0.84f;
    }

    // ------------------------------------------------------------------ bedroom
    void BuildBedroom()
    {
        var bed = NewLamp("Bedroom", new Vector3(10.6f, 1.6f, 7.0f));
        Furn("F_Bed", 11.0f, 7.86f, 180f, true);
        Furn("F_RugRound", 11.0f, 6.0f, 0f, false);
        for (int i = 0; i < 2; i++)
        {
            float x = i == 0 ? 9.55f : 12.45f;
            Furn("F_Nightstand", x, 8.78f, 180f, true);
            var bl = Furn("F_BedsideLamp", x, 8.78f, 0f, false, 0.53f);
            Glow("F_BedsideLamp_Glow", bl, bed);
            var l = MkLight("bedside" + i, bed.transform, new Vector3(x, 0.95f, 8.7f), Warm, 1.05f, 3.8f, i == 0);
            bed.AddLight(l);
        }
        Furn("F_Wardrobe", 8.41f, 6.8f, 90f, true);
        Furn("F_Plant", 8.6f, 8.5f, 0f, true);
        Strip(bed, new Vector3(11.0f, 2.2f, 8.96f), 3.2f, 0f);
        var wash = MkLight("headwall", bed.transform, new Vector3(11f, 2.0f, 8.4f), WarmSoft, 0.8f, 3.2f, false);
        bed.AddLight(wash);
        var ceil = MkLight("bed_ceiling", bed.transform, new Vector3(10.6f, 2.55f, 6.3f), WarmSoft, 1.0f, 5.2f, false, 110f);
        bed.AddLight(ceil);
        Art(new Vector3(11.0f, 1.55f, 9.0f), Vector3.back, 1.7f, 0.8f, "art_sand", "N");

        var cp4 = MakeControl("SW-04", "Bedroom lights", "Bedroom", "Rocker switch", ControlKind.Rocker,
            new Vector3(11.6f, 1.25f, 4.26f), Vector3.forward, bed, "Bedroom lights");
        cp4.hasWifi = true; cp4.channelNote = "Wi-Fi smart plug";
        cp4.pWifi = 0.78f; cp4.pPhys = 0.82f;
    }

    void BuildPatrol()
    {
        patrol.Add(new PatrolStop(5.9f, 2.3f, 90f, "Living room"));
        patrol.Add(new PatrolStop(3.6f, 4.1f, 270f, "Living room"));
        patrol.Add(new PatrolStop(5.0f, 5.5f, 90f, "Kitchen & dining"));
        patrol.Add(new PatrolStop(9.7f, 2.4f, 180f, "Hall"));
        patrol.Add(new PatrolStop(11.0f, 5.9f, 180f, "Bedroom"));
    }

    void BuildProbes()
    {
        Vector3[,] spec = { { new Vector3(4f, 1.5f, 4.5f), new Vector3(8f, 3f, 9f) }, { new Vector3(10.5f, 1.5f, 2.1f), new Vector3(5f, 3f, 4.2f) },
                            { new Vector3(10.5f, 1.5f, 6.6f), new Vector3(5f, 3f, 4.8f) } };
        for (int i = 0; i < 3; i++)
        {
            var g = new GameObject("probe" + i);
            g.transform.SetParent(root, false);
            g.transform.position = spec[i, 0];
            var p = g.AddComponent<ReflectionProbe>();
            p.size = spec[i, 1];
            p.boxProjection = true;
            p.resolution = 128;
            p.mode = ReflectionProbeMode.Realtime;
            p.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            p.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            p.importance = 1;
            p.intensity = 0.9f;
            p.nearClipPlane = 0.1f;
            p.farClipPlane = 30f;
            p.cullingMask = ~((1 << Util.MinimapLayer) | (1 << 2));
            probes.Add(p);
        }
    }

    public void RefreshProbes() { foreach (var p in probes) p.RenderProbe(); }
}
