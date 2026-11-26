namespace RPG.Stats
{
    public enum CombatStyle
    {
        Melee,
        Ranged,
        Magic
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
