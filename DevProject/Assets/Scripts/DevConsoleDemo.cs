using TeekayUtils;
using TeekayUtils.DevConsole;
using UnityEngine;

/// Registers sample commands/CVars and shows a hint panel. Spawned by
/// DemoBootstrap in the DevConsoleDemo scene. Press F12 to open the console.
public class DevConsoleDemo : MonoBehaviour
{
    float cubeScale = 1f;
    bool autoSpin = true;
    Transform container;
    GUIStyle rich;

    void Start()
    {
        DevConsole.Initialize();
        container = new GameObject("Console Cubes").transform;

        DevConsole.RegisterCategory("Demo", new Color(0.4f, 1f, 0.6f));

        DevConsole.RegisterCommand("demo.spawncube",
            "Spawn N cubes in a ring around the origin. Usage: demo.spawncube [count]",
            args =>
            {
                int count = args.AsInt(0, 5);
                for (int i = 0; i < count; i++)
                {
                    var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cube.transform.SetParent(container);
                    cube.transform.position = Vector3.zero.RandomPointInAnnulus(2f, 5f).With(y: 0.5f);
                    cube.transform.localScale = Vector3.one * cubeScale;
                }
                DevConsole.Log("Demo", $"Spawned {count} cubes (total {container.childCount})");
            });

        DevConsole.RegisterCommand("demo.clear", "Destroy all demo cubes.", _ =>
        {
            container.DestroyChildren();
            DevConsole.Log("Demo", "Cleared cubes");
        });

        DevConsole.RegisterFloat("demo.cubescale", "Scale applied to newly spawned cubes.",
            () => cubeScale, v => cubeScale = v);

        DevConsole.RegisterBool("demo.autospin", "Whether demo cubes spin.",
            () => autoSpin, v => autoSpin = v);

        // Exercises the 2.1.0 log-view features: identical lines collapse into one ×N row,
        // and lines arriving while you're scrolled up surface the "N new" jump pill.
        DevConsole.RegisterCommand("demo.spam",
            "Log the same line N times (collapses to one ×N row), then N distinct lines. Usage: demo.spam [count]",
            args =>
            {
                int count = args.AsInt(0, 20);
                for (int i = 0; i < count; i++) DevConsole.Log("Demo", "spammy repeated line");
                for (int i = 0; i < count; i++) DevConsole.Log("Demo", $"distinct line {i}");
            });
    }

    void Update()
    {
        if (!autoSpin || container == null) return;
        container.ForEveryChild(c => c.Rotate(0f, 90f * Time.deltaTime, 0f));
    }

    void OnGUI()
    {
        rich ??= new GUIStyle(GUI.skin.label) { richText = true };
        GUILayout.BeginArea(new Rect(10, 10, 470, 120), GUI.skin.box);
        GUILayout.Label("<b>DevConsole demo</b> — press <b>F12</b> to open/close", rich);
        GUILayout.Label("Try:  help   |   demo.spawncube 10   |   demo.cubescale 2   |   demo.spam 30");
        GUILayout.Label("      demo.autospin off   |   bind F3 \"demo.spawncube 3\"   |   clear");
        GUILayout.Label("Autocomplete: type 'demo' and watch suggestions; Tab accepts the ghost hint.");
        GUILayout.Label("New: click a log line to copy it. Toolbar: Clear/Copy/Filter. Scroll up + demo.spam → jump pill.");
        GUILayout.EndArea();
    }

    void OnDestroy()
    {
        DevConsole.Unregister("demo.spawncube");
        DevConsole.Unregister("demo.clear");
        DevConsole.Unregister("demo.cubescale");
        DevConsole.Unregister("demo.autospin");
        DevConsole.Unregister("demo.spam");
    }
}
