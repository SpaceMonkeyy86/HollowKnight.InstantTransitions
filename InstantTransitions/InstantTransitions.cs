using Modding;
using System;

namespace InstantTransitions;
public class InstantTransitionsMod : Mod
{
    private static InstantTransitionsMod? _instance;

    internal static InstantTransitionsMod Instance
    {
        get
        {
            if (_instance == null)
            {
                throw new InvalidOperationException($"An instance of {nameof(InstantTransitionsMod)} was never constructed");
            }
            return _instance;
        }
    }

    public override string GetVersion() => GetType().Assembly.GetName().Version.ToString();

    public InstantTransitionsMod() : base("InstantTransitions")
    {
        _instance = this;
    }

    public override void Initialize()
    {
        Log("Initializing");

        // put additional initialization logic here

        Log("Initialized");
    }
}
