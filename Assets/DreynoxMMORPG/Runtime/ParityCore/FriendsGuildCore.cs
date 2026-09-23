using System;
using System.Collections.Generic;

namespace Dreynox.Mmorpg.ParityCore
{
    public sealed class FriendState
    {
        public int PlayerId { get; }
        public bool Online { get; internal set; }
        public FriendState(int playerId, bool online) { PlayerId = playerId; Online = online; }
    }

    public sealed class FriendsCore
    {
        private readonly Dictionary<int, FriendState> _friends = new Dictionary<int, FriendState>();
        private readonly HashSet<int> _incoming = new HashSet<int>();
        private readonly HashSet<int> _outgoing = new HashSet<int>();
        public IReadOnlyDictionary<int, FriendState> Friends => _friends;
        public IReadOnlyCollection<int> IncomingRequests => _incoming;
        public IReadOnlyCollection<int> OutgoingRequests => _outgoing;

        public bool SendRequest(int playerId)
        {
            if (playerId <= 0 || _friends.ContainsKey(playerId) || _outgoing.Contains(playerId)) return false;
            _outgoing.Add(playerId);
            return true;
        }

        public bool ReceiveRequest(int playerId)
        {
            if (playerId <= 0 || _friends.ContainsKey(playerId) || _incoming.Contains(playerId)) return false;
            _incoming.Add(playerId);
            return true;
        }

        public bool AcceptIncoming(int playerId, bool online = false)
        {
            if (!_incoming.Remove(playerId)) return false;
            _outgoing.Remove(playerId);
            _friends[playerId] = new FriendState(playerId, online);
            return true;
        }

        public bool ConfirmOutgoing(int playerId, bool online = false)
        {
            if (!_outgoing.Remove(playerId)) return false;
            _incoming.Remove(playerId);
            _friends[playerId] = new FriendState(playerId, online);
            return true;
        }

        public bool Remove(int playerId) => _friends.Remove(playerId);

        public bool SetOnline(int playerId, bool online)
        {
            if (!_friends.TryGetValue(playerId, out FriendState state)) return false;
            state.Online = online;
            return true;
        }
    }

    public enum GuildRoleCore { Member, Officer, Leader }

    public sealed class GuildMemberCore
    {
        public int PlayerId { get; }
        public GuildRoleCore Role { get; internal set; }
        public GuildMemberCore(int playerId, GuildRoleCore role) { PlayerId = playerId; Role = role; }
    }

    public sealed class GuildCore
    {
        private readonly Dictionary<int, GuildMemberCore> _members = new Dictionary<int, GuildMemberCore>();
        public string Name { get; }
        public int Capacity { get; }
        public IReadOnlyDictionary<int, GuildMemberCore> Members => _members;
        public int? LeaderId { get; private set; }

        public GuildCore(string name, int capacity)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException(nameof(name));
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            Name = name;
            Capacity = capacity;
        }

        public bool Create(int leaderId)
        {
            if (leaderId <= 0 || _members.Count != 0) return false;
            LeaderId = leaderId;
            _members.Add(leaderId, new GuildMemberCore(leaderId, GuildRoleCore.Leader));
            return true;
        }

        public bool AddMember(int actorId, int playerId)
        {
            if (!CanManage(actorId) || playerId <= 0 || _members.Count >= Capacity || _members.ContainsKey(playerId)) return false;
            _members[playerId] = new GuildMemberCore(playerId, GuildRoleCore.Member);
            return true;
        }

        public bool SetOfficer(int actorId, int playerId, bool officer)
        {
            if (LeaderId != actorId || !_members.TryGetValue(playerId, out GuildMemberCore member) || playerId == LeaderId) return false;
            member.Role = officer ? GuildRoleCore.Officer : GuildRoleCore.Member;
            return true;
        }

        public bool TransferLeadership(int actorId, int newLeaderId)
        {
            if (LeaderId != actorId || actorId == newLeaderId || !_members.TryGetValue(newLeaderId, out GuildMemberCore next)) return false;
            _members[actorId].Role = GuildRoleCore.Officer;
            next.Role = GuildRoleCore.Leader;
            LeaderId = newLeaderId;
            return true;
        }

        public bool RemoveMember(int actorId, int playerId)
        {
            if (!_members.ContainsKey(playerId) || playerId == LeaderId) return false;
            if (actorId != playerId && !CanManage(actorId)) return false;
            return _members.Remove(playerId);
        }

        private bool CanManage(int actorId)
        {
            return _members.TryGetValue(actorId, out GuildMemberCore actor) &&
                (actor.Role == GuildRoleCore.Leader || actor.Role == GuildRoleCore.Officer);
        }
    }
}
