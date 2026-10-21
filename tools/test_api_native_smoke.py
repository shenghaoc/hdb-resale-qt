import pathlib
import plistlib
import tempfile
import unittest

import api_native_smoke as smoke

INFO = {"CFBundleName": smoke.BUNDLE_NAME, "CFBundleDisplayName": smoke.BUNDLE_NAME,
        "CFBundleIdentifier": smoke.BUNDLE_IDENTIFIER, "CFBundleIconFile": "AppIcon"}


def icns(elements) -> bytes:
    body = b"".join(kind.encode("latin-1") + (12).to_bytes(4, "big") + b"png!" for kind in sorted(elements))
    return b"icns" + (8 + len(body)).to_bytes(4, "big") + body


def bundle(root: pathlib.Path, name=smoke.BUNDLE_NAME, info=INFO, icon=icns(smoke.ICON_ELEMENTS)) -> pathlib.Path:
    path = root / f"{name}.app"
    (path / "Contents/Resources").mkdir(parents=True)
    (path / "Contents/Info.plist").write_bytes(plistlib.dumps(info))
    (path / "Contents/Resources/AppIcon.icns").write_bytes(icon)
    return path


class BundleIdentityTests(unittest.TestCase):
    def test_the_named_bundle_with_its_identity_and_a_complete_icon_passes(self):
        with tempfile.TemporaryDirectory() as temp:
            self.assertEqual(smoke.bundle_problems(bundle(pathlib.Path(temp))), [])

    def test_the_previous_bundle_and_each_wrong_detail_are_reported(self):
        cases = {"HdbResale": {"name": "HdbResale"},
                 "CFBundleDisplayName": {"info": {k: v for k, v in INFO.items() if k != "CFBundleDisplayName"}},
                 "CFBundleIdentifier": {"info": INFO | {"CFBundleIdentifier": "io.github.shenghaoc.HdbResale"}},
                 "ic10": {"icon": icns(smoke.ICON_ELEMENTS - {"ic10"})},
                 "lacks": {"icon": b"not an icon"},
                 "None": {"info": {k: v for k, v in INFO.items() if k != "CFBundleIconFile"}}}
        for expected, change in cases.items():
            with tempfile.TemporaryDirectory() as temp:
                problems = smoke.bundle_problems(bundle(pathlib.Path(temp), **change))
                self.assertTrue(any(expected in problem for problem in problems), (expected, problems))

    def test_a_truncated_icon_has_no_elements(self):
        whole = icns(smoke.ICON_ELEMENTS)
        with tempfile.TemporaryDirectory() as temp:
            path = pathlib.Path(temp) / "AppIcon.icns"
            path.write_bytes(whole)
            self.assertEqual(smoke.icon_elements(path), smoke.ICON_ELEMENTS)
            path.write_bytes(whole[:-1])
            self.assertEqual(smoke.icon_elements(path), set())


if __name__ == "__main__":
    unittest.main()
