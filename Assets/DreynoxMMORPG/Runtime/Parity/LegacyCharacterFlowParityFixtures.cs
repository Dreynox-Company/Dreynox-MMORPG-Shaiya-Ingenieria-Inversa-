using Dreynox.Mmorpg.Gameplay.AnimationSystem;
using Dreynox.Mmorpg.Gameplay.Client;
using Dreynox.Mmorpg.ParityCore;
using Dreynox.Mmorpg.UI;
using UnityEngine;

namespace Dreynox.Mmorpg.Parity
{
    public sealed class LegacyCharacterSelectParityFixture : MonoBehaviour
    {
        [SerializeField] private LegacyCharacterSelectScreenController screen;
        [SerializeField] private SemanticAnimationPlayer previewAnimation;

        public void Bind(
            LegacyCharacterSelectScreenController controller,
            SemanticAnimationPlayer animation)
        {
            screen = controller;
            previewAnimation = animation;
        }

        private void Start()
        {
            if (screen == null)
                return;

            screen.SetCharacters(
                new[]
                {
                    new CharacterSummaryCore(
                        1001,
                        "DreynoxLocal",
                        1,
                        0,
                        0,
                        0,
                        0,
                        0,
                        0,
                        0,
                        CharacterDifficultyMode.Basic)
                });

            screen.SelectSlot(0);
            screen.CharacterSelected += OnSelected;
            screen.EmptySlotRequested += OnEmptySlot;
            screen.EnterRequested += OnEnter;
            screen.DeleteRequested += OnDelete;

            if (previewAnimation != null)
                previewAnimation.PlaySemantic("select");
        }

        private void OnDestroy()
        {
            if (screen == null)
                return;

            screen.CharacterSelected -= OnSelected;
            screen.EmptySlotRequested -= OnEmptySlot;
            screen.EnterRequested -= OnEnter;
            screen.DeleteRequested -= OnDelete;
        }

        private void OnSelected(CharacterSummaryCore character)
        {
            screen.SetStatus(
                character.Name + " · Lv." + character.Level +
                " · family=" + character.Family +
                " job=" + character.Job);
        }

        private void OnEmptySlot(int slot)
        {
            screen.SetStatus(
                "Create requested for slot " + (slot + 1) + ".");
        }

        private void OnEnter(long characterId)
        {
            screen.SetStatus(
                "Enter world requested for character " + characterId + ".");
        }

        private void OnDelete(long characterId)
        {
            screen.SetStatus(
                "Delete requested for character " + characterId + ".");
        }
    }

    public sealed class LegacyCharacterMakeParityFixture : MonoBehaviour
    {
        [SerializeField] private LegacyCharacterMakeScreenController screen;
        [SerializeField] private ShaiyaClientActor actor;
        [SerializeField] private SemanticAnimationPlayer previewAnimation;

        public void Bind(
            LegacyCharacterMakeScreenController controller,
            ShaiyaClientActor previewActor,
            SemanticAnimationPlayer animation)
        {
            screen = controller;
            actor = previewActor;
            previewAnimation = animation;
        }

        private void Start()
        {
            if (screen == null)
                return;

            screen.Configure(
                1,
                0,
                0,
                0,
                0,
                0,
                CharacterDifficultyMode.Basic);

            screen.AppearanceChanged += ApplyAppearance;
            screen.CreateRequested += OnCreate;
            screen.CancelRequested += OnCancel;

            ApplyAppearance();

            if (previewAnimation != null)
                previewAnimation.PlaySemantic("idle");
        }

        private void OnDestroy()
        {
            if (screen == null)
                return;

            screen.AppearanceChanged -= ApplyAppearance;
            screen.CreateRequested -= OnCreate;
            screen.CancelRequested -= OnCancel;
        }

        private void ApplyAppearance()
        {
            if (actor != null)
            {
                actor.ConfigureLegacyIdentity(
                    screen.Family,
                    screen.Job,
                    screen.Sex);
            }
        }

        private void OnCreate(CharacterCreationRequestCore request)
        {
            screen.SetStatus(
                "Create: " + request.Name +
                " · family=" + request.Family +
                " · job=" + request.Job +
                " · sex=" + request.Sex +
                " · face=" + (request.Face + 1) +
                " · hair=" + (request.Hair + 1) +
                " · mode=" + request.Mode + ".");
        }

        private void OnCancel()
        {
            screen.SetStatus("Character creation cancelled.");
        }
    }
}
