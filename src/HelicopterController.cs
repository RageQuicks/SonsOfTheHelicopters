using Il2CppInterop.Runtime.Attributes;
using SonsSdk;
using UnityEngine;

namespace RustHelicopter;

/// <summary>
/// Flight model, engine, fuel and damage for one minicopter. Flies like Rust's minicopter:
/// W/S throttle (climb/descend, hover with neither), A/D turn, mouse forward/back pitches, mouse left/right rolls.
/// </summary>
public class HelicopterController : MonoBehaviour
{
    public HelicopterController(IntPtr ptr) : base(ptr) { }

    private const float Mass = 500f;
    private const float LinearDrag = 0.25f;
    private const float AngularDrag = 2.5f;
    private const float SpoolUpSeconds = 3f;
    private const float SpoolDownSeconds = 9f;
    private const float SpawnSettleSeconds = 0.25f;

    private const float AttitudeGain = 3f;          // deg/s of rotation per degree of attitude error
    private const float MaxAttitudeRate = 90f;      // deg/s
    private const float RateResponse = 6f;          // how fast the rotation rate follows the target (1/s)
    private const float KeyboardTiltRate = 60f;     // deg/s of target tilt from the arrow keys
    private const float VerticalDrag = 0.5f;        // extra vertical damping while the rotor turns (1/s)
    private const float HoverHold = 1.5f;           // more vertical damping when the pilot holds neither W nor S (1/s)
    private const float GroundIdleCollective = 0.85f;
    private const float GroundedHeight = 0.6f;

    private const float MaxHealth = 100f;
    private const float MinDamageSpeed = 9f;        // m/s change of speed in one impact before it hurts (full-S landings are ~8.5)
    private const float DamagePerSpeed = 12f;       // health per m/s above that
    private const float WreckLifetime = 60f;

    private const float MainRotorDegPerSec = 1100f;
    private const float TailRotorDegPerSec = 2600f;

    private MinicopterParts _parts;
    private Rigidbody _rb;
    private bool _settled;
    private float _spawnTime;

    private float _fuel;
    private float _health;
    private float _rotor;           // 0..1
    private bool _engineOn;
    private bool _piloted;
    private bool _wrecked;
    private float _wreckTime;
    private float _nextEmptyTankNotice;

    private float _lastContactTime = -10f;
    private float _groundDistance = float.PositiveInfinity;

    // Pilot input.
    private float _throttle;        // -1..1
    private float _yaw;             // -1..1, positive turns right
    private float _targetPitch;     // degrees, positive is nose down
    private float _targetRoll;      // degrees, positive is banked right

    public float Fuel => _fuel;
    public float FuelCapacity => Config.FuelCapacity.Value;
    public float Health01 => _health / MaxHealth;
    public float RotorSpeed01 => _rotor;
    public float ThrottleInput => _throttle;
    public bool EngineOn => _engineOn;
    public bool IsWrecked => _wrecked;
    public float GroundDistance => _groundDistance;
    public Vector3 Velocity => _rb ? _rb.velocity : Vector3.zero;
    public Vector3 SeatPosition => transform.TransformPoint(MinicopterModel.SeatLocal);
    public Vector3 EyePosition => transform.TransformPoint(MinicopterModel.EyeLocal);

    [HideFromIl2Cpp]
    public IReadOnlyList<Collider> Colliders => _parts.Colliders;

    public string EngineStatus
    {
        get
        {
            if (_wrecked) return "WRECKED";
            if (_engineOn) return _rotor < 0.98f ? $"STARTING {_rotor * 100f:0}%" : "RUNNING";
            if (_fuel <= 0f) return "NO FUEL";
            return _rotor > 0.01f ? "SPOOLING DOWN" : "OFF";
        }
    }

    private bool IsGrounded => Time.time - _lastContactTime < 0.2f && _groundDistance < GroundedHeight;

    [HideFromIl2Cpp]
    public void Init(MinicopterParts parts)
    {
        _parts = parts;

        _rb = gameObject.AddComponent<Rigidbody>();
        _rb.mass = Mass;
        _rb.drag = LinearDrag;
        _rb.angularDrag = AngularDrag;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _rb.maxAngularVelocity = 7f;
        // Held still for a moment so the model's throwaway primitive colliders are gone before physics starts.
        _rb.isKinematic = true;

        _fuel = FuelCapacity;   // every helicopter spawns with a full tank
        _health = MaxHealth;
        _spawnTime = Time.time;
    }

    public void SetPiloted(bool piloted)
    {
        _piloted = piloted;
        _throttle = 0f;
        _yaw = 0f;
        _targetPitch = 0f;
        _targetRoll = 0f;

        // Like Rust: the engine shuts off when the pilot gets out.
        if (!piloted)
            StopEngine(null);
    }

    public void Refuel() => _fuel = FuelCapacity;

    /// <summary>A spot next to the cockpit to put the player when they get out.</summary>
    public Vector3 GetExitPosition()
    {
        var t = transform;
        var seat = SeatPosition;
        var offsets = new[] { -t.right * 1.9f, t.right * 1.9f, t.forward * 2.3f, -t.right * 1.9f - t.forward * 1.5f };

        foreach (var offset in offsets)
        {
            if (WorldCast.Down(seat + offset + Vector3.up * 1.2f, 4f, out var hit))
                return hit.point + Vector3.up * 0.05f;
        }

        // In the air: step out to the left and fall.
        return seat - t.right * 1.9f;
    }

    private void Update()
    {
        if (!_rb)
            return;

        var dt = Time.deltaTime;
        if (_piloted && !_wrecked)
            ReadPilotInput(dt);

        SpinRotors(dt);

        if (_wrecked && Time.time - _wreckTime > WreckLifetime)
            Destroy(gameObject);
    }

    private void FixedUpdate()
    {
        if (!_rb)
            return;

        if (!_settled)
        {
            if (Time.time - _spawnTime < SpawnSettleSeconds)
                return;

            _rb.isKinematic = false;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rb.centerOfMass = MinicopterModel.CenterOfMassLocal;
            _rb.WakeUp();
            _settled = true;
        }

        var dt = Time.fixedDeltaTime;
        UpdateEngine(dt);

        if (_rotor <= 0.001f && !_piloted)
        {
            _groundDistance = float.PositiveInfinity;
            return;
        }

        UpdateGroundDistance();

        if (_rotor > 0.001f)
        {
            ApplyLift();
            ApplyAttitude();
        }
    }

    private void LateUpdate()
    {
        if (Pilot.IsFlying(this))
            Pilot.UpdateSeat(this);
    }

    private void OnCollisionEnter(Collision collision)
    {
        _lastContactTime = Time.time;

        if (!_settled || _wrecked || !Config.CrashDamage.Value)
            return;

        // The change of speed this impact caused.
        var speed = collision.impulse.magnitude / _rb.mass;
        if (speed > MinDamageSpeed)
            Damage((speed - MinDamageSpeed) * DamagePerSpeed);
    }

    private void OnCollisionStay(Collision collision)
    {
        _lastContactTime = Time.time;
    }

    private void OnDestroy()
    {
        if (Pilot.IsFlying(this))
            Pilot.Release(false);

        HelicopterRegistry.Remove(this);
    }

    private void ReadPilotInput(float dt)
    {
        if (!GameState.IsPlayerControllable)
        {
            // Console, inventory or book open: let go of the controls.
            _throttle = 0f;
            _yaw = 0f;
            LevelOut(dt, true, true);
            return;
        }

        _throttle = Axis(KeyCode.W, KeyCode.S);
        _yaw = Axis(KeyCode.D, KeyCode.A);

        var pitchInput = Axis(KeyCode.UpArrow, KeyCode.DownArrow) * KeyboardTiltRate * dt;
        var rollInput = Axis(KeyCode.RightArrow, KeyCode.LeftArrow) * KeyboardTiltRate * dt;

        if (!Input.GetKey(Config.FreeLook))
        {
            var sensitivity = Config.MouseSensitivity.Value;
            var mouseY = Input.GetAxis("Mouse Y") * (Config.InvertMouseY.Value ? -1f : 1f);
            pitchInput += mouseY * sensitivity;   // mouse forward: nose down, fly forward
            rollInput += Input.GetAxis("Mouse X") * sensitivity;
        }

        var maxTilt = Config.MaxTilt.Value;
        _targetPitch = Mathf.Clamp(_targetPitch + pitchInput, -maxTilt, maxTilt);
        _targetRoll = Mathf.Clamp(_targetRoll + rollInput, -maxTilt, maxTilt);

        // Sitting on the ground with the rotor still slow: don't let tilt build up before take-off.
        if (_rotor < 0.3f)
        {
            _targetPitch = 0f;
            _targetRoll = 0f;
            return;
        }

        LevelOut(dt, pitchInput == 0f, rollInput == 0f);
    }

    private void LevelOut(float dt, bool pitch, bool roll)
    {
        var step = Config.AutoLevel.Value * dt;
        if (pitch)
            _targetPitch = Mathf.MoveTowards(_targetPitch, 0f, step);
        if (roll)
            _targetRoll = Mathf.MoveTowards(_targetRoll, 0f, step);
    }

    private static float Axis(KeyCode positive, KeyCode negative) =>
        (Input.GetKey(positive) ? 1f : 0f) - (Input.GetKey(negative) ? 1f : 0f);

    private void UpdateEngine(float dt)
    {
        var capacity = FuelCapacity;
        if (_fuel > capacity)
            _fuel = capacity;

        if (_engineOn)
        {
            if (!_piloted || _wrecked)
            {
                StopEngine(null);
            }
            else if (_fuel <= 0f)
            {
                _fuel = 0f;
                StopEngine("Out of fuel! The engine cut out.");
            }
            else
            {
                // 0 when idling or descending, 0.5 hovering, 1 at full climb.
                var load = IsGrounded && _throttle <= 0f ? 0f : 0.5f + 0.5f * _throttle;
                var perMinute = Mathf.Lerp(Config.FuelBurnIdle.Value, Config.FuelBurnFull.Value, load);
                _fuel = Mathf.Max(0f, _fuel - perMinute * dt / 60f);
            }
        }
        else if (_piloted && !_wrecked && _throttle > 0.1f)
        {
            // Like Rust: throttling up starts the engine.
            if (_fuel > 0f)
            {
                _engineOn = true;
                RustHelicopterMod.Notify("Engine started");
            }
            else if (Time.time >= _nextEmptyTankNotice)
            {
                _nextEmptyTankNotice = Time.time + 4f;
                RustHelicopterMod.Notify("The fuel tank is empty. Refuel with: helicopter refuel");
            }
        }

        var rate = _engineOn ? 1f / SpoolUpSeconds : 1f / SpoolDownSeconds;
        _rotor = Mathf.MoveTowards(_rotor, _engineOn ? 1f : 0f, rate * dt);
    }

    private void StopEngine(string message)
    {
        if (!_engineOn)
            return;

        _engineOn = false;
        if (message != null)
            RustHelicopterMod.Notify(message);
    }

    private void UpdateGroundDistance()
    {
        var origin = transform.position + Vector3.up * 0.5f;
        _groundDistance = WorldCast.Down(origin, 500f, out var hit)
            ? Mathf.Max(0f, origin.y - hit.point.y - 0.5f)
            : float.PositiveInfinity;
    }

    private void ApplyLift()
    {
        var hoverThrust = _rb.mass * Physics.gravity.magnitude;
        var lift = _rotor * _rotor;
        var grounded = IsGrounded;

        // Parked with no throttle: a little under hover so it stays on the ground until the pilot throttles up.
        var collective = grounded && _throttle <= 0f
            ? GroundIdleCollective
            : 1f + _throttle * Config.LiftPower.Value;

        // Tilting tips the thrust sideways; add enough to keep holding altitude, which is what turns tilt into speed.
        var upright = Mathf.Clamp(Vector3.Dot(transform.up, Vector3.up), 0.5f, 1f);
        _rb.AddForce(transform.up * (hoverThrust * lift * collective / upright), ForceMode.Force);

        var verticalDamping = VerticalDrag;
        if (_piloted && !grounded && Mathf.Abs(_throttle) < 0.01f)
            verticalDamping += HoverHold;
        _rb.AddForce(Vector3.up * (-_rb.velocity.y * verticalDamping * _rb.mass * lift), ForceMode.Force);
    }

    private void ApplyAttitude()
    {
        var t = transform;
        var pitch = -Mathf.Asin(Mathf.Clamp(t.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
        var roll = -Mathf.Asin(Mathf.Clamp(t.right.y, -1f, 1f)) * Mathf.Rad2Deg;

        var pitchRate = Mathf.Clamp((_targetPitch - pitch) * AttitudeGain, -MaxAttitudeRate, MaxAttitudeRate);
        var rollRate = Mathf.Clamp((_targetRoll - roll) * AttitudeGain, -MaxAttitudeRate, MaxAttitudeRate);
        var yawRate = _yaw * Config.YawSpeed.Value;

        // Unity rotations: +X tips the nose down, +Y turns right, +Z rolls left.
        var desired = new Vector3(pitchRate, yawRate, -rollRate) * Mathf.Deg2Rad;
        var current = t.InverseTransformDirection(_rb.angularVelocity);

        var authority = _rotor * _rotor;
        if (IsGrounded && _throttle <= 0f)
            authority *= 0.2f;     // don't fight the ground when parked on a slope

        _rb.AddRelativeTorque((desired - current) * (RateResponse * authority), ForceMode.Acceleration);
    }

    private void SpinRotors(float dt)
    {
        if (_rotor <= 0f || _parts == null)
            return;

        _parts.MainRotor.Rotate(0f, _rotor * MainRotorDegPerSec * dt, 0f, Space.Self);
        _parts.TailRotor.Rotate(_rotor * TailRotorDegPerSec * dt, 0f, 0f, Space.Self);
    }

    private void Damage(float amount)
    {
        _health = Mathf.Max(0f, _health - amount);
        if (_health <= 0f)
        {
            Wreck();
            return;
        }

        if (_piloted)
            RustHelicopterMod.Notify($"Hull damaged: {_health / MaxHealth * 100f:0}%");
    }

    private void Wreck()
    {
        _wrecked = true;
        _wreckTime = Time.time;
        StopEngine(null);

        // Scorch the paint.
        foreach (var material in _parts.Materials)
        {
            var c = material.GetColor("_BaseColor");
            material.SetColor("_BaseColor", new Color(c.r * 0.25f, c.g * 0.25f, c.b * 0.25f, c.a));
        }

        if (Pilot.IsFlying(this))
            Pilot.Release(true);

        RustHelicopterMod.Notify("The helicopter was wrecked!");
    }
}
