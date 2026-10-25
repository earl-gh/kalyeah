extends GutTest

const SettingsScript: GDScript = preload("res://scripts/autoload/settings.gd")


func test_addresses_are_normalized_to_websocket_urls() -> void:
	assert_eq(SettingsScript.normalize_url("kalyeah.onrender.com"), "wss://kalyeah.onrender.com")
	assert_eq(SettingsScript.normalize_url("https://kalyeah.onrender.com"), "wss://kalyeah.onrender.com")
	assert_eq(SettingsScript.normalize_url("localhost:8080"), "ws://localhost:8080")
	assert_eq(SettingsScript.normalize_url(" ws://192.168.1.5:8080 "), "ws://192.168.1.5:8080")
	assert_eq(SettingsScript.normalize_url(""), "")


func test_the_project_has_a_server_url_setting() -> void:
	assert_true(ProjectSettings.has_setting("kalyeah/network/server_url"))
