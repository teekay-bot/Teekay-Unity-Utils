using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// One-shot setup that switches DevProject onto URP: creates the pipeline and renderer
/// assets and makes them active for every quality level. DevProject exists to verify the
/// package the way it is actually consumed, and consuming projects run URP.
/// <para>Run once via the menu item, then convert materials with
/// Window ▸ Rendering ▸ Render Pipeline Converter ▸ Built-in to URP.</para>
/// Safe to re-run: existing assets are reused rather than duplicated.
public static class UrpSetup
{
    const string SettingsFolder = "Assets/Settings";
    const string RendererPath = SettingsFolder + "/DevProject_Renderer.asset";
    const string PipelinePath = SettingsFolder + "/DevProject_URP.asset";

    [MenuItem("Tools/DevProject/Set up URP")]
    public static void SetUp()
    {
        if (!AssetDatabase.IsValidFolder(SettingsFolder))
        {
            AssetDatabase.CreateFolder("Assets", "Settings");
        }

        UniversalRendererData rendererData = GetOrCreate<UniversalRendererData>(RendererPath);
        UniversalRenderPipelineAsset pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
        if (pipeline == null)
        {
            pipeline = UniversalRenderPipelineAsset.Create(rendererData);
            AssetDatabase.CreateAsset(pipeline, PipelinePath);
        }

        GraphicsSettings.defaultRenderPipeline = pipeline;

        // Quality levels each carry their own pipeline override; leaving any of them unset
        // silently drops that level back to the built-in pipeline.
        int originalQuality = QualitySettings.GetQualityLevel();
        for (int i = 0; i < QualitySettings.count; i++)
        {
            QualitySettings.SetQualityLevel(i);
            QualitySettings.renderPipeline = pipeline;
        }
        QualitySettings.SetQualityLevel(originalQuality);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"URP is now active ({PipelinePath}). Next: Window ▸ Rendering ▸ Render Pipeline " +
                  "Converter ▸ Built-in to URP to upgrade the demo materials.");
    }

    static T GetOrCreate<T>(string path) where T : ScriptableObject
    {
        T existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null) return existing;

        var created = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(created, path);
        return created;
    }
}
