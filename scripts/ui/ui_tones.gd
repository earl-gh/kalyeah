class_name UiTones
extends RefCounted
## The interface's own sounds (owner, 2026-11-03: the menus must not reuse the weapons' sound effects). Each is a few soft, playful notes made in
## code (additive "mallet", "bubble" and "bell" voices, band-passed noise for a tick or a whoosh), mixed into a 16-bit mono `AudioStreamWAV`, so
## there is no sound file to ship or import. `UiSound.ROLES` names the tone of each role; the level and the pitch spread stay there. View only.
##
## What makes them sound finished (and what `tests/unit/test_ui_sound.gd` holds):
## - They are written for a phone speaker: every note's pitch settles at about 500 Hz or higher (a phone's speaker barely plays less) and carries upper partials.
## - Every note is built from sine partials, dropped above 45 % of the rate, so nothing aliases (no naive square or triangle waves).
## - Notes are in one key (C major pentatonic), so quick taps never clash; a role's random pitch spread is a few percent, never off the key.
## - Each tone is levelled to the same loudness (`TARGET_RMS`) and peaks below `CEILING`; the role's `db` then says how loud it is meant to be.
## - A tick of noise or a bubbly pitch glide gives a tap its touch; the success and the select have a short echo.
## - Every tone starts and ends at zero, with no DC offset, and is made the same way every time.

const RATE: int = 32000
## A note rises over this long (s) at least, so it never starts on a click; and falls to zero over the end of its length.
const ATTACK: float = 0.003
const RELEASE: float = 0.012
## The loudness every tone is levelled to (RMS of its sounding part, as a share of full scale: about -18 dBFS), and the most any sample may reach.
const TARGET_RMS: float = 0.125
const CEILING: float = 0.9
## A partial above this share of the rate is dropped, so no note folds back as a whine.
const BRIGHTEST: float = 0.45
## A sample is part of the "sounding" part of a tone when it is above this share of the tone's peak.
const SOUNDING: float = 0.02
## The echo is given room for this many repeats after the last note.
const ECHO_TAIL: float = 2.0
## A note is cut when it has fallen below this share of its start (about -50 dB), so no inaudible tail is made.
const SILENT: float = 0.003

## Voices: a note's partials, each [ratio to the pitch, gain, decay scale (1 = as long as the note)]. All sine, so all clean.
const VOICES: Dictionary[String, Array] = {
	# a soft music-box / kalimba pluck
	"box": [[1.0, 1.0, 1.0], [2.0, 0.30, 0.5], [3.0, 0.12, 0.3]],
	# a glassy little bell (bar-like partials)
	"bell": [[1.0, 1.0, 1.0], [2.76, 0.42, 0.55], [5.4, 0.18, 0.35]],
	# a water drop: nearly pure, with a whisper of the octave
	"bubble": [[1.0, 1.0, 1.0], [2.0, 0.16, 0.5]],
	# a wooden tick
	"wood": [[1.0, 1.0, 1.0], [2.3, 0.50, 0.4]],
	# a rounded "bonk": odd partials, like a soft triangle wave but bandlimited
	"bonk": [[1.0, 1.0, 1.0], [2.0, 0.20, 0.5], [3.0, 0.22, 0.6]],
}

## Tone name -> {notes, echo}. A pitched note: {at: when it starts (s), len: how long (s), voice, hz: pitch, to: the pitch it settles on (a glide
## that closes by `glide` seconds, exponentially: a bubble), tau: its decay time constant (s), gain}. A noise note: {at, len, voice: "noise",
## hz: the centre of the hiss, to: where the centre ends, tau, attack: (s) how softly it rises, gain, q: how narrow it is}. `echo`: {time: s,
## feedback, mix}.
const TONES: Dictionary[String, Dictionary] = {
	# a soft bubble pop with a tick of touch: buttons, switches
	"pop": {"notes": [
		{"at": 0.0, "len": 0.11, "voice": "bubble", "hz": 640.0, "to": 1180.0, "glide": 0.018, "tau": 0.03, "gain": 1.0},
		{"at": 0.0, "len": 0.02, "voice": "noise", "hz": 3600.0, "to": 3600.0, "tau": 0.004, "gain": 0.22, "q": 1.4},
	]},
	# a dry wooden tick: a segment of a choice
	"click": {"notes": [
		{"at": 0.0, "len": 0.06, "voice": "wood", "hz": 1760.0, "tau": 0.014, "gain": 1.0},
		{"at": 0.0, "len": 0.012, "voice": "noise", "hz": 4200.0, "to": 4200.0, "tau": 0.003, "gain": 0.3, "q": 1.2},
	]},
	# two music-box notes climbing, G to C: picking a card, a kid, a tab
	"pick": {"notes": [
		{"at": 0.0, "len": 0.14, "voice": "box", "hz": 784.0, "tau": 0.05, "gain": 0.85},
		{"at": 0.055, "len": 0.3, "voice": "box", "hz": 1046.5, "tau": 0.07, "gain": 1.0},
	], "echo": {"time": 0.07, "feedback": 0.3, "mix": 0.22}},
	# a round, cheerful thump with a little boing on top: the big coloured buttons
	"thump": {"notes": [
		{"at": 0.0, "len": 0.16, "voice": "bubble", "hz": 820.0, "to": 520.0, "glide": 0.05, "tau": 0.06, "gain": 1.0},
		{"at": 0.018, "len": 0.14, "voice": "bubble", "hz": 960.0, "to": 1560.0, "glide": 0.03, "tau": 0.045, "gain": 0.5},
		{"at": 0.0, "len": 0.022, "voice": "noise", "hz": 2600.0, "to": 2600.0, "tau": 0.005, "gain": 0.3, "q": 1.0},
	]},
	# two music-box notes falling, C to G: going back
	"back": {"notes": [
		{"at": 0.0, "len": 0.13, "voice": "box", "hz": 1046.5, "tau": 0.045, "gain": 0.85},
		{"at": 0.055, "len": 0.22, "voice": "box", "hz": 784.0, "tau": 0.075, "gain": 0.9},
	]},
	# a bubble that swells up with a breath of air and a sparkle on top: a window opens
	"open": {"notes": [
		{"at": 0.0, "len": 0.2, "voice": "bubble", "hz": 520.0, "to": 1040.0, "glide": 0.07, "tau": 0.07, "gain": 0.9, "attack": 0.02},
		{"at": 0.0, "len": 0.18, "voice": "noise", "hz": 1400.0, "to": 3400.0, "tau": 0.05, "gain": 0.5, "q": 0.9, "attack": 0.045},
		{"at": 0.085, "len": 0.2, "voice": "box", "hz": 1318.5, "tau": 0.05, "gain": 0.5},
	]},
	# a bright little arpeggio, C E G C, that rings: done
	"chime": {"notes": [
		{"at": 0.0, "len": 0.16, "voice": "bell", "hz": 1046.5, "tau": 0.06, "gain": 0.8},
		{"at": 0.06, "len": 0.16, "voice": "bell", "hz": 1318.5, "tau": 0.06, "gain": 0.8},
		{"at": 0.12, "len": 0.2, "voice": "bell", "hz": 1568.0, "tau": 0.07, "gain": 0.85},
		{"at": 0.18, "len": 0.36, "voice": "bell", "hz": 2093.0, "tau": 0.1, "gain": 1.0},
	], "echo": {"time": 0.09, "feedback": 0.32, "mix": 0.28}},
	# two friendly pings, the second a little softer: a heads-up
	"ping": {"notes": [
		{"at": 0.0, "len": 0.2, "voice": "box", "hz": 1318.5, "tau": 0.06, "gain": 1.0},
		{"at": 0.15, "len": 0.26, "voice": "box", "hz": 1318.5, "tau": 0.06, "gain": 0.7},
	], "echo": {"time": 0.08, "feedback": 0.25, "mix": 0.18}},
	# a goofy falling "uh-oh" bonk-bonk: no
	"buzz": {"notes": [
		{"at": 0.0, "len": 0.12, "voice": "bonk", "hz": 740.0, "to": 659.0, "glide": 0.05, "tau": 0.05, "gain": 0.9},
		{"at": 0.11, "len": 0.2, "voice": "bonk", "hz": 659.0, "to": 523.0, "glide": 0.07, "tau": 0.08, "gain": 1.0},
	]},
	# a breath of air sweeping past: the mode carousel
	"whoosh": {"notes": [
		{"at": 0.0, "len": 0.22, "voice": "noise", "hz": 700.0, "to": 3600.0, "tau": 0.07, "gain": 1.0, "q": 0.8, "attack": 0.05},
		{"at": 0.02, "len": 0.2, "voice": "bubble", "hz": 520.0, "to": 1500.0, "glide": 0.09, "tau": 0.06, "gain": 0.14, "attack": 0.04},
	]},
}


## The sound of tone `name` (a key of `TONES`), or null for an unknown name.
static func make(name: String) -> AudioStreamWAV:
	if not TONES.has(name):
		return null
	var spec: Dictionary = TONES[name]
	var notes: Array = spec["notes"]
	var total: float = 0.0
	for note: Dictionary in notes:
		total = maxf(total, (note["at"] as float) + (note["len"] as float))
	var echo: Dictionary = spec.get("echo", {}) as Dictionary
	if not echo.is_empty():
		total += (echo["time"] as float) * ECHO_TAIL
	var count: int = ceili(total * RATE)
	var mix: PackedFloat32Array = PackedFloat32Array()
	mix.resize(count)
	var noise: RandomNumberGenerator = RandomNumberGenerator.new()
	noise.seed = hash(name)  # the same hiss every time
	for note: Dictionary in notes:
		if note["voice"] == "noise":
			_add_hiss(mix, note, noise)
		else:
			_add_pitched(mix, note)
	if not echo.is_empty():
		_add_echo(mix, echo)
	_finish(mix)
	var bytes: PackedByteArray = PackedByteArray()
	bytes.resize(count * 2)
	for i: int in count:
		bytes.encode_s16(i * 2, int(clampf(mix[i], -1.0, 1.0) * 32767.0))
	var stream: AudioStreamWAV = AudioStreamWAV.new()
	stream.format = AudioStreamWAV.FORMAT_16_BITS
	stream.mix_rate = RATE
	stream.stereo = false
	stream.data = bytes
	return stream


## The envelope of a note at `t` seconds: a soft rise, an exponential fall (`tau` s) and a short release at the end of its `length`.
static func _envelope(t: float, length: float, tau: float, attack: float) -> float:
	var rise: float = minf(1.0, t / maxf(attack, ATTACK))
	rise = rise * rise * (3.0 - 2.0 * rise)  # smooth, no corner
	return exp(-t / tau) * rise * minf(1.0, (length - t) / RELEASE)


## Adds one pitched note (the partials of its voice, with an optional pitch glide) into `mix`. Everything that falls (the note, each partial,
## the glide) is multiplied by a fixed step per sample instead of calling `exp` for every sample: the same sound, several times cheaper.
static func _add_pitched(mix: PackedFloat32Array, note: Dictionary) -> void:
	var first: int = int((note["at"] as float) * RATE)
	var n: int = mini(int((note["len"] as float) * RATE), mix.size() - first)
	var length: float = note["len"] as float
	var hz: float = note["hz"] as float
	var to: float = note.get("to", hz) as float
	var glide_step: float = exp(-1.0 / ((note.get("glide", 0.01) as float) * RATE))
	var tau: float = note["tau"] as float
	var attack: float = maxf(note.get("attack", ATTACK) as float, ATTACK)
	var gain: float = (note["gain"] as float) * 0.5
	var step: float = exp(-1.0 / (tau * RATE))
	var partials: Array = VOICES[note["voice"] as String]
	var count: int = partials.size()
	var ratios: PackedFloat32Array = PackedFloat32Array()
	var amps: PackedFloat32Array = PackedFloat32Array()  # a partial's loudness now
	var steps: PackedFloat32Array = PackedFloat32Array()  # how much of it is left after one more sample (higher partials die sooner)
	var phases: PackedFloat32Array = PackedFloat32Array()
	for partial: Array in partials:
		ratios.append(partial[0] as float)
		amps.append(partial[1] as float)
		steps.append(exp(-(1.0 / (partial[2] as float) - 1.0) / (tau * RATE)))
		phases.append(0.0)
	var offset: float = hz - to  # the glide's remaining distance
	var level: float = 1.0  # the note's own fall
	var release_at: int = n - int(RELEASE * RATE)
	for i: int in n:
		var pitch: float = to + offset
		var value: float = 0.0
		for k: int in count:
			phases[k] += pitch * ratios[k] / RATE
			if pitch * ratios[k] < RATE * BRIGHTEST:  # past that a partial would fold back as a whine
				value += sin(TAU * phases[k]) * amps[k]
			amps[k] *= steps[k]
		var rise: float = minf(1.0, float(i) / (attack * RATE))
		rise = rise * rise * (3.0 - 2.0 * rise)  # smooth, no corner
		var fade: float = 1.0 if i < release_at else float(n - i) / (RELEASE * RATE)
		mix[first + i] += value * gain * level * rise * fade
		level *= step
		offset *= glide_step
		if level < SILENT:
			break


## Adds one band of hiss into `mix`: white noise through a band-pass whose centre glides from `hz` to `to`.
static func _add_hiss(mix: PackedFloat32Array, note: Dictionary, noise: RandomNumberGenerator) -> void:
	var first: int = int((note["at"] as float) * RATE)
	var length: float = note["len"] as float
	var n: int = int(length * RATE)
	var hz: float = note["hz"] as float
	var to: float = note["to"] as float
	var tau: float = note["tau"] as float
	var attack: float = note.get("attack", ATTACK) as float
	var gain: float = (note["gain"] as float) * 0.5
	var damping: float = 1.0 / (note["q"] as float)
	var low: float = 0.0
	var band: float = 0.0
	n = mini(n, mix.size() - first)
	for i: int in n:
		var t: float = float(i) / RATE
		var centre: float = lerpf(hz, to, float(i) / maxf(1.0, float(n - 1)))
		var f: float = 2.0 * sin(PI * minf(centre, RATE * BRIGHTEST) / RATE)  # a state-variable band-pass (Chamberlin)
		var high: float = noise.randf_range(-1.0, 1.0) - low - damping * band
		band += f * high
		low += f * band
		mix[first + i] += band * gain * _envelope(t, length, tau, attack)


## Feeds the mix through a feedback echo (the dry sound stays; the repeats are added under it).
static func _add_echo(mix: PackedFloat32Array, echo: Dictionary) -> void:
	var delay: int = int((echo["time"] as float) * RATE)
	var feedback: float = echo["feedback"] as float
	var wet: float = echo["mix"] as float
	var repeats: PackedFloat32Array = PackedFloat32Array()
	repeats.resize(mix.size())
	for i: int in range(delay, mix.size()):
		repeats[i] = mix[i - delay] + repeats[i - delay] * feedback
	for i: int in mix.size():
		mix[i] += repeats[i] * wet


## Takes out any DC, levels the tone to `TARGET_RMS` over its sounding part (so every tone is as loud as the others before its role's dB),
## keeps its peak under `CEILING` with a soft knee, and makes sure it ends at zero.
static func _finish(mix: PackedFloat32Array) -> void:
	var previous_in: float = 0.0
	var previous_out: float = 0.0
	for i: int in mix.size():  # a one-pole high-pass at about 30 Hz
		var x: float = mix[i]
		previous_out = x - previous_in + 0.9958 * previous_out
		previous_in = x
		mix[i] = previous_out
	var peak: float = 0.0
	for value: float in mix:
		peak = maxf(peak, absf(value))
	if peak <= 0.0:
		return
	var sum: float = 0.0
	var counted: int = 0
	for value: float in mix:
		if absf(value) > peak * SOUNDING:
			sum += value * value
			counted += 1
	var scale: float = TARGET_RMS / sqrt(sum / maxf(1.0, float(counted)))
	for i: int in mix.size():
		var value: float = mix[i] * scale
		# a soft knee: below 0.6 untouched, above it bent smoothly to CEILING
		var magnitude: float = absf(value)
		if magnitude > 0.6:
			magnitude = 0.6 + (CEILING - 0.6) * tanh((magnitude - 0.6) / (CEILING - 0.6))
		mix[i] = signf(value) * magnitude
	var fade: int = mini(mix.size(), int(RELEASE * RATE))
	for i: int in fade:
		mix[mix.size() - 1 - i] *= float(i) / float(fade)
