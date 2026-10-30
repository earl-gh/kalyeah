extends GutTest
## The effects that carry meaning in play must not read as subtle on a phone (docs/ART.md "Effect scale"). At 720 px wide the
## camera shows a metre as 18 px along the street and 26 px across it, and a kid (drawn twice life size, 3.7 m) is about 70 px tall.
## A hit star, a landing puff, a heal ring or a smack arc under about a third of a kid (23 px, 1.2 m at the street's scale) or
## alive under about 100 ms was measured as too small; these tests pin the new sizes so a tweak cannot quietly shrink them back.

## Pixels per metre along the street (the smaller of the two scales) and a kid's height in pixels, at 720 px wide.
const PX_PER_M: float = 18.0
const KID_PX: float = 67.0
## Smallest on-screen size of an effect that confirms something (a hit, a landing, a heal, a block): this share of a kid.
const MEANINGFUL: float = 0.3
## Shortest time a meaningful effect is on screen (seconds).
const MIN_SHOWN: float = 0.1


func _px(metres: float) -> float:
	return metres * PX_PER_M


func test_particles_that_confirm_something_are_big_enough_and_live_long_enough() -> void:
	var table: Dictionary[int, String] = {
		FxParticles.Preset.STARS: "the hit stars",
		FxParticles.Preset.PLUS: "the heal sparkles",
		FxParticles.Preset.IMPACT: "the meteor's ring of dust",
		FxParticles.Preset.SMOKE: "the can's comic smoke",
		FxParticles.Preset.DUST: "the dust at the feet",
		FxParticles.Preset.PAPER: "the paper bits",
		FxParticles.Preset.SOIL: "the soil clods",
	}
	for preset: int in table:
		var spec: FxParticles.Spec = FxParticles.spec(preset)
		assert_gte(_px(spec.size), KID_PX * MEANINGFUL * 0.6, "%s: %.0f px at its fullest is not a speck" % [table[preset], _px(spec.size)])
		assert_gte(spec.lifetime, MIN_SHOWN * 3.0, "%s are on screen at least 0.3 s" % table[preset])
	assert_gte(FxParticles.spec(FxParticles.Preset.STARS).size, 1.0, "a hit star is at least a metre across (it was 0.7)")
	assert_gte(FxParticles.spec(FxParticles.Preset.SMOKE).size, 1.5)
	assert_gte(FxParticles.spec(FxParticles.Preset.DUST).size, 0.9, "dust was 0.6")


func test_the_rock_chips_are_bigger_than_a_pebble() -> void:
	var chips: FxParticles.Spec = FxParticles.spec(FxParticles.Preset.CHIPS)
	var natural: Vector3 = chips.mesh.get_aabb().size
	var longest: float = chips.size * maxf(natural.x, maxf(natural.y, natural.z))
	assert_almost_eq(longest, FxParticles.CHIP_SIZE, 0.001, "the chip's longest side is CHIP_SIZE")
	assert_gte(FxParticles.CHIP_SIZE, 0.4, "chips were 0.25 m (6 px)")


func test_ground_rings_have_a_line_you_can_see() -> void:
	assert_gte(_px(FxKit.RING_WIDTH * FxHeals.RING_TO), 2.5, "the heal ring's line is at least 3 px wide at its widest")
	assert_gte(FxKit.RING_WIDTH, 0.12, "a unit ring's line (it was a 0.08 hairline)")
	assert_gte(FxHeals.RING_TO, 1.5, "the heal ring reaches a kid and a half's width")
	assert_gte(_px(FxHeals.RING_FROM * 2.0), KID_PX * 0.4, "and starts at least two fifths of a kid across")


func test_smears_are_wide_enough_to_find() -> void:
	assert_gte(FxRelease.WIDTH, 1.3, "the release trail is about a kid's body wide (it was 0.9 m, partly hidden by the kid)")
	assert_gte(FxSlippers.SLAP_ARC, 1.0, "the slap arc (it was 0.8 m)")
	assert_gte(FxWalls.BLOCK_STAR, 1.2, "the block flash on a paper wall (it was 0.8 m, three frames)")
	assert_gte(FxKit.SMEAR_ARC_WIDTH, 0.65, "a swing's crescent, per metre of radius")
	assert_gte(FxRelease.HAND_CLEAR, 1.2, "the release trail starts clear of the thrower's drawn body")
	assert_gte(FxSquash.TRAIL_FRAMES * FxSquash.FRAME, MIN_SHOWN * 2.0, "a trail is on screen at least 200 ms")
