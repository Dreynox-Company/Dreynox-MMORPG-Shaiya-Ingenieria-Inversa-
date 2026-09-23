using System;
using UnityEngine;

namespace Dreynox.Mmorpg.World
{
    public enum LegacyMonCatalogKind
    {
        Monster,
        Npc,
        Wing
    }

    [Serializable]
    public struct LegacyAttachedEffectDescriptor
    {
        public int boneId;
        public int effectId;
    }

    public sealed class LegacyMonEntityDescriptor : MonoBehaviour
    {
        [SerializeField] private LegacyMonCatalogKind catalogKind;
        [SerializeField] private int modelIndex;
        [SerializeField] private string legacyName = string.Empty;
        [SerializeField] private float legacyHeight;

        [Header("Legacy audio")]
        [SerializeField] private string attack1Wav = string.Empty;
        [SerializeField] private string attack2Wav = string.Empty;
        [SerializeField] private string attack3Wav = string.Empty;
        [SerializeField] private string deathWav = string.Empty;

        [Header("Legacy VFX")]
        [SerializeField] private string attack1Effect = string.Empty;
        [SerializeField] private string attack2Effect = string.Empty;
        [SerializeField] private string attack3Effect = string.Empty;
        [SerializeField] private string dieEffect = string.Empty;
        [SerializeField] private string attachEffect = string.Empty;

        [SerializeField] private LegacyAttachedEffectDescriptor[] attachedEffects =
            Array.Empty<LegacyAttachedEffectDescriptor>();

        public LegacyMonCatalogKind CatalogKind => catalogKind;
        public int ModelIndex => modelIndex;
        public string LegacyName => legacyName;
        public float LegacyHeight => legacyHeight;
        public string Attack1Wav => attack1Wav;
        public string Attack2Wav => attack2Wav;
        public string Attack3Wav => attack3Wav;
        public string DeathWav => deathWav;
        public string Attack1Effect => attack1Effect;
        public string Attack2Effect => attack2Effect;
        public string Attack3Effect => attack3Effect;
        public string DieEffect => dieEffect;
        public string AttachEffect => attachEffect;
        public IReadOnlyList<LegacyAttachedEffectDescriptor> AttachedEffects =>
            attachedEffects;

        public void Configure(
            LegacyMonCatalogKind kind,
            int index,
            string name,
            float height,
            string wav1,
            string wav2,
            string wav3,
            string wavDeath,
            string effect1,
            string effect2,
            string effect3,
            string effectDie,
            string effectAttach,
            LegacyAttachedEffectDescriptor[] effects)
        {
            catalogKind = kind;
            modelIndex = index;
            legacyName = name ?? string.Empty;
            legacyHeight = Mathf.Max(0f, height);

            attack1Wav = NormalizeResourceName(wav1);
            attack2Wav = NormalizeResourceName(wav2);
            attack3Wav = NormalizeResourceName(wav3);
            deathWav = NormalizeResourceName(wavDeath);

            attack1Effect = NormalizeResourceName(effect1);
            attack2Effect = NormalizeResourceName(effect2);
            attack3Effect = NormalizeResourceName(effect3);
            dieEffect = NormalizeResourceName(effectDie);
            attachEffect = NormalizeResourceName(effectAttach);

            attachedEffects = effects ?? Array.Empty<LegacyAttachedEffectDescriptor>();
        }

        public static string NormalizeResourceName(string value)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                string.Equals(value.Trim(), "LOAD", StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            return value.Trim();
        }
    }
}
