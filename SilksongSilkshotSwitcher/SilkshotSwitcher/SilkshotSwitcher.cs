using UnityEngine;
using System.Linq;
using System.Collections;
using UnityEngine.UI;

namespace SilksongMods.SilkshotSwitcher
{
    /// <summary>
    /// This component runs every frame to check for our custom input.
    /// </summary>
    public class SilkshotInputManager : MonoBehaviour
    {
        private static readonly string[] SilkshotCycle = { "WebShot Forge", "WebShot Architect", "WebShot Weaver" };

        private void Update()
        {
            if (PlayerData.instance == null || !PlayerData.instance.isInventoryOpen) return;

            if (Input.GetKeyDown(Plugin.CycleKey.Value) || InputHandler.Instance.inputActions.MenuExtra.WasPressed)
            {
                var itemManager = FindObjectOfType<InventoryItemManager>();
                if (itemManager == null || itemManager.CurrentSelected == null) return;

                var selectedToolIcon = itemManager.CurrentSelected as InventoryItemTool;
                if (selectedToolIcon != null && selectedToolIcon.ItemData != null && SilkshotCycle.Contains(selectedToolIcon.ItemData.name))
                {
                    CycleSilkshot(itemManager);
                }
            }
        }

        private void CycleSilkshot(InventoryItemManager itemManager)
        {
            string currentToolName = null;
            // Find which Silkshot is currently unlocked in PlayerData.
            foreach (string name in SilkshotCycle)
            {
                if (PlayerData.instance.Tools.GetData(name).IsUnlocked)
                {
                    currentToolName = name;
                    break;
                }
            }

            // If none are found, use the currently selected icon as the source of truth.
            if (currentToolName == null)
            {
                var currentIcon = itemManager.CurrentSelected as InventoryItemTool;
                if (currentIcon != null)
                {
                    currentToolName = currentIcon.ItemData.name;
                    // Ensure only this one is unlocked to fix the initial state.
                    foreach (string name in SilkshotCycle)
                    {
                        var toolData = PlayerData.instance.Tools.GetData(name);
                        toolData.IsUnlocked = (name == currentToolName);
                        PlayerData.instance.SetToolData(name, toolData);
                    }
                }
                else return;
            }

            int currentIndex = System.Array.IndexOf(SilkshotCycle, currentToolName);
            int nextIndex = (currentIndex + 1) % SilkshotCycle.Length;
            string nextToolName = SilkshotCycle[nextIndex];

            Plugin.Log.LogInfo($"Switching active Silkshot from '{currentToolName}' to '{nextToolName}'.");

            // Start the delayed switching coroutine
            StartCoroutine(DelayedToolSwitch(currentToolName, nextToolName, itemManager));
        }

        private IEnumerator DelayedToolSwitch(string currentToolName, string nextToolName, InventoryItemManager itemManager)
        {
            // Update PlayerData: Lock the old, unlock the new (do this atomically)
            var oldToolData = PlayerData.instance.Tools.GetData(currentToolName);
            oldToolData.IsUnlocked = false;
            PlayerData.instance.SetToolData(currentToolName, oldToolData);

            var newToolData = PlayerData.instance.Tools.GetData(nextToolName);
            newToolData.IsUnlocked = true;
            PlayerData.instance.SetToolData(nextToolName, newToolData);

            // Try to force UI layout refresh using Unity's layout system
            var layoutElements = FindObjectsOfType<LayoutGroup>();
            foreach (var layout in layoutElements)
            {
                if (layout != null && layout.gameObject.activeInHierarchy)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(layout.GetComponent<RectTransform>());
                }
            }

            // Call RefreshTools
            var toolManager = FindObjectOfType<InventoryItemToolManager>();
            if (toolManager != null)
            {
                toolManager.RefreshTools(true, true);
            }

            // Force rebuild UI elements
            var allCanvasGroups = FindObjectsOfType<CanvasGroup>();
            foreach (var cg in allCanvasGroups)
            {
                if (cg != null && cg.gameObject.activeInHierarchy && cg.name.ToLower().Contains("inventory"))
                {
                    var rectTransform = cg.GetComponent<RectTransform>();
                    if (rectTransform != null)
                    {
                        LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
                    }
                }
            }

            // Wait a few frames for everything to update
            yield return null;
            yield return null;
            yield return null;

            // Try to find and select the new tool
            var toolGrid = FindObjectOfType<InventoryItemGrid>();
            if (toolGrid != null)
            {
                var newIcon = toolGrid.GetListItems<InventoryItemTool>(tool => tool.ItemData.name == nextToolName).FirstOrDefault();
                if (newIcon != null && itemManager != null)
                {
                    itemManager.SetSelected(newIcon, null, false);
                }
            }
        }
    }
}