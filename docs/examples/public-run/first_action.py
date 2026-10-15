"""Start a public Run and submit current candidates. Not a game strategy."""
import argparse
import json
import pathlib
import sys

ROOT = pathlib.Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / "net-the-spire" / "client"))
from net_client import NetClient, NetClientError, OutcomeUnknown
from public_snapshot import action_name, candidates, field, find_entity

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--host-dll", type=pathlib.Path, required=True)
parser.add_argument("--seed", default="TUTORIAL-RUN-01")
parser.add_argument("--max-actions", type=int, default=3)
args = parser.parse_args()
client = NetClient(["dotnet", str(args.host_dll.resolve()), "--scenario", "formal-dev", "--scope", "dev-map"])
exit_code = 0


def result(response):
    if not response["ok"]:
        raise NetClientError(json.dumps(response["error"], ensure_ascii=False))
    return response["result"]


try:
    description = result(client.describe())
    profile = description["capabilities"]["scenario_profiles"][0]["id"]
    started = result(client.start(description["identity"], profile, seed=args.seed))
    session_id = started["session_id"]
    snapshot = started["snapshot"]
    print(json.dumps({"seed": started["seed"], "session_id": session_id}, ensure_ascii=False))
    for step in range(args.max_actions + 1):
        print(json.dumps({"step": step, "observation_id": snapshot["observation_id"],
                          "status": snapshot["status"], "terminal_result": snapshot.get("terminal_result")},
                         ensure_ascii=False))
        if snapshot["status"] == "terminal":
            break
        if field(find_entity(snapshot, "Run"), "ActCompleted") is True:
            print("act_completed: development frontier; this is not whole-run victory")
            exit_code = 3
            break
        if step == args.max_actions:
            print("step_limit: tutorial stopped; no whole-run conclusion")
            exit_code = 3
            break
        # Pending decisions are also published as current candidates.
        # Choosing the first candidate demonstrates transport, not how to win.
        offered = candidates(snapshot)
        if not offered:
            raise NetClientError("No current public candidate; inspect snapshot status")
        chosen = offered[0]
        print("action:", action_name(chosen))
        snapshot = result(client.step(session_id, snapshot["observation_id"], chosen["candidate_id"]))["snapshot"]
    result(client.close_session(session_id))
except OutcomeUnknown as error:
    print("uncertain:", error, file=sys.stderr)
    exit_code = 4
except NetClientError as error:
    print("stopped:", error, file=sys.stderr)
    exit_code = 4
finally:
    client.shutdown()

sys.exit(exit_code)
