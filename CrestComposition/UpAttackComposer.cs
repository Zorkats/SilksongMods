// Copyright (c) Zorkats. All rights reserved.

using System;
using System.Collections.Generic;
using Needleforge.Data;
using UnityEngine;

namespace CrestComposition
{
    // Clones each source's up slash into the custom moveset and switches between them live.
    public sealed class UpAttackComposer
    {
        private static readonly HashSet<string> _clipNames =
            new HashSet<string>(VanillaCrestCatalog.UpAttackAnimationClips, StringComparer.Ordinal);

        private readonly CrestComposerContext _context;
        private readonly Dictionary<string, GameObject?> _upBySource =
            new Dictionary<string, GameObject?>(StringComparer.Ordinal);
        private readonly Dictionary<string, GameObject?> _altUpBySource =
            new Dictionary<string, GameObject?>(StringComparer.Ordinal);
        private readonly Dictionary<string, GameObject?> _rageUpBySource =
            new Dictionary<string, GameObject?>(StringComparer.Ordinal);
        private readonly List<GameObject> _slotClones = new List<GameObject>();

        private string _activeUpAttack;

        public string ActiveUpAttack
        {
            get
            {
                return _activeUpAttack;
            }
        }

        internal UpAttackComposer(CrestComposerContext context, string initialUpAttack)
        {
            _context = context;
            _activeUpAttack = initialUpAttack;
        }

        internal void InitializeOnHeroAwake()
        {
            HeroController? hc = HeroController.instance;
            HeroController.ConfigGroup? customGroup = _context.CrestData.Moveset.ConfigGroup;
            if (hc == null || customGroup == null || customGroup.ActiveRoot == null)
            {
                _context.Logger?.LogError(
                    $"[CrestComposition] Cannot initialize up-attack clones for '{_context.CrestData.name}': " +
                    "HeroController, ConfigGroup, or ActiveRoot is null.");
                return;
            }

            _upBySource.Clear();
            _altUpBySource.Clear();
            _rageUpBySource.Clear();
            _slotClones.Clear();

            CrestComposerContext.DestroyDefaultAttack(customGroup.UpSlashObject);
            CrestComposerContext.DestroyDefaultAttack(customGroup.AltUpSlashObject);
            customGroup.UpSlashObject = null;
            customGroup.AltUpSlashObject = null;

            foreach (AttackSource source in _context.Sources)
            {
                HeroController.ConfigGroup? sourceGroup = source.FindConfigGroup(hc);
                if (sourceGroup == null)
                {
                    _context.Logger?.LogWarning(
                        $"[CrestComposition] Could not find a ConfigGroup for up-attack source '{source.Id}'.");
                    continue;
                }

                _upBySource[source.Id] = CloneSlot(hc, source.Id, sourceGroup.UpSlashObject, "UpSlash");
                _altUpBySource[source.Id] = CloneSlot(hc, source.Id, sourceGroup.AltUpSlashObject, "AltUpSlash");

                if (source.VanillaType == VanillaCrestType.Warrior)
                {
                    _rageUpBySource[source.Id] = CloneSlot(
                        hc,
                        source.Id,
                        VanillaCrestCatalog.FindWarriorRageGroup(hc)?.UpSlashObject,
                        "RageUpSlash");
                }
            }

            if (!_upBySource.ContainsKey(_activeUpAttack))
            {
                _context.Logger?.LogWarning(
                    $"[CrestComposition] Up-attack source '{_activeUpAttack}' is not available; using Hunter.");
                _activeUpAttack = VanillaCrestType.Hunter.ToString();
            }

            ApplyUpAttack(_activeUpAttack);
        }

        public void ApplyUpAttack(string sourceId)
        {
            _activeUpAttack = sourceId;

            HeroController? hc = HeroController.instance;
            HeroController.ConfigGroup? customGroup = _context.CrestData.Moveset.ConfigGroup;
            AttackSource? source = _context.FindSource(sourceId);
            if (hc == null || customGroup == null || source == null || !_upBySource.ContainsKey(sourceId))
            {
                return;
            }

            // A slash in flight must not outlive the object it is playing on.
            if (hc.didStart && _context.IsCustomCrestEquipped(hc))
            {
                hc.CancelAttack();
            }

            _upBySource.TryGetValue(sourceId, out GameObject? upClone);
            _altUpBySource.TryGetValue(sourceId, out GameObject? altUpClone);
            _rageUpBySource.TryGetValue(sourceId, out GameObject? rageUpClone);

            customGroup.UpSlashObject = upClone;
            customGroup.AltUpSlashObject = altUpClone;

            // Setup() only assigns non-null objects, so a stale alt would survive a switch.
            customGroup.UpSlash = null;
            customGroup.UpSlashDamager = null;
            customGroup.AltUpSlash = null;
            customGroup.AltUpSlashDamager = null;

            _context.ActivateSelection(_slotClones, upClone, altUpClone, rageUpClone);
            _context.SetRageUpAttack(rageUpClone);
            _context.SetSlotClips(
                AttackSlot.Up,
                CrestComposerContext.CollectClips(hc, source, _clipNames.Contains));

            if (hc.didStart)
            {
                _context.RefreshConfigGroup(hc);
                _context.Logger?.LogInfo(
                    $"[CrestComposition] Applied up attack '{sourceId}' to '{_context.CrestData.name}'.");
            }
            else
            {
                _context.RebuildAnimations();
            }
        }

        private GameObject? CloneSlot(
            HeroController hc,
            string sourceId,
            GameObject? source,
            string slotName)
        {
            if (source == null)
            {
                return null;
            }

            GameObject clone = _context.CloneOnce(hc, source, $"{sourceId}_{slotName}");
            _slotClones.Add(clone);
            return clone;
        }
    }
}
