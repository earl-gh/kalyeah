class_name TeamColors
extends RefCounted
## The one place the match asks "what colour is my team, and theirs". By default blue and red (and a green bar for you); with
## `Settings.colorblind` blue and orange (the Okabe-Ito pair, apart for every common colour vision) and a near-white bar for you,
## so green against red can never be the only difference.

const BLIND_OWN: Color = Color("0072b2")
const BLIND_ENEMY: Color = Color("e07b00")
## Your own bar: near white, the brightest of the three and no hue of its own, so it is never read as the enemy's orange.
const BLIND_SELF: Color = Color(0.96, 0.96, 0.96)
## The score blocks' blue and red (owner: vivid and natural like Clash Royale's, not the muted UI pair above).
const SCORE_BLUE: Color = Color(0.13, 0.52, 1.0)
const SCORE_RED: Color = Color(1.0, 0.24, 0.27)


## The flat blue and red of the score card, the results card and the feed.
static func ui_own() -> Color:
	return BLIND_OWN if Settings.colorblind else KalyeahTheme.TEAM_BLUE


static func score_own() -> Color:
	return BLIND_OWN if Settings.colorblind else SCORE_BLUE


static func score_enemy() -> Color:
	return BLIND_ENEMY if Settings.colorblind else SCORE_RED


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


## The health bars over the kids.
static func bar_self() -> Color:
	return BLIND_SELF if Settings.colorblind else OverheadHud.SELF_COLOR


static func bar_ally() -> Color:
	return BLIND_OWN.lightened(0.05) if Settings.colorblind else OverheadHud.ALLY_COLOR


static func bar_enemy() -> Color:
	return BLIND_ENEMY.lightened(0.1) if Settings.colorblind else OverheadHud.ENEMY_COLOR
