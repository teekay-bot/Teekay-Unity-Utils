using TeekayUtils;

/// Demo: scene-local singleton — dies with the scene, counter resets on reload.
public class DemoSceneCounter : Singleton<DemoSceneCounter>
{
    public int Clicks { get; private set; }

    public void Add() => Clicks++;
}
