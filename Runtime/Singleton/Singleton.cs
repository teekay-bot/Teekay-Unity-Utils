using UnityEngine;

namespace TeekayUtils
{
    /// <summary>
    /// Scene-local MonoBehaviour singleton. The first instance to Awake wins;
    /// later duplicates destroy their GameObject with a warning.
    /// Accessing <see cref="Instance"/> in Play mode auto-creates a GameObject
    /// if no instance exists (including inactive ones); in Edit mode or during
    /// application quit it returns null instead of creating anything.
    /// Overrides of Awake/OnDestroy/OnApplicationQuit must call base.
    /// </summary>
    public abstract class Singleton<T> : MonoBehaviour where T : Singleton<T>
    {
        protected static T instance;
        protected static bool isQuitting;

        public static bool HasInstance => instance != null;

        public static T TryGetInstance() => HasInstance ? instance : null;

        public static T Instance
        {
            get
            {
                if (instance != null) return instance;

                if (isQuitting)
                {
                    Debug.LogWarning($"[Singleton] {typeof(T).Name}.Instance requested during application quit — returning null instead of re-creating.");
                    return null;
                }

                instance = FindFirstObjectByType<T>(FindObjectsInactive.Include);
                if (instance == null && Application.isPlaying)
                {
                    var go = new GameObject($"{typeof(T).Name} (Auto-Generated)");
                    instance = go.AddComponent<T>();
                }

                return instance;
            }
        }

        protected virtual void Awake()
        {
            InitializeSingleton();
        }

        protected virtual void InitializeSingleton()
        {
            if (!Application.isPlaying) return;

            // Survives Enter Play Mode Options with domain reload disabled.
            isQuitting = false;

            if (instance == null)
            {
                instance = (T)this;
            }
            else if (instance != this)
            {
                Debug.LogWarning($"[Singleton] Duplicate {typeof(T).Name} on '{name}' destroyed — keeping '{instance.name}'.");
                Destroy(gameObject);
            }
        }

        protected virtual void OnApplicationQuit()
        {
            isQuitting = true;
        }

        protected virtual void OnDestroy()
        {
            if (instance == this) instance = null;
        }
    }
}
