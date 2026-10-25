class_name TeamColors
extends RefCounted
## The one place for team and weapon-kind colours (UI, HUD and street alike). By default blue and red (and a green bar for
## you); with `Settings.colorblind` blue and orange (the Okabe-Ito pair, apart for every common colour vision) and a
## near-white bar for you, so green against red can never be the only difference. Shapes on the health bars say the same
## thing without colour.

## The flat team colours of the menus, the score card, the results card and the room (blue team first).
const TEAM_BLUE: Color = Color("435bc2")
const TEAM_RED: Color = Color("b83246")
const TEAMS: Array[Color] = [TEAM_BLUE, TEAM_RED]
## The street's team colours: bandanas, rings, base posts and landing marks.
const WORLD_OWN: Color = Color(0.2, 0.5, 1.0)
const WORLD_ALLY: Color = Color(0.45, 0.75, 1.0)
const WORLD_ENEMY: Color = Color(1.0, 0.28, 0.28)
## The health bars over the kids.
const BAR_SELF: Color = Color(0.35, 0.95, 0.35)
const BAR_ALLY: Color = Color(0.35, 0.65, 1.0)
const BAR_ENEMY: Color = Color(1.0, 0.3, 0.3)

const BLIND_OWN: Color = Color("0072b2")
const BLIND_ENEMY: Color = Color("e07b00")
## Your own bar: near white, the brightest of the three and no hue of its own, so it is never read as the enemy's orange.
const BLIND_SELF: Color = Color(0.96, 0.96, 0.96)
## The street's base tints are pulled this far toward a calm grey; colour-blind colours keep their hue (a softened orange read as khaki).
## (0.3 before the visual pass: the bases read as faint grey.)
const BASE_SOFTEN: float = 0.15
const BLIND_BASE_SOFTEN: float = 0.08

## Weapon kinds, one hue each: the solid fill of a kind's chip (white words read on it, at least 4.5:1).
const KIND_ATTACK: Color = Color("c2332e")
const KIND_CONTROL: Color = Color("8a55e8")
const KIND_BLOCK: Color = Color("2068d0")
const KIND_HEAL: Color = Color("1a8340")
const KINDS: Dictionary[int, Color] = {
	WeaponDef.Kind.ATTACK: KIND_ATTACK, WeaponDef.Kind.CROWD_CONTROL: KIND_CONTROL,
	WeaponDef.Kind.BLOCK: KIND_BLOCK, WeaponDef.Kind.HEAL: KIND_HEAL,
}
## How far a kind's bright version (card frames, rims, coloured words on navy) is lightened from its fill.
const KIND_BRIGHT: float = 0.3
## A kind nobody named: slate.
const KIND_NONE: Color = Color("3b4c74")


## The solid fill of a weapon kind (chips).
static func kind_fill(kind: int) -> Color:
	return KINDS.get(kind, KIND_NONE) as Color


## The bright version of a weapon kind: a card's frame, a rim, coloured words on the navy panels.
static func kind_color(kind: int) -> Color:
	return kind_fill(kind).lightened(KIND_BRIGHT)


## The flat blue and red of the score card, the results card and the feed.
static func ui_own() -> Color:
	return BLIND_OWN if Settings.colorblind else TEAM_BLUE


static func ui_enemy() -> Color:
	return BLIND_ENEMY if Settings.colorblind else TEAM_RED


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
	return BLIND_OWN if Settings.colorblind else WORLD_OWN


static func world_ally() -> Color:
	return BLIND_OWN.lightened(0.35) if Settings.colorblind else WORLD_ALLY


static func world_enemy() -> Color:
	return BLIND_ENEMY if Settings.colorblind else WORLD_ENEMY


## How far the street's team tints are pulled toward grey.
static func base_soften() -> float:
	return BLIND_BASE_SOFTEN if Settings.colorblind else BASE_SOFTEN


## The health bars over the kids.
static func bar_self() -> Color:
	return BLIND_SELF if Settings.colorblind else BAR_SELF


static func bar_ally() -> Color:
	return BLIND_OWN.lightened(0.05) if Settings.colorblind else BAR_ALLY


static func bar_enemy() -> Color:
	return BLIND_ENEMY.lightened(0.1) if Settings.colorblind else BAR_ENEMY
