using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEngine;

namespace SilksongMods.CompletionTracker
{
    public class CompletionTrackerUI : MonoBehaviour
    {
        #region Constants
        private const float WindowWidth = 400f;
        private const float WindowHeight = 700f;
        private const float UpdateInterval = 1.0f;
        #endregion

        #region Static Data Maps
        // Tool information: InternalName -> (DisplayName, Location)
        private static readonly Dictionary<string, (string DisplayName, string Location)> ToolNameMap = new Dictionary<string, (string, string)>
        {
            { "Wallcling", ("Ascendant's Grip", "Songclave Shop (350 Rosaries)") },
            { "Barbed Wire", ("Barbed Bracelet", "Sinner's Road (corpse)") },
            { "Dazzle Bind", ("Claw Mirror", "Whispering Vaults (Trobbio boss)") },
            { "Dazzle Bind Upgraded", ("Claw Mirrors", "Whispering Vaults (Tormented Trobbio, Act 3)") },
            { "Cogwork Flier", ("Cogfly", "High Falls (table, 1 metal)") },
            { "Cogwork Saw", ("Cogwork Wheel", "Underworks (Architect, 1 metal + 360 Rosaries)") },
            { "Compass", ("Compass", "Shakra's Shop (70 Rosaries)") },
            { "Conch Drill", ("Conchcutter", "Sands of Karak (Coral Tower)") },
            { "Curve Claws", ("Curveclaw", "Hunter's March (Skarr, 140 Rosaries)") },
            { "Curve Claws Upgraded", ("Curvesickle", "Far Fields (target challenge)") },
            { "Dead Mans Purse", ("Dead Bug's Purse", "Wormways (corpse)") },
            { "Screw Attack", ("Delver's Drill", "Underworks (table, southwest)") },
            { "Mosscreep Tool 1", ("Druid's Eye", "Moss Home (Berry Picking quest)") },
            { "Mosscreep Tool 2", ("Druid's Eyes", "Moss Home (all berries, Act 3)") },
            { "Flea Charm", ("Egg of Flealia", "The Marrow (Lost Fleas quest)") },
            { "Flea Brew", ("Flea Brew", "The Marrow (5 fleas reward)") },
            { "Flintstone", ("Flintslate", "Deep Docks (table)") },
            { "Fractured Mask", ("Fractured Mask", "Hunter's March (Skarr, 260 Rosaries)") },
            { "Quickbind", ("Injector Band", "Whiteward (table)") },
            { "Longneedle", ("Longclaw", "Putrified Ducts (Broodfeast quest)") },
            { "Harpoon", ("Longpin", "Shellwood (behind wasp nest)") },
            { "Lava Charm", ("Magma Bell", "Deep Docks (Forge, 1 metal + 110 Rosaries)") },
            { "Rosary Magnet", ("Magnetite Brooch", "Bonebottom (Pebb, 120 Rosaries)") },
            { "Magnetite Dice", ("Magnetite Dice", "Dice game (beat Lumble in Blasted Steps, otherwise buy from Grindle in Act 3)") },
            { "Revenge Crystal", ("Memory Crystal", "Mount Fay (behind ice)") },
            { "Multibind", ("Multibinder", "Bellheart (Frey, 880 Rosaries, quest)") },
            { "Pinstress Tool", ("Pin Badge", "Mount Fay (Pinstress boss, Act 3)") },
            { "Pimpilo", ("Pimpilo", "Greymoor (table, 1 metal)") },
            { "Lifeblood Syringe", ("Plasmium Phial", "Wormways (Alchemist quest)") },
            { "Poison Pouch", ("Pollip Pouch", "Shellwood (Pollip quest)") },
            { "Quick Sling", ("Quick Sling", "Bilewater (false ceiling)") },
            { "Reserve Bind", ("Reserve Bind", "The Forum (Second Sentinel boss)") },
            { "Rosary Cannon", ("Rosary Cannon", "High Halls (Forum, table, requires Simple Key)") },
            { "Brolly Spike", ("Sawtooth Circlet", "Underworks (Architect, 1 metal + 230 Rosaries)") },
            { "Scuttlebrace", ("Scuttlebrace", "Underworks (Architect, 1 metal + 140 Rosaries)") },
            { "Bone Necklace", ("Shard Pendant", "The Marrow (table)") },
            { "WebShot Forge", ("Silkshot I", "Deep Docks (Forge, Ruined Tool)") },
            { "WebShot Architect", ("Silkshot II", "Underworks (Architect, Ruined Tool)") },
            { "WebShot Weaver", ("Silkshot III", "Mount Fay (table, Ruined Tool)") },
            { "Sprintmaster", ("Silkspeed Anklets", "Far Fields (Sprint Challenge in Weavenest, requires Needolin to enter)") },
            { "Thief Claw", ("Snitch Pick", "Blasted Steps (Grindle, 740 Rosaries)") },
            { "Musician Charm", ("Spider Strings", "Songclave (Jubilana, 320 Rosaries, quest)") },
            { "Spool Extender", ("Spool Extender", "Songclave (Jubilana, 720 Rosaries, quest)") },
            { "Sting Shard", ("Sting Shard", "Deep Docks (Forge, 1 metal + 140 Rosaries)") },
            { "Straight Pin", ("Straight Pin", "The Marrow (table above Grindle)") },
            { "Tack", ("Tacks", "Sinner's Road (Roach Guts quest)") },
            { "Thief Charm", ("Thief's Mark", "Blasted Steps (Grindle, 350 Rosaries)") },
            { "Tri Pin", ("Threefold Pin", "Greymoor (secret cave)") },
            { "Shakra Ring", ("Throwing Ring", "Bilewater (Trail's End quest)") },
            { "Zap Imbuement", ("Volt Filament", "Sands of Karak (Voltvyrm boss)") },
            { "Lightning Rod", ("Voltvessels", "Memorium (table, Act 3)") },
            { "Bell Bind", ("Warding Bell", "Far Fields (platform)") },
            { "White Ring", ("Weavelight", "Weavenest Atla (Moss Mothers boss)") },
            { "Weighted Anklet", ("Weighted Belt", "Pilgrim's Rest (Mort, 160 Rosaries)") },
            { "Wisp Lantern", ("Wispfire Lantern", "Bellheart (Father of Flame boss)") },
            { "Maggot Charm", ("Wreath of Purity", "Putrified Ducts (platform)") }
        };

        private static readonly Dictionary<string, (string DisplayName, string Location)> SkillNameMap = new Dictionary<string, (string, string)>
        {
            { "Silk Spear", ("Silkspear", "Moss Grotto (Mosshome statue)") },
            { "Thread Sphere", ("Thread Storm", "Greymoor (Craw Lake statue)") },
            { "Parry", ("Cross Stitch", "Bilewater (Phantom boss, Exhaust Organ)") },
            { "Silk Charge", ("Sharpdart", "Wormways (Weavenest Karn statue)") },
            { "Silk Bomb", ("Rune Rage", "The Slab (First Sinner boss)") },
            { "Silk Boss Needle", ("Pale Nails", "The Cradle (Act 3)") }
        };

        private static readonly Dictionary<string, (string DisplayName, string Location)> CrestNameMap = new Dictionary<string, (string, string)>
        {
            { "Toolmaster", ("Architect", "Underworks (Chapel above Twelfth Architect, requires Architect's Key)") },
            { "Warrior", ("Beast", "Hunter's March (Chapel, southeast)") },
            { "Reaper", ("Reaper", "Greymoor (Chapel, west side)") },
            { "Spell", ("Shaman", "Ruined Chapel (Act 3)") },
            { "Wanderer", ("Wanderer", "Bonegrave (Chapel of the Wanderer)") },
            { "Witch", ("Witch", "Greymoor (northwest, Twisted Bud questline)") }
        };
        #endregion

        #region Fields
        private static bool _isVisible = false;
        private GUIStyle _uiStyle;
        private Rect _windowRect;
        private Vector2 _scrollPosition = Vector2.zero;
        private string _cachedUIText = "Calculating...";
        private float _updateTimer = 0f;
        #endregion

        #region Properties
        public static CompletionTrackerUI Instance { get; private set; }
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            DontDestroyOnLoad(gameObject);
            float padding = 10f;
            _windowRect = new Rect(Screen.width - WindowWidth - padding, padding, WindowWidth, WindowHeight);
            InitializeStyle();
        }

        private void Update()
        {
            if (!_isVisible) return;

            _updateTimer -= Time.deltaTime;
            if (_updateTimer <= 0f)
            {
                UpdateUITextCache();
                _updateTimer = UpdateInterval;
            }
        }

        private void OnGUI()
        {
            if (!_isVisible || GameManager.instance == null || !GameManager.instance.IsGameplayScene() || _uiStyle == null)
                return;

            _windowRect = GUI.Window(0, _windowRect, DrawUI, $"Completion Tracker ({Plugin.Instance.ToggleKey.Value})");
        }
        #endregion

        #region Public Methods
        public static void ToggleVisibility()
        {
            _isVisible = !_isVisible;
            if (_isVisible)
            {
                Instance?.RequestImmediateUpdate();
            }
        }

        public void RequestImmediateUpdate()
        {
            _updateTimer = 0f;
        }
        #endregion

        #region UI Drawing
        private void DrawUI(int windowID)
        {
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
            GUILayout.Label(_cachedUIText, _uiStyle);
            GUILayout.EndScrollView();
        }

        private void InitializeStyle()
        {
            _uiStyle = new GUIStyle
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 16,
                wordWrap = true,
                richText = true
            };
            _uiStyle.normal.textColor = Color.white;

            Font gameFont = Resources.FindObjectsOfTypeAll<Font>().FirstOrDefault(f => f.name == "Trajan Pro");
            if (gameFont != null)
            {
                _uiStyle.font = gameFont;
            }
            else
            {
                Plugin.Log.LogWarning("Font 'Trajan Pro' not found. Using default UI font.");
            }
        }
        #endregion

        #region UI Content Generation
        private void UpdateUITextCache()
        {
            if (PlayerData.instance == null)
            {
                _cachedUIText = "Waiting for PlayerData...";
                return;
            }

            StringBuilder sb = new StringBuilder();
            PlayerData pd = PlayerData.instance;

            BuildNeedleSection(sb, pd);
            BuildMaskShardsSection(sb, pd);
            BuildSilkSpoolSection(sb, pd);
            BuildFleasSection(sb);
            BuildMemoryLocketsSection(sb);
            BuildCraftingMetalsSection(sb);
            BuildSilkHeartsSection(sb, pd);
            BuildMiscellaneousSection(sb, pd);
            BuildAbilitiesSection(sb, pd);
            BuildCraftingKitSection(sb, pd);
            BuildToolPouchSection(sb, pd);
            BuildCrestsSection(sb);
            BuildSilkSkillsSection(sb);
            BuildToolsSection(sb);

            _cachedUIText = sb.ToString();
        }

        private void BuildNeedleSection(StringBuilder sb, PlayerData pd)
        {
            try
            {
                sb.AppendLine("<b>== Needle (4%) ==</b>");
                sb.AppendLine($"Upgrades: {pd.nailUpgrades} / 4");
                if (pd.nailUpgrades < 4)
                {
                    sb.AppendLine($"<color=orange>- Missing {4 - pd.nailUpgrades} upgrades</color>");
                }
                sb.AppendLine();
            }
            catch (Exception e)
            {
                ReportError(sb, "Needle", e);
            }
        }

        private void BuildMaskShardsSection(StringBuilder sb, PlayerData pd)
        {
            try
            {
                sb.AppendLine("<b>== Mask Shards (5%) ==</b>");
                int completedMasks = pd.maxHealthBase - 5;
                sb.AppendLine($"Masks: {completedMasks} / 5");
                if (completedMasks < 5)
                {
                    sb.AppendLine($"<color=orange>- Shards for next: {pd.heartPieces % 4}/4</color>");
                    ListMissingDatabaseItems(ItemDatabase.MaskShards, sb);
                }
                sb.AppendLine();
            }
            catch (Exception e)
            {
                ReportError(sb, "Ancient Masks", e);
            }
        }

        private void BuildSilkSpoolSection(StringBuilder sb, PlayerData pd)
        {
            try
            {
                sb.AppendLine("<b>== Silk Spool (9%) ==</b>");
                int completedSpools = pd.silkMax - 9;
                sb.AppendLine($"Spools: {completedSpools} / 9");
                if (completedSpools < 9)
                {
                    sb.AppendLine($"<color=orange>- Fragments for next: {pd.silkSpoolParts % 2}/2</color>");
                    ListMissingDatabaseItems(ItemDatabase.SpoolFragments, sb);
                }
                sb.AppendLine();
            }
            catch (Exception e)
            {
                ReportError(sb, "Silk Spool", e);
            }
        }

        private void BuildFleasSection(StringBuilder sb)
        {
            try
            {
                ListCollectibles("Fleas", ItemDatabase.Fleas, sb, ItemDatabase.Fleas.Count, -1, null, true);
            }
            catch (Exception e)
            {
                ReportError(sb, "Fleas", e);
            }
        }

        private void BuildMemoryLocketsSection(StringBuilder sb)
        {
            try
            {
                ListCollectibles("Memory Lockets", ItemDatabase.MemoryLockets, sb, ItemDatabase.MemoryLockets.Count);
            }
            catch (Exception e)
            {
                ReportError(sb, "Memory Lockets", e);
            }
        }

        private void BuildCraftingMetalsSection(StringBuilder sb)
        {
            try
            {
                ListCollectibles("Crafting Metals", ItemDatabase.CraftMetals, sb, ItemDatabase.CraftMetals.Count);
            }
            catch (Exception e)
            {
                ReportError(sb, "Crafting Metals", e);
            }
        }

        private void BuildSilkHeartsSection(StringBuilder sb, PlayerData pd)
        {
            try
            {
                sb.AppendLine($"<b>== Silk Hearts (3%) ==</b>");
                sb.AppendLine($"Hearts: {pd.silkRegenMax} / 3");
                if (pd.silkRegenMax < 3)
                {
                    sb.AppendLine($"<color=orange>- Missing {3 - pd.silkRegenMax} hearts</color>");
                }
                sb.AppendLine();
            }
            catch (Exception e)
            {
                ReportError(sb, "Silk Hearts", e);
            }
        }

        private void BuildMiscellaneousSection(StringBuilder sb, PlayerData pd)
        {
            try
            {
                sb.AppendLine("<b>== Miscellaneous (2%) ==</b>");
                CheckBool(GameData.HasCollectedItem("White Flower"), "Everbloom (White Flower)", sb);
                CheckBool(pd.HasSeenEvaHeal, "Sylphsong (Eva Heal)", sb);
                sb.AppendLine();
            }
            catch (Exception e)
            {
                ReportError(sb, "Miscellaneous", e);
            }
        }

        private void BuildAbilitiesSection(StringBuilder sb, PlayerData pd)
        {
            try
            {
                sb.AppendLine("<b>== Abilities (6%) ==</b>");
                CheckBool(pd.hasDash, "Swift Step", sb);
                CheckBool(pd.hasWalljump, "Cling Grip", sb);
                CheckBool(pd.hasNeedolin, "Needolin", sb);
                CheckBool(pd.hasHarpoonDash, "Clawline", sb);
                CheckBool(pd.hasDoubleJump, "Faydown Cloak", sb);
                CheckBool(pd.hasSuperJump, "Silk Soar", sb);
                sb.AppendLine();
            }
            catch (Exception e)
            {
                ReportError(sb, "Abilities", e);
            }
        }

        private void BuildCraftingKitSection(StringBuilder sb, PlayerData pd)
        {
            try
            {
                sb.AppendLine($"<b>== Crafting Kit (4%) ==</b>");
                sb.AppendLine($"Upgrades: {pd.ToolKitUpgrades} / 4");
                if (pd.ToolKitUpgrades < 4)
                {
                    sb.AppendLine($"<color=orange>- Missing {4 - pd.ToolKitUpgrades} upgrades</color>");
                }
                sb.AppendLine();
            }
            catch (Exception e)
            {
                ReportError(sb, "Crafting Kit", e);
            }
        }

        private void BuildToolPouchSection(StringBuilder sb, PlayerData pd)
        {
            try
            {
                sb.AppendLine($"<b>== Tool Pouch (4%) ==</b>");
                sb.AppendLine($"Upgrades: {pd.ToolPouchUpgrades} / 4");
                if (pd.ToolPouchUpgrades < 4)
                {
                    sb.AppendLine($"<color=orange>- Missing {4 - pd.ToolPouchUpgrades} upgrades</color>");
                }
                sb.AppendLine();
            }
            catch (Exception e)
            {
                ReportError(sb, "Tool Pouch", e);
            }
        }

        private void BuildCrestsSection(StringBuilder sb)
        {
            try
            {
                ListMissingItems("Crests", CrestNameMap, sb);
            }
            catch (Exception e)
            {
                ReportError(sb, "Crests", e);
            }
        }

        private void BuildSilkSkillsSection(StringBuilder sb)
        {
            try
            {
                ListMissingItems("Silk Skills", SkillNameMap, sb);
            }
            catch (Exception e)
            {
                ReportError(sb, "Silk Skills", e);
            }
        }

        private void BuildToolsSection(StringBuilder sb)
        {
            try
            {
                ListMissingItems("Tools", ToolNameMap, sb);
            }
            catch (Exception e)
            {
                ReportError(sb, "Tools", e);
            }
        }
        #endregion

        #region Helper Methods
        private void ReportError(StringBuilder sb, string section, Exception e)
        {
            sb.AppendLine($"<color=red><b>Error loading {section} section.</b></color>");
            sb.AppendLine();
            Plugin.Log.LogError($"Error in UI section '{section}': {e}");
        }

        private void ListMissingDatabaseItems(Dictionary<string, ItemLocationInfo> itemDatabase, StringBuilder sb)
        {
            if (itemDatabase.Count == 0)
            {
                sb.AppendLine("<color=grey>- Location data not available</color>");
                return;
            }

            foreach (var item in itemDatabase.Values)
            {
                bool isCollected = item.SceneName == "PlayerData"
                    ? GameData.IsPlayerDataBoolSet(item.ItemID)
                    : GameData.IsPersistentItemCollected(item.SceneName, item.ItemID);

                if (!isCollected)
                {
                    sb.AppendLine($"<color=orange>- Missing: {item.DisplayName} in {item.MapZone}</color>");
                }
            }
        }

        private void ListCollectibles(string categoryName, Dictionary<string, ItemLocationInfo> itemDatabase, StringBuilder sb, int maxCount, int overrideFoundCount = -1, string subtext = null, bool groupByZone = false)
        {
            if (itemDatabase == null) return;

            int foundCount = overrideFoundCount >= 0
                ? overrideFoundCount
                : itemDatabase.Values.Count(IsItemCollected);

            sb.AppendLine($"<b>== {categoryName} ({foundCount}/{maxCount}) ==</b>");

            if (foundCount < maxCount)
            {
                if (subtext != null)
                {
                    sb.AppendLine($"<color=orange>- {subtext}</color>");
                }

                var missingItems = itemDatabase.Values.Where(item => !IsItemCollected(item)).ToList();

                if (groupByZone)
                {
                    var missingGroups = missingItems.GroupBy(item => item.MapZone).OrderBy(group => group.Key);
                    foreach (var group in missingGroups)
                    {
                        sb.AppendLine($"<color=orange>- {group.Key}: {group.Count()} missing</color>");
                    }
                }
                else
                {
                    foreach (var item in missingItems.OrderBy(i => i.MapZone))
                    {
                        sb.AppendLine($"<color=orange>- Missing: {item.DisplayName}</color>");
                        sb.AppendLine($"  <color=grey>{item.MapZone}</color>");
                    }
                }
            }
            sb.AppendLine();
        }

        private bool IsItemCollected(ItemLocationInfo item)
        {
            if (item.SceneName == null && !string.IsNullOrEmpty(item.ItemID))
            {
                return GameData.IsPlayerDataBoolSet(item.ItemID);
            }

            switch (item.SceneName)
            {
                case "PlayerData":
                    return GameData.IsPlayerDataBoolSet(item.ItemID);
                case "Collectable":
                    return GameData.HasCollectedItem(item.ItemID);
                case "QuestCompletion":
                    return GameData.IsQuestCompleted(item.ItemID);
                default:
                    return GameData.IsPersistentItemCollected(item.SceneName, item.ItemID);
            }
        }

        private void CheckBool(bool hasItem, string itemName, StringBuilder sb)
        {
            if (!hasItem)
            {
                sb.AppendLine($"<color=orange>- Missing: {itemName}</color>");
            }
        }

        private void ListMissingItems(string categoryName, Dictionary<string, (string DisplayName, string Location)> itemMap, StringBuilder sb)
        {
            var distinctDisplayNames = itemMap.Values.Select(v => v.DisplayName).Distinct().ToList();
            int totalCount = distinctDisplayNames.Count;
            int foundCount = distinctDisplayNames.Count(displayName =>
                itemMap.Where(pair => pair.Value.DisplayName == displayName)
                       .Select(pair => pair.Key)
                       .Any(IsItemUnlocked));

            sb.AppendLine($"<b>== {categoryName} ({foundCount}/{totalCount}) ==</b>");

            if (foundCount < totalCount)
            {
                foreach (var displayName in distinctDisplayNames)
                {
                    var matchingItems = itemMap.Where(pair => pair.Value.DisplayName == displayName).ToList();
                    var internalNames = matchingItems.Select(pair => pair.Key);

                    if (!internalNames.Any(IsItemUnlocked))
                    {
                        // Get the location from the first matching item
                        string location = matchingItems.First().Value.Location;
                        sb.AppendLine($"<color=orange>- Missing: {displayName}</color>");
                        sb.AppendLine($"  <color=grey>{location}</color>");
                    }
                }
            }
            sb.AppendLine();
        }

        private bool IsItemUnlocked(string internalName)
        {
            if (string.IsNullOrEmpty(internalName)) return false;

            ToolItem tool = ToolItemManager.GetToolByName(internalName);
            if (tool != null) return tool.IsUnlocked;

            ToolCrest crest = ToolItemManager.GetCrestByName(internalName);
            if (crest != null) return crest.IsUnlocked;

            return false;
        }
        #endregion
    }
}