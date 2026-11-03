using UnityEngine;

/// <summary>Spawns single parts of the Blender-exported FBX files with the project's own materials.</summary>
public static class ModelLib
{
    public static GameObject Make(string file, string part, Transform parent, bool collider = false, bool cast = true)
    {
        var root = Resources.Load<GameObject>("Models/" + file);
        Transform s = root != null ? root.transform.Find(part) : null;
        var g = new GameObject(part);
        g.transform.SetParent(parent, false);
        if (s == null) { Debug.LogError("Missing model part " + file + "/" + part); return g; }
        g.transform.localPosition = s.localPosition;
        g.transform.localRotation = s.localRotation;
        var smf = s.GetComponent<MeshFilter>();
        var smr = s.GetComponent<MeshRenderer>();
        g.AddComponent<MeshFilter>().sharedMesh = smf.sharedMesh;
        var mr = g.AddComponent<MeshRenderer>();
        var src = smr.sharedMaterials;
        var mats = new Material[src.Length];
        for (int i = 0; i < src.Length; i++)
            mats[i] = src[i] != null && MatLib.Has(src[i].name) ? MatLib.Get(src[i].name) : src[i];
        mr.sharedMaterials = mats;
        if (!cast) mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (collider)
        {
            var b = smf.sharedMesh.bounds;
            var bc = g.AddComponent<BoxCollider>();
            bc.center = b.center;
            bc.size = b.size;
        }
        return g;
    }
}
