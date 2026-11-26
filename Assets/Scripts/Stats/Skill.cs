namespace RPG.Stats
{
    public enum Skill
    {
        Attack,
        Strength,
        Defense,
        Vitality,
        Ranged,
        Magic,
        Woodcutting,
        Mining,
        Smithing,
        Crafting
    }

    public static class SkillInfo
    {
        public const int Count = 10;

        public static string Name(Skill skill)
        {
            return skill.ToString();
        }
    }
}
