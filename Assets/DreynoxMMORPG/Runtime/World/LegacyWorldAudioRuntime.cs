using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dreynox.Mmorpg.World
{
    [Serializable]
    public sealed class LegacyMusicZoneRuntimeDescriptor
    {
        public int id;
        public Bounds bounds;
        [Min(0f)] public float radius;
        public AudioClip clip;
    }

    [Serializable]
    public sealed class LegacyPositionalSoundRuntimeDescriptor
    {
        public int id;
        public Vector3 center;
        [Min(0.1f)] public float radius = 25f;
        public AudioClip clip;

        [NonSerialized] public AudioSource activeSource;
    }

    public sealed class LegacyWorldAudioRuntime : MonoBehaviour
    {
        [SerializeField] private Transform observer;

        [Header("Music")]
        [SerializeField, Min(0.05f)] private float musicEvaluationInterval = 0.25f;
        [SerializeField, Min(0.01f)] private float musicCrossFadeSeconds = 1.5f;
        [SerializeField] private List<LegacyMusicZoneRuntimeDescriptor> musicZones =
            new List<LegacyMusicZoneRuntimeDescriptor>();

        [Header("Positional ambience")]
        [SerializeField, Min(0.05f)] private float soundEvaluationInterval = 0.35f;
        [SerializeField, Min(1)] private int maxActivePositionalSounds = 24;
        [SerializeField, Min(1f)] private float unloadMultiplier = 1.25f;
        [SerializeField] private List<LegacyPositionalSoundRuntimeDescriptor> positionalSounds =
            new List<LegacyPositionalSoundRuntimeDescriptor>();

        private AudioSource _musicA;
        private AudioSource _musicB;
        private AudioSource _activeMusic;
        private AudioSource _incomingMusic;

        private int _activeMusicZoneIndex = -1;
        private int _incomingMusicZoneIndex = -1;
        private float _musicFadeElapsed;
        private float _nextMusicEvaluation;
        private float _nextSoundEvaluation;

        public IReadOnlyList<LegacyMusicZoneRuntimeDescriptor> MusicZones =>
            musicZones;

        public IReadOnlyList<LegacyPositionalSoundRuntimeDescriptor> PositionalSounds =>
            positionalSounds;

        public int ActiveMusicZoneIndex =>
            _activeMusicZoneIndex;

        public int ActivePositionalSoundCount
        {
            get
            {
                int count = 0;

                for (int i = 0;
                     i < positionalSounds.Count;
                     i++)
                {
                    AudioSource source =
                        positionalSounds[i]
                            .activeSource;

                    if (source != null &&
                        source.isPlaying)
                        count++;
                }

                return count;
            }
        }

        public void Configure(
            Transform playerObserver,
            IEnumerable<LegacyMusicZoneRuntimeDescriptor> sourceMusicZones,
            IEnumerable<LegacyPositionalSoundRuntimeDescriptor> sourceSounds)
        {
            observer = playerObserver;

            musicZones.Clear();
            positionalSounds.Clear();

            if (sourceMusicZones != null)
            {
                foreach (LegacyMusicZoneRuntimeDescriptor zone in sourceMusicZones)
                {
                    if (zone == null ||
                        zone.clip == null ||
                        zone.id < 0)
                        continue;

                    musicZones.Add(zone);
                }
            }

            if (sourceSounds != null)
            {
                foreach (LegacyPositionalSoundRuntimeDescriptor sound in sourceSounds)
                {
                    if (sound == null ||
                        sound.clip == null ||
                        sound.id < 0)
                        continue;

                    sound.radius =
                        Mathf.Max(
                            0.1f,
                            sound.radius);

                    positionalSounds.Add(sound);
                }
            }

            EnsureMusicSources();
            StopAllPositionalSounds();

            _activeMusicZoneIndex = -1;
            _incomingMusicZoneIndex = -1;
            _nextMusicEvaluation = 0f;
            _nextSoundEvaluation = 0f;

            EvaluateMusic(force: true);
            EvaluatePositionalSounds();
        }

        private void Awake()
        {
            EnsureMusicSources();
        }

        private void OnDisable()
        {
            if (_musicA != null)
                _musicA.Stop();

            if (_musicB != null)
                _musicB.Stop();

            StopAllPositionalSounds();
        }

        private void Update()
        {
            if (observer == null)
                return;

            float now =
                Time.unscaledTime;

            if (now >= _nextMusicEvaluation)
            {
                _nextMusicEvaluation =
                    now +
                    musicEvaluationInterval;

                EvaluateMusic(force: false);
            }

            UpdateMusicCrossFade();

            if (now >= _nextSoundEvaluation)
            {
                _nextSoundEvaluation =
                    now +
                    soundEvaluationInterval;

                EvaluatePositionalSounds();
            }
        }

        public static int SelectMusicZoneIndex(
            Vector3 observerPosition,
            IReadOnlyList<LegacyMusicZoneRuntimeDescriptor> zones)
        {
            if (zones == null ||
                zones.Count == 0)
                return -1;

            int best = -1;
            float bestDistance =
                float.PositiveInfinity;

            for (int i = 0;
                 i < zones.Count;
                 i++)
            {
                LegacyMusicZoneRuntimeDescriptor zone =
                    zones[i];

                if (zone == null ||
                    zone.clip == null)
                    continue;

                if (!Contains(
                        zone.bounds,
                        zone.radius,
                        observerPosition))
                    continue;

                float distance =
                    (zone.bounds.center -
                     observerPosition)
                    .sqrMagnitude;

                if (distance <
                    bestDistance)
                {
                    best = i;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private void EvaluateMusic(bool force)
        {
            int selected =
                SelectMusicZoneIndex(
                    observer.position,
                    musicZones);

            if (!force &&
                (selected ==
                     _activeMusicZoneIndex ||
                 selected ==
                     _incomingMusicZoneIndex))
                return;

            if (selected < 0)
            {
                BeginMusicTransition(
                    null,
                    -1);
                return;
            }

            BeginMusicTransition(
                musicZones[selected].clip,
                selected);
        }

        private void BeginMusicTransition(
            AudioClip clip,
            int zoneIndex)
        {
            EnsureMusicSources();

            if (_incomingMusic != null)
            {
                _incomingMusic.Stop();
                _incomingMusic.clip = null;
                _incomingMusic.volume = 0f;
            }

            AudioSource next =
                ReferenceEquals(
                    _activeMusic,
                    _musicA)
                    ? _musicB
                    : _musicA;

            _incomingMusic =
                next;

            _incomingMusicZoneIndex =
                zoneIndex;

            _musicFadeElapsed = 0f;

            if (clip != null)
            {
                _incomingMusic.clip = clip;
                _incomingMusic.loop = true;
                _incomingMusic.volume = 0f;
                _incomingMusic.Play();
            }
            else
            {
                _incomingMusic.clip = null;
                _incomingMusic.volume = 0f;
            }

            if (_activeMusic == null)
            {
                _activeMusic =
                    ReferenceEquals(
                        next,
                        _musicA)
                        ? _musicB
                        : _musicA;

                _activeMusic.volume = 0f;
            }
        }

        private void UpdateMusicCrossFade()
        {
            if (_incomingMusic == null)
                return;

            _musicFadeElapsed +=
                Time.unscaledDeltaTime;

            float t =
                musicCrossFadeSeconds <= 0f
                    ? 1f
                    : Mathf.Clamp01(
                        _musicFadeElapsed /
                        musicCrossFadeSeconds);

            if (_activeMusic != null)
            {
                _activeMusic.volume =
                    1f - t;
            }

            if (_incomingMusic.clip != null)
            {
                _incomingMusic.volume =
                    t;
            }

            if (t < 1f)
                return;

            if (_activeMusic != null)
            {
                _activeMusic.Stop();
                _activeMusic.clip = null;
                _activeMusic.volume = 0f;
            }

            _activeMusic =
                _incomingMusic;

            _activeMusicZoneIndex =
                _incomingMusicZoneIndex;

            _incomingMusic = null;
            _incomingMusicZoneIndex = -1;
        }

        private void EvaluatePositionalSounds()
        {
            float unloadScale =
                Mathf.Max(
                    1f,
                    unloadMultiplier);

            int active = 0;

            for (int i = 0;
                 i < positionalSounds.Count;
                 i++)
            {
                LegacyPositionalSoundRuntimeDescriptor sound =
                    positionalSounds[i];

                AudioSource source =
                    sound.activeSource;

                if (source == null)
                    continue;

                float unloadRadius =
                    sound.radius *
                    unloadScale;

                float sqr =
                    (observer.position -
                     sound.center)
                    .sqrMagnitude;

                if (sqr >
                    unloadRadius *
                    unloadRadius)
                {
                    UnityEngine.Object.Destroy(
                        source.gameObject);

                    sound.activeSource =
                        null;
                }
                else
                {
                    active++;
                }
            }

            if (active >=
                maxActivePositionalSounds)
                return;

            var candidates =
                new List<SoundCandidate>();

            for (int i = 0;
                 i < positionalSounds.Count;
                 i++)
            {
                LegacyPositionalSoundRuntimeDescriptor sound =
                    positionalSounds[i];

                if (sound.activeSource != null)
                    continue;

                float sqr =
                    (observer.position -
                     sound.center)
                    .sqrMagnitude;

                if (sqr <=
                    sound.radius *
                    sound.radius)
                {
                    candidates.Add(
                        new SoundCandidate
                        {
                            descriptor = sound,
                            sqrDistance = sqr
                        });
                }
            }

            candidates.Sort(
                (left, right) =>
                    left.sqrDistance
                        .CompareTo(
                            right.sqrDistance));

            for (int i = 0;
                 i < candidates.Count &&
                 active <
                 maxActivePositionalSounds;
                 i++)
            {
                Activate(
                    candidates[i]
                        .descriptor);

                active++;
            }
        }

        private void Activate(
            LegacyPositionalSoundRuntimeDescriptor descriptor)
        {
            GameObject go =
                new GameObject(
                    "LegacyWorldSound_" +
                    descriptor.id);

            go.transform.SetParent(
                transform,
                false);

            go.transform.position =
                descriptor.center;

            AudioSource source =
                go.AddComponent<AudioSource>();

            source.clip =
                descriptor.clip;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode =
                AudioRolloffMode.Linear;
            source.minDistance =
                Mathf.Max(
                    1f,
                    descriptor.radius *
                    0.15f);
            source.maxDistance =
                descriptor.radius;
            source.dopplerLevel = 0f;
            source.volume = 1f;
            source.Play();

            descriptor.activeSource =
                source;
        }

        private void StopAllPositionalSounds()
        {
            for (int i = 0;
                 i < positionalSounds.Count;
                 i++)
            {
                AudioSource source =
                    positionalSounds[i]
                        .activeSource;

                if (source != null)
                    UnityEngine.Object.Destroy(
                        source.gameObject);

                positionalSounds[i]
                    .activeSource = null;
            }
        }

        private void EnsureMusicSources()
        {
            if (_musicA == null)
                _musicA =
                    CreateMusicSource(
                        "LegacyMusic_A");

            if (_musicB == null)
                _musicB =
                    CreateMusicSource(
                        "LegacyMusic_B");
        }

        private AudioSource CreateMusicSource(
            string sourceName)
        {
            GameObject go =
                new GameObject(sourceName);

            go.transform.SetParent(
                transform,
                false);

            AudioSource source =
                go.AddComponent<AudioSource>();

            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0f;
            source.dopplerLevel = 0f;

            return source;
        }

        private static bool Contains(
            Bounds bounds,
            float radius,
            Vector3 point)
        {
            if (bounds.Contains(point))
                return true;

            Vector3 closest =
                bounds.ClosestPoint(point);

            float extra =
                Mathf.Max(
                    0f,
                    radius);

            return (closest - point)
                       .sqrMagnitude <=
                   extra * extra;
        }

        private struct SoundCandidate
        {
            public LegacyPositionalSoundRuntimeDescriptor descriptor;
            public float sqrDistance;
        }
    }
}
