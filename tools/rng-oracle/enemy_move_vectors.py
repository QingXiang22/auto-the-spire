"""Generate source-only move vectors; no Engine, DSL or reference simulator is imported."""
import argparse
import hashlib
import json
from pathlib import Path
import struct

from vanilla_rng_oracle import Rng, deterministic_hash_code, stream_seed

ROOT = Path(__file__).resolve().parents[2]


def f32(value):
    return struct.unpack('<f', struct.pack('<f', value))[0]


def bits(value):
    return struct.unpack('<I', struct.pack('<f', value))[0]


def move_rolls(seed, branch_weights=(1, 1)):
    rng_seed = stream_seed(deterministic_hash_code(seed), 'MonsterAi')
    rng = Rng(rng_seed)
    history = ['STICKY_SHOT_MOVE']
    rows = []
    for _ in range(12):
        attack_ok = len(history) < 2 or history[-2:] != ['POKEY_POUNCE_MOVE'] * 2
        status_ok = history[-1] != 'STICKY_SHOT_MOVE'
        weights = [f32(branch_weights[0]) if attack_ok else 0.0, f32(branch_weights[1]) if status_ok else 0.0]
        total = f32(weights[0] + weights[1])
        raw = rng.next_double()
        roll = f32(raw * total)
        remaining = roll
        selected = None
        for candidate, weight in zip(['POKEY_POUNCE_MOVE', 'STICKY_SHOT_MOVE'], weights):
            remaining = f32(remaining - weight)
            if remaining <= 0:
                selected = candidate
                break
        assert selected is not None
        rows.append({'before': rng.counter - 1, 'after': rng.counter,
                     'candidates': ['POKEY_POUNCE_MOVE', 'STICKY_SHOT_MOVE'],
                     'weight_bits': [bits(w) for w in weights], 'total_bits': bits(total),
                     'raw_double': raw, 'roll_bits': bits(roll), 'selected': selected})
        history.append(selected)
    return {'seed': seed, 'monster_ai_seed': rng_seed, 'initial': history[0], 'rolls': rows}


def generate():
    paths = ['Models/Monsters/TwigSlimeM.cs', 'Models/Monsters/FuzzyWurmCrawler.cs',
             'Models/Cards/Slimed.cs', 'Models/MonsterModel.cs',
             'MonsterMoves/MonsterMoveStateMachine/MonsterMoveStateMachine.cs',
             'MonsterMoves/MonsterMoveStateMachine/RandomBranchState.cs',
             'MonsterMoves/MonsterMoveStateMachine/MoveState.cs', 'Random/Rng.cs',
             'Random/MegaRandom.cs', 'Runs/RunRngSet.cs', 'Helpers/StringHelper.cs']
    source = ROOT / 'sts2/src/Core'
    hashes = {f'sts2/src/Core/{p}': hashlib.sha256((source / p).read_bytes()).hexdigest() for p in paths}
    twig = (source / paths[0]).read_text(encoding='utf-8-sig')
    branch = (source / paths[5]).read_text(encoding='utf-8-sig')
    rng_source = (source / 'Random/Rng.cs').read_text(encoding='utf-8-sig')
    assert 'AddBranch(moveState, 2);' in twig and 'AddBranch(moveState2, MoveRepeatType.CannotRepeat);' in twig
    assert 'return new MonsterMoveStateMachine(list, moveState2);' in twig
    assert 'AddBranch(state, maxRepeats, 1f);' in branch
    assert 'float num = rng.NextFloat(max);' in branch and 'num -= GetStateWeight(state, owner);' in branch
    assert 'return (float)(_random.NextDouble() * (double)(max - min) + (double)min);' in rng_source
    vectors = []
    # The source state log starts with the fixed first move; repeat limits inspect that saved log.
    for seed in ['20261018', '777', 'ENV-IRONCLAD-001', 'A😀B']:
        vectors.append(move_rolls(seed))
    return {'schema': 'base-spire/source-enemy-moves@1', 'source_sha256': hashes,
            'scope': 'Controlled A0 move selection only; no HP roll, normal encounter, complete combat or simulator comparison.',
            'vectors': vectors,
            'fractional_probe': {**move_rolls('20261018', (0.3, 0.9)),
                                 'scope': 'Synthetic fractional weights for the source RandomBranchState algorithm; not TwigSlimeM gameplay.'}}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(generate(), ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
