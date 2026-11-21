#!/usr/bin/env bash
# Build every game-ready multi-LOD kid from assets/models/raw/ (apex-rig-forge R1-R3, R5, R7).
# Runs strictly sequentially (never parallel Blender instances).
# usage:
#   BLENDER="/c/Program Files/Blender Foundation/Blender 5.2/blender.exe" build_all_kids.sh <work dir> [Kid ...]
#   PY=<python with bpy> build_all_kids.sh <work dir> [Kid ...]
#   INSTALL=1 ...   also installs <kid>.glb, _lod1.glb, _lod2.glb, _2.png, _orm.png, _rig.json to assets/models/characters/
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
root="$(cd "$here/../../.." && pwd)"
raw="$root/assets/models/raw"
clips="$here/clips/roster_clips.glb"
limits="$root/unity/Data/rig__joint_limits.json"
chars_dst="$root/assets/models/characters"

work="$1"; shift
kids=("$@")
[ ${#kids[@]} -gt 0 ] || kids=(Narda Jumong Inggo Jopay Oriang Paeng Kikay Karding Manoy Tere Pamela Domeng)

if [ -z "${PY:-}" ]; then
  if [ -z "${BLENDER:-}" ]; then
    if [ -x "/c/Program Files/Blender Foundation/Blender 5.2/blender.exe" ]; then
      BLENDER="/c/Program Files/Blender Foundation/Blender 5.2/blender.exe"
    else
      BLENDER="$(command -v blender || true)"
    fi
  fi
  if [ -z "${BLENDER:-}" ]; then
    echo "error: set BLENDER=<blender.exe> or PY=<python-with-bpy>" >&2
    exit 1
  fi
fi

run_bpy() {
  local script="$1"; shift
  if [ -n "${PY:-}" ]; then
    "$PY" "$script" "$@"
  else
    "$BLENDER" --background --python-exit-code 1 --python-expr \
      "import sys, runpy; i = sys.argv.index('--'); sys.argv = sys.argv[i+1:]; runpy.run_path(sys.argv[0], run_name='__main__')" \
      -- "$script" "$@"
  fi
}

mkdir -p "$work"/{fixed,norm,contract,retopo,out,review}
[ -f "$work/ref_rest.json" ] || run_bpy "$here/build_reference.py" "$clips" "$work/ref_rest.json"

for kid in "${kids[@]}"; do
  src="$raw/$kid.glb"
  lower="$(echo "$kid" | tr '[:upper:]' '[:lower:]')"
  echo "=== [$kid] starting pipeline ==="

  case "$kid" in
    Paeng)         run_bpy "$here/tube_fingers.py" "$src" "$work/fixed/$kid.glb" Bone_019,Bone_024 pinky; src="$work/fixed/$kid.glb" ;;
    Domeng|Oriang) run_bpy "$here/fix_fingers.py" "$src" "$work/fixed/$kid.glb" Bone_019,Bone_024; src="$work/fixed/$kid.glb" ;;
    Jopay)         run_bpy "$here/fix_fingers.py" "$src" "$work/fixed/$kid.glb" Bone_019,Bone_024 force; src="$work/fixed/$kid.glb" ;;
    Manoy)         run_bpy "$here/fix_fingers.py" "$src" "$work/fixed/$kid.glb" Bone_018,Bone_023; src="$work/fixed/$kid.glb" ;;
    Tere)          run_bpy "$here/fingers_from_scratch.py" "$src" "$work/fixed/$kid.glb" LeftHand,RightHand; src="$work/fixed/$kid.glb" ;;
  esac

  run_bpy "$here/normalize_kid.py" "$src" "$work/norm/$kid.glb"
  run_bpy "$here/contract_kid.py" "$work/norm/$kid.glb" "$work/ref_rest.json" \
    "$work/contract/$kid.glb" "$work/out/${lower}_rig.json" "$lower"

  run_bpy "$here/retopo_kid.py" "$work/contract/$kid.glb" "$clips" "$work/ref_rest.json" \
    "$work/retopo/${lower}.glb" 20000

  cp "$work/retopo/${lower}_2.png" "$work/out/${lower}_2.png"
  cp "$work/retopo/${lower}_orm.png" "$work/out/${lower}_orm.png"

  limits_arg=()
  [ -f "$limits" ] && limits_arg=(--limits "$limits")
  run_bpy "$here/bake_correctives.py" "$work/retopo/${lower}.glb" "$work/out/${lower}.glb" \
    "${limits_arg[@]}" --rig-json "$work/out/${lower}_rig.json" --kid "$lower"

  run_bpy "$here/build_lod_tiers.py" "$work/out/${lower}.glb" \
    "$work/out/${lower}_lod1.glb" "$work/out/${lower}_lod2.glb" \
    --rig-json "$work/out/${lower}_rig.json" --kid "$lower"

  if [ "${INSTALL:-0}" = "1" ]; then
    cp "$work/out/${lower}.glb" \
       "$work/out/${lower}_lod1.glb" \
       "$work/out/${lower}_lod2.glb" \
       "$work/out/${lower}_2.png" \
       "$work/out/${lower}_orm.png" \
       "$work/out/${lower}_rig.json" \
       "$chars_dst/"
  fi
  echo "=== [$kid] done ==="
done
