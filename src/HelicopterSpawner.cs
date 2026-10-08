using TheForest.Utils;
using UnityEngine;

namespace RustHelicopter;

/// <summary>
/// Finds open, fairly flat ground in front of the player and puts a helicopter there.
/// </summary>
public static class HelicopterSpawner
{
    private const float MaxSlope = 30f;
    private const float MaxHeightDifference = 15f;

    // Try straight ahead first, then fan out to the sides and behind.
    private static readonly float[] Angles = { 0f, 30f, -30f, 60f, -60f, 100f, -100f, 180f };

    public static HelicopterController SpawnNearPlayer(out string error)
    {
        error = null;
        var player = LocalPlayer.Transform;
        if (!player)
        {
            error = "Load into the world first.";
            return null;
        }

        var cam = LocalPlayer.MainCamTr;
        var forward = cam ? cam.forward : player.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f)
            forward = player.forward;
        forward.y = 0f;
        forward.Normalize();

        var distance = Config.SpawnDistance.Value;

        // Room for the rotor and the body first, then just the body (the forest is dense), then any flat-enough ground.
        foreach (var clearance in new[] { Clearance.BodyAndRotor, Clearance.Body, Clearance.None })
        {
            foreach (var angle in Angles)
            {
                foreach (var d in new[] { distance, distance + 4f })
                {
                    var direction = Quaternion.Euler(0f, angle, 0f) * forward;
                    if (!TryFindGround(player.position, player.position + direction * d, out var point, out var normal))
                        continue;

                    if (Vector3.Angle(normal, Vector3.up) > MaxSlope)
                        continue;

                    // Side-on to the player (left side, with the fuel tank, towards them), sat flat on the slope.
                    var facing = Quaternion.LookRotation(Vector3.Cross(direction, Vector3.up), Vector3.up);
                    var rotation = Quaternion.FromToRotation(Vector3.up, normal) * facing;

                    if (IsBlocked(point, rotation, clearance))
                        continue;

                    return Spawn(point + normal * 0.1f, rotation);
                }
            }
        }

        error = "No flat ground nearby. Move to a clearing and try again.";
        return null;
    }

    public static HelicopterController Spawn(Vector3 position, Quaternion rotation)
    {
        var parts = MinicopterModel.Build(position, rotation);
        var heli = parts.Root.AddComponent<HelicopterController>();
        heli.Init(parts);
        HelicopterRegistry.Add(heli);
        return heli;
    }

    /// <summary>
    /// Ground under <paramref name="probe"/>. Casts from just above the player's height first so that a roof or
    /// cave ceiling overhead isn't mistaken for the ground, then from higher up for steep uphill spots.
    /// </summary>
    private static bool TryFindGround(Vector3 playerPosition, Vector3 probe, out Vector3 point, out Vector3 normal)
    {
        foreach (var height in new[] { 3f, 25f })
        {
            var origin = new Vector3(probe.x, playerPosition.y + height, probe.z);
            if (!WorldCast.Down(origin, 60f, out var hit))
                continue;

            if (Mathf.Abs(hit.point.y - playerPosition.y) > MaxHeightDifference)
                continue;

            point = hit.point;
            normal = hit.normal;
            return true;
        }

        point = default;
        normal = Vector3.up;
        return false;
    }

    private enum Clearance { BodyAndRotor, Body, None }

    private static bool IsBlocked(Vector3 ground, Quaternion rotation, Clearance clearance)
    {
        if (clearance == Clearance.None)
            return false;

        // Cockpit, engine and tail boom, kept clear of the ground itself.
        var body = ground + rotation * new Vector3(0f, 1.3f, -1.4f);
        if (Physics.CheckBox(body, new Vector3(0.9f, 0.65f, 2.6f), rotation, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            return true;

        if (clearance == Clearance.Body)
            return false;

        var rotor = ground + rotation * new Vector3(0f, 2.45f, -0.75f);
        var half = MinicopterModel.RotorRadius - 0.2f;
        return Physics.CheckBox(rotor, new Vector3(half, 0.3f, half), rotation, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
    }
}
