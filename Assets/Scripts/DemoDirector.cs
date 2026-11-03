using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Runs the seven-stage process end to end: S1 recognize, S2 map and verify, S3 routine, S4 trigger and intent,
/// S5 wireless + CV check, S6 physical fallback, S7 learning. Also does the per-frame perception of the wall controls.
/// </summary>
public class DemoDirector : MonoBehaviour
{
    public static readonly string[] StageName =
        { "Recognize", "Map & verify", "Routine", "Trigger & intent", "Wireless + CV", "Physical fallback", "Learn" };

    public static readonly Color Cyan = new Color(0.30f, 0.86f, 1f);
    public static readonly Color Blue = new Color(0.42f, 0.66f, 1f);
    public static readonly Color Amber = new Color(1f, 0.72f, 0.30f);
    public static readonly Color Green = new Color(0.38f, 0.92f, 0.62f);
    public static readonly Color Red = new Color(1f, 0.42f, 0.42f);
    public static readonly Color Mute = new Color(0.66f, 0.72f, 0.80f);

    public static Color StageColor(int i) { return i < 3 ? Blue : i < 6 ? Amber : Green; }

    public class Toast { public string title, body; public Color col; public float t0, dur; }
    public class LogLine { public string time, tag, text; public Color col; }
    public class ProbeRow { public string level, name, detail; public int state; }
    public class SafetyRow { public string text; public int state; }
    public class Proposal
    {
        public string title, trigger; public List<string> lines = new List<string>();
        public bool open, approved; public float openT, autoAt = -1, clickT = -10;
    }
    public class Routine
    {
        public string name, trigger, intent;
        public List<KeyValuePair<string, bool>> actions = new List<KeyValuePair<string, bool>>();
    }

    public House house;
    public RobotRig robot;
    public CamRig cam;

    public bool auto, missionRunning, introVisible = true, finished;
    public int[] stageState = new int[7];
    public string capHead = "", capBody = "";
    public float capT;
    public List<Toast> toasts = new List<Toast>();
    public List<LogLine> log = new List<LogLine>();
    public ControlPoint focus;
    public Proposal proposal = new Proposal();
    public ProbeRow[] probe;
    public SafetyRow[] safety;
    public bool safetyOpen;
    public bool probeOpen;
    public string missionName = "";
    public float missionT0;
    public string lastLearn = "";
    public int wirelessOk, physicalOk, wirelessFail, cancelled;
    public bool hazard;
    public GameObject hazardObj;
    public string levelText = "L3";
    public float channelFlash;
    public string routineText = "";
    public bool endCard;

    // command line (used for automated screenshots)
    public string shotDir;
    public bool quitAfter;
    public float speed = 1f;

    Coroutine mission;
    readonly Queue<ControlPoint> verifyQueue = new Queue<ControlPoint>();
    bool verifyBusy;
    LineRenderer pathLine, linkLine;
    Material lineMat;
    Texture2D dashTex;
    Dictionary<string, Routine> routines = new Dictionary<string, Routine>();
    float t0;

    // ------------------------------------------------------------------ setup
    public void Init()
    {
        t0 = Time.time;
        BuildRoutines();
        var dash = new Texture2D(32, 4, TextureFormat.RGBA32, false);
        for (int x = 0; x < 32; x++) for (int y = 0; y < 4; y++) dash.SetPixel(x, y, new Color(1, 1, 1, x < 18 ? 1f : 0f));
        dash.wrapMode = TextureWrapMode.Repeat; dash.Apply();
        dashTex = dash;
        lineMat = new Material(Shader.Find("Sprites/Default")) { mainTexture = dashTex };
        pathLine = MakeLine("PathLine", 0.05f);
        linkLine = MakeLine("LinkLine", 0.035f);
        probe = new[]
        {
            new ProbeRow { level = "L0", name = "Observe", detail = "camera stream, 30 fps" },
            new ProbeRow { level = "L1", name = "Re-observe", detail = "head pan / tilt, approach" },
            new ProbeRow { level = "L2", name = "Wireless control", detail = "Wi-Fi hub, IR blaster, BT" },
            new ProbeRow { level = "L3", name = "Manipulate", detail = "arm reach + safe contact" },
        };
        safety = new[]
        {
            new SafetyRow { text = "Path to control is clear" },
            new SafetyRow { text = "No person inside the arm zone" },
            new SafetyRow { text = "Contact force limit armed" },
            new SafetyRow { text = "Control located by CV" },
        };
        robot.SetMood(Cyan, 0.25f);
        levelText = "--";
    }

    LineRenderer MakeLine(string name, float w)
    {
        var g = new GameObject(name);
        g.transform.SetParent(transform, false);
        var l = g.AddComponent<LineRenderer>();
        l.material = lineMat;
        l.startWidth = l.endWidth = w;
        l.textureMode = LineTextureMode.Tile;
        l.alignment = LineAlignment.View;
        l.numCapVertices = 4;
        l.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        l.receiveShadows = false;
        l.enabled = false;
        return l;
    }

    void BuildRoutines()
    {
        var ev = new Routine
        {
            name = "Evening arrival",
            trigger = "Door sensor: someone arrived home, 18:42",
            intent = "Light the home: living, kitchen, hall and reading corner (confidence 0.94)",
        };
        ev.actions.Add(new KeyValuePair<string, bool>("SW-01", true));
        ev.actions.Add(new KeyValuePair<string, bool>("SW-02", true));
        ev.actions.Add(new KeyValuePair<string, bool>("SW-03", true));
        ev.actions.Add(new KeyValuePair<string, bool>("SW-05", true));
        routines["evening"] = ev;
        var gn = new Routine
        {
            name = "Goodnight",
            trigger = "Schedule 23:00 and phone charging in the bedroom",
            intent = "Switch off the common areas and light the bedroom (confidence 0.91)",
        };
        gn.actions.Add(new KeyValuePair<string, bool>("SW-05", false));
        gn.actions.Add(new KeyValuePair<string, bool>("SW-03", false));
        gn.actions.Add(new KeyValuePair<string, bool>("SW-02", false));
        gn.actions.Add(new KeyValuePair<string, bool>("SW-01", false));
        gn.actions.Add(new KeyValuePair<string, bool>("SW-04", true));
        routines["goodnight"] = gn;
    }

    static int Hash(string s) { int h = 7; foreach (char c in s) h = h * 31 + c; return Mathf.Abs(h); }

    public ControlPoint Cp(string id) { return house.controls.Find(c => c.id == id); }

    // ------------------------------------------------------------------ small helpers
    string Stamp() { float t = Time.time - t0; return ((int)(t / 60)).ToString("00") + ":" + (t % 60).ToString("00.0"); }

    public void Log(string tag, string text, Color? col = null)
    {
        log.Add(new LogLine { time = Stamp(), tag = tag, text = text, col = col ?? Mute });
        if (log.Count > 60) log.RemoveAt(0);
    }

    public void ToastMsg(string title, string body, Color col, float dur = 3.6f)
    {
        toasts.Add(new Toast { title = title, body = body, col = col, t0 = Time.time, dur = dur });
        if (toasts.Count > 4) toasts.RemoveAt(0);
    }

    public void Say(string head, string body) { capHead = head; capBody = body; capT = Time.time; }

    public void SetStage(int i)
    {
        for (int k = 0; k < 7; k++)
        {
            if (k < i) { if (stageState[k] != 2 && (k >= 3 || stageState[k] == 1)) stageState[k] = 2; }
            else if (k == i) stageState[k] = 1;
            else if (i >= 4 && k >= 4) stageState[k] = 0;
        }
    }

    public void FinishStage(int i) { stageState[i] = 2; }

    IEnumerator W(float s) { yield return new WaitForSeconds(s); }

    public void Shot(string name)
    {
        if (string.IsNullOrEmpty(shotDir)) return;
        StartCoroutine(ShotRoutine(name));
    }

    IEnumerator ShotRoutine(string name)
    {
        yield return new WaitForEndOfFrame();
        Directory.CreateDirectory(shotDir);
        ScreenCapture.CaptureScreenshot(Path.Combine(shotDir, name + ".png"));
    }

    // ------------------------------------------------------------------ perception (every frame)
    void LateUpdate()
    {
        if (robot == null || robot.eyeCam == null) return;
        Perceive(Time.deltaTime);
        // auto-clicking proposal card
        if (proposal.open && proposal.autoAt > 0 && Time.time >= proposal.autoAt) { proposal.clickT = Time.time; proposal.autoAt = -1; }
        toasts.RemoveAll(t => Time.time - t.t0 > t.dur);
        if (hazardObj != null) hazardObj.SetActive(hazard);
    }

    void Perceive(float dt)
    {
        var c = robot.eyeCam;
        Vector3 o = c.transform.position;
        float tanHalf = Mathf.Tan(c.fieldOfView * 0.5f * Mathf.Deg2Rad);
        foreach (var cp in house.controls)
        {
            Vector3 sp = cp.Surface;
            Vector3 vp = c.WorldToViewportPoint(sp);
            bool vis = false; float conf = 0;
            if (vp.z > 0.25f && vp.z < 7.5f && vp.x > 0.04f && vp.x < 0.96f && vp.y > 0.06f && vp.y < 0.94f)
            {
                Vector3 toCam = o - sp; float dist = toCam.magnitude; toCam /= dist;
                float facing = Vector3.Dot(cp.Normal, toCam);
                if (facing > 0.30f && !Physics.Linecast(o, sp + cp.Normal * 0.03f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    vis = true;
                    float size = 0.115f / (2f * dist * tanHalf);
                    float sizeF = Mathf.Clamp01(size / 0.03f);
                    float angF = Mathf.InverseLerp(0.30f, 0.85f, facing);
                    conf = 0.30f + 0.66f * Mathf.Pow(sizeF, 0.7f) * (0.55f + 0.45f * angF);
                    conf += (Mathf.PerlinNoise(Time.time * 2.3f, Hash(cp.id) % 97) - 0.5f) * 0.04f;
                    if (cp.IsDecoy) conf = Mathf.Min(conf, 0.48f) * 0.78f;
                    conf = Mathf.Clamp01(conf);
                }
            }
            cp.visibleNow = vis;
            cp.visConf = vis ? Mathf.Lerp(cp.visConf, conf, 1f - Mathf.Exp(-9f * dt)) : Mathf.Lerp(cp.visConf, 0f, 1f - Mathf.Exp(-6f * dt));
            if (vis)
            {
                Vector2 mn = new Vector2(9, 9), mx = new Vector2(-9, -9);
                foreach (var w in cp.Corners())
                {
                    Vector3 v = c.WorldToViewportPoint(w);
                    mn = Vector2.Min(mn, v); mx = Vector2.Max(mx, v);
                }
                cp.screenMin = mn; cp.screenMax = mx;
                cp.seenT = Time.time;
            }
            bool strong = vis && cp.visConf >= (cp.IsDecoy ? 0.3f : 0.80f);
            cp.visTime = strong ? cp.visTime + dt : Mathf.Max(0, cp.visTime - dt * 1.5f);
            if (cp.status == CtrlStatus.Unseen && cp.visTime >= (cp.IsDecoy ? 1.2f : 0.55f)) Register(cp);
        }
    }

    void Register(ControlPoint cp)
    {
        if (cp.IsDecoy)
        {
            if (cp.conf > 0) return;
            cp.conf = 0.01f;
            Log("S1", "Rejected: wall panel at " + cp.room + " (conf " + cp.visConf.ToString("0.00") + " < 0.60), not an actuator", Red);
            ToastMsg("Not a control", "Thermostat panel ignored (" + cp.visConf.ToString("0.00") + ")", Mute, 2.8f);
            return;
        }
        cp.status = CtrlStatus.Detected;
        cp.conf = Mathf.Max(cp.visConf, 0.86f);
        SetStage(0);
        Sfx.Play("detect");
        Log("S1", cp.typeName + " found in " + cp.room + " (conf " + cp.conf.ToString("0.00") + ") -> " + cp.id, DemoDirector.Blue);
        ToastMsg(cp.id + "  " + cp.typeName, "Registered in " + cp.room + "   conf " + cp.conf.ToString("0.00"), Blue);
        verifyQueue.Enqueue(cp);
        Shot("s1_detect_" + cp.id);
    }

    // ------------------------------------------------------------------ input (manual)
    void Update()
    {
        Time.timeScale = speed;
        if (introVisible && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)))
        {
            introVisible = false;
            if (Input.GetKeyDown(KeyCode.Space)) StartAuto();
            return;
        }
        if (Input.GetKeyDown(KeyCode.Escape) && missionRunning) CancelMission("Cancelled by user");
        if (Input.GetKeyDown(KeyCode.Space) && !introVisible) { if (missionRunning && auto) CancelMission("Auto demo stopped"); else StartAuto(); }
        if (Input.GetKeyDown(KeyCode.R) && !missionRunning) RunRoutineKey("evening");
        if (Input.GetKeyDown(KeyCode.G) && !missionRunning) RunRoutineKey("goodnight");
        if (Input.GetKeyDown(KeyCode.H)) { hazard = !hazard; Log("SAFE", hazard ? "Hazard injected: a person stands near the switch" : "Hazard cleared", hazard ? Red : Green); }
        if (Input.GetKeyDown(KeyCode.C)) cam.Cycle();
        if (Input.GetKeyDown(KeyCode.F)) cam.SetMode(CamMode.Overview);
        if (Input.GetKeyDown(KeyCode.V)) cam.SetMode(CamMode.Pov);
        if (Input.GetKeyDown(KeyCode.T)) ResetWorld();
        if (proposal.open && Input.GetKeyDown(KeyCode.Return)) ApproveProposal();

        if (missionRunning && missionName == "Move" && (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.D)))
        {
            if (mission != null) StopCoroutine(mission);
            mission = null; missionRunning = false; robot.Stop(); pathLine.enabled = false;
        }
        ClickToMove();
        if (!missionRunning)
        {
            float f = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 0.6f : 0f);
            float t = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
            if (Input.GetKey(KeyCode.LeftShift)) f *= 1.35f;
            robot.Drive(f, t);
            robot.SetMood(Cyan, 0.25f);
            if (!verifyBusy && verifyQueue.Count > 0) StartCoroutine(ManualVerify());
        }
    }

    void ClickToMove()
    {
        if (!Input.GetMouseButtonDown(0) || missionRunning || introVisible || proposal.open || cam == null) return;
        float s = Screen.height / 1080f;
        Vector2 m = Input.mousePosition;
        if (m.x < 370f * s || m.x > Screen.width - 560f * s || m.y < 120f * s) return;
        var plane = new Plane(Vector3.up, Vector3.zero);
        Ray ray = cam.cam.ScreenPointToRay(m);
        float d;
        if (!plane.Raycast(ray, out d)) return;
        Vector3 p = ray.GetPoint(d);
        if (p.x < 0.3f || p.x > 12.7f || p.z < 0.3f || p.z > 8.7f) return;
        StartMission("Move", MoveTo(p));
    }

    IEnumerator MoveTo(Vector3 p)
    {
        ShowPath(p);
        robot.SetMood(Cyan, 0.5f);
        yield return robot.GoTo(p);
        pathLine.enabled = false;
    }

    IEnumerator ManualVerify()
    {
        verifyBusy = true;
        while (verifyQueue.Count > 0 && !missionRunning)
        {
            var cp = verifyQueue.Dequeue();
            if (cp.status == CtrlStatus.Detected) yield return VerifyMapping(cp, false);
        }
        verifyBusy = false;
    }

    // ------------------------------------------------------------------ mission control
    public void StartMission(string name, IEnumerator routine)
    {
        if (mission != null) { StopCoroutine(mission); }
        robot.Stop();
        missionRunning = true;
        missionName = name;
        missionT0 = Time.time;
        mission = StartCoroutine(MissionWrap(routine));
    }

    IEnumerator MissionWrap(IEnumerator r)
    {
        yield return r;
        missionRunning = false;
        mission = null;
        robot.SetMood(Cyan, 0.25f);
        robot.LookForward();
        linkLine.enabled = false; pathLine.enabled = false;
        safetyOpen = false; probeOpen = false;
        if (cam.mode == CamMode.Close) cam.SetMode(CamMode.Chase);
    }

    public void CancelMission(string why)
    {
        if (mission != null) { StopCoroutine(mission); mission = null; }
        missionRunning = false; auto = false;
        robot.Stop();
        robot.RelaxArms();
        robot.LookForward();
        robot.SetMood(Cyan, 0.25f);
        linkLine.enabled = false; pathLine.enabled = false;
        safetyOpen = false; probeOpen = false; proposal.open = false;
        if (cam.mode == CamMode.Close) cam.SetMode(CamMode.Chase);
        Log("SYS", why, Amber);
        ToastMsg("Mission stopped", why, Amber);
    }

    public void StartAuto()
    {
        if (missionRunning) return;
        introVisible = false;
        ResetWorld();
        auto = true;
        StartMission("Auto demo", AutoDemo());
    }

    public void RunRoutineKey(string key)
    {
        if (missionRunning) return;
        introVisible = false;
        auto = false;
        StartMission(routines[key].name, RoutineFlow(routines[key]));
    }

    public void ResetWorld()
    {
        if (mission != null) { StopCoroutine(mission); mission = null; }
        missionRunning = false; auto = false; finished = false; endCard = false;
        verifyQueue.Clear();
        foreach (var cp in house.controls)
        {
            cp.status = CtrlStatus.Unseen; cp.conf = 0; cp.mapConf = 0; cp.visTime = 0; cp.visConf = 0; cp.runs = 0;
            cp.SetOn(false, false);
            if (cp.lamp != null) cp.lamp.level = 0f;
        }
        // restore the designed channel scores
        var s = house;
        Cp("SW-01").pWifi = 0.66f; Cp("SW-01").pPhys = 0.82f;
        Cp("SW-05").pBt = 0.55f; Cp("SW-05").pPhys = 0.78f;
        Cp("SW-02").pIr = 0.74f; Cp("SW-02").pPhys = 0.80f;
        Cp("SW-03").pPhys = 0.84f;
        Cp("SW-04").pWifi = 0.78f; Cp("SW-04").pPhys = 0.82f;
        for (int i = 0; i < 7; i++) stageState[i] = 0;
        foreach (var r in probe) r.state = 0;
        levelText = "--";
        log.Clear(); toasts.Clear();
        focus = null; proposal.open = false; safetyOpen = false; probeOpen = false;
        wirelessOk = physicalOk = wirelessFail = cancelled = 0;
        lastLearn = "";
        capHead = ""; capBody = "";
        linkLine.enabled = false; pathLine.enabled = false;
        robot.RelaxArms();
        robot.Teleport(house.dockPos + new Vector3(0, 0, 0.06f), 0f);
        robot.LookForward();
        robot.SetMood(Cyan, 0.25f);
        house.RefreshProbes();
        t0 = Time.time;
    }

    public void ApproveProposal()
    {
        if (!proposal.open) return;
        proposal.approved = true;
        proposal.open = false;
        Sfx.Play("ok");
    }

    public void DeclineProposal() { proposal.open = false; proposal.approved = false; Sfx.Play("ui"); }

    // ------------------------------------------------------------------ auto demo
    IEnumerator AutoDemo()
    {
        Shot("00_start");
        cam.SetMode(CamMode.Overview);
        Say("CLOiD  /  Legacy-control integration", "A wall switch that no network knows about, found, mapped and operated by the robot.");
        yield return W(2.6f);
        cam.SetMode(CamMode.Chase);
        yield return Probe();
        yield return Phase1();
        yield return RoutineFlow(routines["evening"]);
        // wrap up
        yield return ReturnToDock();
        finished = true; endCard = true;
        Say("Demo complete", "SPACE replays   WASD or click the floor to drive   R runs the routine again   G goodnight");
        Shot("99_end");
        yield return W(1.5f);
        if (quitAfter) Bootstrap.QuitApp();
    }

    IEnumerator Probe()
    {
        Say("Capability probe", "Measure what the robot can do before any stage is enabled.");
        probeOpen = true;
        robot.SetMood(Cyan, 0.5f);
        Log("SYS", "Capability probe started", Cyan);
        cam.SetMode(CamMode.Chase);
        for (int i = 0; i < probe.Length; i++)
        {
            probe[i].state = 1;
            if (i == 1) { robot.Scan(40f); }
            if (i == 3) yield return robot.ReachTest();
            else yield return W(i == 0 ? 0.9f : 1.2f);
            robot.Scan(0);
            probe[i].state = 2;
            Sfx.Play("tick");
            levelText = "L" + i;
            Log("SYS", probe[i].level + " " + probe[i].name + " passed (" + probe[i].detail + ")", Green);
        }
        Shot("01_probe");
        ToastMsg("Capability level L3", "All stages may run; physical actions stay behind the Safety Guard.", Green);
        yield return W(1.4f);
        probeOpen = false;
    }

    IEnumerator Phase1()
    {
        Say("S1 · Recognize controls", "The robot patrols and looks for wall switches, buttons and dials.");
        SetStage(0);
        yield return robot.GoTo(house.dockPos + new Vector3(0, 0, 1.4f));
        for (int i = 0; i < house.patrol.Count; i++)
        {
            var st = house.patrol[i];
            robot.SetMood(Cyan, 0.6f);
            Vector3 stop = st.pos;
            ShowPath(stop);
            yield return robot.GoTo(stop);
            pathLine.enabled = false;
            yield return robot.TurnTo(st.yaw);
            robot.Scan(36f);
            float t = 0;
            while (t < 3.4f)
            {
                t += Time.deltaTime;
                if (verifyQueue.Count > 0) { robot.Scan(0); yield return DrainVerify(); robot.Scan(36f); }
                yield return null;
            }
            robot.Scan(0);
            yield return DrainVerify();
        }
        // anything the patrol missed gets a direct look
        foreach (var cp in house.controls)
        {
            if (cp.IsDecoy || cp.status != CtrlStatus.Unseen) continue;
            Log("S1", "Re-observe (L1): approach " + cp.id, Amber);
            yield return LookAtControl(cp, 1.9f);
            yield return DrainVerify();
        }
        FinishStage(0); FinishStage(1);
        yield return ProposeRoutine();
    }

    IEnumerator DrainVerify()
    {
        while (verifyQueue.Count > 0)
        {
            var cp = verifyQueue.Dequeue();
            if (cp.status == CtrlStatus.Detected) yield return VerifyMapping(cp, true);
        }
    }

    IEnumerator LookAtControl(ControlPoint cp, float away)
    {
        Vector3 pos = cp.transform.position + cp.Normal * away; pos.y = 0;
        ShowPath(pos);
        yield return robot.GoTo(pos);
        pathLine.enabled = false;
        yield return robot.TurnTo(Quaternion.LookRotation(-cp.Normal).eulerAngles.y);
        robot.LookAt(cp.Surface);
        float t = 0;
        while (cp.status == CtrlStatus.Unseen && t < 3f) { t += Time.deltaTime; yield return null; }
        robot.LookForward();
    }

    void ShowPath(Vector3 to)
    {
        var path = PathGrid.Find(robot.transform.position, to);
        var pts = new List<Vector3> { robot.transform.position + Vector3.up * 0.04f };
        foreach (var p in path) pts.Add(p + Vector3.up * 0.04f);
        pathLine.positionCount = pts.Count;
        pathLine.SetPositions(pts.ToArray());
        float len = 0; for (int i = 1; i < pts.Count; i++) len += Vector3.Distance(pts[i - 1], pts[i]);
        pathLine.material.mainTextureScale = new Vector2(len * 3.5f, 1f);
        pathLine.startColor = pathLine.endColor = new Color(0.3f, 0.9f, 1f, 0.9f);
        pathLine.enabled = true;
    }

    void ShowLink(ControlPoint cp, Color col)
    {
        if (cp.lamp == null) { linkLine.enabled = false; return; }
        Vector3 a = cp.Surface, b = cp.lamp.anchor;
        var pts = new Vector3[28];
        for (int i = 0; i < pts.Length; i++)
        {
            float t = i / (float)(pts.Length - 1);
            Vector3 p = Vector3.Lerp(a, b, t);
            p.y += Mathf.Sin(t * Mathf.PI) * (0.7f + Vector3.Distance(a, b) * 0.12f);
            pts[i] = p;
        }
        linkLine.positionCount = pts.Length;
        linkLine.SetPositions(pts);
        linkLine.material.mainTextureScale = new Vector2(Vector3.Distance(a, b) * 2.2f, 1f);
        linkLine.startColor = linkLine.endColor = col;
        linkLine.enabled = true;
    }

    // ------------------------------------------------------------------ S2
    IEnumerator VerifyMapping(ControlPoint cp, bool cinema)
    {
        focus = cp;
        SetStage(1);
        Say("S2 · Map and verify", cp.id + "  ->  " + cp.lampName);
        cp.status = CtrlStatus.Candidate;
        float spatial = 0.58f + 0.12f * Mathf.Clamp01(1f - Vector3.Distance(cp.transform.position, cp.lamp.anchor) / 9f) + 0.04f * (Hash(cp.id) % 3);
        spatial = Mathf.Clamp(spatial, 0.60f, 0.76f);
        cp.mapConf = spatial;
        Log("S2", "Candidate: " + cp.id + " -> " + cp.lampName + " (spatial context " + spatial.ToString("0.00") + ")", Blue);
        ToastMsg("Candidate mapping", cp.id + " -> " + cp.lampName, Blue, 2.4f);
        Color prevMood = Cyan;
        robot.SetMood(Blue, 0.5f);
        ShowLink(cp, new Color(0.45f, 0.7f, 1f, 0.95f));
        if (cinema) cam.Glance(cp.lamp.anchor, new Vector3(-3.4f, 3.8f, -4.6f), 4.8f);
        yield return W(0.9f);

        bool wireless = cp.hasWifi || cp.hasIr || cp.hasBt;
        if (wireless)
        {
            Log("S2", "Register channel: " + cp.channelNote + ", sending test pulse", Blue);
            Sfx.Play("tick");
            yield return W(0.5f);
            cp.lamp.level = Mathf.Max(0.5f, cp.onLevel);
            channelFlash = Time.time;
            yield return W(1.3f);
            float dl = 0.31f + 0.12f * ((Hash(cp.id) & 7) / 7f);
            Log("S2", "CV state check: lamp region delta-L +" + dl.ToString("0.00") + " -> state ON confirmed", Green);
            cp.lamp.level = 0f;
            yield return W(0.9f);
            cp.mapConf = Mathf.Min(0.97f, spatial + 0.27f);
        }
        else
        {
            Log("S2", "No wireless channel known; mapped from spatial context and usage history", Amber);
            yield return W(1.2f);
            cp.mapConf = Mathf.Min(0.90f, spatial + 0.14f);
        }
        cp.status = CtrlStatus.Verified;
        Sfx.Play("ok");
        Log("S2", "Mapping verified: " + cp.id + " -> " + cp.lampName + " (" + cp.mapConf.ToString("0.00") + ")", Green);
        ToastMsg("Mapping verified", cp.id + " -> " + cp.lampName + "   conf " + cp.mapConf.ToString("0.00"), Green);
        Shot("s2_verified_" + cp.id);
        linkLine.enabled = false;
        robot.SetMood(Cyan, 0.5f);
        yield return W(0.4f);
    }

    // ------------------------------------------------------------------ S3
    IEnumerator ProposeRoutine()
    {
        SetStage(2);
        Say("S3 · Integrate into routines", "Verified controls become part of a proactive routine, once the user approves.");
        var r = routines["evening"];
        proposal.title = r.name;
        proposal.trigger = r.trigger;
        proposal.lines.Clear();
        foreach (var a in r.actions)
        {
            var cp = Cp(a.Key);
            proposal.lines.Add(cp.lampName + (cp.kind == ControlKind.Dial ? (a.Value ? "  " + Mathf.RoundToInt(cp.onLevel * 100) + "%" : "  off") : (a.Value ? "  on" : "  off")) + "   via " + cp.id);
        }
        proposal.open = true; proposal.approved = false; proposal.openT = Time.time;
        Log("S3", "Routine proposed: " + r.name + " (" + r.actions.Count + " actions)", Blue);
        Sfx.Play("ui");
        Shot("s3_proposal");
        if (auto) proposal.autoAt = Time.time + 3.6f;
        float t = 0;
        while (proposal.open) { t += Time.deltaTime; if (auto && Time.time >= proposal.clickT + 0.5f && proposal.clickT > 0) ApproveProposal(); yield return null; }
        if (proposal.approved)
        {
            foreach (var a in r.actions) Cp(a.Key).status = CtrlStatus.InRoutine;
            FinishStage(2);
            Log("S3", "User approved: routine active", Green);
            ToastMsg("Routine active", r.name + " is now part of the schedule", Green);
        }
        else
        {
            Log("S3", "Routine kept as a suggestion", Amber);
        }
        yield return W(1.2f);
    }

    // ------------------------------------------------------------------ S4-S7
    IEnumerator RoutineFlow(Routine r)
    {
        cam.SetMode(CamMode.Chase);
        // make sure every involved control is mapped (pre-map when missing)
        foreach (var a in r.actions)
        {
            var cp = Cp(a.Key);
            if (cp.status < CtrlStatus.Verified)
            {
                Log("S5", "No mapping for " + cp.id + ": pre-map first", Amber);
                yield return LookAtControl(cp, 1.9f);
                yield return DrainVerify();
            }
        }
        SetStage(3);
        for (int i = 4; i < 7; i++) stageState[i] = 0;
        routineText = r.name;
        Say("S4 · Trigger and intent", r.name);
        robot.SetMood(Amber, 0.5f);
        Log("S4", "Trigger: " + r.trigger, Amber);
        ToastMsg("Trigger", r.trigger, Amber, 3.2f);
        Shot("s4_trigger");
        yield return W(2.0f);
        Log("S4", "Intent (cloud VLM): " + r.intent, Amber);
        yield return W(1.6f);
        Log("S4", "Risk class R1 (lighting): runs without asking", Amber);
        yield return W(1.0f);
        FinishStage(3);

        foreach (var a in r.actions)
        {
            yield return RunAction(Cp(a.Key), a.Value);
        }
        FinishStage(5);
        SetStage(6); FinishStage(6);
        Say("Routine complete", r.name + ": " + wirelessOk + " wireless, " + physicalOk + " physical" + (cancelled > 0 ? ", " + cancelled + " cancelled" : ""));
        ToastMsg("Routine complete", wirelessOk + " wireless / " + physicalOk + " physical", Green);
        Shot("s7_complete");
        yield return W(1.8f);
        if (!auto) yield return ReturnToDock();
    }

    class Chan { public string name; public float p; public float cost; public bool fails; public char kind; }

    List<Chan> Plan(ControlPoint cp)
    {
        var l = new List<Chan>();
        if (cp.hasWifi) l.Add(new Chan { name = "Wi-Fi", p = cp.pWifi, cost = 0.05f, fails = cp.wifiFails, kind = 'w' });
        if (cp.hasIr) l.Add(new Chan { name = "IR", p = cp.pIr, cost = 0.08f, fails = cp.irFails, kind = 'i' });
        if (cp.hasBt) l.Add(new Chan { name = "BT", p = cp.pBt, cost = 0.10f, fails = cp.btFails, kind = 'b' });
        l.Sort((a, b) => (b.p - b.cost).CompareTo(a.p - a.cost));
        return l;
    }

    IEnumerator RunAction(ControlPoint cp, bool on)
    {
        focus = cp;
        for (int i = 4; i < 7; i++) stageState[i] = 0;
        string verb = on ? "ON" : "OFF";
        if (cp.lamp != null && cp.lamp.On == on && cp.On == on)
        {
            Log("S5", cp.lampName + " already " + verb + ", nothing to do", Mute);
            yield break;
        }
        SetStage(4);
        Say("S5 · Wireless control and CV check", cp.lampName + " " + verb);
        robot.SetMood(Amber, 0.5f);
        var plan = Plan(cp);
        var sb = new List<string>();
        foreach (var c in plan) sb.Add(c.name + " " + c.p.ToString("0.00"));
        sb.Add("PHYS " + cp.pPhys.ToString("0.00"));
        Log("S5", "Mapping lookup: " + cp.id + " -> " + cp.lampName + ". Channels: " + string.Join(", ", sb.ToArray()), Amber);
        ShowLink(cp, new Color(1f, 0.78f, 0.35f, 0.95f));
        cam.Glance(cp.lamp.anchor, new Vector3(-3.4f, 3.8f, -4.6f), 3.6f);
        yield return W(1.0f);

        bool done = false;
        foreach (var ch in plan)
        {
            if (ch.p < 0.40f)
            {
                Log("S5", ch.name + " demoted (score " + ch.p.ToString("0.00") + " < 0.40): skipped", Amber);
                continue;
            }
            Log("S5", "Send " + verb + " via " + ch.name + " (" + cp.channelNote + ")", Amber);
            channelFlash = Time.time;
            Sfx.Play("tick");
            yield return W(1.1f);
            if (ch.fails)
            {
                wirelessFail++;
                Log("S5", ch.name + ": no acknowledgement, CV delta-L +0.01 -> state unchanged", Red);
                ToastMsg(ch.name + " failed", "No state change detected by CV", Red, 2.8f);
                Sfx.Play("fail");
                Learn(cp, ch, false);
                yield return W(0.9f);
                continue;
            }
            cp.SetOn(on, false);
            yield return W(1.0f);
            Log("S5", "CV state check: delta-L " + (on ? "+0.42" : "-0.42") + " -> " + cp.lampName + " " + verb + " confirmed", Green);
            ToastMsg(cp.lampName + " " + verb, "Done via " + ch.name, Green);
            Sfx.Play("ok");
            wirelessOk++;
            done = true;
            Shot("s5_wireless_" + cp.id);
            SetStage(6);
            Learn(cp, ch, true);
            yield return W(1.8f);
            break;
        }
        linkLine.enabled = false;
        if (done) { cp.runs++; yield break; }

        // ---------------- S6
        SetStage(5);
        Say("S6 · Physical fallback", "Safety Guard, then the robot operates " + cp.id + " itself.");
        robot.SetMood(Amber, 0.8f);
        Log("S6", "Wireless unavailable: physical fallback for " + cp.id, Amber);
        yield return SafetyCheck(cp);
        if (safetyAborted)
        {
            cancelled++;
            Log("S6", "Cancelled by Safety Guard; user notified", Red);
            ToastMsg("Action cancelled", cp.id + ": person near the control. User notified.", Red, 4f);
            Sfx.Play("fail");
            Learn(cp, null, false);
            robot.SetMood(Red, 0.6f);
            yield return W(1.6f);
            yield break;
        }
        Vector3 pos; float yaw;
        StandPose(cp, out pos, out yaw);
        Log("S6", "VLA plan: approach " + cp.id + ", stop " + cp.standoff.ToString("0.00") + " m from the wall", Amber);
        ShowPath(pos);
        cam.SetMode(CamMode.Chase);
        yield return robot.GoTo(pos);
        pathLine.enabled = false;
        yield return robot.TurnTo(yaw);
        robot.LookAt(cp.Surface);
        float wait = 0;
        while ((!cp.visibleNow || cp.visConf < 0.82f) && wait < 2.5f) { wait += Time.deltaTime; yield return null; }
        Log("S6", "CV located " + cp.id + " (" + Mathf.Max(0.88f, cp.visConf).ToString("0.00") + "), pose estimated", Amber);
        yield return W(0.5f);
        cam.CloseUp(cp.transform.position);
        Log("S6", "Arm: reach and " + (cp.kind == ControlKind.Dial ? "turn" : "press"), Amber);
        Shot("s6_reach_" + cp.id);
        yield return robot.Press(cp, on);
        Sfx.Play("press");
        yield return W(0.9f);
        Log("S6", "CV state check: delta-L " + (on ? "+0.46" : "-0.46") + " -> " + cp.lampName + " " + verb + " confirmed", Green);
        ToastMsg(cp.lampName + " " + verb, "Operated physically, confirmed by CV", Green);
        Sfx.Play("ok");
        physicalOk++;
        Shot("s6_done_" + cp.id);
        robot.LookForward();
        cam.SetMode(CamMode.Chase);
        SetStage(6);
        Learn(cp, null, true);
        robot.SetMood(Cyan, 0.4f);
        cp.runs++;
        yield return W(1.8f);
    }

    bool safetyAborted;

    IEnumerator SafetyCheck(ControlPoint cp)
    {
        safetyAborted = false;
        safetyOpen = true;
        foreach (var r in safety) r.state = 0;
        Vector3 pos; float yaw;
        StandPose(cp, out pos, out yaw);
        if (hazard)
        {
            if (hazardObj == null) hazardObj = BuildPerson();
            hazardObj.transform.position = pos + Quaternion.Euler(0, yaw, 0) * new Vector3(0.7f, 0, 0.5f);
            hazardObj.SetActive(true);
        }
        for (int i = 0; i < safety.Length; i++)
        {
            safety[i].state = 1;
            yield return W(0.55f);
            bool bad = hazard && (i == 0 || i == 1);
            safety[i].state = bad ? 3 : 2;
            if (bad)
            {
                Log("SAFE", safety[i].text + ": FAILED (person detected)", Red);
                Sfx.Play("fail");
                yield return W(1.5f);
                safetyAborted = true;
                break;
            }
            Sfx.Play("tick");
        }
        if (!safetyAborted) Log("SAFE", "Safety Guard: all checks passed", Green);
        yield return W(0.5f);
        safetyOpen = false;
    }

    GameObject BuildPerson()
    {
        var g = new GameObject("Visitor");
        var body = Util.Prim(PrimitiveType.Capsule, "body", g.transform, new Vector3(0, 0.9f, 0), new Vector3(0.48f, 0.9f, 0.34f), MatLib.Get("m_fabric_blue"), false);
        Util.Prim(PrimitiveType.Sphere, "head", g.transform, new Vector3(0, 1.72f, 0), Vector3.one * 0.24f, MatLib.Get("m_fabric_sand"), false);
        g.SetActive(false);
        return g;
    }

    public void StandPose(ControlPoint cp, out Vector3 pos, out float yaw)
    {
        Vector3 n = cp.Normal, facing = -n;
        Vector3 right = Vector3.Cross(Vector3.up, facing);
        Vector3 b = cp.transform.position; b.y = 0;
        yaw = Quaternion.LookRotation(facing).eulerAngles.y;
        pos = b + n * cp.standoff;
        float[] so = { cp.standoff, cp.standoff + 0.08f, cp.standoff + 0.2f };
        float[] lat = { 0.262f, 0.12f, 0f };
        foreach (var s in so)
            foreach (var l in lat)
            {
                Vector3 p = b + n * s - right * l;
                if (PathGrid.IsFree(p)) { pos = p; return; }
            }
    }

    // ------------------------------------------------------------------ S7
    void Learn(ControlPoint cp, Chan ch, bool ok)
    {
        float a = 0.32f;
        float before, after;
        string name;
        if (ch == null)
        {
            before = cp.pPhys; cp.pPhys = Mathf.Lerp(cp.pPhys, ok ? 1f : 0f, a); after = cp.pPhys; name = "PHYS";
        }
        else if (ch.kind == 'w') { before = cp.pWifi; cp.pWifi = Mathf.Lerp(cp.pWifi, ok ? 1f : 0f, a); after = cp.pWifi; name = "Wi-Fi"; }
        else if (ch.kind == 'i') { before = cp.pIr; cp.pIr = Mathf.Lerp(cp.pIr, ok ? 1f : 0f, a); after = cp.pIr; name = "IR"; }
        else { before = cp.pBt; cp.pBt = Mathf.Lerp(cp.pBt, ok ? 1f : 0f, a); after = cp.pBt; name = "BT"; }
        cp.mapConf = Mathf.Clamp(cp.mapConf + (ok ? 0.01f : -0.02f), 0.5f, 0.99f);
        stageState[6] = 1;
        lastLearn = cp.id + "  " + name + "  " + before.ToString("0.00") + " -> " + after.ToString("0.00");
        Log("S7", "Learn: " + lastLearn + (ok ? "  (success)" : "  (failure)"), Green);
        if (!ok || ch == null) ToastMsg("Learning update", lastLearn, Green, 2.8f);
    }

    // ------------------------------------------------------------------ return
    IEnumerator ReturnToDock()
    {
        robot.SetMood(Cyan, 0.3f);
        robot.LookForward();
        Say("Returning to the dock", "Charging until the next trigger.");
        Vector3 d = house.dockPos + new Vector3(0, 0, 0.06f);
        ShowPath(d);
        yield return robot.GoTo(d + new Vector3(0, 0, 0.9f));
        yield return robot.GoTo(d, null, 0.05f, 0.5f);
        pathLine.enabled = false;
        yield return robot.TurnTo(0f);
    }
}
