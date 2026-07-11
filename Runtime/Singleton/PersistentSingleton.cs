using UnityEngine;

namespace TeekayUtils
{
    /// <summary>
    /// MonoBehaviour singleton that survives scene loads (DontDestroyOnLoad).
    /// Inherits all <see cref="Singleton{T}"/> behavior: first-Awake-wins,
    /// duplicate self-destroy, quit guard, no auto-create in Edit mode.
    /// Overrides of Awake/OnDestroy/OnApplicationQuit must call base.
    /// </summary>
    public abstract class PersistentSingleton<T> : Singleton<T> where T : PersistentSingleton<T>
    {
        [Tooltip("Detach from any parent on Awake so DontDestroyOnLoad applies to this object alone.")]
        public bool AutoUnparentOnAwake = true;

        protected override void InitializeSingleton()
        {
            if (!Application.isPlaying) return;

            if (AutoUnparentOnAwake)
            {
                transform.SetParent(null);
            }

            base.InitializeSingleton();

            if (instance == this)
            {
                DontDestroyOnLoad(gameObject);
            }
        }
    }
}
