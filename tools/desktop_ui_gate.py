#!/usr/bin/env python3
"""Opt-in native window resize, keyboard isolation and focus restoration gate."""
import argparse
import os
from pathlib import Path
import re
import subprocess
import sys
from native_gate import FORBIDDEN

STEPS = ['loaded', 'minimum-layout', 'long-label', 'list-keyboard', 'selected', 'map-keyboard',
         'editing-isolated', 'modal-isolated', 'focus-restored', 'reset-medium',
         'system-font-scaling', 'settings', 'tab-order', 'back-navigation']

def run(executable, output, negative=False):
    env = {**os.environ, 'HDB_DESKTOP_UI_GATE': '1',
           'HDB_DESKTOP_UI_FAULT': 'skip-pass' if negative else '',
           'HDB_UI_SETTINGS_FILE': str(output.resolve().with_suffix('.ini'))}
    with subprocess.Popen([str(executable.resolve())], cwd=executable.resolve().parent,
                          env=env, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                          text=True) as child:
        try:
            log, _ = child.communicate(timeout=30)
        except subprocess.TimeoutExpired:
            child.kill()
            log, _ = child.communicate()
            output.write_text(log)
            raise RuntimeError('Desktop gate process/teardown deadline')
    output.write_text(log)
    if child.returncode != 0 or FORBIDDEN.search(log):
        raise RuntimeError('Native runtime error:\n' + log)
    if re.findall(r'HDB_DESKTOP_STEP ([a-z-]+)', log) != STEPS or log.count('HDB_GATE_EXIT') != 1:
        raise RuntimeError('Missing transition/clean teardown:\n' + log)
    if negative:
        if log.count('HDB_DESKTOP_FAIL negative control') != 1 or 'HDB_DESKTOP_PASS' in log:
            raise RuntimeError('Negative control was not detected:\n' + log)
    elif log.count('HDB_DESKTOP_PASS') != 1 or 'HDB_DESKTOP_FAIL' in log:
        raise RuntimeError('Desktop assertion failed:\n' + log)
    return log

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--executable', type=Path, required=True)
    parser.add_argument('--log', type=Path, required=True)
    parser.add_argument('--negative', action='store_true')
    args = parser.parse_args()
    try:
        print(run(args.executable, args.log, args.negative), end='')
    except (RuntimeError, OSError) as error:
        print(error, file=sys.stderr)
        sys.exit(1)
