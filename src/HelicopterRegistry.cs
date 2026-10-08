using UnityEngine;

namespace RustHelicopter;

/// <summary>
/// Every helicopter spawned in this session.
/// </summary>
public static class HelicopterRegistry
{
    private static readonly List<HelicopterController> Helicopters = new();

    /// <summary>Living helicopters (destroyed ones are dropped first).</summary>
    public static IReadOnlyList<HelicopterController> All
    {
        get
        {
            Prune();
            return Helicopters;
        }
    }

    public static void Add(HelicopterController heli)
    {
        if (!Helicopters.Contains(heli))
            Helicopters.Add(heli);
    }

    public static void Remove(HelicopterController heli) => Helicopters.Remove(heli);

    public static void Prune() => Helicopters.RemoveAll(h => h is null || !h);

    /// <summary>The closest flyable helicopter whose seat is within <paramref name="maxDistance"/> of a point.</summary>
    public static HelicopterController FindUsable(Vector3 position, float maxDistance)
    {
        HelicopterController best = null;
        var bestDistance = maxDistance;

        foreach (var heli in All)
        {
            if (heli.IsWrecked)
                continue;

            var distance = Vector3.Distance(position, heli.SeatPosition);
            if (distance <= bestDistance)
            {
                best = heli;
                bestDistance = distance;
            }
        }

        return best;
    }

    /// <summary>The closest helicopter to a point, wrecked or not.</summary>
    public static HelicopterController FindNearest(Vector3 position)
    {
        HelicopterController best = null;
        var bestDistance = float.MaxValue;

        foreach (var heli in All)
        {
            var distance = Vector3.Distance(position, heli.transform.position);
            if (distance < bestDistance)
            {
                best = heli;
                bestDistance = distance;
            }
        }

        return best;
    }
}
