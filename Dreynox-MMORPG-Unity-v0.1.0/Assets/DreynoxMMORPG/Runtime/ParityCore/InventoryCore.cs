using System;
using System.Collections.Generic;

namespace Dreynox.Mmorpg.ParityCore
{
    public sealed class ItemStackState
    {
        public int ItemId { get; }
        public int Quantity { get; private set; }
        public int MaxStack { get; }

        public ItemStackState(int itemId, int quantity, int maxStack)
        {
            if (itemId <= 0) throw new ArgumentOutOfRangeException(nameof(itemId));
            if (maxStack <= 0) throw new ArgumentOutOfRangeException(nameof(maxStack));
            if (quantity <= 0 || quantity > maxStack) throw new ArgumentOutOfRangeException(nameof(quantity));
            ItemId = itemId;
            Quantity = quantity;
            MaxStack = maxStack;
        }

        internal int Add(int amount)
        {
            if (amount <= 0) return amount;
            int accepted = Math.Min(amount, MaxStack - Quantity);
            Quantity += accepted;
            return amount - accepted;
        }

        internal int Remove(int amount)
        {
            if (amount <= 0) return 0;
            int removed = Math.Min(amount, Quantity);
            Quantity -= removed;
            return removed;
        }
    }

    public sealed class InventoryCore
    {
        private readonly ItemStackState[] _slots;
        public int Capacity => _slots.Length;
        public IReadOnlyList<ItemStackState> Slots => _slots;

        public InventoryCore(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _slots = new ItemStackState[capacity];
        }

        public int CountItem(int itemId)
        {
            int total = 0;
            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i] != null && _slots[i].ItemId == itemId) total += _slots[i].Quantity;
            return total;
        }

        public int Add(int itemId, int quantity, int maxStack)
        {
            if (quantity <= 0) return quantity;
            int remaining = quantity;
            for (int i = 0; i < _slots.Length && remaining > 0; i++)
            {
                ItemStackState stack = _slots[i];
                if (stack != null && stack.ItemId == itemId && stack.MaxStack == maxStack)
                    remaining = stack.Add(remaining);
            }
            for (int i = 0; i < _slots.Length && remaining > 0; i++)
            {
                if (_slots[i] != null) continue;
                int add = Math.Min(maxStack, remaining);
                _slots[i] = new ItemStackState(itemId, add, maxStack);
                remaining -= add;
            }
            return remaining;
        }

        public bool Remove(int itemId, int quantity)
        {
            if (quantity <= 0 || CountItem(itemId) < quantity) return false;
            int remaining = quantity;
            for (int i = _slots.Length - 1; i >= 0 && remaining > 0; i--)
            {
                ItemStackState stack = _slots[i];
                if (stack == null || stack.ItemId != itemId) continue;
                remaining -= stack.Remove(remaining);
                if (stack.Quantity == 0) _slots[i] = null;
            }
            return remaining == 0;
        }

        public bool Move(int from, int to)
        {
            if (!Valid(from) || !Valid(to) || from == to) return false;
            ItemStackState source = _slots[from];
            if (source == null) return false;
            ItemStackState target = _slots[to];
            if (target == null)
            {
                _slots[to] = source;
                _slots[from] = null;
                return true;
            }
            if (target.ItemId == source.ItemId && target.MaxStack == source.MaxStack)
            {
                int before = source.Quantity;
                int left = target.Add(before);
                int moved = before - left;
                if (moved == 0) return false;
                source.Remove(moved);
                if (source.Quantity == 0) _slots[from] = null;
                return true;
            }
            _slots[to] = source;
            _slots[from] = target;
            return true;
        }

        private bool Valid(int index) => index >= 0 && index < _slots.Length;
    }

    public sealed class WarehouseCore
    {
        public InventoryCore Inventory { get; }
        public long Gold { get; private set; }

        public WarehouseCore(int capacity) => Inventory = new InventoryCore(capacity);

        public bool DepositGold(long amount)
        {
            if (amount <= 0 || Gold > long.MaxValue - amount) return false;
            Gold += amount;
            return true;
        }

        public bool WithdrawGold(long amount)
        {
            if (amount <= 0 || amount > Gold) return false;
            Gold -= amount;
            return true;
        }
    }
}
