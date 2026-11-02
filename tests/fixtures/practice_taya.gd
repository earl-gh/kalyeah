extends RefCounted
## A Practice match for the tests that need the player's kid NOT to be Tumbang Preso's first taya. The match draws the first taya at random
## (docs/GDD.md), and when it is the player's kid the street starts seen from the far end (the taya's view), which these tests do not expect.
## A match whose draw picked the player is thrown away and made again (one in six does), so no test depends on luck.


## The match built from `scene`, in `host`, stepped by hand, past the pick and the opening show. `prepare` (if valid) is called with the
## match before the pick finishes. Not autofreed: the caller does that.
static func make(host: Node, scene: PackedScene, prepare: Callable = Callable()) -> PracticeMatch:
	var practice: PracticeMatch = null
	for attempt: int in 40:
		practice = scene.instantiate() as PracticeMatch
		host.add_child(practice)
		practice.set_process(false)
		if prepare.is_valid():
			prepare.call(practice)
		practice.finish_pick()
		practice.skip_intro()
		var game: TumbangPreso = practice.sim.mode as TumbangPreso
		if game == null or game.taya_id != practice.local_id:
			break
		host.remove_child(practice)
		practice.free()
		practice = null
	return practice
