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
        [SerializeField] private LegacyEftEffectPlayer[] effects =
            Array.Empty<LegacyEftEffectPlayer>();
        [SerializeField] private LegacyEftSequenceDefinition[] sequences =
            Array.Empty<LegacyEftSequenceDefinition>();
        [SerializeField] private bool deactivateWhenFinished = true;

        private LegacyEftSequenceDefinition _sequence;
        private float _time;
        private int _nextEvent;
        private bool _playing;

        public bool IsPlaying => _playing;
        public string CurrentSequence =>
            _sequence != null
                ? _sequence.name
                : string.Empty;

        public void Configure(
            LegacyEftEffectPlayer[] effectPlayers,
            LegacyEftSequenceDefinition[] definitions)
        {
            effects = effectPlayers ??
                      Array.Empty<LegacyEftEffectPlayer>();
            sequences = definitions ??
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

            if (_time >= _sequence.duration &&
                _nextEvent >= events.Length)
            {
                _playing = false;

                if (deactivateWhenFinished)
                    gameObject.SetActive(false);
            }
        }

        public bool Play(string sequenceName)
        {
            LegacyEftSequenceDefinition sequence =
                FindSequence(sequenceName);

            if (sequence == null)
                return false;

            StopAllEffects();

            _sequence = sequence;
            _time = 0f;
            _nextEvent = 0;
            _playing = true;
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

        public bool PlayDefault()
        {
            if (sequences == null ||
                sequences.Length == 0)
                return false;

            return Play(sequences[0].name);
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

            LegacyEftEffectPlayer effect =
                effects[index];

            if (effect != null)
                effect.Play();
        }

        private void StopAllEffects()
        {
            if (effects == null)
                return;

            for (int i = 0;
                 i < effects.Length;
                 i++)
            {
                if (effects[i] != null)
                    effects[i].Stop(
                        deactivate: true);
            }
        }
    }
}
