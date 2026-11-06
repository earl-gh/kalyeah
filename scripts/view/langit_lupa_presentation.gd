class_name LangitLupaPresentation
extends RefCounted
## What Langit Lupa adds to the match screen (docs/GDD.md "Langit Lupa"): the brownout (the screen dark but for a circle round your kid; the
## kids out of your sight, their bars and their tag circles hidden), its banners, and the words on the pad. A tag shows the tag toast like
## the other games (no banner); the survival points tick up in the score column without a banner. Reads the sim only.

const BROWNOUT_BANNER: String = "BROWNOUT!"
const LIGHTS_BANNER: String = "LIGHTS ON!"
## The pad's hint while the lights are out (one short line: the pad has room for three).
const DARK_HINT: String = "It's dark! You only see the kids near you."

var owner: PracticeMatch
var game: LangitLupa
var shade: BrownoutShade
var _dark: bool = false


func _init(p_owner: PracticeMatch, p_game: LangitLupa) -> void:
	owner = p_owner
	game = p_game
	shade = BrownoutShade.new()
	owner.add_child(shade)


func sync(delta: float) -> void:
	var dark: bool = game.is_brownout() and owner.sim.phase == MatchSim.Phase.PLAYING
	if dark != _dark:
		_dark = dark
		owner.show_banner(BROWNOUT_BANNER if dark else LIGHTS_BANNER)
	var hidden: Dictionary[int, bool] = {}
	for id: int in owner.sim.players:
		var seen: bool = game.visible_to(owner.sim, owner.local_id, id)
		var view: KidModel = owner._views.get(id) as KidModel
		if view != null:
			view.visible = seen
		if not seen:
			hidden[id] = true
	owner.hud.overhead.hidden_ids = hidden
	var me: PlayerState = owner.sim.players.get(owner.local_id) as PlayerState
	if me == null:
		return
	var at: Vector2 = owner.blend.kid(me)
	var foot: Vector3 = Vector3(at.x, 0.0, at.y)
	var hole: Vector2 = owner._camera.screen_position(foot)
	var edge: Vector2 = owner._camera.screen_position(foot + Vector3(game.rules.brownout_sight, 0.0, 0.0))
	shade.show_state(dark, hole, hole.distance_to(edge), delta)


## What your kid is to do now, for the line on the pad: [what, how].
static func status(sim: MatchSim, player: PlayerState) -> Array[String]:
	var langit: LangitLupa = sim.mode as LangitLupa
	var dark: bool = langit.is_brownout()
	if player.id == langit.taya_id:
		return ["YOU'RE IT!", DARK_HINT if dark else "Tag a kid on the street. Kids on a langit are safe for a while."] as Array[String]
	if langit.is_safe(player):
		return ["SAFE!", UiText.f("Safe for %d s. You don't rest up here.", [ceili(langit.safe_left.get(player.id, 0.0))])] as Array[String]
	if langit.on_langit(player):
		return ["GET DOWN!", "Your safe time is up: the street fills it again."] as Array[String]
	return ["RUN!", DARK_HINT if dark else "Get on a langit, or press LUPA to raise your own."] as Array[String]
