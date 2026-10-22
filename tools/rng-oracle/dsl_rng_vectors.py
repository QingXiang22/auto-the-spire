"""Generate independent source-based vectors for the pure DSL RNG consumers.

Uses the locked source, native pool metadata and independent numerical oracle, never Engine code.
The reward sequence deliberately covers the migrated gold/card operations; PotionRewardOdds
and population are checked separately by normal_reward_vectors, never compensation draws.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import struct

from locked_rng_oracle import Rng, deterministic_hash_code, stream_seed, content_seed, RUN_RNG_TYPES, PLAYER_RNG_TYPES, snake_case, SOURCE, source_hashes, reference_lock, native_metadata

ROOT = Path(__file__).resolve().parents[2]

def f32(value):
    return struct.unpack('<f', struct.pack('<f', value))[0]

def bits(value):
    return struct.unpack('<I', struct.pack('<f', value))[0]

def next_float(rng):
    return f32(rng.next_double())

def generate():
    paths = ['Random/MegaRandom.cs', 'Random/Rng.cs', 'Random/PlayerRngSet.cs',
             'Runs/RunRngSet.cs', 'Helpers/StringHelper.cs', 'Helpers/ListExtensions.cs',
             'Models/CardModel.cs', 'Odds/CardRarityOdds.cs', 'Odds/PlayerOddsSet.cs',
             'Factories/CardFactory.cs', 'Models/CardPools/IroncladCardPool.cs']
    pool = [{'id': c['id'], 'rarity': c['rarity']} for c in native_metadata()['cards']
            if not c['multiplayer_only'] and c['rarity'] in ('common', 'uncommon', 'rare')]
    assert len(pool) == 80
    hashes = source_hashes(paths + [f"Models/Cards/{c['name']}.cs" for c in native_metadata()['cards']])
    vectors = []
    for seed in ['20261018', '777', 'ENV-IRONCLAD-001', 'BASE-DSL-STDIO-00', 'ABC', 'abc', '斩杀尖塔2', 'A😀B', ' seed ', '', 'old', 'old777', 'oldold777', 'Old777']:
        root = deterministic_hash_code(seed)
        streams = {f'base-spire:run:{snake_case(name)}': stream_seed(root, name) for name in RUN_RNG_TYPES}
        streams.update({f'base-spire:player:{name.lower()}': stream_seed(root, name) for name in PLAYER_RNG_TYPES})
        draws = {}
        for name, value in streams.items():
            r = Rng(value)
            draws[name] = [r.next_int(100) for _ in range(4)]
        shuffle = Rng(streams['base-spire:run:shuffle'])
        initial = shuffle.shuffle(list(range(10)))
        # Initial source deck order: 5 Strike, 4 Defend, Bash. StableShuffle first
        # sorts by ModelId.Entry and upgrade level, then uses the SAME RNG cursor.
        entries = ['STRIKE_IRONCLAD'] * 5 + ['DEFEND_IRONCLAD'] * 4 + ['BASH']
        sorted_cards = sorted(initial, key=lambda i: (entries[i], 0))
        reshuffled = shuffle.shuffle(sorted_cards)
        floats = Rng(streams['base-spire:player:rewards'])
        float_bits = [bits(next_float(floats)) for _ in range(8)]
        rewards = Rng(streams['base-spire:player:rewards'])
        offset = f32(-0.05)
        offers = []
        for _ in range(2):
            gold = rewards.next_int_range(10, 21)
            candidates = list(pool)
            cards = []
            for _ in range(3):
                roll = next_float(rewards)
                rare = f32(f32(0.03) + offset)
                uncommon = f32(f32(0.37) + rare)
                rarity = 'rare' if roll < rare else 'uncommon' if roll < uncommon else 'common'
                offset = f32(-0.05) if rarity == 'rare' else min(f32(offset + f32(0.01)), f32(0.4))
                eligible = [c for c in candidates if c['rarity'] == rarity]
                chosen = eligible[rewards.next_int(len(eligible))]
                candidates.remove(chosen)
                up = next_float(rewards) <= 0.0
                cards.append({'id': chosen['id'], 'upgraded': up})
            offers.append({'gold': gold, 'cards': cards, 'offset_bits': bits(offset), 'counter': rewards.counter})
        vectors.append({'seed': seed, 'run_seed': root, 'streams': streams, 'int100_first4': draws,
                        'float32_bits_first8': float_bits, 'initial_order': initial,
                        'reshuffle_order': reshuffled, 'shuffle_counter': shuffle.counter,
                        'content_seed_slot2_mixin7': content_seed(root, 2, 'SAPPHIRE_SEED', 7), 'rewards': offers})
    return {'schema': 'base-spire/source-rng-vectors@2', 'reference_lock': reference_lock(), 'source_hash_encoding': 'utf8-lf', 'source_sha256': hashes,
            'reward_scope': 'migrated gold/card calls; excludes potion-roll and normal Run prerequisites', 'vectors': vectors}

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    args.output.write_text(json.dumps(generate(), ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('Source RNG vectors written:', args.output)
