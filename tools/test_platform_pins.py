"""Evaluate actual MSBuild package selection without restoring other platforms."""
import json
from pathlib import Path
import shutil
import subprocess
import unittest


@unittest.skipUnless(shutil.which('dotnet'), 'Platform pin evaluation requires the pinned .NET SDK.')
class PlatformPinTests(unittest.TestCase):
    def test_linux_pin_does_not_change_other_platform_pins(self):
        root = Path(__file__).resolve().parents[1]
        project = root / 'src/HdbResale.App/HdbResale.App.csproj'
        for rid in ('linux-x64', 'osx-arm64', 'osx-x64', 'win-x64', 'win-arm64'):
            with self.subTest(rid=rid):
                result = subprocess.run(
                    ['dotnet', 'msbuild', str(project),
                     f'-p:QtBridgeTemplateRid={rid}',
                     '-getProperty:QtBridgePackageId,QtBridgePackageVersion'],
                    cwd=root, check=True, text=True, capture_output=True, timeout=30)
                properties = json.loads(result.stdout)['Properties']
                self.assertEqual(properties['QtBridgePackageId'], 'QtGroup.Qt.Bridge.CSharp.' + rid)
                self.assertEqual(properties['QtBridgePackageVersion'],
                                 '0.4.0-beta' if rid == 'linux-x64' else '0.4.0.22-beta')
