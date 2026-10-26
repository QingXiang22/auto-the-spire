"""Exercise removal guards in a disposable repository, never in project worktrees."""
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest


SCRIPT = Path(__file__).with_name("cleanup_worktrees.ps1").resolve()
SHELL = shutil.which("pwsh") or shutil.which("powershell")


@unittest.skipUnless(SHELL, "PowerShell is required for worktree cleanup")
class CleanupTests(unittest.TestCase):
    def test_preview_removal_and_protection(self):
        with tempfile.TemporaryDirectory(prefix="spire-cleanup-test-") as directory:
            root = Path(directory).resolve()
            env = dict(os.environ, GIT_AUTHOR_NAME="Cleanup test", GIT_AUTHOR_EMAIL="test@example.invalid",
                       GIT_COMMITTER_NAME="Cleanup test", GIT_COMMITTER_EMAIL="test@example.invalid")

            def git(*args, cwd=root):
                return subprocess.check_output(["git", *args], cwd=cwd, env=env, stderr=subprocess.STDOUT)

            def cleanup(name, remove=True):
                args = [SHELL, "-NoProfile", "-File", str(SCRIPT), "-Name", name]
                if remove:
                    args.append("-Remove")
                return subprocess.run(args, cwd=root, capture_output=True)

            git("init", "-b", "main")
            (root / ".gitignore").write_text(".artifacts/\n", encoding="utf-8")
            (root / "content.txt").write_text("original", encoding="utf-8")
            git("add", ".")
            git("-c", "commit.gpgsign=false", "commit", "-m", "baseline")
            paths = {}
            for name in ("finished", "dirty", "unique"):
                path = root / ".artifacts/worktrees" / name
                git("worktree", "add", "-b", name, str(path), "main")
                paths[name] = path
            (paths["finished"] / ".artifacts").mkdir()
            (paths["finished"] / ".artifacts/output.log").write_text("disposable", encoding="utf-8")
            self.assertEqual(cleanup("finished", remove=False).returncode, 0)
            self.assertTrue(paths["finished"].exists())
            self.assertEqual(cleanup("finished").returncode, 0)
            self.assertFalse(paths["finished"].exists())
            git("show-ref", "--verify", "refs/heads/finished")
            (paths["dirty"] / "content.txt").write_text("unfinished work", encoding="utf-8")
            self.assertNotEqual(cleanup("dirty").returncode, 0)
            self.assertEqual((paths["dirty"] / "content.txt").read_text(encoding="utf-8"), "unfinished work")
            (paths["unique"] / "content.txt").write_text("unmerged commit", encoding="utf-8")
            git("add", ".", cwd=paths["unique"])
            git("-c", "commit.gpgsign=false", "commit", "-m", "unique", cwd=paths["unique"])
            self.assertNotEqual(cleanup("unique").returncode, 0)
            self.assertTrue(paths["unique"].exists())
            self.assertNotEqual(cleanup("../outside").returncode, 0)


if __name__ == "__main__":
    unittest.main()
