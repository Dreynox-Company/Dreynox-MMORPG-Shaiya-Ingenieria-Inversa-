using UnityEngine;

namespace Dreynox.Mmorpg.UI
{
    public sealed class LegacyCharacterMakeTabController : MonoBehaviour
    {
        [SerializeField] private GameObject appearancePanel;

        public bool AppearanceVisible =>
            appearancePanel != null &&
            appearancePanel.activeSelf;

        public void Bind(
            GameObject appearance)
        {
            appearancePanel =
                appearance;

            ShowBasic();
        }

        public void ShowBasic()
        {
            if (appearancePanel != null)
                appearancePanel.SetActive(false);
        }

        public void ShowAppearance()
        {
            if (appearancePanel != null)
                appearancePanel.SetActive(true);
        }
    }
}
