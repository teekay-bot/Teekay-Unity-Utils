using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace TeekayUtils
{
    /// <summary>
    /// Drives <see cref="GLDebugDrawer"/> on a camera: owns the line material, opens and closes the
    /// GL block, and hooks whichever render pipeline is active. Put it on the camera whose view should
    /// show the debug lines and subscribe to <see cref="Drawing"/>.
    /// <para>
    /// This exists because the camera hook differs per pipeline and getting it wrong fails silently:
    /// <c>OnPostRender</c> is only called by the Built-in pipeline, so under URP/HDRP a hand-wired
    /// GL drawer draws nothing at all — no error, no warning. SRP delivers the equivalent moment
    /// through <see cref="RenderPipelineManager.endCameraRendering"/> instead. Both are wired here,
    /// and only the one matching the active pipeline ever fires.
    /// </para>
    /// <para>
    /// <see cref="RenderPipelineManager"/> ships in UnityEngine.CoreModule, so supporting SRP costs
    /// this package no dependency on URP or HDRP.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public sealed class GLDebugDrawRenderer : MonoBehaviour
    {
        /// <summary>
        /// Raised once per render of this camera. Draw through the supplied drawer; the GL block is
        /// already open, so do not call GL.Begin/GL.End yourself.
        /// </summary>
        public event Action<IDebugDrawer> Drawing;

        readonly GLDebugDrawer _drawer = new GLDebugDrawer();
        Camera _camera;
        Material _lineMaterial;

        void Awake()
        {
            _camera = GetComponent<Camera>();

            // Unity's built-in immediate-mode colored shader; present in every pipeline.
            _lineMaterial = new Material(Shader.Find("Hidden/Internal-Colored"))
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            _lineMaterial.SetInt("_ZWrite", 0);
        }

        void OnEnable() => RenderPipelineManager.endCameraRendering += OnEndCameraRendering;

        // Unsubscribing matters: a stale static subscription outlives the component across domain
        // reloads and keeps drawing from a destroyed object.
        void OnDisable() => RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;

        void OnDestroy()
        {
            if (_lineMaterial != null) DestroyImmediate(_lineMaterial);
        }

        /// Built-in render pipeline only — never called under SRP.
        void OnPostRender() => Emit();

        /// Scriptable render pipelines (URP/HDRP) — never called under Built-in.
        void OnEndCameraRendering(ScriptableRenderContext context, Camera renderingCamera)
        {
            if (renderingCamera == _camera) Emit();
        }

        void Emit()
        {
            if (Drawing == null || _lineMaterial == null) return;

            _lineMaterial.SetPass(0);

            // Set the matrices explicitly so both pipeline paths draw identically, rather than
            // depending on whatever happens to be current when the hook fires.
            GL.PushMatrix();
            GL.LoadProjectionMatrix(_camera.projectionMatrix);
            GL.modelview = _camera.worldToCameraMatrix;
            GL.Begin(GL.LINES);

            Drawing.Invoke(_drawer);

            GL.End();
            GL.PopMatrix();
        }
    }
}
