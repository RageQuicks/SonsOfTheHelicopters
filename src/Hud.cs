using SonsSdk;
using UnityEngine;

namespace RustHelicopter;

/// <summary>
/// The flight readout (fuel, hull, engine, altitude, speed) while flying, and the "get in" prompt when standing
/// next to a helicopter. Plain IMGUI, drawn from the mod's OnGUI callback.
/// </summary>
public static class Hud
{
    private const int BarSegments = 20;

    /// <summary>The helicopter the player could get into right now, if any.</summary>
    public static HelicopterController Prompt { get; set; }

    public static void Draw()
    {
        if (!GameState.IsInGame || GameState.IsInConsole)
            return;

        var heli = Pilot.Current;
        var prompt = Prompt;
        var flying = heli is not null && heli;
        if (!flying && (prompt is null || !prompt))
            return;

        var scale = Mathf.Max(1f, Screen.height / 1080f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        var width = Screen.width / scale;
        var height = Screen.height / scale;

        if (flying)
            DrawFlightPanel(heli, width, height);
        else
            DrawPrompt(prompt, width, height);

        GUI.matrix = Matrix4x4.identity;
    }

    private static void DrawFlightPanel(HelicopterController heli, float width, float height)
    {
        var fuel01 = heli.FuelCapacity > 0f ? heli.Fuel / heli.FuelCapacity : 0f;
        var velocity = heli.Velocity;
        var altitude = float.IsInfinity(heli.GroundDistance) ? "--" : $"{heli.GroundDistance:0}";
        var collective = heli.ThrottleInput > 0f ? "CLIMB" : heli.ThrottleInput < 0f ? "DESCEND" : "HOLD";

        GUILayout.BeginArea(new Rect(width - 380f, height - 250f, 360f, 230f));
        GUILayout.BeginVertical(GUI.skin.box);

        GUILayout.Label("MINICOPTER");
        GUILayout.Label($"Fuel    {Bar(fuel01)}  {heli.Fuel:0.0} / {heli.FuelCapacity:0} L{(fuel01 < 0.15f ? "  LOW" : "")}");
        GUILayout.Label($"Hull    {Bar(heli.Health01)}  {heli.Health01 * 100f:0}%");
        GUILayout.Label($"Engine  {heli.EngineStatus}    Rotor {heli.RotorSpeed01 * 100f:0}%    {collective}");
        GUILayout.Label($"Alt {altitude} m    Speed {velocity.magnitude * 3.6f:0} km/h    Climb {velocity.y:+0.0;-0.0;0.0} m/s");
        GUILayout.Label($"W/S throttle  A/D turn  Mouse tilt  {Config.FreeLook} look  {Config.CameraToggle} camera  {Config.Interact} exit");

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    private static void DrawPrompt(HelicopterController heli, float width, float height)
    {
        var fuel01 = heli.FuelCapacity > 0f ? heli.Fuel / heli.FuelCapacity : 0f;

        GUILayout.BeginArea(new Rect(width * 0.5f - 170f, height * 0.62f, 340f, 80f));
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label($"[{Config.Interact}] Fly minicopter");
        GUILayout.Label($"Fuel {fuel01 * 100f:0}%    Hull {heli.Health01 * 100f:0}%");
        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    private static string Bar(float fraction)
    {
        var filled = Mathf.RoundToInt(Mathf.Clamp01(fraction) * BarSegments);
        return "[" + new string('|', filled) + new string('.', BarSegments - filled) + "]";
    }
}
