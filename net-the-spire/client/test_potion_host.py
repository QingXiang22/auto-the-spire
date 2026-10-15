"""Public potion boundary checks; no builds, hidden state, or RNG access.

--only catalog|normal|full|skip|target (default all). Normal scenarios replay
an identity-bound published prefix, matching every instruction in live candidates.
Target is explicitly the existing legacy author-text controlled scenario (0.1),
not evidence of a target potion dropping in formal 0.2 gameplay.
"""
from __future__ import annotations
import argparse
import hashlib
import json
import os
import pathlib
import sys
import traceback
import uuid

HERE = pathlib.Path(__file__).resolve().parent
ROOT = HERE.parents[1]
sys.path.insert(0, str(ROOT / 'tools/contract-check'))
from net_client import NetClient
import jsonschema_min

AUDIT = ROOT / '.audit/dsh-potion-public-2026-10-11'
TRACE = ROOT / '.audit/owner-potion-flow-2026-10-11/public-replay-author-final/public-trajectory.jsonl'
GROUPS = ('catalog', 'normal', 'full', 'skip', 'target')


def fields(entity):
    return {f['field_id'].rsplit('.', 1)[-1]: f.get('value') for f in entity.get('fields', [])}


def entities(s):
    return {e['instance_id']: e for e in s['entities']}


def potion_fields(s):
    return {k: fields(e) for k, e in entities(s).items() if e['kind_id'].endswith(':Potion')}


def members(s, name='PotionBelt'):
    seq = [q['elements'] for q in s.get('sequences', []) if q['type_id'].endswith(':' + name)]
    if seq:
        return seq[0]
    if name == 'PotionBelt':
        groups = [q for q in s.get('groups', []) if q['type_id'].endswith(':potion_belt')]
        if groups:
            return [m['entity_ref'] for m in groups[0]['members']]
    return []


def candidates(s, action):
    return [a for a in s['legal_actions'] if a['action_type'].endswith(':' + action)]


def energy(s):
    return next(fields(e)['Energy'] for e in s['entities'] if e['kind_id'].endswith(':Player'))


class EvidenceClient(NetClient):
    def __init__(self, command, *, recorder, schema):
        self.recorder = recorder
        self.schema = schema
        super().__init__(command)

    def _write_frame(self, frame):
        text = json.dumps(frame, ensure_ascii=False, separators=(',', ':'))
        self.recorder('request', text)
        super()._write_frame(frame)

    def _read_line(self, timeout):
        text = super()._read_line(timeout)
        if text is not None:
            self.recorder('response', text)
        return text


class Case:
    def __init__(self, group, dll, output, trace):
        self.group, self.dll, self.output, self.trace = group, dll, output, trace
        self.n = 0
        self.frames = 0
        self.steps = 0
        self.branches = []
        self.meta = {'group': group}
        self.client = None

    def check(self, condition, label):
        self.n += 1
        if not condition:
            raise AssertionError(label)

    def record(self, direction, text):
        self.frames += 1
        row = {'group': self.group, 'frame_number': self.frames,
               'direction': direction, 'raw_text': text}
        self.output.write(json.dumps(row, ensure_ascii=False) + '\n')
        self.output.flush()
        self.check(not jsonschema_min.validate(json.loads(text), self.schema),
                   f'{direction} schema violation: {jsonschema_min.validate(json.loads(text), self.schema)}')
        self.check('MapDraws' not in text, 'MapDraws leaked in ordinary frame')

    def open(self, author=False):
        version = '0.1' if author else '0.2'
        self.schema = json.loads((ROOT / f'docs/contracts/net/{version}/wire.schema.json').read_text(encoding='utf8'))
        args = ['--author-content'] if author else ['--scenario', 'formal-dev', '--scope', 'dev-map']
        self.client = EvidenceClient(['dotnet', str(self.dll)] + args,
                                     recorder=self.record, schema=self.schema)
        d = self.result(self.client.describe())
        self.identity = d['identity']
        self.catalog = self.result(self.client.get_catalog(self.identity['catalog_id']))
        seed = '1' if author else 'ACT1-BASIC-10'
        profile = d['capabilities']['scenario_profiles'][0]['id']
        r = self.result(self.client.start(self.identity, profile, seed=seed))
        self.sid, self.s = r['session_id'], r['snapshot']
        self.check(self.observe() == self.s, 'initial observe differs from start snapshot')
        self.meta.update(identity=self.identity, seed=r.get('seed', seed), profile=profile,
                         path='controlled author text' if author else 'normal formal public prefix',
                         schema_version=version)

    def result(self, r):
        self.check(r.get('ok') is True, f'Host error: {r}')
        return r['result']

    def observe(self):
        return self.result(self.client.observe(self.sid))['snapshot']

    def step(self, a, request_id=None):
        r = self.client.step(self.sid, self.s['observation_id'], a['candidate_id'], request_id=request_id)
        self.s = self.result(r)['snapshot']
        self.steps += 1
        return r

    def action(self, name, instance=None):
        matches = candidates(self.s, name)
        if instance is not None:
            matches = [a for a in matches if instance in fields(a).values()]
        self.check(len(matches) == 1, f'expected one {name} for {instance}, got {len(matches)}')
        return matches[0]

    def reject(self, observation, candidate, code, label):
        before = self.observe()
        r = self.client.step(self.sid, observation, candidate)
        self.check(r.get('ok') is False and r.get('error', {}).get('code') == code,
                   f'{label}: expected {code}, got {r}')
        self.check(self.observe() == before, f'{label}: rejection changed public snapshot')
        self.branches.append({'branch': label, 'through_frame': self.frames})

    def exercise(self, a, label, pending=False):
        before = self.s
        request_id = uuid.uuid4().hex
        response = self.step(a, request_id)
        after = self.s
        replay = self.client.step(self.sid, before['observation_id'], a['candidate_id'], request_id=request_id)
        self.check(replay == response, label + ': replay response changed')
        self.check(self.observe() == after, label + ': replay caused extra mutation')
        stale_code = ('stale_observation' if before['observation_id'] != after['observation_id']
                      else 'illegal_candidate')
        self.reject(before['observation_id'], a['candidate_id'], stale_code,
                    label + (' stale' if stale_code == 'stale_observation' else ' replaced candidate'))
        # Candidate tokens may recur if the action remains legal (pending target);
        # only assert consumption once the candidate actually disappears.
        if not pending:
            self.check(a['candidate_id'] not in {x['candidate_id'] for x in after['legal_actions']},
                       label + ': consumed candidate still offered')
            self.reject(after['observation_id'], a['candidate_id'], 'illegal_candidate', label + ' consumed')
        self.branches.append({'branch': label + ' deduplicated', 'through_frame': self.frames})
        return before, after

    def prefix(self, count):
        rows = [json.loads(line) for line in self.trace.read_text(encoding='utf8').splitlines()]
        recorded_identity = rows[0]['describe']['result']['identity']
        self.check(self.identity == recorded_identity,
                   'published prefix identity mismatch; provide a newly published trace for this build')
        self.meta['prefix_source'] = str(self.trace)
        self.meta['prefix_identity'] = recorded_identity
        for row in rows[1:count + 1]:
            ins = row['instruction']
            matches = [a for a in self.s['legal_actions']
                       if a['action_type'] == ins['action'] and a['fields'] == ins['fields']]
            self.check(len(matches) == 1, f'public prefix action absent at {self.steps + 1}: {ins}')
            self.step(matches[0])
        self.meta['prefix_steps'] = count

    def stable_survivors(self, before, after, removed):
        bp, ap = potion_fields(before), potion_fields(after)
        survivors = [x for x in members(before) if x != removed]
        self.check(all(x in members(after) and bp[x]['SlotIndex'] == ap[x]['SlotIndex'] for x in survivors),
                   'other potion instance/slot changed')
        self.check(removed not in entities(after), 'consumed/discarded bottle entity still exists')

    def take(self):
        before = self.s
        offered = members(before, 'RewardPotions')
        self.check(len(offered) == 1, 'missing actual offered bottle')
        held = members(before)
        used = {int(potion_fields(before)[x]['SlotIndex']) for x in held}
        capacity = next(q['max_count'] for q in self.catalog['declarations']['sequence_types']
                        if q['id'].endswith(':PotionBelt'))
        first_empty = next(x for x in range(capacity) if x not in used)
        _, after = self.exercise(self.action('TakeRewardPotion'), 'take')
        self.check(offered[0] in members(after), 'take failed to retain offered instance')
        self.check(int(potion_fields(after)[offered[0]]['SlotIndex']) == first_empty, 'take did not use first empty slot')
        self.check(not members(after, 'RewardPotions') and not candidates(after, 'TakeRewardPotion'), 'take did not close potion reward')
        self.check(all(potion_fields(before)[x]['SlotIndex'] == potion_fields(after)[x]['SlotIndex'] for x in held), 'take changed other slots')

    def run(self):
        self.open(author=self.group == 'target')
        if self.group == 'catalog':
            declarations = self.catalog['declarations']
            ids = {x['id'] for values in declarations.values() for x in values}
            for suffix in ('Potion', 'PotionBelt', 'RewardPotions', 'UsePotion', 'DiscardPotion', 'TakeRewardPotion',
                           'Potion.SlotIndex', 'Potion.UseSupported', 'Potion.UnsupportedUseReason'):
                self.check(any(x.endswith(':' + suffix) for x in ids), 'missing declaration ' + suffix)
            contents = [x for x in declarations.get('content_types', [])
                        if any(k.endswith(':Potion') for k in x.get('kind_ids', []))]
            self.check(bool(contents), 'zero potion content entries')
            self.meta['actual_potion_catalog_count'] = len(contents)
            self.meta['use_program_baseline'] = {'count': 11, 'source': '.audit/owner-potion-flow-2026-10-11/potion-checks-final.log', 'kind': 'owner baseline, not runtime count claim'}
            self.branches.append({'branch': 'runtime declarations', 'through_frame': self.frames})
        elif self.group == 'normal':
            self.prefix(27)
            self.take()
            self.prefix_from(28, 61)
            supported = [x for x in members(self.s) if potion_fields(self.s)[x]['UseSupported']]
            unsupported = [x for x in members(self.s) if not potion_fields(self.s)[x]['UseSupported']]
            self.check(bool(supported) and bool(unsupported), 'missing real supported/unsupported held bottles')
            self.check(not any(unsupported[0] in fields(a).values() for a in candidates(self.s, 'UsePotion')), 'unsupported bottle exposes use')
            self.check(bool(potion_fields(self.s)[unsupported[0]].get('UnsupportedUseReason')), 'unsupported reason absent')
            self.reject(self.s['observation_id'], 't03-unsupported-use-not-offered', 'illegal_candidate', 'unsupported noncandidate use')
            before, after = self.exercise(self.action('UsePotion', supported[0]), 'use')
            self.stable_survivors(before, after, supported[0])
            self.check(energy(before) == energy(after), 'self use paid energy')
            before, after = self.exercise(self.action('DiscardPotion', unsupported[0]), 'unsupported discard')
            self.stable_survivors(before, after, unsupported[0])
        elif self.group == 'full':
            self.prefix(206)
            capacity = next(q['max_count'] for q in self.catalog['declarations']['sequence_types'] if q['id'].endswith(':PotionBelt'))
            self.check(len(members(self.s)) == capacity, 'normal prefix did not reach full belt')
            reward = members(self.s, 'RewardPotions')
            self.check(len(reward) == 1 and not candidates(self.s, 'TakeRewardPotion'), 'full belt lacks retained reward or exposes take')
            self.reject(self.s['observation_id'], 't03-full-take-not-offered', 'illegal_candidate', 'full take not legal')
            self.check(members(self.s, 'RewardPotions') == reward, 'full rejection removed reward')
            bottle = members(self.s)[0]
            before, after = self.exercise(self.action('DiscardPotion', bottle), 'full discard')
            self.stable_survivors(before, after, bottle)
            self.check(members(after, 'RewardPotions') == reward, 'discard removed reward')
            self.take()
        elif self.group == 'skip':
            self.prefix(27)
            belt = members(self.s)
            _, after = self.exercise(self.action('SkipRewardPotion'), 'skip')
            self.check(not members(after, 'RewardPotions') and not candidates(after, 'TakeRewardPotion') and not candidates(after, 'SkipRewardPotion'), 'skip did not close reward')
            self.check(members(after) == belt, 'skip changed held belt')
        else:
            targets = [x for x in members(self.s) if potion_fields(self.s)[x]['UseSupported'] and str(potion_fields(self.s)[x]['Target']).endswith(':enemy')]
            self.check(bool(targets), 'controlled author scenario has no real enemy-target bottle')
            bottle = targets[0]
            before, waiting = self.exercise(self.action('UsePotion', bottle), 'target use pending', pending=True)
            self.check(bottle in members(waiting) and bottle in entities(waiting), 'pending decision consumed bottle')
            self.check(potion_fields(before) == potion_fields(waiting), 'pending changed potion fields')
            self.check(energy(before) == energy(waiting), 'pending use paid energy')
            answers = candidates(waiting, 'net.answer_decision')
            self.check(bool(answers), 'target use did not publish decision answers')
            _, after = self.exercise(answers[0], 'target answer')
            self.stable_survivors(before, after, bottle)
            self.check(energy(before) == energy(after), 'answered target use paid energy')
            powers_before = {k: e for k, e in entities(before).items() if e['kind_id'].endswith(':Power')}
            powers_after = {k: e for k, e in entities(after).items() if e['kind_id'].endswith(':Power')}
            self.check(powers_after != powers_before, 'target answer had no public power effect')
            self.reject(after['observation_id'], self.action_id(before, 'UsePotion', bottle), 'illegal_candidate', 'target use consumed')

    @staticmethod
    def action_id(s, name, instance):
        return next(a['candidate_id'] for a in candidates(s, name) if instance in fields(a).values())

    def prefix_from(self, start, end):
        rows = [json.loads(line) for line in self.trace.read_text(encoding='utf8').splitlines()]
        for row in rows[start + 1:end + 1]:
            ins = row['instruction']
            matches = [a for a in self.s['legal_actions'] if a['action_type'] == ins['action'] and a['fields'] == ins['fields']]
            self.check(len(matches) == 1, f'public continuation action absent: {ins}')
            self.step(matches[0])


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument('--only', choices=GROUPS)
    ap.add_argument('--trace', type=pathlib.Path, default=TRACE,
                    help='published identity-bound trajectory for the selected Host')
    ap.add_argument('--log', type=pathlib.Path, default=AUDIT / 'frames.jsonl')
    ap.add_argument('--summary', type=pathlib.Path, default=AUDIT / 'summary.json')
    args = ap.parse_args()
    explicit = os.environ.get('NET_THE_SPIRE_HOST_DLL')
    if not explicit or not pathlib.Path(explicit).is_file():
        ap.error('NET_THE_SPIRE_HOST_DLL must select an existing DLL; no build fallback')
    dll = pathlib.Path(explicit)
    args.log.parent.mkdir(parents=True, exist_ok=True)
    results = []
    with args.log.open('w', encoding='utf8') as output:
        for group in ([args.only] if args.only else GROUPS):
            case = Case(group, dll, output, args.trace)
            try:
                case.run()
                case.check(bool(case.branches), 'zero executed branches')
                status, error = 'passed', None
            except Exception as exc:
                status, error = 'failed', f'{type(exc).__name__}: {exc}'
                traceback.print_exc()
            finally:
                if case.client is not None:
                    rc = case.client.shutdown()
                    if rc != 0:
                        status, error = 'failed', f'Host shutdown rc={rc}; {error}'
                else:
                    rc = None
            result = dict(case.meta, status=status, error=error, assertions=case.n,
                          frames=case.frames, steps=case.steps, branches=case.branches, host_rc=rc)
            results.append(result)
            print(json.dumps(result, ensure_ascii=False), flush=True)
    summary = {'host_dll': str(dll), 'host_sha256': hashlib.sha256(dll.read_bytes()).hexdigest(),
               'groups': results, 'rc': int(any(r['status'] != 'passed' for r in results)),
               'uncovered': ['formal normally dropped enemy-target bottle; target only controlled author 0.1',
                             'remaining potion effects; independent vanilla comparison'],
               'frames_file': str(args.log)}
    args.summary.parent.mkdir(parents=True, exist_ok=True)
    args.summary.write_text(json.dumps(summary, ensure_ascii=False, indent=2) + '\n', encoding='utf8')
    return summary['rc']


if __name__ == '__main__':
    raise SystemExit(main())
