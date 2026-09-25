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
        private AnimationClipPlayable _slot0, _slot1;
        private bool _slot0Valid, _slot1Valid;
        private int _currentSlot, _targetSlot;
        private bool _transitioning, _stopping;
        private float _fadeElapsed, _fadeDuration, _stopWeight0, _stopWeight1;
        private string _semantic = string.Empty, _resolvedSemantic = string.Empty;

        public string CurrentSemantic => _semantic;
        public string ResolvedSemantic => _resolvedSemantic;
        public bool HasPlayableClip => _slot0Valid || _slot1Valid;
        public int PlaybackSerial { get; private set; }
        public float CurrentClipDuration { get; private set; }
        public bool CurrentClipCompleted => HasPlayableClip && CurrentClipTime >= CurrentClipDuration;
        public double CurrentClipTime
        {
            get
            {
                int slot = _transitioning ? _targetSlot : _currentSlot;
                if (!SlotValid(slot)) return 0;
                return slot == 0 ? _slot0.GetTime() : _slot1.GetTime();
            }
        }
        public AnimationStateCatalog Catalog
        {
            get => catalog;
            set { if (catalog != value) _semantic = string.Empty; catalog = value; }
        }

        private void Awake() { _animator = GetComponent<Animator>(); CreateGraph(); }
        private void OnEnable() { if (_graph.IsValid() && !_graph.IsPlaying()) _graph.Play(); }
        private void OnDisable() { if (_graph.IsValid() && _graph.IsPlaying()) _graph.Stop(); }
        private void OnDestroy() { if (_graph.IsValid()) _graph.Destroy(); }

        private void Update()
        {
            if (!_graph.IsValid() || (!_transitioning && !_stopping)) return;
            _fadeElapsed += Time.deltaTime;
            float t = _fadeDuration <= 0 ? 1 : Mathf.Clamp01(_fadeElapsed / _fadeDuration);
            if (_stopping)
            {
                _mixer.SetInputWeight(0, _stopWeight0 * (1 - t));
                _mixer.SetInputWeight(1, _stopWeight1 * (1 - t));
                if (t >= 1) ClearPlayback();
                return;
            }
            _mixer.SetInputWeight(_currentSlot, 1 - t);
            _mixer.SetInputWeight(_targetSlot, t);
            if (t >= 1) CompleteTransition();
        }
        public bool PlaySemantic(string semanticState)
        {
            return PlaySemantic(semanticState, defaultCrossFadeSeconds);
        }
        public bool PlaySemantic(string semanticState, float crossFadeSeconds)
        {
            return PlayInternal(semanticState, crossFadeSeconds, false);
        }
        /// <summary>One accepted action, one restart. Never invoke this every render frame.</summary>
        public bool ReplaySemantic(string semanticState, float crossFadeSeconds = 0.06f)
        {
            return PlayInternal(semanticState, crossFadeSeconds, true);
        }
        private bool PlayInternal(string semanticState, float crossFadeSeconds, bool restart)
        {
            if (catalog == null || string.IsNullOrWhiteSpace(semanticState)) return false;
            if (!restart && !_stopping && HasPlayableClip &&
                string.Equals(_semantic, semanticState, System.StringComparison.OrdinalIgnoreCase)) return true;
            if (!catalog.TryResolve(semanticState, out AnimationClip clip, out float speed, out string resolved)) return false;
            if (!_graph.IsValid()) CreateGraph();
            _stopping = false;
            int sourceSlot = ResolveSourceSlot();
            int destinationSlot = sourceSlot == 0 ? 1 : 0;
            DestroySlot(destinationSlot);
            var playable = AnimationClipPlayable.Create(_graph, clip);
            playable.SetSpeed(speed);
            playable.SetApplyFootIK(applyFootIk);
            playable.SetApplyPlayableIK(applyPlayableIk);
            playable.SetTime(0);
            SetSlot(destinationSlot, playable);
            _mixer.ConnectInput(destinationSlot, playable, 0);
            if (!SlotValid(sourceSlot))
            {
                _mixer.SetInputWeight(destinationSlot, 1);
                _currentSlot = destinationSlot; _targetSlot = destinationSlot;
                _transitioning = false;
            }
            else
            {
                _currentSlot = sourceSlot; _targetSlot = destinationSlot;
                _mixer.SetInputWeight(sourceSlot, 1);
                _mixer.SetInputWeight(destinationSlot, 0);
                _fadeElapsed = 0;
                _fadeDuration = Mathf.Max(0, crossFadeSeconds);
                _transitioning = true;
                if (_fadeDuration <= 0) CompleteTransition();
            }
            _semantic = semanticState; _resolvedSemantic = resolved;
            CurrentClipDuration = clip.length;
            PlaybackSerial++;
            if (!_graph.IsPlaying()) _graph.Play();
            return true;
        }
        public void Stop(float fadeSeconds = 0)
        {
            if (!HasPlayableClip) return;
            if (fadeSeconds <= 0) { ClearPlayback(); return; }
            _stopWeight0 = _mixer.GetInputWeight(0);
            _stopWeight1 = _mixer.GetInputWeight(1);
            _stopping = true; _transitioning = false;
            _fadeElapsed = 0; _fadeDuration = fadeSeconds;
        }
        private void ClearPlayback()
        {
            DestroySlot(0); DestroySlot(1);
            if (_graph.IsValid()) { _mixer.SetInputWeight(0, 0); _mixer.SetInputWeight(1, 0); }
            _semantic = string.Empty; _resolvedSemantic = string.Empty;
            _transitioning = false; _stopping = false;
            CurrentClipDuration = 0;
        }
        private void CreateGraph()
        {
            if (_animator == null) _animator = GetComponent<Animator>();
            if (_graph.IsValid()) _graph.Destroy();
            _graph = PlayableGraph.Create(name + "_SemanticAnimationGraph");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            _mixer = AnimationMixerPlayable.Create(_graph, 2);
            _output = AnimationPlayableOutput.Create(_graph, "Animation", _animator);
            _output.SetSourcePlayable(_mixer);
            _currentSlot = 0; _targetSlot = 0;
            _slot0Valid = false; _slot1Valid = false;
            _transitioning = false; _stopping = false;
            _mixer.SetInputWeight(0, 0); _mixer.SetInputWeight(1, 0);
            _graph.Play();
        }
        private int ResolveSourceSlot()
        {
            if (_transitioning)
            {
                int winner = _mixer.GetInputWeight(1) > _mixer.GetInputWeight(0) ? 1 : 0;
                int loser = winner == 0 ? 1 : 0;
                _mixer.SetInputWeight(loser, 0);
                DestroySlot(loser);
                _mixer.SetInputWeight(winner, SlotValid(winner) ? 1 : 0);
                _transitioning = false;
                return winner;
            }
            if (SlotValid(_currentSlot)) return _currentSlot;
            if (SlotValid(0)) return 0;
            return SlotValid(1) ? 1 : 0;
        }
        private void CompleteTransition()
        {
            if (_currentSlot != _targetSlot)
            {
                _mixer.SetInputWeight(_currentSlot, 0);
                DestroySlot(_currentSlot);
            }
            _mixer.SetInputWeight(_targetSlot, SlotValid(_targetSlot) ? 1 : 0);
            _currentSlot = _targetSlot;
            _transitioning = false;
        }
        private bool SlotValid(int slot) { return slot == 0 ? _slot0Valid : _slot1Valid; }
        private void SetSlot(int slot, AnimationClipPlayable playable)
        {
            if (slot == 0) { _slot0 = playable; _slot0Valid = true; }
            else { _slot1 = playable; _slot1Valid = true; }
        }
        private void DestroySlot(int slot)
        {
            if (slot == 0)
            {
                if (_slot0Valid && _slot0.IsValid()) _slot0.Destroy();
                _slot0Valid = false;
            }
            else
            {
                if (_slot1Valid && _slot1.IsValid()) _slot1.Destroy();
                _slot1Valid = false;
            }
        }
    }
}
