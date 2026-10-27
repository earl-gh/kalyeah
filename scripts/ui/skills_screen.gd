class_name SkillsScreen
extends CardsScreen
## The Skills screen: the weapon cards as their own card pictures, four columns (so 4 + 4 + 4 + 2, the last row from the left), the
## name small under each and the kind (Attack, Control, Block, Heal) as a thin colour tag. A tap opens the skill's drawer with its
## picture, what it does, its figures as bars and the street game behind it.

const COLUMN_COUNT: int = 4
const RULES_PATH: String = "res://data/rules/game_rules.tres"
## The name is small (the caption size, the floor for menu text) and may take two rows ("Gunting" over "Light").
const NAME_SIZE: int = UiMetrics.CAPTION
const NAME_ROWS: int = 2


func _screen_name() -> String:
	return "SkillsScreen"


func _title() -> String:
	return "Skills"


func _subtitle() -> String:
	return "Tap a card to read more."


func _columns() -> int:
	return COLUMN_COUNT


func _ready() -> void:
	super._ready()
	# the owner calls the weapon cards skills; the UI lint bans the word elsewhere (see `CardsHub.WORD_BY_OWNER`)
	(header.find_child("Title", true, false) as Label).set_meta(CardsHub.WORD_BY_OWNER, true)


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
