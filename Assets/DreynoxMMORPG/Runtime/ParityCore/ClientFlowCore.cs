using System;
using System.Collections.Generic;

namespace Dreynox.Mmorpg.ParityCore
{
    public enum ClientFlowState
    {
        Boot,
        Login,
        Connecting,
        ServerSelect,
        CharacterSelect,
        CharacterCreate,
        EnteringWorld,
        InWorld,
        Disconnected
    }

    public enum CharacterDifficultyMode
    {
        Basic = 0,
        Ultimate = 1
    }

    public sealed class CharacterSummaryCore
    {
        public long CharacterId { get; }
        public string Name { get; }
        public int Level { get; }
        public int Slot { get; }
        public int Family { get; }
        public int Job { get; }
        public int Sex { get; }
        public int Face { get; }
        public int Hair { get; }
        public int MapId { get; }
        public CharacterDifficultyMode Mode { get; }

        public CharacterSummaryCore(
            long characterId,
            string name,
            int level,
            int slot)
            : this(
                characterId,
                name,
                level,
                slot,
                0,
                0,
                0,
                0,
                0,
                0,
                CharacterDifficultyMode.Basic)
        {
        }

        public CharacterSummaryCore(
            long characterId,
            string name,
            int level,
            int slot,
            int family,
            int job,
            int sex,
            int face,
            int hair,
            int mapId,
            CharacterDifficultyMode mode)
        {
            if (characterId <= 0)
                throw new ArgumentOutOfRangeException(nameof(characterId));
            if (level < 1)
                throw new ArgumentOutOfRangeException(nameof(level));
            if (slot < 0)
                throw new ArgumentOutOfRangeException(nameof(slot));

            ValidateAppearance(family, job, sex, face, hair);

            CharacterId = characterId;
            Name = name ?? string.Empty;
            Level = level;
            Slot = slot;
            Family = family;
            Job = job;
            Sex = sex;
            Face = face;
            Hair = hair;
            MapId = mapId;
            Mode = mode;
        }

        internal static void ValidateAppearance(
            int family,
            int job,
            int sex,
            int face,
            int hair)
        {
            if (family < 0 || family > 3)
                throw new ArgumentOutOfRangeException(nameof(family));
            if (job < 0 || job > 5)
                throw new ArgumentOutOfRangeException(nameof(job));
            if (!LegacyCharacterRigCore.IsJobAllowed(
                    family,
                    job))
            {
                throw new ArgumentException(
                    "Job " + job +
                    " is not available for family " +
                    family + " in ps0032.",
                    nameof(job));
            }
            if (sex < 0 || sex > 1)
                throw new ArgumentOutOfRangeException(nameof(sex));
            if (face < 0 || face > 4)
                throw new ArgumentOutOfRangeException(nameof(face));
            if (hair < 0 || hair > 4)
                throw new ArgumentOutOfRangeException(nameof(hair));
        }
    }

    public readonly struct CharacterCreationRequestCore
    {
        public readonly string Name;
        public readonly int Slot;
        public readonly int Family;
        public readonly int Job;
        public readonly int Sex;
        public readonly int Face;
        public readonly int Hair;
        public readonly CharacterDifficultyMode Mode;

        public CharacterCreationRequestCore(
            string name,
            int slot,
            int family,
            int job,
            int sex,
            int face,
            int hair,
            CharacterDifficultyMode mode)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException(
                    "Character name is required.",
                    nameof(name));

            if (slot < 0)
                throw new ArgumentOutOfRangeException(nameof(slot));

            CharacterSummaryCore.ValidateAppearance(
                family,
                job,
                sex,
                face,
                hair);

            Name = name.Trim();
            Slot = slot;
            Family = family;
            Job = job;
            Sex = sex;
            Face = face;
            Hair = hair;
            Mode = mode;
        }
    }

    public sealed class ClientFlowCore
    {
        private readonly List<CharacterSummaryCore> _characters =
            new List<CharacterSummaryCore>();

        public ClientFlowState State { get; private set; } =
            ClientFlowState.Boot;

        public IReadOnlyList<CharacterSummaryCore> Characters =>
            _characters;

        public int? SelectedServerId { get; private set; }
        public long? SelectedCharacterId { get; private set; }
        public int? CharacterCreateSlot { get; private set; }
        public string DisconnectReason { get; private set; }

        public bool ReadyForLogin()
        {
            if (State != ClientFlowState.Boot &&
                State != ClientFlowState.Disconnected)
                return false;

            State = ClientFlowState.Login;
            DisconnectReason = null;
            return true;
        }

        public bool BeginConnect()
        {
            if (State != ClientFlowState.Login)
                return false;

            State = ClientFlowState.Connecting;
            return true;
        }

        public bool LoginAccepted()
        {
            if (State != ClientFlowState.Connecting)
                return false;

            State = ClientFlowState.ServerSelect;
            return true;
        }

        public bool SelectServer(int serverId)
        {
            if (State != ClientFlowState.ServerSelect ||
                serverId <= 0)
                return false;

            SelectedServerId = serverId;
            _characters.Clear();
            SelectedCharacterId = null;
            CharacterCreateSlot = null;
            State = ClientFlowState.CharacterSelect;
            return true;
        }

        public bool SetCharacterList(
            IEnumerable<CharacterSummaryCore> characters)
        {
            if (State != ClientFlowState.CharacterSelect ||
                characters == null)
                return false;

            var incoming = new List<CharacterSummaryCore>();
            var occupiedSlots = new HashSet<int>();
            var ids = new HashSet<long>();

            foreach (CharacterSummaryCore character in characters)
            {
                if (character == null ||
                    !occupiedSlots.Add(character.Slot) ||
                    !ids.Add(character.CharacterId))
                    return false;

                incoming.Add(character);
            }

            incoming.Sort(
                (left, right) => left.Slot.CompareTo(right.Slot));

            _characters.Clear();
            _characters.AddRange(incoming);
            return true;
        }

        public bool BeginCharacterCreate(int slot, int maxSlots = 5)
        {
            if (State != ClientFlowState.CharacterSelect ||
                slot < 0 ||
                slot >= maxSlots)
                return false;

            for (int i = 0; i < _characters.Count; i++)
            {
                if (_characters[i].Slot == slot)
                    return false;
            }

            CharacterCreateSlot = slot;
            State = ClientFlowState.CharacterCreate;
            return true;
        }

        public bool CancelCharacterCreate()
        {
            if (State != ClientFlowState.CharacterCreate ||
                !SelectedServerId.HasValue)
                return false;

            CharacterCreateSlot = null;
            State = ClientFlowState.CharacterSelect;
            return true;
        }

        public bool CharacterCreated(CharacterSummaryCore character)
        {
            if (State != ClientFlowState.CharacterCreate ||
                character == null ||
                !CharacterCreateSlot.HasValue ||
                character.Slot != CharacterCreateSlot.Value)
                return false;

            for (int i = 0; i < _characters.Count; i++)
            {
                if (_characters[i].CharacterId == character.CharacterId ||
                    _characters[i].Slot == character.Slot)
                    return false;
            }

            _characters.Add(character);
            _characters.Sort(
                (left, right) => left.Slot.CompareTo(right.Slot));

            CharacterCreateSlot = null;
            State = ClientFlowState.CharacterSelect;
            return true;
        }

        public bool CharacterDeleted(long characterId)
        {
            if (State != ClientFlowState.CharacterSelect)
                return false;

            for (int i = 0; i < _characters.Count; i++)
            {
                if (_characters[i].CharacterId != characterId)
                    continue;

                _characters.RemoveAt(i);

                if (SelectedCharacterId == characterId)
                    SelectedCharacterId = null;

                return true;
            }

            return false;
        }

        public bool EnterCharacter(long characterId)
        {
            if (State != ClientFlowState.CharacterSelect)
                return false;

            bool exists = false;

            for (int i = 0; i < _characters.Count; i++)
            {
                if (_characters[i].CharacterId == characterId)
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
                return false;

            SelectedCharacterId = characterId;
            State = ClientFlowState.EnteringWorld;
            return true;
        }

        public bool WorldAccepted()
        {
            if (State != ClientFlowState.EnteringWorld ||
                !SelectedCharacterId.HasValue)
                return false;

            State = ClientFlowState.InWorld;
            return true;
        }

        public void Disconnect(string reason)
        {
            DisconnectReason = reason ?? string.Empty;
            CharacterCreateSlot = null;
            State = ClientFlowState.Disconnected;
        }

        public bool ReturnToCharacterSelect()
        {
            if (State != ClientFlowState.InWorld ||
                !SelectedServerId.HasValue)
                return false;

            SelectedCharacterId = null;
            CharacterCreateSlot = null;
            State = ClientFlowState.CharacterSelect;
            return true;
        }
    }
}
