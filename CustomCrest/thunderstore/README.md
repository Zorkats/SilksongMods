# Custom Crest

Build your own crest. Take the neutral attack from one crest, the down attack from another, and the bind from a third. Crests from other mods work too.

## Quick path

1. Install this package and its dependencies with a mod manager.
2. Start the game once, so BepInEx writes the config file.
3. Equip **Custom Crest** at a bench. The crest is unlocked from the start.
4. Edit `BepInEx/config/com.zorkats.silksong.customcrest.cfg`, or use an in-game config manager.

Changes apply while the game runs. If you change the slots, reopen the inventory to see the new layout.

## Crest names

The config uses the game's internal names.

| In the config | In the game |
| --- | --- |
| `Hunter` | Hunter |
| `Reaper` | Reaper |
| `Wanderer` | Wanderer |
| `Warrior` | Beast |
| `Witch` | Witch |
| `Toolmaster` | Architect |
| `Spell` | Shaman |

## Settings

### 1. Attack Movesets

| Setting | Accepts | Default |
| --- | --- | --- |
| `NeutralAttack` | Any crest, including other mods' crests | `Hunter` |
| `UpAttack` | Any crest, including other mods' crests | `Hunter` |
| `DashAttack` | Any crest with a dash attack that plays, including other mods' crests | `Hunter` |
| `DownAttack` | The seven crests above | `Hunter` |
| `BindingAction` | The seven crests above, plus `Void` with Void Crest installed | `Hunter` |

Each attack keeps its own crest's feel:

- **Timing:** neutral, up and down attacks each swing at the speed of the crest they came from.
- **Beast:** while you are in rage, attacks taken from `Warrior` change to Beast's rage attacks. Attacks from other crests stay the same.
- **Wanderer:** with `DashAttack` set to `Wanderer`, attacking right after a dash attack recoil does Wanderer's follow-up dash.

`BindingAction` chooses what binding does:

| Value | Result |
| --- | --- |
| `Hunter` | Heals normally |
| `Wanderer` | Heals normally, and your hits can be lucky while you have 9 or more silk |
| `Warrior` | Enters rage |
| `Witch` | Binds with tentacles |
| `Reaper` | Enters reaper mode |
| `Spell` | Charges the bind |
| `Toolmaster` | Quick crafts when you bind with Up held |
| `Void` | Void Crest's focus bind, which heals nothing by default. Bind with Up held to spend 9 silk on its sphere, which heals from your void mass |

### 2. Attack Slot Distribution

| Setting | Accepts | Default |
| --- | --- | --- |
| `NeutralSlots`, `UpSlots`, `DownSlots` | 0 or 1 | 1 |
| `NeutralUseSkillSlots`, `UpUseSkillSlots`, `DownUseSkillSlots` | `true` for white slots, `false` for red | `false` |

### 3. Passive & Utility Slots

| Setting | Accepts | Default |
| --- | --- | --- |
| `BlueSlots` | 0 to 10 | 3 |
| `YellowSlots` | 0 to 10 | 3 |

### 4. General Options

| Setting | Accepts | Default |
| --- | --- | --- |
| `UnlockAllSlots` | `true` or `false` | `true` |

## Using other mods' crests

Install the other crest's mod, then use its internal name in `NeutralAttack`, `UpAttack` or `DashAttack`. The internal name can differ from the name in the game. The attack's tint, screen effects and charged attack come with it.

| Mod | Name to use |
| --- | --- |
| Queen Crest | `Yen` |
| Void Crest | `Void` (not for `DashAttack`) |

For any other mod, open `BepInEx/LogOutput.log` and look for the line that starts with `Attack sources available:`. It lists every name you can use. The line `Dash attack sources available:` lists the names `DashAttack` accepts.

If you remove a mod while the config still names its crest, that setting goes back to `Hunter`.

## Limits

- **Other mods' crests:** the attacks come across, but other code written for that crest does not, such as its passive effects.
- **Other mods' tints:** a tint set for a whole crest colors sprites only, so particles and meshes keep their own color.
- **Down attacks:** these come from the seven base game crests only. Queen Crest and Void Crest have no down attack of their own.
- **Void's dash:** Void Crest's dash attack has no animation, and its own crest never plays it, so `DashAttack` does not accept `Void`. If your config already has it, the setting goes back to `Hunter`.
- **Binds from other mods:** only Void Crest's bind is supported. Other mods' binds generally check that their own crest is equipped.
- **Void's bind:** you get the bind only. Void's sharp shadow dash, silk skill changes, plasmium mass and hero color stay with its own crest. The void spool appears on the HUD after your first bind.
- **Wanderer's luck:** lucky hits work, but the HUD frame that shows them does not appear.
- **Tools with no slot left:** a tool with no slot of its type in the new layout is unequipped. This also applies when you load a save after editing slots with the game closed.

## For mod authors

The attack composition is a library, `CrestComposition.dll`, that your own crest mod can use. See the [framework guide](https://github.com/Zorkats/SilksongMods/blob/master/CrestComposition/README.md).
