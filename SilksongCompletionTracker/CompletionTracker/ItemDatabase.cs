using System.Collections.Generic;

namespace SilksongMods.CompletionTracker
{
    public class ItemLocationInfo
    {
        public string DisplayName { get; set; }
        public string SceneName { get; set; }
        public string ItemID { get; set; }
        public string MapZone { get; set; }
    }

    public static class ItemDatabase
    {
        #region Mask Shards
        public static readonly Dictionary<string, ItemLocationInfo> MaskShards = new Dictionary<string, ItemLocationInfo>
        {
            { "MaskShard_Dock_08", new ItemLocationInfo { DisplayName = "Mask Shard (Deep Docks)", SceneName = "Dock_08", ItemID = "Heart Piece", MapZone = "Deep Docks" } },
            { "MaskShard_Weave_05b", new ItemLocationInfo { DisplayName = "Mask Shard (Weavenest Atla)", SceneName = "Weave_05b", ItemID = "Heart Piece", MapZone = "Weavenest Atla" } },
            { "MaskShard_Crawl_02", new ItemLocationInfo { DisplayName = "Mask Shard (Wormways)", SceneName = "Crawl_02", ItemID = "Heart Piece", MapZone = "Wormways" } },
            { "MaskShard_Shellwood_14", new ItemLocationInfo { DisplayName = "Mask Shard (Shellwood)", SceneName = "Shellwood_14", ItemID = "Heart Piece", MapZone = "Shellwood" } },
            { "MaskShard_Bone_East_20", new ItemLocationInfo { DisplayName = "Mask Shard (Far Fields)", SceneName = "Bone_East_20", ItemID = "Heart Piece", MapZone = "Far Fields" } },
            { "MaskShard_Bone_East_LavaChallenge", new ItemLocationInfo { DisplayName = "Mask Shard (Far Fields Lava Room)", SceneName = "Bone_East_LavaChallenge", ItemID = "Heart Piece (1)", MapZone = "Far Fields" } },
            { "MaskShard_Coral_19b", new ItemLocationInfo { DisplayName = "Mask Shard (Blasted Steps)", SceneName = "Coral_19b", ItemID = "Heart Piece", MapZone = "Blasted Steps" } },
            { "MaskShard_Wisp_07", new ItemLocationInfo { DisplayName = "Mask Shard (Wisp Thicket)", SceneName = "Wisp_07", ItemID = "Heart Piece", MapZone = "Wisp Thicket" } },
            { "MaskShard_Shadow_13", new ItemLocationInfo { DisplayName = "Mask Shard (Bilewater)", SceneName = "Shadow_13", ItemID = "Heart Piece", MapZone = "Bilewater" } },
            { "MaskShard_Library_05", new ItemLocationInfo { DisplayName = "Mask Shard (Whispering Vaults)", SceneName = "Library_05", ItemID = "Heart Piece", MapZone = "Whispering Vaults" } },
            { "MaskShard_Song_09", new ItemLocationInfo { DisplayName = "Mask Shard (Choral Chambers)", SceneName = "Song_09", ItemID = "Heart Piece", MapZone = "Choral Chambers" } },
            { "MaskShard_Slab_17", new ItemLocationInfo { DisplayName = "Mask Shard (The Slab)", SceneName = "Slab_17", ItemID = "Heart Piece", MapZone = "The Slab" } },
            { "MaskShard_Peak_06", new ItemLocationInfo { DisplayName = "Mask Shard (Brightvein)", SceneName = "Peak_06", ItemID = "Heart Piece", MapZone = "Mount Fay" } },
            { "MaskShard_Peak_04c", new ItemLocationInfo { DisplayName = "Mask Shard (Mount Fay)", SceneName = "Peak_04c", ItemID = "Heart Piece", MapZone = "Mount Fay" } },
            { "MaskShard_Shop_Bonebottom", new ItemLocationInfo { DisplayName = "Mask Shard (Shop)", SceneName = "PlayerData", ItemID = "PurchasedBonebottomHeartPiece", MapZone = "Bone Bottom" } },
            { "MaskShard_Shop_Enclave", new ItemLocationInfo { DisplayName = "Mask Shard (Shop)", SceneName = "PlayerData", ItemID = "MerchantEnclaveShellFragment", MapZone = "Songclave" } },
            { "MaskShard_Quest_BeastflyHunt", new ItemLocationInfo { DisplayName = "Mask Shard (Quest)", SceneName = "QuestCompletion", ItemID = "Beastfly Hunt", MapZone = "Savage Beastfly Quest" } },
            { "MaskShard_Quest_ThreadCores", new ItemLocationInfo { DisplayName = "Mask Shard (Quest)", SceneName = "QuestCompletion", ItemID = "Destroy Thread Cores", MapZone = "Dark Hearts Quest (Act 3, Bellhart board)" } },
            { "MaskShard_Quest_SprintChallenge", new ItemLocationInfo { DisplayName = "Mask Shard (Quest)", SceneName = "QuestCompletion", ItemID = "Sprintmaster Pre", MapZone = "Fastest in Pharloom Quest (Far Fields)" } },
            { "MaskShard_Quest_AntTrapper", new ItemLocationInfo { DisplayName = "Mask Shard (Quest)", SceneName = "QuestCompletion", ItemID = "Ant Trapper", MapZone = "The Hidden Hunter Quest (Act 3, Bellhart board)" } }
        };
        #endregion

        #region Spool Fragments
        public static readonly Dictionary<string, ItemLocationInfo> SpoolFragments = new Dictionary<string, ItemLocationInfo>
        {
            { "SpoolFragment_Bone_East_13", new ItemLocationInfo { DisplayName = "Spool Fragment (Deep Docks)", SceneName = "Bone_East_13", ItemID = "Silk Spool", MapZone = "Deep Docks" } },
            { "SpoolFragment_Weave_11", new ItemLocationInfo { DisplayName = "Spool Fragment (Weavenest Atla)", SceneName = "Weave_11", ItemID = "Silk Spool", MapZone = "Weavenest Atla" } },
            { "SpoolFragment_Bone_11b", new ItemLocationInfo { DisplayName = "Spool Fragment (Bone Bottom)", SceneName = "Bone_11b", ItemID = "Silk Spool", MapZone = "Bone Bottom" } },
            { "SpoolFragment_Dock_03c", new ItemLocationInfo { DisplayName = "Spool Fragment (Deep Docks)", SceneName = "Dock_03c", ItemID = "Silk Spool", MapZone = "Deep Docks" } },
            { "SpoolFragment_Song_19_entrance", new ItemLocationInfo { DisplayName = "Spool Fragment (Grand Gate)", SceneName = "Song_19_entrance", ItemID = "Silk Spool", MapZone = "Citadel" } },
            { "SpoolFragment_Greymoor_02", new ItemLocationInfo { DisplayName = "Spool Fragment (Greymoor)", SceneName = "Greymoor_02", ItemID = "Silk Spool", MapZone = "Greymoor" } },
            { "SpoolFragment_Arborium_09", new ItemLocationInfo { DisplayName = "Spool Fragment (Memorium)", SceneName = "Arborium_09", ItemID = "Silk Spool", MapZone = "Memorium" } },
            { "SpoolFragment_Cog_07", new ItemLocationInfo { DisplayName = "Spool Fragment (Cogwork Core)", SceneName = "Cog_07", ItemID = "Silk Spool", MapZone = "Cogwork Core" } },
            { "SpoolFragment_Hang_03_top", new ItemLocationInfo { DisplayName = "Spool Fragment (High Halls)", SceneName = "Hang_03_top", ItemID = "Silk Spool", MapZone = "High Halls" } },
            { "SpoolFragment_Ward_01", new ItemLocationInfo { DisplayName = "Spool Fragment (Whiteward)", SceneName = "Ward_01", ItemID = "Silk Spool", MapZone = "Whiteward" } },
            { "SpoolFragment_Library_11b", new ItemLocationInfo { DisplayName = "Spool Fragment (Underworks)", SceneName = "Library_11b", ItemID = "Silk Spool", MapZone = "Underworks" } },
            { "SpoolFragment_Under_10", new ItemLocationInfo { DisplayName = "Spool Fragment (Underworks)", SceneName = "Under_10", ItemID = "Silk Spool", MapZone = "Underworks" } },
            { "SpoolFragment_Peak_01", new ItemLocationInfo { DisplayName = "Spool Fragment (The Slab, Mount Fay Room)", SceneName = "Peak_01", ItemID = "Silk Spool", MapZone = "The Slab" } },
            { "SpoolFragment_Shop_Belltown", new ItemLocationInfo { DisplayName = "Spool Fragment (Bellhart Shop)", SceneName = "PlayerData", ItemID = "PurchasedBelltownSpoolSegment", MapZone = "Bellhart" } },
            { "SpoolFragment_Shop_Grindle", new ItemLocationInfo { DisplayName = "Spool Fragment (Grindle's Shop)", SceneName = "PlayerData", ItemID = "purchasedGrindleSpoolPiece", MapZone = "Blasted Steps" } },
            { "SpoolFragment_Quest_Pilgrim", new ItemLocationInfo { DisplayName = "Spool Fragment (Quest)", SceneName = "PlayerData", ItemID = "pilgrimQuestSpoolCollected", MapZone = "Pilgrim's Rest" } },
            { "SpoolFragment_Shop_Enclave", new ItemLocationInfo { DisplayName = "Spool Fragment (Jubilana's Shop)", SceneName = "PlayerData", ItemID = "MerchantEnclaveSpoolPiece", MapZone = "Songclave" } },
            { "SpoolFragment_TroupeLeader_Judge", new ItemLocationInfo { DisplayName = "Spool Fragment (Fleamaster Mooshka, 14 Fleas)", SceneName = "PlayerData", ItemID = "MetCaravanTroupeLeaderJudge", MapZone = "Blasted Steps (Last Judge Room)" } }
        };
        #endregion

        #region Fleas
        public static readonly Dictionary<string, ItemLocationInfo> Fleas = new Dictionary<string, ItemLocationInfo>
        {
            { "Flea_Bone_06", new ItemLocationInfo { ItemID = "SavedFlea_Bone_06", MapZone = "The Marrow" } },
            { "Flea_Dock_16", new ItemLocationInfo { ItemID = "SavedFlea_Dock_16", MapZone = "Deep Docks" } },
            { "Flea_Bone_East_05", new ItemLocationInfo { ItemID = "SavedFlea_Bone_East_05", MapZone = "Deep Docks" } },
            { "Flea_Bone_East_17b", new ItemLocationInfo { ItemID = "SavedFlea_Bone_East_17b", MapZone = "Deep Docks" } },
            { "Flea_Ant_03", new ItemLocationInfo { ItemID = "SavedFlea_Ant_03", MapZone = "Hunter's March" } },
            { "Flea_Greymoor_15b", new ItemLocationInfo { ItemID = "SavedFlea_Greymoor_15b", MapZone = "Greymoor" } },
            { "Flea_Greymoor_06", new ItemLocationInfo { ItemID = "SavedFlea_Greymoor_06", MapZone = "Greymoor" } },
            { "Flea_Shellwood_03", new ItemLocationInfo { ItemID = "SavedFlea_Shellwood_03", MapZone = "Shellwood" } },
            { "Flea_Bone_East_10_Church", new ItemLocationInfo { ItemID = "SavedFlea_Bone_East_10_Church", MapZone = "Far Fields (Near Pilgrim's Rest)" } },
            { "Flea_Coral_35", new ItemLocationInfo { ItemID = "SavedFlea_Coral_35", MapZone = "Sands of Karak" } },
            { "Flea_Dust_12", new ItemLocationInfo { ItemID = "SavedFlea_Dust_12", MapZone = "Sinner's Road" } },
            { "Flea_Dust_09", new ItemLocationInfo { ItemID = "SavedFlea_Dust_09", MapZone = "Sinner's Road" } },
            { "Flea_Belltown_04", new ItemLocationInfo { ItemID = "SavedFlea_Belltown_04", MapZone = "Bellhart" } },
            { "Flea_Crawl_06", new ItemLocationInfo { ItemID = "SavedFlea_Crawl_06", MapZone = "Wormways" } },
            { "Flea_Slab_Cell", new ItemLocationInfo { ItemID = "SavedFlea_Slab_Cell", MapZone = "The Slab" } },
            { "Flea_Shadow_28", new ItemLocationInfo { ItemID = "SavedFlea_Shadow_28", MapZone = "Bilewater" } },
            { "Flea_Dock_03d", new ItemLocationInfo { ItemID = "SavedFlea_Dock_03d", MapZone = "Deep Docks" } },
            { "Flea_Under_23", new ItemLocationInfo { ItemID = "SavedFlea_Under_23", MapZone = "Underworks" } },
            { "Flea_Shadow_10", new ItemLocationInfo { ItemID = "SavedFlea_Shadow_10", MapZone = "Bilewater" } },
            { "Flea_Song_14", new ItemLocationInfo { ItemID = "SavedFlea_Song_14", MapZone = "The Citadel" } },
            { "Flea_Coral_24", new ItemLocationInfo { ItemID = "SavedFlea_Coral_24", MapZone = "Sands of Karak" } },
            { "Flea_Peak_05c", new ItemLocationInfo { ItemID = "SavedFlea_Peak_05c", MapZone = "Mount Fay" } },
            { "Flea_Library_09", new ItemLocationInfo { ItemID = "SavedFlea_Library_09", MapZone = "Whispering Vaults" } },
            { "Flea_Song_11", new ItemLocationInfo { ItemID = "SavedFlea_Song_11", MapZone = "The Citadel" } },
            { "Flea_Library_01", new ItemLocationInfo { ItemID = "SavedFlea_Library_01", MapZone = "Whispering Vaults" } },
            { "Flea_Under_21", new ItemLocationInfo { ItemID = "SavedFlea_Under_21", MapZone = "Underworks" } },
            { "Flea_Slab_06", new ItemLocationInfo { ItemID = "SavedFlea_Slab_06", MapZone = "The Slab" } },
            { "Flea_Big_Tamed", new ItemLocationInfo { ItemID = "tamedGiantFlea", MapZone = "Memorium" } },
            { "Flea_Lech_Saved", new ItemLocationInfo { ItemID = "CaravanLechSaved", MapZone = "Greymoor" } },
            { "Flea_Troupe_Hunter", new ItemLocationInfo { ItemID = "MetTroupeHunterWild", MapZone = "Putrified Ducts" } }
        };
        #endregion

        #region Memory Lockets
        public static readonly Dictionary<string, ItemLocationInfo> MemoryLockets = new Dictionary<string, ItemLocationInfo>
        {
            { "Locket_World_Bone_18", new ItemLocationInfo { DisplayName = "Memory Locket", SceneName = "Bone_18", ItemID = "Collectable Item Pickup", MapZone = "The Marrow (arena battle, east of Bellway)" } },
            { "Locket_World_Bellway_City", new ItemLocationInfo { DisplayName = "Memory Locket", SceneName = "Bellway_City", ItemID = "Collectable Item Pickup", MapZone = "Choral Chambers (west breakable wall from locked trapdoor)" } },
            { "Locket_World_Crawl_09", new ItemLocationInfo { DisplayName = "Memory Locket", SceneName = "Crawl_09", ItemID = "Collectable Item Pickup", MapZone = "Wormways (Simple Door path, basement corner)" } },
            { "Locket_World_Dock_13", new ItemLocationInfo { DisplayName = "Memory Locket", SceneName = "Dock_13", ItemID = "Collectable Item Pickup", MapZone = "Deep Docks (breakable wall, secret passage below bench)" } },
            { "Locket_World_Ant_20", new ItemLocationInfo { DisplayName = "Memory Locket", SceneName = "Ant_20", ItemID = "Collectable Item Pickup", MapZone = "Hunter's March (pilgrim corpse in cage, red bud area)" } },
            { "Locket_World_Bone_East_25_right", new ItemLocationInfo { DisplayName = "Memory Locket", SceneName = "Bone_East_25", ItemID = "Collectable Item Pickup", MapZone = "Far Fields (Skarrsinger Karmelita room, Act 3)" } },
            { "Locket_World_Coral_23", new ItemLocationInfo { DisplayName = "Memory Locket", SceneName = "Coral_23", ItemID = "Collectable Item Pickup", MapZone = "Sands of Karak (corpse, west upper platform)" } },
            { "Locket_World_Coral_02", new ItemLocationInfo { DisplayName = "Memory Locket", SceneName = "Coral_02", ItemID = "Collectable Item Pickup (1)", MapZone = "Blasted Steps (elevated platform east from Shakra)" } },
            { "Locket_World_Shadow_27", new ItemLocationInfo { DisplayName = "Memory Locket", SceneName = "Shadow_27", ItemID = "Sack Corpse Pickup", MapZone = "Bilewater (Muckmaggot path, near secret Bench)" } },
            { "Locket_World_Shadow_20", new ItemLocationInfo { DisplayName = "Memory Locket", SceneName = "Shadow_20", ItemID = "Collectable Item Pickup", MapZone = "Bilewater (hanging cocoon from ceiling)" } },
            { "Locket_World_Under_08", new ItemLocationInfo { DisplayName = "Memory Locket", SceneName = "Under_08", ItemID = "Collectable Item Pickup", MapZone = "Underworks (secret passage west of Confession Booth bench)" } },
            { "Locket_World_Arborium_05", new ItemLocationInfo { DisplayName = "Memory Locket", SceneName = "Arborium_05", ItemID = "Collectable Item Pickup", MapZone = "Memorium (breakable floor, climb vertical path west)" } },
            { "Locket_World_Library_08", new ItemLocationInfo { DisplayName = "Memory Locket", SceneName = "Library_08", ItemID = "Collectable Item Pickup", MapZone = "Whispering Vaults (Giant Drapemite east, breakable box)" } },
            { "Locket_World_Greymoor_16", new ItemLocationInfo { DisplayName = "Memory Locket", SceneName = "Greymoor_16", ItemID = "Collectable Item Pickup", MapZone = "Greymoor (Left of Greymoor Bellway, above giant rats)" } },
            { "Locket_World_Halfway_01", new ItemLocationInfo { DisplayName = "Memory Locket", SceneName = "Halfway_01", ItemID = "Collectable Item Pickup", MapZone = "Greymoor (Halfway Home, top left ledge from Creige, needs Faydown Cloak)" } },
            { "Locket_World_Slab_Cell_Quiet", new ItemLocationInfo { DisplayName = "Memory Locket", SceneName = "Slab_Cell_Quiet", ItemID = "Collectable Item Pickup", MapZone = "The Slab (Shortcut Cave, breakable wall upper east)" } },
            { "Locket_World_Belltown", new ItemLocationInfo { DisplayName = "Memory Locket", SceneName = "Belltown", ItemID = "Collectable Item Pickup", MapZone = "Bellhart (top of Bellhome, needs Silk Soar)" } },
            { "Locket_Shop_PilgrimsRest", new ItemLocationInfo { DisplayName = "Memory Locket", SceneName = "PlayerData", ItemID = "PurchasedPilgrimsRestMemoryLocket", MapZone = "Pilgrim's Rest (Mort, 150 Rosaries)" } },
            { "Locket_Shop_Belltown", new ItemLocationInfo { DisplayName = "Memory Locket", SceneName = "PlayerData", ItemID = "PurchasedBelltownMemoryLocket", MapZone = "Bellhart (Frey, 330 Rosaries, after Threadspun Town)" } },
            { "Locket_Quest_RockRollers", new ItemLocationInfo { DisplayName = "Memory Locket", SceneName = "QuestCompletion", ItemID = "Rock Rollers", MapZone = "Bone Bottom (Volatile Flintbeetles quest)" } }
        };
        #endregion

        #region Crafting Metals
        public static readonly Dictionary<string, ItemLocationInfo> CraftMetals = new Dictionary<string, ItemLocationInfo>
        {
            { "Metal_World_Bone_07", new ItemLocationInfo { DisplayName = "Crafting Metal", SceneName = "Bone_07", ItemID = "Collectable Item Pickup - Tool Metal", MapZone = "The Marrow (explosive rock secret path, east of bench)" } },
            { "Metal_World_Dock_03", new ItemLocationInfo { DisplayName = "Crafting Metal", SceneName = "Dock_03", ItemID = "Collectable Item Pickup", MapZone = "Deep Docks (chest room, Simple Key door or Far Fields secret)" } },
            { "Metal_World_Wisp_05", new ItemLocationInfo { DisplayName = "Crafting Metal", SceneName = "Wisp_05", ItemID = "Collectable Item Pickup - Tool Metal", MapZone = "Wisp Thicket (secret path west, break two ceilings)" } },
            { "Metal_World_Under_19b", new ItemLocationInfo { DisplayName = "Crafting Metal", SceneName = "Under_19b", ItemID = "Collectable Item Pickup - Tool Metal", MapZone = "Underworks (wedged in wall, 2 levels below bench)" } },
            { "Metal_World_Coral_32", new ItemLocationInfo { DisplayName = "Crafting Metal", SceneName = "Coral_32", ItemID = "Collectable Item Pickup - Tool Metal", MapZone = "Blasted Steps (narrow path, northeast dipped area)" } },
            { "Metal_World_Aqueduct_05", new ItemLocationInfo { DisplayName = "Crafting Metal", SceneName = "Aqueduct_05", ItemID = "Collectable Item Pickup - Tool Metal", MapZone = "Putrified Ducts (east of Fleatopia bench)" } },
            { "Metal_Shop_Bonebottom", new ItemLocationInfo { DisplayName = "Crafting Metal", SceneName = "PlayerData", ItemID = "PurchasedBonebottomToolMetal", MapZone = "Bone Bottom (Pebb, 60 Rosaries)" } },
            { "Metal_Shop_Enclave", new ItemLocationInfo { DisplayName = "Crafting Metal", SceneName = "PlayerData", ItemID = "MerchantEnclaveToolMetal", MapZone = "Songclave (Jubilana, 180 Rosaries, after Lost Merchant quest)" } }
        };
        #endregion
    }
}