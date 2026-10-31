// Copyright (c) Zorkats. All rights reserved.

using CrestComposition;

namespace CustomCrestMod
{
    public static class AnimationLibraryHelper
    {
        public static void RefreshAnimationLookup(tk2dSpriteAnimation? animLib)
        {
            if (animLib == null)
            {
                return;
            }

            AnimationLibraryLookup.Refresh(animLib);
            Plugin.ModLogger?.LogInfo("Refreshed animation library lookup table");
        }
    }
}