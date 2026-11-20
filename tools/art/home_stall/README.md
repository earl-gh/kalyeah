# Home stall plate

`assets/ui/home/stall.jpg` is the home screen's backdrop: a Palengke stall's roll-up shutter with Karding's chalk map of the
market on it, under the tin roof's skylight shafts (`docs/MAPS.md`, `docs/LORE.md`). It is rendered in Blender (Cycles) with
no kid in it: the game draws the selected kid live in front of it, and the plate is shot through the same camera as
`HeroStage` (4.3 m back, 1.0 m up, aimed at 0.8 m), opened from 26 to 32 degrees tall, so both share one perspective.

```
python tools/art/home_stall/chalk_map.py /tmp/chalk_map.png
blender -b -P tools/art/home_stall/stall_scene.py -- assets/models/characters/karding.glb /tmp/chalk_map.png /tmp/unused.png /tmp/stall.png home 64
python -c "from PIL import Image; Image.open('/tmp/stall.png').convert('RGB').save('assets/ui/home/stall.jpg', quality=88)"
```

The kid model is loaded only to size the scene and is hidden in the `home` shot. `HomeScreenView.PlateOverHero` and
`PlateAspect` must change with the camera angle (32 degrees) or the 2400 by 1000 size.
