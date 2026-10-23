class_name TeamColors
extends RefCounted
## The one place the match asks "what colour is my team, and theirs". By default blue and red (and a green bar for you); with
## `Settings.colorblind` blue and orange (the Okabe-Ito pair, apart for every common colour vision) and a near-white bar for you,
## so green against red can never be the only difference. Shapes on the health bars say the same thing without colour.

const BLIND_OWN: Color = Color("0072b2")
const BLIND_ENEMY: Color = Color("e07b00")
## Your own bar: near white, the brightest of the three and no hue of its own, so it is never read as the enemy's orange.
const BLIND_SELF: Color = Color(0.96, 0.96, 0.96)
## The street's base tints are pulled this far toward a calm grey; colour-blind colours keep their hue (a softened orange read as khaki).
const BASE_SOFTEN: float = 0.3
const BLIND_BASE_SOFTEN: float = 0.08


## The flat blue and red of the score card, the results card and the feed.
static func ui_own() -> Color:
	return BLIND_OWN if Settings.colorblind else KalyeahTheme.TEAM_BLUE


static func ui_enemy() -> Color:
	return BLIND_ENEMY if Settings.colorblind else KalyeahTheme.TEAM_RED


## The pills of the TAGGED! and RESCUED! feed.
static func feed_own() -> Color:
	return Color(BLIND_OWN, 0.92) if Settings.colorblind else Color(0.2, 0.42, 0.85, 0.92)


static func feed_enemy() -> Color:
	return Color(BLIND_ENEMY, 0.92) if Settings.colorblind else Color(0.82, 0.22, 0.2, 0.92)


## Lighter versions for the capture bar along the score card.
static func capture_own() -> Color:
	return BLIND_OWN.lightened(0.5) if Settings.colorblind else Color(0.62, 0.8, 1.0)


static func capture_enemy() -> Color:
	return BLIND_ENEMY.lightened(0.5) if Settings.colorblind else Color(1.0, 0.62, 0.6)


## The street: base tints, rescue areas and the ring under each kid.
static func world_own() -> Color:
	return BLIND_OWN if Settings.colorblind else Palette.TEAM_OWN


static func world_ally() -> Color:
	return BLIND_OWN.lightened(0.35) if Settings.colorblind else Palette.TEAM_ALLY


static func world_enemy() -> Color:
	return BLIND_ENEMY if Settings.colorblind else Palette.TEAM_ENEMY


## How far the street's team tints are pulled toward grey.
static func base_soften() -> float:
	return BLIND_BASE_SOFTEN if Settings.colorblind else BASE_SOFTEN


## The health bars over the kids.
static func bar_self() -> Color:
	return BLIND_SELF if Settings.colorblind else OverheadHud.SELF_COLOR


static func bar_ally() -> Color:
	return BLIND_OWN.lightened(0.05) if Settings.colorblind else OverheadHud.ALLY_COLOR


static func bar_enemy() -> Color:
	return BLIND_ENEMY.lightened(0.1) if Settings.colorblind else OverheadHud.ENEMY_COLOR
