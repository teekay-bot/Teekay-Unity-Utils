using UnityEngine;
using UnityEngine.SceneManagement;

/// Drives the singleton demos via IMGUI. Spawned by DemoBootstrap in the
/// SingletonDemo scene (never during PlayMode test runs).
public class SingletonDemoUI : MonoBehaviour
{
    void Awake()
    {
        // Survive the reload-scene demo; RuntimeInitializeOnLoadMethod only fires once per play session.
        DontDestroyOnLoad(gameObject);
    }

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 460, 320), GUI.skin.box);
        GUILayout.Label("<b>TeekayUtils Singleton demo</b> — watch the Console too", new GUIStyle(GUI.skin.label) { richText = true });

        var gm = DemoGameManager.TryGetInstance();
        GUILayout.Label(gm != null
            ? $"DemoGameManager (persistent): '{gm.name}' id {gm.GetInstanceID()}, score {gm.Score}"
            : "DemoGameManager (persistent): none");

        var counter = DemoSceneCounter.TryGetInstance();
        GUILayout.Label(counter != null
            ? $"DemoSceneCounter (scene-local): clicks {counter.Clicks}"
            : "DemoSceneCounter (scene-local): none");

        GUILayout.Space(8);

        if (GUILayout.Button("1. DemoGameManager.Instance  →  auto-create + DontDestroyOnLoad"))
            _ = DemoGameManager.Instance;

        if (GUILayout.Button("2. Add score +10"))
            DemoGameManager.Instance.AddScore(10);

        if (GUILayout.Button("3. Spawn DUPLICATE DemoGameManager  →  self-destroys + warning"))
            new GameObject("Duplicate GM").AddComponent<DemoGameManager>();

        if (GUILayout.Button("4. DemoSceneCounter.Instance.Add()  →  auto-create, clicks++"))
            DemoSceneCounter.Instance.Add();

        if (GUILayout.Button("5. Reload scene  →  GameManager + score survive, SceneCounter dies"))
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

        GUILayout.EndArea();
    }
}
