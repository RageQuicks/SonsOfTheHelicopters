using RedLoader;
using UnityEngine;

namespace RustHelicopter;

/// <summary>
/// Settings, saved to UserData\RustHelicopter.cfg and editable in the game's mod settings menu.
/// </summary>
public static class Config
{
    public static ConfigCategory Category { get; private set; }

    // Fuel
    public static ConfigEntry<float> FuelCapacity { get; private set; }
    public static ConfigEntry<float> FuelBurnIdle { get; private set; }
    public static ConfigEntry<float> FuelBurnFull { get; private set; }

    // Flight
    public static ConfigEntry<float> LiftPower { get; private set; }
    public static ConfigEntry<float> MaxTilt { get; private set; }
    public static ConfigEntry<float> YawSpeed { get; private set; }
    public static ConfigEntry<float> AutoLevel { get; private set; }
    public static ConfigEntry<float> MouseSensitivity { get; private set; }
    public static ConfigEntry<bool> InvertMouseY { get; private set; }
    public static ConfigEntry<bool> CrashDamage { get; private set; }

    // Camera and player
    public static ConfigEntry<bool> StartInThirdPerson { get; private set; }
    public static ConfigEntry<float> ThirdPersonDistance { get; private set; }
    public static ConfigEntry<float> LookSensitivity { get; private set; }
    public static ConfigEntry<bool> HidePlayerBody { get; private set; }

    // Spawning and keys
    public static ConfigEntry<float> SpawnDistance { get; private set; }
    public static ConfigEntry<string> InteractKey { get; private set; }
    public static ConfigEntry<string> CameraKey { get; private set; }
    public static ConfigEntry<string> FreeLookKey { get; private set; }

    public static void Init()
    {
        Category = ConfigSystem.CreateFileCategory("RustHelicopter", "Rust Helicopter", "RustHelicopter.cfg");

        FuelCapacity = Category.CreateEntry("fuel_capacity", 100f, "Fuel capacity (L)",
            "Size of the fuel tank. Every spawned helicopter starts with a full tank.");
        FuelCapacity.SetRange(10f, 500f);

        FuelBurnIdle = Category.CreateEntry("fuel_burn_idle", 0.5f, "Fuel use at idle (L/min)",
            "Fuel burned per minute while the engine runs with no throttle.");
        FuelBurnIdle.SetRange(0f, 10f);

        FuelBurnFull = Category.CreateEntry("fuel_burn_full", 2f, "Fuel use at full throttle (L/min)",
            "Fuel burned per minute while climbing at full throttle.");
        FuelBurnFull.SetRange(0f, 20f);

        LiftPower = Category.CreateEntry("lift_power", 0.65f, "Climb power",
            "Extra lift at full throttle, as a fraction of the thrust needed to hover.");
        LiftPower.SetRange(0.2f, 1.5f);

        MaxTilt = Category.CreateEntry("max_tilt", 35f, "Max tilt (degrees)",
            "How far the helicopter can pitch or roll. More tilt means more speed.");
        MaxTilt.SetRange(10f, 60f);

        YawSpeed = Category.CreateEntry("yaw_speed", 70f, "Turn speed (degrees/s)",
            "How fast A/D turn the helicopter.");
        YawSpeed.SetRange(20f, 180f);

        AutoLevel = Category.CreateEntry("auto_level", 10f, "Auto level (degrees/s)",
            "How fast the helicopter levels out when you stop pushing the mouse. 0 keeps the tilt you set.");
        AutoLevel.SetRange(0f, 90f);

        MouseSensitivity = Category.CreateEntry("mouse_sensitivity", 1.5f, "Mouse flight sensitivity",
            "How much moving the mouse tilts the helicopter.");
        MouseSensitivity.SetRange(0.1f, 5f);

        InvertMouseY = Category.CreateEntry("invert_mouse_y", false, "Invert mouse pitch",
            "Mouse forward pitches the nose up instead of down.");

        CrashDamage = Category.CreateEntry("crash_damage", true, "Crash damage",
            "Hard impacts damage the helicopter. At zero health it is wrecked.");

        StartInThirdPerson = Category.CreateEntry("start_third_person", false, "Start in third person",
            "Use the chase camera when you get in. Toggle while flying with the camera key.");

        ThirdPersonDistance = Category.CreateEntry("third_person_distance", 9f, "Chase camera distance (m)",
            "How far behind the helicopter the chase camera sits.");
        ThirdPersonDistance.SetRange(4f, 20f);

        LookSensitivity = Category.CreateEntry("look_sensitivity", 2f, "Free look sensitivity",
            "Mouse sensitivity while holding the free look key.");
        LookSensitivity.SetRange(0.2f, 6f);

        HidePlayerBody = Category.CreateEntry("hide_player_body", true, "Hide player body while flying",
            "Hides your character model in the pilot seat so it doesn't clip through the cockpit.");

        SpawnDistance = Category.CreateEntry("spawn_distance", 7f, "Spawn distance (m)",
            "How far in front of you /helicopter places the helicopter.");
        SpawnDistance.SetRange(4f, 20f);

        InteractKey = Category.CreateEntry("key_interact", "E", "Enter / exit key",
            "Unity KeyCode name, for example E, F or Return.");

        CameraKey = Category.CreateEntry("key_camera", "V", "Camera toggle key",
            "Switches between cockpit and chase camera while flying.");

        FreeLookKey = Category.CreateEntry("key_free_look", "LeftAlt", "Free look key",
            "Hold to look around with the mouse without steering.");
    }

    public static KeyCode Interact => ParseKey(InteractKey, KeyCode.E);
    public static KeyCode CameraToggle => ParseKey(CameraKey, KeyCode.V);
    public static KeyCode FreeLook => ParseKey(FreeLookKey, KeyCode.LeftAlt);

    private static KeyCode ParseKey(ConfigEntry<string> entry, KeyCode fallback)
    {
        var text = entry?.Value;
        return !string.IsNullOrWhiteSpace(text) && Enum.TryParse(text.Trim(), true, out KeyCode key) ? key : fallback;
    }
}
