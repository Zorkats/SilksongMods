# Changelog

## 4.0.0

### Added

- `BindingAction` accepts `Void` when Void Crest is installed: its focus bind, and its sphere when you bind with Up held.
- `BindingAction` set to `Wanderer` gives lucky hits while you have 9 or more silk.
- Beast's rage attacks, for attacks taken from `Warrior`, while you are in rage.
- Wanderer's follow-up dash after a dash attack recoil, with `DashAttack` set to `Wanderer`.
- Other mods' tints, screen effects and charged attacks now come with their attacks.

### Changed

- Neutral, up and down attacks each swing at the speed of the crest they came from. `NeutralAttack` used to set the timing for all of them.
- Editing slots with the game closed now moves your saved tools to matching slots when you load a save.

## 3.0.0

Rebuilt on Needleforge.

### Added

- Dash attack selection (`DashAttack`).
- Bind outcome selection for all seven crests (`BindingAction`).
- Attacks from other mods' Needleforge crests, for `NeutralAttack`, `UpAttack` and `DashAttack`. `DashAttack` does not accept `Void`, because Void Crest's dash attack has no animation.
- Slot changes while the game runs. Equipped tools move to a matching slot of the new layout.
- `CrestComposition.dll`, a library that lets other mods compose crests the same way.

### Changed

- CustomCrest requires Needleforge and FsmUtil.
- `NeutralAttack`, `UpAttack` and `DashAttack` take a crest name rather than a fixed list.

### Removed

- `UseBindingCrestDash`. Use `DashAttack` instead. The old line in your config file has no effect.
