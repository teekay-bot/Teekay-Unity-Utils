using TeekayUtils;
using UnityEngine;

/// Demos both IDebugDrawer backends (spawned by DemoBootstrap in the
/// DebugDrawDemo scene):
///  - GizmosDebugDrawer via OnDrawGizmos (Scene view; Game view needs the Gizmos toggle)
///  - GLDebugDrawer via GLDebugDrawRenderer on the main camera (Game view, works in
///    builds, and under both the Built-in pipeline and URP/HDRP).
/// Both rows draw the identical showcase, so any divergence between the backends is
/// visible at a glance.
public class DebugDrawDemo : MonoBehaviour
{
    readonly GizmosDebugDrawer gizmos = new();

    void OnDrawGizmos() => DrawShowcase(gizmos, new Vector3(-6, 1.5f, 0), Time.time);

    /// Drawn through IDebugDrawer so both backends render the exact same set.
    public static void DrawShowcase(IDebugDrawer drawer, Vector3 origin, float time)
    {
        // --- sphere density ---
        drawer.WireSphere(origin, 1f, Color.green);                                 // default
        drawer.WireSphere(origin + new Vector3(0, 0, 3), 1f, Color.green, 3, 8);    // coarse
        drawer.WireSphere(origin + new Vector3(0, 0, -3), 1f, Color.green, 10, 24); // fine

        // --- partial bands: polar degrees measured from `up` (0 = pole, 90 = equator) ---
        drawer.WireSphereBand(origin + new Vector3(0, 0, 6), Vector3.up, 1f, Color.cyan, 0f, 90f, 4, 16);
        drawer.WireSphereBand(origin + new Vector3(0, 0, -6), Vector3.up, 1f, Color.magenta, 60f, 120f, 3, 16);
        drawer.WireSphereBand(origin + new Vector3(0, 0, 9), new Vector3(1, 1, 0), 1f, Color.yellow, 0f, 90f, 4, 16);

        // --- capsules: end points are the SPHERE CENTRES, as in Physics.CheckCapsule ---
        Vector3 capsule = origin + new Vector3(0, 0, 12);
        drawer.WireCapsule(capsule + Vector3.down * 0.8f, capsule + Vector3.up * 0.8f, 0.6f, Color.white, 4, 16);
        Vector3 diagonal = origin + new Vector3(0, 0, 15);
        drawer.WireCapsule(diagonal, diagonal + new Vector3(1.5f, 1.5f, 0), 0.5f, Color.white, 4, 16);

        // --- view cone: full angle, spherical far end (sweeps so the shape reads in motion) ---
        Vector3 eye = origin + new Vector3(0, 0, -9);
        var facing = new Vector3(Mathf.Sin(time * 0.5f), 0f, Mathf.Cos(time * 0.5f));
        drawer.ViewCone(eye, facing, 70f, 4f, new Color(1f, 0.6f, 0.1f), 4, 16);

        // --- primitives ---
        drawer.Circle(origin, Vector3.up, 1.5f, Color.yellow);
        drawer.WireCube(origin + new Vector3(0, 2.5f, 0), new Vector3(1, 0.5f, 1), Color.cyan);
        drawer.Arrow(origin + Vector3.up * 3.5f, Quaternion.Euler(0, time * 60f, 0) * Vector3.forward * 2f, Color.red);
        drawer.Ray(origin, Vector3.up * 3f, new Color(1f, 0.4f, 0.4f));
    }
}

/// GL side of the demo. GLDebugDrawRenderer (from the package) owns the line material
/// and the per-pipeline camera hook; this just says what to draw.
[RequireComponent(typeof(GLDebugDrawRenderer))]
public class GLDebugDrawCameraDemo : MonoBehaviour
{
    GLDebugDrawRenderer glRenderer;

    void Awake() => glRenderer = GetComponent<GLDebugDrawRenderer>();

    void OnEnable() => glRenderer.Drawing += Draw;

    void OnDisable() => glRenderer.Drawing -= Draw;

    void Draw(IDebugDrawer drawer) => DebugDrawDemo.DrawShowcase(drawer, new Vector3(6, 1.5f, 0), Time.time);
}
