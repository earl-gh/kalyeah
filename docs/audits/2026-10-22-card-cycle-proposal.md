# Picking a kid and weapons takes too long: options, including a Clash Royale style card cycle

Owner, 2026-10-22: no face selection at the main screen; picking the kid and the weapons at the same time takes time and hurts the UI/UX;
idea: instead of picking weapons, they shuffle like Clash Royale cards, so every skill gets used without picking at the start or while tagged.

## Done now
The profile sheet is only your name (the six-face picker is gone). The face next to your name on Home is read only: it is the kid you played last.

## What costs time today
- Before every match: kid row + two weapon slots + a 14-weapon grid + info panel + Ready, on a 10 s timer (`WeaponPick`).
- The kid is chosen in **three** places: the room, the loadout, and (before) the profile.
- Tagged: a second weapon-swap screen opens.
- 14 weapons with cooldowns of 4 to 15 s (`data/weapons/*.tres`); nobody can know them all before the first match.

## Option A: two short steps (small change, no rules change)
1. **The kid is chosen once, in the room** (online and hotspot) or on a one-row strip before Practice. The loadout stops showing the kid row.
2. **The loadout is weapons only**, and opens already filled with *your last pair* (first time: the suggested pair). One tap on **Ready** is enough; the grid is for changing.
3. The tagged swap stays but opens on the same screen.
Effect: the common case (same as last time) is one tap. Risk: low. Rules: none.

## Option B: a deck you build once, a hand that cycles (Clash Royale, with your pick)
- Build a **deck of 6 to 8 weapons** once (a Book tab, not on a timer). A match deals a **hand of 2** (the two skill buttons); using one sends it to the back and the next card of the shuffled deck takes its place; a **Next** card is shown.
- No picking at the start and **no swap while tagged** (nothing to pick). The kid is chosen in the room.
- Rules: a card's own cooldown (4 to 15 s) no longer fits. A used card is back in the cycle after the others, so the wait comes from the deck size; keep a short per-slot recharge (about 2 s) so a card is not spammed. Needs a rebalance of all 14 numbers and a new `deck`/`hand` model in `scripts/sim` (seeded shuffle, server side, so it stays authoritative and testable).
- Counter play: the Bato / Gunting / Papel triangle only works if the right card is in hand; a deck with two of each kind makes sure it can be.

## Option C: everyone gets every skill (a shared shuffled deck of all 14)
This is the idea as written: no deck, no picking at all. A hand of 2 from a shuffled 14.
- Pros: nothing to pick, every skill is learned by play, the loadout screen disappears.
- Cons: a hand of 2 from 14 is luck-heavy (a heal and a shield when you needed an attack); both teams must get the same sequence (one seed per match) to stay fair; the 4 to 15 s cooldowns become a very long cycle; a player cannot practise one weapon.
- Mitigations: a hand of 3 to 4 (needs a third button or a card row; the deck at 294 px is tight), a "no two of a kind in a hand" rule, and a short slot recharge.

## Recommendation
Do **A now** (it needs no owner decision on rules), and **prototype B behind a data toggle** (`GameRules.card_cycle`, default off, per the project rule "implement the default and keep it a data toggle"), then run the bot round tool with and without it before deciding. C is a larger bet; B keeps the player's agency (a deck is a choice) and is closer to what Clash Royale actually does (the deck is chosen outside the battle).

## Questions for you
1. A first, and prototype B or C? (My vote: A, then B.)
2. Hand size: 2 (the current two buttons) or 3?
3. Cycle rule: strict rotation (CR) or random draw?
4. A deck of 6, 8 or all 14?
5. Is "no swap while tagged" right? (With a cycle there is nothing to swap; the box screen would only show the wait.)
6. Rules docs to update if this goes ahead: `docs/GDD.md` (weapons, loadout, tagged), and the "no basic attack / only picked weapons" line in `CLAUDE.md`, which currently says players use their picked weapons.
