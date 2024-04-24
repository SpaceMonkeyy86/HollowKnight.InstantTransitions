using InstantTransitions.Hooks;
using Modding;
using System;

namespace InstantTransitions;

public class InstantTransitionsMod : Mod
{
    private static InstantTransitionsMod? _instance;
    public static InstantTransitionsMod Instance
    {
        get
        {
            if (_instance == null)
            {
                throw new InvalidOperationException("Not initialized");
            }
            return _instance;
        }
    }

    public InstantTransitionsMod() : base("Instant Transitions")
    {
        if (_instance != null) throw new InvalidOperationException("Instance already exists");
        _instance = this;
    }

    public override string GetVersion()
    {
        return GetType().Assembly.GetName().Version.ToString(3);
    }

    public override void Initialize()
    {
        Hook();

#if DEBUG
        UnityEngine.SceneManagement.SceneManagerAPI.overrideAPI = new SceneLoadLogger();
#endif

        Log("Initialized");
    }

    private void Hook()
    {
        SceneLoadLogic.Hook();
        VanillaFixes.Hook();
        RemoveCameraFade.Hook();
    }
}
