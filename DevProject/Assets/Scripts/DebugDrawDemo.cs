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
        gizmos.WireSphere(center, 1f, Color.green);
        gizmos.Disc(center, Vector3.up, 1.5f, Color.yellow);
        gizmos.WireCube(center + new Vector3(0, 2f, 0), new Vector3(1, 0.5f, 1), Color.cyan);
        gizmos.Ray(center, Vector3.up * 3f, Color.red);
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

        gl.WireSphere(center, 1f, Color.green);
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
