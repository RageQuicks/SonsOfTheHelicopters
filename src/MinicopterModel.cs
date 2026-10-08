using SonsSdk;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RustHelicopter;

/// <summary>
/// The pieces of a built minicopter that the controller needs.
/// </summary>
public class MinicopterParts
{
    public GameObject Root;
    public Transform MainRotor;
    public Transform TailRotor;
    public readonly List<Collider> Colliders = new();
    public readonly List<Material> Materials = new();
}

/// <summary>
/// Builds a Rust-style minicopter out of Unity primitives: an open scrap-metal cockpit with a roll cage, skids,
/// an engine and fuel tank behind the seat, a two-blade main rotor and a tail rotor.
/// Local axes: +Z forward, +Y up, origin on the ground between the skids.
/// </summary>
public static class MinicopterModel
{
    /// <summary>The pilot's eye point in the cockpit.</summary>
    public static readonly Vector3 EyeLocal = new(0f, 1.72f, -0.18f);

    /// <summary>The pilot seat; the enter prompt measures the player's distance to this point.</summary>
    public static readonly Vector3 SeatLocal = new(0f, 0.9f, -0.25f);

    /// <summary>Approximate centre of mass, below the rotor mast so the helicopter hangs level.</summary>
    public static readonly Vector3 CenterOfMassLocal = new(0f, 0.95f, -0.6f);

    public const float RotorRadius = 3.6f;

    private static readonly Color Olive = new(0.30f, 0.33f, 0.20f);
    private static readonly Color Rust = new(0.50f, 0.24f, 0.10f);
    private static readonly Color Steel = new(0.24f, 0.24f, 0.25f);
    private static readonly Color DarkSteel = new(0.09f, 0.09f, 0.10f);
    private static readonly Color Leather = new(0.30f, 0.17f, 0.09f);
    private static readonly Color FuelRed = new(0.55f, 0.08f, 0.06f);
    private static readonly Color Hazard = new(0.78f, 0.60f, 0.10f);

    public static MinicopterParts Build(Vector3 position, Quaternion rotation)
    {
        var parts = new MinicopterParts { Root = new GameObject("RustMinicopter") };
        var root = parts.Root.transform;
        root.SetPositionAndRotation(position, rotation);

        var olive = MakeMaterial(parts, Olive, 0.1f, 0.25f);
        var rust = MakeMaterial(parts, Rust, 0.2f, 0.2f);
        var steel = MakeMaterial(parts, Steel, 0.7f, 0.45f);
        var dark = MakeMaterial(parts, DarkSteel, 0.6f, 0.35f);
        var leather = MakeMaterial(parts, Leather, 0f, 0.3f);
        var red = MakeMaterial(parts, FuelRed, 0.3f, 0.5f);
        var hazard = MakeMaterial(parts, Hazard, 0.2f, 0.35f);

        // Skids (colliders: the helicopter rests and slides on these).
        foreach (var x in new[] { -0.75f, 0.75f })
        {
            Tube(parts, "Skid", root, new(x, 0.06f, -1.15f), new(x, 0.06f, 1.25f), 0.06f, dark, true);
            Tube(parts, "SkidTip", root, new(x, 0.06f, 1.25f), new(x, 0.22f, 1.45f), 0.06f, dark, false);
            Tube(parts, "StrutFront", root, new(x, 0.06f, 0.65f), new(x * 0.67f, 0.48f, 0.55f), 0.04f, dark, false);
            Tube(parts, "StrutRear", root, new(x, 0.06f, -0.65f), new(x * 0.67f, 0.48f, -0.6f), 0.04f, dark, false);
        }

        // Cockpit tub and nose.
        Box(parts, "Tub", root, new(0f, 0.62f, 0.05f), new(1.1f, 0.32f, 1.9f), Quaternion.identity, olive, true);
        Box(parts, "Nose", root, new(0f, 0.88f, 0.85f), new(1.0f, 0.36f, 0.45f), Quaternion.identity, olive, true);
        Box(parts, "NoseStripe", root, new(0f, 1.065f, 0.85f), new(1.02f, 0.02f, 0.47f), Quaternion.identity, rust, false);
        Box(parts, "Dash", root, new(0f, 1.12f, 0.78f), new(0.8f, 0.18f, 0.08f), Quaternion.Euler(-20f, 0f, 0f), dark, false);
        Tube(parts, "Stick", root, new(0f, 0.78f, 0.35f), new(0f, 1.15f, 0.28f), 0.025f, dark, false);
        Ball(parts, "StickKnob", root, new(0f, 1.17f, 0.27f), 0.07f, red);

        // Seat.
        Box(parts, "Seat", root, new(0f, 0.86f, -0.25f), new(0.55f, 0.14f, 0.5f), Quaternion.identity, leather, false);
        Box(parts, "SeatBack", root, new(0f, 1.18f, -0.5f), new(0.55f, 0.55f, 0.1f), Quaternion.Euler(-10f, 0f, 0f), leather, false);

        // Roll cage.
        foreach (var x in new[] { -1f, 1f })
        {
            Tube(parts, "CageRear", root, new(0.52f * x, 0.78f, -0.62f), new(0.48f * x, 1.95f, -0.62f), 0.035f, steel, false);
            Tube(parts, "CagePillar", root, new(0.5f * x, 1.02f, 0.95f), new(0.42f * x, 1.85f, 0.3f), 0.035f, steel, false);
            Tube(parts, "CageTop", root, new(0.42f * x, 1.85f, 0.3f), new(0.48f * x, 1.95f, -0.62f), 0.035f, steel, false);
        }
        Tube(parts, "CageRearBar", root, new(-0.48f, 1.95f, -0.62f), new(0.48f, 1.95f, -0.62f), 0.035f, steel, false);
        Tube(parts, "CageFrontBar", root, new(-0.5f, 1.06f, 0.95f), new(0.5f, 1.06f, 0.95f), 0.035f, steel, false);

        // Engine, exhaust and fuel tank behind the seat.
        Box(parts, "Engine", root, new(0f, 1.25f, -1.05f), new(0.75f, 0.75f, 0.75f), Quaternion.identity, steel, true);
        Box(parts, "EngineCover", root, new(0f, 1.66f, -1.05f), new(0.6f, 0.08f, 0.6f), Quaternion.identity, rust, false);
        Tube(parts, "Exhaust", root, new(0.3f, 1.1f, -1.45f), new(0.3f, 0.9f, -1.75f), 0.05f, dark, false);
        Tube(parts, "FuelTank", root, new(-0.62f, 0.92f, -0.55f), new(-0.62f, 0.92f, -1.25f), 0.17f, red, false);
        Tube(parts, "FuelCap", root, new(-0.62f, 1.08f, -0.9f), new(-0.62f, 1.13f, -0.9f), 0.045f, dark, false);

        // Rotor mast.
        Tube(parts, "Mast", root, new(0f, 1.62f, -0.75f), new(0f, 2.42f, -0.75f), 0.06f, dark, false);
        Tube(parts, "Swashplate", root, new(0f, 2.22f, -0.75f), new(0f, 2.28f, -0.75f), 0.14f, steel, false);

        // Main rotor: two blades, spun around local Y by the controller.
        parts.MainRotor = Pivot("MainRotor", root, new(0f, 2.45f, -0.75f));
        Ball(parts, "Hub", parts.MainRotor, Vector3.zero, 0.24f, steel);
        Box(parts, "Blades", parts.MainRotor, Vector3.zero, new(RotorRadius * 2f, 0.035f, 0.26f), Quaternion.identity, dark, false);
        Box(parts, "BladeTipL", parts.MainRotor, new(-RotorRadius + 0.13f, 0f, 0f), new(0.26f, 0.04f, 0.27f), Quaternion.identity, hazard, false);
        Box(parts, "BladeTipR", parts.MainRotor, new(RotorRadius - 0.13f, 0f, 0f), new(0.26f, 0.04f, 0.27f), Quaternion.identity, hazard, false);

        // Tail boom (collider), fins.
        Tube(parts, "TailBoom", root, new(0f, 1.22f, -1.4f), new(0f, 1.42f, -4.25f), 0.085f, olive, true);
        Tube(parts, "BoomBrace", root, new(0f, 0.95f, -1.35f), new(0f, 1.28f, -2.6f), 0.03f, steel, false);
        Box(parts, "TailFin", root, new(0f, 1.78f, -4.18f), new(0.05f, 0.75f, 0.5f), Quaternion.Euler(-12f, 0f, 0f), rust, false);
        Box(parts, "TailFinStripe", root, new(0f, 2.08f, -4.24f), new(0.06f, 0.12f, 0.45f), Quaternion.Euler(-12f, 0f, 0f), hazard, false);
        Box(parts, "Stabilizer", root, new(0f, 1.42f, -3.85f), new(1.0f, 0.04f, 0.32f), Quaternion.identity, olive, false);

        // Tail rotor: spun around local X by the controller.
        parts.TailRotor = Pivot("TailRotor", root, new(0.14f, 1.55f, -4.25f));
        Ball(parts, "TailHub", parts.TailRotor, Vector3.zero, 0.12f, steel);
        Box(parts, "TailBlades", parts.TailRotor, Vector3.zero, new(0.03f, 1.15f, 0.1f), Quaternion.identity, dark, false);

        return parts;
    }

    private static Material MakeMaterial(MinicopterParts parts, Color color, float metallic, float smoothness)
    {
        var material = DebugTools.GetHdrpMaterial(color);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", smoothness);
        parts.Materials.Add(material);
        return material;
    }

    private static Transform Pivot(string name, Transform parent, Vector3 localPosition)
    {
        var pivot = new GameObject(name).transform;
        pivot.SetParent(parent, false);
        pivot.localPosition = localPosition;
        return pivot;
    }

    private static void Box(MinicopterParts parts, string name, Transform parent, Vector3 center, Vector3 size,
        Quaternion localRotation, Material material, bool collider)
    {
        var t = Primitive(parts, PrimitiveType.Cube, name, parent, material, collider);
        t.localPosition = center;
        t.localRotation = localRotation;
        t.localScale = size;
    }

    private static void Ball(MinicopterParts parts, string name, Transform parent, Vector3 center, float diameter, Material material)
    {
        var t = Primitive(parts, PrimitiveType.Sphere, name, parent, material, false);
        t.localPosition = center;
        t.localScale = Vector3.one * diameter;
    }

    /// <summary>A cylinder from local point a to local point b.</summary>
    private static void Tube(MinicopterParts parts, string name, Transform parent, Vector3 a, Vector3 b, float radius,
        Material material, bool collider)
    {
        var t = Primitive(parts, PrimitiveType.Cylinder, name, parent, material, collider);
        var axis = b - a;
        t.localPosition = (a + b) * 0.5f;
        t.localRotation = Quaternion.FromToRotation(Vector3.up, axis);
        // Unity's cylinder is 2 units tall and 1 unit wide.
        t.localScale = new Vector3(radius * 2f, axis.magnitude * 0.5f, radius * 2f);
    }

    private static Transform Primitive(MinicopterParts parts, PrimitiveType type, string name, Transform parent,
        Material material, bool keepCollider)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.GetComponent<MeshRenderer>().sharedMaterial = material;

        var collider = go.GetComponent<Collider>();
        if (collider)
        {
            if (keepCollider)
                parts.Colliders.Add(collider);
            else
                Object.Destroy(collider);
        }

        return go.transform;
    }
}
