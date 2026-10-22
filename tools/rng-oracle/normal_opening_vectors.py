"""Render opening DSL from independently exported, locked native source metadata."""
import argparse
import json
from pathlib import Path

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
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--input', type=Path, required=True)
    parser.add_argument('--write-definitions', type=Path, required=True)
    parser.add_argument('--write-neow-pool', type=Path, required=True)
    args = parser.parse_args()
    data = json.loads(args.input.read_text(encoding='utf-8'))
    write_definitions(data, args.write_definitions)
    write_neow_pool(data, args.write_neow_pool)
