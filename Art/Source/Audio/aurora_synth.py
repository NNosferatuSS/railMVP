"""
Aurora — síntese procedural de todo o áudio do Rail (SFX + música adaptativa).

Tudo em Ré menor (SFX melódicos na pentatônica D F G A C) a 124 BPM, pra que
qualquer som caia "dentro" da música. Nada de samples externos.

Uso:
    python aurora_synth.py <pasta_saida>
Gera WAVs 16-bit 44.1 kHz. Música: 5 camadas stereo em loop perfeito (mesmo
comprimento, a cauda de reverb/delay é dobrada pro início).
"""
import os
import sys
import numpy as np
from scipy import signal
from scipy.io import wavfile

SR = 44100
RNG = np.random.default_rng(7)

# ============================================================== básicos

def secs(n):
    return int(round(n * SR))


def midi(n):
    return 440.0 * 2.0 ** ((n - 69) / 12.0)


def tvec(dur):
    return np.arange(secs(dur)) / SR


def phase_of(freq, n):
    f = np.broadcast_to(np.asarray(freq, dtype=np.float64), (n,))
    return np.cumsum(f) / SR


def sine(freq, dur, ph0=0.0):
    n = secs(dur)
    return np.sin(2 * np.pi * (phase_of(freq, n) + ph0))


def saw(freq, dur, ph0=0.0):
    """Saw com polyBLEP (sem aliasing feio)."""
    n = secs(dur)
    f = np.broadcast_to(np.asarray(freq, dtype=np.float64), (n,))
    ph = (np.cumsum(f) / SR + ph0) % 1.0
    dt = np.clip(f / SR, 1e-6, 0.5)
    y = 2.0 * ph - 1.0
    m = ph < dt
    x = ph[m] / dt[m]
    y[m] -= x + x - x * x - 1.0
    m = ph > 1.0 - dt
    x = (ph[m] - 1.0) / dt[m]
    y[m] -= x * x + x + x + 1.0
    return y


def noise(dur):
    return RNG.uniform(-1, 1, secs(dur))


def env_exp(dur, decay, attack=0.002):
    t = tvec(dur)
    a = np.clip(t / max(attack, 1e-5), 0, 1)
    return a * np.exp(-t * decay)


def env_adsr(dur, a, d, s, r):
    n = secs(dur)
    t = np.arange(n) / SR
    e = np.where(t < a, t / max(a, 1e-5), 1.0)
    e = np.where((t >= a) & (t < a + d), 1.0 - (1.0 - s) * (t - a) / max(d, 1e-5), e)
    e = np.where(t >= a + d, s, e)
    rel_start = max(dur - r, 0)
    e = np.where(t > rel_start, e * np.clip(1.0 - (t - rel_start) / max(r, 1e-5), 0, 1), e)
    return e


def lp(x, fc, order=2):
    fc = min(fc, SR * 0.45)
    sos = signal.butter(order, fc, "low", fs=SR, output="sos")
    return signal.sosfilt(sos, x, axis=-1)


def hp(x, fc, order=2):
    sos = signal.butter(order, fc, "high", fs=SR, output="sos")
    return signal.sosfilt(sos, x, axis=-1)


def bp(x, lo, hi, order=2):
    sos = signal.butter(order, [lo, min(hi, SR * 0.45)], "band", fs=SR, output="sos")
    return signal.sosfilt(sos, x, axis=-1)


def lp_sweep(x, cutoffs, block=64, q=0.9):
    """Lowpass com cutoff variando no tempo (biquad por blocos, estado contínuo)."""
    y = np.zeros_like(x)
    zi = np.zeros((1, 2))
    n = len(x)
    cut = np.broadcast_to(np.asarray(cutoffs, dtype=np.float64), (n,))
    for i in range(0, n, block):
        fc = float(np.clip(cut[i], 30, SR * 0.45))
        b, a = signal.iirfilter(2, fc, btype="low", ftype="butter", fs=SR)
        sos = np.concatenate([b, a])[None, :]
        y[i:i + block], zi = signal.sosfilt(sos, x[i:i + block], zi=zi)
    return y


def pad_to(x, n):
    if x.ndim == 1:
        return np.pad(x, (0, max(0, n - len(x))))[:n]
    return np.pad(x, ((0, 0), (0, max(0, n - x.shape[1]))))[:, :n]


def place(buf, x, start):
    """Soma x (mono ou stereo) em buf (stereo) a partir de start (amostras)."""
    if x.ndim == 1:
        x = np.vstack([x, x])
    end = min(buf.shape[1], start + x.shape[1])
    if end > start:
        buf[:, start:end] += x[:, : end - start]


def pan(x, p):
    """p em [-1, 1], equal power."""
    a = (p + 1) * np.pi / 4
    return np.vstack([x * np.cos(a), x * np.sin(a)])


def reverb_ir(dur=2.4, decay=2.2, bright=7000, predelay=0.012, stereo=True):
    n = secs(dur)
    t = np.arange(n) / SR
    chans = []
    for c in range(2 if stereo else 1):
        nz = RNG.standard_normal(n) * np.exp(-t * decay)
        nz = lp(nz, bright * (0.85 + 0.3 * c), 1)
        nz = np.concatenate([np.zeros(secs(predelay)), nz])
        chans.append(nz / np.sqrt(np.sum(nz ** 2)))
    return np.vstack(chans)


IR_HALL = None


def reverb(x, wet=0.3, ir=None):
    global IR_HALL
    if ir is None:
        if IR_HALL is None:
            IR_HALL = reverb_ir()
        ir = IR_HALL
    if x.ndim == 1:
        x = np.vstack([x, x])
    out = np.zeros((2, x.shape[1] + ir.shape[1] - 1))
    for c in range(2):
        out[c] = signal.fftconvolve(x[c], ir[c % ir.shape[0]])
    dry = pad_to(x, out.shape[1])
    return dry * (1 - wet * 0.35) + out * wet


def pingpong(x, delay_s, fb=0.45, wet=0.35, repeats=8, damp=4500):
    if x.ndim == 1:
        x = np.vstack([x, x])
    d = secs(delay_s)
    n = x.shape[1] + d * repeats
    out = pad_to(x.copy(), n)
    mono = x.mean(axis=0)
    tap = mono.copy()
    for k in range(1, repeats + 1):
        tap = lp(tap, damp, 1) * fb
        ch = k % 2
        s = d * k
        out[ch, s:s + len(tap)] += tap * wet
    return out


def soft_clip(x, drive=1.0):
    return np.tanh(x * drive) / np.tanh(drive)


def normalize(x, peak=0.89):
    m = np.max(np.abs(x))
    return x if m < 1e-9 else x * (peak / m)


def rms_db(x):
    return 20 * np.log10(np.sqrt(np.mean(x ** 2)) + 1e-12)


def set_rms(x, db):
    return x * 10 ** ((db - rms_db(x)) / 20)


def fade(x, fin=0.002, fout=0.01):
    n = x.shape[-1]
    e = np.ones(n)
    a, b = secs(fin), secs(fout)
    if a: e[:a] = np.linspace(0, 1, a)
    if b: e[-b:] *= np.linspace(1, 0, b)
    return x * e


def trim_tail(x, thresh=0.0015, fout=0.06):
    """Corta o silêncio do fim (cauda de reverb abaixo de ~-56 dB) e faz fade."""
    mono = np.abs(x) if x.ndim == 1 else np.max(np.abs(x), axis=0)
    idx = np.nonzero(mono > thresh)[0]
    end = min(mono.shape[0], (idx[-1] + secs(fout)) if len(idx) else mono.shape[0])
    y = x[..., :end]
    return fade(y, 0, fout)


def write(path, x):
    x = np.clip(x, -1, 1)
    data = (x.T if x.ndim == 2 else x) * 32767
    wavfile.write(path, SR, data.astype(np.int16))


# ============================================================== timbres

def pluck(freq, dur, bright=1.0, decay=7.0, harm=24):
    """Pluck aditivo: harmônicos agudos morrem mais rápido. Cristalino."""
    t = tvec(dur)
    y = np.zeros_like(t)
    for k in range(1, harm + 1):
        f = freq * k
        if f > 15000:
            break
        amp = (1.0 / k ** (1.25 - 0.35 * bright))
        dk = decay * (1 + (k - 1) * 0.55 / bright)
        y += amp * np.sin(2 * np.pi * f * t + RNG.uniform(0, 6.28)) * np.exp(-t * dk)
    return fade(y * np.clip(t / 0.002, 0, 1), 0, 0.01)


def bell(freq, dur, index=3.0, ratio=3.5, decay=3.5):
    """FM bell: brilho cristalino (coins, sparkle)."""
    t = tvec(dur)
    ienv = index * np.exp(-t * decay * 1.8)
    mod = np.sin(2 * np.pi * freq * ratio * t) * ienv
    car = np.sin(2 * np.pi * freq * t + mod)
    car += 0.35 * np.sin(2 * np.pi * freq * 2.0 * t + mod * 0.5) * np.exp(-t * decay * 2)
    return fade(car * np.exp(-t * decay) * np.clip(t / 0.001, 0, 1), 0, 0.01)


def supersaw(freq, dur, voices=7, detune_cents=14, seed_ph=True):
    y = np.zeros(secs(dur))
    for v in range(voices):
        c = (v - (voices - 1) / 2) / ((voices - 1) / 2) * detune_cents
        f = freq * 2 ** (c / 1200)
        y += saw(f, dur, RNG.uniform(0, 1) if seed_ph else 0)
    return y / voices


def kick(dur=0.45, punch=1.0):
    t = tvec(dur)
    f = 44 + 120 * np.exp(-t * 32) * punch
    body = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * 6.5)
    click = hp(noise(dur), 2500) * np.exp(-t * 400) * 0.35
    return soft_clip(body + click, 1.6)


def hat(dur=0.05, open_=False):
    t = tvec(dur)
    n = hp(noise(dur), 7500, 2)
    return n * np.exp(-t * (14 if open_ else 70))


def clap(dur=0.35):
    t = tvec(dur)
    n = bp(noise(dur), 900, 2600)
    e = np.zeros_like(t)
    for k, off in enumerate([0, 0.011, 0.022]):
        e += np.exp(-np.clip(t - off, 0, None) * 140) * (t >= off) * (0.8 if k < 2 else 1.0)
    e += np.exp(-t * 16) * 0.35
    return n * e


def riser(dur, f0=200, f1=4000):
    t = tvec(dur)
    k = (t / dur) ** 2
    nz = lp_sweep(noise(dur), f0 + (f1 - f0) * k, block=128) * (0.25 + k)
    tone = saw(np.geomspace(f0 * 0.5, f1 * 0.25, len(t)), dur) * 0.25 * k
    return (nz + lp(tone, 6000)) * np.clip(t / 0.05, 0, 1)


# ============================================================== teoria

D_MINOR_PENTA = [62, 65, 67, 69, 72]          # D4 F4 G4 A4 C5


def penta(i, base_oct=0):
    o, d = divmod(i, 5)
    return D_MINOR_PENTA[d] + 12 * (o + base_oct)


BPM = 124
BEAT = 60.0 / BPM
BAR = BEAT * 4
STEP = BEAT / 4      # 16th
BARS = 16
LOOP = BAR * BARS
LOOP_N = secs(LOOP)

# Dm  Bb  F  C  (2 compassos cada) x2
CHORDS = [
    [50, 53, 57, 62, 65],   # Dm (D3 F3 A3 D4 F4)
    [46, 50, 53, 58, 62],   # Bb
    [53, 57, 60, 65, 69],   # F
    [48, 52, 55, 60, 64],   # C
]
ROOTS = [38, 34, 41, 36]    # D2 Bb1 F2 C2


def chord_at(bar):
    return (bar // 2) % 4


# ============================================================== camadas da música

def new_buf(extra=4.0):
    return np.zeros((2, LOOP_N + secs(extra)))


def fold_loop(buf):
    """Dobra a cauda (reverb/delay) pro começo → loop sem emenda."""
    out = buf[:, :LOOP_N].copy()
    tail = buf[:, LOOP_N:]
    k = 0
    while tail.shape[1] > 0:
        m = min(LOOP_N, tail.shape[1])
        out[:, :m] += tail[:, :m]
        tail = tail[:, m:]
        k += 1
    return out


def layer_pad():
    buf = new_buf()
    for bar in range(0, BARS, 2):
        notes = CHORDS[chord_at(bar)]
        dur = BAR * 2 + 0.9
        start = secs(bar * BAR)
        for i, n in enumerate(notes):
            for side, p in ((0, -0.6), (1, 0.6)):
                v = supersaw(midi(n), dur, voices=5, detune_cents=16)
                v = lp(v, 1500 + 250 * i, 2) * env_adsr(dur, 0.6, 0.4, 0.8, 0.9)
                place(buf, pan(v * 0.5, p), start)
        # nota grave de sustentação
        sub = sine(midi(notes[0] - 12), dur) * env_adsr(dur, 0.8, 0.2, 0.9, 0.9) * 0.6
        place(buf, sub, start)
    # movimento: leve tremolo de filtro (LFO lento na amplitude dos agudos)
    lfo = 0.85 + 0.15 * np.sin(2 * np.pi * np.arange(buf.shape[1]) / SR / (BAR * 2))
    buf *= lfo
    buf = reverb(buf, 0.55)
    return fold_loop(buf)


ARP_PATTERN = [0, 2, 4, 2, 3, 1, 4, 2, 0, 3, 2, 4, 1, 3, 4, 2]


def layer_arp():
    buf = new_buf()
    for bar in range(BARS):
        notes = CHORDS[chord_at(bar)]
        hi = 12 if bar >= 8 and bar % 4 >= 2 else 0
        for s in range(16):
            idx = ARP_PATTERN[(s + bar * 3) % 16]
            n = notes[idx] + 12 + hi
            accent = 1.0 if s % 4 == 0 else (0.7 if s % 2 == 0 else 0.55)
            p = pluck(midi(n), 0.5, bright=0.9 + 0.3 * accent, decay=9)
            place(buf, pan(p * accent * 0.5, -0.35 + 0.7 * ((s % 8) / 7)), secs(bar * BAR + s * STEP))
    buf = pingpong(buf, BEAT * 0.75, fb=0.42, wet=0.45, repeats=6, damp=3800)
    buf = reverb(pad_to(buf, buf.shape[1]), 0.3)
    return fold_loop(buf)


def layer_drums():
    buf = new_buf(2.0)
    k = kick()
    for bar in range(BARS):
        base = bar * BAR
        for b in range(4):
            place(buf, k * 0.95, secs(base + b * BEAT))
            place(buf, pan(hat(0.32, True) * 0.32, 0.25), secs(base + b * BEAT + BEAT / 2))
        for b in (1, 3):
            place(buf, pan(clap() * 0.6, -0.05), secs(base + b * BEAT))
        for s in range(16):
            if s % 2 == 1 or (bar % 2 == 1 and s in (6, 14)):
                vel = 0.18 + 0.1 * RNG.random()
                place(buf, pan(hat() * vel, -0.3), secs(base + s * STEP))
        # virada a cada 4 compassos
        if bar % 4 == 3:
            for s in (12, 13, 14, 15):
                place(buf, pan(clap(0.12) * (0.25 + 0.1 * (s - 12)), 0.1), secs(base + s * STEP))
    buf = reverb(buf, 0.12)
    return fold_loop(buf)


def kick_sidechain(n):
    """Envelope de ducking no tempo dos kicks (pro baixo e pro pad 'respirar')."""
    t = (np.arange(n) / SR) % BEAT
    return 0.25 + 0.75 * np.clip(t / (BEAT * 0.55), 0, 1) ** 1.6


def layer_bass():
    buf = new_buf(1.0)
    pattern = [0, 0, 1, 0, 0, 1, 0, 1]   # 8ths: 1 = oitava acima
    for bar in range(BARS):
        root = ROOTS[chord_at(bar)]
        for e in range(8):
            if e == 0 and bar % 2 == 0:
                pass
            n = root + 12 * pattern[e]
            dur = BEAT / 2 * 0.92
            f = midi(n)
            v = saw(f, dur) * 0.6 + saw(f * 1.004, dur) * 0.4
            cut = 280 + 900 * np.exp(-tvec(dur) * 22)
            v = lp_sweep(v, cut, block=64)
            v += sine(f, dur) * 0.8
            v *= env_adsr(dur, 0.004, 0.08, 0.75, 0.03)
            place(buf, v * 0.55, secs(bar * BAR + e * BEAT / 2))
    buf *= kick_sidechain(buf.shape[1])
    buf = soft_clip(buf, 1.4)
    return fold_loop(buf)


SPARKLE = [(0, 7), (3, 9), (6, 8), (10, 10), (14, 9), (18, 7), (22, 11), (26, 8),
           (30, 6), (34, 9), (38, 12), (42, 10), (46, 8), (50, 11), (54, 13), (58, 10)]


def layer_sparkle():
    buf = new_buf(5.0)
    for beat, deg in SPARKLE:
        n = penta(deg, 1)
        b = bell(midi(n), 1.6, index=2.2, ratio=3.01, decay=2.6)
        place(buf, pan(b * 0.4, RNG.uniform(-0.7, 0.7)), secs(beat * BEAT))
        # eco "harmônico" uma oitava acima, bem baixo
        b2 = bell(midi(n + 12), 0.9, index=1.2, ratio=2.0, decay=4)
        place(buf, pan(b2 * 0.12, RNG.uniform(-0.8, 0.8)), secs(beat * BEAT + STEP * 3))
    buf = pingpong(buf, BEAT * 0.75, fb=0.5, wet=0.55, repeats=7, damp=5000)
    buf = reverb(buf, 0.6)
    return fold_loop(buf)


def render_music(out):
    layers = {
        "music_pad": (layer_pad, -21.0),
        "music_arp": (layer_arp, -22.0),
        "music_drums": (layer_drums, -18.5),
        "music_bass": (layer_bass, -20.0),
        "music_sparkle": (layer_sparkle, -26.0),
        "music_fever": (layer_fever, -23.0),
    }
    rendered = {}
    for name, (fn, db) in layers.items():
        print("render", name)
        rendered[name] = set_rms(fn(), db)
    full = sum(rendered.values())
    peak = np.max(np.abs(full))
    k = min(1.0, 0.95 / peak)
    print(f"mix peak {peak:.2f} -> scale {k:.2f}")
    for name, x in rendered.items():
        write(os.path.join(out, name + ".wav"), x * k)


# ============================================================== SFX

def sfx_coin(i):
    n = penta(i, 1)                      # D5 em diante, sobe a cada coin do combo
    f = midi(n)
    b = bell(f, 0.55, index=2.6, ratio=3.0, decay=7)
    b[:secs(0.35)] += pluck(f * 2, 0.35, bright=1.3, decay=14) * 0.4
    b[:secs(0.06)] += hp(noise(0.06), 9000) * env_exp(0.06, 80) * 0.15
    return normalize(fade(reverb(b, 0.18)[:, :secs(0.8)].mean(axis=0), 0, 0.15), 0.8)


def sfx_coin_simple():
    """Moeda: 'ti-ding' curto e limpo (B5 → E6), sempre igual."""
    y = np.zeros(secs(0.5))
    for k, (f, start, amp) in enumerate([(987.77, 0.0, 0.55), (1318.51, 0.055, 1.0)]):
        t = tvec(0.42)
        tone = np.sin(2 * np.pi * f * t) + 0.18 * np.sin(2 * np.pi * f * 2 * t) * np.exp(-t * 30)
        env = np.clip(t / 0.003, 0, 1) * np.exp(-t * (16 if k == 0 else 9))
        i0 = secs(start)
        y[i0:i0 + len(t)] += tone * env * amp
    y = reverb(y, 0.12)[:, :secs(0.5)].mean(axis=0)
    return normalize(fade(y, 0, 0.08), 0.75)


def sfx_gem():
    y = np.zeros(secs(1.4))
    for k, deg in enumerate([5, 7, 9, 10, 12]):
        b = bell(midi(penta(deg, 1)), 0.9, index=2.0, ratio=3.01, decay=4.5)
        y[secs(k * 0.045):secs(k * 0.045) + len(b)] += b * (0.6 + 0.1 * k)
    sh = hp(noise(1.4), 8000) * env_exp(1.4, 4, 0.05) * 0.12
    y = y[:secs(1.4)] + sh
    return normalize(fade(reverb(y, 0.35), 0, 0.3), 0.85)


def sfx_powerup_collect():
    r = riser(0.32, 300, 6000) * 0.6
    y = np.zeros(secs(1.6))
    y[:len(r)] += r
    st = secs(0.3)
    for deg in (5, 7, 8, 10):
        b = bell(midi(penta(deg, 1)), 1.2, index=1.8, ratio=2.0, decay=3.2)
        y[st:st + len(b)] += b * 0.45
    sw = supersaw(midi(74), 0.9) * env_exp(0.9, 4, 0.01)
    y[st:st + len(sw)] += lp(sw, 3000) * 0.35
    return normalize(fade(reverb(y, 0.4), 0, 0.3), 0.9)


def sfx_powerup_activate():
    d = 0.7
    t = tvec(d)
    sweep = supersaw(np.geomspace(midi(57), midi(69), len(t)), d) * env_exp(d, 4, 0.03)
    y = lp_sweep(sweep, 600 + 5000 * (t / d) ** 0.7) * 0.6
    y += bell(midi(81), d, index=1.5, ratio=2.0, decay=4) * 0.35
    return normalize(fade(reverb(y, 0.35), 0, 0.2), 0.85)


def sfx_powerdown():
    d = 0.45
    t = tvec(d)
    y = sine(np.geomspace(midi(81), midi(62), len(t)), d) * env_exp(d, 7, 0.005)
    y += sine(np.geomspace(midi(88), midi(69), len(t)), d) * env_exp(d, 9, 0.005) * 0.3
    return normalize(fade(reverb(y, 0.3), 0, 0.2), 0.6)


def sfx_shield_break():
    d = 1.3
    y = np.zeros(secs(d))
    for _ in range(26):
        f = RNG.uniform(2500, 9000)
        st = secs(RNG.uniform(0, 0.09))
        b = bell(f, 0.5, index=RNG.uniform(1, 3), ratio=RNG.uniform(1.4, 3.7), decay=RNG.uniform(9, 20))
        y[st:st + len(b)] += b * RNG.uniform(0.15, 0.35)
    y[:secs(0.25)] += hp(noise(0.25), 3000) * env_exp(0.25, 22) * 0.7
    y[:secs(0.4)] += kick(0.4, 0.6) * 0.6
    return normalize(fade(reverb(y, 0.3), 0, 0.3), 0.9)


def sfx_switch(direction):
    # -1 / 0 / +1 → grave / médio / agudo, notas da pentatônica
    n = {-1: 74, 0: 79, 1: 81}[direction]
    d = 0.12
    t = tvec(d)
    f = midi(n) * (1 + 0.6 * np.exp(-t * 90))
    y = sine(f, d) * env_exp(d, 38, 0.001)
    y += hp(noise(d), 4000) * env_exp(d, 300) * 0.25
    y += pluck(midi(n), d, bright=0.8, decay=40) * 0.4
    return normalize(fade(y, 0, 0.02), 0.7)


def sfx_land():
    d = 0.16
    t = tvec(d)
    y = sine(70 + 60 * np.exp(-t * 40), d) * env_exp(d, 26, 0.002)
    y += lp(noise(d), 900) * env_exp(d, 45) * 0.4
    return normalize(fade(y, 0, 0.03), 0.6)


def sfx_whoosh():
    d = 0.32
    t = tvec(d)
    shape = np.sin(np.pi * t / d) ** 1.5
    y = lp_sweep(noise(d), 600 + 3200 * shape, block=64) * shape
    y = bp(y, 300, 6000)
    return normalize(fade(y, 0.005, 0.05), 0.55)


def sfx_tier_up():
    r = riser(0.7, 250, 7000) * 0.5
    y = np.zeros(secs(3.2))
    y[:len(r)] += r
    st = secs(0.68)
    imp = kick(0.9, 1.3) * 0.9 + hp(noise(0.9), 4000) * env_exp(0.9, 5) * 0.25
    y[st:st + len(imp)] += imp
    for i, n in enumerate([50, 57, 62, 64, 65, 69, 74]):       # Dm add9 larguíssimo
        v = supersaw(midi(n), 2.4) * env_adsr(2.4, 0.005, 0.5, 0.5, 1.4)
        y[st:st + len(v)] += lp(v, 2600 + 300 * i) * 0.22
    for deg in (10, 12, 14):
        b = bell(midi(penta(deg, 1)), 1.8, index=1.6, ratio=2.0, decay=2.4)
        y[st:st + len(b)] += b * 0.2
    return normalize(fade(reverb(y, 0.5), 0, 0.5), 0.95)


def sfx_death():
    d = 2.6
    t = tvec(d)
    y = np.zeros(len(t))
    boom = sine(38 + 60 * np.exp(-t * 18), d) * env_exp(d, 2.8)
    y += boom * 0.9
    crunch = soft_clip(lp(noise(0.5), 2500) * env_exp(0.5, 9), 3) * 0.5
    y[:len(crunch)] += crunch
    down = saw(np.geomspace(700, 45, secs(1.1)), 1.1) * env_exp(1.1, 2.2, 0.01)
    down = lp_sweep(down, np.geomspace(5000, 200, len(down)))
    # glitch: picota o sweep
    gate = (np.floor(np.arange(len(down)) / secs(0.035)) % 3 != 2).astype(float)
    y[:len(down)] += down * gate * 0.45
    return normalize(fade(reverb(y, 0.45), 0, 0.6), 0.95)


def sfx_revive():
    d = 1.2
    sw = supersaw(midi(62), d) * env_exp(d, 2.2)
    sw = reverb(lp(sw, 3500), 0.7)[:, ::-1][:, -secs(d):]     # swell reverso
    y = np.zeros((2, secs(2.4)))
    y[:, :sw.shape[1]] += sw * 0.6
    st = secs(d - 0.02)
    for deg in (5, 8, 10, 12):
        b = bell(midi(penta(deg, 1)), 1.2, index=1.6, ratio=2.0, decay=3)
        place(y, b * 0.4, st)
    return normalize(fade(reverb(y, 0.3), 0, 0.4), 0.9)


def sfx_impact():
    d = 0.7
    y = kick(d, 1.1) * 0.9
    y += hp(noise(d), 1800) * env_exp(d, 18) * 0.5
    y += soft_clip(lp(noise(d), 600) * env_exp(d, 10), 2) * 0.4
    return normalize(fade(reverb(y, 0.2), 0, 0.2), 0.9)


def sfx_debuff():
    d = 0.8
    t = tvec(d)
    f = midi(62) * (1 + 0.08 * np.sin(2 * np.pi * 7 * t)) * np.geomspace(1.5, 0.6, len(t))
    y = lp(saw(f, d), 2200) * env_exp(d, 3.5, 0.01)
    y += sine(f * 0.5, d) * env_exp(d, 3, 0.01) * 0.5
    return normalize(fade(reverb(y, 0.3), 0, 0.2), 0.8)


def sfx_count(final=False):
    if not final:
        d = 0.35
        y = bell(midi(69), d, index=1.2, ratio=2.0, decay=10) + sine(midi(57), d) * env_exp(d, 14) * 0.4
        return normalize(fade(reverb(y, 0.2), 0, 0.1), 0.75)
    d = 1.3
    y = np.zeros(secs(d))
    for i, n in enumerate([62, 69, 74, 77, 81]):
        v = supersaw(midi(n), d) * env_adsr(d, 0.004, 0.3, 0.45, 0.8)
        y += lp(v, 3200) * 0.25
    k = kick(0.5, 1.0)
    y[:len(k)] += k * 0.7
    w = sfx_whoosh()
    y[:len(w)] += w * 0.4
    return normalize(fade(reverb(y, 0.4), 0, 0.3), 0.92)


def sfx_ui_click():
    d = 0.07
    t = tvec(d)
    y = sine(1800 * (1 + 0.5 * np.exp(-t * 120)), d) * env_exp(d, 70, 0.0005)
    y += hp(noise(d), 6000) * env_exp(d, 400) * 0.2
    return normalize(fade(y, 0, 0.01), 0.55)


# ============================================================== SFX — camada de jogo (combo/fever/zonas)

def sfx_clutch():
    """Ting ascendente rápido: duas notas da pentatônica + brilho."""
    y = np.zeros(secs(0.9))
    for k, deg in enumerate([7, 10]):
        b = bell(midi(penta(deg, 1)), 0.6, index=2.2, ratio=3.0, decay=6)
        st = secs(k * 0.06)
        y[st:st + len(b)] += b * (0.6 + 0.2 * k)
    y[:secs(0.12)] += hp(noise(0.12), 7000) * env_exp(0.12, 40) * 0.2
    return normalize(fade(reverb(y, 0.25), 0, 0.2), 0.8)


def sfx_risky():
    """Intervalo 'ousado' (trítono resolvendo) com pluck grave-agudo."""
    y = np.zeros(secs(0.9))
    a = pluck(midi(62), 0.5, bright=1.1, decay=8)
    b = pluck(midi(69 + 12), 0.6, bright=1.3, decay=7)
    y[:len(a)] += a * 0.6
    st = secs(0.09)
    y[st:st + len(b)] += b * 0.7
    return normalize(fade(reverb(y, 0.3), 0, 0.2), 0.8)


def sfx_nearmiss():
    """Whoosh curtinho passando + blip agudo."""
    d = 0.35
    t = tvec(d)
    shape = np.exp(-((t - 0.09) / 0.05) ** 2)
    y = bp(noise(d), 1500, 9000) * shape * 0.8
    blip = sine(midi(93) * (1 + 0.3 * np.exp(-t * 60)), d) * env_exp(d, 18, 0.002)
    y += blip * 0.35
    return normalize(fade(y, 0, 0.05), 0.7)


def sfx_fever_ready():
    """Arpejo cintilante subindo (barra cheia)."""
    y = np.zeros(secs(1.6))
    for k, deg in enumerate([5, 7, 9, 10, 12, 14]):
        b = bell(midi(penta(deg, 1)), 1.0, index=1.8, ratio=2.0, decay=4)
        st = secs(k * 0.055)
        y[st:st + len(b)] += b * 0.5
    return normalize(fade(reverb(y, 0.45), 0, 0.3), 0.85)


def sfx_fever_start():
    """Riser curto + impacto + acorde enorme (o 'drop')."""
    r = riser(0.45, 300, 9000) * 0.6
    y = np.zeros(secs(3.0))
    y[:len(r)] += r
    st = secs(0.44)
    imp = kick(0.9, 1.4) + hp(noise(0.9), 3000) * env_exp(0.9, 4) * 0.35
    y[st:st + len(imp)] += imp
    for i, n in enumerate([50, 62, 65, 69, 72, 74, 77, 81]):
        v = supersaw(midi(n), 2.2) * env_adsr(2.2, 0.004, 0.4, 0.55, 1.2)
        y[st:st + len(v)] += lp(v, 3600 + 300 * i) * 0.18
    return normalize(fade(reverb(y, 0.5), 0, 0.5), 0.95)


def sfx_fever_end():
    d = 0.9
    t = tvec(d)
    y = lp_sweep(supersaw(np.geomspace(midi(74), midi(62), len(t)), d), np.geomspace(5000, 400, len(t)))
    y *= env_exp(d, 3.5, 0.01)
    return normalize(fade(reverb(y, 0.35), 0, 0.3), 0.7)


def sfx_zone_start():
    """Alarme musical: stab de synth duplo + riser."""
    y = np.zeros(secs(2.4))
    r = riser(0.5, 200, 5000) * 0.45
    y[:len(r)] += r
    for k, chord in enumerate([[62, 65, 69, 74], [65, 69, 72, 77]]):
        st = secs(0.5 + k * 0.24)
        for n in chord:
            v = supersaw(midi(n), 0.5) * env_adsr(0.5, 0.003, 0.15, 0.4, 0.2)
            y[st:st + len(v)] += lp(v, 4200) * 0.28
        kk = kick(0.4, 1.0)
        y[st:st + len(kk)] += kk * 0.5
    return normalize(fade(reverb(y, 0.4), 0, 0.4), 0.92)


def sfx_zone_end():
    y = np.zeros(secs(2.0))
    for k, deg in enumerate([5, 8, 10, 12, 15]):
        b = bell(midi(penta(deg, 1)), 1.1, index=1.6, ratio=2.0, decay=3.2)
        st = secs(k * 0.08)
        y[st:st + len(b)] += b * 0.45
    return normalize(fade(reverb(y, 0.45), 0, 0.4), 0.88)


def sfx_bonus_coin():
    """Coin 'gorda': acorde de sinos + brilho longo."""
    y = np.zeros(secs(1.2))
    for deg in (10, 12, 14):
        b = bell(midi(penta(deg, 1)), 0.9, index=2.4, ratio=3.0, decay=5)
        y[:len(b)] += b * 0.4
    y[:secs(0.5)] += hp(noise(0.5), 8000) * env_exp(0.5, 9) * 0.18
    return normalize(fade(reverb(y, 0.35), 0, 0.3), 0.9)


def layer_fever():
    """Camada só do Fever: lead em 16ths uma oitava acima + ride/shaker."""
    buf = new_buf(3.0)
    pat = [0, 2, 4, 3, 2, 4, 1, 4, 0, 3, 4, 2, 4, 3, 1, 4]
    for bar in range(BARS):
        notes = CHORDS[chord_at(bar)]
        for s16 in range(16):
            n = notes[pat[s16]] + 24
            v = saw(midi(n), STEP * 0.95) * 0.5 + saw(midi(n) * 1.006, STEP * 0.95) * 0.5
            v = lp_sweep(v, 900 + 5200 * np.exp(-tvec(STEP * 0.95) * 18), block=64)
            v *= env_adsr(STEP * 0.95, 0.002, 0.05, 0.6, 0.03)
            accent = 1.0 if s16 % 4 == 0 else 0.7
            place(buf, pan(v * 0.32 * accent, -0.4 + 0.8 * ((s16 * 5) % 16) / 15), secs(bar * BAR + s16 * STEP))
            sh = hp(noise(0.04), 9000) * env_exp(0.04, 90) * (0.14 if s16 % 2 else 0.22)
            place(buf, pan(sh, 0.45), secs(bar * BAR + s16 * STEP))
    buf = pingpong(buf, BEAT * 0.5, fb=0.35, wet=0.3, repeats=4, damp=5000)
    buf = reverb(buf, 0.2)
    return fold_loop(buf)


def render_sfx(out):
    jobs = {}   # moeda: só o sfx_coin simples (o combo de 10 notas foi aposentado)
    jobs.update({
        "sfx_gem": sfx_gem,
        "sfx_powerup_collect": sfx_powerup_collect,
        "sfx_powerup_activate": sfx_powerup_activate,
        "sfx_powerdown": sfx_powerdown,
        "sfx_shield_break": sfx_shield_break,
        "sfx_switch_left": lambda: sfx_switch(-1),
        "sfx_switch_mid": lambda: sfx_switch(0),
        "sfx_switch_right": lambda: sfx_switch(1),
        "sfx_land": sfx_land,
        "sfx_whoosh": sfx_whoosh,
        "sfx_tier_up": sfx_tier_up,
        "sfx_death": sfx_death,
        "sfx_revive": sfx_revive,
        "sfx_impact": sfx_impact,
        "sfx_debuff": sfx_debuff,
        "sfx_count": lambda: sfx_count(False),
        "sfx_go": lambda: sfx_count(True),
        "sfx_ui_click": sfx_ui_click,
        "sfx_clutch": sfx_clutch,
        "sfx_risky": sfx_risky,
        "sfx_nearmiss": sfx_nearmiss,
        "sfx_fever_ready": sfx_fever_ready,
        "sfx_fever_start": sfx_fever_start,
        "sfx_fever_end": sfx_fever_end,
        "sfx_zone_start": sfx_zone_start,
        "sfx_zone_end": sfx_zone_end,
        "sfx_bonus_coin": sfx_bonus_coin,
        "sfx_coin": sfx_coin_simple,
    })
    for name, fn in jobs.items():
        x = trim_tail(fn())
        write(os.path.join(out, name + ".wav"), x)
        print("sfx", name, f"{x.shape[-1] / SR:.2f}s")


if __name__ == "__main__":
    out = sys.argv[1] if len(sys.argv) > 1 else "out"
    os.makedirs(out, exist_ok=True)
    which = sys.argv[2] if len(sys.argv) > 2 else "all"
    only = set(sys.argv[3].split(",")) if len(sys.argv) > 3 else None
    if which in ("all", "sfx"):
        render_sfx(out)
    if which in ("all", "music"):
        render_music(out)
