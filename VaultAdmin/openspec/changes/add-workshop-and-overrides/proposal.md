# The workshop, the overrides, and the things they broke on the way

## Why

The spec stopped at a panel that grants things. What shipped after it is most of the mod: a bench
that builds a dweller or an animal to order with a live figure of the result, a page of switches
that override the vault's own rules, and a staffing pass that fills every room by the number that
room runs on.

Writing that down matters more than it usually would. Two of those features write to the player's
save, one of them deletes from it, and the reason it deletes is a fault that took a player's report
to find: dressing the bench's figure calls the game's own `EquipWeapon`, and the game does what it
always does on a weapon swap — it hands the old one back to storage. The old one was fabricated for
a picture. Every re-dress minted a real weapon.

## What changes

- **WORKSHOP.** A bench for a dweller and one for an animal. Name, gender, rarity, level, SPECIAL,
  hair, face, hair colour, skin, headgear, outfit, weapon; for an animal, breed, grade, name, bonus
  and its value. A live figure of the dweller stands beside the fields, filmed by a private camera
  into a render texture and kept idling.
- **OVERRIDES.** Vault-wide actions and switches, and a staffing pass under its own heading.
- **Storage is left as it was found.** Anything the bench pushes into the vault while dressing its
  figure is taken back, and only if it can be proved to be what the bench just minted.
- **A grant says so on its own row**, since a toast at the edge of the screen answers a different
  question than "did the row I pressed fire".

## Non-goals

- No per-copy weapon or outfit stats. Those live on the item's own data, not per instance; changing
  them changes every copy in the game.
- No optimal staffing. The best assignment of fifty dwellers to twenty rooms is a larger problem
  than one button deserves, and greedy costs about a point of production.
- No pathfinding, no moving dwellers between floors, nothing that needs the game to be patched.

## Game API this depends on, and how it was confirmed

| Member | Confirmed by |
|---|---|
| `DwellerSpawner.CreateWaitingDweller(EGender, bool, int, EDwellerRarity, bool)` | Reflection; it registers with the arrival queue, which building one by hand did not |
| `Dweller.ApplyCustomization(<piece type>)` | Reflection, overload per piece type |
| `Catalog.m_dwellerCustomizationData.DwellerCustomizationAttributeDataList` | Reflection; `Attribute` names the slot, `Gender` filters it |
| `DwellerPieceList.m_skinColors`, `m_hairColors`, `m_hairColorsForCustomization` | Reflection |
| `DwellerStats.GetStat(ESpecialStat).SetValueAndMinExp(int)` | Reflection; moves stored experience with the value, which `SetValueOnly` does not |
| `DwellerPool.GetInstance(EGender)` | Reflection; a pooled dweller is the one that arrives assembled |
| `Dweller.GenerateRandomCustomization(bool, …)` | Reflection; a pooled dweller has no pieces until this runs |
| `Vault.Inventory.Items`, `AddItem`, and removal by name | Reflection; removal is believed only when the count falls |
| `Vault.m_minimumRushFailureChance`, `RoomParameters.m_rushDisasterChancePerTier` | Reflection; both are plain floats, so a guarantee is a matter of setting them |
| `Room` — its stat, its places, whether it produces | Reflection, each asked three ways |
| The assignment call | Searched by name across `Room`, `Dweller`, `DwellerManager`; unresolved names are logged in full |

## Two things the game does that this had to be told

**A dweller carries no `Animator`.** It is driven by the legacy `Animation` component with the
game's own controller on top, and that controller replaces whatever clip anybody else sets, one
frame later, for ever. Four attempts at the idle were aimed at Mecanim before a diagnostic said
`0 animator(s)`.

**`ApplyCustomization` only ever puts on.** There is no call that takes off, so a slot set back to
"random" left the figure wearing what it wore before — which is why "random" is no longer offered as
a value anywhere in the bench.

## What cannot be verified without a running game

That the staffing pass finds the game's assignment call; that the panel button attaches under the
dwellers list rather than beside the camera; and that a vault left and re-entered has exactly one
panel button. All three write to the log what they found, so a miss names itself.
