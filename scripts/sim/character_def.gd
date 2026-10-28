class_name CharacterDef
extends Resource
## A cosmetic character (docs/PROJECT.md 3.2). Never affects gameplay: same HP,
## speed, hitbox and timings for everyone. The outfit fields only drive the
## low-poly model (scripts/view/art/kid_model.gd); every kid shares one body.

enum Hair { SHORT, PONYTAIL, PIGTAILS, CAP_BACKWARD, BUZZ, SPIKY, CURLY, LONG_WAVY }
enum Top { SANDO, TEE_KNOTTED, BESTIDA, JERSEY, POLO_STRIPED, PE_SHIRT }
enum Bottom { SHORTS, CARGO_SHORTS, JOGGING_PANTS, NONE }
enum Extra { NONE, BIMPO, HAIR_CLIP, HEADBAND_BELT_BAG, ICE_CANDY, PONY_BANDS }

@export var id: StringName = &""
@export var display_name: String = ""
## What the kid wears and how they look, for the Book (never gameplay).
@export_multiline var description: String = ""
## Main outfit colour (also used for small UI accents).
@export var tint: Color = Color.WHITE
@export var skin: Color = Color(0.78, 0.58, 0.42)
@export var hair_color: Color = Color(0.1, 0.08, 0.07)
@export var hair: Hair = Hair.SHORT
@export var top: Top = Top.TEE_KNOTTED
@export var top_accent: Color = Color.WHITE
@export var bottom: Bottom = Bottom.SHORTS
@export var bottom_color: Color = Color(0.2, 0.3, 0.6)
## Rubber shoes (true) or tsinelas.
@export var shoes: bool = false
@export var shoe_color: Color = Color(0.2, 0.5, 0.9)
@export var extra: Extra = Extra.NONE
@export var extra_color: Color = Color.WHITE
## Seconds into the owner's Cast_OneHand (standing) and Cast_Run (moving) clips where the hand releases: the clip starts
## there so the release lands on the sim's cast tick. Measured on the GLB (docs/ART.md "Authored kids"): the time the
## right hand has covered half of its travel over the cast's swing (Cast_OneHand) or raise (Cast_Run), at 1/60 s.
@export var cast_release_standing: float = 0.0
@export var cast_release_moving: float = 0.0
## Visual body width (Popoy is chubbier). Never the hitbox: that is GameRules.
@export var body_width: float = 1.0
