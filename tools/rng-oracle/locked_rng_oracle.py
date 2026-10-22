"""Independent current-source RNG derivation; raw xoshiro mapping reuses its unchanged oracle."""
import hashlib
import json
import subprocess
from pathlib import Path
from vanilla_rng_oracle import MegaRandom, Rng as LegacyRng, deterministic_hash_code_old, RUN_RNG_TYPES, PLAYER_RNG_TYPES, snake_case, rotl

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'reference/sts2-sim/src/Sts2Sim.Core'
MASK = (1 << 64) - 1
P1, P2, P3, P4, P5 = 11400714785074694791, 14029467366897019727, 1609587929392839161, 9650029242287828579, 2870177450012600261

def hash64(text):
    data = text.encode('utf-8'); n = len(data); i = 0
    def word(at, width=8): return int.from_bytes(data[at:at+width], 'little')
    def round_(a, b): return rotl((a + b * P2) & MASK, 31) * P1 & MASK
    if n >= 32:
        lanes = [(P1 + P2) & MASK, P2, 0, -P1 & MASK]
        while i <= n - 32:
            lanes = [round_(a, word(i + j * 8)) for j, a in enumerate(lanes)]; i += 32
        h = sum(rotl(a, r) for a, r in zip(lanes, [1, 7, 12, 18])) & MASK
        for a in lanes: h = ((h ^ round_(0, a)) * P1 + P4) & MASK
    else: h = P5
    h = (h + n) & MASK
    while i <= n - 8:
        h = (rotl(h ^ round_(0, word(i)), 27) * P1 + P4) & MASK; i += 8
    if i <= n - 4:
        h = (rotl(h ^ (word(i, 4) * P1 & MASK), 23) * P2 + P3) & MASK; i += 4
    while i < n:
        h = rotl(h ^ (data[i] * P5 & MASK), 11) * P1 & MASK; i += 1
    h ^= h >> 33; h = h * P2 & MASK; h ^= h >> 29; h = h * P3 & MASK
    return h ^ (h >> 32)

def deterministic_hash_code(seed):
    return deterministic_hash_code_old(seed[3:]) if seed.startswith('old') else hash64(seed)

def stream_seed(root, name): return (root + hash64(snake_case(name))) & MASK

def content_seed(root, slot, entry, mixin=0): return (root + slot + hash64(entry) + mixin) & MASK

class Rng(LegacyRng):
    def __init__(self, seed, counter=0):
        self.counter = 0; self.seed = seed & MASK; self.random = MegaRandom(self.seed); self.fast_forward(counter)

def reference_lock():
    lock = json.loads((ROOT / 'reference/baselines/sts2-sim.json').read_text(encoding='utf-8'))
    def git(*args): return subprocess.check_output(['git', '-C', str(SOURCE.parents[1]), *args], text=True).strip()
    if git('rev-parse', 'HEAD') != lock['commit'] or git('rev-parse', 'HEAD^{tree}') != lock['tree'] or git('status', '--porcelain'):
        raise RuntimeError('Reference checkout must be clean and match the committed lock')
    return lock

def source_hashes(paths):
    reference_lock()
    return {(SOURCE / p).relative_to(ROOT).as_posix(): hashlib.sha256((SOURCE / p).read_bytes().replace(b'\r\n', b'\n')).hexdigest() for p in paths}


def native_metadata():
    data = json.loads((ROOT / 'tools/base-spire-dev/fixtures/reference-pool-metadata.json').read_text(encoding='utf-8'))
    if data['reference_lock'] != reference_lock(): raise RuntimeError('Regenerate native pool metadata for current lock')
    return data
