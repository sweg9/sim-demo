using UnityEngine;

/// <summary>Small helpers for building the scene out of primitives.</summary>
public static class Util
{
    public const int MinimapLayer = 8;
    public const int RobotLayer = 2; // "Ignore Raycast": the robot never blocks its own line-of-sight tests

    static Shader sh;

    public static Material Mat(Color c, float smooth = 0.3f, float metal = 0f, Color? emit = null)
    {
        if (sh == null)
        {
            sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            if (sh == null) sh = Shader.Find("Sprites/Default");
        }
        var m = new Material(sh);
        m.color = c;
        m.SetFloat("_Smoothness", smooth);
        m.SetFloat("_Glossiness", smooth);
        m.SetFloat("_Metallic", metal);
        if (emit.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emit.Value);
        }
        return m;
    }

    public static GameObject Prim(PrimitiveType t, string name, Transform parent, Vector3 pos, Vector3 scale, Material m, bool collider = true)
    {
        var g = GameObject.CreatePrimitive(t);
        g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        g.GetComponent<Renderer>().sharedMaterial = m;
        if (!collider)
        {
            var c = g.GetComponent<Collider>();
            if (c != null) Object.Destroy(c);
        }
        return g;
    }

    public static void SetLayerRecursive(GameObject g, int layer)
    {
        g.layer = layer;
        foreach (Transform t in g.transform) SetLayerRecursive(t.gameObject, layer);
    }
}
