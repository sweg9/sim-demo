using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class Setup
{
    const string ScenePath = "Assets/Scenes/Main.unity";

    [MenuItem("CLOiD/Setup project")]
    public static void Run()
    {
        ConfigureModels();
        BakeMaterials();
        CreateScene();
        ConfigureSettings();
        AssetDatabase.SaveAssets();
        Debug.Log("CLOiD setup done");
    }

    static void ConfigureModels()
    {
        foreach (var f in new[] { "CLOiD", "Furniture", "Switches" })
        {
            string p = "Assets/Resources/Models/" + f + ".fbx";
            var imp = AssetImporter.GetAtPath(p) as ModelImporter;
            if (imp == null) { Debug.LogWarning("Missing " + p); continue; }
            imp.importNormals = ModelImporterNormals.Import;
            imp.importTangents = ModelImporterTangents.None;
            imp.importCameras = false;
            imp.importLights = false;
            imp.importBlendShapes = false;
            imp.importVisibility = false;
            imp.isReadable = false;
            imp.meshCompression = ModelImporterMeshCompression.Off;
            imp.optimizeMeshPolygons = true;
            imp.optimizeMeshVertices = true;
            imp.weldVertices = true;
            imp.SaveAndReimport();
        }
    }

    [MenuItem("CLOiD/Bake materials")]
    public static void BakeMaterials()
    {
        Directory.CreateDirectory("Assets/Resources/Mats");
        foreach (var n in MatLib.Names)
        {
            string p = "Assets/Resources/Mats/" + n + ".mat";
            var m = MatLib.Create(n);
            var old = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (old != null) { EditorUtility.CopySerialized(m, old); Object.DestroyImmediate(m); }
            else AssetDatabase.CreateAsset(m, p);
        }
        BakeSky();
        AssetDatabase.SaveAssets();
    }

    static void BakeSky()
    {
        var sh = Shader.Find("Skybox/Procedural");
        if (sh == null) { Debug.LogWarning("Skybox/Procedural not found"); return; }
        string p = "Assets/Resources/Mats/sky_dusk.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, p); }
        m.shader = sh;
        m.SetFloat("_SunSize", 0.02f);
        m.SetFloat("_SunSizeConvergence", 5f);
        m.SetFloat("_AtmosphereThickness", 1.35f);
        m.SetColor("_SkyTint", new Color(0.55f, 0.50f, 0.62f));
        m.SetColor("_GroundColor", new Color(0.10f, 0.11f, 0.13f));
        m.SetFloat("_Exposure", 0.95f);
        EditorUtility.SetDirty(m);
    }

    static void CreateScene()
    {
        Directory.CreateDirectory("Assets/Scenes");
        var sc = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("Bootstrap").AddComponent<Bootstrap>();
        EditorSceneManager.SaveScene(sc, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
    }

    static void ConfigureSettings()
    {
        PlayerSettings.SplashScreen.show = false;
        PlayerSettings.SplashScreen.showUnityLogo = false;
        PlayerSettings.productName = "CLOiD Demo";
        PlayerSettings.companyName = "HYU IS";
        PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.defaultScreenWidth = 1920;
        PlayerSettings.defaultScreenHeight = 1080;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;
        PlayerSettings.useMacAppStoreValidation = false;
        PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone, "kr.ac.hanyang.cloid.demo");
    }

    public static void Build()
    {
        Directory.CreateDirectory("Builds");
        var opt = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = "Builds/CLOiD-Demo.app",
            target = BuildTarget.StandaloneOSX,
            options = BuildOptions.None,
        };
        var rep = BuildPipeline.BuildPlayer(opt);
        Debug.Log("BUILD " + rep.summary.result + " " + rep.summary.totalSize);
        if (rep.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
    }

    public static void BuildWin()
    {
        Directory.CreateDirectory("Builds/Windows");
        var opt = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = "Builds/Windows/CLOiD-Demo.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        };
        var rep = BuildPipeline.BuildPlayer(opt);
        Debug.Log("BUILD " + rep.summary.result + " " + rep.summary.totalSize);
        if (rep.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
    }
}
