using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace CustomCrestMod
{
    public static class AnimationLibraryHelper
    {
        private static readonly FieldInfo _isValidField = AccessTools.Field(typeof(tk2dSpriteAnimation), "isValid");
        private static readonly FieldInfo _lookupField = AccessTools.Field(typeof(tk2dSpriteAnimation), "lookup");
        private static readonly Type _animationInfoType = typeof(tk2dSpriteAnimation).GetNestedType("AnimationInfo", BindingFlags.NonPublic);

        public static void RefreshAnimationLookup(tk2dSpriteAnimation animLib)
        {
            if (animLib == null)
                return;

            // Create a new empty dictionary<string, AnimationInfo>
            var dictType = typeof(Dictionary<,>).MakeGenericType(typeof(string), _animationInfoType);
            var newLookup = Activator.CreateInstance(dictType);

            // Assign and reset
            _lookupField.SetValue(animLib, newLookup);
            _isValidField.SetValue(animLib, false);
            animLib.ValidateLookup();

            Plugin.ModLogger.LogInfo("Refreshed animation library lookup table");
        }
    }
}