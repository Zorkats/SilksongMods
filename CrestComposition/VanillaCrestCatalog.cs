// Copyright (c) Zorkats. All rights reserved.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrestComposition
{
    // Describes one child GameObject under a vanilla crest's ActiveRoot used by its down attack.
    public sealed class DownAttackChildBinding
    {
        public string ChildName { get; }
        public string? FsmVariableName { get; }
        public bool IsPrimaryDownSlash { get; }
        public bool IsAltDownSlash { get; }

        public DownAttackChildBinding(
            string childName,
            string? fsmVariableName,
            bool isPrimaryDownSlash,
            bool isAltDownSlash)
        {
            ChildName = childName;
            FsmVariableName = fsmVariableName;
            IsPrimaryDownSlash = isPrimaryDownSlash;
            IsAltDownSlash = isAltDownSlash;
        }
    }

    // Binds an FSM variable to an object found by path under the hero's Attacks transform.
    public sealed class FsmAttackObjectBinding
    {
        public string FsmVariableName { get; }
        public string AttacksPath { get; }
        public string? ChildPath { get; }

        public FsmAttackObjectBinding(string fsmVariableName, string attacksPath, string? childPath = null)
        {
            FsmVariableName = fsmVariableName;
            AttacksPath = attacksPath;
            ChildPath = childPath;
        }
    }

    // How a vanilla crest reaches its charged slash from the Nail Arts FSM. Shared routes read
    // everything from the hero config at runtime; copied ones name crest objects directly.
    public sealed class ChargedSlashRoute
    {
        public string EntryState { get; }
        public bool CopiesBranch { get; }
        public string? ChargeSlashObjectName { get; }
        public IReadOnlyList<FsmAttackObjectBinding> Bindings { get; }

        public ChargedSlashRoute(
            string entryState,
            bool copiesBranch,
            string? chargeSlashObjectName,
            params FsmAttackObjectBinding[] bindings)
        {
            EntryState = entryState;
            CopiesBranch = copiesBranch;
            ChargeSlashObjectName = chargeSlashObjectName;
            Bindings = bindings;
        }
    }

    // How a source reaches its dash attack from the Sprint FSM: the state to enter, the
    // crest objects the branch names directly, and the hero clips the route plays.
    public sealed class DashAttackRoute
    {
        public string EntryState { get; }
        public Predicate<string> ClipFilter { get; }
        public IReadOnlyList<FsmAttackObjectBinding> Bindings { get; }

        public DashAttackRoute(
            string entryState,
            Predicate<string> clipFilter,
            params FsmAttackObjectBinding[] bindings)
        {
            EntryState = entryState;
            ClipFilter = clipFilter;
            Bindings = bindings;
        }
    }

    // Metadata bridging the two vanilla crest naming families (ToolCrest vs ConfigGroup/ActiveRoot)
    // and the Crest Attacks (crest_attacks) FSM structure.
    public sealed class VanillaCrestInfo
    {
        public VanillaCrestType CrestType { get; }
        public string ToolCrestName { get; }
        public IReadOnlyList<string> AlternateToolCrestNames { get; }
        public string ActiveRootName { get; }
        public string ConfigName { get; }
        public HeroControllerConfig.DownSlashTypes DownSlashType { get; }
        public string VanillaDownSlashEvent { get; }
        public string? CrestAttacksEntryState { get; }
        public IReadOnlyList<DownAttackChildBinding> DownAttackChildren { get; }
        public IReadOnlyList<string> DownAttackAnimationClips { get; }

        public VanillaCrestInfo(
            VanillaCrestType crestType,
            string toolCrestName,
            string[] alternateToolCrestNames,
            string activeRootName,
            string configName,
            HeroControllerConfig.DownSlashTypes downSlashType,
            string vanillaDownSlashEvent,
            string? crestAttacksEntryState,
            DownAttackChildBinding[] downAttackChildren,
            string[] downAttackAnimationClips)
        {
            CrestType = crestType;
            ToolCrestName = toolCrestName;
            AlternateToolCrestNames = alternateToolCrestNames;
            ActiveRootName = activeRootName;
            ConfigName = configName;
            DownSlashType = downSlashType;
            VanillaDownSlashEvent = vanillaDownSlashEvent;
            CrestAttacksEntryState = crestAttacksEntryState;
            DownAttackChildren = downAttackChildren;
            DownAttackAnimationClips = downAttackAnimationClips;
        }
    }

    // Canonical lookup table for all seven vanilla crests.
    public static class VanillaCrestCatalog
    {
        private static readonly Dictionary<VanillaCrestType, VanillaCrestInfo> _entries =
            new Dictionary<VanillaCrestType, VanillaCrestInfo>
            {
                [VanillaCrestType.Hunter] = new VanillaCrestInfo(
                    VanillaCrestType.Hunter,
                    "Hunter",
                    new[] { "Hunter", "Hunter_v2", "Hunter_v3" },
                    "Default",
                    "Default",
                    HeroControllerConfig.DownSlashTypes.DownSpike,
                    string.Empty,
                    null,
                    new[]
                    {
                        new DownAttackChildBinding("DownSlash", null, true, false)
                    },
                    new[]
                    {
                        "DownSpike Antic",
                        "DownSpike",
                        "DownSpike Recovery",
                        "Downspike Recovery",
                        "DownSlashEffect"
                    }),

                [VanillaCrestType.Wanderer] = new VanillaCrestInfo(
                    VanillaCrestType.Wanderer,
                    "Wanderer",
                    new[] { "Wanderer" },
                    "Wanderer",
                    "Wanderer",
                    HeroControllerConfig.DownSlashTypes.Slash,
                    string.Empty,
                    null,
                    new[]
                    {
                        new DownAttackChildBinding("DownSlash", null, true, false),
                        new DownAttackChildBinding("DownSlashAlt", null, false, true)
                    },
                    new[]
                    {
                        "DownSlash",
                        "DownSlashAlt",
                        "DownSlashEffect"
                    }),

                [VanillaCrestType.Reaper] = new VanillaCrestInfo(
                    VanillaCrestType.Reaper,
                    "Reaper",
                    new[] { "Reaper", "Scythe" },
                    "Scythe",
                    "Reaper",
                    HeroControllerConfig.DownSlashTypes.Custom,
                    "RPR DOWNSLASH",
                    "Rpr Downslash Antic",
                    new[]
                    {
                        new DownAttackChildBinding("DownSlash New", "Rpr DownSlash", true, false)
                    },
                    new[]
                    {
                        "v3 Down Slash Antic",
                        "v3 Down Slash",
                        "v3 Down Slash Effect",
                        "Downspike Recovery",
                        "DownSpike Recovery"
                    }),

                [VanillaCrestType.Warrior] = new VanillaCrestInfo(
                    VanillaCrestType.Warrior,
                    "Warrior",
                    new[] { "Warrior" },
                    "Warrior",
                    "Warrior",
                    HeroControllerConfig.DownSlashTypes.Custom,
                    "WARRIOR DOWNSLASH",
                    "SpinBall Antic",
                    new[]
                    {
                        new DownAttackChildBinding("SpinSlash", "SpinSlash", true, false),
                        new DownAttackChildBinding("SpinSlashRage", "SpinSlashRage", false, false),
                        new DownAttackChildBinding("SpinSlash Grind Dummy", "SpinSlash Grind Dummy", false, false)
                    },
                    new[]
                    {
                        "SpinBall Antic",
                        "SpinBall Launch",
                        "SpinBall",
                        "SpinBall Rage",
                        "SpinBall Effect",
                        "SpinBall Rage Effect",
                        "SpinBall Grind",
                        "SpinBall Grind Rage",
                        "SpinBall Grind Effect",
                        "SpinBall Grind Rage Effect",
                        "SpinBall Bounce",
                        "SpinBall Rebound",
                        "SpinBall Rebound Rage",
                        "SpinBall Rebound Effect",
                        "SpinBall Rebound Rage Effect"
                    }),

                [VanillaCrestType.Witch] = new VanillaCrestInfo(
                    VanillaCrestType.Witch,
                    "Witch",
                    new[] { "Witch" },
                    "Witch",
                    "Whip",
                    HeroControllerConfig.DownSlashTypes.Custom,
                    "WITCH DOWNSLASH",
                    "Witch Downslash Antic",
                    new[]
                    {
                        new DownAttackChildBinding("DownSlash New", "Witch Downslash", true, false),
                        new DownAttackChildBinding("Lash Damager", "Lash Damager", false, false),
                        new DownAttackChildBinding("Followup Slash", "Followup Slash", false, false),
                        new DownAttackChildBinding("Lash Checker", "Lash Checker", false, false),
                        new DownAttackChildBinding("Tink Checker", "Tink Checker", false, false)
                    },
                    new[]
                    {
                        "DownSpike Antic",
                        "DownSpike",
                        "DownSpike Recovery",
                        "Downspike Recovery",
                        "DownSlashEffect",
                        "Downspike Followup"
                    }),

                [VanillaCrestType.Toolmaster] = new VanillaCrestInfo(
                    VanillaCrestType.Toolmaster,
                    "Toolmaster",
                    new[] { "Toolmaster", "Architect" },
                    "Toolmaster",
                    "Toolmaster",
                    HeroControllerConfig.DownSlashTypes.Custom,
                    "TOOLMASTER DOWNSLASH",
                    "ToolM ChargeStart",
                    new[]
                    {
                        new DownAttackChildBinding("DownSlash New", "Toolmaster Downslash", true, false),
                        new DownAttackChildBinding("DownSlash New Charged", "Toolmaster Downslash Charged", false, false),
                        new DownAttackChildBinding("Drill Checker", "Drill Checker", false, false),
                        new DownAttackChildBinding("DownSlash BouncePodDummy", "DownSlash BouncePodDummy", false, false)
                    },
                    new[]
                    {
                        "DownSpike Charge",
                        "DownSpike Antic",
                        "DownSpike",
                        "DownSpike Charged",
                        "DownSpike Recovery",
                        "Downspike Recovery",
                        "DownSlashEffect",
                        "DownSlashEffect Charged",
                        "Drill Grind",
                        "Drill Grind Effect",
                        "Drill Grind Charged",
                        "Drill Grind Effect Charged",
                        "DownSlash PodDummy"
                    }),

                [VanillaCrestType.Spell] = new VanillaCrestInfo(
                    VanillaCrestType.Spell,
                    "Spell",
                    new[] { "Spell", "Shaman" },
                    "Shaman",
                    "Shaman",
                    HeroControllerConfig.DownSlashTypes.Custom,
                    "SHAMAN DOWNSLASH",
                    "Shaman Downslash Antic",
                    new[]
                    {
                        new DownAttackChildBinding("DownSlash", "Shaman Downslash", true, false),
                        new DownAttackChildBinding("Shaman_blade_cast_effect DownSlash", null, false, false)
                    },
                    new[]
                    {
                        "DownSlash",
                        "DownSlashEffect",
                        "Downspike Recovery",
                        "DownSpike Recovery"
                    })
            };

        // Warrior's ConfigGroup.ChargeSlash points at the unused "Old" object; its real
        // charged slashes are picked by the FSM, so they are named here instead.
        private static readonly Dictionary<VanillaCrestType, ChargedSlashRoute> _chargedSlashRoutes =
            new Dictionary<VanillaCrestType, ChargedSlashRoute>
            {
                [VanillaCrestType.Hunter] = new ChargedSlashRoute("Antic", false, null),
                [VanillaCrestType.Witch] = new ChargedSlashRoute("Antic", false, null),
                [VanillaCrestType.Reaper] = new ChargedSlashRoute("Reaper Kickoff", false, null),
                [VanillaCrestType.Spell] = new ChargedSlashRoute("Shaman Kickoff", false, null),
                [VanillaCrestType.Wanderer] = new ChargedSlashRoute(
                    "Wanderer Antic",
                    true,
                    null,
                    new FsmAttackObjectBinding("Wanderer Lunge Stopper", "Charge Slash Wanderer", "Lunge Stopper")),
                [VanillaCrestType.Toolmaster] = new ChargedSlashRoute(
                    "Antic Drill",
                    true,
                    null,
                    new FsmAttackObjectBinding("Dash Stopper", "Charge Slash Toolmaster", "damager/dash stopper")),
                [VanillaCrestType.Warrior] = new ChargedSlashRoute(
                    "Warrior2 Antic",
                    true,
                    "Charge Slash Warrior",
                    new FsmAttackObjectBinding("Charge Slash Warrior", "Charge Slash Warrior", null),
                    new FsmAttackObjectBinding("Charge Slash Warrior R", "Charge Slash Warrior Rage", null))
            };

        // Crests with no custom charged route of their own read everything from the config group.
        private static readonly ChargedSlashRoute _crossModChargedSlashRoute =
            new ChargedSlashRoute("Antic", false, null);

        // The Hunter and Witch dashes share states up to the Reaction Type branch, so both
        // copies carry the lash objects even though a Hunter copy never takes that path.
        private static readonly FsmAttackObjectBinding[] _witchLashBindings = new[]
        {
            new FsmAttackObjectBinding("Witch Lash Damager", "Witch/Sprint Lash Damager"),
            new FsmAttackObjectBinding("Followup Slash", "Witch/Followup Slash"),
            new FsmAttackObjectBinding("Lash Checker", "Witch/Lash Checker Sprint")
        };

        private static readonly Dictionary<VanillaCrestType, DashAttackRoute> _dashAttackRoutes =
            new Dictionary<VanillaCrestType, DashAttackRoute>
            {
                [VanillaCrestType.Hunter] = new DashAttackRoute(
                    "Set Attack Single",
                    ClipNames(),
                    _witchLashBindings),
                [VanillaCrestType.Witch] = new DashAttackRoute(
                    "Set Attack Multiple",
                    ClipNames(
                        "Dash Attack Antic 1",
                        "Dash Attack 1",
                        "Dash Attack Antic 2",
                        "Dash Attack 2",
                        "Dash Attack Recover",
                        "Sprint Followup",
                        "Downspike Followup"),
                    _witchLashBindings),
                [VanillaCrestType.Reaper] = new DashAttackRoute(
                    "Reaper Antic",
                    ClipNames("Dash Upper Antic", "Dash Upper", "Dash Upper Recovery"),
                    new FsmAttackObjectBinding("Reaper Slash", "Scythe/DashUpper Slash")),
                // The entry state's name really does end in a space.
                [VanillaCrestType.Wanderer] = new DashAttackRoute(
                    "Wanderer Set ",
                    ClipNames(
                        "Wanderer Dash Attack",
                        "Wanderer Dash Attack Alt",
                        "Wanderer DashRecoil",
                        "Wanderer RecoilStab",
                        "Dash Attack Recover"),
                    new FsmAttackObjectBinding("Wanderer RecoilStab", "Wanderer/RecoilStab")),
                [VanillaCrestType.Warrior] = new DashAttackRoute(
                    "Warrior Antic",
                    ClipNames("Dash Attack Antic", "Dash Attack Leap", "Dash Attack Slash"),
                    new FsmAttackObjectBinding("Warrior Dash Slash", "Warrior/DashSlash"),
                    new FsmAttackObjectBinding("Warrior Dash Slash R", "Warrior/DashSlash Rage")),
                [VanillaCrestType.Spell] = new DashAttackRoute(
                    "Shaman Antic",
                    ClipNames("Dash Attack Antic", "Dash Attack Leap", "Dash Attack Slash"),
                    new FsmAttackObjectBinding("Shaman Dash Slash", "Shaman/DashSlash")),
                [VanillaCrestType.Toolmaster] = new DashAttackRoute(
                    "Drill Charge Start",
                    ClipNames("Dash Attack Charge", "Dash Attack", "Drill Grind", "Drill Grind Charged"),
                    new FsmAttackObjectBinding("Toolmaster Drill Stab", "Toolmaster/Dash Stab"),
                    new FsmAttackObjectBinding("Toolmaster Drill Stab Charged", "Toolmaster/Dash Stab Charged"))
            };

        // Needleforge dashes always run the multi-step path with Dash Attack [Antic] N clips.
        private static readonly DashAttackRoute _crossModDashAttackRoute = new DashAttackRoute(
            "Set Attack Multiple",
            clipName => clipName.StartsWith("Dash Attack", StringComparison.Ordinal),
            _witchLashBindings);

        private static readonly string[] _neutralAttackAnimationClips = new[]
        {
            "Slash",
            "SlashAlt",
            "SlashEffect",
            "SlashEffectAlt",
            "SlashEffect Rage",
            "SlashEffectAlt Rage",
            "v3 Slash Effect",
            "Wall Slash",
            "Slash_Charged",
            "Slash_Charged_Loop",
            "Slash_Charged Effect",
            "Slash_Charged Effect Rage",
            "NeedleArt Dash",
            "Taunt"
        };

        private static readonly string[] _upAttackAnimationClips = new[]
        {
            "UpSlash",
            "UpSlashAlt",
            "UpSlashEffect",
            "UpSlashEffect Rage"
        };

        private static readonly HashSet<string> _allVanillaCrestAttackVariables =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "Charge Slash Warrior",
                "Charge Slash Warrior R",
                "Dash Stab",
                "Dash Stab Alt",
                "Reaper Slash",
                "Wanderer RecoilStab",
                "Warrior Dash Slash",
                "Warrior Dash Slash R",
                "Witch Lash Damager",
                "Toolmaster Drill Stab",
                "Toolmaster Drill Stab Charged",
                "Shaman Dash Slash",
                "Dash Stopper",
                "Wanderer Lunge Stopper",
                "Rpr DownSlash",
                "Witch Downslash",
                "Lash Damager",
                "Followup Slash",
                "Lash Checker",
                "Tink Checker",
                "SpinSlash",
                "SpinSlashRage",
                "SpinSlash Grind Dummy",
                "Shaman Downslash",
                "Toolmaster Downslash",
                "Toolmaster Downslash Charged",
                "Drill Checker",
                "DownSlash BouncePodDummy"
            };

        public static IReadOnlyDictionary<VanillaCrestType, VanillaCrestInfo> All
        {
            get
            {
                return _entries;
            }
        }

        public static IReadOnlyCollection<string> AllVanillaCrestAttackVariables
        {
            get
            {
                return _allVanillaCrestAttackVariables;
            }
        }

        public static IReadOnlyList<string> NeutralAttackAnimationClips
        {
            get
            {
                return _neutralAttackAnimationClips;
            }
        }

        public static IReadOnlyList<string> UpAttackAnimationClips
        {
            get
            {
                return _upAttackAnimationClips;
            }
        }

        public static VanillaCrestInfo Get(VanillaCrestType crestType)
        {
            return _entries[crestType];
        }

        public static ChargedSlashRoute GetChargedSlashRoute(VanillaCrestType crestType)
        {
            return _chargedSlashRoutes[crestType];
        }

        public static ChargedSlashRoute CrossModChargedSlashRoute
        {
            get
            {
                return _crossModChargedSlashRoute;
            }
        }

        public static DashAttackRoute GetDashAttackRoute(VanillaCrestType crestType)
        {
            return _dashAttackRoutes[crestType];
        }

        public static DashAttackRoute CrossModDashAttackRoute
        {
            get
            {
                return _crossModDashAttackRoute;
            }
        }

        // Warrior's rage attacks live in a group of their own, applied over its normal one.
        public static HeroController.ConfigGroup? FindWarriorRageGroup(HeroController hc)
        {
            HeroControllerConfig? warriorConfig = FindVanillaConfigGroup(hc, VanillaCrestType.Warrior)?.Config;
            return warriorConfig != null && hc.specialConfigs != null
                ? Array.Find(hc.specialConfigs, group => group.Config == warriorConfig)
                : null;
        }

        public static HeroController.ConfigGroup? FindVanillaConfigGroup(
            HeroController heroController,
            VanillaCrestType crestType)
        {
            if (heroController == null || heroController.configs == null)
            {
                return null;
            }

            VanillaCrestInfo info = Get(crestType);
            foreach (HeroController.ConfigGroup? group in heroController.configs)
            {
                if (group == null || group.ActiveRoot == null)
                {
                    continue;
                }

                if (string.Equals(group.ActiveRoot.name, info.ActiveRootName, StringComparison.OrdinalIgnoreCase))
                {
                    return group;
                }
            }

            foreach (HeroController.ConfigGroup? group in heroController.configs)
            {
                if (group == null || group.Config == null)
                {
                    continue;
                }

                if (string.Equals(group.Config.name, info.ConfigName, StringComparison.OrdinalIgnoreCase))
                {
                    return group;
                }
            }

            return null;
        }

        public static ToolCrest? FindVanillaToolCrest(VanillaCrestType crestType)
        {
            VanillaCrestInfo info = Get(crestType);
            ToolCrest? crest = ToolItemManager.GetCrestByName(info.ToolCrestName);
            if (crest != null)
            {
                return crest;
            }

            foreach (string altName in info.AlternateToolCrestNames)
            {
                crest = ToolItemManager.GetCrestByName(altName);
                if (crest != null)
                {
                    return crest;
                }
            }

            return null;
        }

        public static List<Transform> GetAllVanillaActiveRoots(HeroController heroController)
        {
            List<Transform> roots = new List<Transform>();
            if (heroController == null || heroController.configs == null)
            {
                return roots;
            }

            foreach (VanillaCrestInfo info in _entries.Values)
            {
                HeroController.ConfigGroup? group = FindVanillaConfigGroup(heroController, info.CrestType);
                if (group != null && group.ActiveRoot != null)
                {
                    roots.Add(group.ActiveRoot.transform);
                }
            }

            return roots;
        }

        // Active roots plus the attack objects that live outside them.
        public static List<Transform> GetAllVanillaAttackRoots(HeroController heroController)
        {
            List<Transform> roots = GetAllVanillaActiveRoots(heroController);
            if (heroController == null || heroController.configs == null)
            {
                return roots;
            }

            foreach (VanillaCrestInfo info in _entries.Values)
            {
                HeroController.ConfigGroup? group = FindVanillaConfigGroup(heroController, info.CrestType);
                if (group?.ChargeSlash != null)
                {
                    roots.Add(group.ChargeSlash.transform);
                }

                if (group?.TauntSlash != null)
                {
                    roots.Add(group.TauntSlash.transform);
                }
            }

            Transform? attacksRoot = heroController.transform.Find("Attacks");
            foreach (ChargedSlashRoute route in _chargedSlashRoutes.Values)
            {
                AddBindingRoots(roots, attacksRoot, route.Bindings);
            }

            foreach (DashAttackRoute route in _dashAttackRoutes.Values)
            {
                AddBindingRoots(roots, attacksRoot, route.Bindings);
            }

            return roots;
        }

        private static void AddBindingRoots(
            List<Transform> roots,
            Transform? attacksRoot,
            IReadOnlyList<FsmAttackObjectBinding> bindings)
        {
            foreach (FsmAttackObjectBinding binding in bindings)
            {
                Transform? child = attacksRoot?.Find(binding.AttacksPath);
                if (child != null)
                {
                    roots.Add(child);
                }
            }
        }

        private static Predicate<string> ClipNames(params string[] names)
        {
            HashSet<string> nameSet = new HashSet<string>(names, StringComparer.Ordinal);
            return nameSet.Contains;
        }
    }
}
