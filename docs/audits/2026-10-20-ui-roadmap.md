# UI roadmap: from a good-looking menu to a game that guides you

A UI-first plan that replaces the plan section of [the audit](2026-10-20-ui-next-audit.md). A visual version with
phone sketches for every round is published as an artifact (link in the pull request). The audit's measured
findings (skin build cost, contrast, tall phones) still stand and are folded into the rounds below.

## What a player does today

Open (boot screen, one tip) → Home, Start practice (1 tap) → Loadout, two weapons and Ready (3 taps) → Match
(no instructions) → Results card. With a friend: Join room, then type eight characters by hand.

## Six gaps (found in the code, not guessed)

1. **Nobody is taught.** No first-run help, coach marks or "How to play"; one random tip on the loading bar.
2. **The screen never answers back.** No toast or status component; copying the room code (`DisplayServer.clipboard_set`) shows nothing.
3. **You cannot pick your face.** With the Book display-only, the Home face is the last kid played.
4. **Typing a code on a phone.** The join code is one plain field: no boxes, no paste button, one line of error text.
5. **The match gives no direction.** Nothing points to the enemy base or a tagged teammate.
6. **Chrome from two eras.** The opening card, tagged panel, capture pill and scoreboard frame are flat beside skinned buttons.

## Rules for every round

One gold thing per screen (selection is blue). Thumb first (88 px targets, lower 60% of the screen). Every
action replies (press, success, error, waiting). Show, then tell (bars before paragraphs). Motion explains and
stops with Reduce motion. Checked at 16:9, 19.5:9 and short screens.

## Rounds

| Round | Ships | Fixes | Needs a phone |
| --- | --- | --- | --- |
| **7** | Toast and status strip (first use: "Room code copied"); profile sheet (name and a six-face picker); **UI gallery** page in Settings showing every component in every state | 2, 3 | No |
| **8** | First-run coach marks (move, aim, hold the base; once, skippable, replayable from Settings → How to play); join with eight code boxes, Paste and specific errors; the room code big after Create, with Copy | 1, 4 | Light check |
| **9** | Home that fits every phone (hero scales to the stage; last-match strip from the history; mode chips; page slides; tab highlight glides); skin textures built four times faster and behind the loading bar; chip contrast fixed (Heal is 2.41:1) | audit 1, 2, 3 | Perf check |
| **10** | The Book with depth: the real street game behind each weapon (one factual line each), damage, cooldown and range as bars, outfit swatches on each kid, history summary (played, win rate, streak) with a mode filter | | No |
| **11** | In-match chrome on the skin (opening card, tagged panel, capture pill, feed, scoreboard frame); loadout bars and a "Suggested pair"; bigger online pick countdown | 6 | Light check |
| **12** | Direction arrows to the enemy base and a tagged teammate (fade when on screen); results with a count-up, confetti on a win only, and Rematch online; tagged hint | 5 | **Yes** |

The gallery runs through every round: a new component is not done until it is in the gallery with every state.
Sound and transitions (the audit's feel round) fold into round 9 and 12 where they belong.

## Decisions for the owner

1. Start with round 7? It builds the pieces the others use, and the gallery lets the owner review everything on an iPhone.
2. Weapon blurbs: one factual line per street game, drafted by me and corrected by the owner. In Filipino as well?
3. How loud a win should be: confetti and a count-up, or just the card.
4. A native share sheet for the room code on Android (the web build can only copy): worth the code?

Not planned, on purpose: accounts, ranked, friends lists, stats and levelling (out of scope by design), and
per-player match stats (they need a sim and design change first).
