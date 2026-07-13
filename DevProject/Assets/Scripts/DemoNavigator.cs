using UnityEngine;
using UnityEngine.SceneManagement;

/// Persistent bottom-left button strip for jumping between demo scenes while
/// in Play mode. Created once by DemoBootstrap; survives scene loads.
public class DemoNavigator : MonoBehaviour
{
    static DemoNavigator instance;

    static readonly string[] Scenes =
    {
        "DemoHub", "SingletonDemo", "ExtensionsDemo", "DebugDrawDemo", "DevConsoleDemo", "EventBusDemo"
    };

    public static void EnsureExists()
    {
        if (instance != null) return;
        instance = new GameObject("Demo Navigator").AddComponent<DemoNavigator>();
        DontDestroyOnLoad(instance.gameObject);
    }

    void OnGUI()
    {
        const float height = 30f;
        GUILayout.BeginArea(new Rect(10, Screen.height - height - 10, Screen.width - 20, height));
        GUILayout.BeginHorizontal();

        string current = SceneManager.GetActiveScene().name;
        foreach (string scene in Scenes)
        {
            GUI.enabled = scene != current;
            if (GUILayout.Button(scene, GUILayout.Height(height)))
                SceneManager.LoadScene(scene);
        }

        GUI.enabled = true;
        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }
}
