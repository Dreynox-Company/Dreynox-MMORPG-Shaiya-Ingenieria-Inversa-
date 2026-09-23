using System;
using Dreynox.Mmorpg.ParityCore;
using UnityEngine;
using UnityEngine.UI;

namespace Dreynox.Mmorpg.UI
{
    public sealed class LegacyCharacterMakeScreenController : MonoBehaviour
    {
        [SerializeField] private InputField nameInput;
        [SerializeField] private Text statusText;
        [SerializeField] private Text selectionText;
        [SerializeField] private Button createButton;
        [SerializeField] private Button cancelButton;

        [SerializeField] private int _slot;
        [SerializeField] private int _family;
        [SerializeField] private int _job;
        [SerializeField] private int _sex;
        [SerializeField] private int _face;
        [SerializeField] private int _hair;
        [SerializeField] private CharacterDifficultyMode _mode =
            CharacterDifficultyMode.Basic;

        public int Slot => _slot;
        public int Family => _family;
        public int Job => _job;
        public int Sex => _sex;
        public int Face => _face;
        public int Hair => _hair;
        public CharacterDifficultyMode Mode => _mode;

        public event Action<CharacterCreationRequestCore> CreateRequested;
        public event Action CancelRequested;
        public event Action AppearanceChanged;

        private void Awake()
        {
            HookButtons();
            RefreshSelection();
        }

        private void OnDestroy()
        {
            UnhookButtons();
        }

        public void Bind(
            InputField characterName,
            Text status,
            Text selection,
            Button create,
            Button cancel)
        {
            UnhookButtons();

            nameInput = characterName;
            statusText = status;
            selectionText = selection;
            createButton = create;
            cancelButton = cancel;

            HookButtons();
            RefreshSelection();
        }

        public void Configure(
            int slot,
            int family,
            int job = 0,
            int sex = 0,
            int face = 0,
            int hair = 0,
            CharacterDifficultyMode mode =
                CharacterDifficultyMode.Basic)
        {
            CharacterSummaryCore.ValidateAppearance(
                family,
                job,
                sex,
                face,
                hair);

            if (slot < 0)
                throw new ArgumentOutOfRangeException(nameof(slot));

            _slot = slot;
            _family = family;
            _job = job;
            _sex = sex;
            _face = face;
            _hair = hair;
            _mode = mode;

            RefreshSelection();
            AppearanceChanged?.Invoke();
        }

        public void SelectFamily(int family)
        {
            if (family < 0 || family > 3)
                return;

            _family = family;
            RefreshSelection();
            AppearanceChanged?.Invoke();
        }

        public void SelectJob(int job)
        {
            if (job < 0 || job > 5)
                return;

            _job = job;
            RefreshSelection();
            AppearanceChanged?.Invoke();
        }

        public void SelectSex(int sex)
        {
            if (sex < 0 || sex > 1)
                return;

            _sex = sex;
            RefreshSelection();
            AppearanceChanged?.Invoke();
        }

        public void SelectMode(CharacterDifficultyMode mode)
        {
            _mode = mode;
            RefreshSelection();
            AppearanceChanged?.Invoke();
        }

        public void NextFace()
        {
            _face = (_face + 1) % 5;
            RefreshSelection();
            AppearanceChanged?.Invoke();
        }

        public void PreviousFace()
        {
            _face = (_face + 4) % 5;
            RefreshSelection();
            AppearanceChanged?.Invoke();
        }

        public void NextHair()
        {
            _hair = (_hair + 1) % 5;
            RefreshSelection();
            AppearanceChanged?.Invoke();
        }

        public void PreviousHair()
        {
            _hair = (_hair + 4) % 5;
            RefreshSelection();
            AppearanceChanged?.Invoke();
        }

        public void Submit()
        {
            string characterName =
                nameInput != null
                    ? nameInput.text.Trim()
                    : string.Empty;

            if (characterName.Length < 3)
            {
                SetStatus("Character name must contain at least 3 characters.");
                return;
            }

            try
            {
                CharacterCreationRequestCore request =
                    new CharacterCreationRequestCore(
                        characterName,
                        _slot,
                        _family,
                        _job,
                        _sex,
                        _face,
                        _hair,
                        _mode);

                CreateRequested?.Invoke(request);
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message);
            }
        }

        public void Cancel()
        {
            CancelRequested?.Invoke();
        }

        public void SetStatus(string value)
        {
            if (statusText != null)
                statusText.text = value ?? string.Empty;
        }

        private void RefreshSelection()
        {
            if (selectionText == null)
                return;

            selectionText.text =
                "Family " + _family +
                " · Job " + _job +
                " · " + (_sex == 0 ? "Male" : "Female") +
                " · Face " + (_face + 1) +
                " · Hair " + (_hair + 1) +
                " · " + _mode;
        }

        private void HookButtons()
        {
            if (createButton != null)
                createButton.onClick.AddListener(Submit);

            if (cancelButton != null)
                cancelButton.onClick.AddListener(Cancel);
        }

        private void UnhookButtons()
        {
            if (createButton != null)
                createButton.onClick.RemoveListener(Submit);

            if (cancelButton != null)
                cancelButton.onClick.RemoveListener(Cancel);
        }
    }
}
