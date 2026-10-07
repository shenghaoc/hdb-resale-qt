#!/usr/bin/env python3
"""Dump and check the application's AT-SPI accessibility tree.

Launches the app with Qt's accessibility bridge forced on, walks its tree through
AT-SPI and checks metadata a screen reader depends on (names, roles, focusability).
This verifies *exposed metadata*. It is not a screen-reader session and does not
verify spoken output, focus announcements or speech order.
Requires python3-gobject with Atspi (system python, not a venv).
"""
import argparse
import json
import os
from pathlib import Path
import subprocess
import sys
import time

import gi
gi.require_version('Atspi', '2.0')
from gi.repository import Atspi  # noqa: E402

INTERACTIVE = {'push button', 'toggle button', 'check box', 'combo box', 'spin button', 'text', 'entry',
               'list item', 'page tab', 'menu item', 'slider', 'scroll bar'}


# Inner TextInput/Text nodes created by the style for combo and spin boxes; the owning control is named.
KNOWN_STYLE_INTERNALS = {'text'}


def walk(node, depth=0, out=None):
    out = [] if out is None else out
    try:
        role = node.get_role_name()
        name = node.get_name() or ''
        states = node.get_state_set()
        out.append({'depth': depth, 'role': role, 'name': name, 'description': node.get_description() or '',
                    'focusable': states.contains(Atspi.StateType.FOCUSABLE), 'showing': states.contains(Atspi.StateType.SHOWING),
                    'selected': states.contains(Atspi.StateType.SELECTED), 'enabled': states.contains(Atspi.StateType.ENABLED)})
        for i in range(node.get_child_count()):
            child = node.get_child_at_index(i)
            if child is not None:
                walk(child, depth + 1, out)
    except Exception as error:  # a vanished node must not abort the audit
        out.append({'depth': depth, 'role': 'error', 'name': str(error), 'focusable': False, 'showing': False, 'selected': False, 'enabled': False, 'description': ''})
    return out


# Metadata a screen-reader user needs; each (role, name fragment) must be exposed and showing.
EXPECT_INITIAL = [('combo box', 'Town filter'), ('combo box', 'Flat type filter'), ('spin button', 'Minimum resale price'),
                  ('spin button', 'Maximum resale price'), ('combo box', 'Registration month window'),
                  ('button', 'Zoom in'), ('button', 'Return to Singapore view'), ('filler', 'Matching address results'),
                  ('button', 'median')]
EXPECT_SELECTED = [('button', 'Back to all addresses'), ('button', 'Show on map'), ('heading', '')]
# Roles that must not appear while they carry nothing to announce.
FORBID = [('alert', '⚠ ')]


def find(nodes, role, fragment):
    return [n for n in nodes if n['role'] == role and fragment in n['name'] and n['showing']]


def press_first_address(app):
    """Invoke the first address row's default action, as an assistive technology would."""
    def search(node):
        if (node.get_role_name() == 'button' and 'median' in (node.get_name() or '')):
            return node
        for i in range(node.get_child_count()):
            child = node.get_child_at_index(i)
            found = child and search(child)
            if found:
                return found
    row = search(app)
    if row is None:
        return False
    action = row.get_action_iface()
    return bool(action and action.get_n_actions() > 0 and action.do_action(0))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--executable', type=Path, required=True)
    parser.add_argument('--out', type=Path, required=True)
    parser.add_argument('--platform', default=None)
    parser.add_argument('--data-directory', type=Path)
    parser.add_argument('--select-first', action='store_true', help='select the first address through the app, then dump')
    args = parser.parse_args()
    env = {**os.environ, 'QT_LINUX_ACCESSIBILITY_ALWAYS_ON': '1', 'QT_ACCESSIBILITY': '1',
           'HDB_UI_SETTINGS_FILE': str(args.out.with_suffix('.ini'))}
    if os.environ.get('QtDir'):
        env['LD_LIBRARY_PATH'] = os.environ['QtDir'] + '/lib'
    if args.platform:
        env['QT_QPA_PLATFORM'] = args.platform
    if args.data_directory:
        env['HDB_DATA_DIRECTORY'] = str(args.data_directory)
    child = subprocess.Popen([str(args.executable.resolve())], cwd=args.executable.resolve().parent, env=env,
                             stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    try:
        app = None
        for _ in range(60):
            time.sleep(0.5)
            desktop = Atspi.get_desktop(0)
            for i in range(desktop.get_child_count()):
                candidate = desktop.get_child_at_index(i)
                if candidate and candidate.get_process_id() == child.pid:
                    app = candidate
            if app:
                break
        if app is None:
            sys.exit('FAILED: application did not appear on the accessibility bus')
        time.sleep(4)  # map and models settle
        nodes = walk(app)
        selected_nodes = []
        if args.select_first:
            if not press_first_address(app):
                sys.exit('FAILED: no address row exposes a press action')
            time.sleep(2)
            selected_nodes = walk(app)
    finally:
        child.terminate()
        try:
            child.wait(timeout=10)
        except subprocess.TimeoutExpired:
            child.kill()
    args.out.write_text(json.dumps({'initial': nodes, 'selected': selected_nodes}, indent=1))
    failures = []
    for label, tree, expected in (('initial', nodes, EXPECT_INITIAL), ('selected', selected_nodes, EXPECT_SELECTED)):
        if label == 'selected' and not args.select_first:
            continue
        for role, fragment in expected:
            if not find(tree, role, fragment):
                failures.append(f'{label}: missing showing {role} containing "{fragment}"')
        for role, name in FORBID:
            if [n for n in tree if n['role'] == role and n['name'] == name]:
                failures.append(f'{label}: empty {role} node exposed')
        failures += [f'{label}: unnamed showing {n["role"]} (depth {n["depth"]})' for n in tree
                     if n['role'] in INTERACTIVE and n['showing'] and not n['name'].strip() and n['role'] not in KNOWN_STYLE_INTERNALS]
    print(f'{len(nodes)} accessible nodes initially' + (f', {len(selected_nodes)} after selecting' if selected_nodes else ''))
    print('Style-provided inner text nodes of combo/spin boxes are unnamed and not checked (Qt Quick Controls internals).')
    for failure in failures:
        print('  FAIL', failure)
    sys.exit(1 if failures else 0)


if __name__ == '__main__':
    main()
