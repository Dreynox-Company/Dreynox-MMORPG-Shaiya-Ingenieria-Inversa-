using System;
using UnityEngine;

namespace Dreynox.Mmorpg.Vfx
{
    [Serializable]
    public struct LegacyEftSequenceEvent
    {
        public int effectIndex;
        public float time;
    }

    [Serializable]
    public sealed class LegacyEftSequenceDefinition
    {
        public string name = string.Empty;
        public LegacyEftSequenceEvent[] events =
            Array.Empty<LegacyEftSequenceEvent>();
        public float duration;
    }

    public sealed class LegacyEftSequencePlayer : MonoBehaviour
    {
        [SerializeField] private LegacyEftParticleEmitter[] effects =
            Array.Empty<LegacyEftParticleEmitter>();
        [SerializeField] private LegacyEftSequenceDefinition[] sequences =
            Array.Empty<LegacyEftSequenceDefinition>();
        [SerializeField] private bool deactivateWhenFinished = true;

        private LegacyEftSequenceDefinition _sequence;
        private float _time;
        private int _nextEvent;
        private bool _playing;
        private bool _forceOneShot;

        public bool IsPlaying => _playing;
        public int SequenceCount =>
            sequences != null ? sequences.Length : 0;
        public int EffectCount =>
            effects != null ? effects.Length : 0;

        public string CurrentSequence =>
            _sequence != null
                ? _sequence.name
                : string.Empty;

        public void Configure(
            LegacyEftParticleEmitter[] effectEmitters,
            LegacyEftSequenceDefinition[] definitions)
        {
            effects =
                effectEmitters ??
                Array.Empty<LegacyEftParticleEmitter>();

            sequences =
                definitions ??
                Array.Empty<LegacyEftSequenceDefinition>();
        }

        private void Awake()
        {
            StopAllEffects();
        }

        private void Update()
        {
            if (!_playing ||
                _sequence == null)
                return;

            _time += Time.deltaTime;

            LegacyEftSequenceEvent[] events =
                _sequence.events ??
                Array.Empty<LegacyEftSequenceEvent>();

            while (_nextEvent < events.Length &&
                   events[_nextEvent].time <= _time)
            {
                PlayEffect(
                    events[_nextEvent].effectIndex);

                _nextEvent++;
            }

            bool allEmittersFinished =
                true;

            for (int i = 0;
                 i < effects.Length;
                 i++)
            {
                if (effects[i] != null &&
                    effects[i].IsPlaying)
                {
                    allEmittersFinished = false;
                    break;
                }
            }

            if (_nextEvent >= events.Length &&
                allEmittersFinished &&
                _time >= _sequence.duration)
            {
                _playing = false;

                if (deactivateWhenFinished)
                    gameObject.SetActive(false);
            }
        }

        public bool Play(
            string sequenceName,
            bool forceOneShot = true)
        {
            LegacyEftSequenceDefinition sequence =
                FindSequence(sequenceName);

            return StartSequence(
                sequence,
                forceOneShot);
        }

        public bool PlaySequence(
            int sequenceIndex,
            bool forceOneShot = true)
        {
            if (sequences == null ||
                sequenceIndex < 0 ||
                sequenceIndex >= sequences.Length)
                return false;

            return StartSequence(
                sequences[sequenceIndex],
                forceOneShot);
        }

        public bool PlayRawEffect(
            int effectIndex,
            bool forceOneShot = true)
        {
            if (effects == null ||
                effectIndex < 0 ||
                effectIndex >= effects.Length ||
                effects[effectIndex] == null)
                return false;

            LegacyEftParticleEmitter emitter =
                effects[effectIndex];

            var synthetic =
                new LegacyEftSequenceDefinition
                {
                    name =
                        "raw_effect_" +
                        effectIndex,
                    events =
                        new[]
                        {
                            new LegacyEftSequenceEvent
                            {
                                effectIndex =
                                    effectIndex,
                                time = 0f
                            }
                        },
                    duration =
                        Mathf.Max(
                            0.05f,
                            emitter
                                .EstimatedOneShotDuration)
                };

            return StartSequence(
                synthetic,
                forceOneShot);
        }

        public bool PlayDefault(
            bool forceOneShot = true)
        {
            return PlaySequence(
                0,
                forceOneShot);
        }

        private bool StartSequence(
            LegacyEftSequenceDefinition sequence,
            bool forceOneShot)
        {
            if (sequence == null)
                return false;

            StopAllEffects();

            _sequence = sequence;
            _time = 0f;
            _nextEvent = 0;
            _playing = true;
            _forceOneShot = forceOneShot;

            gameObject.SetActive(true);

            LegacyEftSequenceEvent[] events =
                sequence.events ??
                Array.Empty<LegacyEftSequenceEvent>();

            while (_nextEvent < events.Length &&
                   events[_nextEvent].time <= 0f)
            {
                PlayEffect(
                    events[_nextEvent].effectIndex);

                _nextEvent++;
            }

            return true;
        }

        public void Stop()
        {
            _playing = false;
            _sequence = null;
            _time = 0f;
            _nextEvent = 0;
            StopAllEffects();
        }

        private LegacyEftSequenceDefinition FindSequence(
            string name)
        {
            if (sequences == null ||
                sequences.Length == 0)
                return null;

            if (string.IsNullOrWhiteSpace(name))
                return sequences[0];

            for (int i = 0;
                 i < sequences.Length;
                 i++)
            {
                if (sequences[i] != null &&
                    string.Equals(
                        sequences[i].name,
                        name,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return sequences[i];
                }
            }

            return null;
        }

        private void PlayEffect(int index)
        {
            if (index < 0 ||
                effects == null ||
                index >= effects.Length)
                return;

            LegacyEftParticleEmitter effect =
                effects[index];

            if (effect == null)
                return;

            effect.gameObject.SetActive(true);
            effect.Play(_forceOneShot);
        }

        private void StopAllEffects()
        {
            if (effects == null)
                return;

            for (int i = 0;
                 i < effects.Length;
                 i++)
            {
                if (effects[i] == null)
                    continue;

                effects[i].Stop(
                    clearParticles: true);

                effects[i].gameObject.SetActive(false);
            }
        }
    }
}
