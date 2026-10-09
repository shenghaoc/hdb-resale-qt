#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-3.0-or-later
"""Run the opt-in Qt gate and independent, read-only AT-SPI checks on Wayland.

Use system Python for GI. No OS settings or unrelated processes are modified.
Screenshots stay local and contain the inspector pane, never map tiles.
"""
import argparse
import json
import os
from pathlib import Path
import signal
import subprocess
import sys
import time

from atspi_probe import probe


def read_api(base, route, path):
    # Use the runbook's curl client. This Worker rejects urllib's request
    # fingerprint with HTTP 403/1010; ordinary curl GET succeeds.
    subprocess.run(['curl', '--fail', '--silent', '--show-error', base+route,
                    '--output', str(path)], check=True, timeout=35)
    return json.loads(path.read_text())



def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--executable', required=True, type=Path)
    parser.add_argument('--mode', choices=('poc', 'fixture', 'production'), required=True)
    parser.add_argument('--output', required=True, type=Path)
    parser.add_argument('--fixture-helper', required=True, type=Path)
    args = parser.parse_args()
    out = args.output.resolve()
    out.mkdir(parents=True, exist_ok=False)  # Never overwrite another run's evidence.
    executable = args.executable.resolve()
    helper = args.fixture_helper.resolve()
    base = 'https://hdb-resale-visualizer.shenghaoc.workers.dev'
    fixture_root = helper.parent.parent / 'tests/fixtures/worker-api'
    server = app = None
    failures = []
    reports = []
    captures = []
    try:
        if args.mode in ('poc', 'fixture'):
            summaries = json.loads((fixture_root/'block-summaries.json').read_text())
            manifest = json.loads((fixture_root/'manifest.json').read_text())
            server = subprocess.Popen([sys.executable, str(helper)], stdout=subprocess.PIPE,
                                      stderr=(out/'fixture.log').open('w'), text=True, start_new_session=True)
            line = server.stdout.readline().strip()
            assert line.startswith('Serving recorded API responses at '), line
            fixture_base = line.split(' at ', 1)[1]
            (out/'endpoint.txt').write_text(line+'\n')
        else:
            manifest = read_api(base, '/api/manifest', out/'manifest-before.json')
            summaries = read_api(base, '/api/block-summaries', out/'summaries.json')
            after = read_api(base, '/api/manifest', out/'manifest-after-summaries.json')
            assert manifest['generatedAt'] == after['generatedAt'], 'snapshot changed during summaries'
            read_api(base, '/api/details/ang-mo-kio-121-ang-mo-kio-ave-3', out/'detail-121.json')
        (out/'summaries.json').write_text(json.dumps(summaries)+'\n')
        expected_default = sum(row['medianPrice'] <= 1000000 for row in summaries)
        env = os.environ.copy()
        for name in ('HDB_PACKAGE_SMOKE','HDB_API_GATE','HDB_API_BASE_URL','HDB_TILE_TEST','HDB_TEST_TILE_ENDPOINT'):
            env.pop(name, None)
        env.update(QT_QPA_PLATFORM='wayland', QSG_INFO='1', QT_LINUX_ACCESSIBILITY_ALWAYS_ON='1',
                   HDB_API_GATE='acceptance-'+args.mode)
        if args.mode in ('poc', 'fixture'):
            env['HDB_API_BASE_URL'] = fixture_base
        # Preserve the session-selected style; reduce unrelated pointer diagnostic noise.
        env['QT_LOGGING_RULES'] = 'qt.quick.controls.style=true;qt.quick.controls.qtquickcontrols2plugin=true'
        if args.mode == 'poc':
            keyboard = subprocess.run([sys.executable, str(Path(__file__).resolve().parent.parent/'api_native_smoke.py'),
                '--executable',str(executable),'--mode','keyboard','--log',str(out/'keyboard.log')],
                env=env, capture_output=True, text=True, timeout=55)
            (out/'keyboard.result').write_text(keyboard.stdout+keyboard.stderr)
            assert keyboard.returncode == 0, 'existing keyboard gate failed'
        with (out/'app.log').open('w') as log:
            app = subprocess.Popen([str(executable)], cwd=out, env=env, stdout=log,
                                   stderr=subprocess.STDOUT, start_new_session=True)
            read_at = 0
            deadline = time.monotonic() + 180
            while app.poll() is None and time.monotonic() < deadline:
                text = (out/'app.log').read_text()
                lines = text.splitlines()
                for line in lines[read_at:]:
                    if 'HDB_ACCEPTANCE_ATSPI ' in line:
                        expected = json.loads(line.split('HDB_ACCEPTANCE_ATSPI ',1)[1])
                        try:
                            expectation = out/('expected-'+expected['name']+'.json')
                            expectation.write_text(json.dumps(expected)+'\n')
                            destination = out/('atspi-'+expected['name']+'.json')
                            # A fresh libatspi client per snapshot avoids a polling
                            # reader's unpumped event/cache state affecting the oracle.
                            external = subprocess.run([sys.executable, str(Path(__file__).with_name('atspi_probe.py')),
                                '--pid',str(app.pid),'--checkpoint',str(expectation),'--summaries',str(out/'summaries.json'),
                                '--output',str(destination)],capture_output=True,text=True,timeout=10)
                            (out/('atspi-'+expected['name']+'.log')).write_text(external.stdout+external.stderr)
                            if external.returncode:
                                raise AssertionError(json.loads(destination.read_text()).get('error',external.stderr))
                            report = json.loads(destination.read_text())
                            reports.append(report)
                            print('AT-SPI PASS',expected['name'],len(report['rows']),'rows',flush=True)
                        except Exception as error:
                            failures.append(f'AT-SPI {expected["name"]}: {error}')
                            print(failures[-1],flush=True)
                    elif args.mode == 'poc' and 'HDB_ACCEPTANCE_READY' in line:
                        try:
                            report, _ = probe(app.pid, '748B BEDOK RESERVOIR CRES')
                            (out/'atspi-poc.json').write_text(json.dumps(report,indent=2)+'\n')
                            reports.append(report)
                            print('AT-SPI PASS poc', flush=True)
                        except Exception as error:
                            failures.append('AT-SPI poc: '+str(error))
                    elif args.mode != 'poc' and 'HDB_ACCEPTANCE_CAPTURE ' in line:
                        captures.append(json.loads(line.split('HDB_ACCEPTANCE_CAPTURE ',1)[1]))
                read_at = len(lines)
                time.sleep(.05)
            if app.poll() is None:
                raise TimeoutError('Qt gate exceeded 180 seconds')
        text = (out/'app.log').read_text()
        assert app.returncode == 0, f'app exit {app.returncode}'
        if args.mode == 'poc':
            assert 'HDB_ACCEPTANCE_PASS poc' in text, 'PoC gate did not pass'
            assert len(reports) == 1, 'external PoC assertion missing'
            assert (out/'acceptance-poc.png').stat().st_size > 1000, 'PoC capture missing/empty'
            captures.append({'file':'acceptance-poc.png'})
        else:
            assert 'HDB_ACCEPTANCE_COMPLETE '+args.mode in text, 'Qt gate did not complete'
            failures.extend(line for line in text.splitlines() if 'HDB_ACCEPTANCE_DEFECT' in line)
            assert 'HDB_ACCEPTANCE_FAIL' not in text, 'Qt gate reported failure'
            assert f'defaultCount={expected_default}' in text, 'app count differs from pinned API'
            assert manifest['generatedAt'] in text, 'app dataset summary differs from pinned API'
            assert len(reports) == 6, f'external checkpoints: {len(reports)}/6'
            assert len(captures) == 3, f'captures: {len(captures)}/3'
    except Exception as error:
        failures.append(str(error))
    finally:
        for process in (app,server):
            if process is not None and process.poll() is None:
                os.killpg(process.pid, signal.SIGTERM)
                process.wait(timeout=10)
        if args.mode == 'production':
            try:
                after = read_api(base, '/api/manifest', out/'manifest-final.json')
                if after['generatedAt'] != manifest['generatedAt']:
                    failures.append('snapshot changed; discard production results')
            except Exception as error:
                failures.append('final coherence: '+str(error))
        report = {'mode':args.mode,'failures':failures,'atspi':reports,'captures':captures,
                  'executable':str(executable),'input':'QtTest in-process; not compositor input',
                  'settingsChanged':False}
        (out/'result.json').write_text(json.dumps(report,indent=2)+'\n')
    print(json.dumps({'mode':args.mode,'failures':failures,'atspiCheckpoints':len(reports),'captures':len(captures)}),flush=True)
    return 1 if failures else 0


if __name__ == '__main__':
    raise SystemExit(main())
