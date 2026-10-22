"""Freeze source-only permanent upgrade vectors; never imports the Engine or base rules."""
import argparse
import hashlib
import json
from pathlib import Path
import re

from locked_rng_oracle import Rng, deterministic_hash_code, stream_seed, SOURCE, source_hashes, reference_lock

ROOT = Path(__file__).resolve().parents[2]


def generate():
    source = SOURCE
    names = ['StrikeIronclad', 'Anger', 'DefendIronclad', 'Bash', 'StrikeIronclad',
             'Armaments', 'Impervious', 'Inflame', 'TwinStrike', 'ShrugItOff',
             'BodySlam', 'Whirlwind', 'Cascade', 'StrikeIronclad']
    paths = ['Helpers/ListExtensions.cs', 'Models/CardModel.cs', 'Random/Rng.cs',
             'Random/MegaRandom.cs', 'Runs/RunRngSet.cs', 'Helpers/StringHelper.cs']
    paths += [f'Models/Relics/{name}.cs' for name in ['Whetstone', 'WarPaint', 'SandCastle']]
    cards = []
    for index, name in enumerate(names):
        path = f'Models/Cards/{name}.cs'
        text = (source / path).read_text(encoding='utf-8-sig')
        cards.append(dict(name=name, entry=re.sub(r'(?<!^)(?=[A-Z])', '_', name).upper(),
                          kind=re.search(r'CardType\.(\w+)', text).group(1).lower(),
                          upgraded=index == 3, can_upgrade=index != 4))
        paths.append(path)
    vectors = []
    for seed in ['20261018', '777', 'BASE-DSL-STDIO-00']:
        rng = Rng(stream_seed(deterministic_hash_code(seed), 'Niche'))
        upgraded = {i for i, card in enumerate(cards) if card['upgraded']}
        steps = []
        for name, kind, amount in [('Whetstone', 'attack', 2), ('Whetstone', 'attack', 2),
                                   ('WarPaint', 'skill', 2), ('SandCastle', 'all', 6)]:
            candidates = [i for i, card in enumerate(cards) if card['can_upgrade']
                          and i not in upgraded and (kind == 'all' or card['kind'] == kind)]
            # At most 13 candidates: source List.Sort uses insertion sort for this small
            # range, preserving equal-key instance order. Large equal-key decks are not an oracle claim.
            candidates.sort(key=lambda i: (cards[i]['entry'], 0))
            selected = rng.shuffle(candidates)[:amount]
            upgraded.update(selected)
            steps.append(dict(relic=name, selected_indices=selected,
                              upgraded_indices=sorted(upgraded), niche_counter=rng.counter))
        vectors.append(dict(seed=seed, steps=steps))
    return dict(schema='base-spire/source-pickup-upgrade-vectors@2', reference_lock=reference_lock(), cards=cards, vectors=vectors,
                source_sha256=source_hashes(sorted(set(paths))))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    args.output.write_text(json.dumps(generate(), ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('Source pickup upgrade vectors written:', args.output)
