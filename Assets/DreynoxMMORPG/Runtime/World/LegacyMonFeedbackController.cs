using System;
using Dreynox.Mmorpg.Gameplay.AnimationSystem;
using Dreynox.Mmorpg.Gameplay.Combat;
using Dreynox.Mmorpg.Vfx;
using UnityEngine;

namespace Dreynox.Mmorpg.World
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class LegacyMonFeedbackController : MonoBehaviour
    {
        [SerializeField] private SemanticAnimationPlayer animationPlayer;
        [SerializeField] private ShaiyaCombatTarget combatTarget;
        [SerializeField] private AudioSource audioSource;
        [Header("MON WAV")]
        [SerializeField] private AudioClip attack1, attack2, attack3, death;
        [Header("MON EFT")]
        [SerializeField] private GameObject attack1Effect, attack2Effect, attack3Effect, deathEffect;
        [Header("Runtime")]
        [SerializeField, Min(0f)] private float damageAnimationMinimumInterval = 0.08f;
        private float _nextDamageAnimationTime;
        private bool _dead, _returnToIdle;
        private int _reactionSerial;

        public void Configure(SemanticAnimationPlayer animation, ShaiyaCombatTarget target,
            AudioClip attackClip1, AudioClip attackClip2, AudioClip attackClip3, AudioClip deathClip,
            GameObject attackEffect1 = null, GameObject attackEffect2 = null,
            GameObject attackEffect3 = null, GameObject dieEffect = null)
        {
            UnbindTarget();
            animationPlayer = animation; combatTarget = target;
            attack1 = attackClip1; attack2 = attackClip2; attack3 = attackClip3; death = deathClip;
            attack1Effect = attackEffect1; attack2Effect = attackEffect2; attack3Effect = attackEffect3; deathEffect = dieEffect;
            EnsureAudioSource(); BindTarget();
        }
        private void Awake()
        {
            EnsureAudioSource();
            if (animationPlayer == null) animationPlayer = GetComponent<SemanticAnimationPlayer>();
            if (combatTarget == null) combatTarget = GetComponent<ShaiyaCombatTarget>();
            BindTarget();
        }
        private void OnEnable()
        {
            _dead = combatTarget != null && !combatTarget.IsAlive;
            _nextDamageAnimationTime = 0;
            _returnToIdle = false;
            if (audioSource != null) audioSource.Stop();
            BindTarget();
        }
        private void OnDisable() { UnbindTarget(); }
        private void OnDestroy() { UnbindTarget(); }
        public void PlayAttack(int attackIndex)
        {
            if (_dead) return;
            switch (attackIndex)
            {
                case 1: PlayReaction("attack_1"); PlayClip(attack1); PlayEffect(attack1Effect); break;
                case 2: PlayReaction("attack_2"); PlayClip(attack2); PlayEffect(attack2Effect); break;
                case 3: PlayReaction("attack_3"); PlayClip(attack3); PlayEffect(attack3Effect); break;
                default: throw new ArgumentOutOfRangeException(nameof(attackIndex), "Legacy MON supports attack slots 1..3.");
            }
        }
        public void PlayIdle()
        {
            if (_dead) return;
            if (!PlayAnimation("idle")) PlayAnimation("breath");
        }
        private void OnDamaged(ShaiyaCombatTarget target, int amount)
        {
            if (_dead || Time.unscaledTime < _nextDamageAnimationTime) return;
            _nextDamageAnimationTime = Time.unscaledTime + damageAnimationMinimumInterval;
            PlayReaction("damage");
        }
        private void OnDied(ShaiyaCombatTarget target)
        {
            _dead = true; _returnToIdle = false;
            if (animationPlayer != null) animationPlayer.ReplaySemantic("dead");
            PlayClip(death); PlayEffect(deathEffect);
        }
        private void OnReborn(ShaiyaCombatTarget target) { _dead = false; _returnToIdle = false; PlayIdle(); }
        private bool PlayReaction(string semantic)
        {
            if (animationPlayer == null || !animationPlayer.ReplaySemantic(semantic)) return false;
            _reactionSerial = animationPlayer.PlaybackSerial;
            _returnToIdle = true;
            return true;
        }
        private void Update()
        {
            if (!_returnToIdle || _dead || animationPlayer == null) return;
            // A newer action owned by another gameplay system must not be replaced.
            if (animationPlayer.PlaybackSerial != _reactionSerial) { _returnToIdle = false; return; }
            if (!animationPlayer.CurrentClipCompleted) return;
            _returnToIdle = false;
            PlayIdle();
        }
        private bool PlayAnimation(string semantic)
        {
            return animationPlayer != null && animationPlayer.PlaySemantic(semantic);
        }
        private void PlayClip(AudioClip clip)
        {
            if (clip == null) return;
            EnsureAudioSource(); audioSource.PlayOneShot(clip);
        }
        private void PlayEffect(GameObject prefab)
        {
            if (prefab != null) LegacyEffectPool.Play(prefab, transform.position, transform.rotation);
        }
        private void EnsureAudioSource()
        {
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false; audioSource.spatialBlend = 1;
            audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            audioSource.minDistance = 2.5f; audioSource.maxDistance = 45; audioSource.dopplerLevel = 0;
        }
        private void BindTarget()
        {
            if (combatTarget == null) return;
            UnbindTarget();
            combatTarget.Damaged += OnDamaged; combatTarget.Died += OnDied; combatTarget.Reborn += OnReborn;
        }
        private void UnbindTarget()
        {
            if (combatTarget == null) return;
            combatTarget.Damaged -= OnDamaged; combatTarget.Died -= OnDied; combatTarget.Reborn -= OnReborn;
        }
    }
}
