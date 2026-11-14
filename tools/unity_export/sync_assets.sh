#!/usr/bin/env bash
# Copies what the Unity project needs from the Godot project and the shared sim into unity/Game, before Unity opens it.
# Nothing it writes is kept in git (unity/.gitignore, unity/Game/.gitignore): the Godot project stays the one source of the art.
#
# Delegates to tools/unity_export/sync_assets.py (cross-platform Python 3 + Pillow .webp -> .png conversion + i18n CSV sync)
# when Python 3 is available, keeping a bash fallback below.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

for py in python3 python; do
	if command -v "$py" > /dev/null 2>&1 && "$py" -c 'import sys; raise SystemExit(0 if sys.version_info >= (3, 8) else 1)' > /dev/null 2>&1; then
		exec "$py" "$ROOT/tools/unity_export/sync_assets.py" "$@"
	fi
done

GAME="$ROOT/unity/Game"
IMPORTED="$GAME/Assets/Imported/Resources"
SIM_SRC="$ROOT/unity/Kalyeah.Sim"
SIM_PKG="$GAME/Packages/com.kalyeah.sim"

ASSET_DIRS=(models/characters models/barangay models/props models/weapons textures audio fonts ui icons portraits)
GODOT_ONLY=('*.import' '*.uid' '*.tres' '*.res' '*.tscn' '*.gd' '*.gdshader' '*.godot' '.gdignore')

mirror() {
	local src="$1" dest="$2"
	shift 2
	mkdir -p "$dest"
	local glob
	if command -v rsync > /dev/null 2>&1; then
		local args=(-a --delete --prune-empty-dirs --exclude '*.meta')
		for glob in "$@"; do args+=(--exclude "$glob"); done
		rsync "${args[@]}" "$src/" "$dest/"
	else
		cp -R "$src/." "$dest/"
		for glob in "$@"; do
			find "$dest" -name "$glob" ! -name '*.meta' -prune -exec rm -rf {} +
		done
		find "$dest" -mindepth 1 -type d -empty -delete
	fi
}

for dir in "${ASSET_DIRS[@]}"; do
	if [[ -d "$ROOT/assets/$dir" ]]; then
		mirror "$ROOT/assets/$dir" "$IMPORTED/$dir" "${GODOT_ONLY[@]}"
	else
		echo "sync_assets: assets/$dir is missing, skipped" >&2
	fi
done

mirror "$ROOT/unity/Data" "$IMPORTED/Data"
if [[ -f "$ROOT/data/i18n/strings.csv" ]]; then
	cp "$ROOT/data/i18n/strings.csv" "$IMPORTED/Data/i18n__strings.csv"
fi

mirror "$SIM_SRC" "$SIM_PKG/Runtime" 'bin' 'obj' '*.csproj' '*.csproj.user'
mirror "$ROOT/unity/Kalyeah.Net" "$SIM_PKG/Net" 'bin' 'obj' '*.csproj' '*.csproj.user'
version="0.1.0"
if grep -rqsE --include='*.cs' 'class[[:space:]]+MatchSim([^A-Za-z0-9_]|$)' "$SIM_SRC"; then
	version="1.0.0"
fi
cat > "$SIM_PKG/package.json" << JSON
{
  "name": "com.kalyeah.sim",
  "displayName": "Kalyeah Sim",
  "version": "$version",
  "unity": "6000.0",
  "description": "The game's rules: engine-free C# shared with the server and the .NET tests. Written by tools/unity_export/sync_assets.sh from unity/Kalyeah.Sim; edit the sim there, not here."
}
JSON

count=$(find "$GAME/Assets/Imported" "$SIM_PKG" -type f ! -name '*.meta' | wc -l | tr -d ' ')
echo "sync_assets: $count files in unity/Game/Assets/Imported and unity/Game/Packages/com.kalyeah.sim (sim package $version)"
