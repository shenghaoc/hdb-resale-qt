"""Keep mandatory OneMap attribution local and byte-identical to the official asset."""
import hashlib
from pathlib import Path
import unittest
import xml.etree.ElementTree as ET

class OneMapAssetTests(unittest.TestCase):
 def test_official_logo_is_bundled_and_qml_uses_its_local_resource(self):
  root=Path(__file__).resolve().parents[1]
  app=root/'src/HdbResale.App'
  data=(app/'assets/onemap-logo.png').read_bytes()
  self.assertEqual(hashlib.sha256(data).hexdigest(),'657db347017b13e71bc4942afbe985566052446b13433f2dd5492e92fce34f16')
  self.assertEqual(data[:8],b'\x89PNG\r\n\x1a\n')
  self.assertEqual((int.from_bytes(data[16:20],'big'),int.from_bytes(data[20:24],'big')),(32,32))
  resources=ET.parse(app/'HdbResale.App.csproj').getroot().findall('.//QtResource')
  self.assertTrue(any(r.attrib.get('Include')=='assets/onemap-logo.png' and r.attrib.get('Alias')=='hdb-resale/onemap-logo.png' for r in resources))
  qml=(app/'Main.qml').read_text()
  self.assertIn('source: "qrc:/hdb-resale/onemap-logo.png"',qml)
  self.assertNotIn('source: "https://www.onemap.gov.sg/web-assets/images/logo/',qml)
  self.assertIn('OneMap</a> © contributors |',qml)

if __name__=='__main__':unittest.main()
