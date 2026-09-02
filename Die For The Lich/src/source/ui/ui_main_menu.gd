class_name UIMainMenu extends Control

const MetaProgression = preload("res://mods/meta_progression.gd")
const MetaFont = preload("res://fonts/FontDefault.tres")

@onready var thunders: Array[Panel] = [$Image / Thunder1, $Image / Thunder2]
@onready var thunders_timer: Timer = $TimerThunders
@onready var fogs: Array[Control] = [$Image / Fog1, $Image / Fog2]
@onready var back_buffer: BackBufferCopy = $"BackBufferCopy[Thunder]"
@onready var buttons: Control = $Buttons
@onready var warnings_container: Control = $Warnings
var meta_button: UIBtnText
var meta_button_label: Label
var meta_overlay: ColorRect
var meta_shards_label: Label
var meta_upgrade_buttons: Dictionary[String, Button]
var meta_respec_button: Button

func _ready() -> void :
	thunders_timer.timeout.connect(_thunder)
	$Buttons / Quit.pressed.connect(get_tree().quit)
	_setup_meta_progression_ui()

	await owner.ready



















func toggle(enable: bool) -> void :
	visible = enable
	if enable:
		_refresh_meta_progression_ui()
		thunders_timer.start(1.0)
	else:
		meta_overlay.visible = false
		thunders_timer.stop()


func _unhandled_input(event: InputEvent) -> void :
	if meta_overlay && meta_overlay.visible && event.is_action_pressed("cancel"):
		meta_overlay.visible = false
		get_viewport().set_input_as_handled()


func _setup_meta_progression_ui() -> void :
	meta_button = $Buttons / Options.duplicate(
		Node.DUPLICATE_GROUPS | Node.DUPLICATE_SCRIPTS | Node.DUPLICATE_USE_INSTANTIATION
	) as UIBtnText
	meta_button.name = "Legacy"
	buttons.add_child(meta_button)
	buttons.move_child(meta_button, 2)
	meta_button_label = meta_button.get_node("Button/Control/Label") as Label
	_set_meta_button_text("Legacy")
	meta_button.pressed.connect(_open_meta_progression)

	meta_overlay = ColorRect.new()
	meta_overlay.name = "MetaProgression"
	meta_overlay.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	meta_overlay.color = Color(0.015, 0.01, 0.02, 0.94)
	meta_overlay.mouse_filter = Control.MOUSE_FILTER_STOP
	meta_overlay.z_index = 100
	meta_overlay.visible = false
	add_child(meta_overlay)

	var panel: PanelContainer = PanelContainer.new()
	panel.name = "Panel"
	panel.set_anchors_and_offsets_preset(Control.PRESET_CENTER)
	panel.offset_left = -410.0
	panel.offset_top = -330.0
	panel.offset_right = 410.0
	panel.offset_bottom = 330.0
	var panel_style: StyleBoxFlat = StyleBoxFlat.new()
	panel_style.bg_color = Color("18121d")
	panel_style.border_color = Color("8d6133")
	panel_style.set_border_width_all(3)
	panel_style.set_corner_radius_all(10)
	panel_style.content_margin_left = 34.0
	panel_style.content_margin_top = 26.0
	panel_style.content_margin_right = 34.0
	panel_style.content_margin_bottom = 24.0
	panel.add_theme_stylebox_override("panel", panel_style)
	meta_overlay.add_child(panel)

	var content: VBoxContainer = VBoxContainer.new()
	content.add_theme_constant_override("separation", 10)
	panel.add_child(content)

	var title: Label = _meta_label("LEGACY", 40, Color("e8b85a"))
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	content.add_child(title)

	meta_shards_label = _meta_label("", 28, Color.WHITE)
	meta_shards_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	content.add_child(meta_shards_label)

	var description: Label = _meta_label(
		"Soul Shards survive between runs. All upgrades can be fully refunded.\nLegacy bonuses are disabled in Daily Runs.",
		17,
		Color("c9c2cf"),
	)
	description.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	description.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	content.add_child(description)

	var upgrades_scroll: ScrollContainer = ScrollContainer.new()
	upgrades_scroll.custom_minimum_size = Vector2(0, 300)
	# Keep the viewport bounded as the upgrade catalog grows; only its contents scroll.
	upgrades_scroll.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	upgrades_scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	upgrades_scroll.vertical_scroll_mode = ScrollContainer.SCROLL_MODE_AUTO
	upgrades_scroll.clip_contents = true
	content.add_child(upgrades_scroll)

	var upgrades_content: VBoxContainer = VBoxContainer.new()
	upgrades_content.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	upgrades_content.add_theme_constant_override("separation", 8)
	upgrades_scroll.add_child(upgrades_content)

	for key: String in MetaProgression.UPGRADE_ORDER:
		var upgrade_button: Button = Button.new()
		upgrade_button.custom_minimum_size = Vector2(0, 72)
		upgrade_button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		upgrade_button.add_theme_font_override("font", MetaFont)
		upgrade_button.add_theme_font_size_override("font_size", 18)
		upgrade_button.add_theme_color_override("font_color", Color("d8d2d9"))
		upgrade_button.add_theme_color_override("font_hover_color", Color.WHITE)
		upgrade_button.add_theme_color_override("font_pressed_color", Color("e8b85a"))
		upgrade_button.add_theme_color_override("font_disabled_color", Color("9c939d"))
		upgrade_button.add_theme_stylebox_override("normal", _meta_upgrade_style(Color("09070b"), Color("5a505c")))
		upgrade_button.add_theme_stylebox_override("hover", _meta_upgrade_style(Color("211824"), Color("b07a3b")))
		upgrade_button.add_theme_stylebox_override("pressed", _meta_upgrade_style(Color("2b1c20"), Color("e8b85a")))
		upgrade_button.add_theme_stylebox_override("disabled", _meta_upgrade_style(Color("0b090c"), Color("403a42")))
		upgrade_button.add_theme_stylebox_override("focus", StyleBoxEmpty.new())
		upgrade_button.pressed.connect(_purchase_meta_upgrade.bind(key))
		upgrades_content.add_child(upgrade_button)
		meta_upgrade_buttons[key] = upgrade_button

	var spacer: Control = Control.new()
	spacer.custom_minimum_size.y = 4.0
	content.add_child(spacer)

	var footer: HBoxContainer = HBoxContainer.new()
	footer.add_theme_constant_override("separation", 12)
	content.add_child(footer)

	meta_respec_button = Button.new()
	meta_respec_button.custom_minimum_size = Vector2(0, 54)
	meta_respec_button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	meta_respec_button.add_theme_font_override("font", MetaFont)
	meta_respec_button.add_theme_font_size_override("font_size", 20)
	meta_respec_button.pressed.connect(_respec_meta_progression)
	footer.add_child(meta_respec_button)

	var close_button: Button = Button.new()
	close_button.custom_minimum_size = Vector2(0, 54)
	close_button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	close_button.text = "CLOSE"
	close_button.add_theme_font_override("font", MetaFont)
	close_button.add_theme_font_size_override("font_size", 22)
	close_button.pressed.connect(_close_meta_progression)
	footer.add_child(close_button)

	_refresh_meta_progression_ui()


func _meta_label(text_value: String, font_size: int, color: Color) -> Label:
	var label: Label = Label.new()
	label.text = text_value
	label.add_theme_font_override("font", MetaFont)
	label.add_theme_font_size_override("font_size", font_size)
	label.add_theme_color_override("font_color", color)
	return label


func _meta_upgrade_style(background: Color, border: Color) -> StyleBoxFlat:
	var style: StyleBoxFlat = StyleBoxFlat.new()
	style.bg_color = background
	style.border_color = border
	style.set_border_width_all(2)
	style.set_corner_radius_all(6)
	style.content_margin_left = 14.0
	style.content_margin_right = 14.0
	style.content_margin_top = 6.0
	style.content_margin_bottom = 6.0
	return style


func _open_meta_progression() -> void :
	_refresh_meta_progression_ui()
	meta_overlay.visible = true


func _close_meta_progression() -> void :
	meta_overlay.visible = false
	_refresh_meta_progression_ui()


func _purchase_meta_upgrade(key: String) -> void :
	MetaProgression.purchase(key)
	_refresh_meta_progression_ui()


func _respec_meta_progression() -> void :
	MetaProgression.respec()
	_refresh_meta_progression_ui()


func _set_meta_button_text(value: String) -> void :
	meta_button.text = value
	if is_instance_valid(meta_button_label):
		meta_button_label.text = value


func _refresh_meta_progression_ui() -> void :
	if !is_instance_valid(meta_button):
		return
	var data: Dictionary = MetaProgression.load_data()
	var shards: int = int(data["shards"])
	_set_meta_button_text("Legacy  [%d]" % shards)
	if is_instance_valid(meta_shards_label):
		meta_shards_label.text = "SOUL SHARDS: %d" % shards
	if is_instance_valid(meta_respec_button):
		var refund: int = MetaProgression.get_respec_refund(data)
		meta_respec_button.text = "RESPEC ALL  (+%d)" % refund
		meta_respec_button.disabled = refund <= 0
	for key: String in MetaProgression.UPGRADE_ORDER:
		var definition: Dictionary = MetaProgression.get_definition(key)
		var level: int = int(data["upgrades"].get(key, 0))
		var max_level: int = int(definition["max_level"])
		var upgrade_button: Button = meta_upgrade_buttons.get(key)
		if !is_instance_valid(upgrade_button):
			continue
		if level >= max_level:
			upgrade_button.text = "%s  %d/%d\n%s   -   MAXIMUM" % [
				definition["name"], level, max_level, definition["description"],
			]
			upgrade_button.disabled = true
		else:
			var cost: int = MetaProgression.get_cost(key, level)
			upgrade_button.text = "%s  %d/%d\n%s   -   COST: %d" % [
				definition["name"], level, max_level, definition["description"], cost,
			]
			upgrade_button.disabled = shards < cost


func _thunder() -> void :
	var tween = create_tween()

	thunders[0].visible = true
	back_buffer.visible = true
	tween.set_parallel()
	tween.tween_property(thunders[0], "visible", false, 0.0).set_delay(0.15)
	tween.tween_property(thunders[1], "visible", true, 0.0).set_delay(0.15)
	tween.tween_property(thunders[1], "visible", false, 0.0).set_delay(0.3)
	tween.tween_property(fogs[0], "visible", false, 0.0).set_delay(0.15)
	tween.tween_property(fogs[1], "visible", false, 0.0).set_delay(0.15)
	tween.tween_property(fogs[0], "visible", true, 0.0).set_delay(0.3)
	tween.tween_property(fogs[1], "visible", true, 0.0).set_delay(0.3)
	tween.tween_property(back_buffer, "visible", false, 0.0).set_delay(0.3)

	tween.tween_callback(GM.game.sfx["thunder"].play_stream).set_delay(0.5)

	thunders_timer.start(10.0 + randf() * 10.0)
