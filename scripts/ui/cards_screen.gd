class_name CardsScreen
extends VBoxContainer
## An inline five-column card grid. Its parent owns scrolling; cells open shared drawer details.

signal drawer_requested(screen: CardsScreen, index: int)

const GRID_GAP: int = UiMetrics.GAP
const GRID_AIR: int = 8
const COLUMNS: int = 5

var slots: int = 0
var entries: Array[Dictionary] = []
var cells: Array[CardCell] = []
var scroll: ScrollContainer
var grid: GridContainer
var selected: int = -1


func _ready() -> void:
	name = _screen_name()
	mouse_filter = Control.MOUSE_FILTER_PASS
	var air: MarginContainer = MarginContainer.new()
	air.mouse_filter = Control.MOUSE_FILTER_PASS
	air.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	for side: String in ["top", "bottom"]:
		air.add_theme_constant_override("margin_" + side, GRID_AIR)
	add_child(air)
	grid = GridContainer.new()
	grid.name = "Grid"
	grid.columns = COLUMNS
	grid.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	grid.add_theme_constant_override("h_separation", GRID_GAP)
	grid.add_theme_constant_override("v_separation", GRID_GAP)
	air.add_child(grid)
	entries = _entries()
	for slot: int in range(entries.size(), slots):
		entries.append({"id": StringName("unknown_%d" % slot), "name": "?", "picture": CharactersPage.card_picture(&"junjun"), "tag": null, "access": "Unknown card: coming soon", "locked": true})
	for i: int in entries.size():
		entries[i]["compact"] = true
		var cell: CardCell = CardCell.create(entries[i])
		cell.pressed.connect(_open.bind(i))
		grid.add_child(cell)
		cells.append(cell)
	grid.resized.connect(_fit_cells)
	_fit_cells()


func _screen_name() -> String:
	return "CardsScreen"


func _entries() -> Array[Dictionary]:
	return []


func column_width() -> float:
	return maxf(1.0, (grid.size.x - float(GRID_GAP) * float(COLUMNS - 1)) / float(COLUMNS))


func _fit_cells() -> void:
	for cell: CardCell in cells:
		cell.fit(column_width())


func _open(index: int) -> void:
	if not bool(entries[index].get("locked", false)):
		drawer_requested.emit(self, index)


func mark(index: int) -> void:
	selected = index
	for i: int in cells.size():
		cells[i].set_selected(i == index)
	if scroll != null and index >= 0 and index < cells.size():
		scroll.ensure_control_visible.call_deferred(cells[index])
