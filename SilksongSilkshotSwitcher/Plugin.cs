using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace SilksongMods.SilkshotSwitcher
{
    [BepInPlugin("com.zorkats.silksong.silkshotswitcher", "Silkshot Switcher", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal static BepInEx.Logging.ManualLogSource Log;
        public static ConfigEntry<KeyCode> CycleKey { get; private set; }

        private void Awake()
        {
            Log = Logger;
            CycleKey = Config.Bind("Keybinds", "CycleSilkshot", KeyCode.S, "Key to cycle Silkshot versions in the inventory.");
            gameObject.AddComponent<SilkshotInputManager>();
            var harmony = new Harmony("com.zorkats.silksong.silkshotswitcher");
            harmony.PatchAll();
            Log.LogInfo($"Silkshot Switcher loaded! Cycle Key: {CycleKey.Value}");
        }
    }
}