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
        Crafting,
        Pyro,
        Cooking
    }

    public static class SkillInfo
    {
        public const int Count = 12;

        public static string Name(Skill skill)
        {
            return skill.ToString();
        }
    }
}
