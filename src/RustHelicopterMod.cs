using Il2CppInterop.Runtime.Injection;
using RedLoader;
using SonsSdk;
using SonsSdk.Attributes;
using SUI;
using TheForest.Utils;
using UnityEngine;
using GameState = SonsSdk.GameState;

namespace RustHelicopter;

/// <summary>
/// Rust-style minicopters for Sons of the Forest.
///
/// Spawn one with:
///   /helicopter          in multiplayer chat, when you are the host
///   helicopter           in the F1 console (single player or host); "/helicopter" is registered there too
/// Options: "helicopter refuel" fills the nearest helicopter's tank, "helicopter clear" removes them all.
/// </summary>
public class RustHelicopterMod : SonsMod
{
    private const float InteractDistance = 3.5f;

    public RustHelicopterMod()
    {
        OnUpdateCallback = OnFrame;
        OnGUICallback = Hud.Draw;
    }

    protected override void OnInitializeMod()
    {
        Config.Init();
    }

    protected override void OnSdkInitialized()
    {
        if (!ClassInjector.IsTypeRegisteredInIl2Cpp<HelicopterController>())
            ClassInjector.RegisterTypeInIl2Cpp<HelicopterController>();

        SettingsRegistry.CreateSettings(this, null, typeof(Config));

        ChatCommand.Register();
    }

    protected override void OnGameStart()
    {
        HelicopterRegistry.Prune();
        Hud.Prompt = null;

        // [DebugCommand] below registers "helicopter"; also accept the chat spelling in the F1 console.
        try
        {
            RegisterCommand("/helicopter", args =>
            {
                RunCommand(args);
                return true;
            });
        }
        catch (Exception e)
        {
            RLog.Warning($"[RustHelicopter] Could not register /helicopter in the console: {e.Message}");
        }
    }

    /// <summary>F1 console: helicopter [spawn|refuel|clear|help]</summary>
    [DebugCommand("helicopter")]
    private static void HelicopterConsoleCommand(string args) => RunCommand(args);

    /// <returns>True if the command did what was asked.</returns>
    public static bool RunCommand(string args)
    {
        var option = (args ?? "").Trim().ToLowerInvariant();
        bool ok;
        string message;

        try
        {
            switch (option)
            {
                case "":
                case "spawn":
                    ok = Spawn(out message);
                    break;
                case "refuel":
                    ok = Refuel(out message);
                    break;
                case "clear":
                case "remove":
                    ok = Clear(out message);
                    break;
                case "help":
                    ok = true;
                    message = Help;
                    break;
                default:
                    ok = false;
                    message = $"Unknown option '{option}'. {Help}";
                    break;
            }
        }
        catch (Exception e)
        {
            RLog.Error($"[RustHelicopter] '{option}' failed: {e}");
            ok = false;
            message = "Something went wrong; see the RedLoader console for details.";
        }

        Notify(message);
        return ok;
    }

    private const string Help = "Usage: helicopter (spawn), helicopter refuel, helicopter clear";

    private static bool Spawn(out string message)
    {
        if (!GameState.IsInGame)
        {
            message = "Load into the world first.";
            return false;
        }

        var heli = HelicopterSpawner.SpawnNearPlayer(out var error);
        if (!heli)
        {
            message = error;
            return false;
        }

        message = $"Minicopter ready with a full tank ({heli.FuelCapacity:0} L). Walk up to it and press {Config.Interact}.";
        return true;
    }

    private static bool Refuel(out string message)
    {
        var heli = Pilot.Current;
        if ((heli is null || !heli) && LocalPlayer.Transform)
            heli = HelicopterRegistry.FindNearest(LocalPlayer.Transform.position);

        if (!heli)
        {
            message = "There is no helicopter to refuel.";
            return false;
        }

        if (heli.IsWrecked)
        {
            message = "That helicopter is wrecked.";
            return false;
        }

        heli.Refuel();
        message = $"Refueled: {heli.Fuel:0} L";
        return true;
    }

    private static bool Clear(out string message)
    {
        Pilot.Release(true);

        var helicopters = HelicopterRegistry.All.ToList();
        foreach (var heli in helicopters)
            UnityEngine.Object.Destroy(heli.gameObject);

        message = helicopters.Count == 1 ? "Removed 1 helicopter." : $"Removed {helicopters.Count} helicopters.";
        return true;
    }

    /// <summary>Shows a message on screen (bottom left) and in the RedLoader console.</summary>
    public static void Notify(string message)
    {
        if (string.IsNullOrEmpty(message))
            return;

        RLog.Msg($"[RustHelicopter] {message}");
        SonsTools.ShowMessage(message, 4f);
    }

    private static void OnFrame()
    {
        if (!GameState.IsInGame)
        {
            Hud.Prompt = null;
            return;
        }

        var current = Pilot.Current;
        if (current is not null)
        {
            Hud.Prompt = null;

            if (!current)
            {
                // The helicopter was removed while flying.
                Pilot.Release(false);
                return;
            }

            if (GameState.IsInConsole)
                return;

            if (Input.GetKeyDown(Config.Interact))
                Pilot.Release(true);
            else if (Input.GetKeyDown(Config.CameraToggle))
                Pilot.ThirdPerson = !Pilot.ThirdPerson;

            return;
        }

        var player = LocalPlayer.Transform;
        var nearby = player ? HelicopterRegistry.FindUsable(player.position, InteractDistance) : null;
        Hud.Prompt = nearby;

        if (nearby && GameState.IsPlayerControllable && Input.GetKeyDown(Config.Interact))
            Pilot.Enter(nearby);
    }
}
