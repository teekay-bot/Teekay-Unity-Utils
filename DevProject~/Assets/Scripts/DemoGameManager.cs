using TeekayUtils;
using UnityEngine;

/// Demo: persistent singleton — survives scene reloads, keeps its score.
public class DemoGameManager : PersistentSingleton<DemoGameManager>
{
    public int Score { get; private set; }

    public void AddScore(int amount) => Score += amount;

    protected override void Awake()
    {
        base.Awake();
        Debug.Log($"[Demo] DemoGameManager.Awake on '{name}' (id {GetInstanceID()})");
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        Debug.Log($"[Demo] DemoGameManager.OnDestroy on '{name}' (id {GetInstanceID()})");
    }
}
