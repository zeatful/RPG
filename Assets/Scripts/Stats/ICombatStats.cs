namespace RPG.Stats
{
    public enum CombatStyle
    {
        Melee,
        Ranged,
        Magic
    }

    /// <summary>
    /// The melee stance the player picks. Each trains one skill and swings differently:
    /// Slash (Attack) is quicker but hits lower, Crush (Strength) is slower and hits harder,
    /// Bash (Defense) needs a shield and hits like Slash.
    /// </summary>
    public enum MeleeStance
    {
        Slash,
        Crush,
        Bash
    }

    /// <summary>Animation used for a swing (the "attackKind" animator parameter).</summary>
    public enum AttackAnimation
    {
        Slash = 0,
        Crush = 1,
        Bash = 2,
        Shoot = 3,
        Cast = 4,
        Chop = 5,
        Mine = 6
    }

    /// <summary>Optional: overrides the Fighter's attack speed, max hit and swing animation (the player).</summary>
    public interface IAttackStyle
    {
        float IntervalMultiplier { get; }
        int MaxHit { get; }
        AttackAnimation Animation { get; }
    }

    /// <summary>
    /// Levels and gear bonuses used by combat formulas. Implemented by the player and by enemies.
    /// For ranged/magic, AttackLevel and StrengthLevel return the Ranged/Magic level.
    /// </summary>
    public interface ICombatStats
    {
        int AttackLevel { get; }
        int StrengthLevel { get; }
        int DefenseLevel { get; }
        int AttackBonus { get; }
        int StrengthBonus { get; }
        int DefenseBonus { get; }
        string DisplayName { get; }
        int CombatLevel { get; }

        CombatStyle Style { get; }

        // Attack range in metres; 0 or less means "use the Fighter's default weapon range".
        float AttackRange { get; }

        // Consume one arrow / spark stone if the weapon needs it. False (with a message) if out.
        bool TryUseAmmo(out string failMessage);
    }
}
