class_name MatchHistory
extends RefCounted
## The last battles this phone played (mode, result, score), kept in a small local file so the
## History tab can list them. Nothing leaves the phone.

const MAX_ENTRIES: int = 30

## Where the history is kept (tests point it elsewhere).
static var path: String = "user://history.cfg"


## Adds a finished battle. `mode` is "practice", "hotspot" or "online".
static func record(mode: String, won: bool, mine: int, theirs: int) -> void:
	var list: Array[Dictionary] = entries()
	list.push_front({"mode": mode, "won": won, "mine": mine, "theirs": theirs, "at": int(Time.get_unix_time_from_system())})
	while list.size() > MAX_ENTRIES:
		list.pop_back()
	var file: ConfigFile = ConfigFile.new()
	file.set_value("history", "entries", list)
	file.save(path)


## Newest first.
static func entries() -> Array[Dictionary]:
	var file: ConfigFile = ConfigFile.new()
	var found: Array[Dictionary] = []
	if file.load(path) != OK:
		return found
	for item: Variant in file.get_value("history", "entries", []) as Array:
		if item is Dictionary:
			found.append(item as Dictionary)
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
	DirAccess.remove_absolute(path)


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
