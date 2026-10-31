# CrestComposition

CrestComposition lets a Silksong mod build a crest from other crests' attacks. Your mod names the source crest for each attack, and CrestComposition clones the attacks and their animations onto a [Needleforge](https://thunderstore.io/c/hollow-knight-silksong/p/Voidlings/Needleforge/) crest. It adds FSM states of its own and leaves vanilla states and attack objects unchanged, so vanilla crests keep working as before.

Custom Crest ships this library and is built on it.

## Quick path

1. Set up your project references:
   - Add the NuGet packages `Needleforge`, `Silksong.FsmUtil` and `Silksong.GameLibs`.
   - Copy `CrestComposition.dll` from the `plugins` folder of the Custom Crest package, and reference that file.
2. Declare the plugin dependencies, then create the crest and its composer in `Awake`. Replace the two sprite fields with your own loaded sprites:

   ```csharp
   using BepInEx;
   using CrestComposition;
   using Needleforge;
   using Needleforge.Data;
   using UnityEngine;

   [BepInPlugin("com.example.mycrest", "My Crest", "1.0.0")]
   [BepInDependency("io.github.needleforge", "0.9.0")]
   [BepInDependency("org.silksong-modding.fsmutil", "0.3.16")]
   [BepInDependency("com.zorkats.silksong.customcrest", "4.0.0")]
   public class MyCrestPlugin : BaseUnityPlugin
   {
       private Sprite crestSprite = null!;
       private Sprite crestSilhouette = null!;
       private CrestMovesetComposer composer = null!;

       private void Awake()
       {
           CrestData crest = NeedleforgePlugin.AddCrest("MyCrest", crestSprite, crestSilhouette);
           crest.UnlockedAtStart = true;

           composer = new CrestMovesetComposer(
               crest,
               VanillaCrestType.Witch,    // neutral
               VanillaCrestType.Hunter,   // up
               VanillaCrestType.Reaper,   // down
               Logger);
           composer.SetDashAttack(VanillaCrestType.Wanderer);
           composer.SetBindOutcome(VanillaCrestType.Warrior);
       }
   }
   ```

3. Add your crest's tool slots with Needleforge's `CrestData` methods, such as `AddRedSlot`, `AddBlueSlot` and `AddYellowSlot`.
4. Start the game and search `BepInEx/LogOutput.log` for `[CrestComposition]`. A failed composition logs an error there and leaves the hero's setup running.

## Choosing sources

| Attack | Setter | Accepts |
| --- | --- | --- |
| Neutral, alt, wall, taunt, charged | `SetNeutralAttack` | Vanilla crest or crest name |
| Up, alt up | `SetUpAttack` | Vanilla crest or crest name |
| Down | `SetDownAttack` | Vanilla crest |
| Dash | `SetDashAttack` | Vanilla crest or crest name |
| Bind outcome | `SetBindOutcome` | Vanilla crest, or `"Void"` with Void Crest installed |

The setters also work while the game runs, so you can connect them to config entries.

Each swing uses the attack timing of the crest its direction came from. Neutral timing also covers wall and charged attacks.

Some behavior follows the selected source:

| Source | Behavior |
| --- | --- |
| `Warrior` for neutral or up | Those attacks change to their rage versions while the hero is in rage |
| `Wanderer` for dash | Attacking in the window after a dash recoil starts the follow-up dash |
| `Wanderer` for bind | `HeroController.IsWandererLucky` applies while silk is at 9 or more |

### Borrowing from another mod's crest

To borrow from another mod, pass its crest name in `Start`. All plugins have run `Awake` by then, so every crest is registered:

```csharp
private void Start()
{
    composer.SetNeutralAttack("Yen");   // Queen Crest's internal name
}
```

To list the names, call `AttackSourceCatalog.GetAvailable()` in `Start` or later. A source that is not available when the hero loads logs a warning and falls back to Hunter.

A crest can be listed but have no dash to borrow. Check `AttackSource.HasPlayableDashAttack` before you pass a name to `SetDashAttack`. Void Crest is one: its dash attack has no animation, and its own crest never plays it. After the hero loads, `SetDashAttack` logs a warning for such a name and keeps the current dash.

A borrowed attack brings its Needleforge tint, its charged attack steps and their screen effects. A tint set on the source crest's root colors the clone's sprites only.

To use Void Crest's bind, call `composer.SetBindOutcome("Void")` in `Start` or later. If Void Crest is missing or has changed, it logs a warning and falls back to Hunter. To list the bind outcomes, call `composer.BindOutcome.GetAvailableOutcomes()`.

Void's bind patches its focus check, `VoidCrest.AddVoid.VoidFocusEnabled`, to also accept a composed crest that is binding with the `Void` outcome. Its other crest-gated features stay with Void's own crest.

Crests built with `CrestMovesetComposer` are left out of the list, so one composed crest cannot borrow from another.

## What the composer owns

Do not set these yourself. The composer overwrites them:

| Member | Set by |
| --- | --- |
| `CrestData.BindEvent`, `CrestData.BindCompleteEvent` | `BindOutcomeComposer` |
| `DashSlashFsmEdit`, dash stab fields | `DashAttackComposer` |
| `ChargedSlashFsmEdit`, attack and charged fields | `NeutralAttackComposer` |
| Custom downslash, `downSlashType`, `downSlashEvent` | `DownAttackComposer` |
| `heroAnimOverrideLib` | Rebuilt from the selected attacks |

To keep your own animation clips in the merged library, pass them to `SetBaseOverrideClips`.

## Hooks and checks

| Member | Use |
| --- | --- |
| `OnMovesetInitialized` | Runs on hero `Awake`, before the composer builds the attacks |
| `LastSelfCheckPassed` | `false` if a copied FSM branch still references a source crest's attacks |
| `AttackSourceCatalog.GetAvailable()` | Lists every crest you can borrow from |
| `AttackSource.HasPlayableDashAttack` | `false` if the crest's dash attack cannot be played |

## Limits

- **Attack sources:** down attacks come from vanilla crests only. Bind outcomes come from vanilla crests and Void Crest only.
- **Third-party attacks:** custom FSM edits from another mod do not carry over. The attack objects, config, animations, tints and charged steps do. Lambda actions in copied FSM states are removed, because they do not survive the copy.
- **Wanderer's luck:** the lucky hits apply to a composed crest, but `BindOrbHudFrame` still checks the Wanderer crest, so its HUD frame does not appear.
- **Multiple composed crests:** each crest has its own composer, but this setup has not been tested in-game.

## Distributing your mod

To ship a crest built on this library, depend on the Custom Crest package in your Thunderstore manifest. Do not include `CrestComposition.dll` in your own package: two copies in `BepInEx/plugins` can load as separate assemblies, each with its own composer list and Harmony patches.
