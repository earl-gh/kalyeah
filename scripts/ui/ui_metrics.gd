class_name UiMetrics
extends RefCounted
## Shared design pixels at 720px width (2x the 360pt reference phone).
## One readable type scale with 44pt minimum touch targets; no in-game text-size modes.

## Sized for Bungee, which is much wider than a text face: the same room as the old scale in a lighter face.
const TITLE: int = 34
const BODY: int = 28
const ACTION: int = 26
const DETAIL: int = 24
const CAPTION: int = 22
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
