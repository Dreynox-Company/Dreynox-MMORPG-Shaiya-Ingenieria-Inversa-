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
        [SerializeField] private AudioClip attack1;
        [SerializeField] private AudioClip attack2;
        [SerializeField] private AudioClip attack3;
        [SerializeField] private AudioClip death;

        [Header("MON EFT")]
        [SerializeField] private GameObject attack1Effect;
        [SerializeField] private GameObject attack2Effect;
        [SerializeField] private GameObject attack3Effect;
        [SerializeField] private GameObject deathEffect;

        [Header("Runtime")]
        [SerializeField, Min(0f)] private float damageAnimationMinimumInterval = 0.08f;

        private float _nextDamageAnimationTime;
        private bool _dead;

        public void Configure(
            SemanticAnimationPlayer animation,
            ShaiyaCombatTarget target,
            AudioClip attackClip1,
            AudioClip attackClip2,
            AudioClip attackClip3,
            AudioClip deathClip,
            GameObject attackEffect1 = null,
            GameObject attackEffect2 = null,
            GameObject attackEffect3 = null,
            GameObject dieEffect = null)
        {
            UnbindTarget();

            animationPlayer = animation;
            combatTarget = target;
            attack1 = attackClip1;
            attack2 = attackClip2;
            attack3 = attackClip3;
            death = deathClip;
            attack1Effect = attackEffect1;
            attack2Effect = attackEffect2;
            attack3Effect = attackEffect3;
            deathEffect = dieEffect;

            EnsureAudioSource();
            BindTarget();
        }

        private void Awake()
        {
            EnsureAudioSource();

            if (animationPlayer == null)
                animationPlayer = GetComponent<SemanticAnimationPlayer>();

            if (combatTarget == null)
                combatTarget = GetComponent<ShaiyaCombatTarget>();

            BindTarget();
        }

        private void OnEnable()
        {
            BindTarget();
        }

        private void OnDisable()
        {
            UnbindTarget();
        }

        private void OnDestroy()
        {
            UnbindTarget();
        }

        public void PlayAttack(int attackIndex)
        {
            if (_dead)
                return;

            switch (attackIndex)
            {
                case 1:
                    PlayAnimation("attack_1");
                    PlayClip(attack1);
                    PlayEffect(attack1Effect);
                    break;

                case 2:
                    PlayAnimation("attack_2");
                    PlayClip(attack2);
                    PlayEffect(attack2Effect);
                    break;

                case 3:
                    PlayAnimation("attack_3");
                    PlayClip(attack3);
                    PlayEffect(attack3Effect);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(attackIndex),
                        attackIndex,
                        "Legacy MON supports attack slots 1..3.");
            }
        }

        public void PlayIdle()
        {
            if (_dead)
                return;

            if (!PlayAnimation("idle"))
                PlayAnimation("breath");
        }

        private void OnDamaged(
            ShaiyaCombatTarget target,
            int amount)
        {
            if (_dead ||
                Time.unscaledTime <
                _nextDamageAnimationTime)
                return;

            _nextDamageAnimationTime =
                Time.unscaledTime +
                damageAnimationMinimumInterval;

            PlayAnimation("damage");
        }

        private void OnDied(
            ShaiyaCombatTarget target)
        {
            _dead = true;
            PlayAnimation("dead");
            PlayClip(death);
            PlayEffect(deathEffect);
        }

        private void OnReborn(
            ShaiyaCombatTarget target)
        {
            _dead = false;
            PlayIdle();
        }

        private bool PlayAnimation(string semantic)
        {
            return animationPlayer != null &&
                   animationPlayer.PlaySemantic(semantic);
        }

        private void PlayClip(AudioClip clip)
        {
            if (clip == null)
                return;

            EnsureAudioSource();
            audioSource.PlayOneShot(clip);
        }

        private void PlayEffect(GameObject prefab)
        {
            if (prefab == null)
                return;

            LegacyEffectPool.Play(
                prefab,
                transform.position,
                transform.rotation);
        }

        private void EnsureAudioSource()
        {
            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();

            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();

            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
            audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            audioSource.minDistance = 2.5f;
            audioSource.maxDistance = 45f;
            audioSource.dopplerLevel = 0f;
        }

        private void BindTarget()
        {
            if (combatTarget == null)
                return;

            combatTarget.Damaged -= OnDamaged;
            combatTarget.Died -= OnDied;
            combatTarget.Reborn -= OnReborn;

            combatTarget.Damaged += OnDamaged;
            combatTarget.Died += OnDied;
            combatTarget.Reborn += OnReborn;
        }

        private void UnbindTarget()
        {
            if (combatTarget == null)
                return;

            combatTarget.Damaged -= OnDamaged;
            combatTarget.Died -= OnDied;
            combatTarget.Reborn -= OnReborn;
        }
    }
}
