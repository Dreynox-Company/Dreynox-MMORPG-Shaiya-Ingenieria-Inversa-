using System;
using System.Collections.Generic;

namespace Dreynox.Mmorpg.ParityCore
{
    public enum QuestState { Available, Active, Completed, Rewarded, Failed }

    public sealed class QuestObjectiveState
    {
        public int ObjectiveId { get; }
        public int Required { get; }
        public int Current { get; private set; }
        public bool Complete => Current >= Required;

        public QuestObjectiveState(int objectiveId, int required)
        {
            if (required <= 0) throw new ArgumentOutOfRangeException(nameof(required));
            ObjectiveId = objectiveId;
            Required = required;
        }

        public void Add(int amount)
        {
            if (amount <= 0) return;
            Current = Math.Min(Required, Current + amount);
        }
    }

    public sealed class QuestCore
    {
        private readonly Dictionary<int, QuestObjectiveState> _objectives = new Dictionary<int, QuestObjectiveState>();
        public int QuestId { get; }
        public QuestState State { get; private set; } = QuestState.Available;
        public IReadOnlyDictionary<int, QuestObjectiveState> Objectives => _objectives;

        public QuestCore(int questId) { QuestId = questId; }
        public void AddObjective(int objectiveId, int required) => _objectives.Add(objectiveId, new QuestObjectiveState(objectiveId, required));
        public bool Accept() { if (State != QuestState.Available) return false; State = QuestState.Active; return true; }

        public bool Progress(int objectiveId, int amount)
        {
            if (State != QuestState.Active || !_objectives.TryGetValue(objectiveId, out QuestObjectiveState objective)) return false;
            objective.Add(amount);
            bool all = _objectives.Count > 0;
            foreach (QuestObjectiveState o in _objectives.Values) if (!o.Complete) { all = false; break; }
            if (all) State = QuestState.Completed;
            return true;
        }

        public bool Reward()
        {
            if (State != QuestState.Completed) return false;
            State = QuestState.Rewarded;
            return true;
        }
    }

    public sealed class ShopCore
    {
        private readonly Dictionary<int, long> _buyPrices = new Dictionary<int, long>();
        private readonly Dictionary<int, long> _sellPrices = new Dictionary<int, long>();

        public void SetPrice(int itemId, long buyPrice, long sellPrice)
        {
            if (itemId <= 0 || buyPrice < 0 || sellPrice < 0) throw new ArgumentOutOfRangeException();
            _buyPrices[itemId] = buyPrice;
            _sellPrices[itemId] = sellPrice;
        }

        public bool TryBuy(InventoryCore inventory, int itemId, int quantity, int maxStack, ref long gold)
        {
            if (inventory == null || quantity <= 0 || !_buyPrices.TryGetValue(itemId, out long price)) return false;
            long cost = checked(price * quantity);
            if (gold < cost) return false;
            int before = inventory.CountItem(itemId);
            int remaining = inventory.Add(itemId, quantity, maxStack);
            int accepted = quantity - remaining;
            if (accepted <= 0) return false;
            gold -= price * accepted;
            return inventory.CountItem(itemId) == before + accepted;
        }

        public bool TrySell(InventoryCore inventory, int itemId, int quantity, ref long gold)
        {
            if (inventory == null || quantity <= 0 || !_sellPrices.TryGetValue(itemId, out long price)) return false;
            if (!inventory.Remove(itemId, quantity)) return false;
            gold = checked(gold + price * quantity);
            return true;
        }
    }

    public sealed class GatekeeperDestination
    {
        public int DestinationId { get; set; }
        public int MapId { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public int MinimumLevel { get; set; }
        public long Cost { get; set; }
    }

    public sealed class GatekeeperCore
    {
        private readonly Dictionary<int, GatekeeperDestination> _destinations = new Dictionary<int, GatekeeperDestination>();
        public void Add(GatekeeperDestination destination) => _destinations[destination.DestinationId] = destination;

        public bool TryResolve(int destinationId, int level, ref long gold, out GatekeeperDestination destination)
        {
            if (!_destinations.TryGetValue(destinationId, out destination)) return false;
            if (level < destination.MinimumLevel || gold < destination.Cost) return false;
            gold -= destination.Cost;
            return true;
        }
    }

    public sealed class BlacksmithUpgradeProfile
    {
        public int MaxLevel { get; set; } = 20;
        public Func<int, long> CostForNextLevel { get; set; }
        public Func<int, double> SuccessProbabilityForNextLevel { get; set; }
    }

    public sealed class BlacksmithCore
    {
        private readonly BlacksmithUpgradeProfile _profile;
        public BlacksmithCore(BlacksmithUpgradeProfile profile) => _profile = profile ?? throw new ArgumentNullException(nameof(profile));

        public bool TryUpgrade(ref int level, ref long gold, double roll01)
        {
            if (level < 0 || level >= _profile.MaxLevel || roll01 < 0 || roll01 > 1) return false;
            if (_profile.CostForNextLevel == null || _profile.SuccessProbabilityForNextLevel == null) throw new InvalidOperationException("Blacksmith profile incomplete.");
            long cost = _profile.CostForNextLevel(level + 1);
            if (cost < 0 || gold < cost) return false;
            gold -= cost;
            double chance = _profile.SuccessProbabilityForNextLevel(level + 1);
            if (chance < 0 || chance > 1) throw new InvalidOperationException("Invalid blacksmith success probability.");
            if (roll01 > chance) return false;
            level++;
            return true;
        }
    }
}
