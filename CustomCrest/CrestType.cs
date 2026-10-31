// Copyright (c) Zorkats. All rights reserved.

using CrestComposition;

namespace CustomCrestMod
{
    public enum CrestType
    {
        Hunter,
        Reaper,
        Wanderer,
        Warrior,
        Witch,
        Toolmaster,
        Spell
    }

    public static class CrestTypeExtensions
    {
        public static string GetInternalName(this CrestType crestType)
        {
            VanillaCrestInfo info = VanillaCrestCatalog.Get(crestType.ToVanillaCrestType());
            return info.ActiveRootName;
        }

        public static VanillaCrestType ToVanillaCrestType(this CrestType crestType)
        {
            switch (crestType)
            {
                case CrestType.Hunter:
                    return VanillaCrestType.Hunter;
                case CrestType.Reaper:
                    return VanillaCrestType.Reaper;
                case CrestType.Wanderer:
                    return VanillaCrestType.Wanderer;
                case CrestType.Warrior:
                    return VanillaCrestType.Warrior;
                case CrestType.Witch:
                    return VanillaCrestType.Witch;
                case CrestType.Toolmaster:
                    return VanillaCrestType.Toolmaster;
                case CrestType.Spell:
                    return VanillaCrestType.Spell;
                default:
                    return VanillaCrestType.Hunter;
            }
        }
    }
}