using UnityEngine;
using UnityEngine.SceneManagement;

/// Central demo spawner. RuntimeInitializeOnLoadMethod only fires ONCE per play
/// session, so scene switches are handled via SceneManager.sceneLoaded instead
/// of per-demo bootstraps. Each demo scene gets exactly its own demo objects;
/// stale demo UIs from a previous scene are cleaned up on every switch.
public static class DemoBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded += (scene, _) => SpawnFor(scene.name);
        SpawnFor(SceneManager.GetActiveScene().name);
    }

    static void SpawnFor(string sceneName)
    {
        if (!sceneName.EndsWith("Demo") && sceneName != "DemoHub") return;

        DemoNavigator.EnsureExists();
        CleanupStaleDemoUIs(sceneName);

        switch (sceneName)
        {
            case "SingletonDemo":
                if (Object.FindAnyObjectByType<SingletonDemoUI>() == null)
                    new GameObject("Singleton Demo UI").AddComponent<SingletonDemoUI>();
                break;

            case "ExtensionsDemo":
                new GameObject("Extensions Demo UI").AddComponent<ExtensionsDemoUI>();
                break;

            case "DebugDrawDemo":
                new GameObject("DebugDraw Demo (Gizmos)").AddComponent<DebugDrawDemo>();
                if (Camera.main != null && Camera.main.GetComponent<GLDebugDrawCameraDemo>() == null)
                    Camera.main.gameObject.AddComponent<GLDebugDrawCameraDemo>();
                break;

            case "DevConsoleDemo":
                new GameObject("DevConsole Demo").AddComponent<DevConsoleDemo>();
                break;
        }
    }

    /// SingletonDemoUI is DontDestroyOnLoad (survives its own reload-scene demo),
    /// so it must be destroyed explicitly when the user navigates to another demo.
    static void CleanupStaleDemoUIs(string sceneName)
    {
        if (sceneName != "SingletonDemo")
        {
            var stale = Object.FindAnyObjectByType<SingletonDemoUI>();
            if (stale != null) Object.Destroy(stale.gameObject);
        }
    }
}
