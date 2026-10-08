import importlib.util
import json
import os
from pathlib import Path
import re
import sys
import tempfile
import unittest
from unittest import mock

PACKAGE_DIR = Path(__file__).parent / "package"
sys.path.insert(0, str(PACKAGE_DIR))


def load(name):
    spec = importlib.util.spec_from_file_location(name, PACKAGE_DIR / (name + ".py"))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


common = load("package_common")
stage = load("stage_linux")
ROOT = Path(__file__).parents[1]


def write(root: Path, relative: str, content: bytes = b"x", mode: int = 0o644) -> Path:
    path = root / relative
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(content)
    path.chmod(mode)
    return path


def manifest_for(root: Path) -> dict:
    files, modes, links = common.tree_entries(root)
    return {"files": files, "modes": modes, "symlinks": links}


class VersionAndPins(unittest.TestCase):
    def test_one_authoritative_version_comes_from_the_project(self):
        project = (ROOT / "src/HdbResale.App/HdbResale.App.csproj").read_text()
        declared = re.search(r"<Version>([^<]+)</Version>", project).group(1)
        self.assertEqual(common.application_version(), declared)

    def test_assembly_versions_must_agree_with_version(self):
        with tempfile.TemporaryDirectory() as temp:
            project = Path(temp) / "a.csproj"
            for body, valid in (("<Version>1.2.3</Version><AssemblyVersion>1.2.3.0</AssemblyVersion><FileVersion>1.2.3.0</FileVersion>", True),
                                ("<Version>1.2.3</Version><AssemblyVersion>1.2.4.0</AssemblyVersion><FileVersion>1.2.3.0</FileVersion>", False),
                                ("<Version>1.2</Version><AssemblyVersion>1.2.0</AssemblyVersion><FileVersion>1.2.0</FileVersion>", False),
                                ("<Version>1.2.3-beta</Version><AssemblyVersion>1.2.3-beta.0</AssemblyVersion><FileVersion>1.2.3-beta.0</FileVersion>", False)):
                project.write_text(f"<Project><PropertyGroup>{body}</PropertyGroup></Project>")
                if valid:
                    self.assertEqual(common.application_version(project), "1.2.3")
                else:
                    with self.assertRaises(ValueError):
                        common.application_version(project)

    def test_script_pins_match_the_project_and_global_json(self):
        project = (ROOT / "src/HdbResale.App/HdbResale.App.csproj").read_text()
        self.assertIn(f"'$(QtBridgeTemplateRid)' == 'linux-x64'\">{common.PINS['bridge']}<", project)
        sdk = json.loads((ROOT / "global.json").read_text())["sdk"]["version"]
        self.assertTrue(sdk.startswith("10.0."))
        self.assertTrue(common.PINS["dotnet_runtime"].startswith("10.0."))


class ForbiddenContent(unittest.TestCase):
    def test_api_era_tree_passes_and_stale_corpus_assumptions_fail(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            write(root, "app/HdbResale.App", b"\x7fELF")
            write(root, "licenses/APPLICATION-LICENSE", b"GPL")
            common.audit_tree(root)
            for bad in ("app/data/transactions.csv", "data/provenance.json", "app/address-evidence.csv",
                        "app/building-evidence.geojson", "app/HdbResale.App.pdb", "app/tiles/1.png",
                        "qt/plugins/qmltooling/libqmldbg_server.so", "qt/plugins/egldeviceintegrations/x.so",
                        "app/.env", "app/key.pem", "src/Program.cs", "app/__pycache__/x.pyc", ".git/config"):
                with self.subTest(bad=bad):
                    write(root, bad)
                    with self.assertRaises(ValueError):
                        common.audit_tree(root)
                    path = root / bad
                    path.unlink()

    def test_text_config_may_not_name_the_build_machine(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            write(root, "app/qt.conf", b"[Paths]\nPrefix = ../qt\n")
            common.audit_tree(root)
            for leak in (b"Prefix = /opt/Qt/6.12.0/gcc_64", b"/home/dev/.nuget/packages", b"/home/runner/work/x/x"):
                write(root, "app/qt.conf", leak)
                with self.assertRaises(ValueError):
                    common.audit_tree(root)

    def test_licence_prose_may_contain_urls_and_paths(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            write(root, "licenses/licensing-packaging.md", b"see /home/user/example and /opt/Qt/")
            common.audit_tree(root)


class ManifestAgreement(unittest.TestCase):
    def tree(self, root: Path) -> dict:
        write(root, "app/host", b"host", 0o755)
        write(root, "qt/lib/libx.so.1", b"lib")
        (root / "qt/lib/libx.so").symlink_to("libx.so.1")
        return manifest_for(root)

    def test_untouched_tree_verifies(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            common.verify_manifest(root, self.tree(root))

    def test_every_kind_of_drift_is_rejected(self):
        for name, mutate in {
            "changed": lambda r: write(r, "app/host", b"tampered", 0o755),
            "missing": lambda r: (r / "qt/lib/libx.so.1").unlink(),
            "extra": lambda r: write(r, "app/stray.txt"),
            "mode": lambda r: (r / "app/host").chmod(0o644),
            "symlink retargeted": lambda r: ((r / "qt/lib/libx.so").unlink(), (r / "qt/lib/libx.so").symlink_to("/etc/passwd")),
        }.items():
            with self.subTest(name), tempfile.TemporaryDirectory() as temp:
                root = Path(temp)
                manifest = self.tree(root)
                mutate(root)
                with self.assertRaises(ValueError):
                    common.verify_manifest(root, manifest)

    def test_manifest_cannot_point_outside_the_package(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            manifest = self.tree(root)
            manifest["files"]["../outside"] = "0" * 64
            manifest["modes"]["../outside"] = "0644"
            with self.assertRaises(ValueError):
                common.verify_manifest(root, manifest)


class QtResolution(unittest.TestCase):
    def test_bridge_deploy_is_preferred_and_must_agree_with_the_prefix(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            bridge, qt = root / "bridge", root / "qt"
            write(bridge, "lib/libQt6Core.so.6", b"same")
            write(qt, "lib/libQt6Core.so.6", b"same")
            write(qt, "lib/libQt6Qml.so.6", b"only-qt")
            self.assertEqual(stage.locate("lib/libQt6Core.so.6", bridge, qt)[1], "bridge")
            self.assertEqual(stage.locate("lib/libQt6Qml.so.6", bridge, qt)[1], "qt")
            write(bridge, "lib/libQt6Core.so.6", b"different")
            with self.assertRaises(ValueError):
                stage.locate("lib/libQt6Core.so.6", bridge, qt)
            with self.assertRaises(FileNotFoundError):
                stage.locate("lib/libQt6Missing.so.6", bridge, qt)

    def test_library_closure_copies_only_qt_and_icu_and_records_suppliers(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            bridge, qt, package = root / "bridge", root / "qt", root / "package"
            write(bridge, "lib/libQt6Core.so.6", b"core")
            write(qt, "lib/libQt6Core.so.6", b"core")
            write(qt, "lib/libQt6Gui.so.6", b"gui")
            write(qt, "lib/libicuuc.so.73", b"icu")
            host = write(package, "app/host")
            needed = {"host": ["libQt6Gui.so.6", "libc.so.6"], "libQt6Gui.so.6": ["libQt6Core.so.6", "libicuuc.so.73"]}
            with mock.patch.object(stage, "dynamic", lambda path: (needed.get(path.name, []), [])):
                result = stage.select_qt_libraries([host], package, bridge, qt)
            self.assertEqual(result, {"libQt6Gui.so.6": "qt", "libQt6Core.so.6": "bridge", "libicuuc.so.73": "qt"})
            self.assertFalse((package / "qt/lib/libc.so.6").exists())

    def test_a_missing_qt_dependency_and_a_foreign_prefix_library_are_fatal(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            bridge, qt, package = root / "bridge", root / "qt", root / "package"
            host = write(package, "app/host")
            with mock.patch.object(stage, "dynamic", lambda path: (["libQt6Gone.so.6"], [])):
                with self.assertRaises(ValueError):
                    stage.select_qt_libraries([host], package, bridge, qt)
            write(qt, "lib/libfoo.so.1")
            with mock.patch.object(stage, "dynamic", lambda path: (["libfoo.so.1"], [])):
                with self.assertRaises(ValueError):
                    stage.select_qt_libraries([host], package, bridge, qt)


class StagingParts(unittest.TestCase):
    def test_tile_monitor_is_required_and_staged_byte_exactly(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            build = root / "build"
            build.mkdir()
            with self.assertRaises(FileNotFoundError):
                stage.copy_app_native_libraries(build, root / "app")
            (build / "libhdb_tile_status.so").write_bytes(b"native-monitor")
            stage.copy_app_native_libraries(build, root / "app")
            self.assertEqual((root / "app/libhdb_tile_status.so").read_bytes(), b"native-monitor")

    def test_app_local_icu_uses_only_relative_internal_links(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            (root / "qt/lib").mkdir(parents=True)
            (root / "app").mkdir()
            runtime = root / "dotnet/shared/Microsoft.NETCore.App" / common.PINS["dotnet_runtime"]
            runtime.mkdir(parents=True)
            for name in ("libicudata.so.73", "libicuuc.so.73", "libicui18n.so.73"):
                (root / "qt/lib" / name).write_bytes(b"unchanged-runtime")
            config = root / "app/HdbResale.App.runtimeconfig.json"
            config.write_text('{"runtimeOptions":{"framework":{"version":"10.0.0"}}}')
            stage.configure_app_local_icu(root)
            self.assertEqual(json.loads(config.read_text())["runtimeOptions"]["configProperties"]["System.Globalization.AppLocalIcu"], "73")
            for link in runtime.iterdir():
                self.assertTrue(link.is_symlink())
                self.assertFalse(link.readlink().is_absolute())
                self.assertTrue(link.resolve().is_relative_to((root / "qt/lib").resolve()))
                self.assertEqual(link.read_bytes(), b"unchanged-runtime")

    def test_output_never_overwrites_or_enters_repo(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            with self.assertRaises(ValueError):
                stage.validate_output(root / "dist", root)
            with self.assertRaises(ValueError):
                stage.validate_output(root, Path("/other/repo"))
            stage.validate_output(root / "new", Path("/other/repo"))

    def test_qml_copy_excludes_unselected_child_module(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            source = root / "source"
            (source / "private").mkdir(parents=True)
            (source / "Unselected").mkdir()
            (source / "qmldir").write_text("module Parent")
            (source / "private/icon.svg").write_text("asset")
            (source / "Unselected/qmldir").write_text("module Unselected")
            stage.copy_qml_module(source, root / "out")
            self.assertTrue((root / "out/private/icon.svg").exists())
            self.assertFalse((root / "out/Unselected").exists())

    def test_version_floors_are_the_highest_required_symbol_versions(self):
        text = "Version needs section\n  Name: GLIBC_2.2.5  Flags: none\n  Name: GLIBC_2.34  Flags: none\n" \
               "  Name: GLIBC_2.4\n  Name: GLIBCXX_3.4.29\n  Name: GLIBCXX_3.4.9\n"
        with mock.patch.object(stage, "run", lambda *a, **k: text):
            self.assertEqual(stage.version_floors([Path("a"), Path("b")]), {"glibc": "2.34", "glibcxx": "3.4.29"})

    def test_launcher_resolves_through_a_symlink_and_never_uses_host_runtimes(self):
        script = stage.launcher_script()
        self.assertIn("readlink -f", script)
        for needed in ('DOTNET_ROOT="$here/dotnet"', "DOTNET_MULTILEVEL_LOOKUP=0", 'QT_PLUGIN_PATH="$here/qt/plugins"',
                       'QML_IMPORT_PATH="$here/qt/qml"'):
            self.assertIn(needed, script)

    def test_plugin_policy_has_no_embedded_display_or_debug_plugins(self):
        for relative in stage.PLUGIN_FILES:
            self.assertIsNone(common.forbidden_reason("qt/plugins/" + relative), relative)
            self.assertNotIn("eglfs", relative)
        self.assertIn("platforms/libqxcb.so", stage.PLUGIN_FILES)

    def test_original_code_license_excludes_data_and_imported_experiments(self):
        import tomllib
        metadata = tomllib.loads((ROOT / "REUSE.toml").read_text())
        annotation = metadata["annotations"][0]
        self.assertEqual(annotation["SPDX-License-Identifier"], "GPL-3.0-or-later")
        self.assertEqual(annotation["precedence"], "closest")
        for scope in annotation["path"]:
            self.assertFalse(scope.startswith(("data/", "experiments/", "docs/")))
            self.assertNotIn("assets", scope)

    def test_licence_texts_the_package_ships_are_in_the_repository(self):
        for name in ("LGPL-3.0-only.txt", "GPL-3.0-only.txt", "BSD-3-Clause.txt", "GPL-3.0-or-later.txt"):
            self.assertTrue((ROOT / "LICENSES" / name).is_file(), name)


if __name__ == "__main__":
    unittest.main()
