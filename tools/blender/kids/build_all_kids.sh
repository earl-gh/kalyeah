#!/usr/bin/env bash
# Build every game-ready kid from assets/models/raw/ (see README.md "Game-ready kids").
# usage: PY=<python with bpy> build_all_kids.sh <work dir> [Kid ...]
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
root="$(cd "$here/../../.." && pwd)"
raw="$root/assets/models/raw"
clips="$here/clips/roster_clips.glb"
work="$1"; shift
kids=("$@")
[ ${#kids[@]} -gt 0 ] || kids=(Narda Jumong Inggo Jopay Oriang Paeng Kikay Karding Manoy Tere Pamela Domeng)
mkdir -p "$work"/{fixed,norm,contract,out,review}
[ -f "$work/ref_rest.json" ] || "$PY" "$here/build_reference.py" "$clips" "$work/ref_rest.json"
for kid in "${kids[@]}"; do
  src="$raw/$kid.glb"
  case "$kid" in   # hand repairs from README.md
    Paeng)  "$PY" "$here/tube_fingers.py" "$src" "$work/fixed/$kid.glb" Bone_019,Bone_024 pinky; src="$work/fixed/$kid.glb" ;;
    Domeng|Oriang) "$PY" "$here/fix_fingers.py" "$src" "$work/fixed/$kid.glb" Bone_019,Bone_024; src="$work/fixed/$kid.glb" ;;
    Jopay)  "$PY" "$here/fix_fingers.py" "$src" "$work/fixed/$kid.glb" Bone_019,Bone_024 force; src="$work/fixed/$kid.glb" ;;
    Manoy)  "$PY" "$here/fix_fingers.py" "$src" "$work/fixed/$kid.glb" Bone_018,Bone_023; src="$work/fixed/$kid.glb" ;;
    Tere)   "$PY" "$here/fingers_from_scratch.py" "$src" "$work/fixed/$kid.glb" LeftHand,RightHand; src="$work/fixed/$kid.glb" ;;
  esac
  "$PY" "$here/normalize_kid.py" "$src" "$work/norm/$kid.glb"
  SKIP=hinge,helper,twist,stray "$PY" "$here/contract_kid.py" "$work/norm/$kid.glb" "$work/ref_rest.json" \
    "$work/contract/$kid.glb" "$work/contract/${kid}_rig.json"
  lower="$(echo "$kid" | tr '[:upper:]' '[:lower:]')"
  RENDER="$work/review/$kid" "$PY" "$here/retopo_kid.py" "$work/contract/$kid.glb" "$clips" "$work/ref_rest.json" \
    "$work/out/$lower.glb" 20000
  echo "=== $kid done"
done
