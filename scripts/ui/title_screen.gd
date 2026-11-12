class_name TitleScreen
extends Control
## Modern MOBA Main Screen benchmarked against LoL: Wild Rift and Mobile Legends: Bang Bang.
## At 1280x720 landscape, 70%+ of the viewport breathes as an unobstructed, full-bleed interactive
## 3D Barangay Hero Showcase Stage framed by a sleek 4-zone glassmorphic & radiant-gold layout:
## 1) Slim Translucent Top Header (Player Profile + Rank LP capsule + KALYEAH brand lockup | Utility cluster)
## 2) Frameless Left Vertical Navigation Rail (LARO, MGA BATA, ARMAS, RANGGO)
## 3) Lower-Left Floating Kid Watermark Callout (Filipino street epithet + role + live < / > 3D switcher)
## 4) Bottom-Right Signature Play Cluster (Mode pills + Street Mode / Join selector + Chamfered Gold MAGLARO CTA)
## Preserves 720x1280 portrait layout & all public contracts/signals for deterministic navigation and tests.

signal play_pressed
signal practice_pressed
signal loadout_pressed
signal ranked_pressed
signal history_pressed
signal settings_pressed
signal hotspot_host_pressed
signal hotspot_join_pressed
signal join_code_pressed

const SERVER_SCENE: String = "res://server/main.tscn"
const LOBBY_SCENE: String = "res://scenes/ui/lobby.tscn"
const PRACTICE_SCENE: String = "res://scenes/match/practice.tscn"
const RANKED_SYSTEM_PATH: String = "res://scripts/sim/ranked_system.gd"
const BRAND_WORDMARK_PATH: String = "res://assets/ui/brand/kalyeah_wordmark.png"
const BRAND_CREST_PATH: String = "res://assets/ui/brand/kalyeah_crest.png"
const SKY_SHADER: Shader = preload("res://shaders/tropical_sky.gdshader")
const CEMENT_SHADER: Shader = preload("res://shaders/street_cement.gdshader")
const LOGO: Texture2D = preload("res://assets/ui/kalyeah_logo.png")
const TIPS_ICON: Texture2D = preload("res://assets/ui/tips_icon.png")
const MAIL_ICON: Texture2D = preload("res://assets/ui/mail_icon.png")
const SUNLIT_CREAM: Color = Color("faf3e3")
const SUNLIT_ESPRESSO: Color = Color("2a2522")
const SUNLIT_MANGO: Color = Color("f5a623")
const SUNLIT_JADE: Color = Color("1f9d8b")
const SUNLIT_JADE_TEXT: Color = Color("156e61")

## Canonical 90s Filipino street epithets, tactical archetypes, and signature aura tints for all 10 kids.
const KID_SHOWCASE_META: Dictionary = {
	"junjun": {"epithet": "KAPITAN KUMOT", "role": "FRONTLINER", "accent": Color("f04e3e")},
	"ligaya": {"epithet": "KUMANDER TEKS", "role": "TACTICIAN", "accent": Color("ff8c42")},
	"migo": {"epithet": "PLASTIK-BALLOON", "role": "ZONER", "accent": Color("36c9b8")},
	"toni": {"epithet": "BARANGAY LIGA MVP", "role": "DUELIST", "accent": Color("f5a623")},
	"popoy": {"epithet": "TANGKE NG TANGHALI", "role": "GUARDIAN", "accent": Color("4aa0ff")},
	"inday": {"epithet": "REYNA NG PATINTERO", "role": "RUNNER", "accent": Color("ffd15c")},
	"kiko": {"epithet": "TSINELAS SPRINTER", "role": "SKIRMISHER", "accent": Color("28c7a8")},
	"nene": {"epithet": "TIRADOR SCOUT", "role": "MARKSMAN", "accent": Color("f78ca2")},
	"buknoy": {"epithet": "MEKANIKO NG KOLONG", "role": "BRAWLER", "accent": Color("e07a38")},
	"tala": {"epithet": "KOMIKS PINTOR", "role": "ARTILLERY", "accent": Color("b37cf5")},
}

## The game modes in card order: Online, Local in the middle (the default, `Session.menu_mode` 1), Practice (owner).
const MODES: Array[Dictionary] = [
	{"id": "online", "bare": true, "plate": Rect2(120.0, 618.0, 528.0, 72.0), "title": "Online", "color": "orange", "accent": "45dd78", "power": 7450,
		"text": "Invite friends with a room code!"},
	{"id": "hotspot", "bare": true, "plate": Rect2(120.0, 618.0, 528.0, 76.0), "title": "Local", "color": "blue", "accent": "4aa0ff", "power": 9999,
		"text": "Battle your friends nearby!"},
	{"id": "practice", "bare": true, "plate": Rect2(120.0, 636.0, 528.0, 58.0), "title": "Practice", "color": "green", "accent": "a45cf0", "power": 1200,
		"text": "Take on 5v5 Barangay Bots!"},
]
## The bottom tabs, left to right (used in portrait layout & preserved for tab navigation contracts).
const TABS: Array[Dictionary] = [
	{"id": &"street", "label": "Games", "icon": &"street"},
	{"id": &"codex", "label": "Cards", "icon": &"cards"},
	{"id": &"home", "label": "Play", "icon": &"play"},
	{"id": &"history", "label": "History", "icon": &"history"},
	{"id": &"settings", "label": "Settings", "icon": &"settings"},
]
## Left vertical navigation rail definitions for 1280x720 widescreen.
const RAIL_ITEMS: Array[Dictionary] = [
	{"id": &"play", "title": "LARO", "sub": "PLAY", "glyph": &"play", "a11y": "Play home"},
	{"id": &"kids", "title": "MGA BATA", "sub": "KIDS", "glyph": &"kids", "a11y": "Kids roster"},
	{"id": &"arsenal", "title": "ARMAS", "sub": "ARSENAL", "glyph": &"arsenal", "a11y": "Weapons arsenal"},
	{"id": &"ranked", "title": "RANGGO", "sub": "RANKED", "glyph": &"ranked", "a11y": "Ranked and match history"},
]

const HERO_CARD: Vector2 = Vector2(400.0, 571.0)
const HERO_RATIO: float = 0.7
const MODEL_RATIO: float = 768.0 / 717.0
const MODEL_SIDE_ROOM: float = 40.0
const MODEL_SCALE: float = 0.64
const MODEL_BACK_RISE: float = 0.25
const MODEL_SPREAD_SCALE: float = 1.73
const MODEL_BACK_SCALE: float = 0.75
const HERO_MIN_HEIGHT: float = 440.0
const HERO_MAX_HEIGHT: float = 780.0
const HERO_AIR: float = 36.0
const HERO_BOTTOM_GAP: float = 14.0
const TITLES_SEPARATION: int = -10
const BACKLIGHT_REACH: float = 1.15
const MODE_TEXT_OUTLINE: int = 4
const HERO_SCALE: float = 0.86
const ACTIONS_SLOT_SHARE: float = 0.35
const TAG_AIR: float = 10.0
const TAG_MIN_SCALE: float = 0.6
const TAG_DROP: float = 0.0
const TAG_OVERSHOOT: float = 60.0
const TEXT_Z: int = 8
const AMBIENT_BOB: float = 12.0
const PAGE_RANK: Dictionary[StringName, int] = {&"street": -2, &"codex": -1, &"": 0, &"history": 1, &"settings": 2}
const LOGO_HEIGHT: float = UiKit.PAGE_HEADER_ROW_HEIGHT
const LOGO_DROP: float = UiKit.PAGE_HEADER_DROP
const TIPS_INSET: float = 22.0
const TOP_ICON_SIZE: float = 96.0
const SIDE_MARGIN: float = 28.0
const PAGE_SIDE_MARGIN: float = 40.0
const TOP_MARGIN: float = 20.0
const CTA_HEIGHT: float = 117.0
const CTA_MIN_WIDTH: float = 240.0
const CTA_FONT: int = UiMetrics.HERO_BUTTON
const CTA_PADDING: float = 56.0

## Widescreen 1280x720 4-zone spatial constants (zero overlap, <= 0.35 area share, >= 80x80 BaseButton hitboxes).
const WIDE_SIDE_INSET: float = 22.0
const WIDE_BOTTOM_INSET: float = 12.0
const WIDE_HEADER_TOP: float = 4.0
const WIDE_HEADER_VISUAL_H: float = 74.0
const WIDE_HIT_FLOOR: float = UiMetrics.MIN_TARGET
const WIDE_RAIL_WIDTH: float = 176.0
const WIDE_RAIL_H: float = 326.0
const WIDE_PAGE_LEFT: float = 218.0
const WIDE_PAGE_TOP: float = 90.0
const WIDE_CALLOUT_W: float = 384.0
const WIDE_CALLOUT_H: float = 158.0
const WIDE_CLUSTER_W: float = 412.0
const WIDE_CTA_H: float = 88.0
const WIDE_CLUSTER_H: float = 252.0
const WIDE_HERO_MIN_HEIGHT: float = 224.0
const WIDE_HERO_MAX_HEIGHT: float = 282.0
const WIDE_HERO_AIR: float = 24.0
const WIDE_NAMETAG_SCALE: float = 0.72
const WIDE_TAB_WIDTH: float = 720.0
const WIDE_TAB_HEIGHT: float = 148.0
const WIDE_DOCK_HEIGHT: float = 92.0
const WIDE_LOGO_HEIGHT: float = 88.0
const WIDE_TOP_ICON_SIZE: float = 84.0
const WIDE_TOP_MARGIN: float = 8.0
const WIDE_CTA_HEIGHT: float = 90.0
const WIDE_PRACTICE_WIDTH: float = 480.0
const RANK_CREST_SIZE: Vector2 = Vector2(376.0, 92.0)

const PULSE_DELAY: float = 1.2
const PULSE_SCALE: float = 1.025
const PULSE_HALF: float = 0.9

## The screen asks for the game to close (replaced in tests).
var quit_action: Callable = _quit_game
## The screen leaves for the room scene (replaced in tests).
var open_lobby_action: Callable = _change_to_lobby

var backdrop: UiBackdrop
var hero_stage: _HeroShowcaseStage
var carousel: MenuCarousel
var parallax: HomeParallax
var name_tag: NameTag
var rank_crest: Control
var mode_models: Array[Button]:
	get:
		return carousel.cards
var tab_bar: UiTabBar
var mode_text_label: Label
var primary_button: Button
var secondary_button: Button
var practice_row: PracticeRow
var dialog: MenuDialog
var kid_pick: WeaponPickScreen
var corner_buttons: Dictionary[StringName, Button] = {}
var pages: Dictionary[StringName, Control] = {}
var current_page: StringName = &""
var street_page: StreetPage
var codex_page: CodexPage
var card_drawer: CardDrawer
var _drawer_screen: CardsScreen
var history_page: HistoryPage
var settings_page: SettingsPage

## Widescreen 4-zone modern MOBA stage HUD nodes.
var stage_hud: Control
var top_header: Control
var profile_capsule: _ProfileCapsuleButton
var nav_rail: VBoxContainer
var nav_rail_buttons: Dictionary[StringName, _WildRiftNavTab] = {}
var kid_callout: Control
var action_cluster: Control
var cta_button: _WildRiftGoldCtaButton
var mode_selector_button: _ChamferedGlassButton
var sub_action_button: _ChamferedGlassButton
var _mode_pills: Dictionary[int, _ModeSegmentPill] = {}
var _status_pill_label: Label
var _status_dot: _StatusDot
var _kid_epithet_label: Label
var _kid_name_label: Label
var _kid_roster_button: _ChamferedGlassButton
var _active_rail_id: StringName = &"play"
var _stage_dragging: bool = false
var _stage_last_x: float = 0.0

var loading_screen: BootScreen
var _loading: bool = false
var _margin: MarginContainer
var _shell: VBoxContainer
var _home: VBoxContainer
var _top_bar: Control
var _actions_slot: CenterContainer
var _actions_box: BoxContainer
var _nav_dock: Control
var tips_button: Button
var mail_button: Button
var _wide_tips_button: _GlassIconButton
var _wide_mail_button: _GlassIconButton
var _wide_history_button: _GlassIconButton
var _wide_settings_button: _GlassIconButton
var _page_holder: Control
var swipe_area: Control
var _page_tween: Tween
var _pulse: Tween
var _pulse_generation: int = 0


static func is_server_run() -> bool:
	return OS.has_feature("dedicated_server") or OS.get_cmdline_user_args().has("--server")


func _ready() -> void:
	if is_server_run():
		get_tree().change_scene_to_file.call_deferred(SERVER_SCENE)
		return
	Session.ensure_profile()
	Net.stop_host()
	Net.local.leave()
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	backdrop = UiBackdrop.new()
	add_child(backdrop)
	_build_layout()
	refresh_rank_crest()
	_sync_showcase_kid()
	resized.connect(_apply_safe_area)
	_apply_safe_area()
	_refresh_mode()
	carousel.selected.connect(_on_mode_selected)
	carousel.laid_out.connect(_aim_backlight)
	carousel.swiping_changed.connect(_on_swiping_changed)
	_aim_backlight.call_deferred()
	primary_button.pressed.connect(_on_primary_pressed)
	primary_button.button_down.connect(_stop_pulse)
	primary_button.button_up.connect(_start_pulse)
	secondary_button.pressed.connect(_on_secondary_pressed)
	if cta_button != null:
		cta_button.pressed.connect(_on_primary_pressed)
		cta_button.button_down.connect(_stop_pulse)
		cta_button.button_up.connect(_start_pulse)
	get_viewport().size_changed.connect(_apply_safe_area)
	Settings.accessibility_changed.connect(_on_accessibility_changed)
	_pop_in.call_deferred()


# ---- layout ---------------------------------------------------------------------------

func _build_layout() -> void:
	hero_stage = _HeroShowcaseStage.new()
	hero_stage.name = "HeroStage"
	hero_stage.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	hero_stage.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(hero_stage)

	swipe_area = Control.new()
	swipe_area.name = "SwipeArea"
	swipe_area.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	swipe_area.mouse_filter = Control.MOUSE_FILTER_STOP
	swipe_area.gui_input.connect(_on_swipe_area_input)
	add_child(swipe_area)

	_margin = MarginContainer.new()
	_margin.name = "Safe"
	_margin.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_margin.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_margin)

	var shell: VBoxContainer = VBoxContainer.new()
	shell.name = "Shell"
	shell.add_theme_constant_override("separation", UiMetrics.COMPACT_GAP)
	shell.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_margin.add_child(shell)
	_shell = shell

	_top_bar = _build_top_bar()
	shell.add_child(_top_bar)

	var middle: Control = Control.new()
	middle.name = "Middle"
	middle.size_flags_vertical = Control.SIZE_EXPAND_FILL
	middle.mouse_filter = Control.MOUSE_FILTER_IGNORE
	shell.add_child(middle)

	_home = _build_home()
	_home.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	middle.add_child(_home)

	_page_holder = Control.new()
	_page_holder.name = "Pages"
	_page_holder.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_page_holder.mouse_filter = Control.MOUSE_FILTER_IGNORE
	middle.add_child(_page_holder)
	_build_pages()
	_page_holder.resized.connect(_fit_pages)

	stage_hud = _build_stage_hud()
	add_child(stage_hud)

	card_drawer = CardDrawer.new()
	card_drawer.z_index = MenuDialog.MODAL_Z - 2
	card_drawer.closed.connect(_on_drawer_closed)
	add_child(card_drawer)

	_nav_dock = _KomiksNavDock.new()
	_nav_dock.name = "NavDock"
	_nav_dock.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_nav_dock)

	tab_bar = UiTabBar.new(TABS)
	tab_bar.tab_pressed.connect(_on_tab_pressed)
	add_child(tab_bar)
	for id: StringName in tab_bar.tabs:
		corner_buttons[id] = tab_bar.tabs[id]
	tab_bar.set_active(&"home")
	move_child(card_drawer, get_child_count() - 1)


## Builds the 4-zone 1280x720 Wild Rift / MLBB modern MOBA HUD layer over the 3D Hero Showcase Stage.
func _build_stage_hud() -> Control:
	var hud: Control = Control.new()
	hud.name = "StageHud"
	hud.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	hud.mouse_filter = Control.MOUSE_FILTER_IGNORE
	hud.z_index = TEXT_Z + 4

	var scrim: _StageEdgeScrims = _StageEdgeScrims.new()
	scrim.name = "StageScrims"
	scrim.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	scrim.mouse_filter = Control.MOUSE_FILTER_IGNORE
	hud.add_child(scrim)

	# Zone 1: Slim Translucent Top Header Strip
	top_header = Control.new()
	top_header.name = "TopHeader"
	top_header.set_anchors_preset(Control.PRESET_TOP_WIDE)
	top_header.offset_left = WIDE_SIDE_INSET
	top_header.offset_right = -WIDE_SIDE_INSET
	top_header.offset_top = WIDE_HEADER_TOP
	top_header.offset_bottom = WIDE_HEADER_TOP + WIDE_HIT_FLOOR
	top_header.mouse_filter = Control.MOUSE_FILTER_IGNORE
	hud.add_child(top_header)

	var header_rule: _TopHeaderTrim = _TopHeaderTrim.new()
	header_rule.name = "HeaderTrim"
	header_rule.set_anchors_preset(Control.PRESET_TOP_WIDE)
	header_rule.offset_top = 0.0
	header_rule.offset_bottom = WIDE_HEADER_VISUAL_H
	header_rule.mouse_filter = Control.MOUSE_FILTER_IGNORE
	top_header.add_child(header_rule)

	profile_capsule = _ProfileCapsuleButton.new()
	profile_capsule.name = "ProfileCapsule"
	profile_capsule.accessibility_name = "Edit player profile"
	profile_capsule.tooltip_text = "Edit callsign and avatar"
	profile_capsule.custom_minimum_size = Vector2(304.0, WIDE_HIT_FLOOR)
	profile_capsule.size = Vector2(304.0, WIDE_HIT_FLOOR)
	profile_capsule.position = Vector2(0.0, 0.0)
	profile_capsule.pressed.connect(open_name_editor)
	top_header.add_child(profile_capsule)

	var brand_box: HBoxContainer = HBoxContainer.new()
	brand_box.name = "BrandLockup"
	brand_box.position = Vector2(318.0, 18.0)
	brand_box.custom_minimum_size = Vector2(188.0, 44.0)
	brand_box.add_theme_constant_override("separation", 8)
	brand_box.alignment = BoxContainer.ALIGNMENT_CENTER
	brand_box.mouse_filter = Control.MOUSE_FILTER_IGNORE
	top_header.add_child(brand_box)

	var crest: TextureRect = KomiksStyle.crest_rect(40.0)
	crest.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	brand_box.add_child(crest)

	var wordmark: TextureRect = KomiksStyle.wordmark_rect(42.0)
	wordmark.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	brand_box.add_child(wordmark)

	var util_row: HBoxContainer = HBoxContainer.new()
	util_row.name = "UtilityCluster"
	util_row.set_anchors_preset(Control.PRESET_TOP_RIGHT)
	util_row.offset_left = -510.0
	util_row.offset_right = 0.0
	util_row.offset_top = 0.0
	util_row.offset_bottom = WIDE_HIT_FLOOR
	util_row.alignment = BoxContainer.ALIGNMENT_END
	util_row.add_theme_constant_override("separation", 4)
	util_row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	top_header.add_child(util_row)

	var status_wrap: CenterContainer = CenterContainer.new()
	status_wrap.name = "StatusWrap"
	status_wrap.custom_minimum_size = Vector2(152.0, WIDE_HIT_FLOOR)
	status_wrap.mouse_filter = Control.MOUSE_FILTER_IGNORE
	util_row.add_child(status_wrap)

	var status_pill: PanelContainer = PanelContainer.new()
	status_pill.name = "ConnectionPill"
	status_pill.custom_minimum_size = Vector2(148.0, 40.0)
	status_pill.mouse_filter = Control.MOUSE_FILTER_IGNORE
	status_pill.add_theme_stylebox_override("panel", KalyeahTheme.glass_capsule(UiSkin.round_radius(40.0), KalyeahTheme.GLASS_RIM, KalyeahTheme.GLASS_DARK, 12.0, 4.0))
	status_wrap.add_child(status_pill)

	var status_hbox: HBoxContainer = HBoxContainer.new()
	status_hbox.alignment = BoxContainer.ALIGNMENT_CENTER
	status_hbox.add_theme_constant_override("separation", 8)
	status_hbox.mouse_filter = Control.MOUSE_FILTER_IGNORE
	status_pill.add_child(status_hbox)

	_status_dot = _StatusDot.new()
	_status_dot.custom_minimum_size = Vector2(10.0, 10.0)
	_status_dot.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	_status_dot.mouse_filter = Control.MOUSE_FILTER_IGNORE
	status_hbox.add_child(_status_dot)

	_status_pill_label = Label.new()
	_status_pill_label.name = "StatusLabel"
	_status_pill_label.text = "LOCAL · 5v5"
	_status_pill_label.add_theme_font_override("font", KalyeahTheme.STAGE_TITLE_FONT)
	_status_pill_label.add_theme_font_size_override("font_size", UiMetrics.MIN_TEXT)
	_status_pill_label.add_theme_color_override("font_color", SUNLIT_CREAM)
	_status_pill_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	status_hbox.add_child(_status_pill_label)

	_wide_tips_button = _GlassIconButton.new(&"tips", "Game of the month")
	_wide_tips_button.name = "WideTipsButton"
	_wide_tips_button.pressed.connect(_open_tips)
	util_row.add_child(_wide_tips_button)

	_wide_mail_button = _GlassIconButton.new(&"mail", "Donate")
	_wide_mail_button.name = "WideMailButton"
	_wide_mail_button.pressed.connect(_open_mail)
	util_row.add_child(_wide_mail_button)

	_wide_history_button = _GlassIconButton.new(&"trophy", "Match history")
	_wide_history_button.name = "WideHistoryButton"
	_wide_history_button.pressed.connect(_on_history_shortcut)
	util_row.add_child(_wide_history_button)

	_wide_settings_button = _GlassIconButton.new(&"settings", "Settings")
	_wide_settings_button.name = "WideSettingsButton"
	_wide_settings_button.pressed.connect(_on_settings_shortcut)
	util_row.add_child(_wide_settings_button)

	# Zone 2: Left Vertical Frameless Navigation Rail
	nav_rail = VBoxContainer.new()
	nav_rail.name = "NavRail"
	nav_rail.position = Vector2(WIDE_SIDE_INSET, 92.0)
	nav_rail.custom_minimum_size = Vector2(WIDE_RAIL_WIDTH, WIDE_RAIL_H)
	nav_rail.add_theme_constant_override("separation", 2)
	nav_rail.mouse_filter = Control.MOUSE_FILTER_IGNORE
	hud.add_child(nav_rail)

	for item: Dictionary in RAIL_ITEMS:
		var rail_id: StringName = item["id"] as StringName
		var tab: _WildRiftNavTab = _WildRiftNavTab.new(
			String(item["title"]),
			String(item["sub"]),
			item["glyph"] as StringName,
			String(item["a11y"])
		)
		tab.name = "Rail_%s" % String(rail_id)
		tab.custom_minimum_size = Vector2(WIDE_RAIL_WIDTH, WIDE_HIT_FLOOR)
		tab.pressed.connect(_on_rail_pressed.bind(rail_id))
		nav_rail.add_child(tab)
		nav_rail_buttons[rail_id] = tab
	_set_active_rail(&"play")

	# Zone 3: Subtle Selected Kid Callout (Lower-Left Floating Watermark Typography)
	kid_callout = Control.new()
	kid_callout.name = "KidCallout"
	kid_callout.set_anchors_preset(Control.PRESET_BOTTOM_LEFT)
	kid_callout.offset_left = WIDE_SIDE_INSET
	kid_callout.offset_right = WIDE_SIDE_INSET + WIDE_CALLOUT_W
	kid_callout.offset_top = -(WIDE_BOTTOM_INSET + WIDE_CALLOUT_H)
	kid_callout.offset_bottom = -WIDE_BOTTOM_INSET
	kid_callout.mouse_filter = Control.MOUSE_FILTER_IGNORE
	hud.add_child(kid_callout)

	var callout_col: VBoxContainer = VBoxContainer.new()
	callout_col.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	callout_col.alignment = BoxContainer.ALIGNMENT_END
	callout_col.add_theme_constant_override("separation", 0)
	callout_col.mouse_filter = Control.MOUSE_FILTER_IGNORE
	kid_callout.add_child(callout_col)

	_kid_epithet_label = Label.new()
	_kid_epithet_label.name = "KidEpithet"
	_kid_epithet_label.add_theme_font_override("font", KalyeahTheme.STAGE_SUB_FONT)
	_kid_epithet_label.add_theme_font_size_override("font_size", UiMetrics.MIN_TEXT)
	_kid_epithet_label.add_theme_color_override("font_color", Color("ffd466"))
	_kid_epithet_label.add_theme_color_override("font_shadow_color", Color(0.05, 0.04, 0.03, 0.88))
	_kid_epithet_label.add_theme_constant_override("shadow_offset_x", 0)
	_kid_epithet_label.add_theme_constant_override("shadow_offset_y", 2)
	_kid_epithet_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	callout_col.add_child(_kid_epithet_label)

	_kid_name_label = Label.new()
	_kid_name_label.name = "KidName"
	_kid_name_label.add_theme_font_override("font", KalyeahTheme.STAGE_DISPLAY_FONT)
	_kid_name_label.add_theme_font_size_override("font_size", UiMetrics.HEADLINE)
	_kid_name_label.add_theme_color_override("font_color", SUNLIT_CREAM)
	_kid_name_label.add_theme_color_override("font_outline_color", Color("1b1512"))
	_kid_name_label.add_theme_constant_override("outline_size", 4)
	_kid_name_label.add_theme_color_override("font_shadow_color", Color(0.04, 0.03, 0.02, 0.72))
	_kid_name_label.add_theme_constant_override("shadow_offset_x", 0)
	_kid_name_label.add_theme_constant_override("shadow_offset_y", 3)
	_kid_name_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	callout_col.add_child(_kid_name_label)

	var accent_line: _GoldAccentRule = _GoldAccentRule.new()
	accent_line.custom_minimum_size = Vector2(280.0, 4.0)
	accent_line.mouse_filter = Control.MOUSE_FILTER_IGNORE
	callout_col.add_child(accent_line)

	var cycle_row: HBoxContainer = HBoxContainer.new()
	cycle_row.name = "KidCycleRow"
	cycle_row.add_theme_constant_override("separation", 6)
	cycle_row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	callout_col.add_child(cycle_row)

	var prev_kid: _GlassIconButton = _GlassIconButton.new(&"chevron_left", "Previous kid")
	prev_kid.name = "PrevKidButton"
	prev_kid.pressed.connect(cycle_showcase_kid.bind(-1))
	cycle_row.add_child(prev_kid)

	_kid_roster_button = _ChamferedGlassButton.new("PALITAN · 1/10", "MGA BATA", SUNLIT_MANGO, "Choose kid")
	_kid_roster_button.name = "KidRosterButton"
	_kid_roster_button.visual_height = 66.0
	_kid_roster_button.set_centered(true)
	_kid_roster_button.set_show_chevron(false)
	_kid_roster_button.custom_minimum_size = Vector2(212.0, WIDE_HIT_FLOOR)
	_kid_roster_button.pressed.connect(_on_open_kid_picker_pressed)
	cycle_row.add_child(_kid_roster_button)

	var next_kid: _GlassIconButton = _GlassIconButton.new(&"chevron_right", "Next kid")
	next_kid.name = "NextKidButton"
	next_kid.pressed.connect(cycle_showcase_kid.bind(1))
	cycle_row.add_child(next_kid)

	# Zone 4: Bottom-Right MLBB / Wild Rift Signature Action Cluster
	action_cluster = Control.new()
	action_cluster.name = "ActionCluster"
	action_cluster.set_anchors_preset(Control.PRESET_BOTTOM_RIGHT)
	action_cluster.offset_left = -(WIDE_CLUSTER_W + WIDE_SIDE_INSET)
	action_cluster.offset_right = -WIDE_SIDE_INSET
	action_cluster.offset_top = -(WIDE_CLUSTER_H + WIDE_BOTTOM_INSET)
	action_cluster.offset_bottom = -WIDE_BOTTOM_INSET
	action_cluster.mouse_filter = Control.MOUSE_FILTER_IGNORE
	hud.add_child(action_cluster)

	var cluster_col: VBoxContainer = VBoxContainer.new()
	cluster_col.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	cluster_col.alignment = BoxContainer.ALIGNMENT_END
	cluster_col.add_theme_constant_override("separation", 2)
	cluster_col.mouse_filter = Control.MOUSE_FILTER_IGNORE
	action_cluster.add_child(cluster_col)

	var pills_row: HBoxContainer = HBoxContainer.new()
	pills_row.name = "ModePillsRow"
	pills_row.add_theme_constant_override("separation", 6)
	pills_row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	cluster_col.add_child(pills_row)

	var pill_defs: Array[Dictionary] = [
		{"index": 2, "label": "PRACTICE", "accent": SUNLIT_JADE, "a11y": "Practice mode"},
		{"index": 1, "label": "LOCAL", "accent": KalyeahTheme.BLUE, "a11y": "Local hotspot mode"},
		{"index": 0, "label": "ONLINE", "accent": SUNLIT_MANGO, "a11y": "Online room code mode"},
	]
	for pdef: Dictionary in pill_defs:
		var m_idx: int = int(pdef["index"])
		var pill: _ModeSegmentPill = _ModeSegmentPill.new(
			String(pdef["label"]),
			pdef["accent"] as Color,
			String(pdef["a11y"])
		)
		pill.name = "ModePill_%d" % m_idx
		pill.custom_minimum_size = Vector2(132.0, WIDE_HIT_FLOOR)
		pill.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		pill.pressed.connect(_select_stage_mode.bind(m_idx))
		pills_row.add_child(pill)
		_mode_pills[m_idx] = pill

	var selector_row: HBoxContainer = HBoxContainer.new()
	selector_row.name = "ModeSelectorRow"
	selector_row.add_theme_constant_override("separation", 6)
	selector_row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	cluster_col.add_child(selector_row)

	mode_selector_button = _ChamferedGlassButton.new("AGAWANG BASE", "5V5 · RULES", SUNLIT_MANGO, "Select street game")
	mode_selector_button.name = "ModeSelectorCard"
	mode_selector_button.visual_height = 66.0
	mode_selector_button.set_show_chevron(true)
	mode_selector_button.custom_minimum_size = Vector2(240.0, WIDE_HIT_FLOOR)
	mode_selector_button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	mode_selector_button.pressed.connect(_on_mode_card_pressed)
	selector_row.add_child(mode_selector_button)

	sub_action_button = _ChamferedGlassButton.new("SUMALI", "HOTSPOT", KalyeahTheme.BLUE, "Join room")
	sub_action_button.name = "SubActionButton"
	sub_action_button.visual_height = 66.0
	sub_action_button.set_centered(true)
	sub_action_button.set_show_chevron(false)
	sub_action_button.custom_minimum_size = Vector2(166.0, WIDE_HIT_FLOOR)
	sub_action_button.pressed.connect(_on_secondary_pressed)
	selector_row.add_child(sub_action_button)

	cta_button = _WildRiftGoldCtaButton.new()
	cta_button.name = "PlayCtaButton"
	cta_button.accessibility_name = "Play"
	cta_button.custom_minimum_size = Vector2(WIDE_CLUSTER_W, WIDE_CTA_H)
	cta_button.size = Vector2(WIDE_CLUSTER_W, WIDE_CTA_H)
	cluster_col.add_child(cta_button)

	return hud


static func _resolve_logo_texture() -> Texture2D:
	if ResourceLoader.exists(BRAND_WORDMARK_PATH):
		var brand_tex: Texture2D = load(BRAND_WORDMARK_PATH) as Texture2D
		if brand_tex != null:
			return brand_tex
	return LOGO


func _build_top_bar() -> Control:
	var row: HBoxContainer = HBoxContainer.new()
	row.name = "TopBar"
	row.alignment = BoxContainer.ALIGNMENT_CENTER
	row.custom_minimum_size = Vector2(0.0, LOGO_HEIGHT + LOGO_DROP)
	row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var left: Control = Control.new()
	left.name = "TopLeft"
	left.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	left.mouse_filter = Control.MOUSE_FILTER_IGNORE
	left.size_flags_vertical = Control.SIZE_SHRINK_END
	left.custom_minimum_size = Vector2(0.0, LOGO_HEIGHT)
	row.add_child(left)
	tips_button = _icon_button("TipsButton", "Game of the month", TIPS_ICON, _open_tips)
	tips_button.position = Vector2(TIPS_INSET, (LOGO_HEIGHT - TOP_ICON_SIZE) / 2.0)
	left.add_child(tips_button)
	var logo_tex: Texture2D = _resolve_logo_texture()
	var logo_ratio: float = float(logo_tex.get_width()) / maxf(float(logo_tex.get_height()), 1.0)
	var logo: TextureRect = TextureRect.new()
	logo.name = "Logo"
	logo.texture = logo_tex
	logo.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	logo.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	logo.custom_minimum_size = Vector2(minf(LOGO_HEIGHT * logo_ratio, 400.0), LOGO_HEIGHT)
	logo.size_flags_vertical = Control.SIZE_SHRINK_END
	logo.mouse_filter = Control.MOUSE_FILTER_IGNORE
	logo.z_as_relative = false
	logo.z_index = NameTag.LOGO_Z
	row.add_child(logo)
	var right: Control = Control.new()
	right.name = "TopRight"
	right.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	right.mouse_filter = Control.MOUSE_FILTER_IGNORE
	right.size_flags_vertical = Control.SIZE_SHRINK_END
	right.custom_minimum_size = Vector2(0.0, LOGO_HEIGHT)
	mail_button = _icon_button("MailButton", "Donate", MAIL_ICON, _open_mail)
	right.add_child(mail_button)
	mail_button.set_anchors_and_offsets_preset(Control.PRESET_CENTER_RIGHT)
	mail_button.offset_left = -(TOP_ICON_SIZE + TIPS_INSET)
	mail_button.offset_right = -TIPS_INSET
	mail_button.offset_top = -TOP_ICON_SIZE / 2.0
	mail_button.offset_bottom = TOP_ICON_SIZE / 2.0
	row.add_child(right)
	return row


func _icon_button(button_name: String, say: String, texture: Texture2D, action: Callable) -> Button:
	var button: Button = Button.new()
	button.name = button_name
	button.accessibility_name = say
	button.flat = true
	button.focus_mode = Control.FOCUS_ALL
	button.custom_minimum_size = Vector2.ONE * TOP_ICON_SIZE
	button.size = Vector2.ONE * TOP_ICON_SIZE
	for state: String in ["normal", "hover", "pressed", "hover_pressed"]:
		button.add_theme_stylebox_override(state, StyleBoxEmpty.new())
	button.add_theme_stylebox_override("focus", UiSkin.focus_ring(UiSkin.RADIUS_L))
	var picture: TextureRect = TextureRect.new()
	picture.name = "Picture"
	picture.texture = texture
	picture.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	picture.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	picture.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	picture.mouse_filter = Control.MOUSE_FILTER_IGNORE
	button.add_child(picture)
	UiKit.feel_press(button)
	button.pressed.connect(action)
	return button


func _open_mail() -> void:
	open_donate()


func _open_tips() -> void:
	open_game_of_the_month()


func open_game_of_the_month(month: int = 0) -> MenuDialog:
	var card: MenuDialog = GameOfMonthSheet.build(month)
	_show_dialog(card)
	return card


func open_donate() -> MenuDialog:
	var card: MenuDialog = DonateSheet.build()
	_show_dialog(card)
	return card


func _on_swipe_area_input(event: InputEvent) -> void:
	if _is_widescreen():
		if current_page != &"" or hero_stage == null:
			return
		if event is InputEventScreenDrag:
			var drag: InputEventScreenDrag = event as InputEventScreenDrag
			hero_stage.rotate_showcase(drag.relative.x * 0.0085)
		elif event is InputEventMouseButton:
			var mb: InputEventMouseButton = event as InputEventMouseButton
			if mb.button_index == MOUSE_BUTTON_LEFT:
				_stage_dragging = mb.pressed
				_stage_last_x = mb.position.x
		elif event is InputEventMouseMotion and _stage_dragging:
			var mm: InputEventMouseMotion = event as InputEventMouseMotion
			var dx: float = mm.position.x - _stage_last_x
			_stage_last_x = mm.position.x
			hero_stage.rotate_showcase(dx * 0.0085)
		return
	if _home.is_visible_in_tree():
		carousel.swipe_input(event)


func _build_home() -> VBoxContainer:
	var home: VBoxContainer = VBoxContainer.new()
	home.name = "Home"
	home.add_theme_constant_override("separation", UiMetrics.COMPACT_GAP)
	home.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var stage: Control = Control.new()
	stage.name = "Stage"
	stage.size_flags_vertical = Control.SIZE_EXPAND_FILL
	stage.custom_minimum_size = Vector2(0.0, HERO_CARD.y * 0.5)
	stage.mouse_filter = Control.MOUSE_FILTER_IGNORE
	home.add_child(stage)
	carousel = MenuCarousel.new()
	carousel.accessibility_name = "Game mode"
	carousel.name = "ModeCarousel"
	carousel.card_size = HERO_CARD
	carousel.bottom_gap = HERO_BOTTOM_GAP
	carousel.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	stage.add_child(carousel)
	carousel.back_rise = MODEL_BACK_RISE
	carousel.spread_scale = MODEL_SPREAD_SCALE
	carousel.wrap = true
	carousel.scale_loss = 1.0 - MODEL_BACK_SCALE
	carousel.setup("", MODES, Session.menu_mode)
	carousel.bob = AMBIENT_BOB
	parallax = HomeParallax.new(carousel, backdrop)
	add_child(parallax)
	stage.resized.connect(_fit_hero)
	stage.resized.connect(_aim_backlight)
	stage.resized.connect(_place_name_tag.call_deferred)
	name_tag = NameTag.new()
	name_tag.tapped.connect(open_name_editor)
	stage.add_child(name_tag)
	rank_crest = _RankCrestBadge.new()
	rank_crest.name = "RankCrest"
	stage.add_child(rank_crest)
	home.add_child(_build_titles())
	var slot: CenterContainer = CenterContainer.new()
	slot.name = "ActionsSlot"
	slot.z_index = TEXT_Z
	slot.size_flags_vertical = Control.SIZE_EXPAND_FILL
	slot.size_flags_stretch_ratio = ACTIONS_SLOT_SHARE
	slot.custom_minimum_size = Vector2(0.0, CTA_HEIGHT)
	slot.mouse_filter = Control.MOUSE_FILTER_IGNORE
	slot.add_child(_build_actions())
	home.add_child(slot)
	_actions_slot = slot
	return home


func _build_titles() -> Control:
	var titles: VBoxContainer = VBoxContainer.new()
	titles.name = "Titles"
	titles.z_index = TEXT_Z
	titles.alignment = BoxContainer.ALIGNMENT_CENTER
	titles.add_theme_constant_override("separation", TITLES_SEPARATION)
	titles.mouse_filter = Control.MOUSE_FILTER_IGNORE
	mode_text_label = Label.new()
	mode_text_label.name = "ModeText"
	mode_text_label.add_theme_font_size_override("font_size", UiMetrics.DETAIL)
	mode_text_label.add_theme_color_override("font_color", SUNLIT_CREAM)
	mode_text_label.add_theme_color_override("font_outline_color", SUNLIT_ESPRESSO)
	mode_text_label.add_theme_constant_override("outline_size", MODE_TEXT_OUTLINE)
	mode_text_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	mode_text_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	mode_text_label.add_theme_constant_override("line_spacing", KalyeahTheme.READING_LINE_SPACING)
	titles.add_child(mode_text_label)
	return titles


func _build_actions() -> Control:
	var column: BoxContainer = BoxContainer.new()
	column.vertical = true
	column.name = "ActionsColumn"
	column.alignment = BoxContainer.ALIGNMENT_CENTER
	column.add_theme_constant_override("separation", UiMetrics.COMPACT_GAP)
	column.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_actions_box = column
	practice_row = PracticeRow.new()
	practice_row.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
	practice_row.picker_opened.connect(_show_dialog)
	practice_row.kid_screen_opened.connect(_show_kid_pick)
	practice_row.changed.connect(_on_practice_row_changed)
	column.add_child(practice_row)
	var actions: HBoxContainer = HBoxContainer.new()
	actions.name = "Actions"
	actions.alignment = BoxContainer.ALIGNMENT_CENTER
	actions.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
	actions.add_theme_constant_override("separation", UiMetrics.COMPACT_GAP)
	actions.mouse_filter = Control.MOUSE_FILTER_IGNORE
	primary_button = _action_button(actions, "PrimaryButton", "")
	secondary_button = _action_button(actions, "SecondaryButton", "Join room")
	column.add_child(actions)
	return column


func _action_button(parent: Control, button_name: String, text: String) -> Button:
	var button: Button = MenuDialog.make_button(text, "gold", CTA_FONT, Vector2(CTA_MIN_WIDTH, CTA_HEIGHT))
	button.name = button_name
	parent.add_child(button)
	return button


func _fit_buttons() -> void:
	var font: Font = primary_button.get_theme_font("font")
	var widest: float = CTA_MIN_WIDTH
	for button: Button in [primary_button, secondary_button]:
		var words: float = font.get_string_size(button.text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, CTA_FONT).x
		widest = maxf(widest, words + CTA_PADDING * 2.0)
	for button: Button in [primary_button, secondary_button]:
		button.custom_minimum_size.x = widest


func _build_pages() -> void:
	street_page = StreetPage.new()
	codex_page = CodexPage.new()
	history_page = HistoryPage.new()
	settings_page = SettingsPage.new()
	pages = {&"street": street_page, &"codex": codex_page, &"history": history_page, &"settings": settings_page}
	for id: StringName in pages:
		var page: Control = pages[id]
		page.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		page.visible = false
		_page_holder.add_child(page)
	codex_page.drawer_requested.connect(open_card_drawer)
	settings_page.quit_pressed.connect(open_quit_confirm)
	history_page.play_pressed.connect(close_page)


func _fit_pages() -> void:
	for page: Control in pages.values():
		page.offset_right = page.offset_left
		page.offset_bottom = page.offset_top


func _on_tab_pressed(id: StringName) -> void:
	if id == &"home":
		close_page()
	else:
		open_page(id)


func _on_rail_pressed(rail_id: StringName) -> void:
	match rail_id:
		&"play":
			close_page()
		&"kids":
			open_codex_section(CodexPage.CHARACTERS, &"kids")
		&"arsenal":
			loadout_pressed.emit()
			open_codex_section(CodexPage.WEAPONS, &"arsenal")
		&"ranked":
			ranked_pressed.emit()
			open_page(&"history")


func _on_history_shortcut() -> void:
	history_pressed.emit()
	open_page(&"history")


func _on_settings_shortcut() -> void:
	settings_pressed.emit()
	open_page(&"settings")


func open_codex_section(section: StringName, rail_id: StringName = &"kids") -> void:
	open_page(&"codex")
	_set_active_rail(rail_id)
	if codex_page != null:
		codex_page.show_view(section)


func _set_active_rail(rail_id: StringName) -> void:
	_active_rail_id = rail_id
	for id: StringName in nav_rail_buttons:
		nav_rail_buttons[id].set_active(id == rail_id)
	if _wide_history_button != null:
		_wide_history_button.set_active(current_page == &"history")
	if _wide_settings_button != null:
		_wide_settings_button.set_active(current_page == &"settings")


func open_page(id: StringName) -> void:
	if not pages.has(id):
		return
	var way: int = signi(int(PAGE_RANK.get(id, 0)) - int(PAGE_RANK.get(current_page, 0)))
	card_drawer.close(false)
	current_page = id
	for other: StringName in pages:
		pages[other].visible = other == id
	if id == &"history":
		history_page.refresh()
		_set_active_rail(&"ranked")
	elif id == &"codex":
		codex_page.show_view(CodexPage.HUB)
		_set_active_rail(&"kids")
	else:
		_set_active_rail(&"")
	tab_bar.set_active(id)
	_apply_safe_area()
	_sync_ambient()
	_fade_in(pages[id], way)


func close_page() -> void:
	var way: int = signi(0 - int(PAGE_RANK.get(current_page, 0)))
	card_drawer.close(false)
	current_page = &""
	_set_active_rail(&"play")
	refresh_rank_crest()
	_sync_showcase_kid()
	for id: StringName in pages:
		pages[id].visible = false
	tab_bar.set_active(&"home")
	_apply_safe_area()
	_sync_ambient()
	if not _is_widescreen():
		_fade_in(_home, way)


func _fade_in(content: Control, way: int = 0) -> void:
	if is_instance_valid(_page_tween):
		_page_tween.kill()
	content.modulate.a = 1.0
	content.position = Vector2.ZERO
	if Settings.reduced_motion or not is_inside_tree():
		return
	content.modulate.a = 0.0
	content.position = Vector2(float(way) * 56.0, 0.0 if way != 0 else 18.0)
	_page_tween = create_tween().set_parallel(true)
	_page_tween.tween_property(content, "modulate:a", 1.0, 0.2)
	_page_tween.tween_property(content, "position", Vector2.ZERO, 0.26).set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT)


static func models_are_bare() -> bool:
	return bool(MODES[0].get("bare", false))


static func model_ratio() -> float:
	return MODEL_RATIO if models_are_bare() else HERO_RATIO


func _is_widescreen() -> bool:
	var extent: Vector2 = size
	if extent.x <= 0.0 or extent.y <= 0.0:
		var parent_ctrl: Control = get_parent() as Control
		if parent_ctrl != null and parent_ctrl.size.x > 0.0 and parent_ctrl.size.y > 0.0:
			extent = parent_ctrl.size
		else:
			var vp: Viewport = get_viewport()
			extent = vp.get_visible_rect().size if vp != null else Vector2(1280.0, 720.0)
	return extent.x > extent.y


func refresh_rank_crest() -> void:
	var lp: int = 0
	var tier_text: String = "Kahoy"
	var progress: float = 0.0
	var tier_color: Color = SUNLIT_MANGO
	if ResourceLoader.exists(RANKED_SYSTEM_PATH):
		var rs: Script = load(RANKED_SYSTEM_PATH) as Script
		if rs != null:
			lp = int(rs.call(&"current_lp"))
			tier_text = String(rs.call(&"tier_name", lp))
			progress = float(rs.call(&"tier_progress", lp))
			var t_dict: Dictionary = rs.call(&"tier_for_lp", lp) as Dictionary
			if t_dict.has("color"):
				tier_color = t_dict["color"] as Color
	if rank_crest != null:
		(rank_crest as _RankCrestBadge).set_rank(tier_text, lp)
	if profile_capsule != null:
		profile_capsule.set_profile_data(Session.player_name, _current_avatar_id(), tier_text, lp, progress, tier_color)


func _current_avatar_id() -> String:
	var id: String = Session.avatar_id
	if id.is_empty() or not Session.AVATARS.has(id):
		return "junjun"
	return id


## Cycles the 3D showcase kid backward (-1) or forward (+1) and saves `Session.avatar_id`.
func cycle_showcase_kid(step: int) -> void:
	var count: int = Session.AVATARS.size()
	if count == 0:
		return
	var current: String = _current_avatar_id()
	var idx: int = Session.AVATARS.find(current)
	if idx < 0:
		idx = 0
	var next_idx: int = posmod(idx + step, count)
	select_showcase_kid(Session.AVATARS[next_idx])


func select_showcase_kid(avatar_id: String) -> void:
	if not Session.AVATARS.has(avatar_id):
		return
	if practice_row != null:
		practice_row.choose_kid(StringName(avatar_id))
	else:
		Session.avatar_id = avatar_id
		Session.save()
		_sync_showcase_kid()


func _on_practice_row_changed() -> void:
	_sync_showcase_kid()
	_refresh_stage_actions()
	if name_tag != null:
		name_tag.refresh_photo()


func _sync_showcase_kid() -> void:
	var id: String = _current_avatar_id()
	var meta: Dictionary = KID_SHOWCASE_META.get(id, KID_SHOWCASE_META["junjun"]) as Dictionary
	var epithet: String = String(meta.get("epithet", "BATANG KALYE"))
	var role: String = String(meta.get("role", "RUNNER"))
	var accent: Color = meta.get("accent", SUNLIT_MANGO) as Color
	var display_name: String = PracticeRow.kid_name(StringName(id)).to_upper()
	if _kid_epithet_label != null:
		_kid_epithet_label.text = "%s · %s" % [epithet, role]
	if _kid_name_label != null:
		_kid_name_label.text = display_name
	var total_kids: int = maxi(Session.AVATARS.size(), KID_SHOWCASE_META.size())
	var idx: int = maxi(Session.AVATARS.find(id), 0) + 1
	if _kid_roster_button != null:
		_kid_roster_button.set_copy("PALITAN · %d/%d" % [idx, total_kids], "MGA BATA", accent)
	if hero_stage != null:
		hero_stage.set_character(StringName(id), accent)
	refresh_rank_crest()


func _on_open_kid_picker_pressed() -> void:
	if practice_row != null:
		practice_row.open_kid_picker()


func _select_stage_mode(mode_index: int) -> void:
	if mode_index < 0 or mode_index >= MODES.size():
		return
	if carousel != null and carousel.index != mode_index:
		carousel.select(mode_index)
	_on_mode_selected(mode_index)


func _on_mode_card_pressed() -> void:
	var mode_id: String = String(MODES[Session.menu_mode]["id"])
	if mode_id == "practice" and practice_row != null:
		practice_row.open_game_picker()
	else:
		open_page(&"street")


func _fit_hero() -> void:
	var stage: Control = carousel.get_parent() as Control
	if stage.size.y < 100.0:
		return
	var wide: bool = _is_widescreen()
	var diorama: bool = models_are_bare()
	var ratio: float = MODEL_RATIO if diorama else HERO_RATIO
	var fitted: Vector2
	if wide:
		var wide_h: float = clampf(stage.size.y - WIDE_HERO_AIR, WIDE_HERO_MIN_HEIGHT, WIDE_HERO_MAX_HEIGHT)
		var max_w: float = maxf(200.0, stage.size.x * 0.44)
		var wide_w: float = minf(wide_h * ratio, max_w)
		fitted = Vector2(roundf(wide_w), roundf(wide_w / ratio))
	else:
		var height: float = clampf(stage.size.y - HERO_AIR, HERO_MIN_HEIGHT, HERO_MAX_HEIGHT)
		var width: float = minf(height * ratio, stage.size.x - (MODEL_SIDE_ROOM if diorama else 120.0)) * (MODEL_SCALE if diorama else HERO_SCALE)
		fitted = Vector2(roundf(width), roundf(width / ratio))
	if fitted.distance_to(carousel.card_size) < 3.0:
		return
	carousel.card_size = fitted
	carousel.setup("", MODES, carousel.index)


func _place_name_tag() -> void:
	if name_tag == null or carousel == null or _top_bar == null:
		return
	var stage: Control = carousel.get_parent() as Control
	if _is_widescreen():
		if rank_crest != null:
			rank_crest.visible = false
		return
	if rank_crest != null:
		rank_crest.visible = false
	var logo: Control = _top_bar.find_child("Logo", true, false) as Control
	var band_top: float = logo.get_global_rect().end.y - stage.get_global_rect().position.y
	var band_bottom: float = carousel.front_top()
	var middle: float = (band_top + band_bottom) / 2.0 + TAG_DROP
	var fitted: float = clampf(minf((band_bottom - band_top - 2.0 * TAG_AIR - TAG_DROP) / NameTag.CARD_HEIGHT, (band_bottom - TAG_AIR - middle) / (NameTag.CARD_HEIGHT / 2.0)), TAG_MIN_SCALE, 1.0)
	var card_top: float = middle - NameTag.CARD_HEIGHT * fitted / 2.0
	var hole: Vector2 = Vector2(stage.size.x / 2.0, card_top + NameTag.SLOT_MIDDLE * fitted)
	name_tag.hang(hole, -stage.get_global_rect().position.y - TAG_OVERSHOOT, fitted)


func _on_swiping_changed(active: bool) -> void:
	if backdrop == null:
		return
	if not active:
		_aim_backlight()
	backdrop.hold_lights(active)


func _aim_backlight() -> void:
	if backdrop == null or carousel == null or carousel.cards.is_empty() or carousel.swiping:
		return
	var card: Button = carousel.cards[carousel.index]
	var centre: Vector2 = backdrop.get_global_transform().affine_inverse() * card.get_global_rect().get_center()
	backdrop.aim_backlight(centre, card.size.y * BACKLIGHT_REACH)


func mode_name() -> String:
	var face: CardFace = mode_models[carousel.index].get_node("Face") as CardFace
	return face.plate().text


func _refresh_mode() -> void:
	var mode: Dictionary = MODES[Session.menu_mode]
	var rooms: bool = String(mode["id"]) != "practice"
	mode_text_label.text = String(mode["text"])
	primary_button.text = "Create" if rooms else "Play"
	secondary_button.text = "Join"
	secondary_button.visible = rooms
	practice_row.visible = not rooms
	KalyeahTheme.style_button(primary_button, "orange")
	KalyeahTheme.style_button(secondary_button, "blue")
	_fit_buttons()
	_refresh_stage_actions()


func _refresh_stage_actions() -> void:
	for m_idx: int in _mode_pills:
		_mode_pills[m_idx].set_active(m_idx == Session.menu_mode)
	var mode_id: String = String(MODES[Session.menu_mode]["id"])
	var street_entry: Dictionary = PracticeRow.game_entry(Session.practice_game)
	var game_title: String = String(street_entry.get("game", "Agawang Base")).to_upper()
	var game_rules: StreetModeRules = StreetMode.rules_for(Session.practice_game)
	var format_tag: String = "5v5"
	if game_rules != null and StreetMode.create(game_rules).is_free_for_all():
		format_tag = "FFA"

	if mode_id == "practice":
		if _status_pill_label != null:
			_status_pill_label.text = "BOTS · %s" % format_tag
		if _status_dot != null:
			_status_dot.set_color(SUNLIT_JADE)
		if mode_selector_button != null:
			mode_selector_button.set_copy("%s · %s" % [game_title, format_tag], "VS BARANGAY BOTS · CHALLENGE", SUNLIT_JADE)
			mode_selector_button.accessibility_name = "Select street game"
		if sub_action_button != null:
			sub_action_button.visible = false
		if cta_button != null:
			cta_button.set_cta_copy("MAGLARO", "PLAY · %s" % game_title)
			cta_button.accessibility_name = "Play practice match"
	elif mode_id == "hotspot":
		if _status_pill_label != null:
			_status_pill_label.text = "LOCAL · WIFI"
		if _status_dot != null:
			_status_dot.set_color(KalyeahTheme.BLUE)
		if mode_selector_button != null:
			mode_selector_button.set_copy("AGAWANG BASE", "5V5 · RULES", KalyeahTheme.BLUE)
			mode_selector_button.accessibility_name = "View street games"
		if sub_action_button != null:
			sub_action_button.visible = true
			sub_action_button.set_copy("SUMALI", "HOTSPOT", KalyeahTheme.BLUE)
			sub_action_button.accessibility_name = "Join local hotspot"
		if cta_button != null:
			cta_button.set_cta_copy("MAGLARO", "HOST LOCAL HOTSPOT")
			cta_button.accessibility_name = "Host local hotspot"
	else:
		if _status_pill_label != null:
			_status_pill_label.text = "ONLINE · 5v5"
		if _status_dot != null:
			_status_dot.set_color(SUNLIT_MANGO)
		if mode_selector_button != null:
			mode_selector_button.set_copy("AGAWANG BASE", "5V5 · RULES", SUNLIT_MANGO)
			mode_selector_button.accessibility_name = "View street games"
		if sub_action_button != null:
			sub_action_button.visible = true
			sub_action_button.set_copy("JOIN CODE", "ROOM CODE", SUNLIT_MANGO)
			sub_action_button.accessibility_name = "Join online room code"
		if cta_button != null:
			cta_button.set_cta_copy("MAGLARO", "CREATE ONLINE ROOM")
			cta_button.accessibility_name = "Create online room"


func _on_mode_selected(index: int) -> void:
	Session.menu_mode = index
	_refresh_mode()


func _on_primary_pressed() -> void:
	play_pressed.emit()
	match String(MODES[Session.menu_mode]["id"]):
		"practice":
			practice_pressed.emit()
			open_practice_confirm()
		"online":
			_open_lobby(LobbyScreen.Intent.CREATE, false)
		_:
			hotspot_host_pressed.emit()
			open_hotspot_create()


func _on_secondary_pressed() -> void:
	if String(MODES[Session.menu_mode]["id"]) == "hotspot":
		hotspot_join_pressed.emit()
		open_hotspot_join()
	else:
		join_code_pressed.emit()
		_open_lobby(LobbyScreen.Intent.JOIN, false)


func open_practice_confirm() -> MenuDialog:
	var game: StreetModeRules = StreetMode.rules_for(Session.practice_game)
	var card: MenuDialog = MenuDialog.create(game.title if game != null else "Practice", MenuDialog.WIDTH_SMALL)
	var words: String = "5 v 5 against Barangay bots! Ready to rumble?"
	if game != null:
		var who: String = "You against 9 bots." if StreetMode.create(game).is_free_for_all() else "5 v 5 against bots."
		words = "%s %d minutes, most points win!" % [who, roundi(game.match_seconds / 60.0)]
	card.add_label(words, MenuDialog.TEXT_SIZE, MenuDialog.TEXT_COLOR, HORIZONTAL_ALIGNMENT_CENTER)
	if game == null:
		_add_difficulty_choice(card)
	var play: Button = MenuDialog.make_button("Play", "gold", UiMetrics.BODY)
	card.add_buttons([play])
	play.pressed.connect(func() -> void:
		card.close(false)
		_on_practice_pressed(&"match"))
	_show_dialog(card)
	return card


func _add_difficulty_choice(card: MenuDialog) -> void:
	var rules: GameRules = load("res://data/rules/game_rules.tres") as GameRules
	var options: Array[Dictionary] = []
	for level: BotLevel in rules.bot_levels:
		options.append({"id": level.id, "label": level.label})
	if options.is_empty():
		return
	var choice: UiSegmented = UiSegmented.new(options, UiMetrics.TOUCH, UiMetrics.BODY)
	choice.name = "DifficultyChoice"
	var blurb: Label = card.add_hint("")
	blurb.name = "DifficultyBlurb"
	blurb.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	var show: Callable = func(id: StringName) -> void:
		blurb.text = UiText.t(rules.bot_level(id).blurb)
	var current: BotLevel = rules.bot_level(Settings.bot_level)
	choice.select(current.id, false)
	show.call(current.id)
	choice.chosen.connect(func(id: StringName) -> void:
		Settings.set_bot_level(id)
		show.call(id))
	card.body.add_child(choice)
	card.body.move_child(choice, blurb.get_index())


func open_hotspot_create() -> MenuDialog:
	var card: MenuDialog = HotspotDialogs.create_check(func() -> void: _open_lobby(LobbyScreen.Intent.CREATE, true))
	_show_dialog(card)
	return card


func open_hotspot_join(interfaces: Callable = Callable(), plan_for: Callable = Callable()) -> MenuDialog:
	var card: MenuDialog = HotspotDialogs.join_check(func(url: String) -> void:
		Session.lobby_url = url
		_open_lobby(LobbyScreen.Intent.JOIN, true), interfaces, plan_for)
	_show_dialog(card)
	return card


func _apply_safe_area() -> void:
	if _margin == null:
		return
	var inset: Vector4 = safe_insets()
	var wide: bool = _is_widescreen()
	if wide:
		var on_home: bool = current_page == &""
		_margin.add_theme_constant_override("margin_left", int((SIDE_MARGIN if on_home else WIDE_PAGE_LEFT) + inset.x))
		_margin.add_theme_constant_override("margin_right", int((SIDE_MARGIN if on_home else PAGE_SIDE_MARGIN) + inset.z))
		_margin.add_theme_constant_override("margin_top", int((WIDE_TOP_MARGIN if on_home else WIDE_PAGE_TOP) + inset.y))
		_margin.add_theme_constant_override("margin_bottom", int(18.0 + inset.w))
		_top_bar.visible = false
		_home.visible = false
		_nav_dock.visible = false
		tab_bar.visible = false
		if stage_hud != null:
			stage_hud.visible = true
			top_header.offset_left = WIDE_SIDE_INSET + inset.x
			top_header.offset_right = -(WIDE_SIDE_INSET + inset.z)
			top_header.offset_top = WIDE_HEADER_TOP + inset.y
			top_header.offset_bottom = WIDE_HEADER_TOP + inset.y + WIDE_HIT_FLOOR
			nav_rail.position = Vector2(WIDE_SIDE_INSET + inset.x, 92.0 + inset.y)
			kid_callout.visible = on_home
			kid_callout.offset_left = WIDE_SIDE_INSET + inset.x
			kid_callout.offset_right = WIDE_SIDE_INSET + WIDE_CALLOUT_W + inset.x
			kid_callout.offset_bottom = -(WIDE_BOTTOM_INSET + inset.w)
			kid_callout.offset_top = -(WIDE_BOTTOM_INSET + WIDE_CALLOUT_H + inset.w)
			action_cluster.visible = on_home
			action_cluster.offset_right = -(WIDE_SIDE_INSET + inset.z)
			action_cluster.offset_left = -(WIDE_CLUSTER_W + WIDE_SIDE_INSET + inset.z)
			action_cluster.offset_bottom = -(WIDE_BOTTOM_INSET + inset.w)
			action_cluster.offset_top = -(WIDE_CLUSTER_H + WIDE_BOTTOM_INSET + inset.w)
		if hero_stage != null:
			hero_stage.set_stage_active(on_home)
		if backdrop != null:
			backdrop.stage_covered = on_home
	else:
		var side: float = SIDE_MARGIN if current_page == &"" else PAGE_SIDE_MARGIN
		_margin.add_theme_constant_override("margin_left", int(side + inset.x))
		_margin.add_theme_constant_override("margin_right", int(side + inset.z))
		_margin.add_theme_constant_override("margin_top", int(TOP_MARGIN + inset.y))
		_margin.add_theme_constant_override("margin_bottom", int(UiTabBar.HEIGHT + inset.w + UiMetrics.COMPACT_GAP))
		_apply_top_bar_metrics(LOGO_HEIGHT, LOGO_DROP, TOP_ICON_SIZE)
		_apply_action_metrics(false)
		_top_bar.visible = current_page == &""
		_home.visible = current_page == &""
		_nav_dock.visible = false
		tab_bar.visible = true
		tab_bar.set_anchors_preset(Control.PRESET_BOTTOM_WIDE)
		tab_bar.offset_left = 0.0
		tab_bar.offset_right = 0.0
		tab_bar.offset_bottom = 0.0
		tab_bar.offset_top = -(UiTabBar.HEIGHT + inset.w)
		if stage_hud != null:
			stage_hud.visible = false
		if hero_stage != null:
			hero_stage.set_stage_active(false)
		if backdrop != null:
			backdrop.stage_covered = false
	card_drawer.offset_bottom = 0.0
	card_drawer.set_bottom_inset(inset.w)
	if not wide and carousel != null and carousel.get_parent() != null:
		_fit_hero()
		_place_name_tag.call_deferred()


func _apply_top_bar_metrics(logo_h: float, drop: float, icon_sz: float) -> void:
	if _top_bar == null:
		return
	_top_bar.custom_minimum_size = Vector2(0.0, logo_h + drop)
	var left: Control = _top_bar.get_node_or_null("TopLeft") as Control
	var right: Control = _top_bar.get_node_or_null("TopRight") as Control
	var logo: TextureRect = _top_bar.get_node_or_null("Logo") as TextureRect
	if left != null:
		left.custom_minimum_size = Vector2(0.0, logo_h)
	if right != null:
		right.custom_minimum_size = Vector2(0.0, logo_h)
	if logo != null:
		if logo.texture == LOGO and ResourceLoader.exists(BRAND_WORDMARK_PATH):
			logo.texture = _resolve_logo_texture()
		var tex: Texture2D = logo.texture if logo.texture != null else LOGO
		var tex_ratio: float = float(tex.get_width()) / maxf(float(tex.get_height()), 1.0)
		var max_logo_w: float = 520.0 if _is_widescreen() else 400.0
		logo.custom_minimum_size = Vector2(minf(logo_h * tex_ratio, max_logo_w), logo_h)
	if tips_button != null:
		tips_button.custom_minimum_size = Vector2.ONE * icon_sz
		tips_button.size = Vector2.ONE * icon_sz
		tips_button.position = Vector2(TIPS_INSET, (logo_h - icon_sz) / 2.0)
	if mail_button != null:
		mail_button.custom_minimum_size = Vector2.ONE * icon_sz
		mail_button.size = Vector2.ONE * icon_sz
		mail_button.offset_left = -(icon_sz + TIPS_INSET)
		mail_button.offset_right = -TIPS_INSET
		mail_button.offset_top = -icon_sz / 2.0
		mail_button.offset_bottom = icon_sz / 2.0


func _apply_action_metrics(wide: bool) -> void:
	var cta_h: float = WIDE_CTA_HEIGHT if wide else CTA_HEIGHT
	if _shell != null:
		_shell.add_theme_constant_override("separation", 6 if wide else UiMetrics.COMPACT_GAP)
	if _home != null:
		_home.add_theme_constant_override("separation", 6 if wide else UiMetrics.COMPACT_GAP)
		var stage: Control = _home.get_node_or_null("Stage") as Control
		if stage != null:
			stage.custom_minimum_size.y = WIDE_HERO_MIN_HEIGHT if wide else (HERO_CARD.y * 0.5)
	if _actions_box != null:
		_actions_box.vertical = not wide
	if _actions_slot != null:
		_actions_slot.custom_minimum_size = Vector2(0.0, cta_h)
		_actions_slot.size_flags_vertical = Control.SIZE_SHRINK_END if wide else Control.SIZE_EXPAND_FILL
	if primary_button != null:
		primary_button.custom_minimum_size.y = cta_h
	if secondary_button != null:
		secondary_button.custom_minimum_size.y = cta_h
	if practice_row != null:
		var row_size: Vector2 = Vector2(WIDE_PRACTICE_WIDTH, cta_h) if wide else PracticeRow.ROW_SIZE
		practice_row.custom_minimum_size = row_size
		if practice_row.game_button != null:
			practice_row.game_button.custom_minimum_size.y = row_size.y
		if practice_row.kid_button != null:
			practice_row.kid_button.custom_minimum_size.y = row_size.y


func safe_insets() -> Vector4:
	var window: Vector2i = DisplayServer.window_get_size()
	if window.x <= 0 or window.y <= 0:
		return Vector4.ZERO
	var safe: Rect2i = DisplayServer.get_display_safe_area()
	var origin: Vector2i = DisplayServer.window_get_position()
	var to_ui: Vector2 = get_viewport().get_visible_rect().size / Vector2(window)
	var left: float = maxf(0.0, float(safe.position.x - origin.x))
	var top: float = maxf(0.0, float(safe.position.y - origin.y))
	var right: float = maxf(0.0, float(origin.x + window.x - (safe.position.x + safe.size.x)))
	var bottom: float = maxf(0.0, float(origin.y + window.y - (safe.position.y + safe.size.y)))
	return Vector4(minf(left * to_ui.x, 120.0), minf(top * to_ui.y, 120.0), minf(right * to_ui.x, 120.0), minf(bottom * to_ui.y, 120.0))


# ---- entrance & pulse ------------------------------------------------------------------

func _pop_in() -> void:
	if Settings.reduced_motion:
		return
	var parts: Array[Control] = []
	if _is_widescreen():
		parts.append(nav_rail)
		parts.append(kid_callout)
		parts.append(action_cluster)
	else:
		parts.append(carousel)
		parts.append(primary_button)
		parts.append(secondary_button)
	for i: int in parts.size():
		var part: Control = parts[i]
		if part == null:
			continue
		part.pivot_offset = part.size / 2.0
		part.scale = Vector2.ONE * 0.94
		part.modulate.a = 0.0
		var pop: Tween = create_tween().set_parallel(true)
		part.set_meta(&"entrance_tween", pop)
		pop.tween_property(part, "modulate:a", 1.0, 0.26).set_delay(0.08 * float(i))
		pop.tween_property(part, "scale", Vector2.ONE, 0.34).set_delay(0.08 * float(i)).set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT)
	_start_pulse.call_deferred()


func _active_cta_control() -> Button:
	if _is_widescreen() and is_instance_valid(cta_button):
		return cta_button
	return primary_button


func _start_pulse() -> void:
	_stop_pulse()
	var target: Button = _active_cta_control()
	if not is_inside_tree() or Settings.reduced_motion or not is_instance_valid(target):
		return
	var generation: int = _pulse_generation
	await get_tree().create_timer(PULSE_DELAY).timeout
	target = _active_cta_control()
	if generation != _pulse_generation or not is_inside_tree() or Settings.reduced_motion or not is_instance_valid(target):
		return
	target.pivot_offset = target.size / 2.0
	_pulse = create_tween().set_loops()
	_pulse.tween_property(target, "scale", Vector2.ONE * PULSE_SCALE, PULSE_HALF).set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)
	_pulse.tween_property(target, "scale", Vector2.ONE, PULSE_HALF).set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)


func _stop_pulse() -> void:
	_pulse_generation += 1
	if is_instance_valid(_pulse):
		_pulse.kill()


func _on_accessibility_changed() -> void:
	if Settings.reduced_motion:
		_stop_pulse()
		if is_instance_valid(primary_button):
			primary_button.scale = Vector2.ONE
		if is_instance_valid(cta_button):
			cta_button.scale = Vector2.ONE
	else:
		_start_pulse()
	if hero_stage != null:
		hero_stage.sync_motion()


# ---- actions ----------------------------------------------------------------------------

func _on_practice_pressed(kind: StringName = &"training") -> void:
	if _loading:
		return
	Session.practice_kind = kind
	Session.briefing_pending = kind == &"match"
	_loading = true
	loading_screen = BootScreen.for_scene(PRACTICE_SCENE)
	add_child(loading_screen)
	loading_screen.finished.connect(func() -> void: get_tree().change_scene_to_packed(loading_screen.scene()))


func _open_lobby(intent: LobbyScreen.Intent, hotspot: bool) -> void:
	Session.lobby_intent = intent
	Session.lobby_hotspot = hotspot
	open_lobby_action.call(intent)


func _change_to_lobby(_intent: LobbyScreen.Intent) -> void:
	get_tree().change_scene_to_file(LOBBY_SCENE)


func _show_dialog(new_dialog: MenuDialog) -> void:
	if dialog != null and is_instance_valid(dialog) and dialog.is_open:
		dialog.close(false)
	dialog = new_dialog
	dialog.closed.connect(_on_dialog_closed)
	dialog.open(self)
	_sync_ambient()


func _on_dialog_closed() -> void:
	_refresh_stage_actions()
	_sync_ambient()


func _show_kid_pick(screen: WeaponPickScreen) -> void:
	kid_pick = screen
	add_child(screen)
	screen.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	screen.tree_exited.connect(_on_kid_pick_exited)
	_sync_ambient()


func _on_kid_pick_exited() -> void:
	_sync_showcase_kid()
	_sync_ambient()


func _sync_ambient() -> void:
	var covered: bool = current_page != &"" or (dialog != null and is_instance_valid(dialog) and dialog.is_open) \
			or (is_instance_valid(kid_pick) and kid_pick.is_inside_tree())
	var wide: bool = _is_widescreen()
	backdrop.ambient = not covered
	backdrop.lights_on = current_page == &""
	backdrop.stage_covered = wide and current_page == &""
	carousel.bob = 0.0 if (covered or wide) else AMBIENT_BOB
	if parallax != null:
		parallax.active = not covered and not wide
	name_tag.swing = not covered and not wide
	if hero_stage != null:
		hero_stage.set_paused_for_modal(covered)


func open_card_drawer(screen: CardsScreen, index: int) -> void:
	if _drawer_screen != screen:
		if _drawer_screen != null:
			_drawer_screen.mark(-1)
		_drawer_screen = screen
	if not card_drawer.shown.is_connected(_on_drawer_shown):
		card_drawer.shown.connect(_on_drawer_shown)
	var available: Array[Dictionary] = screen.entries.filter(func(entry: Dictionary) -> bool: return not bool(entry.get("locked", false)))
	card_drawer.open_entries(available, index)
	_sync_ambient()


func _on_drawer_shown(index: int) -> void:
	if _drawer_screen != null:
		_drawer_screen.mark(index)


func _on_drawer_closed() -> void:
	if _drawer_screen != null:
		_drawer_screen.mark(-1)
	_drawer_screen = null


func open_name_editor() -> MenuDialog:
	var card: MenuDialog = ProfileSheet.build(_on_profile_saved, open_id_camera, _on_photo_changed.bind("Photo removed"), open_gallery, _on_photo_changed.bind("Avatar saved!"))
	card.closed.connect(name_tag.show)
	name_tag.hide()
	_show_dialog(card)
	return card


func open_id_camera() -> MenuDialog:
	var card: MenuDialog = IdCameraSheet.build(_on_photo_changed.bind("Photo saved!"))
	_show_dialog(card)
	return card


func open_gallery() -> void:
	var asked: Error = IdPhotoPicker.pick(_on_gallery_answer)
	if asked != OK:
		UiToast.say(self, "The gallery can't open on this phone.")


func _on_gallery_answer(result: Error) -> void:
	if result == OK:
		_on_photo_changed("Photo saved!")
	elif result != ERR_SKIP:
		UiToast.say(self, "That picture can't be opened. Try another!")


func _on_photo_changed(toast: String) -> void:
	name_tag.refresh_photo()
	_place_name_tag()
	_sync_showcase_kid()
	UiToast.say(self, toast)


func _on_profile_saved() -> void:
	name_tag.set_player_name(Session.player_name)
	_place_name_tag()
	_sync_showcase_kid()
	UiToast.say(self, "Profile saved!")


func open_quit_confirm() -> MenuDialog:
	var card: MenuDialog = MenuDialog.create("Quit game?", MenuDialog.WIDTH_SMALL)
	var leave: Button = MenuDialog.make_button("Quit", "red", UiMetrics.BODY)
	card.add_buttons([leave])
	leave.pressed.connect(func() -> void:
		card.close(false)
		quit_action.call())
	_show_dialog(card)
	return card


func _quit_game() -> void:
	get_tree().quit()


func _go_back() -> void:
	if card_drawer.is_open:
		card_drawer.close()
		return
	if current_page != &"":
		close_page()
	else:
		open_quit_confirm()


func _notification(what: int) -> void:
	if what == NOTIFICATION_WM_GO_BACK_REQUEST:
		_back_pressed.call_deferred()


func _back_pressed() -> void:
	if _loading:
		return
	if is_instance_valid(kid_pick) and kid_pick.is_inside_tree():
		return
	if dialog == null or not is_instance_valid(dialog) or not dialog.is_open:
		_go_back()


# =======================================================================================
# 3D HERO SHOWCASE STAGE (Full-Bleed 1280x720 Sunlit Barangay Plaza + Interactive KidModel)
# =======================================================================================

class _HeroShowcaseStage extends Control:
	const BARANGAY_PROP_PATH: String = "res://assets/models/barangay/%s.glb"
	const CHARACTER_DEF_PATH: String = "res://data/characters/%s.tres"
	const STAGE_TURN_POS: Vector3 = Vector3(0.24, 0.044, 0.0)
	const DEFAULT_YAW: float = -0.22

	var _vp_container: SubViewportContainer
	var _viewport: SubViewport
	var _world_root: Node3D
	var _turntable: Node3D
	var _kid_model: KidModel
	var _aura_ring_mat: StandardMaterial3D
	var _rim_light: DirectionalLight3D
	var _current_kid_id: StringName = &""
	var _target_yaw: float = DEFAULT_YAW
	var _current_yaw: float = DEFAULT_YAW
	var _idle_clock: float = 0.0
	var _stage_active: bool = true
	var _modal_paused: bool = false

	func _init() -> void:
		_vp_container = SubViewportContainer.new()
		_vp_container.name = "StageViewportContainer"
		_vp_container.stretch = true
		_vp_container.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		_vp_container.mouse_filter = Control.MOUSE_FILTER_IGNORE
		add_child(_vp_container)

		_viewport = SubViewport.new()
		_viewport.name = "StageViewport"
		_viewport.own_world_3d = true
		_viewport.size = Vector2i(1280, 720)
		_viewport.msaa_3d = Viewport.MSAA_2X
		_viewport.render_target_update_mode = SubViewport.UPDATE_WHEN_VISIBLE
		_vp_container.add_child(_viewport)

		_world_root = Node3D.new()
		_world_root.name = "ShowcaseWorld"
		_viewport.add_child(_world_root)

		_build_environment_and_lights()
		_build_plaza_and_podium()
		_build_barangay_vista()

		_turntable = Node3D.new()
		_turntable.name = "HeroTurntable"
		_turntable.position = STAGE_TURN_POS
		_turntable.rotation.y = DEFAULT_YAW
		_world_root.add_child(_turntable)

	func _process(delta: float) -> void:
		if not _stage_active or _modal_paused:
			return
		_idle_clock += delta
		var breath_yaw: float = 0.0 if Settings.reduced_motion else sin(_idle_clock * 0.75) * 0.045
		_current_yaw = lerpf(_current_yaw, _target_yaw, minf(delta * 10.0, 1.0))
		if _turntable != null:
			_turntable.rotation.y = _current_yaw + breath_yaw

	func rotate_showcase(delta_rad: float) -> void:
		_target_yaw = wrapf(_target_yaw + delta_rad, -PI, PI)

	func sync_motion() -> void:
		if Settings.reduced_motion and _turntable != null:
			_turntable.rotation.y = _current_yaw

	func set_stage_active(active: bool) -> void:
		_stage_active = active
		visible = active
		if _viewport != null:
			_viewport.render_target_update_mode = SubViewport.UPDATE_WHEN_VISIBLE if (active and not _modal_paused) else SubViewport.UPDATE_DISABLED

	func set_paused_for_modal(paused: bool) -> void:
		_modal_paused = paused
		if _viewport != null:
			_viewport.render_target_update_mode = SubViewport.UPDATE_WHEN_VISIBLE if (_stage_active and not paused) else SubViewport.UPDATE_DISABLED

	func set_character(kid_id: StringName, accent: Color) -> void:
		if _aura_ring_mat != null:
			var glow_tint: Color = accent.lerp(TitleScreen.SUNLIT_MANGO, 0.38)
			_aura_ring_mat.albedo_color = Color(glow_tint.lightened(0.18), 0.82)
			_aura_ring_mat.emission = glow_tint
		if _rim_light != null:
			_rim_light.light_color = Color(0.72, 0.88, 1.0).lerp(accent.lightened(0.35), 0.25)
		if _current_kid_id == kid_id and _kid_model != null:
			return
		_current_kid_id = kid_id
		if _kid_model != null:
			_kid_model.queue_free()
			_kid_model = null
		var def_path: String = CHARACTER_DEF_PATH % String(kid_id)
		if not ResourceLoader.exists(def_path):
			def_path = CHARACTER_DEF_PATH % "junjun"
		var def: CharacterDef = load(def_path) as CharacterDef
		if def == null or _turntable == null:
			return
		_kid_model = KidModel.new()
		_kid_model.name = "ShowcaseKid"
		_turntable.add_child(_kid_model)
		_kid_model.setup(def)
		# KidModel faces -Z natively; facing (0, 1) turns rotation.y to PI (+Z toward camera).
		_kid_model.face(Vector2(0.0, 1.0))
		_target_yaw = DEFAULT_YAW
		if not Settings.reduced_motion and is_inside_tree():
			_kid_model.scale = Vector3.ONE * 0.90
			var pop: Tween = create_tween()
			pop.tween_property(_kid_model, "scale", Vector3.ONE, 0.24).set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT)

	func _build_environment_and_lights() -> void:
		var world_env: WorldEnvironment = WorldEnvironment.new()
		world_env.name = "WorldEnv"
		var env: Environment = Environment.new()
		var sky_mat: ShaderMaterial = ShaderMaterial.new()
		sky_mat.shader = TitleScreen.SKY_SHADER
		var sky: Sky = Sky.new()
		sky.sky_material = sky_mat
		env.background_mode = Environment.BG_SKY
		env.sky = sky
		env.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
		env.ambient_light_color = Color(0.96, 0.91, 0.84)
		env.ambient_light_energy = 0.46
		env.tonemap_mode = Environment.TONE_MAPPER_ACES
		env.tonemap_exposure = 0.98
		env.fog_enabled = true
		env.fog_light_color = Color(0.95, 0.88, 0.76)
		env.fog_light_energy = 0.22
		env.fog_density = 0.0014
		env.fog_sky_affect = 0.08
		world_env.environment = env
		_world_root.add_child(world_env)

		var cam: Camera3D = Camera3D.new()
		cam.name = "ShowcaseCamera"
		cam.fov = 31.0
		var cam_pos: Vector3 = Vector3(-0.22, 1.18, 4.68)
		var cam_target: Vector3 = Vector3(0.14, 0.98, 0.0)
		cam.transform = Transform3D(Basis.looking_at(cam_target - cam_pos, Vector3.UP), cam_pos)
		_world_root.add_child(cam)

		var sun: DirectionalLight3D = DirectionalLight3D.new()
		sun.name = "KeySunLight"
		sun.light_color = Color(1.0, 0.94, 0.82)
		sun.light_energy = 0.84
		sun.shadow_enabled = true
		sun.shadow_opacity = 0.68
		sun.directional_shadow_max_distance = 36.0
		sun.directional_shadow_split_1 = 0.22
		sun.shadow_bias = 0.052
		sun.shadow_normal_bias = 1.85
		sun.rotation_degrees = Vector3(-36.0, -28.0, 0.0)
		_world_root.add_child(sun)

		_rim_light = DirectionalLight3D.new()
		_rim_light.name = "HeroRimLight"
		_rim_light.light_color = Color(0.72, 0.88, 1.0)
		_rim_light.light_energy = 0.42
		_rim_light.shadow_enabled = false
		_rim_light.rotation_degrees = Vector3(-22.0, 152.0, 0.0)
		_world_root.add_child(_rim_light)

		var bounce: OmniLight3D = OmniLight3D.new()
		bounce.name = "PodiumWarmFill"
		bounce.light_color = Color(1.0, 0.82, 0.52)
		bounce.light_energy = 0.40
		bounce.omni_range = 3.8
		bounce.position = Vector3(0.32, 0.78, 1.45)
		_world_root.add_child(bounce)

	func _build_plaza_and_podium() -> void:
		# Weathered sunlit Barangay plaza concrete floor using procedural street_cement shader + vertex color
		var st: SurfaceTool = SurfaceTool.new()
		st.begin(Mesh.PRIMITIVE_TRIANGLES)
		var floor_col: Color = Color(0.54, 0.47, 0.40)
		var half_ext: float = 45.0
		var v0: Vector3 = Vector3(-half_ext, 0.0, -half_ext)
		var v1: Vector3 = Vector3(half_ext, 0.0, -half_ext)
		var v2: Vector3 = Vector3(half_ext, 0.0, half_ext)
		var v3: Vector3 = Vector3(-half_ext, 0.0, half_ext)
		for tri_v: Vector3 in [v0, v1, v2, v0, v2, v3]:
			st.set_normal(Vector3.UP)
			st.set_color(floor_col)
			st.add_vertex(tri_v)
		var plaza: MeshInstance3D = MeshInstance3D.new()
		plaza.name = "PlazaFloor"
		plaza.mesh = st.commit()
		plaza.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		var plaza_mat: ShaderMaterial = ShaderMaterial.new()
		plaza_mat.shader = TitleScreen.CEMENT_SHADER
		plaza_mat.set_shader_parameter("wear", 0.85)
		plaza_mat.set_shader_parameter("lane_half_width", 9.5)
		plaza.material_override = plaza_mat
		_world_root.add_child(plaza)

		# Subtle sunlit brick/terracotta plaza apron behind podium
		var apron_mesh: CylinderMesh = CylinderMesh.new()
		apron_mesh.top_radius = 3.6
		apron_mesh.bottom_radius = 3.7
		apron_mesh.height = 0.010
		apron_mesh.radial_segments = 48
		var apron: MeshInstance3D = MeshInstance3D.new()
		apron.mesh = apron_mesh
		apron.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		apron.position = Vector3(STAGE_TURN_POS.x, 0.005, 0.0)
		var apron_mat: StandardMaterial3D = StandardMaterial3D.new()
		apron_mat.albedo_color = Color(0.45, 0.38, 0.32)
		apron_mat.roughness = 0.86
		apron.material_override = apron_mat
		_world_root.add_child(apron)

		# Midground raised painted Barangay sidewalk curb spanning full widescreen horizon
		var curb_mesh: BoxMesh = BoxMesh.new()
		curb_mesh.size = Vector3(48.0, 0.14, 2.8)
		var curb: MeshInstance3D = MeshInstance3D.new()
		curb.mesh = curb_mesh
		curb.position = Vector3(0.0, 0.07, -8.8)
		var curb_mat: StandardMaterial3D = StandardMaterial3D.new()
		curb_mat.albedo_color = Color(0.56, 0.50, 0.43)
		curb_mat.roughness = 0.86
		curb.material_override = curb_mat
		_world_root.add_child(curb)

		# Sleek Low-Profile Barrio Medallion Podium under the selected Kid
		var podium_root: Node3D = Node3D.new()
		podium_root.name = "HeroPodium"
		podium_root.position = Vector3(STAGE_TURN_POS.x, 0.0, STAGE_TURN_POS.z)
		_world_root.add_child(podium_root)

		var base_cyl: CylinderMesh = CylinderMesh.new()
		base_cyl.top_radius = 0.90
		base_cyl.bottom_radius = 0.98
		base_cyl.height = 0.032
		base_cyl.radial_segments = 48
		var base_inst: MeshInstance3D = MeshInstance3D.new()
		base_inst.mesh = base_cyl
		base_inst.position.y = 0.016
		var stone_mat: StandardMaterial3D = StandardMaterial3D.new()
		stone_mat.albedo_color = Color(0.20, 0.17, 0.15)
		stone_mat.roughness = 0.58
		stone_mat.metallic = 0.18
		base_inst.material_override = stone_mat
		podium_root.add_child(base_inst)

		var stage_top: CylinderMesh = CylinderMesh.new()
		stage_top.top_radius = 0.82
		stage_top.bottom_radius = 0.85
		stage_top.height = 0.044
		stage_top.radial_segments = 48
		var top_inst: MeshInstance3D = MeshInstance3D.new()
		top_inst.mesh = stage_top
		top_inst.position.y = 0.022
		var top_mat: StandardMaterial3D = StandardMaterial3D.new()
		top_mat.albedo_color = Color(0.15, 0.14, 0.13)
		top_mat.roughness = 0.46
		top_mat.metallic = 0.28
		top_inst.material_override = top_mat
		podium_root.add_child(top_inst)

		var gold_torus: TorusMesh = TorusMesh.new()
		gold_torus.outer_radius = 0.86
		gold_torus.inner_radius = 0.825
		gold_torus.rings = 48
		gold_torus.ring_segments = 16
		var gold_ring: MeshInstance3D = MeshInstance3D.new()
		gold_ring.mesh = gold_torus
		gold_ring.position.y = 0.040
		var gold_mat: StandardMaterial3D = StandardMaterial3D.new()
		gold_mat.albedo_color = Color("e8b84b")
		gold_mat.metallic = 0.78
		gold_mat.roughness = 0.26
		gold_mat.emission_enabled = true
		gold_mat.emission = Color("c88418")
		gold_mat.emission_energy_multiplier = 0.30
		gold_ring.material_override = gold_mat
		podium_root.add_child(gold_ring)

		var aura_torus: TorusMesh = TorusMesh.new()
		aura_torus.outer_radius = 0.68
		aura_torus.inner_radius = 0.652
		aura_torus.rings = 48
		aura_torus.ring_segments = 12
		var aura_ring: MeshInstance3D = MeshInstance3D.new()
		aura_ring.mesh = aura_torus
		aura_ring.position.y = 0.045
		_aura_ring_mat = StandardMaterial3D.new()
		_aura_ring_mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
		_aura_ring_mat.albedo_color = Color(1.0, 0.83, 0.40, 0.82)
		_aura_ring_mat.roughness = 0.28
		_aura_ring_mat.emission_enabled = true
		_aura_ring_mat.emission = TitleScreen.SUNLIT_MANGO
		_aura_ring_mat.emission_energy_multiplier = 0.55
		aura_ring.material_override = _aura_ring_mat
		podium_root.add_child(aura_ring)

	func _build_barangay_vista() -> void:
		var vista: Node3D = Node3D.new()
		vista.name = "BarangayVista"
		_world_root.add_child(vista)

		# Wall-to-wall 1280x720 Barangay streetscape: dark timber/capiz + mango foliage on left,
		# Sari-Sari + Jeepney in mid-right, and angled 2-storey house + mango canopy filling far-right (x=1045..1280).
		var placements: Array[Dictionary] = [
			{"id": "brgy_house_two_storey", "pos": Vector3(-5.8, 0.0, -13.6), "yaw": 0.38},
			{"id": "brgy_mango_tree", "pos": Vector3(-3.8, 0.0, -12.4), "yaw": 0.24},
			{"id": "brgy_sorbetes_cart", "pos": Vector3(-2.3, 0.0, -9.2), "yaw": 0.52},
			{"id": "brgy_meralco_post", "pos": Vector3(-7.4, 0.0, -10.8), "yaw": 0.15},
			{"id": "brgy_sari_sari", "pos": Vector3(3.5, 0.0, -15.6), "yaw": -0.36},
			{"id": "brgy_jeepney", "pos": Vector3(5.0, 0.0, -11.4), "yaw": -0.62},
			{"id": "brgy_mango_tree", "pos": Vector3(8.0, 0.0, -11.8), "yaw": -0.32},
			{"id": "brgy_house_two_storey", "pos": Vector3(11.6, 0.0, -13.2), "yaw": -0.52},
			{"id": "brgy_coconut_palm", "pos": Vector3(7.4, 0.0, -15.4), "yaw": -0.35},
			{"id": "brgy_waiting_shed", "pos": Vector3(-0.8, 0.0, -21.0), "yaw": 0.06},
		]
		for spec: Dictionary in placements:
			var model_path: String = BARANGAY_PROP_PATH % String(spec["id"])
			if not ResourceLoader.exists(model_path):
				continue
			var packed: PackedScene = load(model_path) as PackedScene
			if packed == null:
				continue
			var prop: Node3D = packed.instantiate() as Node3D
			if prop == null:
				continue
			prop.position = spec["pos"] as Vector3
			prop.rotation.y = float(spec["yaw"])
			vista.add_child(prop)

		# Festive overhead fiesta banderitas spanning wall-to-wall across to the far-right 2-storey house eave
		var band_colors: Array[Color] = [
			TitleScreen.SUNLIT_MANGO,
			TitleScreen.SUNLIT_JADE,
			Color("e2553e"),
			Color("f2d59b"),
		]
		var span_p0: Vector3 = Vector3(-6.2, 3.44, -11.4)
		var span_p1: Vector3 = Vector3(10.2, 3.56, -11.8)
		var flag_count: int = 22
		for i: int in flag_count:
			var t: float = (float(i) + 0.5) / float(flag_count)
			var sag: float = sin(t * PI) * 0.42
			var pt: Vector3 = span_p0.lerp(span_p1, t) - Vector3(0.0, sag, 0.0)
			var prism: PrismMesh = PrismMesh.new()
			prism.size = Vector3(0.30, 0.34, 0.02)
			var flag: MeshInstance3D = MeshInstance3D.new()
			flag.mesh = prism
			flag.position = pt
			flag.rotation_degrees.z = 180.0
			var fmat: StandardMaterial3D = StandardMaterial3D.new()
			fmat.albedo_color = band_colors[i % band_colors.size()]
			fmat.roughness = 0.70
			fmat.cull_mode = BaseMaterial3D.CULL_DISABLED
			flag.material_override = fmat
			vista.add_child(flag)


# =======================================================================================
# WIDESCREEN 4-ZONE GLASSMORPHIC & RADIANT GOLD CONTROLS
# =======================================================================================

class _StageEdgeScrims extends Control:
	func _init() -> void:
		resized.connect(queue_redraw)

	func _draw() -> void:
		if size.x <= 8.0 or size.y <= 8.0:
			return
		var charcoal: Color = Color(0.065, 0.052, 0.042, 1.0)
		# Two-band soft warm-charcoal top header scrim (y = 0..112)
		var top_mid: float = 58.0
		var top_end: float = 114.0
		draw_polygon(
			PackedVector2Array([Vector2(0.0, 0.0), Vector2(size.x, 0.0), Vector2(size.x, top_mid), Vector2(0.0, top_mid)]),
			PackedColorArray([Color(charcoal, 0.62), Color(charcoal, 0.62), Color(charcoal, 0.26), Color(charcoal, 0.26)])
		)
		draw_polygon(
			PackedVector2Array([Vector2(0.0, top_mid), Vector2(size.x, top_mid), Vector2(size.x, top_end), Vector2(0.0, top_end)]),
			PackedColorArray([Color(charcoal, 0.26), Color(charcoal, 0.26), Color(charcoal, 0.0), Color(charcoal, 0.0)])
		)

		# Two-band soft warm-charcoal left rail & kid callout vignette (x = 0..380)
		var left_mid: float = minf(190.0, size.x * 0.18)
		var left_end: float = minf(390.0, size.x * 0.34)
		draw_polygon(
			PackedVector2Array([Vector2(0.0, 0.0), Vector2(left_mid, 0.0), Vector2(left_mid, size.y), Vector2(0.0, size.y)]),
			PackedColorArray([Color(charcoal, 0.52), Color(charcoal, 0.22), Color(charcoal, 0.24), Color(charcoal, 0.56)])
		)
		draw_polygon(
			PackedVector2Array([Vector2(left_mid, 0.0), Vector2(left_end, 0.0), Vector2(left_end, size.y), Vector2(left_mid, size.y)]),
			PackedColorArray([Color(charcoal, 0.22), Color(charcoal, 0.0), Color(charcoal, 0.0), Color(charcoal, 0.24)])
		)

		# Seamless C0-continuous bottom-right corner triangle fan (alpha == 0.0 along all outer edges)
		var br_corner: Vector2 = size
		var p_left: Vector2 = Vector2(maxf(0.0, size.x - 540.0), size.y)
		var p_diag: Vector2 = Vector2(maxf(0.0, size.x - 460.0), maxf(0.0, size.y - 230.0))
		var p_top: Vector2 = Vector2(size.x, maxf(0.0, size.y - 310.0))
		var c_corner: Color = Color(charcoal, 0.38)
		var c_zero: Color = Color(charcoal, 0.0)
		draw_polygon(
			PackedVector2Array([br_corner, p_left, p_diag]),
			PackedColorArray([c_corner, c_zero, c_zero])
		)
		draw_polygon(
			PackedVector2Array([br_corner, p_diag, p_top]),
			PackedColorArray([c_corner, c_zero, c_zero])
		)


class _TopHeaderTrim extends Control:
	func _init() -> void:
		resized.connect(queue_redraw)

	func _draw() -> void:
		if size.x <= 8.0:
			return
		var y: float = size.y - 1.0
		draw_line(Vector2(18.0, y), Vector2(size.x - 18.0, y), Color(TitleScreen.SUNLIT_CREAM, 0.14), 1.0, true)
		draw_line(Vector2(318.0, y), Vector2(506.0, y), Color(TitleScreen.SUNLIT_MANGO, 0.58), 2.0, true)


class _StatusDot extends Control:
	var _color: Color = TitleScreen.SUNLIT_JADE

	func set_color(col: Color) -> void:
		_color = col
		queue_redraw()

	func _draw() -> void:
		var c: Vector2 = size * 0.5
		draw_circle(c, 5.0, Color(_color, 0.32))
		draw_circle(c, 3.6, _color)


class _GoldAccentRule extends Control:
	func _draw() -> void:
		if size.x <= 4.0:
			return
		var pts: PackedVector2Array = PackedVector2Array([
			Vector2(0.0, 1.0), Vector2(size.x, 1.0), Vector2(size.x, 3.0), Vector2(0.0, 3.0)
		])
		var cols: PackedColorArray = PackedColorArray([
			Color(TitleScreen.SUNLIT_MANGO, 0.92), Color(TitleScreen.SUNLIT_MANGO, 0.0),
			Color(TitleScreen.SUNLIT_MANGO, 0.0), Color(TitleScreen.SUNLIT_MANGO, 0.92),
		])
		draw_polygon(pts, cols)


class _ProfileCapsuleButton extends Button:
	const FACE_PATH: String = "res://assets/portraits/faces/%s.png"
	var _avatar_tex: Texture2D
	var _callsign_label: Label
	var _rank_label: Label
	var _emblem: _RankEmblemCanvas
	var _progress: float = 0.0
	var _tier_color: Color = TitleScreen.SUNLIT_MANGO

	func _init() -> void:
		flat = true
		focus_mode = Control.FOCUS_ALL
		for state: String in ["normal", "hover", "pressed", "hover_pressed"]:
			add_theme_stylebox_override(state, StyleBoxEmpty.new())
		add_theme_stylebox_override("focus", UiSkin.focus_ring(UiSkin.RADIUS_L))
		mouse_entered.connect(queue_redraw)
		mouse_exited.connect(queue_redraw)
		button_down.connect(queue_redraw)
		button_up.connect(queue_redraw)

		var text_col: VBoxContainer = VBoxContainer.new()
		text_col.position = Vector2(62.0, 10.0)
		text_col.size = Vector2(214.0, 52.0)
		text_col.alignment = BoxContainer.ALIGNMENT_CENTER
		text_col.add_theme_constant_override("separation", -2)
		text_col.mouse_filter = Control.MOUSE_FILTER_IGNORE
		add_child(text_col)

		_callsign_label = Label.new()
		_callsign_label.name = "ProfileNameLabel"
		_callsign_label.text = "Player"
		_callsign_label.add_theme_font_override("font", KalyeahTheme.STAGE_TITLE_FONT)
		_callsign_label.add_theme_font_size_override("font_size", UiMetrics.DETAIL)
		_callsign_label.add_theme_color_override("font_color", TitleScreen.SUNLIT_CREAM)
		_callsign_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
		text_col.add_child(_callsign_label)

		var rank_row: HBoxContainer = HBoxContainer.new()
		rank_row.add_theme_constant_override("separation", 5)
		rank_row.mouse_filter = Control.MOUSE_FILTER_IGNORE
		text_col.add_child(rank_row)

		_emblem = _RankEmblemCanvas.new()
		_emblem.name = "MiniRankEmblem"
		_emblem.custom_minimum_size = Vector2(18.0, 18.0)
		_emblem.size_flags_vertical = Control.SIZE_SHRINK_CENTER
		_emblem.mouse_filter = Control.MOUSE_FILTER_IGNORE
		rank_row.add_child(_emblem)

		_rank_label = Label.new()
		_rank_label.name = "RankTierLabel"
		_rank_label.text = "Kahoy · 0 LP"
		_rank_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		_rank_label.add_theme_font_override("font", KalyeahTheme.STAGE_SUB_FONT)
		_rank_label.add_theme_font_size_override("font_size", UiMetrics.MIN_TEXT)
		_rank_label.add_theme_color_override("font_color", Color("ffd466"))
		_rank_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
		rank_row.add_child(_rank_label)

	func set_profile_data(player_name: String, avatar_id: String, tier_name: String, lp: int, progress: float, tier_color: Color) -> void:
		_callsign_label.text = player_name if not player_name.is_empty() else "Player"
		_rank_label.text = "%s · %d LP" % [tier_name, lp]
		_emblem.set_lp(lp)
		_progress = clampf(progress, 0.06, 1.0)
		_tier_color = tier_color
		var custom_photo: Texture2D = IdPhoto.texture()
		if custom_photo != null:
			_avatar_tex = custom_photo
		else:
			var fpath: String = FACE_PATH % avatar_id
			if ResourceLoader.exists(fpath):
				_avatar_tex = load(fpath) as Texture2D
			else:
				_avatar_tex = KidModel.portrait(StringName(avatar_id))
		queue_redraw()

	func _draw() -> void:
		var capsule_rect: Rect2 = Rect2(0.0, 8.0, size.x, 64.0)
		var hovered: bool = is_hovered()
		KomiksStyle.draw_chamfered_glass_card(self, capsule_rect, TitleScreen.SUNLIT_MANGO, false, hovered, 10.0)

		# Avatar portrait frame on left aligned on y = 40.0 optical axis
		var av_center: Vector2 = Vector2(32.0, 40.0)
		draw_circle(av_center, 22.0, Color("1a1512"))
		if _avatar_tex != null:
			var av_rect: Rect2 = Rect2(av_center - Vector2.ONE * 19.0, Vector2.ONE * 38.0)
			draw_texture_rect(_avatar_tex, av_rect, false)
		draw_arc(av_center, 20.5, 0.0, TAU, 28, TitleScreen.SUNLIT_MANGO, 2.2, true)

		# Subtle edit icon at right edge of capsule
		KomiksStyle.draw_stage_glyph(self, &"edit", Vector2(size.x - 18.0, 26.0), 7.0, Color(TitleScreen.SUNLIT_CREAM, 0.76 if hovered else 0.48))

		# Sleek LP progress bar along lower inner edge
		var bar_x: float = 62.0
		var bar_w: float = size.x - 76.0
		var bar_y: float = capsule_rect.end.y - 5.0
		draw_line(Vector2(bar_x, bar_y), Vector2(bar_x + bar_w, bar_y), Color(1.0, 1.0, 1.0, 0.16), 2.5, true)
		draw_line(Vector2(bar_x, bar_y), Vector2(bar_x + bar_w * _progress, bar_y), _tier_color.lightened(0.15), 2.5, true)


class _GlassIconButton extends Button:
	var _glyph: StringName
	var _active: bool = false

	func _init(glyph: StringName, a11y: String) -> void:
		_glyph = glyph
		accessibility_name = a11y
		tooltip_text = a11y
		flat = true
		focus_mode = Control.FOCUS_ALL
		custom_minimum_size = Vector2(TitleScreen.WIDE_HIT_FLOOR, TitleScreen.WIDE_HIT_FLOOR)
		size = Vector2(TitleScreen.WIDE_HIT_FLOOR, TitleScreen.WIDE_HIT_FLOOR)
		for state: String in ["normal", "hover", "pressed", "hover_pressed"]:
			add_theme_stylebox_override(state, StyleBoxEmpty.new())
		add_theme_stylebox_override("focus", UiSkin.focus_ring(UiSkin.RADIUS_L))
		UiKit.feel_press(self)
		mouse_entered.connect(queue_redraw)
		mouse_exited.connect(queue_redraw)
		button_down.connect(queue_redraw)
		button_up.connect(queue_redraw)

	func set_active(active_now: bool) -> void:
		_active = active_now
		queue_redraw()

	func _draw() -> void:
		var center: Vector2 = size * 0.5
		var hovered: bool = is_hovered()
		var med_r: float = 22.0
		var bg: Color = KalyeahTheme.GLASS_ACTIVE if _active else (KalyeahTheme.GLASS_HOVER if hovered else KalyeahTheme.GLASS_DARK)
		draw_circle(center + Vector2(0.0, 2.0), med_r, Color(0.03, 0.02, 0.02, 0.34))
		draw_circle(center, med_r, bg)
		var rim_col: Color = TitleScreen.SUNLIT_MANGO if (_active or hovered) else KalyeahTheme.GLASS_RIM
		draw_arc(center, med_r, 0.0, TAU, 28, rim_col, 1.8 if _active else 1.3, true)
		var icon_col: Color = Color("ffd466") if _active else TitleScreen.SUNLIT_CREAM
		KomiksStyle.draw_stage_glyph(self, _glyph, center, 10.5, icon_col)


class _WildRiftNavTab extends Button:
	var _glyph: StringName
	var _active: bool = false
	var _title_label: Label
	var _sub_label: Label

	func _init(title_str: String, sub_str: String, glyph: StringName, a11y: String) -> void:
		_glyph = glyph
		accessibility_name = a11y
		tooltip_text = a11y
		flat = true
		focus_mode = Control.FOCUS_ALL
		for state: String in ["normal", "hover", "pressed", "hover_pressed"]:
			add_theme_stylebox_override(state, StyleBoxEmpty.new())
		add_theme_stylebox_override("focus", UiSkin.focus_ring(UiSkin.RADIUS_M))
		UiKit.feel_press(self)
		mouse_entered.connect(queue_redraw)
		mouse_exited.connect(queue_redraw)

		var col: VBoxContainer = VBoxContainer.new()
		col.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		col.offset_left = 40.0
		col.offset_right = -6.0
		col.alignment = BoxContainer.ALIGNMENT_CENTER
		col.add_theme_constant_override("separation", 0)
		col.mouse_filter = Control.MOUSE_FILTER_IGNORE
		add_child(col)

		_title_label = Label.new()
		_title_label.text = title_str
		_title_label.add_theme_font_override("font", KalyeahTheme.STAGE_DISPLAY_FONT)
		_title_label.add_theme_font_size_override("font_size", UiMetrics.ACTION)
		_title_label.add_theme_color_override("font_shadow_color", Color(0.04, 0.03, 0.02, 0.80))
		_title_label.add_theme_constant_override("shadow_offset_x", 0)
		_title_label.add_theme_constant_override("shadow_offset_y", 2)
		_title_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
		col.add_child(_title_label)

		_sub_label = Label.new()
		_sub_label.text = sub_str
		_sub_label.add_theme_font_override("font", KalyeahTheme.STAGE_SUB_FONT)
		_sub_label.add_theme_font_size_override("font_size", UiMetrics.MIN_TEXT)
		_sub_label.add_theme_color_override("font_shadow_color", Color(0.04, 0.03, 0.02, 0.72))
		_sub_label.add_theme_constant_override("shadow_offset_x", 0)
		_sub_label.add_theme_constant_override("shadow_offset_y", 1)
		_sub_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
		col.add_child(_sub_label)
		_sync_colors()

	func set_active(active_now: bool) -> void:
		_active = active_now
		_sync_colors()
		queue_redraw()

	func _sync_colors() -> void:
		if _title_label != null:
			_title_label.add_theme_color_override("font_color", Color("ffe380") if _active else TitleScreen.SUNLIT_CREAM)
		if _sub_label != null:
			_sub_label.add_theme_color_override("font_color", TitleScreen.SUNLIT_MANGO if _active else Color(TitleScreen.SUNLIT_CREAM, 0.72))

	func _draw() -> void:
		var hovered: bool = is_hovered()
		if _active or hovered:
			var band: Rect2 = Rect2(0.0, 6.0, size.x, size.y - 12.0)
			var left_a: float = 0.80 if _active else 0.46
			var pts: PackedVector2Array = PackedVector2Array([
				band.position,
				Vector2(band.end.x, band.position.y),
				band.end,
				Vector2(band.position.x, band.end.y),
			])
			var cols: PackedColorArray = PackedColorArray([
				Color(0.16, 0.12, 0.08, left_a),
				Color(0.16, 0.12, 0.08, 0.0),
				Color(0.16, 0.12, 0.08, 0.0),
				Color(0.16, 0.12, 0.08, left_a),
			])
			draw_polygon(pts, cols)

		# Luminous vertical accent indicator bar on the left edge
		if _active:
			draw_rect(Rect2(0.0, 10.0, 4.0, size.y - 20.0), Color("ffd466"))
			draw_rect(Rect2(4.0, 14.0, 2.0, size.y - 28.0), Color(TitleScreen.SUNLIT_MANGO, 0.45))
		elif hovered:
			draw_rect(Rect2(0.0, 18.0, 3.0, size.y - 36.0), Color(TitleScreen.SUNLIT_CREAM, 0.55))

		var icon_col: Color = Color("ffd466") if _active else (TitleScreen.SUNLIT_CREAM if hovered else Color(TitleScreen.SUNLIT_CREAM, 0.82))
		KomiksStyle.draw_stage_glyph(self, _glyph, Vector2(20.0, size.y * 0.5), 10.0, icon_col)


class _ModeSegmentPill extends Button:
	const PILL_H: float = 42.0
	var _accent: Color
	var _active: bool = false
	var _label: Label

	func _init(label_str: String, accent: Color, a11y: String) -> void:
		_accent = accent
		accessibility_name = a11y
		tooltip_text = a11y
		flat = true
		focus_mode = Control.FOCUS_ALL
		for state: String in ["normal", "hover", "pressed", "hover_pressed"]:
			add_theme_stylebox_override(state, StyleBoxEmpty.new())
		add_theme_stylebox_override("focus", UiSkin.focus_ring(UiSkin.RADIUS_M))
		UiKit.feel_press(self)
		mouse_entered.connect(queue_redraw)
		mouse_exited.connect(queue_redraw)
		resized.connect(_sync_box)

		_label = Label.new()
		_label.text = label_str
		_label.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
		_label.add_theme_font_override("font", KalyeahTheme.STAGE_TITLE_FONT)
		_label.add_theme_font_size_override("font_size", UiMetrics.MIN_TEXT)
		_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
		add_child(_label)
		_sync()
		_sync_box()

	func _sync_box() -> void:
		if _label == null:
			return
		var h: float = maxf(size.y, TitleScreen.WIDE_HIT_FLOOR)
		_label.offset_top = h - PILL_H - 4.0
		_label.offset_bottom = -4.0

	func set_active(active_now: bool) -> void:
		_active = active_now
		_sync()
		queue_redraw()

	func _sync() -> void:
		if _label != null:
			_label.add_theme_color_override("font_color", Color("ffe680") if _active else Color(TitleScreen.SUNLIT_CREAM, 0.84))

	func _draw() -> void:
		var pill_rect: Rect2 = Rect2(0.0, size.y - PILL_H - 4.0, size.x, PILL_H)
		KomiksStyle.draw_chamfered_glass_card(self, pill_rect, _accent, _active, is_hovered(), 8.0)


class _ChamferedGlassButton extends Button:
	var visual_height: float = 66.0
	var show_chevron: bool = false
	var _centered: bool = false
	var _accent: Color = TitleScreen.SUNLIT_MANGO
	var _text_col: VBoxContainer
	var _title_label: Label
	var _sub_label: Label

	func _init(title_str: String, sub_str: String, accent: Color, a11y: String) -> void:
		_accent = accent
		accessibility_name = a11y
		tooltip_text = a11y
		flat = true
		focus_mode = Control.FOCUS_ALL
		for state: String in ["normal", "hover", "pressed", "hover_pressed"]:
			add_theme_stylebox_override(state, StyleBoxEmpty.new())
		add_theme_stylebox_override("focus", UiSkin.focus_ring(UiSkin.RADIUS_M))
		UiKit.feel_press(self)
		mouse_entered.connect(queue_redraw)
		mouse_exited.connect(queue_redraw)
		resized.connect(_sync_insets)

		_text_col = VBoxContainer.new()
		_text_col.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		_text_col.alignment = BoxContainer.ALIGNMENT_CENTER
		_text_col.add_theme_constant_override("separation", -2)
		_text_col.mouse_filter = Control.MOUSE_FILTER_IGNORE
		add_child(_text_col)

		_title_label = Label.new()
		_title_label.text = title_str
		_title_label.add_theme_font_override("font", KalyeahTheme.STAGE_TITLE_FONT)
		_title_label.add_theme_font_size_override("font_size", UiMetrics.MIN_TEXT)
		_title_label.add_theme_color_override("font_color", TitleScreen.SUNLIT_CREAM)
		_title_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
		_text_col.add_child(_title_label)

		_sub_label = Label.new()
		_sub_label.text = sub_str
		_sub_label.add_theme_font_override("font", KalyeahTheme.STAGE_SUB_FONT)
		_sub_label.add_theme_font_size_override("font_size", UiMetrics.MIN_TEXT)
		_sub_label.add_theme_color_override("font_color", Color("ffd466"))
		_sub_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
		_text_col.add_child(_sub_label)
		_sync_insets()

	func _sync_insets() -> void:
		if _text_col == null:
			return
		var h: float = maxf(size.y, TitleScreen.WIDE_HIT_FLOOR)
		var y_pad: float = maxf(4.0, (h - visual_height) * 0.5 + 4.0)
		_text_col.offset_top = y_pad
		_text_col.offset_bottom = -y_pad
		_text_col.offset_left = 10.0 if _centered else 14.0
		_text_col.offset_right = -32.0 if show_chevron else (-10.0 if _centered else -12.0)

	func set_centered(enabled: bool) -> void:
		_centered = enabled
		var align: HorizontalAlignment = HORIZONTAL_ALIGNMENT_CENTER if enabled else HORIZONTAL_ALIGNMENT_LEFT
		if _title_label != null:
			_title_label.horizontal_alignment = align
		if _sub_label != null:
			_sub_label.horizontal_alignment = align
		_sync_insets()
		queue_redraw()

	func set_show_chevron(enabled: bool) -> void:
		show_chevron = enabled
		_sync_insets()
		queue_redraw()

	func set_copy(title_str: String, sub_str: String, accent: Color) -> void:
		_accent = accent
		_title_label.text = title_str
		_sub_label.text = sub_str
		_sub_label.add_theme_color_override("font_color", accent.lightened(0.24))
		_sync_insets()
		queue_redraw()

	func _draw() -> void:
		var y0: float = (size.y - visual_height) * 0.5
		var card_rect: Rect2 = Rect2(0.0, y0, size.x, visual_height)
		KomiksStyle.draw_chamfered_glass_card(self, card_rect, _accent, true, is_hovered(), 10.0)
		if show_chevron:
			KomiksStyle.draw_stage_glyph(self, &"chevron_down", Vector2(size.x - 13.0, size.y * 0.5), 7.5, _accent.lightened(0.25))


class _WildRiftGoldCtaButton extends Button:
	var _title_label: Label
	var _sub_label: Label
	var _glint_clock: float = 0.0

	func _init() -> void:
		flat = true
		focus_mode = Control.FOCUS_ALL
		for state: String in ["normal", "hover", "pressed", "hover_pressed"]:
			add_theme_stylebox_override(state, StyleBoxEmpty.new())
		add_theme_stylebox_override("focus", UiSkin.focus_ring(UiSkin.RADIUS_L))
		UiKit.feel_press(self)
		mouse_entered.connect(queue_redraw)
		mouse_exited.connect(queue_redraw)
		button_down.connect(queue_redraw)
		button_up.connect(queue_redraw)

		var col: VBoxContainer = VBoxContainer.new()
		col.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		col.offset_left = 54.0
		col.offset_right = -20.0
		col.offset_top = 6.0
		col.offset_bottom = -6.0
		col.alignment = BoxContainer.ALIGNMENT_CENTER
		col.add_theme_constant_override("separation", -2)
		col.mouse_filter = Control.MOUSE_FILTER_IGNORE
		add_child(col)

		_title_label = Label.new()
		_title_label.name = "CtaTitle"
		_title_label.text = "MAGLARO"
		_title_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		_title_label.add_theme_font_override("font", KalyeahTheme.STAGE_DISPLAY_FONT)
		_title_label.add_theme_font_size_override("font_size", UiMetrics.HEADLINE)
		_title_label.add_theme_color_override("font_color", Color("1f150d"))
		_title_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
		col.add_child(_title_label)

		_sub_label = Label.new()
		_sub_label.name = "CtaSub"
		_sub_label.text = "PLAY · 5v5"
		_sub_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		_sub_label.add_theme_font_override("font", KalyeahTheme.STAGE_SUB_FONT)
		_sub_label.add_theme_font_size_override("font_size", UiMetrics.MIN_TEXT)
		_sub_label.add_theme_color_override("font_color", Color("452c14"))
		_sub_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
		col.add_child(_sub_label)

	func set_cta_copy(main_str: String, sub_str: String) -> void:
		_title_label.text = main_str
		_sub_label.text = sub_str
		queue_redraw()

	func _process(delta: float) -> void:
		if not is_visible_in_tree() or Settings.reduced_motion:
			return
		_glint_clock = fmod(_glint_clock + delta, 3.8)
		if _glint_clock <= 1.1:
			queue_redraw()

	func _draw() -> void:
		var glint: float = (_glint_clock / 0.95) if (not Settings.reduced_motion and _glint_clock <= 0.95) else -1.0
		var body_rect: Rect2 = Rect2(2.0, 2.0, size.x - 4.0, size.y - 4.0)
		KomiksStyle.draw_chamfered_gold_cta(self, body_rect, is_hovered(), button_pressed, glint)
		KomiksStyle.draw_stage_glyph(self, &"play", Vector2(38.0, size.y * 0.5), 13.0, Color("26190e"))


# =======================================================================================
# PRESERVED PORTRAIT / RANK CREST / DOCK HELPERS
# =======================================================================================

class _KomiksNavDock extends Control:
	const DOCK_TOP: Color = Color(0.24, 0.19, 0.16, 0.90)
	const DOCK_BOTTOM: Color = Color(0.16, 0.14, 0.13, 0.96)
	var _pts: PackedVector2Array = PackedVector2Array()
	var _cols: PackedColorArray = PackedColorArray([
		DOCK_TOP,
		DOCK_TOP,
		DOCK_BOTTOM,
		DOCK_BOTTOM,
	])

	func _init() -> void:
		_pts.resize(4)
		resized.connect(_rebuild)

	func _rebuild() -> void:
		_pts[0] = Vector2(0.0, 0.0)
		_pts[1] = Vector2(size.x, 0.0)
		_pts[2] = Vector2(size.x, size.y)
		_pts[3] = Vector2(0.0, size.y)
		queue_redraw()

	func _draw() -> void:
		if size.x <= 0.0 or size.y <= 0.0:
			return
		if _pts[2] != size:
			_pts[1] = Vector2(size.x, 0.0)
			_pts[2] = Vector2(size.x, size.y)
			_pts[3] = Vector2(0.0, size.y)
		draw_polygon(_pts, _cols)
		draw_rect(Rect2(0.0, 0.0, size.x, 2.0), Color(TitleScreen.SUNLIT_MANGO, 0.85))
		draw_rect(Rect2(0.0, 2.0, size.x, 1.0), Color(TitleScreen.SUNLIT_CREAM, 0.48))
		var wing: float = maxf(0.0, (size.x - TitleScreen.WIDE_TAB_WIDTH) * 0.5 - 24.0)
		if wing > 24.0:
			draw_rect(Rect2(16.0, 6.0, wing, 2.0), Color(TitleScreen.SUNLIT_JADE, 0.52))
			draw_rect(Rect2(size.x - 16.0 - wing, 6.0, wing, 2.0), Color(TitleScreen.SUNLIT_JADE, 0.52))


class _RankEmblemCanvas extends Control:
	var lp: int = 0
	var _script_ref: Script = null
	var _brand_crest: Texture2D = null
	var _checked: bool = false

	func set_lp(new_lp: int) -> void:
		lp = new_lp
		queue_redraw()

	func _draw() -> void:
		if not _checked:
			_checked = true
			if ResourceLoader.exists(TitleScreen.RANKED_SYSTEM_PATH):
				_script_ref = load(TitleScreen.RANKED_SYSTEM_PATH) as Script
			if ResourceLoader.exists(TitleScreen.BRAND_CREST_PATH):
				_brand_crest = load(TitleScreen.BRAND_CREST_PATH) as Texture2D
		var center: Vector2 = size * 0.5
		var radius: float = minf(size.x, size.y) * 0.45
		if _script_ref != null:
			_script_ref.call(&"draw_emblem", self, center, radius, lp)
		elif _brand_crest != null:
			var side: float = radius * 2.0
			draw_texture_rect(_brand_crest, Rect2(center - Vector2.ONE * radius, Vector2.ONE * side), false)
		else:
			draw_circle(center, radius, TitleScreen.SUNLIT_ESPRESSO)
			draw_circle(center, radius * 0.86, TitleScreen.SUNLIT_MANGO)
			draw_circle(center, radius * 0.56, TitleScreen.SUNLIT_JADE)


class _RankCrestBadge extends PanelContainer:
	var _emblem: _RankEmblemCanvas
	var _tier_label: Label
	var _lp_label: Label

	static func _signboard_style() -> StyleBoxFlat:
		var box: StyleBoxFlat = StyleBoxFlat.new()
		box.bg_color = TitleScreen.SUNLIT_CREAM
		box.set_corner_radius_all(UiSkin.RADIUS_M)
		box.set_border_width_all(2)
		box.border_color = TitleScreen.SUNLIT_MANGO.darkened(0.08)
		box.shadow_color = Color(TitleScreen.SUNLIT_ESPRESSO, 0.22)
		box.shadow_size = 6
		box.shadow_offset = Vector2(0.0, 3.0)
		box.content_margin_left = 12.0
		box.content_margin_right = 14.0
		box.content_margin_top = 8.0
		box.content_margin_bottom = 8.0
		box.anti_aliasing = true
		return box

	static func _accent_bar_style() -> StyleBoxFlat:
		var bar: StyleBoxFlat = StyleBoxFlat.new()
		bar.bg_color = TitleScreen.SUNLIT_JADE
		bar.set_corner_radius_all(UiSkin.round_radius(4.0))
		bar.anti_aliasing = true
		return bar

	func _init() -> void:
		mouse_filter = Control.MOUSE_FILTER_IGNORE
		custom_minimum_size = TitleScreen.RANK_CREST_SIZE
		add_theme_stylebox_override("panel", _signboard_style())
		var row: HBoxContainer = HBoxContainer.new()
		row.add_theme_constant_override("separation", 10)
		row.alignment = BoxContainer.ALIGNMENT_CENTER
		row.mouse_filter = Control.MOUSE_FILTER_IGNORE
		add_child(row)
		_emblem = _RankEmblemCanvas.new()
		_emblem.name = "RankEmblem"
		_emblem.custom_minimum_size = Vector2(62.0, 62.0)
		_emblem.size_flags_vertical = Control.SIZE_SHRINK_CENTER
		_emblem.mouse_filter = Control.MOUSE_FILTER_IGNORE
		row.add_child(_emblem)
		var accent: Panel = Panel.new()
		accent.name = "JadeAccent"
		accent.custom_minimum_size = Vector2(4.0, 48.0)
		accent.size_flags_vertical = Control.SIZE_SHRINK_CENTER
		accent.mouse_filter = Control.MOUSE_FILTER_IGNORE
		accent.add_theme_stylebox_override("panel", _accent_bar_style())
		row.add_child(accent)
		var text_col: VBoxContainer = VBoxContainer.new()
		text_col.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		text_col.alignment = BoxContainer.ALIGNMENT_CENTER
		text_col.add_theme_constant_override("separation", 2)
		text_col.mouse_filter = Control.MOUSE_FILTER_IGNORE
		row.add_child(text_col)
		_tier_label = Label.new()
		_tier_label.name = "RankTierLabel"
		_tier_label.add_theme_font_override("font", KalyeahTheme.DISPLAY_FONT)
		_tier_label.add_theme_font_size_override("font_size", UiMetrics.ACTION)
		_tier_label.add_theme_color_override("font_color", TitleScreen.SUNLIT_ESPRESSO)
		_tier_label.autowrap_mode = TextServer.AUTOWRAP_OFF
		_tier_label.clip_text = true
		_tier_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
		text_col.add_child(_tier_label)
		_lp_label = Label.new()
		_lp_label.name = "RankLpLabel"
		_lp_label.add_theme_font_override("font", KalyeahTheme.CAPTION_FONT)
		_lp_label.add_theme_font_size_override("font_size", UiMetrics.MIN_TEXT)
		_lp_label.add_theme_color_override("font_color", TitleScreen.SUNLIT_JADE_TEXT)
		_lp_label.autowrap_mode = TextServer.AUTOWRAP_OFF
		_lp_label.clip_text = true
		_lp_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
		text_col.add_child(_lp_label)
		if ResourceLoader.exists(TitleScreen.BRAND_CREST_PATH):
			var seal_tex: Texture2D = load(TitleScreen.BRAND_CREST_PATH) as Texture2D
			if seal_tex != null:
				var seal: TextureRect = TextureRect.new()
				seal.name = "BrandSeal"
				seal.texture = seal_tex
				seal.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
				seal.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
				seal.custom_minimum_size = Vector2(28.0, 28.0)
				seal.size_flags_vertical = Control.SIZE_SHRINK_CENTER
				seal.mouse_filter = Control.MOUSE_FILTER_IGNORE
				row.add_child(seal)

	func set_rank(tier_name: String, lp: int) -> void:
		_tier_label.text = tier_name
		_lp_label.text = "RANGGO · %d LP" % lp
		_emblem.set_lp(lp)
