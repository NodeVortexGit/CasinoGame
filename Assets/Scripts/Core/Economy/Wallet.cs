using System;

namespace Casino.Core.Economy
{
    public interface IChipBank
    {
        long Balance { get; }
        bool TryDebit(long amount);
        void Credit(long amount);
    }

    public sealed class Wallet : IChipBank
    {
        public Wallet(long balance)
        {
            if (balance < 0) throw new ArgumentOutOfRangeException(nameof(balance));
            Balance = balance;
        }

        public long Balance { get; private set; }

        /// <summary>(new balance, delta)</summary>
        public event Action<long, long> Changed;

        public bool TryDebit(long amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (amount > Balance) return false;
            if (amount == 0) return true;
            Balance -= amount;
            Changed?.Invoke(Balance, -amount);
            return true;
        }

        public void Credit(long amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (amount == 0) return;
            Balance = checked(Balance + amount);
            Changed?.Invoke(Balance, amount);
        }
    }
}
