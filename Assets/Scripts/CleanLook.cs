using UnityEngine;

/// <summary>
/// Clean, neutral look for the main camera: a small full-screen colour grade (Resources/Shaders/CleanGrade).
/// Does nothing if the shader is missing, so the scene still renders.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CleanLook : MonoBehaviour
{
    public float exposure = 1.35f;
    public Color tint = new Color(1.02f, 1.0f, 0.96f);   // slightly warm white balance
    [Range(0, 1.5f)] public float saturation = 0.88f;
    public float contrast = 1.04f;
    public Color lift = new Color(0.035f, 0.038f, 0.045f); // lifted, very slightly cool blacks
    [Range(0, 1)] public float vignette = 0.18f;

    Material mat;

    void OnEnable()
    {
        var sh = Resources.Load<Shader>("Shaders/CleanGrade");
        if (sh != null && sh.isSupported) mat = new Material(sh) { hideFlags = HideFlags.DontSave };
    }

    void OnDisable()
    {
        if (mat != null) Destroy(mat);
        mat = null;
    }

    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        if (mat == null) { Graphics.Blit(src, dst); return; }
        mat.SetFloat("_Exposure", exposure);
        mat.SetVector("_Tint", (Vector4)tint);      // raw values, no sRGB conversion
        mat.SetFloat("_Saturation", saturation);
        mat.SetFloat("_Contrast", contrast);
        mat.SetVector("_Lift", (Vector4)lift);
        mat.SetFloat("_Vignette", vignette);
        Graphics.Blit(src, dst, mat);
    }
}
