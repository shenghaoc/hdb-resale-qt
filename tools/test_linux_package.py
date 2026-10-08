import importlib.util
import json
from pathlib import Path
import re
import socket
import tempfile
import unittest
import urllib.request


def load(name):
    spec = importlib.util.spec_from_file_location(name, Path(__file__).parent / "package" / (name + ".py"))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


package = load("linux_rc")
launch = load("launch_check")


class PackageChecks(unittest.TestCase):
    def test_tile_monitor_is_required_and_staged_byte_exactly(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            build = root / "build"
            build.mkdir()
            with self.assertRaises(FileNotFoundError):
                package.copy_app_native_libraries(build, root / "app")
            (build / "libhdb_tile_status.so").write_bytes(b"native-monitor")
            package.copy_app_native_libraries(build, root / "app")
            self.assertEqual((root / "app/libhdb_tile_status.so").read_bytes(), b"native-monitor")

    def test_app_local_icu_uses_only_relative_internal_links(self):
        import json
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            (root / "qt/lib").mkdir(parents=True)
            (root / "app").mkdir()
            runtime = root / "dotnet/shared/Microsoft.NETCore.App/10.0.12"
            runtime.mkdir(parents=True)
            for name in ("libicudata.so.73", "libicuuc.so.73", "libicui18n.so.73"):
                (root / "qt/lib" / name).write_bytes(b"unchanged-runtime")
            config = root / "app/HdbResale.App.runtimeconfig.json"
            config.write_text('{"runtimeOptions":{"framework":{"version":"10.0.0"}}}')
            package.configure_app_local_icu(root)
            self.assertEqual(json.loads(config.read_text())["runtimeOptions"]["configProperties"]["System.Globalization.AppLocalIcu"], "73")
            for link in runtime.iterdir():
                self.assertTrue(link.is_symlink())
                self.assertFalse(link.readlink().is_absolute())
                self.assertTrue(link.resolve().is_relative_to((root / "qt/lib").resolve()))
                self.assertEqual(link.read_bytes(), b"unchanged-runtime")

    def test_environment_excludes_developer_state(self):
        original = {"DISPLAY": ":0", "PATH": "/private/sdk", "LD_LIBRARY_PATH": "/qt", "DOTNET_ROOT": "/sdk",
                    "QML_IMPORT_PATH": "/qml", "HDB_DATA_DIRECTORY": "/private-data",
                    "HDB_API_BASE_URL": "https://developer.example/", "HDB_API_GATE": "high-zoom"}
        result = launch.clean_environment(original, Path("/tmp/fresh"), "production")
        self.assertEqual(result["DISPLAY"], ":0")
        for forbidden in ("LD_LIBRARY_PATH", "DOTNET_ROOT", "QML_IMPORT_PATH", "HDB_DATA_DIRECTORY",
                          "HDB_API_BASE_URL", "HDB_API_GATE"):
            self.assertNotIn(forbidden, result)
        self.assertEqual(result["PATH"], "/usr/bin:/bin")
        self.assertEqual(result["HDB_PACKAGE_SMOKE"], "1")

    def test_each_api_mode_chooses_its_own_address(self):
        original = {"DISPLAY": ":0", "HDB_API_BASE_URL": "https://developer.example/"}
        recorded = launch.clean_environment(original, Path("/tmp/fresh"), "recorded", "http://127.0.0.1:9/")
        self.assertEqual(recorded["HDB_API_BASE_URL"], "http://127.0.0.1:9/")
        self.assertEqual(recorded["HDB_PACKAGE_SMOKE"], "1")
        refused = launch.clean_environment(original, Path("/tmp/fresh"), "unreachable", "http://127.0.0.1:9/")
        self.assertEqual(refused["HDB_API_GATE"], "unreachable")
        self.assertNotIn("HDB_PACKAGE_SMOKE", refused)

    def test_package_bundles_no_resale_data(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            (root / "app/Application").mkdir(parents=True)
            (root / "app/Application/Main.qml").write_text("Item {}")
            (root / "qt/lib").mkdir(parents=True)
            (root / "qt/lib/third-party-table.csv").write_text("not application data")
            package.refuse_bundled_data(root)
            for planted in ("app/data/README.md", "app/data/transactions.csv", "app/evidence.geojson", "app/Export.CSV"):
                with self.subTest(planted=planted), tempfile.TemporaryDirectory() as other:
                    copy = Path(other)
                    (copy / planted).parent.mkdir(parents=True)
                    (copy / planted).write_text("row")
                    with self.assertRaises(ValueError):
                        package.refuse_bundled_data(copy)

    def test_manifest_api_address_is_the_apps_production_address(self):
        source = (Path(__file__).parents[1] / "src/HdbResale.Domain/WorkerApi.cs").read_text()
        self.assertEqual(re.search(r'ProductionBaseAddress = new\("([^"]+)"\)', source).group(1), package.API_BASE_URL)
        self.assertNotIn("DATA_FILES", vars(package))

    def test_recorded_and_refused_endpoints_stay_on_loopback(self):
        recorded = launch.ApiEndpoint("recorded")
        try:
            self.assertTrue(recorded.base_url.startswith("http://127.0.0.1:"))
            with urllib.request.urlopen(recorded.base_url + "api/manifest", timeout=5) as response:
                self.assertEqual(json.load(response)["dataWindow"]["maxMonth"], "2026-10")
        finally:
            recorded.close()
        refused = launch.ApiEndpoint("unreachable")
        try:
            port = int(refused.base_url.rsplit(":", 1)[1].rstrip("/"))
            with self.assertRaises(OSError):
                socket.create_connection(("127.0.0.1", port), timeout=2).close()
        finally:
            refused.close()
        self.assertIsNone(launch.ApiEndpoint("production").base_url)

    def test_marker_and_runtime_failures_cannot_pass(self):
        root = Path("/tmp/bundle")
        mapped = {str(root / "qt/lib/libQt6Core.so.6"), str(root / "dotnet/libcoreclr.so"),
                  str(root / "qt/plugins/geoservices/libqtgeoservices_osm.so"), str(root / "qt/lib/libQt6Graphs.so.6")}
        good = "\n".join(launch.MARKERS)
        launch.verify(0, good, mapped, root)
        for log in (good.replace("HDB_PACKAGE_DATA", ""), good + "\nTypeError: invalid", good + "\nHDB_PACKAGE_EXIT"):
            with self.assertRaises(ValueError):
                launch.verify(0, log, mapped, root)
        with self.assertRaises(ValueError):
            launch.verify(1, good, mapped, root)
        with self.assertRaises(ValueError):
            launch.verify(0, good, mapped | {"/build/Qt/lib/libQt6Qml.so.6"}, root)
        with self.assertRaises(ValueError):
            launch.verify(0, good, mapped - {str(root / "qt/lib/libQt6Graphs.so.6")}, root)

    def test_unreachable_launch_must_not_claim_data(self):
        root = Path("/tmp/bundle")
        mapped = {str(root / "qt/lib/libQt6Core.so.6"), str(root / "dotnet/libcoreclr.so"),
                  str(root / "qt/plugins/geoservices/libqtgeoservices_osm.so")}
        good = "HDB_API_UNREACHABLE_PASS Could not load addresses.\nHDB_API_GATE_EXIT"
        launch.verify(0, good, mapped, root, "unreachable")
        for log in ("HDB_API_GATE_EXIT", good.replace("\n", "\nHDB_PACKAGE_DATA\n"), good + "\nHDB_API_GATE_FAIL unreachable"):
            with self.assertRaises(ValueError):
                launch.verify(0, log, mapped, root, "unreachable")
        with self.assertRaises(ValueError):
            launch.verify(0, "\n".join(launch.MARKERS), mapped, root, "unreachable")

    def test_original_code_license_excludes_data_and_imported_experiments(self):
        import tomllib
        metadata = tomllib.loads((Path(__file__).parents[1] / "REUSE.toml").read_text())
        annotation = metadata["annotations"][0]
        self.assertEqual(annotation["SPDX-License-Identifier"], "GPL-3.0-or-later")
        self.assertEqual(annotation["precedence"], "closest")
        for scope in annotation["path"]:
            self.assertFalse(scope.startswith(("data/", "experiments/", "docs/")))
            self.assertNotIn("assets", scope)

    def test_output_never_overwrites_or_enters_repo(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            with self.assertRaises(ValueError):
                package.validate_output(root / "dist", root)
            with self.assertRaises(ValueError):
                package.validate_output(root, Path("/other/repo"))
            package.validate_output(root / "new", Path("/other/repo"))

    def test_qml_copy_excludes_unselected_child_module(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            source = root / "source"
            (source / "private").mkdir(parents=True)
            (source / "Unselected").mkdir()
            (source / "qmldir").write_text("module Parent")
            (source / "private/icon.svg").write_text("asset")
            (source / "Unselected/qmldir").write_text("module Unselected")
            package.copy_qml_module(source, root / "out")
            self.assertTrue((root / "out/private/icon.svg").exists())
            self.assertFalse((root / "out/Unselected").exists())


if __name__ == "__main__":
    unittest.main()
