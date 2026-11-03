using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Builds the whole demo at runtime: lighting, house, robot, cameras, director, HUD.</summary>
public class Bootstrap : MonoBehaviour
{
    House house;
    RobotRig robot;
    CamRig camRig;
    DemoDirector dir;
    Hud hud;
    Camera mainCam, mapCam;
    RenderTexture mapRT;
    float nextMap;
    string shotDir;
    bool viewTest;

    void Awake()
    {
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 1;
        QualitySettings.pixelLightCount = 16;
        QualitySettings.antiAliasing = 4;
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
        QualitySettings.shadows = ShadowQuality.All;
        QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
        QualitySettings.shadowDistance = 30f;
        QualitySettings.shadowCascades = 4;
        QualitySettings.shadowProjection = ShadowProjection.CloseFit;
        QualitySettings.softParticles = false;
        QualitySettings.realtimeReflectionProbes = true;
    }

    void Start()
    {
        Sfx.Init(gameObject);
        SetupLighting();
        house = new GameObject("World").AddComponent<House>();
        house.Build();
        house.sky = skyLight;
        PathGrid.Build(-0.4f, -0.4f, 13.4f, 9.4f, 0.1f, 0.40f);

        robot = new GameObject("CLOiD").AddComponent<RobotRig>();
        robot.Build();
        robot.Teleport(house.dockPos + new Vector3(0, 0, 0.06f), 0f);

        SetupCameras();

        dir = new GameObject("Director").AddComponent<DemoDirector>();
        dir.house = house; dir.robot = robot; dir.cam = camRig;
        dir.Init();

        hud = new GameObject("Hud").AddComponent<Hud>();
        hud.d = dir; hud.robot = robot; hud.house = house; hud.mainCam = mainCam; hud.mapCam = mapCam; hud.mapRT = mapRT;

        house.RefreshProbes();
        ParseArgs();
    }

    Light skyLight;

    void SetupLighting()
    {
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.40f, 0.47f, 0.60f);
        RenderSettings.ambientEquatorColor = new Color(0.33f, 0.35f, 0.41f);
        RenderSettings.ambientGroundColor = new Color(0.17f, 0.16f, 0.16f);
        RenderSettings.ambientIntensity = 1f;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
        RenderSettings.reflectionIntensity = 0.6f;
        RenderSettings.fog = false;

        // dusk sky: a sun just below the horizon drives the procedural sky
        var sunG = new GameObject("SunDummy");
        var sun = sunG.AddComponent<Light>();
        sun.type = LightType.Directional; sun.intensity = 0f; sun.shadows = LightShadows.None;
        sunG.transform.rotation = Quaternion.Euler(-3f, 160f, 0f);
        RenderSettings.sun = sun;
        var sky = Resources.Load<Material>("Mats/sky_dusk");
        if (sky == null)
        {
            sky = new Material(Shader.Find("Skybox/Procedural"));
            sky.SetFloat("_SunSize", 0.02f);
            sky.SetFloat("_AtmosphereThickness", 1.35f);
            sky.SetColor("_SkyTint", new Color(0.55f, 0.50f, 0.62f));
            sky.SetColor("_GroundColor", new Color(0.10f, 0.11f, 0.13f));
            sky.SetFloat("_Exposure", 0.95f);
        }
        RenderSettings.skybox = sky;

        // cool skylight that comes in through the windows
        var g = new GameObject("SkyLight");
        skyLight = g.AddComponent<Light>();
        skyLight.type = LightType.Directional;
        skyLight.color = new Color(0.62f, 0.72f, 1f);
        skyLight.intensity = 0.42f;
        skyLight.shadows = LightShadows.Soft;
        skyLight.shadowStrength = 0.9f;
        skyLight.shadowBias = 0.03f;
        skyLight.shadowNormalBias = 0.5f;
        g.transform.rotation = Quaternion.Euler(42f, 22f, 0f);
    }

    void SetupCameras()
    {
        var mg = new GameObject("MainCamera");
        mg.tag = "MainCamera";
        mainCam = mg.AddComponent<Camera>();
        mg.AddComponent<AudioListener>();
        mainCam.clearFlags = CameraClearFlags.Skybox;
        mainCam.nearClipPlane = 0.1f; mainCam.farClipPlane = 220f;
        mainCam.allowMSAA = true;
        mainCam.depth = 0;
        camRig = mg.AddComponent<CamRig>();
        camRig.cam = mainCam; camRig.robot = robot; camRig.house = house;

        mapRT = new RenderTexture(512, 384, 16, RenderTextureFormat.ARGB32) { antiAliasing = 2, name = "MapRT" };
        var pg = new GameObject("MapCamera");
        mapCam = pg.AddComponent<Camera>();
        mapCam.orthographic = true; mapCam.orthographicSize = 5.2f;
        mapCam.transform.position = new Vector3(6.5f, 30f, 4.5f);
        mapCam.transform.rotation = Quaternion.Euler(90, 0, 0);
        mapCam.clearFlags = CameraClearFlags.SolidColor;
        mapCam.backgroundColor = new Color(0.02f, 0.03f, 0.045f);
        mapCam.cullingMask = ~(1 << Util.MinimapLayer);
        mapCam.targetTexture = mapRT;
        mapCam.depth = -10;
        mapCam.enabled = false;
        mapCam.allowMSAA = false;
    }

    void Update()
    {
        if (mapCam != null && Time.unscaledTime > nextMap)
        {
            nextMap = Time.unscaledTime + 0.08f;
            mapCam.Render();
        }
    }

    // ------------------------------------------------------------------ command line
    static string Arg(string name)
    {
        var a = Environment.GetCommandLineArgs();
        for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
        return null;
    }

    static bool Flag(string name) { return Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0; }

    void ParseArgs()
    {
        shotDir = Arg("-shots");
        dir.shotDir = shotDir;
        dir.quitAfter = Flag("-quitafter");
        string sp = Arg("-speed");
        float spd;
        if (sp != null && float.TryParse(sp, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out spd)) dir.speed = spd;
        if (Flag("-skipintro")) dir.introVisible = false;
        viewTest = Flag("-viewtest");
        if (Flag("-autodemo")) { dir.introVisible = false; dir.StartAuto(); }
        if (viewTest) StartCoroutine(ViewTest());
    }

    // fixed vantage points used to review the house and the models
    IEnumerator ViewTest()
    {
        dir.introVisible = false;
        hud.enabled = false;
        camRig.enabled = false;
        yield return null;
        var views = new (string name, Vector3 pos, Vector3 look, float fov, bool lights)[]
        {
            ("v01_dollhouse", new Vector3(-6f, 17f, -9f), new Vector3(6.5f, 0f, 4.5f), 42, false),
            ("v02_dollhouse_lit", new Vector3(-6f, 17f, -9f), new Vector3(6.5f, 0f, 4.5f), 42, true),
            ("v03_living", new Vector3(7.4f, 2.0f, 0.5f), new Vector3(2.0f, 0.8f, 3.3f), 62, true),
            ("v04_living_b", new Vector3(0.6f, 2.0f, 5.0f), new Vector3(5.5f, 0.8f, 1.8f), 62, true),
            ("v05_kitchen", new Vector3(0.7f, 2.0f, 5.4f), new Vector3(4.5f, 0.9f, 7.6f), 62, true),
            ("v06_dining", new Vector3(5.0f, 2.0f, 5.4f), new Vector3(6.6f, 0.9f, 8.0f), 62, true),
            ("v07_hall", new Vector3(12.6f, 2.0f, 3.8f), new Vector3(9.5f, 1.0f, 0.5f), 62, true),
            ("v08_bedroom", new Vector3(8.5f, 2.1f, 4.6f), new Vector3(11.5f, 0.8f, 8.0f), 62, true),
            ("v09_robot", new Vector3(5.6f, 1.5f, 1.8f), new Vector3(6.85f, 0.85f, 0.7f), 40, false),
            ("v10_switch", new Vector3(7.3f, 1.35f, 2.55f), new Vector3(7.94f, 1.25f, 3.0f), 30, false),
        };
        foreach (var v in views)
        {
            foreach (var l in house.lamps) l.level = v.lights ? 1f : 0f;
            mainCam.transform.position = v.pos;
            mainCam.transform.rotation = Quaternion.LookRotation(v.look - v.pos, Vector3.up);
            mainCam.fieldOfView = v.fov;
            bool above = v.pos.y > 2.4f;
            if (above) mainCam.cullingMask &= ~(1 << Util.MinimapLayer); else mainCam.cullingMask |= (1 << Util.MinimapLayer);
            foreach (var kv in house.wallGroups) kv.Value.localScale = new Vector3(1, (v.pos.y > 10f && (kv.Key == "S" || kv.Key == "W")) ? 0.05f : 1f, 1);
            house.RefreshProbes();
            yield return new WaitForSeconds(1.2f);
            yield return new WaitForEndOfFrame();
            Directory.CreateDirectory(shotDir);
            ScreenCapture.CaptureScreenshot(Path.Combine(shotDir, v.name + ".png"));
            yield return new WaitForSeconds(0.4f);
        }
        QuitApp();
    }

    public static void QuitApp()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        UnityEditor.EditorApplication.Exit(0);
#else
        Application.Quit();
#endif
    }
}
