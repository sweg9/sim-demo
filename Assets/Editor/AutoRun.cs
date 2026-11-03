using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Opens the main scene and enters play mode with a fixed Game view size (used for automated screenshots).</summary>
public static class AutoRun
{
    public static void Play()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        try { SetGameViewSize(1920, 1080); } catch (Exception e) { Debug.LogWarning("GameView size: " + e.Message); }
        EditorApplication.EnterPlaymode();
    }

    static void SetGameViewSize(int w, int h)
    {
        var asm = typeof(Editor).Assembly;
        var sizesType = asm.GetType("UnityEditor.GameViewSizes");
        var single = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
        var inst = single.GetProperty("instance").GetValue(null, null);
        var group = sizesType.GetMethod("GetGroup").Invoke(inst, new object[] { 0 });
        var gvsType = asm.GetType("UnityEditor.GameViewSize");
        var typeEnum = asm.GetType("UnityEditor.GameViewSizeType");
        var size = Activator.CreateInstance(gvsType, new object[] { Enum.ToObject(typeEnum, 1), w, h, "Shot" + w + "x" + h });
        group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
        int idx = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null) - 1;
        var gvType = asm.GetType("UnityEditor.GameView");
        var gv = EditorWindow.GetWindow(gvType);
        gvType.GetMethod("SizeSelectionCallback", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(gv, new object[] { idx, null });
    }
}
