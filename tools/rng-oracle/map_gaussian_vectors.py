"""Source-only Gaussian room-count vectors; no Engine or production DSL imports."""
import argparse
import hashlib
import json
import math
import subprocess
import tempfile
from pathlib import Path

from vanilla_rng_oracle import Rng, deterministic_hash_code, stream_seed

ROOT = Path(__file__).resolve().parents[2]


def generate(source_root):
    paths = ['Core/Models/Acts/Overgrowth.cs', 'Core/Map/MapPointTypeCounts.cs',
             'Core/Map/StandardActMap.cs', 'Core/Random/Rng.cs', 'Core/Random/MegaRandom.cs']
    hashes = {'sts2/src/' + path: hashlib.sha256((source_root / path).read_bytes()).hexdigest()
              for path in paths}
    vectors = []
    for seed in ['20261018', '777', '1234', '99', 'ABC', 'abc', 'BASE-DSL-STDIO-00', '斩杀尖塔2', 'A😀B', '']:
        rng = Rng(stream_seed(deterministic_hash_code(seed), 'act_1_map'))
        calls = []
        for mean, deviation, low, high in [(7, 1, 6, 7), (12, 1, 10, 14)] * 4:
            attempts = []
            before = rng.counter
            while True:
                # Rng.NextDouble increments the same raw-word counter once.
                left = rng.random.next_double()
                right = rng.random.next_double()
                rng.counter += 2
                normal = math.sqrt(-2.0 * math.log(1.0 - left)) * math.sin(math.pi * 2.0 * (1.0 - right))
                value = round(mean + deviation * normal)
                attempts.append({'left_payload': int(left * (1 << 53)),
                                 'right_payload': int(right * (1 << 53)), 'rounded': value})
                if low <= value <= high:
                    break
            calls.append({'mean': mean, 'deviation': deviation, 'min': low, 'max': high,
                          'before': before, 'after': rng.counter, 'result': value, 'attempts': attempts})
        vectors.append({'seed': seed, 'stream_seed': rng.seed, 'calls': calls})
    return {'format': 'source-map-gaussian-vectors@1', 'source_sha256': hashes, 'vectors': vectors}


def write_map_vectors(source_root, output):
    paths = ['Map/StandardActMap.cs', 'Map/MapPathPruning.cs', 'Map/MapPostProcessing.cs',
             'Map/ActMap.cs', 'Map/MapPoint.cs', 'Map/MapCoord.cs', 'Map/MapPointTypeCounts.cs',
             'Map/MapPointType.cs', 'Random/Rng.cs', 'Random/MegaRandom.cs', 'Extensions/ListExtensions.cs']
    hashes = {'sts2/src/Core/' + path: hashlib.sha256((source_root / 'Core' / path).read_bytes()).hexdigest() for path in paths}
    for path in (ROOT / 'tools/base-migration-baseline/BaseSpire/Maps/Generation').glob('*.cs'):
        hashes[path.relative_to(ROOT).as_posix()] = hashlib.sha256(path.read_text(encoding='utf-8-sig').replace('\r\n', '\n').encode('utf-8')).hexdigest()
    vectors = []
    for seed in ['20261018', '777', '1234', '99', 'ABC', 'abc', 'BASE-DSL-STDIO-00', '斩杀尖塔2', 'A😀B', '']:
        rng = Rng(stream_seed(deterministic_hash_code(seed), 'act_1_map'))
        vectors.append({'seed': seed, 'payloads': [rng.random.next_ulong() >> 11 for _ in range(10000)]})
    with tempfile.TemporaryDirectory(prefix='source-map-vectors-') as directory:
        temporary = Path(directory).resolve()
        assert temporary.parent == Path(tempfile.gettempdir()).resolve()
        input_path = temporary / 'input.json'
        input_path.write_text(json.dumps({'source_sha256': hashes, 'vectors': vectors}, ensure_ascii=False), encoding='utf-8')
        subprocess.run(['dotnet', 'run', '--project', str(ROOT / 'tools/rng-oracle/map-reference'), '-c', 'Release', '--', str(input_path), str(output.resolve())], check=True, cwd=ROOT)


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--source-root', type=Path, default=ROOT / 'sts2/src')
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--map-output', type=Path)
    args = parser.parse_args()
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(generate(args.source_root), ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    if args.map_output:
        write_map_vectors(args.source_root, args.map_output)
