#!/bin/bash
# Rebuilds one building: tools/art/buildings/run.sh <id> <work dir>
#   PY: a Python 3 with numpy and Pillow (default python3); BPY: a Python with Blender's bpy (required).
#   <work dir>/<id>/orig.glb must hold the owner's model (see README for which commit).
# Writes <work dir>/<id>/final.glb, ready to copy to assets/models/props/<id>.glb.
set -e
here=$(cd "$(dirname "$0")" && pwd); id=$1; w=$(cd "$2" && pwd)/$id; PY=${PY:-python3}
[ -n "$BPY" ] || { echo "set BPY to a Python with bpy"; exit 1; }
mkdir -p $w/ph $w/v3
cp $here/config/$id.json $w/config.json
$PY -c "from PIL import Image; Image.new('RGB',(64,64),(128,128,128)).save('$w/ph/prop_base_color.png')"
$BPY $here/geo_base.py -- $w/orig.glb $w/geo.glb > /dev/null 2>&1 || [ -f $w/geo.glb ]     # bpy may crash on exit after writing
$PY $here/parts.py $here $w/geo.glb $w/piece.npy > /dev/null
$PY $here/features.py $here $w/geo.glb $w/piece.npy $w/features.json
$PY $here/materials.py $w/features.json $w/config.json $w/mat.json > /dev/null
$BPY $here/roof_build.py -- $w/orig.glb $w/config.json $w/ph/prop_base_color.png $w/r1.glb $w/regions.json > /dev/null 2>&1 || [ -f $w/r1.glb ]
signs=$($PY -c "import json;print(','.join(str(i) for i in json.load(open('$w/config.json')).get('sign',[])))")
cp $w/r1.glb $w/r1p.glb
if [ -n "$signs" ]; then
  $PY $here/sign_raw.py $here $w/geo.glb $w/piece.npy $signs 1600 $w/sign_raw.png > /dev/null
  $PY $here/band.py $w/sign_raw_box.json 0.78 $w/band.json > /dev/null
  $PY -c "
import json; F={f['k']:f for f in json.load(open('$w/features.json'))}
json.dump([[F[int(i)]['lo'],F[int(i)]['hi']] for i in '$signs'.split(',')], open('$w/sign_pieces.json','w'))"
  $PY $here/uv_patch.py $here $w/r1p.glb $w/band.json $w/sign_pieces.json > /dev/null
  sign=$here/signs/$id.png
else
  echo '{"u0":0,"v0":0,"w":0,"h":0,"shrink":0.78,"x0":0,"x1":1,"y0":0,"y1":1}' > $w/band.json
  $PY $here/shrink_only.py $w/r1p.glb 0.78 > /dev/null; sign=-
fi
$PY $here/paint3.py $here $w/r1p.glb $w/geo.glb $w/features.json $w/mat.json $w/regions.json $w/config.json $w/band.json $sign $w/v3/prop_base_color.png
$BPY $here/roof_build.py -- $w/orig.glb $w/config.json $w/v3/prop_base_color.png $w/final.glb $w/regions_final.json > /dev/null 2>&1 || [ -f $w/final.glb ]
if [ -n "$signs" ]; then $PY $here/uv_patch.py $here $w/final.glb $w/band.json $w/sign_pieces.json > /dev/null; else $PY $here/shrink_only.py $w/final.glb 0.78 > /dev/null; fi
cmp -s $w/regions.json $w/regions_final.json || { echo "the two builds differ"; exit 1; }
echo "$id: $w/final.glb"
