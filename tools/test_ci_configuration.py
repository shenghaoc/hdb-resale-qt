"""Static checks that keep the CI split, action pins and dependency automation honest.

Plain text parsing on purpose: the lightweight domain workflow runs these without extra packages.
"""
from pathlib import Path
import re
import sys
import unittest

sys.path.insert(0, str(Path(__file__).parent / "package"))
from package_common import PINS  # noqa: E402

ROOT = Path(__file__).parents[1]
WORKFLOWS = ROOT / ".github/workflows"
ACTIONS = ROOT / ".github/actions"
CI = (WORKFLOWS / "ci.yml").read_text()
RELEASE = (WORKFLOWS / "release.yml").read_text()
TOOLCHAIN = (ACTIONS / "qt-linux/action.yml").read_text()
CI_TESTS = CI.split("\n  tests:")[1].split("\n  native:")[0]
CI_NATIVE = CI.split("\n  native:")[1]
PACKAGE = RELEASE.split("\n  package:")[1].split("\n  publish:")[0]
PUBLISH = RELEASE.split("\n  publish:")[1]


def triggers(workflow: str) -> str:
    return workflow.split("\npermissions:")[0]


def automation_files() -> list[Path]:
    return sorted(WORKFLOWS.glob("*.yml")) + sorted(ACTIONS.glob("*/action.yml"))
DEPENDABOT = (ROOT / ".github/dependabot.yml").read_text()


def uses(text: str) -> list[str]:
    """Third-party action references; local actions and reusable workflows (./...) are this repository's own."""
    return [found for found in re.findall(r"^\s*-?\s*uses:\s*(\S+)(.*)$", text, re.M)
            if not found[0].startswith("./")]


class ActionPins(unittest.TestCase):
    def test_every_action_in_every_workflow_is_pinned_to_a_commit_with_a_version_comment(self):
        found = 0
        for workflow in automation_files():
            for reference, trailer in uses(workflow.read_text()):
                found += 1
                self.assertRegex(reference, r"^[\w.-]+/[\w.-]+@[0-9a-f]{40}$", f"{workflow.name}: {reference}")
                self.assertRegex(trailer, r"#\s*v\d+", f"{workflow.name}: {reference} needs a '# vN' comment")
        self.assertGreaterEqual(found, 4)

    def test_the_same_action_uses_the_same_commit_everywhere(self):
        commits = {}
        for workflow in automation_files():
            for reference, _ in uses(workflow.read_text()):
                name, commit = reference.split("@")
                self.assertEqual(commits.setdefault(name, commit), commit, name)


class WorkflowRoles(unittest.TestCase):
    """CI decides whether a change is correct; release decides whether a commit can ship."""

    def test_ci_runs_on_pull_requests_on_merge_to_main_and_as_the_release_gate(self):
        on = triggers(CI)
        for trigger in ("pull_request:", "push:", "workflow_call:"):
            self.assertIn(trigger, on)
        self.assertRegex(on, r"branches: \[main\b")
        self.assertNotIn("paths:", on)
        self.assertNotIn("tags:", on)

    def test_ci_runs_the_tests_once_and_never_stages_packages_or_installs(self):
        self.assertIn("dotnet test tests/HdbResale.Tests", CI_TESTS)
        self.assertIn("python3 -m unittest discover -s tools", CI_TESTS)
        self.assertIn("windows-latest", CI_TESTS)
        self.assertNotIn("qt-linux", CI_TESTS)
        self.assertIn("ctest --test-dir", CI_NATIVE)
        self.assertIn("api_native_smoke.py", CI_NATIVE)
        for forbidden in ("docker", "stage_linux", "build_packages", "verify_installed", "launch_check",
                          "rpm", "contents: write", "gh release"):
            self.assertNotIn(forbidden, CI)
        self.assertEqual(1, CI.count("dotnet test"))
        self.assertEqual(1, CI.count("unittest discover"))

    def test_release_runs_only_for_a_tag_or_on_demand_never_for_pull_requests_or_merges(self):
        on = triggers(RELEASE)
        self.assertIn('tags: ["v*"]', on)
        self.assertIn("workflow_dispatch:", on)
        self.assertNotIn("pull_request", on)
        self.assertNotIn("branches:", on)

    def test_packages_are_built_only_after_the_full_ci_passed(self):
        self.assertIn("uses: ./.github/workflows/ci.yml", RELEASE)
        self.assertIn("needs: ci", PACKAGE)
        self.assertNotIn("if:", PACKAGE.split("steps:")[0])
        self.assertIn("needs: package", PUBLISH)

    def test_release_verifies_the_artifacts_on_one_distribution_per_family(self):
        self.assertIn('DEBIAN_IMAGE: "debian:13"', RELEASE)
        self.assertIn('FEDORA_IMAGE: "fedora:43"', RELEASE)
        self.assertIn("verify_installed.sh deb", PACKAGE)
        self.assertIn("verify_installed.sh rpm", PACKAGE)
        self.assertIn("a tampered staged tree was packaged", PACKAGE)
        self.assertIn("launch_check.py", PACKAGE)
        self.assertLess(PACKAGE.index("build_packages.py"), PACKAGE.index("verify_installed.sh"))

    def test_a_release_tag_must_name_the_project_version(self):
        self.assertIn("application_version()", PACKAGE)
        self.assertIn('"v$version"', PACKAGE)

    def test_only_verified_packages_are_published_and_only_from_a_tag(self):
        verification, packages = PACKAGE.split("name: linux-packages")
        self.assertIn("name: linux-package-verification", verification)
        # The package upload has no "if: always()": it is reached only when every verification step passed.
        upload_step = verification[verification.rindex("- uses: actions/upload-artifact"):]
        self.assertNotIn("if:", upload_step)
        self.assertIn("if-no-files-found: error", packages)
        self.assertIn("if: github.event_name == 'push' && startsWith(github.ref, 'refs/tags/v')", PUBLISH)
        self.assertIn("contents: write", PUBLISH)
        self.assertEqual(1, RELEASE.count("contents: write"))
        self.assertIn("permissions:\n  contents: read", RELEASE)
        for forbidden in ("gpg", "rpmsign", "dpkg-sig", "twine", "docker push", "git tag"):
            self.assertNotIn(forbidden, RELEASE)

    def test_a_tag_only_ever_creates_a_draft_and_never_changes_a_published_release(self):
        # Distribution is not cleared, so CI must not make packages public; publishing the draft is manual.
        self.assertIn("gh release create", PUBLISH)
        create = PUBLISH[PUBLISH.index("gh release create"):]
        self.assertIn("--draft", create.split("\n          fi")[0])
        self.assertIn("isDraft", PUBLISH)
        self.assertIn("refusing to change its assets", PUBLISH)
        self.assertNotIn("gh release edit", RELEASE)

    def test_a_rerun_of_the_package_job_replaces_its_artifacts(self):
        uploads = PACKAGE.split("- uses: actions/upload-artifact")[1:]
        self.assertEqual(2, len(uploads))
        for upload in uploads:
            self.assertIn("overwrite: true", upload)

    def test_the_native_toolchain_is_pinned_once_and_shared(self):
        self.assertIn(f'QT_VERSION: "{PINS["qt"]}"', TOOLCHAIN)
        self.assertIn("global-json-file: global.json", TOOLCHAIN)
        for pinned in ("AQT_VERSION", "CMAKE_VERSION", "NINJA_VERSION"):
            self.assertRegex(TOOLCHAIN, pinned + r': "\d+\.\d+\.\d+"')
        self.assertIn("uses: ./.github/actions/qt-linux", CI_NATIVE)
        self.assertIn("uses: ./.github/actions/qt-linux", PACKAGE)
        for workflow in (CI, RELEASE):
            self.assertNotIn("aqt install-qt", workflow)
            self.assertNotIn("QT_VERSION", workflow)


class DependencyAutomation(unittest.TestCase):
    def test_only_ecosystems_that_exist_here_are_configured(self):
        ecosystems = re.findall(r"package-ecosystem:\s*(\S+)", DEPENDABOT)
        self.assertEqual(sorted(ecosystems), ["github-actions", "nuget"])
        self.assertTrue(list(ROOT.glob("src/*/*.csproj")))
        self.assertTrue(list(WORKFLOWS.glob("*.yml")))
        self.assertEqual(list(ROOT.glob("**/requirements*.txt")) + list(ROOT.glob("**/package.json")) +
                         list(ROOT.glob("**/Dockerfile")), [])

    def test_updates_are_weekly_and_grouped(self):
        self.assertEqual(DEPENDABOT.count("interval: weekly"), 2)
        self.assertEqual(DEPENDABOT.count("groups:"), 2)

    def test_deliberate_qt_bridge_pins_are_never_updated_automatically(self):
        self.assertRegex(DEPENDABOT, r'dependency-name:\s*"QtGroup\.Qt\.Bridge\.CSharp\*"')
        project = (ROOT / "src/HdbResale.App/HdbResale.App.csproj").read_text()
        self.assertIn("0.4.0.22-beta", project)  # macOS
        self.assertIn(PINS["bridge"], project)  # Linux
        self.assertNotEqual("0.4.0.22-beta", PINS["bridge"])

    def test_dotnet_sdk_toolchain_is_documented_as_manual_not_silently_uncovered(self):
        self.assertNotIn("dotnet-sdk", re.findall(r"package-ecosystem:\s*(\S+)", DEPENDABOT))
        self.assertIn("global.json", DEPENDABOT)
        self.assertIn("Qt 6.12.0", DEPENDABOT)


if __name__ == "__main__":
    unittest.main()
