#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-3.0-or-later
"""Check pixels from Qt item captures; never claim compositor/frame coverage."""
import argparse
import json
import math
from pathlib import Path
from PIL import Image, ImageChops, ImageColor


def verify(output):
    report = json.loads((output/'result.json').read_text())
    captures = {entry['file']: entry for entry in report['captures']}
    assert len(captures) == 3, 'three captures required'
    images = {name: Image.open(output/name).convert('RGBA') for name in captures}
    baseline = images['inspector-top.png']
    for name, picture in images.items():
        assert picture.size == baseline.size, f'image geometry differs: {name}'
        assert picture.getextrema()[3] == (255,255), f'capture lacks opaque pane background: {name}'
    meta = captures['inspector-top.png']
    # Selected heading + status, above the scroll viewport. The search field's
    # focus ring legitimately changes when the inspector receives QtTest input,
    # so it is intentionally outside this clipping sentinel.
    sentinel = (0, math.ceil(meta['headerY']+30), baseline.width, math.floor(meta['scroll']['y']-2))
    assert sentinel[3] > sentinel[1]+10, 'header sentinel has no area'
    original = baseline.crop(sentinel).convert('RGB')
    for name in ('chart-bottom-clip.png','chart-top-clip.png'):
        assert ImageChops.difference(original,images[name].crop(sentinel).convert('RGB')).getbbox() is None, f'chart scroll altered pixels above clipping boundary: {name}'
    bottom = captures['chart-bottom-clip.png']
    top = captures['chart-top-clip.png']
    assert 0 < bottom['chart']['y'] < bottom['scroll']['height'] < bottom['chart']['y']+bottom['chart']['height'], 'chart must cross bottom boundary'
    assert top['chart']['y'] < 0 < top['chart']['y']+top['chart']['height'], 'chart must cross top boundary'
    scroll = top['scroll']
    pixels = images['chart-top-clip.png'].crop((math.ceil(scroll['x']), math.ceil(scroll['y']), math.floor(scroll['x']+scroll['width']-15), math.floor(scroll['y']+scroll['height'])))
    colour = ImageColor.getrgb(top['palette']['link'])
    matching = sum(max(abs(pixel[i]-colour[i]) for i in range(3))<=12 for pixel in pixels.get_flattened_data())
    assert matching>=20, f'chart link-colour pixels absent: {matching}'
    result = {'result':'PASS','scope':'Qt-rendered inspector subtree only','headerSentinel':sentinel,
              'clipping':'chart crosses both edges; fixed heading pixels unchanged','linkColourPixels':matching,
              'palette':top['palette'],'font':top['font']}
    (output/'pixels.json').write_text(json.dumps(result,indent=2)+'\n')
    return result


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('output',type=Path)
    args=parser.parse_args()
    print(json.dumps(verify(args.output)))
