# Choose-your-kid screen: what is needed and what is not

Method: the real web export on a 390 x 844 touch phone, driven by Playwright (the harness in `tools/qa/`, #236): every element tapped, swiped, and reached by keyboard; then Play into the match.

| Element | Interaction found | Needed? |
| --- | --- | --- |
| Kid row (6 faces + names) | Tap selects (gold ring, portrait changes); keyboard arrows move a focus ring, Enter selects | **Yes**: the only choice on the screen |
| Play | Starts the match | **Yes** |
| Back | Leaves to Home | **Yes** |
| Big portrait | Shows the chosen kid; tap and swipe do nothing | Useful as confirmation (kept); it is the one big thing a player looks at. A left/right swipe to change kid would make it useful, not built |
| Name under the portrait | Repeats the name already under the face in the row | **No**, removed |
| "Your weapons come as cards" subtitle | Static text | **No**, removed (the walkthrough and the NEXT card teach it) |
| "Your deck: 14 weapons" + 14 round cards | Static, tapping a card does nothing | **No**, removed (my addition from the last round; the weapons are in the Book) |
| Title "Choose your kid" | Static | Yes, one line |

Other findings:
- The screen costs one tap before every Practice match even when the kid is the same. A returning player could go straight in (last kid) with a way to change it; that would need a place for the choice other than the main screen (the owner said no face selection there), so it is left as an option: **Play vs bots starts at once and the kid is changed from the Book**.
- Play then goes straight into the street (2 s objective banner, no fly-through of the two teams: removed in the same change).

Result: the screen is the kid row, the portrait and Play.
