class_name CardsScreen
extends Control
## A full screen of cards behind a Cards hub card (Kids, Skills): a Back button with the page's title, then a scrolling grid of the
## owner's bare card pictures, each with its name under it. Tapping one asks for the bottom drawer with its details (`drawer_requested`);
## the screen decides nothing about what the card says. Kids and Skills only differ in the columns, the words and the cards
## (`_entries`).

signal back_pressed
## A card was tapped: `index` is its place in `entries` (the drawer shows that list and can step through it).
signal drawer_requested(screen: CardsScreen, index: int)

## The gap between cards is a UiMetrics step; the grid gets a little air above its first row so a gold edge is not cut by the scroll.
const GRID_GAP: int = UiMetrics.GAP
const GRID_AIR: int = 8
const BACK_SIZE: Vector2 = Vector2(160.0, UiMetrics.TOUCH)

## What every card is: {id, name, picture, tag (Color or null), access, name_lines, name_size, content (Callable -> Array[Control])}.
var entries: Array[Dictionary] = []
var cells: Array[CardCell] = []
var back_button: Button
var scroll: ScrollContainer
var grid: GridContainer
var header: Control
## The card whose drawer is open, or -1.
var selected: int = -1

var _column: VBoxContainer


func _ready() -> void:
	name = _screen_name()
	_column = VBoxContainer.new()
	_column.name = "Column"
	_column.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_column.add_theme_constant_override("separation", UiMetrics.SECTION_GAP)
	add_child(_column)
	var top: HBoxContainer = HBoxContainer.new()
	top.name = "Top"
	top.add_theme_constant_override("separation", UiMetrics.GAP)
	_column.add_child(top)
	back_button = MenuDialog.make_button("Back", "grey", UiMetrics.ACTION, BACK_SIZE)
	back_button.name = "BackButton"
	back_button.accessibility_name = "Back to Cards"
	back_button.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	back_button.pressed.connect(back_pressed.emit)
	top.add_child(back_button)
	header = UiKit.page_header(_title(), _subtitle())
	header.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	top.add_child(header)
	scroll = ScrollContainer.new()
	scroll.name = "Scroll"
	scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	_column.add_child(scroll)
	var air: MarginContainer = MarginContainer.new()
	air.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	for side: String in ["top", "bottom"]:
		air.add_theme_constant_override("margin_" + side, GRID_AIR)
	scroll.add_child(air)
	grid = GridContainer.new()
	grid.name = "Grid"
	grid.columns = _columns()
	grid.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	grid.add_theme_constant_override("h_separation", GRID_GAP)
	grid.add_theme_constant_override("v_separation", GRID_GAP)
	air.add_child(grid)
	entries = _entries()
	for i: int in entries.size():
		var cell: CardCell = CardCell.create(entries[i])
		cell.pressed.connect(_open.bind(i))
		grid.add_child(cell)
		cells.append(cell)
	grid.resized.connect(_fit_cells)
	_fit_cells()


# ---- what Kids and Skills say differently ----------------------------------------------

func _screen_name() -> String:
	return "CardsScreen"


func _title() -> String:
	return ""


func _subtitle() -> String:
	return ""


func _columns() -> int:
	return 2


func _entries() -> Array[Dictionary]:
	return []


# ---- the grid --------------------------------------------------------------------------

## Every column is as wide as the grid allows (`columns` of them with a gap between).
func column_width() -> float:
	var columns: int = grid.columns
	return maxf(1.0, (grid.size.x - float(GRID_GAP) * float(columns - 1)) / float(columns))


func _fit_cells() -> void:
	var width: float = column_width()
	for cell: CardCell in cells:
		cell.fit(width)


func _open(index: int) -> void:
	drawer_requested.emit(self, index)


## Marks card `index` as the one whose drawer is open (`-1`: none): it wears the gold edge and is scrolled into view.
func mark(index: int) -> void:
	selected = index
	for i: int in cells.size():
		cells[i].set_selected(i == index)
	if index >= 0 and index < cells.size():
		scroll.ensure_control_visible.call_deferred(cells[index])
