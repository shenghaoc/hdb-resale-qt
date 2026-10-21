import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest import mock

PACKAGE_DIR = Path(__file__).parent / "package"
sys.path.insert(0, str(PACKAGE_DIR))
sys.path.insert(0, str(Path(__file__).parent))
spec = importlib.util.spec_from_file_location("launch_check", PACKAGE_DIR / "launch_check.py")
launch = importlib.util.module_from_spec(spec)
spec.loader.exec_module(launch)

ROOT = Path("/opt/hdb-resale-explorer")
MAPPED = {str(ROOT / "qt/lib/libQt6Core.so.6"), str(ROOT / "dotnet/shared/Microsoft.NETCore.App/10.0.12/libcoreclr.so"),
          str(ROOT / "qt/plugins/geoservices/libqtgeoservices_osm.so"), str(ROOT / "qt/lib/libQt6Graphs.so.6"),
          str(ROOT / "qt/plugins/platforms/libqxcb.so"), "/usr/lib/x86_64-linux-gnu/libxcb.so.1"}
GOOD = "\n".join(launch.MARKERS)


class Environment(unittest.TestCase):
    def test_environment_excludes_developer_state_but_keeps_the_recorded_api(self):
        original = {"DISPLAY": ":0", "PATH": "/private/sdk", "LD_LIBRARY_PATH": "/qt", "LD_PRELOAD": "/x.so",
                    "DOTNET_ROOT": "/sdk", "DOTNET_gcServer": "1", "QML_IMPORT_PATH": "/qml", "QT_PLUGIN_PATH": "/p",
                    "QT_QPA_PLATFORM": "offscreen", "HDB_DATA_DIRECTORY": "/private-data", "HDB_API_GATE": "x",
                    "HDB_TILE_TEST": "1", "HDB_TEST_TILE_ENDPOINT": "http://x/", "NUGET_PACKAGES": "/n",
                    "QtDir": "/opt/Qt", "XDG_CURRENT_DESKTOP": "GNOME", "QT_QPA_PLATFORMTHEME": "xdgdesktopportal"}
        result = launch.clean_environment(original, Path("/tmp/fresh"), "http://127.0.0.1:9/")
        self.assertEqual(result["DISPLAY"], ":0")
        self.assertEqual(result["XDG_CURRENT_DESKTOP"], "GNOME")
        self.assertNotIn("QT_QPA_PLATFORMTHEME", result)
        self.assertEqual(result["HDB_API_BASE_URL"], "http://127.0.0.1:9/")
        self.assertEqual(result["QT_QPA_PLATFORM"], "xcb")
        self.assertEqual(result["PATH"], "/usr/bin:/bin")
        for forbidden in ("LD_LIBRARY_PATH", "LD_PRELOAD", "DOTNET_ROOT", "DOTNET_gcServer", "QML_IMPORT_PATH",
                          "QT_PLUGIN_PATH", "HDB_DATA_DIRECTORY", "HDB_API_GATE", "HDB_TILE_TEST",
                          "HDB_TEST_TILE_ENDPOINT", "NUGET_PACKAGES", "QtDir"):
            self.assertNotIn(forbidden, result)
        self.assertEqual({k for k in result if k.startswith("HDB_")}, {"HDB_API_BASE_URL", "HDB_PACKAGE_SMOKE"})
        for state in ("HOME", "XDG_CONFIG_HOME", "XDG_CACHE_HOME", "XDG_DATA_HOME"):
            self.assertTrue(result[state].startswith("/tmp/fresh"))


class Verdict(unittest.TestCase):
    def test_complete_launch_passes(self):
        launch.verify(0, GOOD, MAPPED, ROOT)

    def test_marker_and_runtime_failures_cannot_pass(self):
        for log in (GOOD.replace("HDB_PACKAGE_DATA", ""), GOOD + "\nTypeError: invalid", GOOD + "\nHDB_PACKAGE_EXIT",
                    GOOD + "\nHDB_PACKAGE_FAIL readiness timeout", GOOD + "\nCould not find the Qt platform plugin"):
            with self.assertRaises(ValueError):
                launch.verify(0, log, MAPPED, ROOT)
        with self.assertRaises(ValueError):
            launch.verify(1, GOOD, MAPPED, ROOT)

    def test_libraries_from_outside_the_package_cannot_pass(self):
        for escaped in ("/build/Qt/lib/libQt6Qml.so.6", "/opt/Qt/6.12.0/gcc_64/qml/QtQuick/libqtquick2plugin.so",
                        "/usr/share/dotnet/shared/Microsoft.NETCore.App/10.0.12/libclrjit.so",
                        "/usr/lib/x86_64-linux-gnu/libicuuc.so.73.2"):
            with self.assertRaises(ValueError, msg=escaped):
                launch.verify(0, GOOD, MAPPED | {escaped}, ROOT)

    def test_missing_evidence_cannot_pass_including_x11_itself(self):
        for required in ("libQt6Graphs.so.6", "libqxcb.so", "libqtgeoservices_osm.so", "libcoreclr.so"):
            with self.assertRaises(ValueError, msg=required):
                launch.verify(0, GOOD, {p for p in MAPPED if required not in p}, ROOT)

    def test_a_host_icu_is_acceptable_only_when_a_host_library_links_it(self):
        with tempfile.TemporaryDirectory() as temp:
            host = Path(temp) / "libxml2.so.2"
            host.write_bytes(b"\x7fELF....libicuuc.so.74....")
            icu = "/usr/lib/libicuuc.so.74.2"
            self.assertTrue(launch.system_icu_pulled_in_by_system_library(icu, MAPPED | {icu, str(host)}, ROOT))
            self.assertFalse(launch.system_icu_pulled_in_by_system_library(icu, MAPPED | {icu}, ROOT))
            self.assertFalse(launch.system_icu_pulled_in_by_system_library("/usr/lib/libQt6Core.so.6", MAPPED, ROOT))
            launch.verify(0, GOOD, MAPPED | {icu, str(host)}, ROOT)
            with self.assertRaises(ValueError):
                launch.verify(0, GOOD, MAPPED | {icu}, ROOT)


class FileAccessTrace(unittest.TestCase):
    TRACE = '''1 execve("/usr/bin/hdb-resale-explorer", [], 0x1) = 0
2 openat(AT_FDCWD, "/opt/hdb-resale-explorer/qt/lib/libQt6Core.so.6", O_RDONLY|O_CLOEXEC) = 3
2 openat(AT_FDCWD, "/opt/Qt/6.12.0/gcc_64/lib/libQt6Core.so.6", O_RDONLY|O_CLOEXEC) = -1 ENOENT (No such file or directory)
2 openat(AT_FDCWD, "/usr/share/dotnet/host/fxr/libhostfxr.so", O_RDONLY) = 4
2 openat(AT_FDCWD, "/usr/lib/qt6/plugins/x.so", O_RDONLY) = -1 ENOENT (No such file or directory)
'''

    def test_only_successful_forbidden_reads_count(self):
        hits = launch.forbidden_opens(self.TRACE, ("/opt/Qt", "/usr/share/dotnet", "/usr/lib/qt6"))
        self.assertEqual(hits, ["/usr/share/dotnet/host/fxr/libhostfxr.so"])
        self.assertEqual(launch.forbidden_opens(self.TRACE, ("/home/runner",)), [])

    def test_system_qt_opens_outside_plugin_directories_are_rejected(self):
        trace = '\n'.join(f'openat(AT_FDCWD, "{p}", O_RDONLY) = 3' for p in (
            '/usr/lib/x86_64-linux-gnu/libQt6Core.so.6', '/usr/lib64/libQt6Gui.so.6',
            '/opt/hdb-resale-explorer/qt/lib/libQt6Core.so.6'))
        self.assertEqual(launch.forbidden_opens(trace, ()), [
            '/usr/lib/x86_64-linux-gnu/libQt6Core.so.6', '/usr/lib64/libQt6Gui.so.6'])

    def test_a_host_dotnet_or_system_qt_is_always_forbidden(self):
        for root in ("/usr/share/dotnet", "/usr/lib/qt6", "/usr/lib64/qt6", "/usr/lib/dotnet"):
            self.assertIn(root, launch.SYSTEM_RUNTIME_ROOTS)


class ProcessInspection(unittest.TestCase):
    def test_process_tree_contains_the_root_and_maps_are_readable(self):
        self.assertIn(os.getpid(), launch.process_tree(os.getpid()))
        self.assertTrue(any(p.endswith("libc.so.6") or "libc" in p for p in launch.mapped_files(os.getpid())))


class Manifest(unittest.TestCase):
    def test_a_tampered_installed_file_fails_before_any_launch(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp) / "pkg"
            (root / "app").mkdir(parents=True)
            (root / "app/host").write_bytes(b"host")
            files, modes, links = launch.verify_manifest.__globals__["tree_entries"](root)
            (root / "manifest.json").write_text(json.dumps({"files": files, "modes": modes, "symlinks": links}))
            (root / "app/host").write_bytes(b"tampered")
            with self.assertRaises(ValueError):
                launch.check(root, root / "launcher", Path(temp) / "l.log", Path(temp) / "r.json", 5,
                             api_base_url="http://127.0.0.1:9/")


class WindowAppId(unittest.TestCase):
    TREE = """
xwininfo: Window id: 0x3a0 (the root window) (has no name)

  Root window id: 0x3a0 (the root window) (has no name)
  Parent window id: 0x0 (none)
     3 children:
     0x200008 "HDB Resale Explorer": ("HdbResale.App" "HdbResale.App")  1280x800+0+0  +0+0
     0x200003 (has no name): ()  1x1+0+0  +0+0
     0x200001 (has no name): ()  1x1+0+0  +0+0
"""
    REPORTED = (f'_GTK_APPLICATION_ID(UTF8_STRING) = "{launch.APPLICATION_ID}"\n'
                f'_KDE_NET_WM_DESKTOP_FILE(UTF8_STRING) = "{launch.APPLICATION_ID}"\n')
    # What Qt reports when the application sets no desktop file name: the executable's base name.
    FALLBACK = '_GTK_APPLICATION_ID(UTF8_STRING) = "HdbResale"\n_KDE_NET_WM_DESKTOP_FILE(UTF8_STRING) = "HdbResale"\n'
    ABSENT = "_GTK_APPLICATION_ID:  not found.\n_KDE_NET_WM_DESKTOP_FILE:  not found.\n"

    def test_only_the_top_level_windows_are_listed(self):
        self.assertEqual(launch.child_windows(self.TREE), ["0x200008", "0x200003", "0x200001"])

    def test_the_properties_a_window_has_are_read(self):
        self.assertEqual(launch.app_id_values(self.REPORTED),
                         dict.fromkeys(launch.APP_ID_PROPERTIES, launch.APPLICATION_ID))
        self.assertEqual(launch.app_id_values(self.ABSENT), {})

    def test_windows_without_the_properties_or_closed_before_the_query_are_skipped(self):
        outputs = {"0x200008": self.REPORTED, "0x200003": self.ABSENT, "0x200001": ""}

        def run(command, **_):
            output = self.TREE if command[0] == "xwininfo" else outputs[command[2]]
            return subprocess.CompletedProcess(command, 0 if output else 1, output, "")
        with mock.patch.object(launch.subprocess, "run", run):
            self.assertEqual(launch.window_app_ids({}), {"0x200008": launch.app_id_values(self.REPORTED)})

    def test_only_the_desktop_entry_id_in_both_properties_of_every_window_passes(self):
        launch.verify_app_id({"0x200008": launch.app_id_values(self.REPORTED)})
        half = self.REPORTED.splitlines()[0] + "\n_KDE_NET_WM_DESKTOP_FILE:  not found.\n"
        for windows in ({}, {"0x200008": launch.app_id_values(self.FALLBACK)},
                        {"0x200008": launch.app_id_values(half)},
                        {"0x200008": launch.app_id_values(self.REPORTED), "0x200009": launch.app_id_values(self.FALLBACK)}):
            with self.assertRaises(ValueError, msg=repr(windows)):
                launch.verify_app_id(windows)



class AppearancePlugins(unittest.TestCase):
    def test_records_only_loaded_plugins_and_rejects_missing_or_wrong_choices(self):
        mapped = MAPPED | {str(ROOT / "qt/plugins/platformthemes/libqgtk3.so"),
                           str(ROOT / "qt/plugins/wayland-decoration-client/libadwaita.so")}
        observed = launch.verify_appearance(mapped, "libqgtk3.so", "libadwaita.so")
        self.assertEqual(observed["platformthemes"], ["libqgtk3.so"])
        for actual, theme, decoration in ((MAPPED, "libqgtk3.so", None),
                                          (mapped, "libqxdgdesktopportal.so", None),
                                          (mapped, None, "libbradient.so")):
            with self.assertRaises(ValueError):
                launch.verify_appearance(actual, theme, decoration)
        self.assertEqual(launch.verify_appearance(MAPPED, None, None)["wayland-decoration-client"], [])


if __name__ == "__main__":
    unittest.main()
