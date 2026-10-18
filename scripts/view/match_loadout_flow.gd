class_name MatchLoadoutFlow
extends RefCounted
## Loadout presentation preserves server-owned lobby choices in networked matches.

static func open(owner: PracticeMatch, heading: String, current: Array[StringName]) -> void:
	# Online preserves the room choice; Practice starts with the saved profile preference.
	var mine: StringName = owner.sim.players[owner.local_id].character_id if owner.online else StringName(Session.avatar_id)
	if not owner.rules.characters.any(func(c: CharacterDef) -> bool: return c.id == mine):
		mine = owner.sim.players[owner.local_id].character_id
	owner._pick_screen.open(WeaponPick.new(_weapon_ids(owner), owner.rules.weapon_pick_time, current), owner.rules.weapons, heading, owner.sim.rng, owner.rules.characters, mine)
	owner._set_controls_active(false)


static func open_swap(owner: PracticeMatch) -> void:
	var player: PlayerState = owner.sim.players[owner.local_id]
	owner._pick_screen.open_swap(WeaponPick.new(_weapon_ids(owner), 0.0, player.weapons), owner.rules.weapons, "CHANGE WEAPONS")
	owner._set_controls_active(false)


static func _weapon_ids(owner: PracticeMatch) -> Array[StringName]:
	var ids: Array[StringName] = []
	for def: WeaponDef in owner.rules.weapons:
		ids.append(def.id)
	return ids


static func confirm(owner: PracticeMatch, picks: Array[StringName]) -> void:
	owner._audio.start_music()
	owner.sim.set_loadout(owner.local_id, picks[0], picks[1])
	var kid: StringName = owner._pick_screen.character_id
	if kid != &"":
		owner.sim.set_character(owner.local_id, kid)
		# the kid you pick is also your face on the main screen from now on
		Session.avatar_id = String(kid)
		Session.save()
		owner._presentation.refresh_kids()
	if owner.online:
		# the server starts play for everyone once all have picked (or time is up)
		owner.client.pick_weapons(picks[0], picks[1], kid)
		if owner.client.stage != "play":
			owner.show_banner("Waiting for the other players...")
			return
	owner._opening_pick = false
	owner.sim.begin_intro()
	owner._set_controls_active(true)
	owner._presentation.sync(0.0)


static func swap(owner: PracticeMatch, picks: Array[StringName]) -> void:
	if owner.online:
		owner.client.swap_weapons(picks[0], picks[1])
		owner.sim.players[owner.local_id].weapons.assign(picks)
	else:
		owner.sim.swap_loadout(owner.local_id, picks[0], picks[1])
	owner._presentation.sync(0.0)


