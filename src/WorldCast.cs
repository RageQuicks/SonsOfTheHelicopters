using TheForest.Utils;
using UnityEngine;

namespace RustHelicopter;

/// <summary>
/// Raycasts against the world that skip the local player and every spawned helicopter.
/// </summary>
public static class WorldCast
{
    private const int MaxSkips = 12;

    /// <summary>
    /// Casts a ray, stepping past ignored colliders. On a hit, use <c>hit.point</c> and <c>hit.normal</c>;
    /// <c>hit.distance</c> is measured from the last step, not from <paramref name="origin"/>.
    /// </summary>
    public static bool Ray(Vector3 origin, Vector3 direction, float maxDistance, out RaycastHit hit)
    {
        direction.Normalize();
        var travelled = 0f;

        for (var i = 0; i < MaxSkips && travelled < maxDistance; i++)
        {
            if (!Physics.Raycast(origin, direction, out hit, maxDistance - travelled,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return false;

            if (!IsIgnored(hit.collider))
                return true;

            // Step just past the ignored collider and keep going.
            var step = hit.distance + 0.05f;
            origin += direction * step;
            travelled += step;
        }

        hit = default;
        return false;
    }

    public static bool Down(Vector3 origin, float maxDistance, out RaycastHit hit) =>
        Ray(origin, Vector3.down, maxDistance, out hit);

    public static bool IsIgnored(Collider collider)
    {
        if (!collider)
            return true;

        var t = collider.transform;
        var player = LocalPlayer.Transform;
        if (player && t.IsChildOf(player))
            return true;

        foreach (var heli in HelicopterRegistry.All)
        {
            if (t.IsChildOf(heli.transform))
                return true;
        }

        return false;
    }
}
