"""Fully unlocked A0 Ironclad opening data, independently derived from source."""
import argparse
import hashlib
import json
from pathlib import Path
import re

from vanilla_rng_oracle import Rng, deterministic_hash_code, stream_seed

ROOT = Path(__file__).resolve().parents[2]
SEEDS = ['20261018', '777', '1234', '99', 'ABC', 'abc', 'BASE-DSL-STDIO-00', '斩杀尖塔2', 'A😀B', '']


def generate(source_root):
    hashes = {}

    def read(relative):
        path = source_root / relative
        hashes['sts2/src/Core/' + relative] = hashlib.sha256(path.read_bytes()).hexdigest()
        return path.read_text(encoding='utf-8-sig')

    fixture = json.loads((ROOT / 'tools/base-spire-dev/fixtures/source-catalogs.json').read_text(encoding='utf-8-sig'))
    ids = {domain: {r['name']: r['id'] for r in value['entries']} for domain, value in fixture['domains'].items()}
    def identifier(domain, name):
        return ids[domain].get(name, 'base-spire:' + re.sub(r'(?<!^)(?=[A-Z])', '-', name).lower())

    for name in ['Runs/RunManager.cs', 'Runs/RunState.cs', 'Runs/RelicGrabBag.cs', 'Models/RelicPoolModel.cs',
                 'Models/ActModel.cs', 'Models/EncounterModel.cs', 'Models/RelicModel.cs', 'Models/EventModel.cs',
                 'Models/AncientEventModel.cs', 'Unlocks/UnlockState.cs', 'Helpers/GrabBag.cs',
                 'Extensions/ListExtensions.cs', 'Random/Rng.cs', 'Random/MegaRandom.cs', 'Nodes/NGame.cs']:
        read(name)
    rows = []
    def add(category, act, name, domain, **extra):
        row = {'category': category, 'act': act, 'name': name, 'id': identifier(domain, name),
               'room': 'none', 'rarity': 'none', 'weak': False, 'tags': [], **extra}
        rows.append(row)
        return row

    for pool, category in [('SharedRelicPool', 'shared-relic'), ('IroncladRelicPool', 'ironclad-relic')]:
        text = read(f'Models/RelicPools/{pool}.cs')
        text = text[text.index('GenerateAllRelics'):text.index('public override IEnumerable<RelicModel> GetUnlockedRelics')]
        names = re.findall(r'ModelDb.Relic<(\w+)>', text)
        assert len(names) == (118 if pool == 'SharedRelicPool' else 8)
        for name in names:
            text = read(f'Models/Relics/{name}.cs')
            match = re.search(r'RelicRarity Rarity\s*=>\s*RelicRarity\.(\w+)', text)
            assert match, name
            add(category, -1, name, 'relics', rarity=match[1])

    db = read('Models/ModelDb.cs')
    shared_events = re.findall(r'Event<(\w+)>', db[db.index('public static IEnumerable<EventModel> AllSharedEvents'):db.index('public static IEnumerable<AncientEventModel> AllAncients')])
    assert len(shared_events) == 18
    acts = []
    for index, act in enumerate(['Overgrowth', 'Hive', 'Glory']):
        text = read(f'Models/Acts/{act}.cs')
        weak_count = int(re.search(r'NumberOfWeakEncounters\s*=>\s*(\d+)', text)[1])
        rooms = int(re.search(r'BaseNumberOfRooms\s*=>\s*(\d+)', text)[1])
        acts.append({'name': act, 'weak': weak_count, 'rooms': rooms})
        events = text[text.index('public override IEnumerable<EventModel> AllEvents'):text.index('protected override int NumberOfWeakEncounters')]
        for name in re.findall(r'ModelDb.Event<(\w+)>', events) + shared_events:
            add('event', index, name, 'events')
        encounters = text[text.index('public override IEnumerable<EncounterModel> GenerateAllEncounters'):text.index('public override IEnumerable<AncientEventModel> GetUnlockedAncients')]
        for name in re.findall(r'ModelDb.Encounter<(\w+)>', encounters):
            encounter = read(f'Models/Encounters/{name}.cs')
            room = re.search(r'RoomType RoomType\s*=>\s*RoomType\.(\w+)', encounter)
            assert room, name
            add('encounter', index, name, 'encounters', room=room[1], weak=bool(re.search(r'bool IsWeak\s*=>\s*true', encounter)),
                tags=re.findall(r'EncounterTag\.(\w+)', encounter))
        ancients = text[text.index('public override IEnumerable<AncientEventModel> AllAncients'):text.index('public override IEnumerable<EventModel> AllEvents')]
        for name in re.findall(r'ModelDb.AncientEvent<(\w+)>', ancients):
            add('ancient', index, name, 'events')
    shared = db[db.index('public static IEnumerable<AncientEventModel> AllSharedAncients'):db.index('public static IEnumerable<EventModel> AllEvents')]
    for name in re.findall(r'AncientEvent<(\w+)>', shared):
        add('shared-ancient', -1, name, 'events')

    neow = read('Models/Events/Neow.cs')
    positive = re.findall(r'RelicOption<(\w+)>', neow[neow.index('private IEnumerable<EventOption> PositiveOptions'):neow.index('private EventOption LavaRockOption')])
    curse = re.findall(r'RelicOption<(\w+)>', neow[neow.index('private IEnumerable<EventOption> CurseOptions'):neow.index('private List<EventOption> ModifierOptions')])
    for name in dict.fromkeys(re.findall(r'RelicOption<(\w+)>', neow)):
        read(f'Models/Relics/{name}.cs')
    assert len(positive) == 14 and len(curse) == 8
    positive.remove('MassiveScroll')  # IsAllowed requires multiplayer.
    exclusions = {'CursedPearl': 'GoldenPearl', 'HeftyTablet': 'ArcaneScroll', 'LeafyPoultice': 'NewLeaf', 'PrecariousShears': 'PreciseScissors'}
    vectors = []
    for seed in SEEDS + ['20261021']:
        rng = Rng(stream_seed(deterministic_hash_code(seed), 'up_front'))
        def shuffle(items):
            result = list(items)
            for i in range(len(result) - 1, 0, -1):
                j = rng.next_int(i + 1)
                result[i], result[j] = result[j], result[i]
            return result
        bags = []
        for category in ['shared-bag', 'player-bag']:
            relics = [r for r in rows if r['category'] == 'shared-relic' or (category == 'player-bag' and r['category'] == 'ironclad-relic')]
            if category == 'player-bag':
                relics = [r for r in relics if r['rarity'] in ['Common', 'Uncommon', 'Rare', 'Shop']]
            groups = {}
            for relic in relics:
                groups.setdefault(relic['rarity'], []).append(relic)
            for rarity, values in groups.items():
                bags.append({'category': category, 'rarity': rarity, 'ids': [v['id'] for v in shuffle(values)]})
        ancients = shuffle([r for r in rows if r['category'] == 'shared-ancient'])
        subsets = {}
        for act in [1, 2]:
            count = rng.next_int(len(ancients) + 1)
            subsets[act], ancients = ancients[:count], ancients[count:]
        plans = []
        rejections = 0
        for act, config in enumerate(acts):
            events = shuffle([r for r in rows if r['category'] == 'event' and r['act'] == act])
            plans.extend({'act': act, 'room': 'event', 'position': i, 'id': event['id']} for i, event in enumerate(events))
            for room, configurations in [('Monster', [(True, config['weak']), (False, config['rooms'] - config['weak'])]), ('Elite', [(False, 15)])]:
                last = None
                selected = []
                for weak, count in configurations:
                    bag = []
                    pool = [r for r in rows if r['category'] == 'encounter' and r['act'] == act and r['room'] == room and r['weak'] == weak]
                    for _ in range(count):
                        if not bag:
                            bag = list(pool)
                        def allowed(row):
                            return last is None or (row['id'] != last['id'] and not set(row['tags']).intersection(last['tags']))
                        predicate = any(allowed(row) for row in bag)
                        while True:
                            picked = rng.next_int(len(bag))
                            if not predicate or allowed(bag[picked]):
                                break
                            rejections += 1
                        last = bag.pop(picked)
                        selected.append(last)
                plans.extend({'act': act, 'room': room, 'position': i, 'id': row['id']} for i, row in enumerate(selected))
            boss_pool = [r for r in rows if r['category'] == 'encounter' and r['act'] == act and r['room'] == 'Boss']
            boss = boss_pool[rng.next_int(len(boss_pool))]
            plans.append({'act': act, 'room': 'Boss', 'position': 0, 'id': boss['id']})
            ancient_pool = [r for r in rows if r['category'] == 'ancient' and r['act'] == act] + subsets.get(act, [])
            ancient = ancient_pool[rng.next_int(len(ancient_pool))]
            plans.append({'act': act, 'room': 'ancient', 'position': 0, 'id': ancient['id']})
        event_rng = Rng((deterministic_hash_code(seed) + deterministic_hash_code('NEOW')) & 0xffffffff)
        cursed = curse[event_rng.next_int(len(curse))]
        choices = [r for r in positive if r != exclusions.get(cursed)]
        def boolean():
            event_rng.counter += 1
            return (event_rng.random.next_ulong() >> 63) == 0
        if cursed != 'LargeCapsule':
            choices.append('LavaRock' if boolean() else 'SmallCapsule')
        choices.append('NutritiousOyster' if boolean() else 'StoneHumidifier')
        choices.append('NeowsTalisman' if boolean() else 'Pomander')
        for i in range(len(choices) - 1, 0, -1):
            j = event_rng.next_int(i + 1)
            choices[i], choices[j] = choices[j], choices[i]
        offers = choices[:2] + [cursed]
        niche = Rng(stream_seed(deterministic_hash_code(seed), 'niche'))
        fuzzy_hp = niche.next_int_range(55, 58)
        vectors.append({'seed': seed, 'fuzzy_hp': fuzzy_hp, 'up_front_draws': rng.counter, 'rejections': rejections, 'bags': bags, 'plans': plans,
                        'neow_draws': event_rng.counter, 'neow_options': [identifier('relics', name) for name in offers]})
    return {'format': 'source-normal-opening-vectors@1', 'profile': 'ironclad-a0-fully-unlocked-singleplayer',
            'source_sha256': hashes, 'definitions': rows, 'neow_pool': {'positive': [identifier('relics', r) for r in positive], 'curse': [identifier('relics', r) for r in curse]},
            'vectors': vectors[:len(SEEDS)], 'regression_vectors': vectors[len(SEEDS):]}


def write_definitions(data, path):
    lines = ['using namespace base-spire;', 'dsl_version = "core/0.3"', '', '# Fully unlocked A0 source pools; consumed by actual opening queues and bags.',
             'procedure BuildSourceOpeningDefinitions {', '  parameters = { run = Ref(Run) }', '  locals = { entry = Binding }', '  body = {']
    for row in data['definitions']:
        lines.append('    call = { procedure = AddOpeningDefinition args = { run = run category = "' + row['category'] + '" act = ' + str(row['act']) + ' definition = "' + row['id'] + '" room = "' + row['room'] + '" rarity = "' + row['rarity'] + '" weak = ' + str(row['weak']).lower() + ' } into = entry }')
        for tag in row['tags']:
            lines.append('    call = { procedure = AddOpeningTag args = { run = run entry = entry name = "' + tag + '" } }')
    lines.extend(['  }', '}'])
    path.write_text('\n'.join(lines) + '\n', encoding='utf-8')


def write_neow_pool(data, path):
    lines = ['using namespace base-spire;', 'dsl_version = "core/0.3"', '', '# Fully unlocked single-player: MassiveScroll requires multiplayer.',
             'procedure BuildSourceNeowPool {', '  parameters = { visit = Ref(NeowVisit) }', '  body = {']
    for group in ['curse', 'positive']:
        for index, identifier in enumerate(data['neow_pool'][group]):
            lines.append('    call = { procedure = AddNeowPoolEntry args = { visit = visit definition = "' + identifier + '" group = "' + group + '" position = ' + str(index) + ' } }')
    lines.extend(['  }', '}'])
    path.write_text('\n'.join(lines) + '\n', encoding='utf-8')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--source-root', type=Path, default=ROOT / 'sts2/src/Core')
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--write-definitions', type=Path)
    parser.add_argument('--write-neow-pool', type=Path)
    args = parser.parse_args()
    data = generate(args.source_root)
    args.output.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    if args.write_definitions:
        write_definitions(data, args.write_definitions)

    if args.write_neow_pool:
        write_neow_pool(data, args.write_neow_pool)
