import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
CHECKER = ROOT / "scripts" / "check_agent_workflow.py"


class AgentWorkflowCheckTests(unittest.TestCase):
    def run_checker(self, agents: str, contributing: str) -> subprocess.CompletedProcess[str]:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "Agents.md").write_text(agents, encoding="utf-8")
            (root / "CONTRIBUTING.md").write_text(contributing, encoding="utf-8")
            return subprocess.run(
                [sys.executable, str(CHECKER), "--root", str(root)],
                capture_output=True,
                text=True,
                check=False,
            )

    def test_accepts_main_only_workflow(self) -> None:
        result = self.run_checker(
            "Work directly on `main`. Do not create a feature branch.",
            "The active checkout is `main`; external contributors may use PRs.",
        )
        self.assertEqual(result.returncode, 0, result.stderr)

    def test_rejects_contradictory_branch_instruction(self) -> None:
        result = self.run_checker(
            "Work directly on `main`.",
            "Create a **feature branch** for every change.\n"
            "Do not commit directly to `main`.",
        )
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("contradictory branch instruction", result.stderr)


if __name__ == "__main__":
    unittest.main()
