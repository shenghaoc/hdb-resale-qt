import importlib.util
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile
import unittest
from unittest import mock
import xml.etree.ElementTree as ET

PACKAGE_DIR = Path(__file__).parent / "package"
sys.path.insert(0, str(PACKAGE_DIR))


def load(name):
    spec = importlib.util.spec_from_file_location(name, PACKAGE_DIR / (name + ".py"))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


common = load("package_common")
stage = load("stage_linux")
build = load("build_packages")
installed = load("verify_installed")
ROOT = Path(__file__).parents[1]
TABLE = build.load_dependency_table()
METADATA = build.load_metadata()


def manifest(**overrides) -> dict:
    result = {"version": "1.2.3", "system_dependencies": ["libc.so.6", "libX11.so.6", "libm.so.6"],
              "dlopened_dependencies": ["libssl.so.3"], "minimum_versions": {"glibc": "2.34", "glibcxx": "3.4.29"},
              "distribution_cleared": False, "bundled_resale_data": False}
    result.update(overrides)
    return result


def staged_tree(root: Path, version: str = "1.2.3", **overrides) -> Path:
    staged = root / f"hdb-resale-explorer-{version}-linux-x64"
    (staged / "app").mkdir(parents=True)
    (staged / "app/HdbResale.App").write_bytes(b"\x7fELFhost")
    (staged / "app/HdbResale.App").chmod(0o755)
    (staged / "hdb-resale-explorer").write_text("#!/bin/sh\n")
    (staged / "hdb-resale-explorer").chmod(0o755)
    (staged / "link").symlink_to("app/HdbResale.App")
    files, modes, links = common.tree_entries(staged)
    data = manifest(version=version, files=files, modes=modes, symlinks=links, **overrides)
    (staged / "manifest.json").write_text(json.dumps(data))
    return staged


class DependencyMapping(unittest.TestCase):
    def test_debian_and_rpm_families_are_declared_separately_not_weakened(self):
        deb = build.dependencies_for(manifest(), TABLE, "deb")
        rpm = build.dependencies_for(manifest(), TABLE, "rpm")
        self.assertIn("libc6 (>= 2.34)", deb)
        self.assertIn("glibc >= 2.34", rpm)
        self.assertIn("libstdc++6 (>= 11.1)", deb)
        self.assertIn("libssl3t64", deb)
        self.assertIn("openssl-libs", rpm)
        self.assertIn("libx11-6", deb)
        self.assertIn("libX11", rpm)
        self.assertIn("xkb-data", deb)
        self.assertIn("xkeyboard-config", rpm)

    def test_the_package_never_depends_on_qt_or_dotnet_it_ships(self):
        for family in ("deb", "rpm"):
            joined = " ".join(build.dependencies_for(manifest(), TABLE, family)).lower()
            for shipped in ("qt6", "qt5", "dotnet", "icu", "libqt"):
                self.assertNotIn(shipped, joined, family)
        for libraries in TABLE["libraries"].values():
            for name in libraries.values():
                self.assertFalse(re.search(r"qt6|dotnet|libicu", name.lower()), name)

    def test_an_unmapped_system_library_fails_construction(self):
        with self.assertRaises(ValueError) as caught:
            build.dependencies_for(manifest(system_dependencies=["libc.so.6", "libnew-thing.so.1"]), TABLE, "deb")
        self.assertIn("libnew-thing.so.1", str(caught.exception))

    def test_unknown_libstdcxx_floor_is_not_guessed(self):
        with self.assertRaises(ValueError):
            build.dependencies_for(manifest(minimum_versions={"glibc": "2.34", "glibcxx": "3.4.99"}), TABLE, "deb")

    def test_table_covers_what_staging_can_emit_and_every_entry_names_both_families(self):
        for name, packages in TABLE["libraries"].items():
            self.assertEqual(set(packages), {"deb", "rpm"}, name)
        for dlopened in stage.DLOPENED_LIBRARIES:
            self.assertIn(dlopened, TABLE["libraries"])
        for family in ("deb", "rpm"):
            self.assertTrue(TABLE["unversioned"][family])
        self.assertEqual(len(TABLE["unversioned"]["deb"]), len(TABLE["unversioned"]["rpm"]))

    def test_the_documented_runtime_libraries_are_all_mapped(self):
        # The libraries the verified Linux x64 build links (docs/packaging/linux.md); a regression guard
        # that someone deleting a row notices before the package CI does.
        for needed in ("libxcb-cursor.so.0", "libxkbcommon-x11.so.0", "libEGL.so.1", "libwayland-client.so.0",
                       "libfontconfig.so.1", "libgssapi_krb5.so.2", "libz.so.1", "libssl.so.3"):
            self.assertIn(needed, TABLE["libraries"])


class PackagingFiles(unittest.TestCase):
    def test_identity_is_consistent_across_every_packaging_file(self):
        app_id = common.APPLICATION_ID
        self.assertEqual(METADATA["application_id"], app_id)
        self.assertEqual(METADATA["name"], common.PACKAGE_NAME)
        desktop = (ROOT / f"packaging/linux/{app_id}.desktop").read_text()
        self.assertIn(f"Icon={app_id}\n", desktop)
        self.assertIn(f"Exec={common.PACKAGE_NAME}\n", desktop)
        self.assertTrue((ROOT / f"packaging/linux/{app_id}.svg").is_file())
        metainfo = ET.parse(ROOT / f"packaging/linux/{app_id}.metainfo.xml.in").getroot()
        self.assertEqual(metainfo.findtext("id"), app_id)
        self.assertEqual(metainfo.findtext("launchable"), app_id + ".desktop")
        self.assertEqual(metainfo.findtext("project_license"), "GPL-3.0-or-later")
        self.assertEqual(metainfo.find("provides/binary").text, common.PACKAGE_NAME)

    def test_the_version_is_written_in_exactly_one_place(self):
        for path in [ROOT / "packaging/metadata.json", ROOT / "packaging/system-dependencies.json",
                     *(ROOT / "packaging/linux").iterdir()]:
            text = path.read_text()
            self.assertNotIn(common.application_version(), text.replace("1.0", ""), path.name)
        self.assertIn("@VERSION@", (ROOT / f"packaging/linux/{common.APPLICATION_ID}.metainfo.xml.in").read_text())

    def test_licence_metadata_matches_the_project(self):
        project = ET.parse(common.PROJECT)
        self.assertIn(project.findtext(".//PackageLicenseExpression"), METADATA["license"])
        self.assertIn("MIT", METADATA["license"])

    def test_names_follow_the_distribution_conventions(self):
        names = build.artifact_names("1.2.3", "1")
        self.assertEqual(names["deb"], "hdb-resale-explorer_1.2.3-1_amd64.deb")
        self.assertEqual(names["rpm"], "hdb-resale-explorer-1.2.3-1.x86_64.rpm")
        self.assertEqual(names["tar"], "hdb-resale-explorer-1.2.3-linux-x64.tar.gz")


class RpmSpec(unittest.TestCase):
    def spec(self):
        return build.rpm_spec(METADATA, manifest(), TABLE, "1.2.3")

    def test_the_payload_is_never_rewritten_and_no_bundled_soname_becomes_a_requirement(self):
        spec = self.spec()
        for needed in ("AutoReqProv:    no", "%global __os_install_post %{nil}", "%global debug_package %{nil}",
                       "ExclusiveArch:  x86_64", "Version:        1.2.3"):
            self.assertIn(needed, spec)
        self.assertNotIn("libQt6", spec)

    def test_files_cover_exactly_what_uninstall_must_remove(self):
        spec = self.spec()
        for path in (common.INSTALL_PREFIX, "/usr/bin/hdb-resale-explorer",
                     "/usr/share/applications/io.github.shenghaoc.hdb-resale-qt.desktop",
                     "/usr/share/metainfo/io.github.shenghaoc.hdb-resale-qt.metainfo.xml",
                     "%dir /usr/share/doc/hdb-resale-explorer"):
            self.assertIn("\n" + path + "\n", spec)


class Construction(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.tmp = Path(self.temp.name)
        os.environ["SOURCE_DATE_EPOCH"] = "1700000000"
        self.addCleanup(os.environ.pop, "SOURCE_DATE_EPOCH", None)

    def test_a_tampered_stage_cannot_be_wrapped(self):
        staged = staged_tree(self.tmp)
        (staged / "app/HdbResale.App").write_bytes(b"tampered")
        with self.assertRaises(ValueError):
            build.build(staged, self.tmp / "out", ("tar",))

    def test_stale_corpus_or_uncleared_manifests_are_refused(self):
        for overrides in ({"bundled_resale_data": True}, {"distribution_cleared": True}):
            with self.subTest(overrides), tempfile.TemporaryDirectory() as temp:
                staged = staged_tree(Path(temp), **overrides)
                with self.assertRaises(ValueError):
                    build.build(staged, Path(temp) / "out", ("tar",))

    def test_forbidden_content_cannot_be_wrapped_even_with_a_consistent_manifest(self):
        staged = staged_tree(self.tmp)
        (staged / "app/data").mkdir()
        (staged / "app/data/transactions.csv").write_text("block,price\n")
        manifest_data = json.loads((staged / "manifest.json").read_text())
        files, modes, links = common.tree_entries(staged)
        manifest_data.update(files=files, modes=modes, symlinks=links)
        (staged / "manifest.json").write_text(json.dumps(manifest_data))
        with self.assertRaises(ValueError):
            build.build(staged, self.tmp / "out", ("tar",))

    def test_the_staged_directory_name_must_match_the_manifest_version(self):
        staged = staged_tree(self.tmp)
        renamed = staged.rename(staged.parent / "hdb-resale-explorer-9.9.9-linux-x64")
        with self.assertRaises(ValueError):
            build.build(renamed, self.tmp / "out", ("tar",))

    def test_missing_input_is_an_error_not_a_partial_package(self):
        with self.assertRaises(OSError):
            build.build(self.tmp / "missing", self.tmp / "out", ("tar",))
        staged = staged_tree(self.tmp)
        (staged / "manifest.json").unlink()
        with self.assertRaises(OSError):
            build.build(staged, self.tmp / "out", ("tar",))

    def test_tar_is_reproducible_and_rooted_in_the_versioned_directory(self):
        import tarfile
        staged = staged_tree(self.tmp)
        first = build.build(staged, self.tmp / "a", ("tar",))["artifacts"]["tar"]["sha256"]
        second = build.build(staged, self.tmp / "b", ("tar",))["artifacts"]["tar"]["sha256"]
        self.assertEqual(first, second)
        with tarfile.open(self.tmp / "a/hdb-resale-explorer-1.2.3-linux-x64.tar.gz") as tar:
            names = tar.getnames()
            self.assertTrue(all(n.startswith("hdb-resale-explorer-1.2.3-linux-x64") for n in names))
            self.assertTrue(all(m.uid == 0 and m.uname == "root" for m in tar.getmembers()))

    @unittest.skipUnless(shutil.which("dpkg-deb"), "dpkg-deb is required")
    def test_deb_carries_the_declared_metadata_the_launcher_and_desktop_integration(self):
        staged = staged_tree(self.tmp)
        build.build(staged, self.tmp / "out", ("deb",))
        deb = self.tmp / "out/hdb-resale-explorer_1.2.3-1_amd64.deb"
        info = subprocess.run(["dpkg-deb", "-f", str(deb)], text=True, capture_output=True, check=True).stdout
        for field in ("Package: hdb-resale-explorer", "Version: 1.2.3-1", "Architecture: amd64",
                      "Depends: ", "libc6 (>= 2.34)"):
            self.assertIn(field, info)
        contents = subprocess.run(["dpkg-deb", "-c", str(deb)], text=True, capture_output=True, check=True).stdout
        for path in ("./opt/hdb-resale-explorer/hdb-resale-explorer", "./usr/bin/hdb-resale-explorer ->",
                     "./usr/share/applications/io.github.shenghaoc.hdb-resale-qt.desktop",
                     "./usr/share/icons/hicolor/scalable/apps/io.github.shenghaoc.hdb-resale-qt.svg",
                     "./usr/share/metainfo/io.github.shenghaoc.hdb-resale-qt.metainfo.xml",
                     "./usr/share/doc/hdb-resale-explorer/copyright"):
            self.assertIn(path, contents)
        self.assertIn("root/root", contents)
        metainfo = subprocess.run(["dpkg-deb", "--fsys-tarfile", str(deb)], capture_output=True, check=True).stdout
        self.assertIn(b'version="1.2.3" date="2023-11-14"', metainfo)

    @unittest.skipUnless(shutil.which("rpmbuild") and shutil.which("rpm"), "rpmbuild is required")
    def test_rpm_declares_explicit_requirements_and_owns_its_files(self):
        staged = staged_tree(self.tmp)
        build.build(staged, self.tmp / "out", ("rpm",))
        rpm = self.tmp / "out/hdb-resale-explorer-1.2.3-1.x86_64.rpm"
        requires = subprocess.run(["rpm", "-qpR", str(rpm)], text=True, capture_output=True, check=True).stdout
        self.assertIn("glibc >= 2.34", requires)
        self.assertIn("openssl-libs", requires)
        self.assertNotIn("libQt", requires)
        files = subprocess.run(["rpm", "-qpl", str(rpm)], text=True, capture_output=True, check=True).stdout
        self.assertIn("/usr/bin/hdb-resale-explorer", files)
        self.assertIn("/opt/hdb-resale-explorer/manifest.json", files)


class InstalledChecks(unittest.TestCase):
    def test_dependency_mapping_is_judged_by_the_package_manager(self):
        libraries = {"libc.so.6": {"deb": "libc6", "rpm": "glibc"}, "libz.so.1": {"deb": "zlib1g", "rpm": "zlib-ng-compat"}}
        owners = {"/lib/libc.so.6": "libc6", "/lib/libz.so.1": "wrong-package"}
        with mock.patch.object(installed, "resolved_library", lambda name: "/lib/" + name), \
                mock.patch.object(installed, "owner", lambda family, path: owners[path]):
            problems = installed.mapping_mismatches("deb", libraries, ["libc.so.6", "libz.so.1"])
        self.assertEqual(len(problems), 1)
        self.assertIn("libz.so.1", problems[0])
        self.assertIn("zlib1g", problems[0])

    def test_an_uninstall_that_leaves_files_fails(self):
        with tempfile.TemporaryDirectory() as temp:
            leftover = Path(temp) / "leftover"
            leftover.write_text("x")
            with mock.patch.object(installed, "OWNED", (str(leftover),)):
                with self.assertRaises(ValueError):
                    installed.verify_removed("deb")


if __name__ == "__main__":
    unittest.main()
