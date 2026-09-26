using System;
using System.Collections.Generic;
using Dreynox.Mmorpg.ParityCore;
using UnityEngine;
using UnityEngine.UI;

namespace Dreynox.Mmorpg.UI
{
    public sealed class LegacyCharacterSelectScreenController : MonoBehaviour
    {
        [SerializeField] private Button[] slotButtons = Array.Empty<Button>();
        [SerializeField] private Text[] slotNameTexts = Array.Empty<Text>();
        [SerializeField] private Text[] slotMetaTexts = Array.Empty<Text>();
        [SerializeField] private Button startButton;
        [SerializeField] private Button createButton;
        [SerializeField] private Button deleteButton;
        [SerializeField] private Text statusText;

        private readonly Dictionary<int, CharacterSummaryCore> _bySlot =
            new Dictionary<int, CharacterSummaryCore>();

        private int _selectedSlot = -1;
        private long? _selectedCharacterId;

        public int SelectedSlot => _selectedSlot;
        public long? SelectedCharacterId => _selectedCharacterId;

        public event Action<int> EmptySlotRequested;
        public event Action<CharacterSummaryCore> CharacterSelected;
        public event Action<long> EnterRequested;
        public event Action<long> DeleteRequested;

        private void Awake()
        {
            HookButtons();
            RefreshActionButtons();
        }

        private void OnDestroy()
        {
            UnhookButtons();
        }

        public void Bind(
            Button[] slots,
            Text[] names,
            Text[] metas,
            Button start,
            Button create,
            Button delete,
            Text status)
        {
            UnhookButtons();

            slotButtons = slots ?? Array.Empty<Button>();
            slotNameTexts = names ?? Array.Empty<Text>();
            slotMetaTexts = metas ?? Array.Empty<Text>();
            startButton = start;
            createButton = create;
            deleteButton = delete;
            statusText = status;

            HookButtons();
            RefreshViews();
        }

        public void SetCharacters(
            IReadOnlyList<CharacterSummaryCore> characters)
        {
            _bySlot.Clear();

            if (characters != null)
            {
                for (int i = 0; i < characters.Count; i++)
                {
                    CharacterSummaryCore character = characters[i];
                    if (character != null)
                        _bySlot[character.Slot] = character;
                }
            }

            if (_selectedSlot >= 0 &&
                !_bySlot.ContainsKey(_selectedSlot))
            {
                _selectedCharacterId = null;
            }

            RefreshViews();
        }

        public void SelectSlot(int slot)
        {
            if (slot < 0 || slot >= slotButtons.Length)
                return;

            _selectedSlot = slot;

            CharacterSummaryCore character;
            if (_bySlot.TryGetValue(slot, out character))
            {
                _selectedCharacterId = character.CharacterId;
                CharacterSelected?.Invoke(character);
                SetStatus(
                    character.Name +
                    " · Lv." + character.Level);
            }
            else
            {
                _selectedCharacterId = null;
                SetStatus("Empty character slot " + (slot + 1) + ".");
            }

            RefreshViews();
        }

        public void RequestPrimaryAction()
        {
            if (_selectedSlot < 0)
            {
                SetStatus("Select a character slot.");
                return;
            }

            if (_selectedCharacterId.HasValue)
            {
                EnterRequested?.Invoke(_selectedCharacterId.Value);
                return;
            }

            EmptySlotRequested?.Invoke(_selectedSlot);
        }

        public void RequestCreate()
        {
            if (_selectedSlot < 0)
            {
                int empty = FindFirstEmptySlot();
                if (empty < 0)
                {
                    SetStatus("No empty character slots.");
                    return;
                }

                _selectedSlot = empty;
            }

            if (_bySlot.ContainsKey(_selectedSlot))
            {
                SetStatus("Select an empty slot to create a character.");
                return;
            }

            EmptySlotRequested?.Invoke(_selectedSlot);
        }

        public void RequestDelete()
        {
            if (!_selectedCharacterId.HasValue)
            {
                SetStatus("Select a character to delete.");
                return;
            }

            DeleteRequested?.Invoke(_selectedCharacterId.Value);
        }

        public void SetStatus(string value)
        {
            if (statusText != null)
                statusText.text = value ?? string.Empty;
        }

        private void HookButtons()
        {
            for (int i = 0; i < slotButtons.Length; i++)
            {
                Button button = slotButtons[i];
                if (button == null)
                    continue;

                int captured = i;
                button.onClick.AddListener(
                    () => SelectSlot(captured));
            }

            if (startButton != null)
                startButton.onClick.AddListener(RequestPrimaryAction);

            if (createButton != null)
                createButton.onClick.AddListener(RequestCreate);

            if (deleteButton != null)
                deleteButton.onClick.AddListener(RequestDelete);
        }

        private void UnhookButtons()
        {
            for (int i = 0; i < slotButtons.Length; i++)
            {
                if (slotButtons[i] != null)
                    slotButtons[i].onClick.RemoveAllListeners();
            }

            if (startButton != null)
                startButton.onClick.RemoveListener(RequestPrimaryAction);

            if (createButton != null)
                createButton.onClick.RemoveListener(RequestCreate);

            if (deleteButton != null)
                deleteButton.onClick.RemoveListener(RequestDelete);
        }

        private void RefreshViews()
        {
            for (int slot = 0; slot < slotButtons.Length; slot++)
            {
                CharacterSummaryCore character;
                bool occupied = _bySlot.TryGetValue(slot, out character);

                if (slot < slotNameTexts.Length &&
                    slotNameTexts[slot] != null)
                {
                    slotNameTexts[slot].text =
                        occupied
                            ? character.Name
                            : string.Empty;
                }

                if (slot < slotMetaTexts.Length &&
                    slotMetaTexts[slot] != null)
                {
                    Text meta =
                        slotMetaTexts[slot];

                    meta.alignment =
                        occupied
                            ? TextAnchor.UpperLeft
                            : TextAnchor.MiddleCenter;

                    meta.text =
                        occupied
                            ? "Lv. " + character.Level +
                              "                                      " +
                              JobLabel(
                                  character.Family,
                                  character.Job) +
                              "\n\nLast Location : " +
                              MapLabel(character.MapId) +
                              "\n\nMode : " +
                              character.Mode
                                  .ToString()
                                  .ToUpperInvariant()
                            : "Please create a character.";
                }

                if (slotButtons[slot] != null)
                {
                    ColorBlock colors =
                        slotButtons[slot].colors;

                    colors.normalColor =
                        slot == _selectedSlot
                            ? new Color(1f, 0.88f, 0.55f, 1f)
                            : Color.white;

                    slotButtons[slot].colors = colors;
                }
            }

            RefreshActionButtons();
        }

        private void RefreshActionButtons()
        {
            bool hasSelection = _selectedSlot >= 0;
            bool occupied = _selectedCharacterId.HasValue;

            if (startButton != null)
                startButton.interactable = hasSelection;

            if (createButton != null)
                createButton.interactable = hasSelection && !occupied;

            if (deleteButton != null)
                deleteButton.interactable = occupied;
        }

        private static string JobLabel(
            int family,
            int job)
        {
            if (family < 0 ||
                family > 3 ||
                job < 0 ||
                job > 5)
            {
                return "Job " + job;
            }

            return LegacyCharacterRigCore
                .ResolveDisplayJob(
                    family,
                    job);
        }

        private static string MapLabel(
            int mapId)
        {
            switch (mapId)
            {
                case 0: return "Apulune";
                case 1: return "Erina";
                default: return "Map " + mapId;
            }
        }

        private int FindFirstEmptySlot()
        {
            for (int slot = 0; slot < slotButtons.Length; slot++)
            {
                if (!_bySlot.ContainsKey(slot))
                    return slot;
            }

            return -1;
        }
    }
}
