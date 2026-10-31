// Copyright (c) Zorkats. All rights reserved.

using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using Needleforge;
using Needleforge.Attacks;
using Needleforge.Data;

namespace CrestComposition
{
    // A crest whose attacks can be borrowed: one of the seven vanilla crests, or a crest another
    // mod registered with Needleforge. Its Id is the vanilla type name or the crest's own name.
    public sealed class AttackSource
    {
        private readonly CrestData? _crest;

        public string Id { get; }
        public VanillaCrestType? VanillaType { get; }

        public bool IsVanilla
        {
            get
            {
                return VanillaType.HasValue;
            }
        }

        // Starting a dash step whose clip can't be found throws. Void Crest defines such a dash, and
        // its own crest never plays it. A crest with no dash of its own gets a copy of Hunter's.
        public bool HasPlayableDashAttack
        {
            get
            {
                DashAttack? dash = _crest?.Moveset.DashSlash;
                if (dash == null)
                {
                    return true;
                }

                return dash.Steps != null &&
                    dash.Steps.Length > 0 &&
                    Array.TrueForAll(
                        dash.Steps,
                        step => step.AnimLibrary != null && step.AnimLibrary.GetClipByName(step.AnimName) != null);
            }
        }

        internal AttackSource(VanillaCrestType vanillaType)
        {
            Id = vanillaType.ToString();
            VanillaType = vanillaType;
        }

        internal AttackSource(CrestData crest)
        {
            Id = crest.name;
            _crest = crest;
        }

        public HeroController.ConfigGroup? FindConfigGroup(HeroController hc)
        {
            return VanillaType.HasValue
                ? VanillaCrestCatalog.FindVanillaConfigGroup(hc, VanillaType.Value)
                : _crest?.Moveset.ConfigGroup;
        }
    }

    public static class AttackSourceCatalog
    {
        private static readonly FieldInfo? _needleforgeCrestsField =
            typeof(NeedleforgePlugin).GetField("newCrestData", BindingFlags.NonPublic | BindingFlags.Static);

        internal static CrestData? FindNeedleforgeCrest(string crestName)
        {
            return _needleforgeCrestsField?.GetValue(null) is List<CrestData> crests
                ? crests.Find(crest => string.Equals(crest.name, crestName, StringComparison.Ordinal))
                : null;
        }

        // Vanilla crests first, then every Needleforge crest not itself built by a composer: those
        // replace their attacks at the same moment we would be cloning them.
        // Call this after all plugins have run Awake, or later crests will be missing.
        public static List<AttackSource> GetAvailable(ManualLogSource? logger = null)
        {
            List<AttackSource> sources = new List<AttackSource>();
            foreach (VanillaCrestType crestType in VanillaCrestCatalog.All.Keys)
            {
                sources.Add(new AttackSource(crestType));
            }

            if (!(_needleforgeCrestsField?.GetValue(null) is List<CrestData> crests))
            {
                logger?.LogWarning(
                    "[CrestComposition] Could not read Needleforge's crest list; only vanilla crests can be borrowed.");
                return sources;
            }

            foreach (CrestData crest in crests)
            {
                if (!CrestMovesetComposer.IsComposed(crest) &&
                    sources.FindIndex(source => string.Equals(source.Id, crest.name, StringComparison.Ordinal)) < 0)
                {
                    sources.Add(new AttackSource(crest));
                }
            }

            return sources;
        }
    }
}
