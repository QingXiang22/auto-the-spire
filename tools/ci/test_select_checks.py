import unittest

from select_checks import SUITES, affected, matching_run


class SelectionTests(unittest.TestCase):
    def test_changed_surfaces(self):
        for paths, expected in [
            (["docs/understand/architecture.md", "AGENTS.md"], ["docs"]),
            ([".vale.ini", "tools/doc-check/check_docs.py"], ["docs"]),
            (["docs/specs/net/0.2/wire.schema.json"], SUITES),
            (["core-the-spire/Core.TheSpire/Engine/Session.cs"], SUITES),
            (["mod-the-spire/base-spire/cards/rules/play.spire"], ["base", "net", "migration", "docs"]),
            (["mod-the-spire/base-spire/package.json"], ["base", "net", "migration", "docs", "package"]),
            (["mod-the-spire/base-spire/enemies/schema.spire"], ["base", "net", "migration", "docs", "enemy-graph"]),
            (["mod-the-spire/base-spire/enemies/shared/twig-slime-m.spire"], ["base", "net", "migration", "docs"]),
            (["tools/base-spire-dev/PackageBoundaryChecks.cs"], ["docs", "package"]),
            (["tools/base-spire-dev/EnemyMoveGraphChecks.cs"], ["docs", "enemy-graph"]),
            (["tools/base-spire-dev/PackageChecks.cs"], ["base", "net", "migration", "docs", "package"]),
            (["net-the-spire/Net.TheSpire/ControlHost.cs"], ["base", "net", "migration", "docs"]),
            (["tools/differential/NormalRunComparison.cs"], ["migration", "docs"]),
            (["tools/ci/check_dsl_migration.py"], ["migration", "docs"]),
            (["tools/ci/normal_report.py"], ["migration", "docs"]),
            (["tools/ci/check_relic_coverage.py"], ["base", "docs"]),
            (["tools/ci/check_card_coverage.py"], ["base", "docs"]),
            (["tools/ci/check_dsl_migration.py", "tools/ci/check_card_coverage.py"], ["base", "migration", "docs"]),
            (["mod-the-spire/base-spire/cards/rules/play.spire", "tools/ci/check_dsl_migration.py"], ["base", "net", "migration", "docs"]),
            (["tools/ci/check_dsl_migration.py", "unknown/new-tool.py"], SUITES),
            (["tools/ci/check.py"], SUITES),
            ([".github/workflows/core-base.yml"], SUITES),
            (["unknown/new-tool.py"], SUITES),
            (["docs/examples/core-first-host/Program.cs"], SUITES),
        ]:
            with self.subTest(paths=paths):
                self.assertEqual(affected(paths), expected)

    def test_reuse_requires_successful_pr_exact_tree_and_live_artifact(self):
        run = {"id": 1, "event": "pull_request", "status": "completed", "conclusion": "success",
               "path": ".github/workflows/core-base.yml"}
        artifact = {"name": "verified-tree-abc", "expired": False}
        self.assertEqual(matching_run([run], lambda _: [artifact], "abc"), run)
        self.assertIsNone(matching_run([run], lambda _: [artifact], "different"))
        self.assertIsNone(matching_run([run], lambda _: [{**artifact, "expired": True}], "abc"))
        for field, value in [("conclusion", "failure"), ("status", "in_progress"), ("event", "push"),
                             ("path", ".github/workflows/other.yml")]:
            with self.subTest(field=field):
                self.assertIsNone(matching_run([{**run, field: value}], lambda _: [artifact], "abc"))


if __name__ == "__main__":
    unittest.main()
