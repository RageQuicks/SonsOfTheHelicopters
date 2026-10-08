using RedLoader;
using SonsSdk;
using TheForest.Utils;
using UnityEngine;
using GameState = SonsSdk.GameState;

namespace RustHelicopter;

/// <summary>
/// Puts the local player in a helicopter's pilot seat and takes them out again.
///
/// While flying, the player's rigidbody is kinematic and the player is carried along with the helicopter every
/// frame (the same approach RedLoader's own free camera uses), the game's mouse look is switched off so the mouse
/// can fly the helicopter, and the main camera is placed at the pilot's eyes or behind the helicopter.
/// </summary>
public static class Pilot
{
    /// <summary>If something else moves the player this far in a frame (death, teleport, cutscene), let go.</summary>
    private const float ExternalMoveThreshold = 10f;

    private static readonly List<(Collider player, Collider heli)> IgnoredPairs = new();

    private static Vector3 _camLocalPosition;
    private static Quaternion _camLocalRotation;
    private static float _eyeHeight;
    private static bool _bodyHidden;
    private static Vector3 _lastSeatedPosition;
    private static bool _seatedOnce;
    private static float _lookYaw;
    private static float _lookPitch;

    public static HelicopterController Current { get; private set; }

    public static bool ThirdPerson { get; set; }

    public static bool IsFlying(HelicopterController heli) => Current is not null && ReferenceEquals(Current, heli);

    public static bool Enter(HelicopterController heli)
    {
        if (Current is not null || !heli || heli.IsWrecked)
            return false;

        var player = LocalPlayer.Transform;
        var cam = LocalPlayer.MainCamTr;
        var body = LocalPlayer.Rigidbody;
        if (!player || !cam || !body)
            return false;

        // How high the camera sits above the player's feet, so the body can be placed with its eyes at the seat.
        _eyeHeight = Mathf.Clamp(Vector3.Dot(cam.position - player.position, player.up), 1f, 2.2f);
        _camLocalPosition = cam.localPosition;
        _camLocalRotation = cam.localRotation;

        body.velocity = Vector3.zero;
        body.isKinematic = true;

        SetMouseLook(false);
        SetAnimatorControl(false);
        _bodyHidden = Config.HidePlayerBody.Value;
        if (_bodyHidden)
            SetBodyVisible(false);

        Current = heli;
        IgnoreCollisions(heli, true);

        ThirdPerson = Config.StartInThirdPerson.Value;
        _lookYaw = 0f;
        _lookPitch = 0f;
        _seatedOnce = false;

        heli.SetPiloted(true);
        UpdateSeat(heli);

        RustHelicopterMod.Notify($"W/S throttle, A/D turn, mouse to tilt. {Config.Interact} to get out, {Config.CameraToggle} for camera.");
        return true;
    }

    /// <summary>Takes the player out of the helicopter.</summary>
    /// <param name="placeBeside">Put the player next to the helicopter (false when the game already moved them).</param>
    public static void Release(bool placeBeside)
    {
        var heli = Current;
        if (heli is null)
            return;

        Current = null;

        try
        {
            if (heli)
                heli.SetPiloted(false);

            IgnoreCollisions(heli, false);

            var player = LocalPlayer.Transform;
            var cam = LocalPlayer.MainCamTr;
            var body = LocalPlayer.Rigidbody;

            if (cam)
            {
                cam.localPosition = _camLocalPosition;
                cam.localRotation = _camLocalRotation;
            }

            SetMouseLook(true);
            SetAnimatorControl(true);
            if (_bodyHidden)
                SetBodyVisible(true);
            _bodyHidden = false;

            if (placeBeside && heli && player)
            {
                player.position = heli.GetExitPosition();
                player.rotation = Quaternion.Euler(0f, heli.transform.eulerAngles.y, 0f);
            }

            if (body)
            {
                body.isKinematic = false;
                body.velocity = Vector3.zero;
            }
        }
        catch (Exception e)
        {
            RLog.Error($"[RustHelicopter] Getting out of the helicopter failed: {e}");
        }
    }

    /// <summary>Called from the helicopter's LateUpdate: carries the player along and places the camera.</summary>
    public static void UpdateSeat(HelicopterController heli)
    {
        var player = LocalPlayer.Transform;
        var cam = LocalPlayer.MainCamTr;
        if (!player || !cam)
        {
            Release(false);
            return;
        }

        if (_seatedOnce && (player.position - _lastSeatedPosition).sqrMagnitude > ExternalMoveThreshold * ExternalMoveThreshold)
        {
            RLog.Msg("[RustHelicopter] The player was moved by the game; leaving the helicopter.");
            Release(false);
            return;
        }

        var heliTransform = heli.transform;
        var eye = heli.EyePosition;

        // Keep the body upright (only turn it with the helicopter) and put its eyes at the pilot's eye point.
        player.position = eye - Vector3.up * _eyeHeight;
        player.rotation = Quaternion.Euler(0f, heliTransform.eulerAngles.y, 0f);
        _lastSeatedPosition = player.position;
        _seatedOnce = true;

        // Something in the game may switch these back on; keep the mouse for flying.
        SetMouseLook(false);

        UpdateFreeLook();

        if (ThirdPerson)
            PlaceChaseCamera(cam, heli);
        else
            cam.SetPositionAndRotation(eye, heliTransform.rotation * Quaternion.Euler(_lookPitch, _lookYaw, 0f));
    }

    private static void UpdateFreeLook()
    {
        if (Input.GetKey(Config.FreeLook) && GameState.IsPlayerControllable)
        {
            var sensitivity = Config.LookSensitivity.Value;
            _lookYaw = Mathf.Clamp(_lookYaw + Input.GetAxis("Mouse X") * sensitivity, -150f, 150f);
            _lookPitch = Mathf.Clamp(_lookPitch - Input.GetAxis("Mouse Y") * sensitivity, -70f, 80f);
            return;
        }

        // Let go of free look: ease back to looking ahead.
        var t = 1f - Mathf.Exp(-8f * Time.deltaTime);
        _lookYaw = Mathf.Lerp(_lookYaw, 0f, t);
        _lookPitch = Mathf.Lerp(_lookPitch, 0f, t);
    }

    private static void PlaceChaseCamera(Transform cam, HelicopterController heli)
    {
        var heliTransform = heli.transform;
        var target = heliTransform.position + Vector3.up * 1.6f;
        var orbit = Quaternion.Euler(15f + _lookPitch, heliTransform.eulerAngles.y + _lookYaw, 0f);
        var offset = orbit * Vector3.back * Config.ThirdPersonDistance.Value;

        // Pull in if a tree or the ground is between the helicopter and the camera.
        var distance = offset.magnitude;
        if (WorldCast.Ray(target, offset, distance, out var hit))
            distance = Mathf.Max(1.5f, Vector3.Distance(target, hit.point) - 0.3f);

        var position = target + offset.normalized * distance;
        cam.SetPositionAndRotation(position, Quaternion.LookRotation(target - position, Vector3.up));
    }

    private static void SetMouseLook(bool enabled)
    {
        var yaw = LocalPlayer.MainRotator;
        if (yaw && yaw.enabled != enabled)
            yaw.enabled = enabled;

        var pitch = LocalPlayer.CamRotator;
        if (pitch && pitch.enabled != enabled)
            pitch.enabled = enabled;
    }

    private static void SetAnimatorControl(bool enabled)
    {
        try
        {
            var animator = LocalPlayer.Animator;
            if (!animator)
                return;

            var control = animator.GetComponent<PlayerAnimatorControl>();
            if (control)
                control.enabled = enabled;
        }
        catch (Exception e)
        {
            RLog.Error($"[RustHelicopter] Could not toggle the player animator: {e.Message}");
        }
    }

    private static void SetBodyVisible(bool visible)
    {
        try
        {
            if (LocalPlayer.RaceSystem)
                LocalPlayer.RaceSystem.gameObject.SetActive(visible);
            if (LocalPlayer.ClothingSystem)
                LocalPlayer.ClothingSystem.gameObject.SetActive(visible);
            var armour = LocalPlayer.Stats?.ArmourSystem;
            if (armour)
                armour.gameObject.SetActive(visible);
        }
        catch (Exception e)
        {
            RLog.Error($"[RustHelicopter] Could not {(visible ? "show" : "hide")} the player body: {e.Message}");
        }
    }

    /// <summary>
    /// The player's collider sits inside the helicopter's while flying; without this the kinematic player would
    /// shove the helicopter around.
    /// </summary>
    private static void IgnoreCollisions(HelicopterController heli, bool ignore)
    {
        try
        {
            if (!ignore)
            {
                foreach (var (playerCollider, heliCollider) in IgnoredPairs)
                {
                    if (playerCollider && heliCollider)
                        Physics.IgnoreCollision(playerCollider, heliCollider, false);
                }

                IgnoredPairs.Clear();
                return;
            }

            var player = LocalPlayer.Transform;
            if (!player || !heli)
                return;

            var playerColliders = player.GetComponentsInChildren<Collider>(true);
            foreach (var heliCollider in heli.Colliders)
            {
                if (!heliCollider)
                    continue;

                foreach (var playerCollider in playerColliders)
                {
                    if (!playerCollider)
                        continue;

                    Physics.IgnoreCollision(playerCollider, heliCollider, true);
                    IgnoredPairs.Add((playerCollider, heliCollider));
                }
            }
        }
        catch (Exception e)
        {
            RLog.Error($"[RustHelicopter] Could not set up collisions between player and helicopter: {e.Message}");
        }
    }
}
