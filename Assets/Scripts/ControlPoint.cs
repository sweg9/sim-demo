using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ControlKind { Rocker, Push, Dial, Decoy }
public enum CtrlStatus { Unseen, Detected, Candidate, Verified, InRoutine }

/// <summary>A lamp group that a wall control switches on and off.</summary>
public class RoomLight : MonoBehaviour
{
    public string roomName;
    public List<Light> lights = new List<Light>();
    public List<float> baseIntensity = new List<float>();
    public List<Renderer> glow = new List<Renderer>();
    public Color glowColor = new Color(1f, 0.78f, 0.45f);
    public float glowBoost = 2.2f;
    public float level;
    public Vector3 anchor;

    float cur;
    public float Current { get { return cur; } }
    public bool On { get { return level > 0.05f; } }

    public void AddLight(Light l)
    {
        lights.Add(l);
        baseIntensity.Add(l.intensity);
        l.intensity = 0f;
        l.enabled = false;
    }

    void Update()
    {
        if (!dirty && Mathf.Approximately(cur, level)) return;
        cur = Mathf.MoveTowards(cur, level, Time.deltaTime * 1.6f);
        dirty = !Mathf.Approximately(cur, level);
        for (int i = 0; i < lights.Count; i++)
        {
            lights[i].intensity = baseIntensity[i] * cur;
            lights[i].enabled = cur > 0.002f;
        }
        var e = glowColor * (0.04f + glowBoost * cur);
        for (int i = 0; i < glow.Count; i++) glow[i].material.SetColor("_EmissionColor", e);
    }

    bool dirty = true;
}

/// <summary>
/// A legacy actuator on a wall (rocker switch, push button, dial). It is the thing the robot has to
/// recognize, map to a lamp, and later operate physically.
/// </summary>
public class ControlPoint : MonoBehaviour
{
    public string id, label, room, typeName, channelNote;
    public ControlKind kind;
    public RoomLight lamp;
    public float standoff = 0.62f;
    public float onLevel = 1f;

    public CtrlStatus status = CtrlStatus.Unseen;
    public float conf;
    public float mapConf;
    public string lampName;

    // wireless channels registered for the lamp (S2) and how they behave in the demo
    public bool hasWifi, hasIr, hasBt;
    public bool wifiFails, irFails, btFails;

    // channel success estimates (learning loop)
    public float pPhys = 0.80f, pWifi = 0.62f, pIr = 0.55f, pBt = 0.50f;
    public int runs;

    // runtime visibility (set by the director)
    public bool visibleNow;
    public float visConf;
    public float seenT;
    public float visTime;
    public Vector2 screenMin, screenMax;

    bool on;
    Transform moving;
    Material ledMat;
    float restAngle = 8f;

    public bool On { get { return on; } }
    public bool IsDecoy { get { return kind == ControlKind.Decoy; } }
    public Vector3 Normal { get { return transform.forward; } }
    public Vector3 Surface { get { return transform.position + transform.forward * 0.02f; } }

    public Vector3[] Corners(float half = 0.058f)
    {
        var r = new Vector3[8];
        int i = 0;
        for (int a = -1; a <= 1; a += 2)
            for (int b = -1; b <= 1; b += 2)
                for (int c = 0; c <= 1; c++)
                    r[i++] = transform.position + transform.right * (a * half) + transform.up * (b * half) + transform.forward * (c * 0.03f);
        return r;
    }

    /// <summary>Where the fingertip should end up to operate the control.</summary>
    public Vector3 Tip(bool turningOn, float offset = 0f)
    {
        Vector3 p = transform.position;
        Vector3 n = transform.forward;
        switch (kind)
        {
            case ControlKind.Rocker: return p + transform.up * (turningOn ? 0.016f : -0.016f) + n * (0.013f + offset);
            case ControlKind.Push: return p + n * (0.026f + offset);
            default: return p + transform.up * 0.022f + n * (0.032f + offset);
        }
    }

    public void Build()
    {
        if (kind == ControlKind.Decoy) { BuildDecoy(); return; }
        ModelLib.Make("Switches", "Switch_Plate", transform, false, false);
        string part = kind == ControlKind.Rocker ? "Switch_Rocker" : kind == ControlKind.Push ? "Switch_Push" : "Switch_Dial";
        var g = ModelLib.Make("Switches", part, transform, false, false);
        moving = g.transform;
        if (kind == ControlKind.Rocker) moving.localRotation = Quaternion.Euler(restAngle, 0, 0);
        var led = ModelLib.Make("Switches", "Switch_Led", transform, false, false);
        ledMat = MatLib.Create("m_led");
        ledMat.SetColor("_EmissionColor", new Color(0.04f, 0.05f, 0.06f));
        ledMat.color = new Color(0.30f, 0.32f, 0.35f);
        led.GetComponent<Renderer>().sharedMaterial = ledMat;
    }

    void BuildDecoy()
    {
        var plate = Util.Prim(PrimitiveType.Cube, "thermostat", transform, new Vector3(0, 0, 0.011f), new Vector3(0.1f, 0.1f, 0.022f), MatLib.Get("thermo"), false);
        plate.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var scr = Util.Prim(PrimitiveType.Cube, "screen", transform, new Vector3(0, 0.004f, 0.0225f), new Vector3(0.062f, 0.04f, 0.002f), MatLib.Get("thermo_screen"), false);
        scr.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    public void SetOn(bool v, bool animate = true)
    {
        on = v;
        if (lamp != null) lamp.level = v ? onLevel : 0f;
        if (ledMat != null)
        {
            ledMat.SetColor("_EmissionColor", v ? new Color(0.2f, 1f, 0.6f) * 1.6f : new Color(0.04f, 0.05f, 0.06f));
            ledMat.color = v ? new Color(0.2f, 0.9f, 0.55f) : new Color(0.30f, 0.32f, 0.35f);
        }
        if (animate) { StopAllCoroutines(); StartCoroutine(Anim()); }
        else SnapPose();
    }

    public void Press() { SetOn(!on); }

    void SnapPose()
    {
        if (moving == null) return;
        if (kind == ControlKind.Rocker) moving.localRotation = Quaternion.Euler(on ? -restAngle : restAngle, 0, 0);
        else if (kind == ControlKind.Dial) moving.localRotation = Quaternion.Euler(0, 0, on ? -200f * onLevel : 0f);
    }

    IEnumerator Anim()
    {
        if (moving == null) yield break;
        float t = 0, dur = kind == ControlKind.Dial ? 0.7f : 0.16f;
        Quaternion fromR = moving.localRotation;
        Vector3 basePos = kind == ControlKind.Rocker ? moving.localPosition : moving.localPosition;
        float z0 = moving.localPosition.z;
        if (kind == ControlKind.Push) z0 = 0.012f;
        Quaternion toR = fromR;
        if (kind == ControlKind.Rocker) toR = Quaternion.Euler(on ? -restAngle : restAngle, 0, 0);
        if (kind == ControlKind.Dial) toR = Quaternion.Euler(0, 0, on ? -200f * onLevel : 0f);
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0, 1, t / dur);
            if (kind == ControlKind.Push)
            {
                float d = Mathf.Sin(k * Mathf.PI) * 0.008f;
                moving.localPosition = new Vector3(basePos.x, basePos.y, z0 - d);
            }
            else moving.localRotation = Quaternion.Slerp(fromR, toR, k);
            yield return null;
        }
        if (kind == ControlKind.Push) moving.localPosition = new Vector3(basePos.x, basePos.y, z0);
        else moving.localRotation = toR;
    }
}
