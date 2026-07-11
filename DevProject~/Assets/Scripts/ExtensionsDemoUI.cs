using System.Collections.Generic;
using System.Linq;
using TeekayUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// Self-spawning demo panel (right side) exercising the runtime extensions
/// visually. Sample scene only — never touches PlayMode test runs.
public class ExtensionsDemoUI : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (SceneManager.GetActiveScene().name != "Sample") return;
        new GameObject("Extensions Demo UI").AddComponent<ExtensionsDemoUI>();
    }

    readonly List<Color> palette = new()
    {
        Color.red, Color.yellow, Color.green, Color.cyan, Color.blue, Color.magenta
    };

    Transform container;
    GameObject orbitCenter;
    GameObject orbiter;
    Rigidbody ball;
    CanvasGroup uiPanel;
    bool childrenVisible = true;
    GUIStyle rich;

    void Awake()
    {
        container = new GameObject("Cubes Container").transform;
        BuildCanvasGroupPanel();
    }

    void BuildCanvasGroupPanel()
    {
        var canvasGo = new GameObject("Demo Canvas", typeof(Canvas), typeof(CanvasGroup));
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        uiPanel = canvasGo.GetComponent<CanvasGroup>();

        var image = new GameObject("Panel", typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(canvasGo.transform, false);
        image.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        image.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        image.rectTransform.pivot = new Vector2(0.5f, 1f);
        image.rectTransform.anchoredPosition = new Vector2(0, -10);
        image.rectTransform.sizeDelta = new Vector2(260, 50);
        image.color = Color.green.SetAlpha(0.5f); // ColorExtensions.SetAlpha
    }

    void Update()
    {
        // Vector2.Rotate + ToVector3XZ: orbiter circles the center at 90 deg/s.
        if (orbiter != null && orbitCenter != null)
        {
            Vector3 offset = orbiter.transform.position - orbitCenter.transform.position;
            Vector2 flat = new Vector2(offset.x, offset.z).Rotate(90f * Time.deltaTime);
            orbiter.transform.position = orbitCenter.transform.position + flat.ToVector3XZ();
        }
    }

    void OnGUI()
    {
        rich ??= new GUIStyle(GUI.skin.label) { richText = true };

        GUILayout.BeginArea(new Rect(Screen.width - 480, 10, 470, 560), GUI.skin.box);
        GUILayout.Label("<b>TeekayUtils Extensions demo</b>", rich);
        GUILayout.Label($"Cubes: {container.Children().Count()}"); // TransformExtensions.Children
        GUILayout.Label("Rich text: " + "bold".RichBold() + " " + "yellow".RichColor("yellow"), rich); // StringExtensions

        if (GUILayout.Button("1. Spawn 15 cubes in annulus r3-6  (RandomPointInAnnulus + With + Random)"))
            SpawnCubes();

        if (GUILayout.Button("2. Quantize positions to 1m grid  (Children + ForEach + Quantize)"))
            container.Children().ForEach(c => c.position = c.position.Quantize(Vector3.one));

        if (GUILayout.Button("3. Shuffle cube colors  (Shuffle)"))
            ShuffleColors();

        if (GUILayout.Button($"4. {(childrenVisible ? "Disable" : "Enable")} children  (Dis/EnableChildren)"))
        {
            childrenVisible = !childrenVisible;
            if (childrenVisible) container.EnableChildren();
            else container.DisableChildren();
        }

        if (GUILayout.Button("5. Destroy children  (DestroyChildren)"))
            container.DestroyChildren();

        if (GUILayout.Button("6. Spawn orbit pair  (Vector2.Rotate + ToVector3XZ moi frame)"))
            SpawnOrbitPair();

        GUILayout.Space(6);
        GUILayout.Label(ball != null
            ? $"Ball speed: {ball.linearVelocity.magnitude:F2}  (huong: {ball.linearVelocity.normalized})"
            : "Ball: none");

        if (GUILayout.Button("7. Spawn ball, velocity (3,4,0) — speed 5  (GetOrAdd)"))
            SpawnBall();

        if (GUILayout.Button("8. Ball.ChangeDirection(forward) — speed phai giu nguyen 5"))
            ball?.ChangeDirection(Vector3.forward);

        if (GUILayout.Button("9. Ball.Stop()"))
            ball?.Stop();

        GUILayout.Space(6);

        if (GUILayout.Button("10. Toggle CanvasGroup panel  (Show/Hide)"))
        {
            if (uiPanel.alpha > 0) uiPanel.Hide();
            else uiPanel.Show();
        }

        if (GUILayout.Button("11. Log misc  (PathFull, IsInLayerMask, IsOdd, Remap, Slice)"))
            LogMisc();

        GUILayout.EndArea();
    }

    void SpawnCubes()
    {
        for (int i = 0; i < 15; i++)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = $"Cube{i}";
            cube.transform.SetParent(container);
            // annulus point is on the XZ plane; lift it so cubes sit on the ground
            cube.transform.position = Vector3.zero.RandomPointInAnnulus(3f, 6f).With(y: 0.5f);
            cube.GetComponent<Renderer>().material.color = palette.Random(); // CollectionExtensions.Random
        }
        childrenVisible = true;
    }

    void ShuffleColors()
    {
        var colors = palette.ToList();
        colors.Shuffle();
        int i = 0;
        container.Children().ForEach(c =>
            c.GetComponent<Renderer>().material.color = colors[i++ % colors.Count]);
    }

    void SpawnOrbitPair()
    {
        if (orbitCenter != null) return;
        orbitCenter = GameObject.CreatePrimitive(PrimitiveType.Cube);
        orbitCenter.name = "Orbit Center";
        orbitCenter.transform.position = new Vector3(0, 2.5f, 0);

        orbiter = GameObject.CreatePrimitive(PrimitiveType.Cube);
        orbiter.name = "Orbiter";
        orbiter.transform.localScale = Vector3.one * 0.4f;
        orbiter.transform.position = orbitCenter.transform.position.Add(x: 2f); // Vector3Extensions.Add
    }

    void SpawnBall()
    {
        if (ball != null) Destroy(ball.gameObject);
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "Ball";
        sphere.transform.position = new Vector3(-3, 1, 0);
        ball = sphere.GetOrAdd<Rigidbody>(); // GameObjectExtensions.GetOrAdd
        ball.useGravity = false;
        ball.linearVelocity = new Vector3(3, 4, 0);
    }

    void LogMisc()
    {
        if (orbiter != null)
        {
            Debug.Log($"PathFull: {orbiter.PathFull()}");
            orbiter.SetLayersRecursively(5);
            Debug.Log($"IsInLayerMask(1<<5): {orbiter.IsInLayerMask(1 << 5)}");
        }

        Debug.Log($"(-3).IsOdd() = {(-3).IsOdd()} (bug goc tra ve false)");
        Debug.Log($"7f.Remap(0,10 -> 0,100) = {7f.Remap(0f, 10f, 0f, 100f)}");
        Debug.Log($"\"teekay\".Slice(1,-1) = {"teekay".Slice(1, -1)}");
        Debug.Log($"12.AtLeast(20) = {12.AtLeast(20)}, 12.AtMost(5) = {12.AtMost(5)}");
        Debug.Log(ColorExtensions.FromHex("#FF8800").ToHex());
    }
}
