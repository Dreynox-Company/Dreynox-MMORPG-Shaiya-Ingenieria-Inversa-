using System;
using UnityEngine;

namespace Dreynox.Mmorpg.Vfx
{
    [Serializable]
    public struct LegacyEftColorKey
    {
        public Color color;
        public float time;
    }

    [Serializable]
    public struct LegacyEftFloatKey
    {
        public float value;
        public float time;
    }

    [Serializable]
    public struct LegacyEftScaleKey
    {
        public float min;
        public float max;
        public float time;
    }

    [RequireComponent(typeof(ParticleSystem))]
    [RequireComponent(typeof(ParticleSystemRenderer))]
    public sealed class LegacyEftParticleEmitter : MonoBehaviour
    {
        private const int MaximumParticles = 200;

        [SerializeField] private ParticleSystem particleSystem;
        [SerializeField] private ParticleSystemRenderer particleRenderer;
        [SerializeField] private LegacyVertexEffectClip meshClip;

        [Header("Emitter")]
        [SerializeField] private bool authoredLoop;
        [SerializeField] private bool velocityRandomX;
        [SerializeField] private bool velocityRandomY;
        [SerializeField] private bool velocityRandomZ;
        [SerializeField] private int velocityMode;
        [SerializeField] private float emitRateMin;
        [SerializeField] private float emitRateMax;
        [SerializeField] private float lifeMin;
        [SerializeField] private float lifeMax;
        [SerializeField] private float emitterDuration;
        [SerializeField] private float swirlSpeed;
        [SerializeField] private Vector3 emitPositionSpread;
        [SerializeField] private Vector3 acceleration;
        [SerializeField] private Vector3 emitOrigin;
        [SerializeField] private Vector3 velocityMin;
        [SerializeField] private Vector3 velocityMax;
        [SerializeField] private bool gravityEnabled;
        [SerializeField] private bool attractEnabled;
        [SerializeField] private Vector3 attractPoint;
        [SerializeField] private float attractStrength;
        [SerializeField] private bool angularVelocityRandom;
        [SerializeField] private bool rotationEnabled;
        [SerializeField] private float angularVelocity;
        [SerializeField] private int rotationAxis;
        [SerializeField] private int initialRotationAxis;
        [SerializeField] private int initialRotationMinDegrees;
        [SerializeField] private int initialRotationMaxDegrees;
        [SerializeField] private bool motionPathEnabled;

        [Header("Lifetime curves")]
        [SerializeField] private LegacyEftColorKey[] colorKeys =
            Array.Empty<LegacyEftColorKey>();
        [SerializeField] private LegacyEftFloatKey[] velocityScaleKeys =
            Array.Empty<LegacyEftFloatKey>();
        [SerializeField] private LegacyEftScaleKey[] scaleKeys =
            Array.Empty<LegacyEftScaleKey>();

        private ParticleState[] _states =
            Array.Empty<ParticleState>();
        private ParticleSystem.Particle[] _particles =
            Array.Empty<ParticleSystem.Particle>();

        private Mesh _animatedMesh;
        private Vector3[] _animatedPositions;
        private Vector2[] _animatedUvs;

        private bool _playing;
        private bool _effectiveLoop;
        private bool _oneShotEmitted;
        private float _elapsed;
        private float _remainingDuration;
        private float _emitAccumulator;
        private float _seedBase = 1f;
        private uint _spawnCounter;

        public bool IsPlaying => _playing;

        public float EstimatedDuration
        {
            get
            {
                float particleLife =
                    Mathf.Max(
                        0f,
                        lifeMax);

                if (authoredLoop)
                    return float.PositiveInfinity;

                return Mathf.Max(
                    0.05f,
                    Mathf.Max(
                        emitterDuration,
                        0f) +
                    particleLife);
            }
        }

        public float EstimatedOneShotDuration =>
            Mathf.Max(
                0.05f,
                Mathf.Max(
                    emitterDuration,
                    0f) +
                Mathf.Max(
                    0f,
                    lifeMax));

        public void Configure(
            bool loop,
            bool randomX,
            bool randomY,
            bool randomZ,
            int mode,
            float rateMin,
            float rateMax,
            float particleLifeMin,
            float particleLifeMax,
            float duration,
            float swirl,
            Vector3 spread,
            Vector3 force,
            Vector3 origin,
            Vector3 minimumVelocity,
            Vector3 maximumVelocity,
            bool useGravity,
            bool useAttract,
            Vector3 attractionPoint,
            float attractionStrength,
            bool randomAngularVelocity,
            bool rotate,
            float spinVelocity,
            int spinAxis,
            int initialAxis,
            int initialMinDegrees,
            int initialMaxDegrees,
            bool useMotionPath,
            LegacyVertexEffectClip pathOrMeshClip,
            LegacyEftColorKey[] colors,
            LegacyEftFloatKey[] velocityScale,
            LegacyEftScaleKey[] scale)
        {
            authoredLoop = loop;
            velocityRandomX = randomX;
            velocityRandomY = randomY;
            velocityRandomZ = randomZ;
            velocityMode = mode;

            emitRateMin =
                Mathf.Max(0f, rateMin);
            emitRateMax =
                Mathf.Max(0f, rateMax);

            lifeMin =
                Mathf.Max(0f, particleLifeMin);
            lifeMax =
                Mathf.Max(lifeMin, particleLifeMax);

            emitterDuration =
                Mathf.Max(0f, duration);

            swirlSpeed = swirl;
            emitPositionSpread = spread;
            acceleration = force;
            emitOrigin = origin;
            velocityMin = minimumVelocity;
            velocityMax = maximumVelocity;
            gravityEnabled = useGravity;
            attractEnabled = useAttract;
            attractPoint = attractionPoint;
            attractStrength = attractionStrength;
            angularVelocityRandom =
                randomAngularVelocity;
            rotationEnabled = rotate;
            angularVelocity = spinVelocity;
            rotationAxis = spinAxis;
            initialRotationAxis = initialAxis;
            initialRotationMinDegrees =
                initialMinDegrees;
            initialRotationMaxDegrees =
                initialMaxDegrees;
            motionPathEnabled = useMotionPath;
            meshClip = pathOrMeshClip;

            colorKeys =
                colors ?? Array.Empty<LegacyEftColorKey>();

            velocityScaleKeys =
                velocityScale ??
                Array.Empty<LegacyEftFloatKey>();

            scaleKeys =
                scale ?? Array.Empty<LegacyEftScaleKey>();

            Prepare();
        }

        private void Awake()
        {
            Prepare();
        }

        private void OnEnable()
        {
            if (particleSystem != null)
                particleSystem.Stop(
                    true,
                    ParticleSystemStopBehavior
                        .StopEmittingAndClear);
        }

        private void OnDestroy()
        {
            if (_animatedMesh != null)
                Destroy(_animatedMesh);
        }

        public void Play(
            bool forceOneShot)
        {
            Prepare();

            _effectiveLoop =
                authoredLoop &&
                !forceOneShot;

            _playing = true;
            _oneShotEmitted = false;
            _elapsed = 0f;
            _remainingDuration =
                emitterDuration;
            _emitAccumulator = 0f;
            _spawnCounter = 0;

            for (int i = 0;
                 i < _states.Length;
                 i++)
            {
                _states[i] = default;
            }

            particleSystem.Clear(true);
            particleSystem.Play(true);

            StepEmitter(0f);
        }

        public void Stop(
            bool clearParticles)
        {
            _playing = false;

            if (particleSystem == null)
                return;

            particleSystem.Stop(
                true,
                clearParticles
                    ? ParticleSystemStopBehavior
                        .StopEmittingAndClear
                    : ParticleSystemStopBehavior
                        .StopEmitting);
        }

        private void Update()
        {
            if (!_playing ||
                particleSystem == null)
                return;

            float dt =
                Mathf.Clamp(
                    Time.deltaTime,
                    0f,
                    0.1f);

            StepEmitter(dt);
            SyncParticleSystem();

            if (!_effectiveLoop &&
                !CanEmitMore() &&
                ActiveStateCount() == 0)
            {
                _playing = false;
            }
        }

        private void Prepare()
        {
            if (particleSystem == null)
                particleSystem =
                    GetComponent<ParticleSystem>();

            if (particleRenderer == null)
                particleRenderer =
                    GetComponent<
                        ParticleSystemRenderer>();

            ParticleSystem.MainModule main =
                particleSystem.main;

            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace =
                ParticleSystemSimulationSpace.Local;
            main.maxParticles =
                ResolveCapacity();
            main.startSpeed = 0f;
            main.startLifetime =
                Mathf.Max(
                    0.05f,
                    lifeMax > 0f
                        ? lifeMax
                        : 1f);

            ParticleSystem.EmissionModule emission =
                particleSystem.emission;

            emission.enabled = false;

            int capacity =
                ResolveCapacity();

            if (_states == null ||
                _states.Length != capacity)
            {
                _states =
                    new ParticleState[
                        capacity];

                _particles =
                    new ParticleSystem.Particle[
                        capacity];
            }

            ConfigureAnimatedMesh();
        }

        private void ConfigureAnimatedMesh()
        {
            if (particleRenderer == null ||
                meshClip == null ||
                meshClip.BaseMesh == null ||
                motionPathEnabled)
                return;

            particleRenderer.renderMode =
                ParticleSystemRenderMode.Mesh;

            if (!Application.isPlaying)
            {
                particleRenderer.mesh =
                    meshClip.BaseMesh;
                return;
            }

            if (_animatedMesh != null)
                Destroy(_animatedMesh);

            _animatedMesh =
                Instantiate(
                    meshClip.BaseMesh);

            _animatedMesh.name =
                meshClip.BaseMesh.name +
                "_ParticleRuntime";

            _animatedMesh.MarkDynamic();

            _animatedPositions =
                new Vector3[
                    _animatedMesh.vertexCount];

            _animatedUvs =
                new Vector2[
                    _animatedMesh.vertexCount];

            particleRenderer.mesh =
                _animatedMesh;
        }

        private void StepEmitter(float dt)
        {
            _elapsed += dt;

            for (int i = 0;
                 i < _states.Length;
                 i++)
            {
                if (!_states[i].active)
                    continue;

                ParticleState state =
                    _states[i];

                state.age += dt;

                if (state.age >
                    state.life)
                {
                    state.active = false;
                    _states[i] = state;
                    continue;
                }

                if (velocityRandomX)
                {
                    state.velocity.x =
                        RandomRange(
                            velocityMin.x,
                            velocityMax.x,
                            state.seed +
                            state.age *
                            997.3f);
                }

                if (velocityRandomY)
                {
                    state.velocity.y =
                        RandomRange(
                            velocityMin.y,
                            velocityMax.y,
                            state.seed +
                            state.age *
                            997.3f +
                            31.7f);
                }

                if (velocityRandomZ)
                {
                    state.velocity.z =
                        RandomRange(
                            velocityMin.z,
                            velocityMax.z,
                            state.seed +
                            state.age *
                            997.3f +
                            63.4f);
                }

                ApplyVelocityMode(
                    ref state,
                    dt);

                float velocityScale =
                    EvaluateVelocityScale(
                        state.age);

                if (Mathf.Abs(
                        velocityScale) >
                    0.0001f)
                {
                    state.velocity +=
                        state.velocity *
                        velocityScale *
                        dt;
                }

                if (attractEnabled)
                {
                    Vector3 delta =
                        attractPoint -
                        state.position;

                    if (delta.sqrMagnitude >
                        0.000001f)
                    {
                        state.velocity +=
                            delta.normalized *
                            attractStrength *
                            dt;
                    }
                }

                if (gravityEnabled)
                {
                    state.velocity +=
                        acceleration *
                        dt;
                }

                if (rotationEnabled)
                {
                    state.rotation +=
                        state.angularVelocity *
                        dt;
                }

                _states[i] = state;
            }

            EmitForStep(dt);
            AnimateSharedMesh();
        }

        private void EmitForStep(float dt)
        {
            if (!EmitsParticles())
            {
                if (!_oneShotEmitted)
                {
                    SpawnParticle(
                        SampleParticleLife(
                            _seedBase),
                        _elapsed);

                    _oneShotEmitted = true;
                }

                return;
            }

            float previousDuration =
                _remainingDuration;

            _remainingDuration -= dt;

            int count = 0;

            if (IsOneShotEmitter())
            {
                bool crossedDuration =
                    previousDuration > 0f &&
                    _remainingDuration <= 0f;

                if (_effectiveLoop ||
                    (!_oneShotEmitted &&
                     (crossedDuration ||
                      emitterDuration <= 0f)))
                {
                    count = 1;
                    _oneShotEmitted = true;
                }
            }
            else
            {
                if (!_effectiveLoop &&
                    previousDuration <= 0f)
                    return;

                float rate =
                    RandomRange(
                        emitRateMin,
                        emitRateMax,
                        _seedBase +
                        _elapsed *
                        97.13f);

                float produced =
                    Mathf.Max(
                        0f,
                        rate) *
                    dt +
                    _emitAccumulator;

                count =
                    Mathf.FloorToInt(
                        produced);

                _emitAccumulator =
                    produced -
                    count;

                if (!_effectiveLoop &&
                    _remainingDuration <= 0f)
                {
                    count = 0;
                }

                count =
                    Mathf.Min(
                        count,
                        MaximumParticles);
            }

            for (int i = 0;
                 i < count;
                 i++)
            {
                SpawnParticle(
                    SampleParticleLife(
                        _seedBase +
                        _spawnCounter *
                        37.17f),
                    _elapsed);
            }
        }

        private void SpawnParticle(
            float life,
            float spawnSeconds)
        {
            int slot =
                FindFreeState();

            if (slot < 0)
                return;

            float seed =
                _seedBase +
                _spawnCounter *
                37.17f +
                slot *
                1009f;

            _spawnCounter++;

            ParticleState state =
                new ParticleState
                {
                    active = true,
                    age = 0f,
                    life =
                        Mathf.Max(
                            0.05f,
                            float.IsInfinity(life)
                                ? 86400f
                                : life),
                    seed = seed
                };

            state.position =
                emitOrigin +
                new Vector3(
                    SignedRandom(
                        seed) *
                    emitPositionSpread.x,
                    SignedRandom(
                        seed + 19.1f) *
                    emitPositionSpread.y,
                    SignedRandom(
                        seed + 47.7f) *
                    emitPositionSpread.z);

            if (motionPathEnabled &&
                meshClip != null &&
                meshClip.BaseMesh != null &&
                meshClip.BaseMesh.vertexCount > 0)
            {
                state.position +=
                    meshClip.SampleVertexAtTick(
                        meshClip.BaseMesh
                            .vertexCount -
                        1,
                        spawnSeconds *
                        meshClip.FramesPerSecond);
            }

            state.velocity =
                RandomVelocity(seed);

            state.angularVelocity =
                angularVelocityRandom
                    ? RandomRange(
                        Mathf.Min(
                            0f,
                            angularVelocity),
                        Mathf.Max(
                            0f,
                            angularVelocity),
                        seed +
                        131.1f)
                    : angularVelocity;

            state.initialRotation =
                SampleInitialRotation(
                    seed);

            _states[slot] = state;

            ParticleSystem.EmitParams emit =
                new ParticleSystem.EmitParams
                {
                    position =
                        RenderPosition(
                            state),
                    velocity =
                        Vector3.zero,
                    startLifetime =
                        state.life,
                    startSize =
                        Mathf.Max(
                            0.001f,
                            EvaluateScale(
                                0f,
                                seed)),
                    startColor =
                        EvaluateColor(0f),
                    randomSeed =
                        (uint)(slot + 1)
                };

            emit.rotation3D =
                RotationVector(
                    state);

            particleSystem.Emit(
                emit,
                1);
        }

        private void SyncParticleSystem()
        {
            int count =
                particleSystem.GetParticles(
                    _particles);

            for (int i = 0;
                 i < count;
                 i++)
            {
                ParticleSystem.Particle particle =
                    _particles[i];

                int slot =
                    (int)particle.randomSeed -
                    1;

                if (slot < 0 ||
                    slot >= _states.Length ||
                    !_states[slot].active)
                {
                    particle.remainingLifetime = 0f;
                    _particles[i] = particle;
                    continue;
                }

                ParticleState state =
                    _states[slot];

                particle.position =
                    RenderPosition(state);

                particle.velocity =
                    Vector3.zero;

                particle.remainingLifetime =
                    Mathf.Max(
                        0.001f,
                        state.life -
                        state.age);

                particle.startLifetime =
                    state.life;

                particle.startSize =
                    Mathf.Max(
                        0.001f,
                        EvaluateScale(
                            state.age,
                            state.seed));

                particle.startColor =
                    EvaluateColor(
                        state.age);

                particle.rotation3D =
                    RotationVector(state);

                _particles[i] =
                    particle;
            }

            particleSystem.SetParticles(
                _particles,
                count);
        }

        private void AnimateSharedMesh()
        {
            if (_animatedMesh == null ||
                meshClip == null ||
                _animatedPositions == null ||
                _animatedUvs == null)
                return;

            meshClip.EvaluateAtTick(
                _elapsed *
                meshClip.FramesPerSecond,
                _animatedPositions,
                _animatedUvs);

            _animatedMesh.vertices =
                _animatedPositions;

            _animatedMesh.uv =
                _animatedUvs;

            _animatedMesh.RecalculateBounds();
        }

        private Vector3 RenderPosition(
            ParticleState state)
        {
            return velocityMode == 0
                ? state.position
                : state.position +
                  state.velocity;
        }

        private void ApplyVelocityMode(
            ref ParticleState state,
            float dt)
        {
            float swirl =
                swirlSpeed *
                dt;

            switch (velocityMode)
            {
                case 1:
                    RotateXZ(
                        ref state.velocity,
                        swirl);

                    state.velocity +=
                        RandomVelocity(
                            state.seed +
                            state.age *
                            313.1f) *
                        dt;
                    break;

                case 2:
                    RotateXY(
                        ref state.velocity,
                        swirl);

                    state.velocity +=
                        RandomVelocity(
                            state.seed +
                            state.age *
                            313.1f) *
                        dt;
                    break;

                case 3:
                    RotateYZ(
                        ref state.velocity,
                        swirl);

                    state.velocity +=
                        RandomVelocity(
                            state.seed +
                            state.age *
                            313.1f) *
                        dt;
                    break;

                default:
                    state.position +=
                        state.velocity *
                        dt;
                    break;
            }
        }

        private Vector3 RandomVelocity(
            float seed)
        {
            return new Vector3(
                RandomRange(
                    velocityMin.x,
                    velocityMax.x,
                    seed + 3.1f),
                RandomRange(
                    velocityMin.y,
                    velocityMax.y,
                    seed + 7.7f),
                RandomRange(
                    velocityMin.z,
                    velocityMax.z,
                    seed + 11.3f));
        }

        private float SampleParticleLife(
            float seed)
        {
            float min =
                Mathf.Max(
                    0f,
                    lifeMin);

            float max =
                Mathf.Max(
                    min,
                    lifeMax);

            if (max <= 0f)
                return float.PositiveInfinity;

            return Mathf.Lerp(
                min,
                max,
                RandomValue(
                    seed +
                    71.3f));
        }

        private float SampleInitialRotation(
            float seed)
        {
            float min =
                initialRotationMinDegrees;

            float max =
                initialRotationMaxDegrees;

            float amount =
                RandomValue(
                    seed +
                    103.7f);

            float degrees;

            if (max >= min)
            {
                degrees =
                    Mathf.Lerp(
                        min,
                        max,
                        amount);
            }
            else if (amount < 0.5f)
            {
                degrees =
                    Mathf.Lerp(
                        0f,
                        max,
                        amount *
                        2f);
            }
            else
            {
                degrees =
                    Mathf.Lerp(
                        min,
                        360f,
                        (amount - 0.5f) *
                        2f);
            }

            return degrees *
                   Mathf.Deg2Rad;
        }

        private Color EvaluateColor(float time)
        {
            if (colorKeys == null ||
                colorKeys.Length == 0)
                return Color.white;

            if (time <=
                colorKeys[0].time)
                return colorKeys[0].color;

            for (int i = 1;
                 i < colorKeys.Length;
                 i++)
            {
                if (time <=
                    colorKeys[i].time)
                {
                    LegacyEftColorKey a =
                        colorKeys[i - 1];

                    LegacyEftColorKey b =
                        colorKeys[i];

                    float amount =
                        InverseRange(
                            a.time,
                            b.time,
                            time);

                    return Color.LerpUnclamped(
                        a.color,
                        b.color,
                        amount);
                }
            }

            return colorKeys[
                colorKeys.Length - 1]
                .color;
        }

        private float EvaluateScale(
            float time,
            float seed)
        {
            if (scaleKeys == null ||
                scaleKeys.Length == 0)
                return 0.3f;

            if (time <=
                scaleKeys[0].time)
            {
                return ScaleValue(
                    scaleKeys[0],
                    0,
                    seed);
            }

            for (int i = 1;
                 i < scaleKeys.Length;
                 i++)
            {
                if (time <=
                    scaleKeys[i].time)
                {
                    float amount =
                        InverseRange(
                            scaleKeys[i - 1].time,
                            scaleKeys[i].time,
                            time);

                    return Mathf.LerpUnclamped(
                        ScaleValue(
                            scaleKeys[i - 1],
                            i - 1,
                            seed),
                        ScaleValue(
                            scaleKeys[i],
                            i,
                            seed),
                        amount);
                }
            }

            return ScaleValue(
                scaleKeys[
                    scaleKeys.Length - 1],
                scaleKeys.Length - 1,
                seed);
        }

        private float EvaluateVelocityScale(
            float time)
        {
            if (velocityScaleKeys == null ||
                velocityScaleKeys.Length == 0)
                return 0f;

            if (time <=
                velocityScaleKeys[0].time)
                return velocityScaleKeys[0].value;

            for (int i = 1;
                 i < velocityScaleKeys.Length;
                 i++)
            {
                if (time <=
                    velocityScaleKeys[i].time)
                {
                    float amount =
                        InverseRange(
                            velocityScaleKeys[i - 1].time,
                            velocityScaleKeys[i].time,
                            time);

                    return Mathf.LerpUnclamped(
                        velocityScaleKeys[i - 1].value,
                        velocityScaleKeys[i].value,
                        amount);
                }
            }

            return velocityScaleKeys[
                velocityScaleKeys.Length - 1]
                .value;
        }

        private float ScaleValue(
            LegacyEftScaleKey key,
            int index,
            float seed)
        {
            return Mathf.Lerp(
                key.min,
                key.max,
                RandomValue(
                    seed +
                    157.3f +
                    index *
                    23.7f));
        }

        private Vector3 RotationVector(
            ParticleState state)
        {
            float angle =
                rotationEnabled
                    ? state.rotation
                    : state.initialRotation;

            int axis =
                rotationEnabled
                    ? rotationAxis
                    : initialRotationAxis;

            switch (axis)
            {
                case 1:
                    return new Vector3(
                        angle,
                        0f,
                        0f);
                case 2:
                    return new Vector3(
                        0f,
                        angle,
                        0f);
                case 3:
                    return new Vector3(
                        0f,
                        0f,
                        angle);
                default:
                    return Vector3.zero;
            }
        }

        private int ResolveCapacity()
        {
            if (!EmitsParticles())
                return 1;

            if (IsOneShotEmitter())
                return 1;

            float averageRate =
                (emitRateMin +
                 emitRateMax) *
                0.5f;

            float averageLife =
                Mathf.Max(
                    1f / 30f,
                    (lifeMin +
                     lifeMax) *
                    0.5f);

            float window =
                authoredLoop
                    ? averageLife
                    : Mathf.Max(
                        averageLife,
                        emitterDuration);

            return Mathf.Clamp(
                Mathf.CeilToInt(
                    averageRate *
                    window),
                1,
                MaximumParticles);
        }

        private bool EmitsParticles()
        {
            return Mathf.Max(
                       emitRateMin,
                       emitRateMax) >
                   0f &&
                   Mathf.Max(
                       lifeMin,
                       lifeMax) >
                   0f;
        }

        private bool IsOneShotEmitter()
        {
            return !authoredLoop &&
                   Mathf.Abs(
                       emitRateMin -
                       1f) <
                   0.0001f &&
                   Mathf.Abs(
                       emitRateMax -
                       1f) <
                   0.0001f;
        }

        private bool CanEmitMore()
        {
            if (_effectiveLoop)
                return true;

            if (!EmitsParticles())
                return !_oneShotEmitted;

            if (IsOneShotEmitter())
            {
                return !_oneShotEmitted &&
                       _remainingDuration >
                       0f;
            }

            return _remainingDuration >
                   0f;
        }

        private int ActiveStateCount()
        {
            int count = 0;

            for (int i = 0;
                 i < _states.Length;
                 i++)
            {
                if (_states[i].active)
                    count++;
            }

            return count;
        }

        private int FindFreeState()
        {
            for (int i = 0;
                 i < _states.Length;
                 i++)
            {
                if (!_states[i].active)
                    return i;
            }

            return -1;
        }

        private static float RandomValue(
            float seed)
        {
            return Mathf.Repeat(
                Mathf.Sin(
                    seed *
                    12.9898f) *
                43758.5453f,
                1f);
        }

        private static float RandomRange(
            float min,
            float max,
            float seed)
        {
            if (max < min)
            {
                float temp = min;
                min = max;
                max = temp;
            }

            return Mathf.Lerp(
                min,
                max,
                RandomValue(seed));
        }

        private static float SignedRandom(
            float seed)
        {
            return RandomValue(seed) *
                   2f -
                   1f;
        }

        private static float InverseRange(
            float min,
            float max,
            float value)
        {
            if (Mathf.Abs(
                    max - min) <
                0.0001f)
                return 0f;

            return Mathf.Clamp01(
                (value - min) /
                (max - min));
        }

        private static void RotateXZ(
            ref Vector3 value,
            float radians)
        {
            float c =
                Mathf.Cos(radians);
            float s =
                Mathf.Sin(radians);

            float x =
                c * value.x -
                s * value.z;

            float z =
                s * value.x +
                c * value.z;

            value.x = x;
            value.z = z;
        }

        private static void RotateXY(
            ref Vector3 value,
            float radians)
        {
            float c =
                Mathf.Cos(radians);
            float s =
                Mathf.Sin(radians);

            float x =
                s * value.y +
                c * value.x;

            float y =
                c * value.y -
                s * value.x;

            value.x = x;
            value.y = y;
        }

        private static void RotateYZ(
            ref Vector3 value,
            float radians)
        {
            float c =
                Mathf.Cos(radians);
            float s =
                Mathf.Sin(radians);

            float y =
                s * value.z +
                c * value.y;

            float z =
                c * value.z -
                s * value.y;

            value.y = y;
            value.z = z;
        }

        [Serializable]
        private struct ParticleState
        {
            public bool active;
            public float age;
            public float life;
            public Vector3 position;
            public Vector3 velocity;
            public float rotation;
            public float angularVelocity;
            public float initialRotation;
            public float seed;
        }
    }
}
