using System;
using System.Reflection;
using HarmonyLib;

namespace SilksongMods.CompletionTracker
{
    /// <summary>
    /// Provides methods to query game save data including persistent bools and PlayerData fields.
    /// </summary>
    public static class GameData
    {
        #region Fields
        private static object CapturedPersistentBools;
        private static MethodInfo _tryGetValueMethod;
        #endregion

        #region Save Data Capture
        public static void CaptureSaveData(SaveGameData data)
        {
            try
            {
                Plugin.Log.LogInfo("Attempting to capture master save data from SaveGameData object...");

                if (data == null)
                {
                    Plugin.Log.LogError("Capture failed: SaveGameData object was null.");
                    return;
                }

                var sceneDataField = data.GetType().GetField("sceneData", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (sceneDataField == null)
                {
                    Plugin.Log.LogError("Capture failed: Could not find 'sceneData' field on SaveGameData.");
                    return;
                }

                var sceneData = sceneDataField.GetValue(data);
                if (sceneData == null)
                {
                    Plugin.Log.LogError("Capture failed: 'sceneData' object is null.");
                    return;
                }

                var boolsField = sceneData.GetType().GetField("persistentBools", BindingFlags.Instance | BindingFlags.NonPublic);
                if (boolsField == null)
                {
                    Plugin.Log.LogError("Capture failed: Could not find 'persistentBools' field.");
                    return;
                }

                CapturedPersistentBools = boolsField.GetValue(sceneData);

                if (CapturedPersistentBools != null)
                {
                    Plugin.Log.LogInfo(">>> Successfully captured master save data for persistent items! <<<");
                }
                else
                {
                    Plugin.Log.LogError("Capture failed: Captured 'persistentBools' object is null.");
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"A critical error occurred during save data capture: {e}");
            }
        }
        #endregion

        #region Query Methods
        public static bool IsPersistentItemCollected(string sceneName, string id)
        {
            if (CapturedPersistentBools == null) return false;

            try
            {
                if (_tryGetValueMethod == null)
                {
                    _tryGetValueMethod = CapturedPersistentBools.GetType().BaseType.GetMethod(
                        "TryGetValue",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                    );
                }

                if (_tryGetValueMethod == null) return false;

                var arguments = new object[] { sceneName, id, null };
                bool keyFound = (bool)_tryGetValueMethod.Invoke(CapturedPersistentBools, arguments);

                if (keyFound)
                {
                    var persistentItemData = arguments[2];
                    if (persistentItemData == null) return false;

                    var valueField = persistentItemData.GetType().GetField("Value");
                    if (valueField != null)
                    {
                        return (bool)valueField.GetValue(persistentItemData);
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Error checking IsPersistentItemCollected for '{id}': {e}");
            }

            return false;
        }

        public static bool IsPlayerDataBoolSet(string fieldName)
        {
            if (PlayerData.instance == null) return false;

            try
            {
                FieldInfo field = PlayerData.instance.GetType().GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.Public
                );
                return field != null && (bool)field.GetValue(PlayerData.instance);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Could not check PlayerData bool '{fieldName}': {e.Message}");
            }

            return false;
        }

        public static bool HasCollectedItem(string itemName)
        {
            if (PlayerData.instance == null || string.IsNullOrEmpty(itemName)) return false;

            try
            {
                var itemData = PlayerData.instance.Collectables.GetData(itemName);
                return itemData.Amount > 0;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Could not check collectable status for '{itemName}': {e.Message}");
                return false;
            }
        }
        public static bool IsQuestCompleted(string questName)
        {
            if (GameManager.instance?.playerData == null) return false;

            try
            {
                var completion = GameManager.instance.playerData.QuestCompletionData.GetData(questName);
                return completion.IsCompleted;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Could not check quest completion for '{questName}': {e.Message}");
                return false;
            }
        }
        #endregion
    }

    #region Harmony Patches
    [HarmonyPatch]
    public static class CaptureSaveDataPatches
    {
        [HarmonyPatch(typeof(GameManager), "LoadGameFromUI", new Type[] { typeof(int) })]
        [HarmonyPostfix]
        public static void Postfix_Int(GameManager __instance, int saveSlot)
        {
            GameData.CaptureSaveData(__instance.GetSaveGameData(saveSlot));
        }

        [HarmonyPatch(typeof(GameManager), "LoadGameFromUI", new Type[] { typeof(int), typeof(SaveGameData) })]
        [HarmonyPostfix]
        public static void Postfix_SaveData(SaveGameData saveGameData)
        {
            GameData.CaptureSaveData(saveGameData);
        }
    }
    #endregion
}