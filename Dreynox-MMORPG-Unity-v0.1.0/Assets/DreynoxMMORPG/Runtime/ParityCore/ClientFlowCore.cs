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
        EnteringWorld,
        InWorld,
        Disconnected
    }

    public sealed class CharacterSummaryCore
    {
        public long CharacterId { get; }
        public string Name { get; }
        public int Level { get; }
        public int Slot { get; }
        public CharacterSummaryCore(long characterId, string name, int level, int slot)
        {
            CharacterId = characterId;
            Name = name ?? string.Empty;
            Level = level;
            Slot = slot;
        }
    }

    public sealed class ClientFlowCore
    {
        private readonly List<CharacterSummaryCore> _characters = new List<CharacterSummaryCore>();
        public ClientFlowState State { get; private set; } = ClientFlowState.Boot;
        public IReadOnlyList<CharacterSummaryCore> Characters => _characters;
        public int? SelectedServerId { get; private set; }
        public long? SelectedCharacterId { get; private set; }
        public string DisconnectReason { get; private set; }

        public bool ReadyForLogin()
        {
            if (State != ClientFlowState.Boot && State != ClientFlowState.Disconnected) return false;
            State = ClientFlowState.Login;
            DisconnectReason = null;
            return true;
        }

        public bool BeginConnect()
        {
            if (State != ClientFlowState.Login) return false;
            State = ClientFlowState.Connecting;
            return true;
        }

        public bool LoginAccepted()
        {
            if (State != ClientFlowState.Connecting) return false;
            State = ClientFlowState.ServerSelect;
            return true;
        }

        public bool SelectServer(int serverId)
        {
            if (State != ClientFlowState.ServerSelect || serverId <= 0) return false;
            SelectedServerId = serverId;
            _characters.Clear();
            SelectedCharacterId = null;
            State = ClientFlowState.CharacterSelect;
            return true;
        }

        public bool SetCharacterList(IEnumerable<CharacterSummaryCore> characters)
        {
            if (State != ClientFlowState.CharacterSelect || characters == null) return false;
            _characters.Clear();
            var occupiedSlots = new HashSet<int>();
            var ids = new HashSet<long>();
            foreach (CharacterSummaryCore character in characters)
            {
                if (character == null || character.CharacterId <= 0 || character.Slot < 0 || !occupiedSlots.Add(character.Slot) || !ids.Add(character.CharacterId))
                {
                    _characters.Clear();
                    return false;
                }
                _characters.Add(character);
            }
            return true;
        }

        public bool EnterCharacter(long characterId)
        {
            if (State != ClientFlowState.CharacterSelect) return false;
            bool exists = false;
            for (int i = 0; i < _characters.Count; i++) if (_characters[i].CharacterId == characterId) { exists = true; break; }
            if (!exists) return false;
            SelectedCharacterId = characterId;
            State = ClientFlowState.EnteringWorld;
            return true;
        }

        public bool WorldAccepted()
        {
            if (State != ClientFlowState.EnteringWorld || !SelectedCharacterId.HasValue) return false;
            State = ClientFlowState.InWorld;
            return true;
        }

        public void Disconnect(string reason)
        {
            DisconnectReason = reason ?? string.Empty;
            State = ClientFlowState.Disconnected;
        }

        public bool ReturnToCharacterSelect()
        {
            if (State != ClientFlowState.InWorld || !SelectedServerId.HasValue) return false;
            SelectedCharacterId = null;
            State = ClientFlowState.CharacterSelect;
            return true;
        }
    }
}
