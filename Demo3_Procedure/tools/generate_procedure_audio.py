#!/usr/bin/env python3
"""Generate the original mechanical sound effects used by Demo 3."""

from pathlib import Path
import wave

import numpy as np
from scipy.signal import butter, sosfilt


SAMPLE_RATE = 44_100
ROOT = Path(__file__).resolve().parents[1]
AUDIO_DIR = ROOT / "Assets" / "Audio"
RNG = np.random.default_rng(0xAC3E)


def envelope(length: int, attack: float, decay: float) -> np.ndarray:
    time = np.arange(length) / SAMPLE_RATE
    return (1.0 - np.exp(-time / attack)) * np.exp(-time / decay)


def filtered_noise(length: int, cutoff: float, highpass: bool = False) -> np.ndarray:
    mode = "highpass" if highpass else "lowpass"
    filt = butter(3, cutoff, btype=mode, fs=SAMPLE_RATE, output="sos")
    return sosfilt(filt, RNG.standard_normal(length))


def place(target: np.ndarray, sound: np.ndarray, start_seconds: float, gain: float = 1.0) -> None:
    start = round(start_seconds * SAMPLE_RATE)
    end = min(len(target), start + len(sound))
    target[start:end] += gain * sound[: end - start]


def metallic_hit(duration: float, frequencies: tuple[float, ...]) -> np.ndarray:
    length = round(duration * SAMPLE_RATE)
    time = np.arange(length) / SAMPLE_RATE
    hit = np.zeros(length)
    for index, frequency in enumerate(frequencies):
        phase = RNG.uniform(0, 2 * np.pi)
        damping = 18.0 + index * 7.0
        hit += np.sin(2 * np.pi * frequency * time + phase) * np.exp(-damping * time)
    hit /= max(1.0, len(frequencies))
    hit += 0.35 * filtered_noise(length, 1_400, highpass=True) * np.exp(-55.0 * time)
    return hit


def make_squeak() -> np.ndarray:
    """A short, dry ratchet-pawl click designed to layer cleanly while a screw turns."""
    duration = 0.052
    length = round(duration * SAMPLE_RATE)
    time = np.arange(length) / SAMPLE_RATE

    # A pawl striking the next tooth: one hard transient and a restrained
    # steel-body ring. There is deliberately no pitched sweep or squeal.
    transient = filtered_noise(length, 1_900, highpass=True)
    transient *= np.exp(-time / 0.0022)

    body = np.zeros(length)
    for frequency, gain, decay in (
        (1_180.0, 0.55, 0.013),
        (2_360.0, 0.32, 0.009),
        (3_920.0, 0.16, 0.006),
    ):
        body += gain * np.sin(2 * np.pi * frequency * time) * np.exp(-time / decay)

    # A quieter secondary contact gives the click a small mechanical "reset."
    reset = metallic_hit(0.026, (1_540.0, 3_080.0))
    reset *= envelope(len(reset), 0.00015, 0.005)

    mono = 0.78 * transient + body
    place(mono, reset, 0.012, 0.18)
    return stereoize(mono, width=0.025, delay_samples=3)


def stereoize(mono: np.ndarray, width: float, delay_samples: int) -> np.ndarray:
    delayed = np.pad(mono, (delay_samples, 0))[: len(mono)]
    side = filtered_noise(len(mono), 5_500) * envelope(len(mono), 0.0005, 0.07)
    left = mono + width * delayed + 0.015 * side
    right = mono - width * delayed - 0.015 * side
    return np.column_stack((left, right))


def write_wav(path: Path, audio: np.ndarray) -> None:
    audio -= np.mean(audio, axis=0, keepdims=True)
    fade_length = round(0.012 * SAMPLE_RATE)
    audio[-fade_length:] *= np.linspace(1.0, 0.0, fade_length)[:, None]
    audio *= (10 ** (-1.0 / 20.0)) / np.max(np.abs(audio))
    pcm = np.round(audio * 32_767.0).astype("<i2")
    with wave.open(str(path), "wb") as output:
        output.setnchannels(2)
        output.setsampwidth(2)
        output.setframerate(SAMPLE_RATE)
        output.writeframes(pcm.tobytes())


def main() -> None:
    AUDIO_DIR.mkdir(parents=True, exist_ok=True)
    write_wav(AUDIO_DIR / "squeak.wav", make_squeak())


if __name__ == "__main__":
    main()
