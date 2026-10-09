#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-3.0-or-later
"""Read the actual application's external AT-SPI tree, never QML properties."""
import argparse
import json
from pathlib import Path
import gi

gi.require_version('Atspi', '2.0')
from gi.repository import Atspi


def descendants(pid):
    parents = {}
    for entry in Path('/proc').iterdir():
        if not entry.name.isdigit():
            continue
        try:
            status = (entry / 'status').read_text()
            parent = int(next(line.split()[1] for line in status.splitlines() if line.startswith('PPid:')))
            parents[int(entry.name)] = parent
        except (OSError, ValueError, StopIteration):
            pass
    owned = {pid}
    while True:
        expanded = owned | {child for child, parent in parents.items() if parent in owned}
        if expanded == owned:
            return owned
        owned = expanded


def read_tree(node, depth=0, out=None):
    if out is None:
        out = []
    if depth > 30 or len(out) >= 5000:
        raise RuntimeError('AT-SPI tree exceeded bounds')
    # This is a synchronous snapshot reader, not an event-loop screen reader.
    # Do not retain libatspi's child/name cache across virtualization changes.
    if depth == 0:  # libatspi permits setting the mask only on the application root
        node.set_cache_mask(Atspi.Cache.NONE)
    node.clear_cache_single()
    states = node.get_state_set()
    out.append({'depth': depth, 'name': node.get_name(), 'role': node.get_role_name(),
                'selected': states.contains(Atspi.StateType.SELECTED),
                'showing': states.contains(Atspi.StateType.SHOWING)})
    for i in range(node.get_child_count()):
        child = node.get_child_at_index(i)
        if child:
            read_tree(child, depth + 1, out)
    return out


def probe(pid, expected):
    owned = descendants(pid)
    desktop = Atspi.get_desktop(0)
    for i in range(desktop.get_child_count()):
        app = desktop.get_child_at_index(i)
        if app.get_process_id() in owned:
            tree = read_tree(app)
            rows = [n for n in tree if n['name'].startswith(expected + ', ') and n['role'] == 'list item' and n['selected']]
            lists = [n for n in tree if n['name'] == 'Matching addresses' and n['role'] == 'list']
            if not lists or not rows:
                raise AssertionError(f'external AT-SPI missing list/expected row {expected!r}: list={len(lists)} row={len(rows)}')
            return {'pid': app.get_process_id(), 'transport': 'AT-SPI2 session accessibility bus',
                    'list': lists, 'rows': rows, 'nodes': len(tree)}, tree
    raise RuntimeError('owned app not registered on external AT-SPI desktop')


def application_tree(pid):
    owned = descendants(pid)
    desktop = Atspi.get_desktop(0)
    for index in range(desktop.get_child_count()):
        app = desktop.get_child_at_index(index)
        if app.get_process_id() in owned:
            return app.get_process_id(), read_tree(app)
    raise AssertionError('owned application absent from external AT-SPI desktop')


def verify_atspi(pid, expected, summaries, output):
    actual_pid, tree = application_tree(pid)
    try:
        lists = [n for n in tree if n['name'] == 'Matching addresses' and n['role'] == 'list']
        assert len(lists) == 1, f'expected one external list, got {len(lists)}'
        observed = [n for n in tree if n['role'] == 'list item']
        matched = []
        for row in expected['rows']:
            source = summaries[row['key']]
            address = source['block'] + ' ' + source['streetName']
            sales = source['transactionCount']
            expected_name = f"{address}, {source['town']}, median S${source['medianPrice']:,.0f}, {sales:,} {'sale' if sales == 1 else 'sales'}, latest {source['latestMonth']}"
            assert row['name'] == expected_name, f'QML row does not match pinned API: {row}'
            matches = [n for n in observed if n['name'] == expected_name]
            assert len(matches) == 1, f'external row missing/duplicated: {expected_name}: {len(matches)}'
            assert matches[0]['showing'], f'external row not showing: {expected_name}'
            assert matches[0]['selected'] == row['selected'], f'external selected state wrong: {expected_name}'
            matched.append(matches[0])
        assert matched, 'no visible rows asserted'
        facts = []
        for section in expected['facts']:
            group = [n for n in tree if n['name'] == section['title'] and n['role'] in ('panel', 'grouping')]
            heading = [n for n in tree if n['name'] == section['title'] and n['role'] == 'heading']
            assert len(group) == 1 and len(heading) == 1, f'external group/heading {section["title"]}: {group} {heading}'
            for fact in section['facts']:
                name = fact['label'] + ': ' + fact['value']
                entries = [n for n in tree if n['name'] == name]
                assert len(entries) == 1, f'external fact not exposed exactly once: {name}'
                facts.append(entries[0])
        registrations = []
        for registration in expected.get('registrations', []):
            name = registration['heading'] + '. ' + registration['details']
            entries = [n for n in tree if n['name'] == name]
            assert len(entries) == 1, f'external registration not exposed exactly once: {name}'
            registrations.append(entries[0])
        report = {'checkpoint': expected['name'], 'pid': actual_pid, 'nodes': len(tree),
                  'transport': 'external AT-SPI2 bus', 'rows': matched, 'facts': facts, 'registrations': registrations}
        output.write_text(json.dumps(report, indent=2) + '\n')
        return report
    except Exception:
        output.with_suffix('.failure-tree.json').write_text(json.dumps(tree, indent=2) + '\n')
        raise


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--pid', required=True, type=int)
    parser.add_argument('--expected', default='748B BEDOK RESERVOIR CRES')
    parser.add_argument('--output', required=True, type=Path)
    parser.add_argument('--checkpoint', type=Path)
    parser.add_argument('--summaries', type=Path)
    args = parser.parse_args()
    try:
        if args.checkpoint:
            expected = json.loads(args.checkpoint.read_text())
            summaries = {row['addressKey']:row for row in json.loads(args.summaries.read_text())}
            report = verify_atspi(args.pid, expected, summaries, args.output)
        else:
            report, tree = probe(args.pid, args.expected)
        args.output.write_text(json.dumps(report, indent=2) + '\n')
        print('HDB_ATSPI_PASS ' + json.dumps(report))
    except Exception as error:
        args.output.write_text(json.dumps({'error': str(error)}, indent=2) + '\n')
        raise


if __name__ == '__main__':
    main()
