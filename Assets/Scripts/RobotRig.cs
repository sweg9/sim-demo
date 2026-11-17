using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Which body the robot is built with. Both share the same driving, head camera and press API.</summary>
public enum RobotModel { Tracked, Cloid }

/// <summary>
/// The demo robot. Two swappable bodies:
/// - Tracked: a tank-tread base with a vertical rail; a carriage on the rail holds the webcam and a piston
///   actuator that presses straight ahead. Built from primitives.
/// - Cloid: the original CLOiD-style robot, Blender parts from CLOiD.fbx with two arms.
/// Both use differential-drive locomotion on the A* grid and a pan/tilt camera that feeds the HUD.
/// </summary>
public class RobotRig : MonoBehaviour
{
    class Arm
    {
        public Transform upper, fore, hand;
        public float side;           // +1 = right arm
        public float w;
        public Vector3 tip;
        public Vector3 restLocal;
        public Vector3 pole;         // elbow direction hint in body space
    }

    class Track
    {
        public float side, phase;
        public Transform[] cleats;
        public Transform[] wheels;
        public float[] wheelR;
        public float spin;
    }

    public const float L1 = 0.40f, L2 = 0.38f, LH = 0.13f;
    const string PrefKey = "robot_model";

    public Camera eyeCam;
    public RenderTexture feed;
    public Transform body, head;
    public CharacterController cc;
    public RobotModel model = RobotModel.Tracked;

    public float maxSpeed = 1.15f, accel = 1.5f, maxTurn = 150f;
    public float speed;
    public bool busy;

    Arm right, left;                 // left is null on the tracked body
    Track trackL, trackR;
    Transform visual;
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
    /// <summary>Sideways offset of the pressing tool from the robot centre (the stand pose shifts by it).</summary>
    public float ToolLateral { get { return model == RobotModel.Cloid ? 0.262f : 0f; } }

    // ------------------------------------------------------------------ model choice
    /// <summary>Saved choice, overridden by the command line flag "-robot tracked|cloid".</summary>
    public static RobotModel ChosenModel()
    {
        var m = RobotModel.Tracked;
        try { if (PlayerPrefs.GetString(PrefKey, "tracked") == "cloid") m = RobotModel.Cloid; } catch (Exception) { }
        var a = Environment.GetCommandLineArgs();
        for (int i = 0; i < a.Length - 1; i++)
            if (a[i] == "-robot") m = a[i + 1].ToLowerInvariant() == "cloid" ? RobotModel.Cloid : RobotModel.Tracked;
        return m;
    }

    public static string ModelName(RobotModel m) { return m == RobotModel.Cloid ? "CLOiD" : "Robot arm demo"; }
    public static string ModelTagline(RobotModel m) { return m == RobotModel.Cloid ? "Life-like home robot with two arms" : "Tracked base, vertical rail and piston actuator"; }

    public string Title { get { return ModelName(model); } }
    public string UnitId { get { return model == RobotModel.Cloid ? "CLOiD-01" : "RAIL-01"; } }
    /// <summary>Plain-language name of what touches the control, for logs and safety rows.</summary>
    public string ToolName { get { return model == RobotModel.Cloid ? "arm" : "actuator"; } }

    /// <summary>Rebuild the body with another model. Call it only when no mission is running.</summary>
    public bool SetModel(RobotModel m)
    {
        model = m;
        try { PlayerPrefs.SetString(PrefKey, m == RobotModel.Cloid ? "cloid" : "tracked"); PlayerPrefs.Save(); } catch (Exception) { }
        BuildModel();
        return true;
    }


    // ------------------------------------------------------------------ build
    public void Build()
    {
        cc = gameObject.AddComponent<CharacterController>();
        cc.radius = 0.32f;
        cc.stepOffset = 0.04f; cc.skinWidth = 0.02f; cc.minMoveDistance = 0f;

        var cg = new GameObject("EyeCam");
        cg.transform.SetParent(transform, false);
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

        var ug = new GameObject("underglow");
        ug.transform.SetParent(transform, false);
        ug.transform.localPosition = new Vector3(0, 0.12f, 0);
        underglow = ug.AddComponent<Light>();
        underglow.type = LightType.Point; underglow.range = 1.9f; underglow.intensity = 0.55f;
        underglow.color = new Color(0.3f, 0.85f, 1f); underglow.shadows = LightShadows.None;
        underglow.renderMode = LightRenderMode.ForcePixel;

        t0 = UnityEngine.Random.value * 10f;
        BuildModel();
    }

    void BuildModel()
    {
        StopAllCoroutines();   // relax / retract tweens of the old body
        if (visual != null)
        {
            eyeCam.transform.SetParent(transform, false);
            visual.gameObject.SetActive(false);
            Destroy(visual.gameObject);
        }
        right = left = null; trackL = trackR = null; carriage = rod = null;
        visual = new GameObject("Model_" + model).transform;
        visual.SetParent(transform, false);
        body = new GameObject("Body").transform;
        body.SetParent(visual, false);

        Vector3 eyePos;
        if (model == RobotModel.Cloid) eyePos = BuildCloid();
        else eyePos = BuildTracked();

        eyeCam.transform.SetParent(head, false);
        eyeCam.transform.localPosition = eyePos;
        eyeCam.transform.localRotation = Quaternion.Euler(7f, 0, 0);

        foreach (var r in visual.GetComponentsInChildren<MeshRenderer>())
        {
            var mats = r.sharedMaterials;
            bool ch = false;
            for (int i = 0; i < mats.Length; i++)
                if (mats[i] != null && mats[i].name == "m_led") { mats[i] = ledMat; ch = true; }
            if (ch) r.sharedMaterials = mats;
        }

        tilt = Quaternion.identity;
        headYaw = headPitch = headYawV = headPitchV = 0f;
        Util.SetLayerRecursive(gameObject, Util.RobotLayer);
    }

    // ---------------------------------------------------------- CLOiD (original)
    Transform Part(string name, Transform parent, Vector3 parentFbx, out Vector3 fbx)
    {
        var g = ModelLib.Make("CLOiD", name, parent, false);
        fbx = g.transform.localPosition;
        g.transform.localPosition = fbx - parentFbx;
        return g.transform;
    }

    Vector3 BuildCloid()
    {
        cc.height = 1.55f; cc.center = new Vector3(0, 0.8f, 0);
        Vector3 fbx;
        Part("CLOiD_Base", visual, Vector3.zero, out fbx);
        Vector3 bodyFbx = new Vector3(0, 0.17f, 0);
        body.localPosition = bodyFbx;
        Part("CLOiD_Torso", body, bodyFbx, out fbx);
        head = Part("CLOiD_Head", body, bodyFbx, out fbx);
        right = BuildCloidArm("N", +1f, bodyFbx);
        left = BuildCloidArm("P", -1f, bodyFbx);
        return new Vector3(0, 0.216f, 0.135f);
    }

    Arm BuildCloidArm(string tag, float side, Vector3 bodyFbx)
    {
        var a = new Arm { side = side, pole = new Vector3(side * 0.9f, -0.7f, -0.15f) };
        Vector3 uF, fF, hF;
        a.upper = Part("CLOiD_Upper_" + tag, body, bodyFbx, out uF);
        a.fore = Part("CLOiD_Fore_" + tag, a.upper, uF, out fF);
        a.hand = Part("CLOiD_Hand_" + tag, a.fore, fF, out hF);
        a.restLocal = transform.InverseTransformPoint(a.hand.position + Vector3.down * LH);
        a.tip = a.hand.position;
        return a;
    }

    // ---------------------------------------------------------- tracked: vertical rail + piston actuator
    // Layout in metres, robot space: x right, y up, z forward. A tracked base carries a vertical rail (mast).
    // A carriage rides the rail (up / down only) and holds the webcam and a linear actuator whose rod extends
    // straight ahead (forward / back only) to press the control.
    const float TrackX = 0.225f, TrackW = 0.10f, AxleZ = 0.19f, SprocketR = 0.09f, CleatT = 0.022f;
    const float AxleY = SprocketR + CleatT;
    const int CleatCount = 30;
    static readonly Vector3 BodyPos = new Vector3(0, 0.26f, 0);
    const float MastZ = -0.02f, RailBot = 0.10f, RailTop = 1.36f;            // body space
    const float CarMin = 0.25f, CarMax = 1.28f, CarRest = 0.86f;             // carriage centre height, body space
    const float CarZ = MastZ + 0.01f, HousingFront = 0.44f, TipLen = 0.02f;  // carriage space
    const float RodMin = 0.0f, RodMax = 0.40f, RodLen = 0.42f;               // rod extension (stroke)
    static float TipZ0 { get { return CarZ + HousingFront + TipLen; } }      // rod tip, robot space, retracted

    Transform carriage, rod;
    float carY = CarRest, rodExt = RodMin;

    static readonly Dictionary<string, Material> paints = new Dictionary<string, Material>();

    // flat colours of the reference drawing (sRGB, like MatLib)
    static Material Paint(string n)
    {
        Material m;
        if (paints.TryGetValue(n, out m) && m != null) return m;
        switch (n)
        {
            case "slate": m = Util.Mat(new Color(0.24f, 0.27f, 0.33f), 0.35f); break;
            case "slate_dark": m = Util.Mat(new Color(0.17f, 0.19f, 0.23f), 0.35f); break;
            case "motor": m = Util.Mat(new Color(0.76f, 0.37f, 0.42f), 0.45f); break;
            case "carriage": m = Util.Mat(new Color(0.91f, 0.79f, 0.55f), 0.4f); break;
            case "carriage_edge": m = Util.Mat(new Color(0.70f, 0.55f, 0.25f), 0.4f); break;
            case "rail": m = Util.Mat(new Color(0.84f, 0.87f, 0.93f), 0.55f, 0.5f); break;
            case "rail_dark": m = Util.Mat(new Color(0.50f, 0.55f, 0.63f), 0.5f, 0.5f); break;
            case "housing": m = Util.Mat(new Color(0.80f, 0.85f, 0.91f), 0.5f, 0.3f); break;
            case "rod": m = Util.Mat(new Color(0.78f, 0.53f, 0.45f), 0.6f, 0.6f); break;
            case "rod_tip": m = Util.Mat(new Color(0.55f, 0.35f, 0.27f), 0.4f); break;
            case "label": m = Util.Mat(new Color(0.36f, 0.52f, 0.70f), 0.4f); break;
            case "wheel": m = Util.Mat(new Color(0.84f, 0.87f, 0.92f), 0.4f, 0.3f); break;
            default: return MatLib.Get(n);
        }
        m.name = "paint_" + n;
        paints[n] = m;
        return m;
    }

    static GameObject Box(string n, Transform p, Vector3 pos, Vector3 size, string mat)
    {
        return Util.Prim(PrimitiveType.Cube, n, p, pos, size, Paint(mat), false);
    }

    // cylinder of radius r and length len along local x (wheels)
    static GameObject CylX(string n, Transform p, Vector3 pos, float r, float len, string mat)
    {
        var g = Util.Prim(PrimitiveType.Cylinder, n, p, pos, new Vector3(r * 2f, len * 0.5f, r * 2f), Paint(mat), false);
        g.transform.localRotation = Quaternion.Euler(0, 0, 90f);
        return g;
    }

    // cylinder of radius r and length len along local z (actuator housing and rod)
    static GameObject CylZ(string n, Transform p, Vector3 pos, float r, float len, string mat)
    {
        var g = Util.Prim(PrimitiveType.Cylinder, n, p, pos, new Vector3(r * 2f, len * 0.5f, r * 2f), Paint(mat), false);
        g.transform.localRotation = Quaternion.Euler(90f, 0, 0);
        return g;
    }

    static GameObject Ball(string n, Transform p, Vector3 pos, float d, string mat)
    {
        return Util.Prim(PrimitiveType.Sphere, n, p, pos, Vector3.one * d, Paint(mat), false);
    }

    Vector3 BuildTracked()
    {
        cc.height = 1.55f; cc.center = new Vector3(0, 0.8f, 0);

        // base: hull between the two motor-driven tracks, one motor per track (does not lean)
        Box("Chassis", visual, new Vector3(0, 0.15f, 0), new Vector3(0.33f, 0.16f, 0.50f), "slate_dark");
        Box("MotorL", visual, new Vector3(-0.17f, 0.12f, -0.16f), new Vector3(0.05f, 0.07f, 0.09f), "motor");
        Box("MotorR", visual, new Vector3(0.17f, 0.12f, -0.16f), new Vector3(0.05f, 0.07f, 0.09f), "motor");
        trackL = BuildTrack(-1f);
        trackR = BuildTrack(+1f);
        Box("Deck", visual, new Vector3(0, 0.245f, 0), new Vector3(0.56f, 0.03f, 0.60f), "slate");
        Box("Driver", visual, new Vector3(0.17f, 0.28f, 0.24f), new Vector3(0.10f, 0.04f, 0.07f), "motor");
        Box("DeckLed", visual, new Vector3(0, 0.245f, 0.301f), new Vector3(0.40f, 0.012f, 0.004f), "m_led");

        // battery + Pi box and the vertical rail (leans with acceleration)
        body.localPosition = BodyPos;
        Box("Battery", body, new Vector3(0, 0.035f, -0.02f), new Vector3(0.26f, 0.07f, 0.22f), "slate");
        Box("Label", body, new Vector3(0, 0.035f, 0.091f), new Vector3(0.16f, 0.03f, 0.004f), "label");
        Box("MastFoot", body, new Vector3(0, 0.087f, MastZ), new Vector3(0.09f, 0.035f, 0.09f), "rail_dark");
        Box("Rail", body, new Vector3(0, (RailBot + RailTop) * 0.5f, MastZ), new Vector3(0.045f, RailTop - RailBot, 0.045f), "rail");
        Box("RailGroove", body, new Vector3(0, (RailBot + RailTop) * 0.5f, MastZ + 0.023f), new Vector3(0.012f, RailTop - RailBot - 0.04f, 0.004f), "rail_dark");
        Box("RailCap", body, new Vector3(0, RailTop + 0.01f, MastZ), new Vector3(0.055f, 0.02f, 0.055f), "rail_dark");

        // carriage with the piston actuator: motor, housing, rod
        carriage = new GameObject("Carriage").transform;
        carriage.SetParent(body, false);
        Box("Block", carriage, Vector3.zero, new Vector3(0.13f, 0.11f, 0.12f), "carriage");
        Box("BlockEdge", carriage, new Vector3(0, -0.058f, 0), new Vector3(0.135f, 0.008f, 0.125f), "carriage_edge");
        Box("ActMotor", carriage, new Vector3(0, 0, 0.10f), new Vector3(0.08f, 0.065f, 0.08f), "motor");
        float hb = 0.14f;
        CylZ("Housing", carriage, new Vector3(0, 0, (hb + HousingFront) * 0.5f), 0.032f, HousingFront - hb, "housing");
        foreach (float z in new[] { 0.22f, 0.30f, 0.38f })
            CylZ("Tick", carriage, new Vector3(0, 0, z), 0.0335f, 0.005f, "rail_dark");
        rod = new GameObject("Rod").transform;
        rod.SetParent(carriage, false);
        CylZ("RodBar", rod, new Vector3(0, 0, -RodLen * 0.5f), 0.011f, RodLen, "rod");
        CylZ("RodTip", rod, new Vector3(0, 0, TipLen * 0.5f), 0.016f, TipLen, "rod_tip");

        // webcam on top of the carriage (pans / tilts, rides with the carriage)
        head = new GameObject("Head").transform;
        head.SetParent(body, false);
        Box("CamBody", head, new Vector3(0, 0.0225f, 0), new Vector3(0.10f, 0.045f, 0.07f), "slate_dark");
        Ball("CamLens", head, new Vector3(0, 0.0225f, 0.036f), 0.026f, "m_led");

        carY = CarRest; rodExt = RodMin;
        ApplyTool();
        return new Vector3(0, 0.0225f, 0.05f);
    }

    void ApplyTool()
    {
        if (carriage == null) return;
        carriage.localPosition = new Vector3(0, carY, CarZ);
        head.localPosition = new Vector3(0, carY + 0.055f, CarZ + 0.01f);
        rod.localPosition = new Vector3(0, 0, HousingFront + rodExt);
    }

    IEnumerator MoveTool(float carTo, float extTo, float dur)
    {
        carTo = Mathf.Clamp(carTo, CarMin, CarMax);
        extTo = Mathf.Clamp(extTo, RodMin, RodMax);
        float c0 = carY, e0 = rodExt, t = 0;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / dur));
            carY = Mathf.Lerp(c0, carTo, k);
            rodExt = Mathf.Lerp(e0, extTo, k);
            yield return null;
        }
        carY = carTo; rodExt = extTo;
    }

    /// <summary>Rail + piston press: face the control, raise the carriage to its height, extend the rod, retract.</summary>
    IEnumerator PressPiston(ControlPoint cp, bool turningOn)
    {
        Vector3 touch = cp.Tip(turningOn, -0.006f);
        LookAt(cp.Surface);
        // the rod only moves straight ahead, so the base turns until the control is on its axis
        Vector3 to = touch - transform.position; to.y = 0;
        if (to.sqrMagnitude > 1e-4f) yield return TurnTo(Quaternion.LookRotation(to).eulerAngles.y);
        yield return MoveTool(touch.y - BodyPos.y, RodMin, 0.9f);
        float reach = transform.InverseTransformPoint(touch).z - TipZ0;
        yield return MoveTool(carY, reach - 0.05f, 0.6f);
        yield return MoveTool(carY, reach, 0.25f);
        cp.SetOn(turningOn);
        yield return new WaitForSeconds(cp.kind == ControlKind.Dial ? 0.7f : 0.22f);
        yield return MoveTool(carY, RodMin, 0.5f);
        yield return MoveTool(CarRest, RodMin, 0.8f);
    }

    IEnumerator ReachTestPiston()
    {
        yield return MoveTool(CarMax - 0.08f, RodMin, 1.0f);
        yield return MoveTool(carY, RodMax * 0.85f, 0.6f);
        yield return new WaitForSeconds(0.25f);
        yield return MoveTool(carY, RodMin, 0.5f);
        yield return MoveTool(CarRest, RodMin, 0.8f);
    }

    Track BuildTrack(float side)
    {
        var tr = new Track { side = side };
        var root = new GameObject(side > 0 ? "TrackR" : "TrackL").transform;
        root.SetParent(visual, false);
        root.localPosition = new Vector3(side * TrackX, 0, 0);

        // sprockets at both ends and three road wheels between
        var wl = new List<Transform>(); var rl = new List<float>();
        float roadR = 0.045f, roadY = CleatT + roadR;
        foreach (float z in new[] { -AxleZ, AxleZ }) { wl.Add(Wheel(root, new Vector3(0, AxleY, z), SprocketR - 0.006f)); rl.Add(SprocketR); }
        foreach (float z in new[] { -0.095f, 0f, 0.095f }) { wl.Add(Wheel(root, new Vector3(0, roadY, z), roadR)); rl.Add(roadR); }
        tr.wheels = wl.ToArray(); tr.wheelR = rl.ToArray();

        tr.cleats = new Transform[CleatCount];
        float pitch = TrackLength / CleatCount;
        for (int i = 0; i < CleatCount; i++)
            tr.cleats[i] = Box("Cleat", root, Vector3.zero, new Vector3(TrackW, CleatT, pitch * 0.72f), "slate_dark").transform;
        PlaceCleats(tr);
        return tr;
    }

    Transform Wheel(Transform root, Vector3 pos, float r)
    {
        var pivot = new GameObject("Wheel").transform;
        pivot.SetParent(root, false);
        pivot.localPosition = pos;
        CylX("Rim", pivot, Vector3.zero, r, TrackW * 0.85f, "wheel");
        CylX("Hub", pivot, Vector3.zero, r * 0.35f, TrackW * 0.95f, "slate_dark");
        // a spoke so the spin is visible
        Box("Spoke", pivot, Vector3.zero, new Vector3(TrackW * 0.9f, r * 1.5f, r * 0.2f), "rail_dark");
        return pivot;
    }

    static float TrackLength { get { return 4f * AxleZ + 2f * Mathf.PI * (SprocketR + CleatT * 0.5f); } }

    // cleats follow a stadium loop: bottom run (front to back), rear arc, top run, front arc
    void PlaceCleats(Track tr)
    {
        float R = SprocketR + CleatT * 0.5f, straight = 2f * AxleZ, arc = Mathf.PI * R, P = TrackLength;
        float pitch = P / tr.cleats.Length;
        for (int i = 0; i < tr.cleats.Length; i++)
        {
            float s = Mathf.Repeat(i * pitch + tr.phase, P);
            float y, z, th;
            if (s < straight) { z = AxleZ - s; y = AxleY - R; th = 0f; }
            else if (s < straight + arc) { th = (s - straight) / R; z = -AxleZ - R * Mathf.Sin(th); y = AxleY - R * Mathf.Cos(th); }
            else if (s < 2f * straight + arc) { z = -AxleZ + (s - straight - arc); y = AxleY + R; th = Mathf.PI; }
            else { th = Mathf.PI + (s - 2f * straight - arc) / R; z = AxleZ - R * Mathf.Sin(th); y = AxleY - R * Mathf.Cos(th); }
            tr.cleats[i].localPosition = new Vector3(0, y, z);
            tr.cleats[i].localRotation = Quaternion.Euler(th * Mathf.Rad2Deg, 0, 0);
        }
    }

    void AnimateTrack(Track tr, float v, float dt)
    {
        tr.phase += v * dt;
        tr.spin += v * dt;
        PlaceCleats(tr);
        for (int i = 0; i < tr.wheels.Length; i++)
            tr.wheels[i].localRotation = Quaternion.Euler(tr.spin / tr.wheelR[i] * Mathf.Rad2Deg, 0, 0);
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

        // tread speeds of a differential drive: turning right makes the left track faster
        if (trackL != null)
        {
            float w = yawRate * Mathf.Deg2Rad * TrackX;
            AnimateTrack(trackL, speed + w, dt);
            AnimateTrack(trackR, speed - w, dt);
        }

        // body lean from acceleration (a tank leans less)
        float leanK = model == RobotModel.Tracked ? 0.5f : 1f;
        float acc = dt > 0 ? (speed - lastSpeed) / dt : 0;
        lastSpeed = speed;
        Quaternion want = Quaternion.Euler(Mathf.Clamp(acc * 1.6f, -3.2f, 3.2f) * leanK, 0, Mathf.Clamp(-yawRate * Mathf.Abs(speed) * 0.018f, -3f, 3f) * leanK);
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

        ApplyTool();
        if (right != null) SolveArm(right);
        if (left != null) SolveArm(left);
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
        float w = a.w;
        Vector3 F = a.tip;
        if (w < 0.001f)
        {
            a.upper.rotation = rest; a.fore.localRotation = Quaternion.identity; a.hand.localRotation = Quaternion.identity;
            return;
        }
        Vector3 S = a.upper.position;
        Vector3 d0 = F - S;
        Vector3 dir0 = d0.sqrMagnitude > 1e-6f ? d0.normalized : Vector3.down;
        // a tip too close to the shoulder would flip the hand: keep the arm at least slightly extended
        if (d0.magnitude < 0.22f + LH) F = S + dir0 * (0.22f + LH);
        Vector3 W = F - dir0 * LH;
        Vector3 dw = W - S;
        float d = Mathf.Clamp(dw.magnitude, 0.22f, L1 + L2 - 0.004f);
        Vector3 dir = dw.sqrMagnitude > 1e-6f ? dw.normalized : Vector3.down;
        Vector3 pole = Vector3.ProjectOnPlane(body.TransformDirection(a.pole), dir).normalized;
        float cosA = Mathf.Clamp((L1 * L1 + d * d - L2 * L2) / (2f * L1 * d), -1f, 1f);
        float sinA = Mathf.Sqrt(1f - cosA * cosA);
        Vector3 E = S + dir * (L1 * cosA) + pole * (L1 * sinA);
        Vector3 W2 = S + dir * d;
        Vector3 uDir = (E - S).normalized, fDir = (W2 - E).normalized, hDir = (F - W2).sqrMagnitude > 1e-6f ? (F - W2).normalized : fDir;

        float flat = Mathf.Abs(Vector3.Dot(dir, body.up));
        Vector3 hint = Vector3.Lerp(body.up, body.forward, flat);
        Quaternion qU = Aim(uDir, hint, body.right), qF = Aim(fDir, hint, body.right), qH = Aim(hDir, hint, body.right);

        a.upper.rotation = Quaternion.Slerp(rest, qU, w);
        a.fore.rotation = Quaternion.Slerp(a.upper.rotation, qF, w);
        a.hand.rotation = Quaternion.Slerp(a.fore.rotation, qH, w);
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
        if (right == null) { yield return PressPiston(cp, turningOn); yield break; }
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

    public void RelaxArms() { StartCoroutine(carriage != null ? MoveTool(CarRest, RodMin, 0.6f) : Relax()); }

    IEnumerator Relax()
    {
        float t = 0;
        float r0 = right != null ? right.w : 0f, l0 = left != null ? left.w : 0f;
        while (t < 0.6f)
        {
            t += Time.deltaTime;
            float k = 1f - Mathf.SmoothStep(0, 1, t / 0.6f);
            if (right != null) right.w = r0 * k;
            if (left != null) left.w = l0 * k;
            yield return null;
        }
        if (right != null) right.w = 0;
        if (left != null) left.w = 0;
    }

    /// <summary>Capability probe: CLOiD lifts its left arm and extends it; the tracked robot raises the carriage and extends the rod.</summary>
    public IEnumerator ReachTest()
    {
        if (left == null) { yield return ReachTestPiston(); yield break; }
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
