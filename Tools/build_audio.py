"""Every sound effect in One Valley, synthesised from scratch.

There was no audio tool in this project. The 39 clips in Resources/Audio were made
somewhere else and arrived as finished files, so there was no way to change one without
re-recording it. This script is that missing half: it builds the whole sound set from
numbers, so a sound that is wrong can be tuned and rebuilt rather than replaced.

WHY SYNTHESIS RATHER THAN RECORDINGS. Unity's AI asset generation answers NoSubscription
(see CLAUDE.md), and a downloaded pack is a licence to track and a download to repeat on
every machine. numpy is already installed and Python ships a WAV writer, so this needs
nothing that is not already here.

WHAT COMES OUT. Mono 16-bit WAV at 44.1 kHz, written straight into
Assets/Resources/Audio/. Mono is not an accident - it is a fix. Every clip already in
that folder is STEREO with forceToMono off in its .meta, and Unity will not properly
position a stereo clip in 3D. GameSound.PlayAt asks for spatialBlend 0.85 and does not
get it. Everything this script writes is mono, so it pans and falls off the way the
code has always assumed it did.

Clips are named the way GameSound expects: "GruntHurt_0", "GruntHurt_1" are two
recordings of one event, and it picks between them at random.

THE RANDOMNESS IS SEEDED PER CLIP. Rebuilding produces byte-identical files. This is
deliberate and it is the lesson the FBX exports taught - an asset that changes every
time it is rebuilt makes every diff a lie, and you stop reading them.

Run:  python Tools/build_audio.py
      python Tools/build_audio.py Grunt Warden      (only those, by name prefix)
"""

import os
import sys
import wave
import struct
import numpy as np

if "ONEVALLEY_ROOT" in globals():
    PROJECT_ROOT = ONEVALLEY_ROOT
else:
    PROJECT_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

AUDIO_FOLDER = os.path.join(PROJECT_ROOT, "Assets", "Resources", "Audio")

SAMPLE_RATE = 44100


# ----------------------------------------------------------------------------------
# The smallest useful signal toolkit
#
# Everything below is built out of these eight functions. They are written for being
# read rather than for being fast - the whole set of clips takes a couple of seconds.
# ----------------------------------------------------------------------------------

def sample_count(seconds):
    return int(seconds * SAMPLE_RATE)


def noise(seconds, rng):
    """White noise. The raw material of every impact, hiss and breath."""
    return rng.normal(0.0, 1.0, sample_count(seconds))


def tone(seconds, frequency_start, frequency_end, shape="sine", bend=1.0):
    """An oscillator that slides from one pitch to another.

    The slide is what makes a sound feel like a physical event rather than a beep.
    Almost nothing in nature holds a steady pitch through an impact - it drops. `bend`
    shapes how the slide is spent: above 1 it falls fast then levels out, which is what
    a struck object does.
    """
    total = sample_count(seconds)
    if total < 2:
        total = 2

    progress = np.linspace(0.0, 1.0, total) ** bend
    frequency = frequency_start + (frequency_end - frequency_start) * progress

    # Accumulating phase rather than multiplying time by frequency. Multiplying is the
    # classic mistake here: it makes the pitch correct but the waveform tear at every
    # change, because the phase jumps.
    phase = np.cumsum(2.0 * np.pi * frequency / SAMPLE_RATE)

    if shape == "sine":
        return np.sin(phase)
    if shape == "saw":
        return 2.0 * ((phase / (2.0 * np.pi)) % 1.0) - 1.0
    if shape == "square":
        return np.sign(np.sin(phase))
    if shape == "triangle":
        return 2.0 * np.abs(2.0 * ((phase / (2.0 * np.pi)) % 1.0) - 1.0) - 1.0
    raise ValueError("unknown wave shape: " + shape)


def envelope(seconds, attack_seconds, decay_bend=3.0, hold_seconds=0.0):
    """Loudness over time: rise, optional hold, then fall away.

    `decay_bend` above 1 makes the tail drop quickly and then linger, which is how a
    real decay sounds. A straight line sounds like a fade-out, which is a different and
    much more artificial thing.
    """
    total = sample_count(seconds)
    if total < 2:
        total = 2

    rising = sample_count(attack_seconds)
    if rising < 1:
        rising = 1
    if rising > total:
        rising = total

    holding = sample_count(hold_seconds)
    if rising + holding > total:
        holding = total - rising

    falling = total - rising - holding

    out = np.zeros(total)
    out[:rising] = np.linspace(0.0, 1.0, rising)
    out[rising:rising + holding] = 1.0
    if falling > 0:
        out[rising + holding:] = np.linspace(1.0, 0.0, falling) ** decay_bend
    return out


def convolve(signal, impulse_response):
    """Multiply in the frequency domain, which is the same as filtering in time.

    Doing it this way means every filter below is just "what does this thing sound like
    when you hit it once", written out as a short decaying wave. That is far easier to
    reason about than filter coefficients, and it is fast enough to be free.
    """
    needed = len(signal) + len(impulse_response) - 1
    size = 1
    while size < needed:
        size = size * 2

    spectrum = np.fft.rfft(signal, size) * np.fft.rfft(impulse_response, size)
    return np.fft.irfft(spectrum, size)[:len(signal)]


def resonate(signal, frequency, bandwidth):
    """Ring the signal at one frequency, the way struck metal or stone rings.

    The impulse response of a resonator IS a decaying sine, so that is written here
    literally. A narrow bandwidth rings for a long time and reads as metal; a wide one
    dies immediately and reads as a dull thud.
    """
    decay_per_sample = np.pi * bandwidth / SAMPLE_RATE

    length = int(6.0 / decay_per_sample)
    if length < 64:
        length = 64
    if length > SAMPLE_RATE // 2:
        length = SAMPLE_RATE // 2

    index = np.arange(length)
    ring = np.exp(-decay_per_sample * index) * np.sin(2.0 * np.pi * frequency * index / SAMPLE_RATE)

    # Keeping the gain sane so stacking six resonators does not explode.
    ring = ring / (np.sum(np.abs(ring)) + 1e-9)
    return convolve(signal, ring)


def lowpass(signal, cutoff_hz):
    """Take the brightness off. A one-pole, written as its decaying impulse response."""
    fade = 2.0 * np.pi * cutoff_hz / SAMPLE_RATE
    keep = np.exp(-fade)

    length = int(8.0 / fade)
    if length < 8:
        length = 8
    if length > SAMPLE_RATE // 4:
        length = SAMPLE_RATE // 4

    index = np.arange(length)
    response = (1.0 - keep) * (keep ** index)
    return convolve(signal, response)


def highpass(signal, cutoff_hz):
    """Everything the lowpass threw away. Removes mud and body."""
    return signal - lowpass(signal, cutoff_hz)


def bandpass(signal, centre_hz, width_hz):
    return lowpass(highpass(signal, centre_hz - width_hz * 0.5), centre_hz + width_hz * 0.5)


def saturate(signal, amount):
    """Soft clipping. Adds harmonics and, more usefully, glues a layered sound together
    so it reads as one event rather than three sounds that happened at once."""
    if amount <= 0.0:
        return signal
    return np.tanh(signal * amount) / np.tanh(amount)


# ----------------------------------------------------------------------------------
# Putting sounds together
# ----------------------------------------------------------------------------------

def layer(*parts):
    """Sum several signals, padding the short ones. This is the single most important
    function in the file. A hit that is one recording sounds thin no matter how loud it
    is; a hit that is a low thump AND a mid body AND a bright edge sounds like impact.
    That is what the game was missing."""
    longest = 0
    for part in parts:
        if len(part) > longest:
            longest = len(part)

    out = np.zeros(longest)
    for part in parts:
        out[:len(part)] = out[:len(part)] + part
    return out


def delay_by(signal, seconds, total_seconds=None):
    """Start a sound late. Real impacts are not simultaneous - the crack arrives before
    the rumble, and staggering layers by a few milliseconds is most of what makes a
    sound feel big rather than loud."""
    offset = sample_count(seconds)
    total = sample_count(total_seconds) if total_seconds is not None else offset + len(signal)
    if total < offset + len(signal):
        total = offset + len(signal)

    out = np.zeros(total)
    out[offset:offset + len(signal)] = signal
    return out


def add_tail(signal, seconds, amount, brightness=2200.0):
    """A crude room. Convolving with decaying noise is the whole trick - it smears the
    sound over time the way a space does. The Vault needs this; a Grunt in the open
    valley needs almost none of it."""
    if amount <= 0.0:
        return signal

    length = sample_count(seconds)
    index = np.arange(length)
    room = np.random.RandomState(7).normal(0.0, 1.0, length) * np.exp(-4.0 * index / length)
    room = lowpass(room, brightness)
    room = room / (np.sum(np.abs(room)) + 1e-9)

    # The clip has to GROW by the length of the tail, or the reverb is cut off at the
    # end of the dry sound and the room simply vanishes mid-decay.
    padded = np.concatenate([signal, np.zeros(length)])
    return padded + convolve(padded, room) * amount * 8.0


def normalise(signal, peak=0.89):
    """Every clip leaves at the same peak, so the volume numbers already written all
    through the game keep meaning what they meant."""
    loudest = np.max(np.abs(signal))
    if loudest < 1e-9:
        return signal
    return signal * (peak / loudest)


def soften_edges(signal, fade_in_ms=0.4, fade_out_ms=4.0):
    """A waveform that starts or stops at a non-zero value clicks. A short ramp at each
    end removes that.

    The two ends need very different ramps and this cost real time to find. An impact's
    loudest sample is a millisecond or two in - it IS the crack of contact - so a fade-in
    long enough to be comfortable at the tail flattens the very thing that makes a hit
    sound like a hit. In goes fast enough only to leave the click behind; out can be
    leisurely because nothing important is happening there."""
    rise = sample_count(fade_in_ms / 1000.0)
    fall = sample_count(fade_out_ms / 1000.0)

    if rise < 2:
        rise = 2
    if fall < 2:
        fall = 2
    if rise + fall >= len(signal):
        return signal

    out = signal.copy()
    out[:rise] = out[:rise] * np.linspace(0.0, 1.0, rise)
    out[-fall:] = out[-fall:] * np.linspace(1.0, 0.0, fall)
    return out


def write_clip(name, signal):
    """One clip to disk, mono 16-bit."""
    # Softened BEFORE normalising, not after. The other way round, the fade quietly
    # scales the loudest sample down again and the clip lands well under the peak every
    # other clip was levelled to - which is how ArrowHitStone ended up half the volume
    # of everything around it.
    signal = normalise(soften_edges(signal))

    # Anything past full scale wraps around into a horrible crackle rather than simply
    # being loud, so it is clamped rather than trusted.
    signal = np.clip(signal, -1.0, 1.0)
    frames = (signal * 32767.0).astype(np.int16)

    path = os.path.join(AUDIO_FOLDER, name + ".wav")
    handle = wave.open(path, "wb")
    handle.setnchannels(1)
    handle.setsampwidth(2)
    handle.setframerate(SAMPLE_RATE)
    handle.writeframes(frames.tobytes())
    handle.close()

    return path


# ----------------------------------------------------------------------------------
# Creature voices
#
# A voice is a buzzing source pushed through a few fixed resonances. The source decides
# the pitch - how big the throat is - and the resonances decide the vowel. Change only
# the resonances and the same creature says something different; change only the pitch
# and it becomes a bigger or smaller animal saying the same thing.
#
# This is why all four creatures are one function. The Grunt and the Darter differ by
# numbers, exactly like everything else in EnemyBrain.
# ----------------------------------------------------------------------------------

def creature_voice(seconds, pitch_start, pitch_end, formants, rng,
                   breath=0.3, roughness=0.0, attack=0.02, decay_bend=2.5, bend=1.0):
    """formants is a list of (frequency, bandwidth, loudness)."""
    total = sample_count(seconds)

    # The buzz of a throat. A saw has all the harmonics the resonances need something
    # to grab; a sine would come out as a whistle whatever is done to it afterwards.
    source = tone(seconds, pitch_start, pitch_end, "saw", bend=bend)

    # No animal holds a steady pitch. Without this wobble the result is unmistakably a
    # synthesiser pretending, and the ear catches it immediately.
    wobble = lowpass(rng.normal(0.0, 1.0, total), 11.0)
    wobble = wobble / (np.max(np.abs(wobble)) + 1e-9)
    source = source * (1.0 + wobble * 0.10)

    if roughness > 0.0:
        # A growl is the vocal folds slapping irregularly. Ring-modulating with slow
        # noise reproduces that far better than distortion does, because it breaks the
        # sound up in time rather than just making it harsher.
        rasp = lowpass(rng.normal(0.0, 1.0, total), 70.0)
        rasp = rasp / (np.max(np.abs(rasp)) + 1e-9)
        source = source * (1.0 - roughness * 0.55 * (0.5 + 0.5 * rasp))

        # And a half-pitch layer underneath, which is what actually makes a big animal
        # sound big rather than merely low.
        source = source + tone(seconds, pitch_start * 0.5, pitch_end * 0.5, "saw") * roughness * 0.4

    air = noise(seconds, rng) * breath

    voiced = np.zeros(total)
    for frequency, bandwidth, loudness in formants:
        voiced = voiced + resonate(source, frequency, bandwidth) * loudness
        voiced = voiced + resonate(air, frequency, bandwidth * 2.2) * loudness * 0.55

    voiced = voiced * envelope(seconds, attack, decay_bend=decay_bend)
    return saturate(voiced, 1.6)


# The vowels each creature is built on. These are the only numbers that separate one
# animal's voice from another's, and they are worth reading as a group.
GRUNT_FORMANTS = [(430.0, 90.0, 1.0), (1050.0, 130.0, 0.55), (2500.0, 220.0, 0.18)]
DARTER_FORMANTS = [(900.0, 170.0, 1.0), (2300.0, 300.0, 0.75), (4100.0, 500.0, 0.40)]
SPITTER_FORMANTS = [(520.0, 200.0, 1.0), (1500.0, 420.0, 0.60), (3000.0, 700.0, 0.25)]
WARDEN_FORMANTS = [(150.0, 60.0, 1.0), (380.0, 110.0, 0.50), (900.0, 260.0, 0.15)]


# ----------------------------------------------------------------------------------
# Impacts
#
# Three layers, always, staggered by a few milliseconds:
#   the CRACK   - very short, very bright, tells you the exact instant of contact
#   the BODY    - what was struck, and what it is made of
#   the WEIGHT  - a low sine dropping in pitch, which is felt more than heard
#
# The old HitEnemy clips are one layer. That is the whole reason they read as "a slight
# sound" - there is nothing under them.
# ----------------------------------------------------------------------------------

def impact_flesh(rng, weight=1.0, wetness=0.5):
    crack = highpass(noise(0.030, rng), 2600.0) * envelope(0.030, 0.0004, decay_bend=5.0)

    body = bandpass(noise(0.16 * weight, rng), 420.0, 700.0)
    body = body * envelope(0.16 * weight, 0.002, decay_bend=3.2)

    slap = bandpass(noise(0.07, rng), 1600.0, 2200.0) * envelope(0.07, 0.001, decay_bend=4.0) * wetness

    thump = tone(0.20 * weight, 150.0 * weight, 48.0, "sine", bend=0.45)
    thump = thump * envelope(0.20 * weight, 0.001, decay_bend=2.6) * (0.9 * weight)

    return saturate(layer(crack * 0.5, delay_by(body, 0.002), delay_by(slap, 0.004), thump), 1.5)


def impact_stone(rng, weight=1.0):
    crack = highpass(noise(0.020, rng), 3800.0) * envelope(0.020, 0.0002, decay_bend=6.0)

    # Three inharmonic resonances. Harmonically related ones would ring like a bell;
    # stone is a lump and rings at frequencies with no relationship to each other.
    struck = noise(0.012, rng) * envelope(0.012, 0.0002, decay_bend=4.0)
    ring = layer(
        resonate(struck, 196.0 / weight, 34.0) * 1.0,
        resonate(struck, 331.0 / weight, 52.0) * 0.62,
        resonate(struck, 547.0 / weight, 95.0) * 0.34,
    )
    ring = ring * envelope(len(ring) / SAMPLE_RATE, 0.001, decay_bend=2.2)

    grit = bandpass(noise(0.13, rng), 2400.0, 3000.0) * envelope(0.13, 0.001, decay_bend=4.5) * 0.35

    weightlayer = tone(0.30 * weight, 110.0, 34.0, "sine", bend=0.4)
    weightlayer = weightlayer * envelope(0.30 * weight, 0.001, decay_bend=2.2) * weight

    return saturate(layer(crack * 0.6, ring * 1.2, delay_by(grit, 0.003), weightlayer), 1.4)


def rubble(rng, seconds=0.9, density=90):
    """Loose stone settling after something heavy landed. Scattered little impacts
    thinning out over time - which is what actually sells a slam as having moved the
    world rather than just being loud."""
    out = np.zeros(sample_count(seconds))
    for _ in range(density):
        # Squared, so most of the debris lands early and it trails off naturally.
        when = (rng.random() ** 2) * seconds * 0.85
        size = 0.25 + rng.random() * 0.75

        piece = resonate(noise(0.010, rng) * envelope(0.010, 0.0003, decay_bend=4.0),
                         500.0 + rng.random() * 2200.0, 220.0)
        piece = piece * envelope(len(piece) / SAMPLE_RATE, 0.001, decay_bend=3.0) * size

        placed = delay_by(piece, when, seconds)
        out[:len(placed)] = out[:len(placed)] + placed[:len(out)]
    return out


def whoosh(rng, seconds=0.30, low=300.0, high=1500.0, sharpness=2.5):
    """Something heavy moved through air. A noise band that sweeps up and then away -
    the sweep is the whole effect, because a static band just sounds like hiss."""
    total = sample_count(seconds)
    air = noise(seconds, rng)

    # Sweeping a filter properly means re-filtering per sample. Crossfading between a
    # dull copy and a bright one gets the same impression for a fraction of the work.
    dull = lowpass(air, low)
    bright = bandpass(air, high, high * 1.1)

    sweep = np.sin(np.linspace(0.0, np.pi, total)) ** sharpness
    blended = dull * (1.0 - sweep) + bright * sweep

    return blended * envelope(seconds, seconds * 0.35, decay_bend=2.0)


# ----------------------------------------------------------------------------------
# The clips themselves
#
# Each entry says how many alternatives to make. GameSound buckets "GruntHurt_0" and
# "GruntHurt_1" together and picks between them, so a sound the player hears fifty times
# a fight never repeats exactly.
# ----------------------------------------------------------------------------------

import hashlib

RECIPES = {}


def recipe(name, variants=1):
    def register(builder):
        RECIPES[name] = (builder, variants)
        return builder
    return register


def rng_for(name, variant):
    """A generator decided by the clip's own name, so rebuilding is repeatable.

    Python's built-in hash() is salted per process and would give a different clip every
    run - which is exactly the trap the FBX exports fell into.
    """
    digest = hashlib.md5((name + "#" + str(variant)).encode("utf-8")).digest()
    return np.random.default_rng(int.from_bytes(digest[:8], "little"))


# ----------------------------------------------------------------------------------
# The Grunt - heavy, slow, swings a club. Voice around 100 Hz.
# ----------------------------------------------------------------------------------

@recipe("GruntWindUp", 2)
def grunt_wind_up(rng, variant):
    """The effort noise as the club goes up. This is the sound the game most needed:
    the Grunt's telegraph was visual only, so an attack from off-screen had no tell."""
    pitch = 94.0 + variant * 8.0
    voice = creature_voice(0.44, pitch * 0.82, pitch * 1.30, GRUNT_FORMANTS, rng,
                           breath=0.45, roughness=0.45, attack=0.18, decay_bend=1.3)
    drawn_breath = bandpass(noise(0.44, rng), 1100.0, 1500.0)
    drawn_breath = drawn_breath * envelope(0.44, 0.26, decay_bend=1.6) * 0.30
    return layer(voice, drawn_breath)


@recipe("GruntSwing", 2)
def grunt_swing(rng, variant):
    heavy_air = whoosh(rng, 0.32, low=170.0, high=980.0, sharpness=3.2)
    return layer(heavy_air, whoosh(rng, 0.32, low=90.0, high=300.0, sharpness=2.0) * 0.5)


@recipe("GruntHurt", 3)
def grunt_hurt(rng, variant):
    pitch = 100.0 + variant * 11.0
    return creature_voice(0.28, pitch * 1.45, pitch * 0.78, GRUNT_FORMANTS, rng,
                          breath=0.35, roughness=0.55, attack=0.006,
                          decay_bend=3.0, bend=0.45)


@recipe("GruntDeath", 2)
def grunt_death(rng, variant):
    """A groan that runs out, and then the body arriving. Not a pop.

    The fall is deliberately late - the creature makes its noise, and only afterwards
    does it hit the ground. Putting them on the same instant is what made the old clip
    read as an effect rather than as something dying."""
    pitch = 98.0 + variant * 7.0
    groan = creature_voice(0.80, pitch * 1.15, pitch * 0.52, GRUNT_FORMANTS, rng,
                           breath=0.55, roughness=0.65, attack=0.02,
                           decay_bend=1.7, bend=0.7)
    body_lands = delay_by(impact_flesh(rng, weight=1.6, wetness=0.35) * 0.85, 0.52)
    return layer(groan, body_lands)


# ----------------------------------------------------------------------------------
# The Darter - small, fast, almost no telegraph. Voice up around 800 Hz.
# ----------------------------------------------------------------------------------

@recipe("DarterWindUp", 2)
def darter_wind_up(rng, variant):
    """A coiling hiss. The Darter's wind-up is only 0.36 s, so this has to say
    that something is about to happen in about a fifth of a second."""
    hiss = bandpass(noise(0.30, rng), 3400.0, 3800.0) * envelope(0.30, 0.20, decay_bend=1.8)
    rattle = bandpass(noise(0.30, rng), 1200.0, 900.0) * envelope(0.30, 0.22, decay_bend=2.0)
    return layer(hiss, rattle * 0.45)


@recipe("DarterLunge", 2)
def darter_lunge(rng, variant):
    pitch = 620.0 + variant * 90.0
    shriek = creature_voice(0.34, pitch * 0.75, pitch * 1.65, DARTER_FORMANTS, rng,
                            breath=0.30, roughness=0.30, attack=0.012,
                            decay_bend=2.4, bend=0.55)
    return layer(saturate(shriek, 2.6), whoosh(rng, 0.34, 600.0, 2600.0, 3.0) * 0.30)


@recipe("DarterHurt", 3)
def darter_hurt(rng, variant):
    pitch = 780.0 + variant * 110.0
    yelp = creature_voice(0.17, pitch * 1.5, pitch * 0.65, DARTER_FORMANTS, rng,
                          breath=0.25, roughness=0.35, attack=0.004,
                          decay_bend=3.4, bend=0.4)
    return saturate(yelp, 2.2)


@recipe("DarterDeath", 2)
def darter_death(rng, variant):
    """A shriek that falls off a cliff rather than finishing. The pitch collapse does
    the work - a sound that stops is an edit, a sound that falls is a death."""
    pitch = 720.0 + variant * 80.0
    shriek = creature_voice(0.46, pitch * 1.35, pitch * 0.25, DARTER_FORMANTS, rng,
                            breath=0.40, roughness=0.50, attack=0.008,
                            decay_bend=2.0, bend=1.9)
    return layer(saturate(shriek, 2.4), delay_by(impact_flesh(rng, 0.7, 0.6) * 0.5, 0.30))


# ----------------------------------------------------------------------------------
# The Spitter - hunched, wet, throws from range. Voice around 400 Hz.
# ----------------------------------------------------------------------------------

def make_it_wet(signal, rng, rate=38.0, depth=0.75):
    """Chop the sound up with fast noise. Interrupted airflow is what wetness actually
    is - a gurgle is a steady sound with liquid getting in the way of it."""
    stutter = lowpass(rng.normal(0.0, 1.0, len(signal)), rate)
    stutter = stutter / (np.max(np.abs(stutter)) + 1e-9)
    return signal * (1.0 - depth * 0.5 * (1.0 + stutter))


@recipe("SpitterWindUp", 2)
def spitter_wind_up(rng, variant):
    """Hawking something up. The Spitter has the longest telegraph in the game at 0.8 s
    and it is the only enemy that can hit you without reaching you, so this one has to
    carry a long way and be unmistakable."""
    pitch = 300.0 + variant * 40.0
    gather = creature_voice(0.62, pitch * 0.85, pitch * 1.35, SPITTER_FORMANTS, rng,
                            breath=0.85, roughness=0.75, attack=0.20, decay_bend=1.4)
    gather = make_it_wet(gather, rng, rate=30.0, depth=0.85)
    scrape = bandpass(noise(0.62, rng), 2000.0, 2600.0) * envelope(0.62, 0.30, decay_bend=1.6)
    return layer(gather, scrape * 0.40)


@recipe("SpitterThrow", 2)
def spitter_throw(rng, variant):
    """The release. A wet burst, then the rock leaving."""
    burst = bandpass(noise(0.11, rng), 1700.0, 2400.0) * envelope(0.11, 0.002, decay_bend=4.0)
    burst = make_it_wet(burst, rng, rate=140.0, depth=0.55)

    voice = creature_voice(0.16, 460.0, 260.0, SPITTER_FORMANTS, rng,
                           breath=0.7, roughness=0.6, attack=0.004, decay_bend=3.2, bend=0.4)

    leaving = delay_by(whoosh(rng, 0.26, 400.0, 1800.0, 2.6) * 0.45, 0.05)
    return layer(burst, voice * 0.8, leaving)


@recipe("SpitterHurt", 3)
def spitter_hurt(rng, variant):
    pitch = 380.0 + variant * 55.0
    gurgle = creature_voice(0.24, pitch * 1.4, pitch * 0.7, SPITTER_FORMANTS, rng,
                            breath=0.6, roughness=0.7, attack=0.005,
                            decay_bend=3.0, bend=0.45)
    return make_it_wet(gurgle, rng, rate=55.0, depth=0.7)


@recipe("SpitterDeath", 2)
def spitter_death(rng, variant):
    pitch = 360.0 + variant * 45.0
    gurgle = creature_voice(0.55, pitch * 1.2, pitch * 0.35, SPITTER_FORMANTS, rng,
                            breath=0.9, roughness=0.8, attack=0.01,
                            decay_bend=1.9, bend=1.3)
    gurgle = make_it_wet(gurgle, rng, rate=26.0, depth=0.9)
    splat = delay_by(impact_flesh(rng, weight=0.9, wetness=1.4) * 0.9, 0.26)
    return layer(gurgle, splat)


# ----------------------------------------------------------------------------------
# The Warden - 3.65 m of stone. Barely a voice at all; mostly it is architecture.
#
# The rule for all of these: the Warden is not a big Grunt. It does not grunt, yelp or
# groan, because it is not made of meat. Every sound it makes is rock under load. The
# one exception is the phase roar, and even that is stone resonating rather than a
# throat.
# ----------------------------------------------------------------------------------

@recipe("WardenWindUp", 2)
def warden_wind_up(rng, variant):
    """Loading up a slam. Stone grinding against stone while something underneath it
    winds tighter, and gravel shaking loose as it does.

    The rising sub is the important part - it is the only layer that says a thing is
    COMING rather than merely happening, which is exactly what a telegraph is for."""
    seconds = 1.05

    grind = bandpass(noise(seconds, rng), 700.0, 900.0)
    grind = grind * envelope(seconds, 0.35, decay_bend=0.9)
    grind = make_it_wet(grind, rng, rate=22.0, depth=0.45)

    winding = tone(seconds, 38.0, 96.0, "saw", bend=1.7)
    winding = lowpass(winding, 220.0) * envelope(seconds, 0.45, decay_bend=0.8) * 1.1

    shaking_loose = rubble(rng, seconds * 0.9, density=40) * 0.30

    stress = resonate(noise(seconds, rng) * 0.04, 128.0, 26.0) * envelope(seconds, 0.5, decay_bend=1.0)

    return saturate(layer(grind * 0.55, winding, shaking_loose, stress * 0.8), 1.3)


@recipe("WardenImpact", 2)
def warden_impact(rng, variant):
    """The slam landing. Four layers, staggered, because a single one at any volume is
    just a loud noise rather than a heavy one."""
    crack = impact_stone(rng, weight=1.0) * 1.0

    enormous = tone(0.85, 66.0, 22.0, "sine", bend=0.35)
    enormous = enormous * envelope(0.85, 0.002, decay_bend=2.0) * 1.5

    debris = delay_by(rubble(rng, 1.1, density=140) * 0.55, 0.045)

    boom = delay_by(lowpass(noise(0.55, rng), 190.0) * envelope(0.55, 0.004, decay_bend=2.6) * 0.8, 0.010)

    together = layer(crack, enormous, boom, debris)
    return add_tail(saturate(together, 1.35), 0.5, 0.16)


@recipe("WardenHurt", 3)
def warden_hurt(rng, variant):
    """Cracking, not crying out. Stone under a blow."""
    return impact_stone(rng, weight=1.15 + variant * 0.12) * 0.85


@recipe("WardenDeath")
def warden_death(rng, variant):
    """Two and a half seconds of a building coming down. The old EnemyDeath pop for a
    creature this size was the single worst mismatch in the game's audio."""
    seconds = 2.6

    failing = bandpass(noise(1.2, rng), 500.0, 700.0) * envelope(1.2, 0.25, decay_bend=1.2)

    groaning_stone = resonate(noise(1.4, rng) * 0.05, 92.0, 16.0) * envelope(1.4, 0.2, decay_bend=1.3)

    # It comes apart in pieces rather than all at once.
    first_break = delay_by(impact_stone(rng, 1.3) * 0.8, 0.55, seconds)
    second_break = delay_by(impact_stone(rng, 1.6) * 0.95, 1.05, seconds)
    final_fall = delay_by(impact_stone(rng, 2.2) * 1.2, 1.55, seconds)

    settling = delay_by(rubble(rng, 1.0, density=190) * 0.6, 1.62, seconds)

    collapse = tone(1.2, 55.0, 18.0, "sine", bend=0.4) * envelope(1.2, 0.01, decay_bend=1.8)

    together = layer(failing * 0.4, groaning_stone * 0.9, first_break, second_break,
                     final_fall, settling, delay_by(collapse, 1.5, seconds))
    return add_tail(saturate(together, 1.3), 0.7, 0.20)


@recipe("WardenPhase")
def warden_phase(rng, variant):
    """The rules just changed. A roar, but a roar made of stone resonating."""
    roar = creature_voice(1.0, 58.0, 44.0, WARDEN_FORMANTS, rng,
                          breath=0.35, roughness=0.85, attack=0.10, decay_bend=1.5)
    rising = tone(1.0, 90.0, 210.0, "saw", bend=1.4)
    rising = lowpass(rising, 700.0) * envelope(1.0, 0.30, decay_bend=1.3) * 0.5
    grit = rubble(rng, 1.0, density=70) * 0.35
    return add_tail(saturate(layer(roar * 1.2, rising, grit), 1.5), 0.45, 0.14)


@recipe("WardenEnrage")
def warden_enrage(rng, variant):
    """The last stand. Lower and longer than a phase change, so it is not mistaken for
    one - this is the moment the fight stops being survivable by running."""
    roar = creature_voice(1.5, 46.0, 34.0, WARDEN_FORMANTS, rng,
                          breath=0.30, roughness=1.0, attack=0.14, decay_bend=1.3)
    under = tone(1.5, 30.0, 24.0, "sine") * envelope(1.5, 0.2, decay_bend=1.2) * 1.3
    grit = rubble(rng, 1.4, density=110) * 0.4
    return add_tail(saturate(layer(roar * 1.3, under, grit), 1.6), 0.6, 0.18)


@recipe("WardenLeapLaunch")
def warden_leap_launch(rng, variant):
    push_off = impact_stone(rng, weight=1.2) * 0.7
    leaving = delay_by(whoosh(rng, 0.45, 120.0, 700.0, 2.2) * 0.8, 0.03)
    return layer(push_off, leaving)


@recipe("WardenLeapLand")
def warden_leap_land(rng, variant):
    """The heaviest sound in the game. Four tonnes arriving from above."""
    crack = impact_stone(rng, weight=1.4) * 1.1
    quake = tone(1.1, 52.0, 16.0, "sine", bend=0.3) * envelope(1.1, 0.002, decay_bend=1.8) * 1.7
    debris = delay_by(rubble(rng, 1.3, density=200) * 0.6, 0.05)
    return add_tail(saturate(layer(crack, quake, debris), 1.4), 0.6, 0.20)


@recipe("WardenShockwave")
def warden_shockwave(rng, variant):
    """A ring going out. The pitch falls as it expands, which is what makes it read as
    travelling away rather than simply as a rumble."""
    front = lowpass(noise(0.9, rng), 400.0) * envelope(0.9, 0.01, decay_bend=1.6)
    sweeping = tone(0.9, 180.0, 40.0, "sine", bend=0.7) * envelope(0.9, 0.005, decay_bend=1.5) * 1.2
    grit = rubble(rng, 0.9, density=90) * 0.35
    return add_tail(saturate(layer(front * 0.8, sweeping, grit), 1.3), 0.5, 0.15)


@recipe("WardenSummon")
def warden_summon(rng, variant):
    """Calling something down. The only Warden sound that is not physical - this one is
    allowed to be eerie, because what it does is not a hit."""
    seconds = 1.3
    voices = np.zeros(sample_count(seconds))
    for detune in [1.0, 1.005, 0.995, 1.498, 2.002]:
        voices = voices + tone(seconds, 220.0 * detune, 330.0 * detune, "sine", bend=1.5)
    voices = voices * envelope(seconds, 0.5, decay_bend=1.4) * 0.35

    shimmer = bandpass(noise(seconds, rng), 5000.0, 3000.0) * envelope(seconds, 0.45, decay_bend=1.6)
    under = tone(seconds, 55.0, 82.0, "sine", bend=1.3) * envelope(seconds, 0.35, decay_bend=1.3) * 0.9

    return add_tail(layer(voices, shimmer * 0.28, under), 0.8, 0.25)


@recipe("WardenStep", 2)
def warden_step(rng, variant):
    """A footfall that weighs something. Wired to the Warden only - an ordinary Grunt
    using this would make the valley sound like it is full of bosses."""
    return impact_stone(rng, weight=1.25 + variant * 0.1) * 0.55


# ----------------------------------------------------------------------------------
# The player's own blows
#
# Split by WHAT WAS HIT rather than by what did the hitting. Hitting a Darter and
# hitting the Warden with the same sword should not sound the same - the material is
# the information the player wants, because it tells them whether they are doing
# anything.
# ----------------------------------------------------------------------------------

@recipe("HitFlesh", 3)
def hit_flesh(rng, variant):
    return impact_flesh(rng, weight=0.9 + variant * 0.14, wetness=0.75)


@recipe("HitStone", 3)
def hit_stone(rng, variant):
    return impact_stone(rng, weight=0.85 + variant * 0.13)


@recipe("KillingBlow", 2)
def killing_blow(rng, variant):
    """An accent laid over the ordinary hit on the blow that kills. Not a sound in its
    own right - it is the difference between a hit and a kill, and nothing more."""
    ring = resonate(noise(0.02, rng) * envelope(0.02, 0.0004, decay_bend=4.0), 880.0, 26.0)
    ring = ring * envelope(len(ring) / SAMPLE_RATE, 0.001, decay_bend=2.4) * 0.8
    drop = tone(0.45, 300.0, 70.0, "sine", bend=0.5) * envelope(0.45, 0.002, decay_bend=2.4)
    return layer(ring, drop * 0.6)


@recipe("SwordWhiff", 2)
def sword_whiff(rng, variant):
    return whoosh(rng, 0.26, low=400.0, high=2400.0, sharpness=3.4) * 0.75


@recipe("HammerWhiff", 2)
def hammer_whiff(rng, variant):
    return whoosh(rng, 0.38, low=140.0, high=900.0, sharpness=2.8) * 0.85


# ----------------------------------------------------------------------------------
# The bow - which had no sounds at all
# ----------------------------------------------------------------------------------

@recipe("BowNock")
def bow_nock(rng, variant):
    tap = highpass(noise(0.035, rng), 2200.0) * envelope(0.035, 0.0008, decay_bend=4.5)
    wood = resonate(tap, 640.0, 150.0) * 0.7
    return layer(tap * 0.5, wood)


@recipe("BowDraw")
def bow_draw(rng, variant):
    """The creak of a stave bending. Slow, quiet, and the reason a drawn bow feels
    like stored energy rather than like a menu state."""
    seconds = 0.55
    creak = bandpass(noise(seconds, rng), 900.0, 700.0) * envelope(seconds, 0.30, decay_bend=1.3)
    creak = make_it_wet(creak, rng, rate=17.0, depth=0.8)
    tightening = tone(seconds, 130.0, 205.0, "triangle", bend=1.3)
    tightening = tightening * envelope(seconds, 0.35, decay_bend=1.4) * 0.25
    return layer(creak * 0.8, tightening)


@recipe("BowRelease", 2)
def bow_release(rng, variant):
    thwack = highpass(noise(0.05, rng), 1400.0) * envelope(0.05, 0.0006, decay_bend=4.0)
    string = resonate(thwack, 175.0 + variant * 18.0, 55.0) * 1.2
    string = string * envelope(len(string) / SAMPLE_RATE, 0.001, decay_bend=2.6)
    departure = delay_by(whoosh(rng, 0.18, 900.0, 3200.0, 3.0) * 0.35, 0.012)
    return saturate(layer(thwack * 0.7, string, departure), 1.4)


@recipe("ArrowFlyBy", 2)
def arrow_fly_by(rng, variant):
    return bandpass(noise(0.22, rng), 2600.0, 2200.0) * envelope(0.22, 0.09, decay_bend=2.6) * 0.6


@recipe("ArrowHitFlesh", 2)
def arrow_hit_flesh(rng, variant):
    return impact_flesh(rng, weight=0.55, wetness=1.1) * 0.9


@recipe("ArrowHitStone", 2)
def arrow_hit_stone(rng, variant):
    """A miss that hits the world. Sharper and smaller than a sword on stone, and it
    should read as a miss immediately."""
    tick = highpass(noise(0.025, rng), 4200.0) * envelope(0.025, 0.0003, decay_bend=5.0)
    chip = resonate(tick, 1750.0, 340.0) * 0.8
    clatter = delay_by(rubble(rng, 0.35, density=18) * 0.3, 0.02)
    return layer(tick * 0.6, chip, clatter)


# ----------------------------------------------------------------------------------
# The player's body
# ----------------------------------------------------------------------------------

@recipe("PlayerLand", 2)
def player_land(rng, variant):
    thud = lowpass(noise(0.18, rng), 320.0) * envelope(0.18, 0.002, decay_bend=3.0)
    body = tone(0.22, 105.0, 42.0, "sine", bend=0.45) * envelope(0.22, 0.002, decay_bend=2.6)
    scuff = bandpass(noise(0.14, rng), 2600.0, 2400.0) * envelope(0.14, 0.004, decay_bend=3.6) * 0.4
    return saturate(layer(thud * 0.9, body * 0.8, scuff), 1.3)


@recipe("Heartbeat")
def heartbeat(rng, variant):
    """Two beats, for when health is nearly gone. Deliberately almost sub-audible - it
    should be felt before it is noticed, or it becomes annoying within one fight."""
    def one_beat(strength):
        beat = tone(0.19, 68.0, 34.0, "sine", bend=0.5)
        return beat * envelope(0.19, 0.006, decay_bend=2.6) * strength

    return layer(one_beat(1.0), delay_by(one_beat(0.62), 0.26, 0.85))


# ----------------------------------------------------------------------------------
# Status effects - none of which made any sound before
# ----------------------------------------------------------------------------------

@recipe("BleedTick", 2)
def bleed_tick(rng, variant):
    """Quiet. This fires once a second for the whole duration, so anything with a
    transient in it would become torture by the third tick."""
    drip = tone(0.13, 900.0, 380.0, "sine", bend=0.6) * envelope(0.13, 0.004, decay_bend=3.4)
    wet = bandpass(noise(0.10, rng), 1500.0, 1600.0) * envelope(0.10, 0.003, decay_bend=4.0)
    return layer(drip * 0.5, wet * 0.35)


@recipe("Stunned")
def stunned(rng, variant):
    """The ears ringing. Says clearly that control has been taken away, which is the
    one thing a player must never have to guess about."""
    seconds = 0.9
    ringing = resonate(noise(0.02, rng) * envelope(0.02, 0.001, decay_bend=3.0), 2400.0, 9.0)
    ringing = ringing[:sample_count(seconds)] if len(ringing) > sample_count(seconds) else ringing
    ringing = ringing * envelope(len(ringing) / SAMPLE_RATE, 0.004, decay_bend=1.8)
    dull = lowpass(noise(seconds, rng), 260.0) * envelope(seconds, 0.02, decay_bend=1.6) * 0.5
    return layer(ringing * 1.2, dull)


@recipe("Weakened")
def weakened(rng, variant):
    """Something draining away. A falling pitch, because every language of game feel
    agrees that down means worse."""
    sag = tone(0.7, 420.0, 150.0, "triangle", bend=0.8) * envelope(0.7, 0.03, decay_bend=2.0)
    breath = bandpass(noise(0.7, rng), 700.0, 900.0) * envelope(0.7, 0.08, decay_bend=2.2) * 0.4
    return layer(sag * 0.6, breath)


# ----------------------------------------------------------------------------------
# The world - things that were all sharing one PortalOpen clip
# ----------------------------------------------------------------------------------

@recipe("SurgeActivate")
def surge_activate(rng, variant):
    """The player's own power coming on. Rising, bright, and clearly THEIRS - it had
    been sharing a clip with the portal, so the game's best moment sounded like a door."""
    seconds = 1.1
    swell = np.zeros(sample_count(seconds))
    for detune in [1.0, 1.5, 2.0, 3.0]:
        swell = swell + tone(seconds, 160.0 * detune, 420.0 * detune, "sine", bend=1.6)
    swell = swell * envelope(seconds, 0.42, decay_bend=1.6) * 0.30

    charge = bandpass(noise(seconds, rng), 3600.0, 4000.0) * envelope(seconds, 0.40, decay_bend=1.8)
    body = tone(seconds, 70.0, 140.0, "sine", bend=1.4) * envelope(seconds, 0.30, decay_bend=1.5)

    return add_tail(layer(swell, charge * 0.30, body * 0.8), 0.5, 0.18)


@recipe("GemShatter")
def gem_shatter(rng, variant):
    """The Warden's gem going. Glass, not stone - it needs to be obviously a different
    material from everything else in that fight."""
    burst = highpass(noise(0.05, rng), 3000.0) * envelope(0.05, 0.0004, decay_bend=4.5)

    shards = np.zeros(sample_count(1.0))
    for _ in range(60):
        when = (rng.random() ** 1.6) * 0.7
        piece = resonate(noise(0.008, rng) * envelope(0.008, 0.0002, decay_bend=4.0),
                         2200.0 + rng.random() * 4500.0, 120.0)
        piece = piece * envelope(len(piece) / SAMPLE_RATE, 0.001, decay_bend=2.6)
        placed = delay_by(piece * (0.3 + rng.random() * 0.7), when, 1.0)
        shards[:len(placed)] = shards[:len(placed)] + placed[:len(shards)]

    drop = tone(0.6, 420.0, 90.0, "sine", bend=0.5) * envelope(0.6, 0.003, decay_bend=2.4)
    return add_tail(layer(burst * 0.8, shards * 0.7, drop * 0.5), 0.5, 0.20)


@recipe("StoryBeat")
def story_beat(rng, variant):
    """Something in the world just changed because of the story. Low, slow, and not a
    combat sound - it must never be mistaken for something the player has to react to."""
    seconds = 1.6
    chord = np.zeros(sample_count(seconds))
    for detune in [1.0, 1.335, 2.0]:
        chord = chord + tone(seconds, 110.0 * detune, 110.0 * detune, "sine")
    chord = chord * envelope(seconds, 0.55, decay_bend=1.5) * 0.32

    air = bandpass(noise(seconds, rng), 2400.0, 2600.0) * envelope(seconds, 0.6, decay_bend=1.7)
    return add_tail(layer(chord, air * 0.18), 0.9, 0.28)


# ----------------------------------------------------------------------------------
# Build everything
# ----------------------------------------------------------------------------------

def main():
    wanted = sys.argv[1:]

    if os.path.isdir(AUDIO_FOLDER) == False:
        os.makedirs(AUDIO_FOLDER)

    names = sorted(RECIPES.keys())
    if len(wanted) > 0:
        names = [n for n in names if any(n.lower().startswith(w.lower()) for w in wanted)]
        if len(names) == 0:
            print("Nothing matched " + ", ".join(wanted))
            print("Known sounds: " + ", ".join(sorted(RECIPES.keys())))
            return 1

    written = 0
    for name in names:
        builder, variants = RECIPES[name]
        for variant in range(variants):
            signal = builder(rng_for(name, variant), variant)
            path = write_clip(name + "_" + str(variant), signal)
            written = written + 1
            seconds = len(signal) / SAMPLE_RATE
            print("  %-22s %5.2f s  %s" % (os.path.basename(path), seconds, ""))

    print("")
    print("Wrote %d clips across %d sounds into Assets/Resources/Audio." % (written, len(names)))
    print("Unity will import them on next focus. They are mono, so PlayAt positions them.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
