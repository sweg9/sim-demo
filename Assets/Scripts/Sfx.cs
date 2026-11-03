using System.Collections.Generic;
using UnityEngine;

/// <summary>Tiny procedural UI sounds so the demo has feedback without any audio assets.</summary>
public static class Sfx
{
    static AudioSource src;
    static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();

    public static void Init(GameObject host)
    {
        src = host.AddComponent<AudioSource>();
        src.spatialBlend = 0f;
        src.volume = 0.35f;
        clips["detect"] = Seq("detect", new[] { 880f, 1320f }, 0.075f, 0.9f);
        clips["ok"] = Seq("ok", new[] { 523f, 659f, 784f }, 0.1f, 0.85f);
        clips["fail"] = Seq("fail", new[] { 196f, 147f }, 0.16f, 0.9f, true);
        clips["tick"] = Seq("tick", new[] { 1500f }, 0.03f, 0.5f);
        clips["ui"] = Seq("ui", new[] { 700f, 940f }, 0.05f, 0.6f);
        clips["press"] = Seq("press", new[] { 260f }, 0.05f, 1f, true);
    }

    static AudioClip Seq(string name, float[] freqs, float each, float amp, bool saw = false)
    {
        int rate = 44100, per = Mathf.RoundToInt(each * rate), n = per * freqs.Length;
        var data = new float[n];
        for (int s = 0; s < freqs.Length; s++)
            for (int i = 0; i < per; i++)
            {
                float t = i / (float)rate, ph = t * freqs[s];
                float w = saw ? 2f * (ph - Mathf.Floor(ph + 0.5f)) * 0.6f : Mathf.Sin(2 * Mathf.PI * ph);
                float env = Mathf.Exp(-5f * i / per) * Mathf.Min(1f, i / 90f);
                data[s * per + i] = w * env * amp * 0.5f;
            }
        var c = AudioClip.Create(name, n, 1, rate, false);
        c.SetData(data, 0);
        return c;
    }

    public static void Play(string n)
    {
        AudioClip c;
        if (src != null && clips.TryGetValue(n, out c)) src.PlayOneShot(c);
    }
}
