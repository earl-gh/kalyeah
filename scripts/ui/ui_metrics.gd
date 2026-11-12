class_name UiMetrics
extends RefCounted
## Shared design pixels at 720px width (2x the 360pt reference phone) and 1280x720 landscape.
## One readable type scale with 44pt minimum touch targets; no in-game text-size modes.
## Paired across KalyeahDisplay (Londrina Solid, SIL OFL) for display/signboard lettering
## and KalyeahSans (Source Sans 3 Variable, SIL OFL) for UI copy, buttons and HUD figures.

## Clean UI and heading steps sized for immediate thumb-distance legibility.
const TITLE: int = 34
const BODY: int = 28
## Home's two hero buttons (owner: 1.25 x BODY).
const HERO_BUTTON: int = 35
const ACTION: int = 26
const DETAIL: int = 24
const CAPTION: int = 22
## Small numbers and captions drawn IN the street or on the bars (the health number over a kid, "Max: 10"), below the reading floor on
## purpose (owner: smaller): they are glanced at, never read. Nothing in a menu uses it.
## The name tag's "Tap to change" (owner: smaller than row 1 and light), below the reading floor on purpose: it only repeats what the whole
## card already says, and the card's screen-reader name carries it. The one menu text under `MIN_TEXT`.
const TAG_HINT: int = 18
## A label carrying this meta is allowed under `MIN_TEXT` (the lint skips it): the owner asked for it, and it is the only one.
const BELOW_FLOOR_BY_OWNER: StringName = &"below_floor_by_owner"
const WORLD: int = 18
## The word over a kid's bar: its name or its status (owner: 16, white with a black outline).
const WORLD_SMALL: int = 16
## The safety floor of the word over a kid's bar, which shrinks until its name or status fits the bar (`OverheadHud.label_font_size`).
const WORLD_MIN: int = 5
const TOUCH: float = 88.0
## The floors the lint test (tests/unit/test_ui_lint.gd) holds every screen to: no text smaller than MIN_TEXT px and
## no tappable control shorter than MIN_TARGET px (the joystick and the weapon buttons are bigger by design).
const MIN_TEXT: int = 22
const MIN_TARGET: float = 80.0
const PAGE_MARGIN: float = 32.0
const PADDING: float = 24.0
const GAP: int = 16
const SECTION_GAP: int = 24
const COMPACT_GAP: int = 12
## Brand emblem proportions (`assets/ui/brand/kalyeah_wordmark.png` 1024x320, `kalyeah_crest.png` 512x512).
const WORDMARK_ASPECT: float = 3.2
const CREST_ASPECT: float = 1.0
## The display scale (docs/ART.md "Shared UI hierarchy"): the only sizes for big lettered words, each step about 1.4x the
## one before so no two read as "almost the same". Lettered in `KalyeahDisplay` with `UiKit.letter`.
const HEADLINE: int = 40
const DISPLAY: int = 56
const HERO: int = 80
const SCORE: int = 120
