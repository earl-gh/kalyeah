class_name UiTones
extends RefCounted
## The interface's own sounds (owner, 2026-11-03: the menus must not reuse the weapons' sound effects). Each is a few soft notes made in
## code (sine / triangle / noise with a quick fade, mixed into a 16-bit mono `AudioStreamWAV`), so there is no sound file to ship or import.
## `UiSound.ROLES` names the tone of each role; the level and the pitch spread stay there. View only.

const RATE: int = 22050
## Every note fades in this long (s) so it never starts on a click, and out over its last `RELEASE` seconds.
const ATTACK: float = 0.004
const RELEASE: float = 0.02
const WAVES: PackedStringArray = ["sine", "tri", "square", "noise"]

## Tone name -> notes. A note: {at: when it starts (s), len: how long (s), from: start pitch (Hz), to: end pitch (Hz, a glide), wave: one of
## `WAVES`, gain: 0..1, decay: how fast it dies away (higher = shorter)}. A noise note's pitch is the brightness of the hiss.
const TONES: Dictionary[String, Array] = {
	# a small soft pop: buttons, switches
	"pop": [{"at": 0.0, "len": 0.06, "from": 880.0, "to": 560.0, "wave": "sine", "gain": 0.9, "decay": 38.0}],
	# a light click: a segment of a choice
	"click": [{"at": 0.0, "len": 0.04, "from": 1500.0, "to": 1300.0, "wave": "tri", "gain": 0.7, "decay": 70.0}],
	# two notes climbing: picking a card, a kid, a tab
	"pick": [
		{"at": 0.0, "len": 0.07, "from": 660.0, "to": 660.0, "wave": "tri", "gain": 0.7, "decay": 30.0},
		{"at": 0.05, "len": 0.1, "from": 880.0, "to": 880.0, "wave": "tri", "gain": 0.8, "decay": 24.0},
	],
	# a round thump with a bright tick on top: the big coloured buttons
	"thump": [
		{"at": 0.0, "len": 0.12, "from": 320.0, "to": 170.0, "wave": "sine", "gain": 1.0, "decay": 22.0},
		{"at": 0.0, "len": 0.025, "from": 2400.0, "to": 2400.0, "wave": "noise", "gain": 0.35, "decay": 90.0},
	],
	# two notes falling: going back
	"back": [
		{"at": 0.0, "len": 0.07, "from": 740.0, "to": 740.0, "wave": "tri", "gain": 0.7, "decay": 30.0},
		{"at": 0.05, "len": 0.1, "from": 520.0, "to": 480.0, "wave": "tri", "gain": 0.8, "decay": 24.0},
	],
	# a soft rise, like a page lifting: a window opens
	"open": [
		{"at": 0.0, "len": 0.16, "from": 360.0, "to": 760.0, "wave": "sine", "gain": 0.8, "decay": 14.0},
		{"at": 0.0, "len": 0.1, "from": 1800.0, "to": 1800.0, "wave": "noise", "gain": 0.12, "decay": 22.0},
	],
	# a bright little arpeggio: done
	"chime": [
		{"at": 0.0, "len": 0.12, "from": 523.0, "to": 523.0, "wave": "tri", "gain": 0.7, "decay": 18.0},
		{"at": 0.07, "len": 0.12, "from": 659.0, "to": 659.0, "wave": "tri", "gain": 0.7, "decay": 18.0},
		{"at": 0.14, "len": 0.26, "from": 784.0, "to": 784.0, "wave": "tri", "gain": 0.8, "decay": 11.0},
	],
	# two even pings: a heads-up
	"ping": [
		{"at": 0.0, "len": 0.1, "from": 740.0, "to": 740.0, "wave": "sine", "gain": 0.8, "decay": 22.0},
		{"at": 0.13, "len": 0.14, "from": 740.0, "to": 740.0, "wave": "sine", "gain": 0.8, "decay": 18.0},
	],
	# a low, short buzz: no
	"buzz": [
		{"at": 0.0, "len": 0.2, "from": 190.0, "to": 140.0, "wave": "square", "gain": 0.45, "decay": 12.0},
	],
	# air moving: the mode carousel
	"whoosh": [
		{"at": 0.0, "len": 0.16, "from": 1400.0, "to": 3200.0, "wave": "noise", "gain": 0.5, "decay": 14.0},
		{"at": 0.0, "len": 0.14, "from": 500.0, "to": 1100.0, "wave": "sine", "gain": 0.25, "decay": 16.0},
	],
}


## The sound of tone `name` (a key of `TONES`), or null for an unknown name.
static func make(name: String) -> AudioStreamWAV:
	if not TONES.has(name):
		return null
	var notes: Array = TONES[name]
	var total: float = 0.0
	for note: Dictionary in notes:
		total = maxf(total, (note["at"] as float) + (note["len"] as float))
	var count: int = ceili(total * RATE)
	var mix: PackedFloat32Array = PackedFloat32Array()
	mix.resize(count)
	var noise: RandomNumberGenerator = RandomNumberGenerator.new()
	noise.seed = hash(name)  # the same hiss every time
	for note: Dictionary in notes:
		_add(mix, note, noise)
	var bytes: PackedByteArray = PackedByteArray()
	bytes.resize(count * 2)
	for i: int in count:
		bytes.encode_s16(i * 2, int(clampf(mix[i], -1.0, 1.0) * 32000.0))
	var stream: AudioStreamWAV = AudioStreamWAV.new()
	stream.format = AudioStreamWAV.FORMAT_16_BITS
	stream.mix_rate = RATE
	stream.stereo = false
	stream.data = bytes
	return stream


## Adds one note into `mix`.
static func _add(mix: PackedFloat32Array, note: Dictionary, noise: RandomNumberGenerator) -> void:
	var first: int = int((note["at"] as float) * RATE)
	var length: float = note["len"] as float
	var n: int = int(length * RATE)
	var from: float = note["from"] as float
	var to: float = note["to"] as float
	var wave: String = note["wave"] as String
	var gain: float = note["gain"] as float
	var decay: float = note["decay"] as float
	var phase: float = 0.0
	var lowpassed: float = 0.0
	for i: int in n:
		if first + i >= mix.size():
			break
		var t: float = float(i) / RATE
		var glide: float = float(i) / maxf(1.0, float(n - 1))
		var pitch: float = lerpf(from, to, glide)
		phase = fmod(phase + pitch / RATE, 1.0)
		var value: float
		match wave:
			"tri":
				value = 4.0 * absf(phase - 0.5) - 1.0
			"square":
				value = 0.6 if phase < 0.5 else -0.6
			"noise":
				# white noise through a one-pole low-pass whose cut-off follows the pitch: a brighter pitch hisses more
				var open: float = clampf(pitch / 6000.0, 0.02, 1.0)
				lowpassed += (noise.randf_range(-1.0, 1.0) - lowpassed) * open
				value = lowpassed
			_:
				value = sin(TAU * phase)
		var envelope: float = exp(-decay * t) * minf(1.0, t / ATTACK) * minf(1.0, (length - t) / RELEASE)
		mix[first + i] += value * gain * envelope * 0.8
