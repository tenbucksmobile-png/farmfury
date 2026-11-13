using UnityEngine;

namespace FarmFury.Shared.Utilities
{
    /// <summary>
    /// Base class for scene-persistent manager singletons. Expects exactly one instance
    /// per GameObject in the GameManagers hierarchy; does not auto-create or DontDestroyOnLoad
    /// itself, since Farm Fury: Stampede is single-scene and managers live for the app lifetime.
    /// </summary>
    public abstract class MonoSingleton<T> : MonoBehaviour where T : MonoSingleton<T>
    {
        public static T Instance { get; private set; }

        protected virtual void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[{typeof(T).Name}] Duplicate instance on '{gameObject.name}' destroyed.");
                Destroy(gameObject);
                return;
            }

            Instance = (T)this;
        }

        protected virtual void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
