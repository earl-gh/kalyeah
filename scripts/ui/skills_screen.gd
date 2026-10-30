class_name SkillsScreen
extends CardsScreen
## The inline skill catalog; entries reuse the owner's pictures and authored descriptions.

const RULES_PATH: String = "res://data/rules/game_rules.tres"
## The name is small (the caption size, the floor for menu text) and may take two rows ("Gunting" over "Light").
const NAME_SIZE: int = UiMetrics.CAPTION
const NAME_ROWS: int = 2
## The skill whose card the locked "Coming Soon" card is drawn from (owner: not the Junjun kid card).
const UNKNOWN_BASE: StringName = &"bato_light"


func _screen_name() -> String:
	return "SkillsScreen"


## The locked card of the Skills grid is the light rock's card, dimmed (not a kid's card).
func _unknown_picture() -> Texture2D:
	return _art(UNKNOWN_BASE)


static func _art(id: StringName) -> Texture2D:
	var art: Texture2D = CardStyle.picture(id)
	if art == null and ResourceLoader.exists(WeaponsPage.ICON_PATH % String(id)):
		art = load(WeaponsPage.ICON_PATH % String(id)) as Texture2D
	return art


func _entries() -> Array[Dictionary]:
	var found: Array[Dictionary] = []
	for weapon: WeaponDef in (load(RULES_PATH) as GameRules).weapons:
		var art: Texture2D = _art(weapon.id)
		found.append({
			"id": weapon.id, "name": weapon.display_name, "picture": art,
			"tag": WeaponsPage.KIND_COLORS.get(weapon.kind, UiKit.CHIP_SLATE) as Color,
			"access": "%s, %s: read about this skill" % [weapon.display_name, weapon.kind_label()],
			"name_lines": NAME_ROWS, "name_size": NAME_SIZE,
			"content": DrawerContent.of_weapon.bind(weapon),
		})
	return found
