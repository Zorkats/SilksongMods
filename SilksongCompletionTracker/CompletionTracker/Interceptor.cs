using System;
using System.Reflection;
using HarmonyLib;

namespace SilksongMods.CompletionTracker
{
    /// <summary>
    /// A diagnostic tool for data gathering.
    /// This should be disabled or removed from the final release version of the mod.
    /// It logs every time the game checks the status of a persistent item.
    /// </summary>
    public static class Interceptor
    {
        #region Public Methods
        public static void Apply(Harmony harmony)
        {
            try
            {
                Plugin.Log.LogInfo("Attempting to apply interceptor patches for data gathering...");

                ApplyCollectableItemPatch(harmony);
                ApplyPersistentBoolPatch(harmony);
                ApplyQuestPatch(harmony);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"[Interceptor] Failed to apply patches: {e}");
            }
        }
        #endregion

        #region Private Patch Methods
        private static void ApplyCollectableItemPatch(Harmony harmony)
        {
            var addItemMethod = typeof(CollectableItemManager).GetMethod(
                "AddItem",
                BindingFlags.Static | BindingFlags.Public
            );

            if (addItemMethod != null)
            {
                var addItemPrefix = typeof(InterceptorPatches).GetMethod("AddItem_Prefix");
                harmony.Patch(addItemMethod, prefix: new HarmonyMethod(addItemPrefix));
                Plugin.Log.LogInfo("[Interceptor] Successfully applied patch to 'CollectableItemManager.AddItem'.");
            }
            else
            {
                Plugin.Log.LogWarning("[Interceptor] Could not find method 'CollectableItemManager.AddItem'.");
            }
        }

        private static void ApplyPersistentBoolPatch(Harmony harmony)
        {
            var persistentBoolCollectionType = typeof(SceneData).GetNestedType(
                "PersistentBoolCollection",
                BindingFlags.NonPublic
            );

            if (persistentBoolCollectionType == null)
            {
                Plugin.Log.LogError("[Interceptor] Could not find type SceneData+PersistentBoolCollection.");
                return;
            }

            var baseType = persistentBoolCollectionType.BaseType;
            if (baseType == null)
            {
                Plugin.Log.LogError("[Interceptor] Could not find base type for PersistentBoolCollection.");
                return;
            }

            PatchGetValueOrDefault(harmony, baseType);
            PatchTryGetValue(harmony, baseType);
        }

        private static void PatchGetValueOrDefault(Harmony harmony, Type baseType)
        {
            var getDefaultMethod = baseType.GetMethod(
                "GetValueOrDefault",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new Type[] { typeof(string), typeof(string) },
                null
            );

            if (getDefaultMethod != null)
            {
                var getDefaultPrefix = typeof(InterceptorPatches).GetMethod("GetValueOrDefault_Prefix");
                harmony.Patch(getDefaultMethod, prefix: new HarmonyMethod(getDefaultPrefix));
                Plugin.Log.LogInfo("[Interceptor] Successfully applied patch to 'GetValueOrDefault'.");
            }
            else
            {
                Plugin.Log.LogWarning("[Interceptor] Could not find method 'GetValueOrDefault'.");
            }
        }

        private static void PatchTryGetValue(Harmony harmony, Type baseType)
        {
            var tryGetMethod = baseType.GetMethod(
                "TryGetValue",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            );

            if (tryGetMethod != null)
            {
                var tryGetPrefix = typeof(InterceptorPatches).GetMethod("TryGetValue_Prefix");
                harmony.Patch(tryGetMethod, prefix: new HarmonyMethod(tryGetPrefix));
                Plugin.Log.LogInfo("[Interceptor] Successfully applied patch to 'TryGetValue'.");
            }
            else
            {
                Plugin.Log.LogWarning("[Interceptor] Could not find method 'TryGetValue'.");
            }
        }

        private static void ApplyQuestPatch(Harmony harmony)
        {
            // Patch BeginQuest - when a quest is accepted
            var beginQuestMethod = typeof(FullQuestBase).GetMethod(
                "BeginQuest",
                BindingFlags.Instance | BindingFlags.Public
            );

            if (beginQuestMethod != null)
            {
                var beginQuestPostfix = typeof(InterceptorPatches).GetMethod("BeginQuest_Postfix");
                harmony.Patch(beginQuestMethod, postfix: new HarmonyMethod(beginQuestPostfix));
                Plugin.Log.LogInfo("[Interceptor] Successfully applied patch to 'FullQuestBase.BeginQuest'.");
            }
            else
            {
                Plugin.Log.LogWarning("[Interceptor] Could not find method 'FullQuestBase.BeginQuest'.");
            }

            // Patch TryEndQuest - when a quest is completed
            var tryEndQuestMethod = typeof(FullQuestBase).GetMethod(
                "TryEndQuest",
                BindingFlags.Instance | BindingFlags.Public
            );

            if (tryEndQuestMethod != null)
            {
                var tryEndQuestPostfix = typeof(InterceptorPatches).GetMethod("TryEndQuest_Postfix");
                harmony.Patch(tryEndQuestMethod, postfix: new HarmonyMethod(tryEndQuestPostfix));
                Plugin.Log.LogInfo("[Interceptor] Successfully applied patch to 'FullQuestBase.TryEndQuest'.");
            }
            else
            {
                Plugin.Log.LogWarning("[Interceptor] Could not find method 'FullQuestBase.TryEndQuest'.");
            }

            // Patch SubQuest.Get - when subquest objectives are completed
            var subQuestGetMethod = typeof(SubQuest).GetMethod(
                "Get",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new Type[] { typeof(bool) },
                null
            );

            if (subQuestGetMethod != null)
            {
                var subQuestGetPostfix = typeof(InterceptorPatches).GetMethod("SubQuestGet_Postfix");
                harmony.Patch(subQuestGetMethod, postfix: new HarmonyMethod(subQuestGetPostfix));
                Plugin.Log.LogInfo("[Interceptor] Successfully applied patch to 'SubQuest.Get'.");
            }
            else
            {
                Plugin.Log.LogWarning("[Interceptor] Could not find method 'SubQuest.Get'.");
            }
        }
        #endregion
    }

    #region Interceptor Patches
    /// <summary>
    /// Contains the prefix and postfix methods that Harmony will execute for intercepting game calls.
    /// </summary>
    public static class InterceptorPatches
    {
        public static void AddItem_Prefix(CollectableItem item, int amount)
        {
            if (item != null)
            {
                Plugin.Log.LogInfo($"[COLLECTABLE ADDED] Name: '{item.name}', Amount: {amount}");
            }
        }

        public static void GetValueOrDefault_Prefix(string sceneName, string id)
        {
            Plugin.Log.LogInfo($"[GET] scene='{sceneName}', id='{id}'");
            // Stack trace can be enabled for deeper debugging if needed
            // Plugin.Log.LogInfo(Environment.StackTrace);
        }

        public static void TryGetValue_Prefix(string sceneName, string id)
        {
            Plugin.Log.LogInfo($"[TRYGET] scene='{sceneName}', id='{id}'");
            // Stack trace can be enabled for deeper debugging if needed
            // Plugin.Log.LogInfo(Environment.StackTrace);
        }

        public static void BeginQuest_Postfix(FullQuestBase __instance)
        {
            if (__instance != null)
            {
                Plugin.Log.LogInfo($"[QUEST ACCEPTED] Name: '{__instance.name}'");
            }
        }

        public static void TryEndQuest_Postfix(FullQuestBase __instance, bool __result)
        {
            if (__instance != null && __result)
            {
                Plugin.Log.LogInfo($"[QUEST COMPLETED] Name: '{__instance.name}'");
            }
        }

        public static void SubQuestGet_Postfix(SubQuest __instance)
        {
            if (__instance != null)
            {
                Plugin.Log.LogInfo($"[SUBQUEST PROGRESS] Name: '{__instance.name}', LinkedBool: '{__instance.TargetCounter?.name ?? "none"}'");
            }
        }
    }
    #endregion
}