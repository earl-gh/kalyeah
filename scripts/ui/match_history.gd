class_name MatchHistory
extends RefCounted
## The last battles this phone played (mode, result, score), kept in a small local file so the
## History tab can list them. Nothing leaves the phone.

const MAX_ENTRIES: int = 20

## Where the history is kept (tests point it elsewhere).
static var path: String = "user://history.cfg"
static var _user_writable: int = -1


static func _can_write_user_dir() -> bool:
	if _user_writable < 0:
		if DirAccess.open("user://") != null:
			var probe_path: String = "user://.history_write_probe.cfg"
			var probe: ConfigFile = ConfigFile.new()
			probe.set_value("probe", "ok", true)
			if probe.save(probe_path) == OK:
				DirAccess.remove_absolute(probe_path)
				if FileAccess.file_exists(probe_path):
					DirAccess.remove_absolute(ProjectSettings.globalize_path(probe_path))
				_user_writable = 1
				return true
		_user_writable = 0
	return _user_writable == 1


static func _resolved_path() -> String:
	if path.begins_with("user://") and not _can_write_user_dir():
		return "res://.godot/_test_tmp_" + path.get_file()
	return path


## Adds a finished battle. `mode` is "practice", "hotspot" or "online".
## Optional `lp_delta` and `tier` attach Tagalog Ranked progression metadata when provided.
static func record(mode: String, won: bool, mine: int, theirs: int, lp_delta: int = 0, tier: String = "") -> void:
	var list: Array[Dictionary] = entries()
	var entry: Dictionary = {
		"mode": mode,
		"won": won,
		"mine": mine,
		"theirs": theirs,
		"at": int(Time.get_unix_time_from_system()),
	}
	if lp_delta != 0:
		entry["lp_delta"] = lp_delta
	if not tier.is_empty():
		entry["tier"] = tier
	list.push_front(entry)
	while list.size() > MAX_ENTRIES:
		list.pop_back()
	var file: ConfigFile = ConfigFile.new()
	file.set_value("history", "entries", list)
	file.save(_resolved_path())


## Newest first.
static func entries() -> Array[Dictionary]:
	var file: ConfigFile = ConfigFile.new()
	var found: Array[Dictionary] = []
	if file.load(_resolved_path()) != OK:
		return found
	for item: Variant in file.get_value("history", "entries", []) as Array:
		if item is Dictionary:
			found.append(item as Dictionary)
			if found.size() == MAX_ENTRIES:
				break
	return found


## The numbers over a list of battles (newest first): how many were played and won, the win rate as a whole
## percent, and the current streak (wins in a row since the newest; 0 if the newest was lost).
static func summary(list: Array[Dictionary]) -> Dictionary:
	var wins: int = 0
	for entry: Dictionary in list:
		if bool(entry.get("won", false)):
			wins += 1
	var streak: int = 0
	for entry: Dictionary in list:
		if not bool(entry.get("won", false)):
			break
		streak += 1
	var rate: int = roundi(100.0 * float(wins) / float(list.size())) if not list.is_empty() else 0
	return {"played": list.size(), "wins": wins, "rate": rate, "streak": streak}


## Only the battles of one `mode` ("practice", "hotspot" or "online"); an empty `mode` keeps them all.
static func of_mode(list: Array[Dictionary], mode: String) -> Array[Dictionary]:
	if mode.is_empty():
		return list
	var kept: Array[Dictionary] = []
	for entry: Dictionary in list:
		if String(entry.get("mode", "")) == mode:
			kept.append(entry)
	return kept


static func clear() -> void:
	var target: String = _resolved_path()
	DirAccess.remove_absolute(target)
	if FileAccess.file_exists(target):
		DirAccess.remove_absolute(ProjectSettings.globalize_path(target))


## "just now", "5 min ago", "3 h ago", "2 days ago".
static func ago(at: int, now: int) -> String:
	var seconds: int = maxi(0, now - at)
	if seconds < 60:
		return "just now"
	if seconds < 3600:
		return "%d min ago" % (seconds / 60)
	if seconds < 86400:
		return "%d h ago" % (seconds / 3600)
	return "%d days ago" % (seconds / 86400)
