import importlib.util
from pathlib import Path
import tempfile
import unittest


def load(name):
    spec = importlib.util.spec_from_file_location(name, Path(__file__).parent / "package" / (name + ".py"))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


package = load("linux_rc")
launch = load("launch_check")


class PackageChecks(unittest.TestCase):
    def test_environment_excludes_developer_state(self):
        result = launch.clean_environment({"DISPLAY": ":0", "PATH": "/private/sdk", "LD_LIBRARY_PATH": "/qt",
                    "DOTNET_ROOT": "/sdk", "QML_IMPORT_PATH": "/qml", "HDB_DATA_DIRECTORY": "/private-data"}, Path("/tmp/fresh"))
        self.assertEqual(result["DISPLAY"], ":0")
        for forbidden in ("LD_LIBRARY_PATH", "DOTNET_ROOT", "QML_IMPORT_PATH", "HDB_DATA_DIRECTORY"):
            self.assertNotIn(forbidden, result)
        self.assertEqual(result["PATH"], "/usr/bin:/bin")

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
