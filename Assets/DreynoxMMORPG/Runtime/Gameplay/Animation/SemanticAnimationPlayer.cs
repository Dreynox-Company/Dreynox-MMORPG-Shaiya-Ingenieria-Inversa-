using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Dreynox.Mmorpg.Gameplay.AnimationSystem
{
    [RequireComponent(typeof(Animator))]
    public sealed class SemanticAnimationPlayer : MonoBehaviour
    {
        [SerializeField] private AnimationStateCatalog catalog;
        [SerializeField, Min(0f)] private float defaultCrossFadeSeconds = 0.12f;
        [SerializeField] private bool applyFootIk;
        [SerializeField] private bool applyPlayableIk;

        private Animator _animator;
        private PlayableGraph _graph;
        private AnimationMixerPlayable _mixer;
        private AnimationPlayableOutput _output;

        private AnimationClipPlayable _slot0;
        private AnimationClipPlayable _slot1;
        private bool _slot0Valid;
        private bool _slot1Valid;
        private int _currentSlot;
        private int _targetSlot;
        private bool _transitioning;
        private float _fadeElapsed;
        private float _fadeDuration;
        private string _semantic = string.Empty;
        private string _resolvedSemantic = string.Empty;

        public string CurrentSemantic => _semantic;
        public string ResolvedSemantic => _resolvedSemantic;
        public bool HasPlayableClip => _slot0Valid || _slot1Valid;

        public AnimationStateCatalog Catalog
        {
            get => catalog;
            set => catalog = value;
        }

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            CreateGraph();
        }

        private void OnEnable()
        {
            if (_graph.IsValid() && !_graph.IsPlaying())
                _graph.Play();
        }

        private void OnDisable()
        {
            if (_graph.IsValid() && _graph.IsPlaying())
                _graph.Stop();
        }

        private void OnDestroy()
        {
            if (_graph.IsValid())
                _graph.Destroy();
        }

        private void Update()
        {
            if (!_transitioning || !_graph.IsValid())
                return;

            _fadeElapsed += Time.deltaTime;
            float t = _fadeDuration <= 0f
                ? 1f
                : Mathf.Clamp01(_fadeElapsed / _fadeDuration);

            _mixer.SetInputWeight(_currentSlot, 1f - t);
            _mixer.SetInputWeight(_targetSlot, t);

            if (t >= 1f)
                CompleteTransition();
        }

        public bool PlaySemantic(string semanticState)
        {
            return PlaySemantic(semanticState, defaultCrossFadeSeconds);
        }

        public bool PlaySemantic(string semanticState, float crossFadeSeconds)
        {
            if (catalog == null || string.IsNullOrWhiteSpace(semanticState))
                return false;

            if (string.Equals(_semantic, semanticState, System.StringComparison.OrdinalIgnoreCase) &&
                HasPlayableClip)
                return true;

            AnimationClip clip;
            float speed;
            string resolved;
            if (!catalog.TryResolve(semanticState, out clip, out speed, out resolved))
                return false;

            if (!_graph.IsValid())
                CreateGraph();

            int sourceSlot = ResolveSourceSlot();
            int destinationSlot = sourceSlot == 0 ? 1 : 0;

            DestroySlot(destinationSlot);
            AnimationClipPlayable playable = AnimationClipPlayable.Create(_graph, clip);
            playable.SetSpeed(speed);
            playable.SetApplyFootIK(applyFootIk);
            playable.SetApplyPlayableIK(applyPlayableIk);
            playable.SetTime(0.0);

            SetSlot(destinationSlot, playable, true);
            _mixer.ConnectInput(destinationSlot, playable, 0);

            if (!SlotValid(sourceSlot))
            {
                _mixer.SetInputWeight(destinationSlot, 1f);
                _currentSlot = destinationSlot;
                _targetSlot = destinationSlot;
                _transitioning = false;
            }
            else
            {
                _currentSlot = sourceSlot;
                _targetSlot = destinationSlot;
                _mixer.SetInputWeight(sourceSlot, 1f);
                _mixer.SetInputWeight(destinationSlot, 0f);
                _fadeElapsed = 0f;
                _fadeDuration = Mathf.Max(0f, crossFadeSeconds);
                _transitioning = true;

                if (_fadeDuration <= 0f)
                {
                    _mixer.SetInputWeight(sourceSlot, 0f);
                    _mixer.SetInputWeight(destinationSlot, 1f);
                    CompleteTransition();
                }
            }

            _semantic = semanticState;
            _resolvedSemantic = resolved;

            if (!_graph.IsPlaying())
                _graph.Play();

            return true;
        }

        public void Stop(float fadeSeconds = 0f)
        {
            if (!HasPlayableClip) return;

            if (fadeSeconds <= 0f)
            {
                DestroySlot(0);
                DestroySlot(1);
                _mixer.SetInputWeight(0, 0f);
                _mixer.SetInputWeight(1, 0f);
                _semantic = string.Empty;
                _resolvedSemantic = string.Empty;
                _transitioning = false;
                return;
            }

            // A stop fade is represented by fading the current slot to zero.
            int source = ResolveSourceSlot();
            if (!SlotValid(source)) return;
            _currentSlot = source;
            _targetSlot = source == 0 ? 1 : 0;
            DestroySlot(_targetSlot);
            _fadeElapsed = 0f;
            _fadeDuration = fadeSeconds;
            _mixer.SetInputWeight(source, 1f);
            _mixer.SetInputWeight(_targetSlot, 0f);
            _transitioning = false;

            // Keep stop deterministic instead of constructing an empty playable.
            _mixer.SetInputWeight(source, 0f);
            DestroySlot(source);
            _semantic = string.Empty;
            _resolvedSemantic = string.Empty;
        }

        private void CreateGraph()
        {
            if (_graph.IsValid())
                _graph.Destroy();

            _graph = PlayableGraph.Create(name + "_SemanticAnimationGraph");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            _mixer = AnimationMixerPlayable.Create(_graph, 2, true);
            _output = AnimationPlayableOutput.Create(_graph, "Animation", _animator);
            _output.SetSourcePlayable(_mixer);
            _currentSlot = 0;
            _targetSlot = 0;
            _slot0Valid = false;
            _slot1Valid = false;
            _mixer.SetInputWeight(0, 0f);
            _mixer.SetInputWeight(1, 0f);
            _graph.Play();
        }

        private int ResolveSourceSlot()
        {
            if (_transitioning)
            {
                float w0 = _mixer.GetInputWeight(0);
                float w1 = _mixer.GetInputWeight(1);
                int winner = w1 > w0 ? 1 : 0;
                int loser = winner == 0 ? 1 : 0;

                if (SlotValid(loser))
                {
                    _mixer.SetInputWeight(loser, 0f);
                    DestroySlot(loser);
                }

                _mixer.SetInputWeight(winner, SlotValid(winner) ? 1f : 0f);
                _transitioning = false;
                return winner;
            }

            if (SlotValid(_currentSlot)) return _currentSlot;
            if (SlotValid(0)) return 0;
            if (SlotValid(1)) return 1;
            return 0;
        }

        private void CompleteTransition()
        {
            if (_currentSlot != _targetSlot)
            {
                _mixer.SetInputWeight(_currentSlot, 0f);
                DestroySlot(_currentSlot);
            }

            _mixer.SetInputWeight(_targetSlot, SlotValid(_targetSlot) ? 1f : 0f);
            _currentSlot = _targetSlot;
            _transitioning = false;
        }

        private bool SlotValid(int slot)
        {
            return slot == 0 ? _slot0Valid : _slot1Valid;
        }

        private void SetSlot(int slot, AnimationClipPlayable playable, bool valid)
        {
            if (slot == 0)
            {
                _slot0 = playable;
                _slot0Valid = valid;
            }
            else
            {
                _slot1 = playable;
                _slot1Valid = valid;
            }
        }

        private void DestroySlot(int slot)
        {
            if (slot == 0)
            {
                if (_slot0Valid && _slot0.IsValid())
                    _slot0.Destroy();
                _slot0Valid = false;
            }
            else
            {
                if (_slot1Valid && _slot1.IsValid())
                    _slot1.Destroy();
                _slot1Valid = false;
            }
        }
    }
}
