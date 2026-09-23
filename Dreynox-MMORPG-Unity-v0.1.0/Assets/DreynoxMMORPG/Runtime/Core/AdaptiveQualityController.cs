using UnityEngine;

namespace Dreynox.Mmorpg.Core
{
    public enum DreynoxQualityTier { MobileLow, MobileHigh, DesktopBalanced, DesktopUltra }

    public sealed class AdaptiveQualityController : MonoBehaviour
    {
        [SerializeField] private bool applyOnAwake = true;
        [SerializeField] private bool forceTier;
        [SerializeField] private DreynoxQualityTier forcedTier = DreynoxQualityTier.DesktopBalanced;
        public DreynoxQualityTier ActiveTier { get; private set; }

        private void Awake()
        {
            if (applyOnAwake) Apply(forceTier ? forcedTier : DetectTier());
        }

        public DreynoxQualityTier DetectTier()
        {
            if (Application.isMobilePlatform)
                return SystemInfo.systemMemorySize >= 6000 && SystemInfo.graphicsMemorySize >= 1500
                    ? DreynoxQualityTier.MobileHigh : DreynoxQualityTier.MobileLow;
            return SystemInfo.systemMemorySize >= 16000 && SystemInfo.graphicsMemorySize >= 6000
                ? DreynoxQualityTier.DesktopUltra : DreynoxQualityTier.DesktopBalanced;
        }

        public void Apply(DreynoxQualityTier tier)
        {
            ActiveTier = tier;
            switch (tier)
            {
                case DreynoxQualityTier.MobileLow:
                    Application.targetFrameRate = 60;
                    QualitySettings.vSyncCount = 0;
                    QualitySettings.shadowDistance = 35f;
                    QualitySettings.lodBias = 0.7f;
                    QualitySettings.antiAliasing = 0;
                    break;
                case DreynoxQualityTier.MobileHigh:
                    Application.targetFrameRate = 60;
                    QualitySettings.vSyncCount = 0;
                    QualitySettings.shadowDistance = 65f;
                    QualitySettings.lodBias = 1f;
                    QualitySettings.antiAliasing = 2;
                    break;
                case DreynoxQualityTier.DesktopUltra:
                    Application.targetFrameRate = 144;
                    QualitySettings.vSyncCount = 0;
                    QualitySettings.shadowDistance = 180f;
                    QualitySettings.lodBias = 2f;
                    QualitySettings.antiAliasing = 4;
                    break;
                default:
                    Application.targetFrameRate = 120;
                    QualitySettings.vSyncCount = 0;
                    QualitySettings.shadowDistance = 110f;
                    QualitySettings.lodBias = 1.35f;
                    QualitySettings.antiAliasing = 2;
                    break;
            }
        }
    }
}
