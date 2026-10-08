using System;

namespace Betaknight.Core.Run
{
    /// <summary>
    /// Werte des Betaknight während eines Runs: Leben, Gold und Runensplitter.
    /// Ändert sich etwas, wird <see cref="Changed"/> ausgelöst.
    /// </summary>
    public sealed class PlayerStats
    {
        public int MaxHp { get; private set; }
        public int Hp { get; private set; }
        public int Gold { get; private set; }

        /// <summary>Runensplitter, Währung für Runenwahlen.</summary>
        public int Shards { get; private set; }

        public bool IsDead => Hp <= 0;

        public event Action Changed;

        public PlayerStats(int maxHp = 30, int gold = 5, int shards = 0)
        {
            if (maxHp < 1) throw new ArgumentOutOfRangeException(nameof(maxHp));
            if (gold < 0) throw new ArgumentOutOfRangeException(nameof(gold));
            if (shards < 0) throw new ArgumentOutOfRangeException(nameof(shards));
            MaxHp = maxHp;
            Hp = maxHp;
            Gold = gold;
            Shards = shards;
        }

        /// <summary>Heilt bis höchstens MaxHp. Gibt die tatsächlich geheilte Menge zurück.</summary>
        public int Heal(int amount)
        {
            if (amount <= 0) return 0;
            int healed = Math.Min(amount, MaxHp - Hp);
            if (healed > 0) { Hp += healed; Changed?.Invoke(); }
            return healed;
        }

        /// <summary>
        /// Zieht Leben ab. Mit <paramref name="lethal"/> = false bleibt mindestens 1 HP übrig
        /// (Oberwelt-Events töten nie, nur Kämpfe). Gibt den tatsächlichen Schaden zurück.
        /// </summary>
        public int Damage(int amount, bool lethal = true)
        {
            if (amount <= 0) return 0;
            int floor = lethal ? 0 : 1;
            int dealt = Math.Max(0, Math.Min(amount, Hp - floor));
            if (dealt > 0) { Hp -= dealt; Changed?.Invoke(); }
            return dealt;
        }

        public void RaiseMaxHp(int amount)
        {
            if (amount <= 0) return;
            MaxHp += amount;
            Hp += amount;
            Changed?.Invoke();
        }

        public void AddGold(int amount)
        {
            if (amount <= 0) return;
            Gold += amount;
            Changed?.Invoke();
        }

        public bool TrySpendGold(int amount)
        {
            if (amount < 0 || amount > Gold) return false;
            if (amount == 0) return true;
            Gold -= amount;
            Changed?.Invoke();
            return true;
        }

        public void AddShards(int amount)
        {
            if (amount <= 0) return;
            Shards += amount;
            Changed?.Invoke();
        }

        public bool TrySpendShards(int amount)
        {
            if (amount < 0 || amount > Shards) return false;
            if (amount == 0) return true;
            Shards -= amount;
            Changed?.Invoke();
            return true;
        }
    }
}
