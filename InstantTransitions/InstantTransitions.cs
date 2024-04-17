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

    public override string GetVersion() => GetType().Assembly.GetName().Version.ToString(3);

    public InstantTransitionsMod() : base("Instant Transitions")
    {
        if (_instance != null) throw new InvalidOperationException("Instance already exists");
        _instance = this;
    }

    public override void Initialize()
    {
        Hook();

        Log("Initialized");
    }

    private void Hook()
    {
        SceneLoadLogic.Hook();
        VanillaFixes.Hook();
    }

    private void Unhook()
    {
        SceneLoadLogic.Unhook();
        VanillaFixes.Unhook();
    }
}
