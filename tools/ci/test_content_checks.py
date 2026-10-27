"""Focused local checks must not silently run or claim broad regression."""
import argparse
import io
import json
from pathlib import Path
import subprocess
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch

import check


class Console(io.StringIO):
    def reconfigure(self, **kwargs):
        pass


class ContentChecksTests(unittest.TestCase):
    def test_focus_preserves_prefix_and_rejects_invalid_selection(self):
        self.assertEqual(check.content_arguments("relic:RL pickup "),
                         ["check", "--relic-content-only", "--filter", "RL pickup "])
        self.assertEqual(check.content_arguments("enemy"), ["check", "--enemy-moves-only"])
        for focus in ["", "base", "relic:", "card:  ", "enemy:foo", "model:foo"]:
            with self.subTest(focus=focus), self.assertRaises(argparse.ArgumentTypeError):
                check.content_arguments(focus)

    def invoke(self, arguments, codes):
        output = Console()
        with tempfile.TemporaryDirectory() as folder:
            destination = Path(folder) / "result"
            processes = [SimpleNamespace(stdout=io.StringIO("detail\n" * 40 + "last detail\n"),
                                         wait=lambda code=code: code) for code in codes]
            with patch.object(check.sys, "argv", ["check.py", *arguments, "--output", str(destination)]), \
                    patch.object(check.sys, "stdout", output), patch.object(check.sys, "stderr", Console()), \
                    patch.object(check.subprocess, "run", return_value=subprocess.CompletedProcess([], 1)), \
                    patch.object(check.subprocess, "Popen", side_effect=processes) as launch, \
                    patch.dict(check.os.environ, {"GITHUB_STEP_SUMMARY": ""}):
                try:
                    code = check.main()
                except SystemExit as error:
                    code = error.code
            result_path = destination / "result.json"
            result = json.loads(result_path.read_text(encoding="utf-8")) if result_path.exists() else None
            logs = {p.name: p.read_text(encoding="utf-8") for p in destination.glob("*.log")}
            return code, result, logs, [call.args[0] for call in launch.call_args_list], output.getvalue()

    def test_requires_explicit_focus_without_broad_suites(self):
        for arguments in [["content"], ["content", "base", "--focus", "relic"],
                          ["base", "--focus", "relic"],
                          ["content", "--focus", "relic", "--focus", "relic"]]:
            with self.subTest(arguments=arguments):
                code, result, _, commands, _ = self.invoke(arguments, [])
                self.assertEqual(code, 2)
                self.assertIsNone(result)
                self.assertEqual(commands, [])

    def test_batch_builds_once_and_only_runs_selected_groups(self):
        code, result, logs, commands, output = self.invoke(
            ["content", "--focus", "relic:RL pickup ", "--focus", "enemy"], [0, 0, 0])
        self.assertEqual(code, 0)
        self.assertEqual([command[1] for command in commands], ["build", "run", "run"])
        self.assertEqual(commands[1][-4:], ["check", "--relic-content-only", "--filter", "RL pickup "])
        self.assertEqual(commands[2][-2:], ["check", "--enemy-moves-only"])
        self.assertTrue(all("--no-build" in command for command in commands[1:]))
        self.assertFalse(result["full_coverage_verified"])
        self.assertFalse(result["independent_verified"])
        self.assertEqual(len(logs), 3)
        self.assertIn("last detail", logs["02.log"])
        self.assertNotIn("last detail", output)

    def test_build_failure_stops_behavior_checks(self):
        code, result, _, commands, output = self.invoke(["content", "--focus", "relic"], [1])
        self.assertEqual(code, 1)
        self.assertEqual(result["exit_code"], 1)
        self.assertEqual(len(commands), 1)
        self.assertIn("last detail", output)

    def test_zero_match_or_behavior_failure_is_not_success(self):
        code, result, logs, commands, output = self.invoke(
            ["content", "--focus", "card:missing", "--focus", "enemy"], [0, 2])
        self.assertEqual(code, 2)
        self.assertEqual(result["exit_code"], 2)
        self.assertEqual(len(commands), 2)
        self.assertEqual(output.count("detail\n"), 30)
        self.assertEqual(logs["02.log"].count("detail\n"), 41)


if __name__ == "__main__":
    unittest.main()
