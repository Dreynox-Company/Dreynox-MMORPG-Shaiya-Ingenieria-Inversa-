using System;
using System.Collections.Generic;
using System.Linq;

namespace Dreynox.Mmorpg.ParityCore
{
    public enum TradePhase { None, Open, Locked, Completed, Cancelled }
    public enum DuelPhase { None, Requested, Countdown, Active, Finished, Cancelled }

    public sealed class PartyCore
    {
        private readonly List<int> _members = new List<int>();
        public int Capacity { get; }
        public int? LeaderId { get; private set; }
        public IReadOnlyList<int> Members => _members;

        public PartyCore(int capacity = 7)
        {
            if (capacity < 2) throw new ArgumentOutOfRangeException(nameof(capacity));
            Capacity = capacity;
        }

        public bool Create(int leaderId)
        {
            if (_members.Count != 0) return false;
            LeaderId = leaderId;
            _members.Add(leaderId);
            return true;
        }

        public bool Add(int memberId)
        {
            if (_members.Count == 0 || _members.Count >= Capacity || _members.Contains(memberId)) return false;
            _members.Add(memberId);
            return true;
        }

        public bool Remove(int memberId)
        {
            if (!_members.Remove(memberId)) return false;
            if (LeaderId == memberId) LeaderId = _members.Count == 0 ? null : _members[0];
            return true;
        }
    }

    public sealed class TradeCore
    {
        private readonly Dictionary<int, int> _leftItems = new Dictionary<int, int>();
        private readonly Dictionary<int, int> _rightItems = new Dictionary<int, int>();
        private bool _leftConfirmed;
        private bool _rightConfirmed;
        public int LeftPlayerId { get; private set; }
        public int RightPlayerId { get; private set; }
        public TradePhase Phase { get; private set; }
        public IReadOnlyDictionary<int, int> LeftItems => _leftItems;
        public IReadOnlyDictionary<int, int> RightItems => _rightItems;

        public void Open(int leftPlayerId, int rightPlayerId)
        {
            if (leftPlayerId == rightPlayerId) throw new ArgumentException("Trade requires two distinct players.");
            LeftPlayerId = leftPlayerId;
            RightPlayerId = rightPlayerId;
            _leftItems.Clear();
            _rightItems.Clear();
            _leftConfirmed = _rightConfirmed = false;
            Phase = TradePhase.Open;
        }

        public bool SetItem(int playerId, int itemId, int quantity)
        {
            if (Phase != TradePhase.Open && Phase != TradePhase.Locked) return false;
            if (quantity < 0) return false;
            Dictionary<int, int> target = playerId == LeftPlayerId ? _leftItems : playerId == RightPlayerId ? _rightItems : null;
            if (target == null) return false;
            if (quantity == 0) target.Remove(itemId); else target[itemId] = quantity;
            _leftConfirmed = _rightConfirmed = false;
            Phase = TradePhase.Open;
            return true;
        }

        public bool Confirm(int playerId)
        {
            if (Phase != TradePhase.Open && Phase != TradePhase.Locked) return false;
            if (playerId == LeftPlayerId) _leftConfirmed = true;
            else if (playerId == RightPlayerId) _rightConfirmed = true;
            else return false;
            Phase = _leftConfirmed && _rightConfirmed ? TradePhase.Completed : TradePhase.Locked;
            return true;
        }

        public void Cancel()
        {
            if (Phase == TradePhase.Completed) return;
            Phase = TradePhase.Cancelled;
        }
    }

    public sealed class DuelCore
    {
        public int ChallengerId { get; private set; }
        public int OpponentId { get; private set; }
        public DuelPhase Phase { get; private set; }
        public double CountdownRemaining { get; private set; }
        public int? WinnerId { get; private set; }

        public void Request(int challengerId, int opponentId)
        {
            if (challengerId == opponentId) throw new ArgumentException("Duel requires two distinct players.");
            ChallengerId = challengerId;
            OpponentId = opponentId;
            Phase = DuelPhase.Requested;
            WinnerId = null;
            CountdownRemaining = 0;
        }

        public bool Accept(double countdownSeconds = 3.0)
        {
            if (Phase != DuelPhase.Requested) return false;
            CountdownRemaining = Math.Max(0, countdownSeconds);
            Phase = CountdownRemaining > 0 ? DuelPhase.Countdown : DuelPhase.Active;
            return true;
        }

        public void Tick(double deltaSeconds)
        {
            if (Phase != DuelPhase.Countdown) return;
            CountdownRemaining = Math.Max(0, CountdownRemaining - deltaSeconds);
            if (CountdownRemaining == 0) Phase = DuelPhase.Active;
        }

        public bool Finish(int winnerId)
        {
            if (Phase != DuelPhase.Active || (winnerId != ChallengerId && winnerId != OpponentId)) return false;
            WinnerId = winnerId;
            Phase = DuelPhase.Finished;
            return true;
        }

        public void Cancel()
        {
            if (Phase == DuelPhase.Finished) return;
            Phase = DuelPhase.Cancelled;
        }
    }

    public sealed class RaidCore
    {
        private readonly List<PartyCore> _groups = new List<PartyCore>();
        public int MaxGroups { get; }
        public int PartyCapacity { get; }
        public IReadOnlyList<PartyCore> Groups => _groups;
        public int MemberCount => _groups.Sum(x => x.Members.Count);

        public RaidCore(int maxGroups = 5, int partyCapacity = 7)
        {
            if (maxGroups < 1) throw new ArgumentOutOfRangeException(nameof(maxGroups));
            MaxGroups = maxGroups;
            PartyCapacity = partyCapacity;
        }

        public bool AddGroup(PartyCore party)
        {
            if (party == null || _groups.Count >= MaxGroups || _groups.Contains(party)) return false;
            _groups.Add(party);
            return true;
        }

        public bool ContainsMember(int playerId)
        {
            return _groups.Any(g => g.Members.Contains(playerId));
        }
    }
}
