using TeekayUtils;
using UnityEngine;

/// Demos both IDebugDrawer backends (spawned by DemoBootstrap in the
/// DebugDrawDemo scene):
///  - GizmosDebugDrawer via OnDrawGizmos (Scene view; Game view needs the Gizmos toggle)
///  - GLDebugDrawer via OnPostRender on the main camera (Game view, works in builds).
/// Shows the manual GL wiring pattern the package expects from consumers.
public class DebugDrawDemo : MonoBehaviour
{
    readonly GizmosDebugDrawer gizmos = new();

    void OnDrawGizmos()
    {
        var center = new Vector3(-6, 1.5f, 0);
        DrawSphereShowcase(gizmos, center);
        gizmos.Disc(center, Vector3.up, 1.5f, Color.yellow);
        gizmos.WireCube(center + new Vector3(0, 2f, 0), new Vector3(1, 0.5f, 1), Color.cyan);
        gizmos.Ray(center, Vector3.up * 3f, Color.red);
    }

    /// Drawn through IDebugDrawer so both backends render the exact same set — put the Gizmos
    /// row and the GL row side by side and they should be indistinguishable.
    public static void DrawSphereShowcase(IDebugDrawer drawer, Vector3 origin)
    {
        drawer.WireSphere(origin, 1f, Color.green);                                  // default density
        drawer.WireSphere(origin + new Vector3(0, 0, 3), 1f, Color.green, 3, 8);     // coarse
        drawer.WireSphere(origin + new Vector3(0, 0, -3), 1f, Color.green, 10, 24);  // fine

        // Polar angles measured from `up`: 0 = the pole along up, 90 = equator, 180 = opposite pole.
        drawer.WireSphereBand(origin + new Vector3(0, 0, 6), Vector3.up, 1f, Color.cyan, 0f, 90f, 4, 16);
        drawer.WireSphereBand(origin + new Vector3(0, 0, -6), Vector3.up, 1f, Color.magenta, 60f, 120f, 3, 16);
        drawer.WireSphereBand(origin + new Vector3(0, 0, 9), new Vector3(1, 1, 0), 1f, Color.yellow, 0f, 90f, 4, 16);
    }
}

/// GL side of the demo: must live on the camera GameObject for OnPostRender
/// (built-in render pipeline).
public class GLDebugDrawCameraDemo : MonoBehaviour
{
    readonly GLDebugDrawer gl = new();
    Material lineMaterial;

    void Awake()
    {
        // Standard immediate-mode line material — the wiring GLDebugDrawer expects.
        lineMaterial = new Material(Shader.Find("Hidden/Internal-Colored"))
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        lineMaterial.SetInt("_ZWrite", 0);
    }

    void OnPostRender()
    {
        float t = Time.time;
        var center = new Vector3(6, 1.5f, 0);

        lineMaterial.SetPass(0);
        GL.Begin(GL.LINES);

        DebugDrawDemo.DrawSphereShowcase(gl, center);
        gl.Disc(center, Vector3.up, 1.5f + Mathf.PingPong(t, 1f), Color.yellow);
        gl.WireCube(center + new Vector3(0, 2f, 0), new Vector3(1, 0.5f, 1), Color.cyan);
        gl.Sphere(center + new Vector3(Mathf.Cos(t) * 2f, 0, Mathf.Sin(t) * 2f), 0.15f, Color.magenta);
        gl.Line(center, center + new Vector3(Mathf.Cos(t) * 2f, 0, Mathf.Sin(t) * 2f), Color.white);

        GL.End();
    }

    void OnDestroy()
    {
        if (lineMaterial != null) DestroyImmediate(lineMaterial);
    }
}
