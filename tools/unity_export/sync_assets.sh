#!/usr/bin/env bash
# Copies what the Unity project needs from the Godot project and the shared sim into unity/Game, before Unity opens it.
# Nothing it writes is kept in git (unity/.gitignore, unity/Game/.gitignore): the Godot project stays the one source of the art.
#
#   assets/models/{characters,barangay,props,weapons}, assets/{textures,audio,fonts,ui,icons,portraits}
#       -> unity/Game/Assets/Imported/Resources/<same path under assets/>  (glTFast imports each .glb as a prefab; Resources.Load finds it)
#   unity/Data/*.json
#       -> unity/Game/Assets/Imported/Resources/Data/                       (TextAssets: Resources.Load<TextAsset>("Data/rules__game_rules"))
#   unity/Kalyeah.Sim/**/*.cs + Kalyeah.Sim.asmdef
#       -> unity/Game/Packages/com.kalyeah.sim/Runtime/                     (an embedded package; its package.json is written here too)
#
# The package version says whether the sim port has landed (a `class MatchSim` in unity/Kalyeah.Sim): 1.0.0 when it has, 0.1.0 while
# not. Kalyeah.Game.Core.asmdef turns 1.0.0 into the KALYEAH_SIM_PORTED define (versionDefines), which switches SimBridge from its
# stand-in to the real sim. Godot-only files (*.import, *.uid, *.tres, *.tscn, *.gd, shaders) are left out, and the .meta files Unity
# writes next to the copies are kept, so a second run does not make Unity import everything again.
#
# Usage: tools/unity_export/sync_assets.sh   (from anywhere in the repository; needs bash, uses rsync when it is installed, else cp)
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
GAME="$ROOT/unity/Game"
IMPORTED="$GAME/Assets/Imported/Resources"
SIM_SRC="$ROOT/unity/Kalyeah.Sim"
SIM_PKG="$GAME/Packages/com.kalyeah.sim"

ASSET_DIRS=(models/characters models/barangay models/props models/weapons textures audio fonts ui icons portraits)
GODOT_ONLY=('*.import' '*.uid' '*.tres' '*.res' '*.tscn' '*.gd' '*.gdshader' '*.godot')

# mirror SRC DEST [exclude-glob...]: DEST gets every file of SRC except the excluded ones. With rsync, files gone from SRC are removed
# from DEST too (Unity's .meta files are always kept). Without rsync it only copies over: run `git clean -xdf unity/Game/Assets/Imported`
# after deleting an asset in the Godot project.
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

# The shared sim as an embedded package: sources and the asmdef only (no .NET build output, no csproj).
mirror "$SIM_SRC" "$SIM_PKG/Runtime" 'bin' 'obj' '*.csproj' '*.csproj.user'
# the network layer (rooms, protocol, client, hotspot host and finder) rides in the same package, in its own assembly (Kalyeah.Net.asmdef)
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
