using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The CLOiD-style robot: Blender parts assembled at runtime, differential-drive style locomotion on the
/// A* grid, a pan/tilt head with the camera that feeds the HUD, and two-bone IK arms for pressing controls.
/// </summary>
public class RobotRig : MonoBehaviour
{
    class Arm
    {
        public Transform upper, fore, hand;
        public float side;           // +1 = right arm (model "_N" at +x)
        public float w;
        public Vector3 tip;
        public Vector3 restLocal;
    }

    public const float L1 = 0.40f, L2 = 0.38f, LH = 0.13f;

    public Camera eyeCam;
    public RenderTexture feed;
    public Transform body, head;
    public CharacterController cc;

    public float maxSpeed = 1.15f, accel = 1.5f, maxTurn = 150f;
    public float speed;
    public bool busy;

    Arm right, left;
    float desiredSpeed, desiredTurn, yawRate;
    float headYaw, headPitch, headYawV, headPitchV;
    float scanAmp, scanFreq = 1.7f;
    Vector3? lookAt;
    Material ledMat;
    Color mood = new Color(0.2f, 0.9f, 1f);
    float moodPulse = 0.35f;
    float lastSpeed;
    Quaternion tilt = Quaternion.identity;
    Light underglow;
    float t0;

    public Vector3 Pos { get { return transform.position; } }
    public Vector3 Forward { get { return transform.forward; } }

    // ------------------------------------------------------------------ build
    Transform Part(string name, Transform parent, Vector3 parentFbx, out Vector3 fbx)
    {
        var g = ModelLib.Make("CLOiD", name, parent, false);
        fbx = g.transform.localPosition;
        g.transform.localPosition = fbx - parentFbx;
        return g.transform;
    }

    public void Build()
    {
        cc = gameObject.AddComponent<CharacterController>();
        cc.radius = 0.32f; cc.height = 1.55f; cc.center = new Vector3(0, 0.8f, 0);
        cc.stepOffset = 0.04f; cc.skinWidth = 0.02f; cc.minMoveDistance = 0f;

        Vector3 fbx;
        Part("CLOiD_Base", transform, Vector3.zero, out fbx);
        body = new GameObject("Body").transform;
        body.SetParent(transform, false);
        body.localPosition = new Vector3(0, 0.17f, 0);
        Vector3 bodyFbx = new Vector3(0, 0.17f, 0);
        Part("CLOiD_Torso", body, bodyFbx, out fbx);
        head = Part("CLOiD_Head", body, bodyFbx, out fbx);

        right = BuildArm("N", +1f, bodyFbx);
        left = BuildArm("P", -1f, bodyFbx);

        // camera in the lens
        var cg = new GameObject("EyeCam");
        cg.transform.SetParent(head, false);
        cg.transform.localPosition = new Vector3(0, 0.216f, 0.135f);
        cg.transform.localRotation = Quaternion.Euler(7f, 0, 0);
        eyeCam = cg.AddComponent<Camera>();
        eyeCam.fieldOfView = 62f;
        eyeCam.nearClipPlane = 0.04f;
        eyeCam.farClipPlane = 40f;
        eyeCam.allowMSAA = true;
        feed = new RenderTexture(1024, 640, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, filterMode = FilterMode.Bilinear, name = "RobotFeed" };
        eyeCam.targetTexture = feed;
        eyeCam.depth = -5;

        // shared, animated LED material (cyan lines, eyes and base ring)
        ledMat = MatLib.Create("m_led");
        foreach (var r in GetComponentsInChildren<MeshRenderer>())
        {
            var mats = r.sharedMaterials;
            bool ch = false;
            for (int i = 0; i < mats.Length; i++)
                if (mats[i] != null && mats[i].name == "m_led") { mats[i] = ledMat; ch = true; }
            if (ch) r.sharedMaterials = mats;
        }

        var ug = new GameObject("underglow");
        ug.transform.SetParent(transform, false);
        ug.transform.localPosition = new Vector3(0, 0.12f, 0);
        underglow = ug.AddComponent<Light>();
        underglow.type = LightType.Point; underglow.range = 1.9f; underglow.intensity = 0.55f;
        underglow.color = new Color(0.3f, 0.85f, 1f); underglow.shadows = LightShadows.None;
        underglow.renderMode = LightRenderMode.ForcePixel;

        Util.SetLayerRecursive(gameObject, Util.RobotLayer);
        t0 = UnityEngine.Random.value * 10f;
    }

    Arm BuildArm(string tag, float side, Vector3 bodyFbx)
    {
        var a = new Arm { side = side };
        Vector3 uF, fF, hF;
        a.upper = Part("CLOiD_Upper_" + tag, body, bodyFbx, out uF);
        a.fore = Part("CLOiD_Fore_" + tag, a.upper, uF, out fF);
        a.hand = Part("CLOiD_Hand_" + tag, a.fore, fF, out hF);
        a.restLocal = transform.InverseTransformPoint(a.hand.position + Vector3.down * LH);
        a.tip = a.hand.position;
        return a;
    }

    public void SetLayer(int layer) { Util.SetLayerRecursive(gameObject, layer); }

    // ------------------------------------------------------------------ teleport / mood
    public void Teleport(Vector3 p, float yaw)
    {
        cc.enabled = false;
        transform.position = new Vector3(p.x, 0, p.z);
        transform.rotation = Quaternion.Euler(0, yaw, 0);
        cc.enabled = true;
        speed = 0; yawRate = 0; desiredSpeed = 0; desiredTurn = 0;
    }

    public void SetMood(Color c, float pulse = 0.35f) { mood = c; moodPulse = pulse; }

    public void Drive(float fwd, float turn)
    {
        desiredSpeed = fwd * maxSpeed;
        desiredTurn = turn * maxTurn;
    }

    public void LookAt(Vector3? p) { lookAt = p; }
    public void Scan(float amplitudeDeg) { scanAmp = amplitudeDeg; }
    public void LookForward() { lookAt = null; scanAmp = 0; }

    // ------------------------------------------------------------------ update
    void Update()
    {
        float dt = Time.deltaTime;
        speed = Mathf.MoveTowards(speed, desiredSpeed, accel * dt);
        yawRate = Mathf.MoveTowards(yawRate, desiredTurn, 520f * dt);
        transform.Rotate(0, yawRate * dt, 0);
        if (Mathf.Abs(speed) > 0.001f) cc.Move(transform.forward * (speed * dt));

        // body lean from acceleration
        float acc = dt > 0 ? (speed - lastSpeed) / dt : 0;
        lastSpeed = speed;
        Quaternion want = Quaternion.Euler(Mathf.Clamp(acc * 1.6f, -3.2f, 3.2f), 0, Mathf.Clamp(-yawRate * Mathf.Abs(speed) * 0.018f, -3f, 3f));
        tilt = Quaternion.Slerp(tilt, want, 1 - Mathf.Exp(-9f * dt));
        body.localRotation = tilt;

        // LED mood
        float k = 1f + moodPulse * Mathf.Sin((Time.time + t0) * 3.2f);
        ledMat.SetColor("_EmissionColor", mood * (1.6f * k));
        ledMat.color = mood * 0.5f;
        underglow.color = Color.Lerp(Color.white, mood, 0.7f);
        underglow.intensity = 0.45f + 0.15f * k;
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        // head
        float ty, tp;
        if (lookAt.HasValue)
        {
            Vector3 loc = body.parent.InverseTransformPoint(lookAt.Value) - body.localPosition - head.localPosition;
            loc = Quaternion.Inverse(body.localRotation) * loc;
            ty = Mathf.Atan2(loc.x, loc.z) * Mathf.Rad2Deg;
            tp = -Mathf.Atan2(loc.y, new Vector2(loc.x, loc.z).magnitude) * Mathf.Rad2Deg;
        }
        else
        {
            float tt = Time.time + t0;
            ty = Mathf.Sin(tt * 0.45f) * 2.5f + (scanAmp > 0 ? Mathf.Sin(tt * scanFreq) * scanAmp : 0f);
            tp = 4f + Mathf.Sin(tt * 0.33f) * 1.5f;
        }
        ty = Mathf.Clamp(ty, -85f, 85f); tp = Mathf.Clamp(tp, -28f, 38f);
        headYaw = Mathf.SmoothDampAngle(headYaw, ty, ref headYawV, 0.16f);
        headPitch = Mathf.SmoothDampAngle(headPitch, tp, ref headPitchV, 0.16f);
        head.localRotation = Quaternion.Euler(headPitch, headYaw, 0);

        SolveArm(right);
        SolveArm(left);
    }

    // ------------------------------------------------------------------ arm IK
    static Quaternion Aim(Vector3 limbDir, Vector3 hint, Vector3 fallback)
    {
        Vector3 z = Vector3.ProjectOnPlane(hint, limbDir);
        if (z.sqrMagnitude < 1e-4f) z = Vector3.ProjectOnPlane(fallback, limbDir);
        return Quaternion.LookRotation(z.normalized, -limbDir);
    }

    void SolveArm(Arm a)
    {
        Quaternion rest = body.rotation;
        if (a.w < 0.001f)
        {
            a.upper.rotation = rest; a.fore.localRotation = Quaternion.identity; a.hand.localRotation = Quaternion.identity;
            return;
        }
        Vector3 S = a.upper.position;
        Vector3 F = a.tip;
        Vector3 d0 = F - S;
        Vector3 dir0 = d0.sqrMagnitude > 1e-6f ? d0.normalized : Vector3.down;
        Vector3 W = F - dir0 * LH;
        Vector3 dw = W - S;
        float d = Mathf.Clamp(dw.magnitude, 0.22f, L1 + L2 - 0.004f);
        Vector3 dir = dw.sqrMagnitude > 1e-6f ? dw.normalized : Vector3.down;
        Vector3 pole = Vector3.ProjectOnPlane(body.right * a.side * 0.9f + Vector3.down * 0.7f - body.forward * 0.15f, dir).normalized;
        float cosA = Mathf.Clamp((L1 * L1 + d * d - L2 * L2) / (2f * L1 * d), -1f, 1f);
        float sinA = Mathf.Sqrt(1f - cosA * cosA);
        Vector3 E = S + dir * (L1 * cosA) + pole * (L1 * sinA);
        Vector3 W2 = S + dir * d;
        Vector3 uDir = (E - S).normalized, fDir = (W2 - E).normalized, hDir = (F - W2).sqrMagnitude > 1e-6f ? (F - W2).normalized : fDir;

        float flat = Mathf.Abs(Vector3.Dot(dir, body.up));
        Vector3 hint = Vector3.Lerp(body.up, body.forward, flat);
        Quaternion qU = Aim(uDir, hint, body.right), qF = Aim(fDir, hint, body.right), qH = Aim(hDir, hint, body.right);

        a.upper.rotation = Quaternion.Slerp(rest, qU, a.w);
        a.fore.rotation = Quaternion.Slerp(a.upper.rotation, qF, a.w);
        a.hand.rotation = Quaternion.Slerp(a.fore.rotation, qH, a.w);
    }

    Vector3 FingerTip(Arm a) { return a.hand.TransformPoint(new Vector3(0, -LH, 0)); }

    IEnumerator MoveArm(Arm a, Vector3 to, float dur, float wFrom, float wTo)
    {
        Vector3 from = a.tip;
        float t = 0;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / dur));
            a.tip = Vector3.Lerp(from, to, k);
            a.w = Mathf.Lerp(wFrom, wTo, k);
            yield return null;
        }
        a.tip = to; a.w = wTo;
    }

    /// <summary>Raise the right arm, touch the control, operate it, and retract.</summary>
    public IEnumerator Press(ControlPoint cp, bool turningOn)
    {
        var a = right;
        a.tip = FingerTip(a);
        Vector3 pre = cp.Tip(turningOn, 0.20f), near = cp.Tip(turningOn, 0.05f), touch = cp.Tip(turningOn, -0.006f);
        LookAt(cp.Surface);
        yield return MoveArm(a, pre, 1.0f, 0f, 1f);
        yield return MoveArm(a, near, 0.5f, 1f, 1f);
        yield return MoveArm(a, touch, 0.3f, 1f, 1f);
        if (cp.kind == ControlKind.Dial)
        {
            cp.SetOn(turningOn);
            float tgt = turningOn ? -200f * cp.onLevel : 0f, start = turningOn ? 0f : -200f * cp.onLevel;
            float t = 0, dur = 0.7f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float th = Mathf.Lerp(start, tgt, Mathf.SmoothStep(0, 1, t / dur));
                a.tip = cp.transform.TransformPoint(Quaternion.Euler(0, 0, th) * new Vector3(0, 0.022f, 0.026f));
                yield return null;
            }
        }
        else
        {
            cp.SetOn(turningOn);
            yield return new WaitForSeconds(0.22f);
        }
        Vector3 back = cp.Tip(turningOn, 0.05f);
        yield return MoveArm(a, back, 0.35f, 1f, 1f);
        yield return MoveArm(a, pre, 0.4f, 1f, 1f);
        yield return MoveArm(a, transform.TransformPoint(a.restLocal), 0.9f, 1f, 0f);
        a.w = 0f;
    }

    public void RelaxArms() { StartCoroutine(Relax()); }

    IEnumerator Relax()
    {
        float w0 = Mathf.Max(right.w, left.w), t = 0;
        float r0 = right.w, l0 = left.w;
        while (t < 0.6f)
        {
            t += Time.deltaTime;
            float k = 1f - Mathf.SmoothStep(0, 1, t / 0.6f);
            right.w = r0 * k; left.w = l0 * k;
            yield return null;
        }
        right.w = 0; left.w = 0;
    }

    /// <summary>Capability probe: lift the left arm and extend it forward, then retract.</summary>
    public IEnumerator ReachTest()
    {
        var a = left;
        a.tip = FingerTip(a);
        Vector3 fwd = transform.TransformPoint(new Vector3(-0.30f, 1.20f, 0.62f));
        yield return MoveArm(a, fwd, 1.0f, 0f, 1f);
        yield return new WaitForSeconds(0.25f);
        Vector3 up = transform.TransformPoint(new Vector3(-0.40f, 1.55f, 0.42f));
        yield return MoveArm(a, up, 0.7f, 1f, 1f);
        yield return MoveArm(a, fwd, 0.6f, 1f, 1f);
        yield return MoveArm(a, transform.TransformPoint(a.restLocal), 0.7f, 1f, 0f);
        a.w = 0f;
    }

    // ------------------------------------------------------------------ navigation
    public IEnumerator GoTo(Vector3 target, Func<bool> cancel = null, float arrive = 0.08f, float cruise = -1f)
    {
        busy = true;
        float vmax = cruise > 0 ? cruise : maxSpeed;
        int replans = 0;
        while (true)
        {
            var path = PathGrid.Find(transform.position, target);
            bool stuck = false;
            for (int i = 0; i < path.Count && !stuck; i++)
            {
                Vector3 wp = path[i];
                bool last = i == path.Count - 1;
                float stall = 0;
                while (true)
                {
                    if (cancel != null && cancel()) { Drive(0, 0); busy = false; yield break; }
                    Vector3 to = wp - transform.position; to.y = 0;
                    float dist = to.magnitude;
                    if (dist < (last ? arrive : 0.28f)) break;
                    float err = Vector3.SignedAngle(transform.forward, to, Vector3.up);
                    float turn = Mathf.Clamp(err * 3.2f, -maxTurn, maxTurn);
                    float remain = dist;
                    for (int j = i + 1; j < path.Count; j++) remain += Vector3.Distance(path[j - 1], path[j]);
                    float vBrake = Mathf.Sqrt(2f * accel * 0.9f * Mathf.Max(0, remain - arrive));
                    float align = Mathf.Clamp01(Mathf.Cos(Mathf.Clamp(Mathf.Abs(err), 0, 90) * Mathf.Deg2Rad));
                    float v = Mathf.Min(vmax, Mathf.Max(0.18f, vBrake)) * (Mathf.Abs(err) > 55f ? 0f : align);
                    Drive(v / maxSpeed, turn / maxTurn);
                    stall = speed < 0.05f && Mathf.Abs(err) < 20f ? stall + Time.deltaTime : 0f;
                    if (stall > 0.8f) { stuck = true; break; }
                    yield return null;
                }
            }
            if (!stuck || ++replans > 3) break;
            // blocked: back off a little and plan again from where we are
            Drive(-0.5f, 0);
            yield return new WaitForSeconds(0.35f);
            Drive(0, 0);
        }
        Drive(0, 0);
        while (speed > 0.02f) yield return null;
        busy = false;
    }

    public IEnumerator TurnTo(float yaw, Func<bool> cancel = null)
    {
        busy = true;
        while (true)
        {
            if (cancel != null && cancel()) break;
            float err = Mathf.DeltaAngle(transform.eulerAngles.y, yaw);
            if (Mathf.Abs(err) < 1.5f && Mathf.Abs(yawRate) < 12f) break;
            Drive(0, Mathf.Clamp(err * 3f, -maxTurn, maxTurn) / maxTurn);
            yield return null;
        }
        Drive(0, 0);
        yawRate = 0;
        busy = false;
    }

    public void Stop() { Drive(0, 0); }
}
