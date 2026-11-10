#!/bin/bash
# Rebuilds one building: tools/art/buildings/run_one.sh <id> <work dir>
#   PY:  a Python 3 with numpy and Pillow (default python3); BPY: a Python with Blender's bpy (required)
# Reads assets/models/props/<id>.glb as first added (git history) from <work dir>/<id>/orig.glb; writes <work dir>/<id>/final.glb.
set -e
here=$(cd "$(dirname "$0")" && pwd); id=$1; w=$2/$id; PY=${PY:-python3}
[ -n "$BPY" ] || { echo "set BPY to a Python with bpy"; exit 1; }
mkdir -p $w/named
[ -f $w/config.json ] || cp $here/config/$id.json $w/config.json
[ -f $w/sign_tones.json ] || cp $here/config/${id}_sign_tones.json $w/sign_tones.json 2>/dev/null || true
$BPY $here/geo_base.py -- $w/orig.glb $w/geo.glb || [ -f $w/geo.glb ]   # bpy may crash on exit after writing
$PY $here/parts.py $here $w/geo.glb $w/piece.npy > /dev/null
$PY $here/features.py $here $w/geo.glb $w/piece.npy $w/features.json
signs=$($PY -c "import json;print(','.join(str(i) for i in json.load(open('$w/config.json')).get('sign',[])))")
if [ -n "$signs" ]; then
  $PY $here/paint_generic.py $here $w/geo.glb $w/piece.npy $w/features.json $w/config.json $w/classes.json $w/painted.png 1720
  $PY $here/sign_raw.py $here $w/geo.glb $w/piece.npy $signs 1600 $w/sign_raw.png
  $PY -c "
import json; F={f['k']:f for f in json.load(open('$w/features.json'))}
json.dump([[F[int(i)]['lo'],F[int(i)]['hi']] for i in '$signs'.split(',')], open('$w/sign_pieces.json','w'))"
  if [ "$id" = tricycle_terminal ]; then
    $PY $here/sign_letter.py "TRICYCLE  TERMINAL" /usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf "#EEE7D8" "#A8352B" "#1D2B57" 1988 251 $w/sign_clean.png
  else
    $PY $here/sign_clean_generic.py $w/sign_raw.png $w/sign_tones.json $w/sign_clean.png
  fi
  $PY $here/compose.py $w/painted.png $w/sign_clean.png $w/sign_raw_box.json $w/named/prop_base_color.png $w/band.json
  $BPY $here/build_bld.py -- $w/orig.glb $w/config.json $w/named/prop_base_color.png $w/final.glb || [ -f $w/final.glb ]
  $PY $here/uv_patch.py $here $w/final.glb $w/band.json $w/sign_pieces.json
else
  $PY $here/paint_generic.py $here $w/geo.glb $w/piece.npy $w/features.json $w/config.json $w/classes.json $w/named/prop_base_color.png 2048
  $BPY $here/build_bld.py -- $w/orig.glb $w/config.json $w/named/prop_base_color.png $w/final.glb || [ -f $w/final.glb ]
fi
