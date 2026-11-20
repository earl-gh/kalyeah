# Kid busts

`assets/portraits/busts/<kid>.png` are the Kids grid's card art: each kid's own game model rendered chest-up on a transparent
background in Blender (Cycles), lit like the home stall (warm key from the skylight side, cool rim). They replace nothing:
the older `portraits/cards` keep their match uses.

```
for k in narda jumong inggo jopay oriang paeng kikay karding manoy tere pamela domeng; do
  blender -b -P tools/art/kid_busts/bust.py -- assets/models/characters/$k.glb /tmp/${k}_b.png /tmp/${k}_f.png 32
  python -c "from PIL import Image; Image.open('/tmp/${k}_b.png').resize((360, 480)).save('assets/portraits/busts/$k.png')"
done
```

Each kid is scaled to 1.5 m by its mesh bounds and framed from the top of the head, so every bust sits at the same height.
