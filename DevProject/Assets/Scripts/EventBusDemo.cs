using System.Collections.Generic;
using TeekayUtils;
using TeekayUtils.Events;
using UnityEngine;

/// Demonstrates decoupled pub/sub: the LEFT panel only PUBLISHES intents, the
/// spawner/scorer below only SUBSCRIBE — neither knows the other exists.
/// Spawned by DemoBootstrap in the EventBusDemo scene.
public class EventBusDemo : MonoBehaviour
{
    // Events: small immutable structs carrying just the intent's data.
    public struct SpawnCubeRequest : IEvent { public int Count; }
    public struct ScoreChanged : IEvent { public int Delta; }
    public struct ClearArena : IEvent { }

    Transform container;
    int score;
    readonly List<string> log = new();
    GUIStyle rich;

    void OnEnable()
    {
        container = new GameObject("EventBus Cubes").transform;
        EventBus.Subscribe<SpawnCubeRequest>(OnSpawnRequested);
        EventBus.Subscribe<ScoreChanged>(OnScoreChanged);
        EventBus.Subscribe<ClearArena>(OnClearArena);
        EventBus.AnyPublished += OnAnyPublished;
    }

    void OnDisable()
    {
        EventBus.Unsubscribe<SpawnCubeRequest>(OnSpawnRequested);
        EventBus.Unsubscribe<ScoreChanged>(OnScoreChanged);
        EventBus.Unsubscribe<ClearArena>(OnClearArena);
        EventBus.AnyPublished -= OnAnyPublished;
    }

    // ── Subscribers (the "gameplay systems") ──

    void OnSpawnRequested(SpawnCubeRequest e)
    {
        for (int i = 0; i < e.Count; i++)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(container);
            cube.transform.position = Vector3.zero.RandomPointInAnnulus(2f, 5f).With(y: 0.5f);
        }
    }

    void OnScoreChanged(ScoreChanged e) => score += e.Delta;

    void OnClearArena(ClearArena e) => container.DestroyChildren();

    void OnAnyPublished(IEvent e)
    {
        log.Add($"[{Time.frameCount}] {e.GetType().Name}");
        if (log.Count > 8) log.RemoveAt(0);
    }

    // ── Publisher (the "input side" — knows nothing about the systems above) ──

    void OnGUI()
    {
        rich ??= new GUIStyle(GUI.skin.label) { richText = true };

        GUILayout.BeginArea(new Rect(10, 10, 470, 330), GUI.skin.box);
        GUILayout.Label("<b>EventBus demo</b> — nut chi PUBLISH, he thong chi SUBSCRIBE", rich);
        GUILayout.Label($"Score: {score}   |   Cubes: {container.childCount}");

        if (GUILayout.Button("Publish SpawnCubeRequest { Count = 5 }"))
            EventBus.Publish(new SpawnCubeRequest { Count = 5 });

        if (GUILayout.Button("Publish ScoreChanged { Delta = +10 }"))
            EventBus.Publish(new ScoreChanged { Delta = 10 });

        if (GUILayout.Button("Publish ClearArena"))
            EventBus.Publish(new ClearArena());

        GUILayout.Space(6);
        GUILayout.Label("<b>AnyPublished stream</b> (hook cho tooling):", rich);
        foreach (string line in log)
            GUILayout.Label(line);

        GUILayout.EndArea();
    }
}
