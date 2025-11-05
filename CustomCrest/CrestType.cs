namespace CustomCrestMod
{
    public enum CrestType
    {
        Hunter,      // Internal name: "Default" or might not have a ConfigGroup
        Reaper,      // Internal name: "Scythe"
        Wanderer,
        Warrior,
        Witch,
        Toolmaster,
        Spell        // Internal name: "Shaman"
    }

    public static class CrestTypeExtensions
    {
        /// <summary>
        /// Gets the internal game name for this crest type.
        /// Some crests have different names in the enum vs in-game.
        /// </summary>
        public static string GetInternalName(this CrestType crestType)
        {
            switch (crestType)
            {
                case CrestType.Spell:
                    return "Shaman";
                case CrestType.Hunter:
                    return "Default";
                case CrestType.Reaper:
                    return "Scythe";
                default:
                    return crestType.ToString();
            }
        }
    }
}