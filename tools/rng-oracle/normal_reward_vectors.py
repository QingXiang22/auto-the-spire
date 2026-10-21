"""Source-only ordinary A0 Ironclad reward vectors, including potion pre-consumption."""
import argparse
import hashlib
import json
from pathlib import Path
import re

from dsl_rng_vectors import f32, bits, next_float
from vanilla_rng_oracle import Rng, deterministic_hash_code, stream_seed

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'sts2/src/Core'


def generate():
    hashes = {}

    def read(relative):
        path = SOURCE / relative
        hashes[path.relative_to(ROOT).as_posix()] = hashlib.sha256(path.read_bytes()).hexdigest()
        return path.read_text(encoding='utf-8-sig')

    for path in ['Rewards/RewardsSet.cs', 'Rewards/PotionReward.cs', 'Rewards/GoldReward.cs',
                 'Rewards/CardReward.cs', 'Factories/PotionFactory.cs', 'Factories/CardFactory.cs',
                 'Odds/PotionRewardOdds.cs', 'Odds/CardRarityOdds.cs', 'Odds/PlayerOddsSet.cs',
                 'Runs/CardCreationOptions.cs', 'Commands/PotionCmd.cs', 'Random/Rng.cs', 'Random/MegaRandom.cs']:
        read(path)
    catalog = json.loads((ROOT / 'tools/base-spire-dev/fixtures/source-catalogs.json').read_text(encoding='utf-8-sig'))['domains']['potions']
    ids = {row['name']: row['id'] for row in catalog['entries']}
    names = re.findall(r'ModelDb.Potion<(\w+)>\(\)', read('Timeline/Epochs/Ironclad4Epoch.cs'))
    names += re.findall(r'ModelDb.Potion<(\w+)>\(\)', read('Models/PotionPools/SharedPotionPool.cs'))
    assert len(names) == len(set(names)) == 48
    supported = {'BloodPotion', 'FirePotion', 'BlockPotion', 'EnergyPotion', 'SwiftPotion', 'FruitJuice'}
    potions = []
    for name in names:
        text = read(f'Models/Potions/{name}.cs')
        potions.append({'id': ids[name], 'name': name,
                        'rarity': re.search(r'PotionRarity\.(\w+)', text).group(1).lower(),
                        'usage': 'anytime' if 'PotionUsage.AnyTime' in text else 'combat',
                        'target': 'enemy' if name == 'FirePotion' else 'self',
                        'effect_supported': name in supported})
    cards = []
    for name in re.findall(r'ModelDb.Card<(\w+)>\(\)', read('Models/CardPools/IroncladCardPool.cs')):
        text = read(f'Models/Cards/{name}.cs')
        rarity = re.search(r'CardRarity\.(\w+)', text).group(1).lower()
        if 'CardMultiplayerConstraint.MultiplayerOnly' in text or rarity not in ('common', 'uncommon', 'rare'):
            continue
        cards.append({'id': 'base-spire:' + re.sub(r'(?<!^)(?=[A-Z])', '-', name).lower(), 'rarity': rarity})
    assert len(cards) == 80
    vectors = []
    for seed in ['20261018', '777', '1234', '99', 'ABC', 'abc', 'BASE-DSL-STDIO-00', '斩杀尖塔2', 'A😀B', '']:
        rng = Rng(stream_seed(deterministic_hash_code(seed), 'Rewards'))
        offset, odds = f32(-0.05), f32(0.4)
        offers = []
        for visit in range(1, 4):
            calls = []

            def floating(label):
                before = rng.counter
                value = next_float(rng)
                calls.append({'label': label, 'operation': 'sampleFloat32', 'before': before,
                              'after': rng.counter, 'result': bits(value)})
                return value

            def integer(label, low, high, candidates=None):
                before = rng.counter
                value = rng.next_int_range(low, high)
                record = {'label': label, 'operation': 'sampleInt' if candidates is None else 'samplePick',
                          'before': before, 'after': rng.counter, 'min': low, 'max': high,
                          'result': value if candidates is None else candidates[value]['id']}
                if candidates is not None:
                    record['candidates'] = [c['id'] for c in candidates]
                calls.append(record)
                return value

            dropped = floating('potion-drop') < odds
            odds = f32(odds - f32(0.1)) if dropped else f32(odds + f32(0.1))
            gold = integer('gold', 10, 21)
            potion = None
            if dropped:
                roll = floating('potion-rarity')
                rarity = 'rare' if roll <= f32(0.1) else 'uncommon' if roll <= f32(0.35) else 'common'
                candidates = [p for p in potions if p['rarity'] == rarity]
                potion = candidates[integer('potion-pick', 0, len(candidates), candidates)]['id']
            available = list(cards)
            selected = []
            for index in range(3):
                roll = floating(f'card-{index}-rarity')
                rare = f32(f32(0.03) + offset)
                uncommon = f32(f32(0.37) + rare)
                rarity = 'rare' if roll < rare else 'uncommon' if roll < uncommon else 'common'
                offset = f32(-0.05) if rarity == 'rare' else min(f32(offset + f32(0.01)), f32(0.4))
                eligible = [c for c in available if c['rarity'] == rarity]
                # These complete pools cannot exhaust a rarity within three selections.
                assert eligible
                chosen = eligible[integer(f'card-{index}-pick', 0, len(eligible), eligible)]
                available.remove(chosen)
                selected.append({'id': chosen['id'], 'upgraded': floating(f'card-{index}-upgrade') <= 0.0})
            offers.append({'visit': visit, 'gold': gold, 'potion': potion, 'cards': selected,
                           'odds_bits': bits(odds), 'offset_bits': bits(offset), 'counter': rng.counter,
                           'calls': calls})
        vectors.append({'seed': seed, 'offers': offers})
    return {'schema': 'base-spire/normal-reward-vectors@1', 'source_sha256': hashes,
            'scope': 'all unlocked Ironclad act-one A0 Monster, no reward modifiers; controlled visits',
            'potions': potions, 'vectors': vectors}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    args.output.write_text(json.dumps(generate(), ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
