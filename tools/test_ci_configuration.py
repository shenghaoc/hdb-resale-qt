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
PACKAGE = (WORKFLOWS / "linux-package.yml").read_text()
DOMAIN = (WORKFLOWS / "domain.yml").read_text()
DEPENDABOT = (ROOT / ".github/dependabot.yml").read_text()


def uses(text: str) -> list[str]:
    return re.findall(r"^\s*-?\s*uses:\s*(\S+)(.*)$", text, re.M)


class ActionPins(unittest.TestCase):
    def test_every_action_in_every_workflow_is_pinned_to_a_commit_with_a_version_comment(self):
        found = 0
        for workflow in sorted(WORKFLOWS.glob("*.yml")):
            for reference, trailer in uses(workflow.read_text()):
                found += 1
                self.assertRegex(reference, r"^[\w.-]+/[\w.-]+@[0-9a-f]{40}$", f"{workflow.name}: {reference}")
                self.assertRegex(trailer, r"#\s*v\d+", f"{workflow.name}: {reference} needs a '# vN' comment")
        self.assertGreaterEqual(found, 4)

    def test_the_same_action_uses_the_same_commit_everywhere(self):
        commits = {}
        for workflow in WORKFLOWS.glob("*.yml"):
            for reference, _ in uses(workflow.read_text()):
                name, commit = reference.split("@")
                self.assertEqual(commits.setdefault(name, commit), commit, name)


class WorkflowSplit(unittest.TestCase):
    def test_the_lightweight_domain_workflow_does_not_install_qt_or_package(self):
        for forbidden in ("aqt", "aqtinstall", "install-qt", "QtDir", "docker", "rpmbuild", "dpkg-deb", "Xvfb",
                          "build_packages", "stage_linux"):
            self.assertNotIn(forbidden, DOMAIN)
        self.assertIn("python3 -m unittest discover -s tools", DOMAIN)

    def test_package_workflow_pins_the_verified_toolchain(self):
        self.assertIn(f'QT_VERSION: "{PINS["qt"]}"', PACKAGE)
        self.assertIn("global-json-file: global.json", PACKAGE)
        self.assertIn('DEBIAN_IMAGE: "debian:13"', PACKAGE)
        self.assertIn('FEDORA_IMAGE: "fedora:43"', PACKAGE)
        for pinned in ("AQT_VERSION", "CMAKE_VERSION", "NINJA_VERSION"):
            self.assertRegex(PACKAGE, pinned + r': "\d+\.\d+\.\d+"')

    def test_package_workflow_runs_for_binary_and_package_inputs_but_not_for_documentation(self):
        paths = re.findall(r'^      - "([^"]+)"', PACKAGE.split("pull_request:")[0], re.M)
        for needed in ("src/**", "packaging/**", "tools/package/**", "global.json", "LICENSES/**",
                       "tests/fixtures/worker-api/**", ".github/workflows/linux-package.yml",
                       "docs/product-rc/licensing-packaging.md"):
            self.assertIn(needed, paths)
        for broad in ("docs/**", "**", "*.md", "tests/**", "README.md"):
            self.assertNotIn(broad, paths)
        # Both triggers use the same list.
        self.assertEqual(paths, re.findall(r'^      - "([^"]+)"', PACKAGE.split("pull_request:")[1].split("workflow_dispatch")[0], re.M))

    def test_package_binaries_are_never_uploaded(self):
        upload = PACKAGE.split("actions/upload-artifact")[1]
        self.assertIn("path: ${{ runner.temp }}/reports", upload)
        self.assertNotIn("dist", upload)
        for forbidden in ("gh release", "git tag", "gpg", "rpmsign", "dpkg-sig", "twine", "docker push"):
            self.assertNotIn(forbidden, PACKAGE)
        self.assertNotIn("contents: write", PACKAGE)

    def test_installed_package_verification_runs_on_one_distribution_per_family(self):
        self.assertIn("verify_installed.sh deb", PACKAGE)
        self.assertIn("verify_installed.sh rpm", PACKAGE)
        self.assertLess(PACKAGE.index("python3 -m unittest"), PACKAGE.index("stage_linux.py"))
        self.assertLess(PACKAGE.index("Native gates"), PACKAGE.index("stage_linux.py"))


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
