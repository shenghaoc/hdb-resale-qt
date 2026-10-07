#!/usr/bin/env python3
"""Run the app's opt-in screenshot gate and validate what it produced.

Image generation is not visual acceptance: this tool only proves that every
scenario reached its checked preconditions and produced a non-empty, decodable,
non-uniform PNG. A person still has to look at the images.

Modes
  grab    offscreen (or any platform) Item.grabToImage of the shell, plus the popup
          overlay composited on top for dialog states.
  native  the app announces each ready state and this tool takes a real compositor
          capture of the active window (spectacle; KDE Plasma / Wayland).
Requires Pillow (development-only dependency).
"""
import argparse
import json
import os
from pathlib import Path
import subprocess
import sys
import time

sys.path.insert(0, str(Path(__file__).parent))
import synthetic_basemap

try:
    from PIL import Image, ImageStat
except ImportError:  # pragma: no cover
    sys.exit('Pillow is required: pip install pillow')


def git(root, *args):
    try:
        return subprocess.check_output(['git', *args], cwd=root, text=True).strip()
    except (OSError, subprocess.CalledProcessError):
        return 'unknown'


def validate(path: Path, minimum=(200, 150)):
    if not path.is_file() or path.stat().st_size == 0:
        raise RuntimeError(f'missing or empty image: {path}')
    with Image.open(path) as image:
        image.verify()
    with Image.open(path) as image:
        image = image.convert('RGB')
        if image.width < minimum[0] or image.height < minimum[1]:
            raise RuntimeError(f'implausibly small image {image.size}: {path}')
        if max(ImageStat.Stat(image).stddev) < 1.0:
            raise RuntimeError(f'uniform (blank) image: {path}')
        return image.size


def compose(out: Path, name: str, origin=(0, 0)):
    shell = out / f'{name}.shell.png'
    overlay = out / f'{name}.overlay.png'
    validate(shell)
    with Image.open(shell) as base:
        base = base.convert('RGBA')
        if overlay.exists():
            validate(overlay, (50, 50))
            with Image.open(overlay) as top:
                base.alpha_composite(top.convert('RGBA'), dest=(max(0, round(origin[0])), max(0, round(origin[1]))))
        base.convert('RGB').save(out / f'{name}.png')
    shell.unlink()
    overlay.unlink(missing_ok=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('--executable', type=Path, required=True)
    parser.add_argument('--out', type=Path, required=True)
    parser.add_argument('--mode', choices=['grab', 'native'], default='grab')
    parser.add_argument('--platform', help='QT_QPA_PLATFORM (default offscreen for grab, host default for native)')
    parser.add_argument('--synthetic-basemap', action='store_true', help='serve neutral synthetic tiles (no OneMap imagery)')
    parser.add_argument('--scale', help='QT_SCALE_FACTOR process-local override')
    parser.add_argument('--data-directory', type=Path, help='HDB_DATA_DIRECTORY for a synthetic or local import')
    parser.add_argument('--color-scheme', choices=['light', 'dark'], help='process-local Qt.styleHints.colorScheme request')
    parser.add_argument('--profile', choices=['sample', 'stress'], default='sample', help='stress = synthetic UI fixture via --data-directory')
    parser.add_argument('--only', help='comma-separated scenario names to run')
    parser.add_argument('--label', default='')
    parser.add_argument('--timeout', type=int, default=0, help='seconds; default 240 (600 for the stress profile)')
    args = parser.parse_args()

    root = args.executable.resolve().parent
    out = args.out.resolve()
    out.mkdir(parents=True, exist_ok=True)
    for stale in list(out.glob('*.png')) + list(out.glob('*.ack')) + [out / 'manifest.json']:
        stale.unlink(missing_ok=True)
    env = {**os.environ, 'HDB_SCREENSHOT_DIR': str(out), 'HDB_SCREENSHOT_MODE': args.mode,
           'HDB_UI_SETTINGS_FILE': str(out / 'ui.ini'), 'QML_XHR_ALLOW_FILE_READ': '1'}
    qt_dir = os.environ.get('QtDir')
    if qt_dir:
        env['LD_LIBRARY_PATH'] = f"{qt_dir}/lib:{env.get('LD_LIBRARY_PATH', '')}"
    platform = args.platform or ('offscreen' if args.mode == 'grab' else None)
    if platform:
        env['QT_QPA_PLATFORM'] = platform
    env['HDB_SCREENSHOT_PROFILE'] = args.profile
    if args.profile == 'stress' and not (args.data_directory and (args.data_directory / 'SYNTHETIC_UI_FIXTURE.txt').exists()):
        sys.exit('--profile stress requires --data-directory pointing at tools/make_ui_fixture.py output')
    if args.only:
        env['HDB_SCREENSHOT_ONLY'] = args.only
    if args.color_scheme:
        env['HDB_SCREENSHOT_COLOR_SCHEME'] = args.color_scheme
    if args.scale:
        env['QT_SCALE_FACTOR'] = args.scale
    if args.data_directory:
        env['HDB_DATA_DIRECTORY'] = str(args.data_directory.resolve())
    server = None
    if args.synthetic_basemap:
        server = synthetic_basemap.serve()
        env['HDB_SYNTHETIC_BASEMAP_HOST'] = f'http://127.0.0.1:{server.server_port}/'

    metas, ready, failed, done, popups = [], [], [], False, {}
    deadline = time.time() + (args.timeout or (600 if args.profile == 'stress' else 240))
    child = subprocess.Popen([str(args.executable.resolve())], cwd=args.executable.resolve().parent, env=env,
                             stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, bufsize=1)
    log = []
    try:
        for line in child.stdout:
            log.append(line)
            if time.time() > deadline:
                raise RuntimeError('screenshot run exceeded its deadline')
            if 'HDB_SHOT_META ' in line:
                metas.append(json.loads(line.split('HDB_SHOT_META ', 1)[1]))
            elif 'HDB_SHOT_POPUP ' in line:
                popup = json.loads(line.split('HDB_SHOT_POPUP ', 1)[1])
                popups[popup['name']] = popup
            elif 'HDB_SHOT_FAIL' in line:
                failed.append(line.strip())
            elif 'HDB_SHOTS_DONE' in line:
                done = True
            elif 'HDB_SHOT_READY ' in line:
                name = line.split('HDB_SHOT_READY ', 1)[1].strip()
                time.sleep(0.5)  # let the compositor present the final frame
                target = out / f'{name}.png'
                capture_env = {k: v for k, v in os.environ.items() if k != 'LD_LIBRARY_PATH'}
                subprocess.run(['spectacle', '-b', '-n', '-a', '-o', str(target)], check=True, env=capture_env, timeout=30)
                validate(target)
                (out / f'{name}.ack').write_text('ok')
                ready.append(name)
        child.wait(timeout=30)
    except Exception as error:
        child.kill()
        (out / 'run.log').write_text(''.join(log))
        sys.exit(f'FAILED: {error}')
    finally:
        if server:
            server.shutdown()
    (out / 'run.log').write_text(''.join(log))
    if failed or child.returncode != 0 or not done:
        sys.exit(f'FAILED (exit {child.returncode}): {failed or "no HDB_SHOTS_DONE"}\nSee {out / "run.log"}')

    names = [m['name'] for m in metas]
    if len(set(names)) != len(names) or not names:
        sys.exit(f'FAILED: unexpected scenario list {names}')
    for name in names:
        if args.mode == 'grab':
            compose(out, name, (popups[name]['x'], popups[name]['y']) if name in popups else (0, 0))
        elif name not in ready:
            sys.exit(f'FAILED: no native capture for {name}')
        size = validate(out / f'{name}.png')
        next(m for m in metas if m['name'] == name)['image'] = list(size)
    for ack in out.glob('*.ack'):
        ack.unlink()
    manifest = {'label': args.label, 'mode': args.mode, 'scale_factor_env': args.scale or '',
                'head_sha': git(root, 'rev-parse', 'HEAD'), 'branch': git(root, 'rev-parse', '--abbrev-ref', 'HEAD'),
                'uncommitted_changes': bool(git(root, 'status', '--porcelain')), 'data': 'SYNTHETIC UI stress fixture (not real data)' if args.profile == 'stress' else 'local import' if args.data_directory else 'bundled six-row development sample',
                'basemap': 'synthetic' if args.synthetic_basemap else 'OneMap (unmodified; not for publication)',
                'scenarios': metas,
                'note': 'Generated images are not visual acceptance; inspect them.'}
    (out / 'manifest.json').write_text(json.dumps(manifest, indent=2))
    print(f'OK {len(names)} images in {out} (generated, not accepted)')


if __name__ == '__main__':
    main()
