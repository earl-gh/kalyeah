class_name SkillsScreen
extends CardsScreen
## The inline skill catalog; entries reuse the owner's pictures and authored descriptions.

const RULES_PATH: String = "res://data/rules/game_rules.tres"
## The name is small (the caption size, the floor for menu text) and may take two rows ("Gunting" over "Light").
const NAME_SIZE: int = UiMetrics.CAPTION
const NAME_ROWS: int = 2


func _screen_name() -> String:
	return "SkillsScreen"


func _entries() -> Array[Dictionary]:
	var found: Array[Dictionary] = []
	for weapon: WeaponDef in (load(RULES_PATH) as GameRules).weapons:
		var art: Texture2D = CardStyle.picture(weapon.id)
		if art == null and ResourceLoader.exists(WeaponsPage.ICON_PATH % String(weapon.id)):
			art = load(WeaponsPage.ICON_PATH % String(weapon.id)) as Texture2D
		found.append({
			"id": weapon.id, "name": weapon.display_name, "picture": art,
			"tag": WeaponsPage.KIND_COLORS.get(weapon.kind, UiKit.CHIP_SLATE) as Color,
			"access": "%s, %s: read about this skill" % [weapon.display_name, weapon.kind_label()],
			"name_lines": NAME_ROWS, "name_size": NAME_SIZE,
			"content": DrawerContent.of_weapon.bind(weapon),
		})
	return found
