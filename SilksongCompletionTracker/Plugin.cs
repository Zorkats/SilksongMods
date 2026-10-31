using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace SilksongMods.CompletionTracker
{
    [BepInPlugin("com.zorkats.silksong.completiontracker", "Completion Tracker", "3.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        public static Plugin Instance { get; private set; }
        public ConfigEntry<KeyCode> ToggleKey { get; private set; }

        private void Awake()
        {
            Log = Logger;
            Instance = this;
            ToggleKey = Config.Bind("General", "ToggleKey", KeyCode.F2, "The keybind to toggle the UI.");

            // Apply only the necessary patches for input handling.
            var harmony = new Harmony("com.zorkats.silksong.completiontracker");
            harmony.PatchAll();

            // Add the UI component to the game.
            gameObject.AddComponent<CompletionTrackerUI>();

            Log.LogInfo($"Completion Tracker loaded! Press {ToggleKey.Value} to toggle the UI.");
        }
    }

    // This patch handles the key press to toggle the UI.
    [HarmonyPatch(typeof(InputHandler))]
    public static class InputHandler_Update_Patch
    {
        [HarmonyPatch("Update")]
        [HarmonyPostfix]
        public static void CheckForToggleKey()
        {
            if (Input.GetKeyDown(Plugin.Instance.ToggleKey.Value))
            {
                CompletionTrackerUI.ToggleVisibility();
            }
        }
    }
}