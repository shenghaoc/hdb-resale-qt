#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-3.0-or-later
"""Focused external acceptance using KDE's unmodified Appium/AT-SPI driver.

Run with KDE's dependency venv. No settings, grants, other containers or HDB
test gates are changed. Nonzero includes upstream input failures and Qt repros.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import signal
import socket
import subprocess
import sys
import time
from xml.etree import ElementTree as ET

from appium import webdriver
from appium.options.common.base import AppiumOptions
from appium.webdriver.common.appiumby import AppiumBy
from selenium.webdriver.support.ui import WebDriverWait

KDE_REF = '50e41aa9913b16e5d0a6fec921c963485aa125b4'
PRODUCTION_SNAPSHOT = '2026-10-04T15:30:00.000Z'
OVERRIDES = ('HDB_PACKAGE_SMOKE', 'HDB_API_GATE', 'HDB_API_BASE_URL',
             'HDB_TILE_TEST', 'HDB_TEST_TILE_ENDPOINT')


def stop(process):
    if process is not None and process.poll() is None:
        os.killpg(process.pid, signal.SIGTERM)
        try:
            process.wait(timeout=10)
        except subprocess.TimeoutExpired:
            os.killpg(process.pid, signal.SIGKILL)
            process.wait(timeout=5)


def fetch(base, route, path):
    subprocess.run(['curl', '--fail', '--silent', '--show-error', base + route,
                    '--output', str(path)], check=True, timeout=40)
    return json.loads(path.read_text())


def attach(pid, extension):
    options = AppiumOptions()
    options.set_capability('app', str(pid))
    options.set_capability('timeouts', {'implicit': 15000})
    return webdriver.Remote('http://127.0.0.1:4723', options=options,
                            extensions=[extension])


def snapshot(driver, path):
    source = driver.page_source
    path.write_text(source)
    return ET.fromstring(source)


def row_names(root):
    return [node.get('name') for node in root.findall('.//list_item')]


def fresh_actions(pid, name):
    code = '''import gi,json,sys
gi.require_version('Atspi','2.0')
from gi.repository import Atspi
d=Atspi.get_desktop(0)
a=next(d.get_child_at_index(i) for i in range(d.get_child_count())
       if d.get_child_at_index(i).get_process_id()==int(sys.argv[1]))
a.set_cache_mask(Atspi.Cache.NONE)
def walk(n):
 n.clear_cache_single()
 if n.get_name()==sys.argv[2]:
  action=n.get_action_iface()
  return [action.get_name(i) for i in range(action.get_n_actions())] if action else []
 for i in range(n.get_child_count()):
  c=n.get_child_at_index(i)
  result=walk(c) if c else None
  if result is not None:return result
print(json.dumps(walk(a)))
'''
    run = subprocess.run(['/usr/bin/python3','-c',code,str(pid),name],
                         capture_output=True,text=True,check=True,timeout=15)
    return json.loads(run.stdout)


def run_hdb(args, mode, extension, out):
    out.mkdir()
    env = os.environ.copy()
    for name in OVERRIDES:
        env.pop(name, None)
    env.update(QT_QPA_PLATFORM='wayland', QT_LINUX_ACCESSIBILITY_ALWAYS_ON='1',
               QSG_INFO='1', QT_FORCE_STDERR_LOGGING='1')
    fixture = app = driver = None
    failures, checks = [], []
    manifest = None
    base = 'https://hdb-resale-visualizer.shenghaoc.workers.dev'
    try:
        if mode == 'fixture':
            fixture = subprocess.Popen([sys.executable, str(args.fixture_helper)],
                stdout=subprocess.PIPE, stderr=(out/'fixture.log').open('w'),
                text=True, start_new_session=True)
            endpoint = fixture.stdout.readline().strip()
            assert endpoint.startswith('Serving recorded API responses at '), endpoint
            env['HDB_API_BASE_URL'] = endpoint.split(' at ', 1)[1]
            (out/'endpoint.txt').write_text(endpoint+'\n')
            root = args.fixture_helper.parent.parent/'tests/fixtures/worker-api'
            manifest = json.loads((root/'manifest.json').read_text())
            summaries = json.loads((root/'block-summaries.json').read_text())
        else:
            manifest = fetch(base, '/api/manifest', out/'manifest-before.json')
            summaries = fetch(base, '/api/block-summaries', out/'summaries.json')
            after = fetch(base, '/api/manifest', out/'manifest-after-summaries.json')
            assert after['generatedAt'] == manifest['generatedAt'], 'snapshot changed'
            assert manifest['generatedAt'] == PRODUCTION_SNAPSHOT, (
                'derive new search/scroll cases from the changed coherent snapshot')
        (out/'summaries.json').write_text(json.dumps(summaries)+'\n')
        expected_count = sum(row['medianPrice'] <= 1000000 for row in summaries)
        app = subprocess.Popen([str(args.executable)], env=env,
            stdout=(out/'app.log').open('w'), stderr=subprocess.STDOUT,
            start_new_session=True)
        driver = attach(app.pid, extension)
        wait = WebDriverWait(driver, 30)
        search = driver.find_element(AppiumBy.NAME, 'Search addresses')
        count_label = f'{expected_count:,} of {len(summaries):,} addresses'
        wait.until(lambda d: any(n.get('name') == count_label
                   for n in ET.fromstring(d.page_source).iter()))
        initial = snapshot(driver, out/'initial.xml')
        assert any(n.get('name') == count_label for n in initial.iter()), (
            'status count differs from saved summaries')
        checks.append('ordinary-load-count')
        search.send_keys('bedok res' if mode == 'fixture' else 'geylang 30')
        wanted = ['748A BEDOK RESERVOIR CRES', '748B BEDOK RESERVOIR CRES',
                  '747A BEDOK RESERVOIR CRES'] if mode == 'fixture' else [
                  '30 BALAM RD', '30 CASSIA CRES']
        wait.until(lambda d: all(any(n.startswith(name+', ') for n in
                   row_names(ET.fromstring(d.page_source))) for name in wanted))
        names = row_names(snapshot(driver, out/'search.xml'))
        if mode == 'fixture':
            assert [n.split(',')[0] for n in names] == wanted, names
        else:
            exact = [i for i,n in enumerate(names) if n.split(',')[0] in wanted]
            prefix = [i for i,n in enumerate(names) if n.startswith(('301 ','302 ','304 ','305 '))]
            assert len(exact) == 2 and prefix and max(exact) < min(prefix), names
        checks.append('external-editable-text-search-ranking')
        search.clear()
        search.send_keys('472748' if mode == 'fixture' else '560121')
        name = '748B BEDOK RESERVOIR CRES' if mode == 'fixture' else '121 ANG MO KIO AVE 3'
        wait.until(lambda d: any(n.get('name') == f'1 of {len(summaries):,} addresses'
                   for n in ET.fromstring(d.page_source).iter()))
        wait.until(lambda d: any(row.startswith(name+', ') for row in
                   row_names(ET.fromstring(d.page_source))))
        selected_rows = row_names(snapshot(driver, out/'postal.xml'))
        selected_rows = [row for row in selected_rows if row.startswith(name+', ')]
        assert len(selected_rows) == 1, ('expected postal row missing/duplicated',selected_rows)
        # Exact accessible-name lookup avoids the upstream XPath locator's
        # XML-path/index race while map/list children are changing. Preserve
        # earlier XPath failures as separate evidence; do not patch the driver.
        (out/'row-actions.json').write_text(json.dumps(
            fresh_actions(app.pid,selected_rows[0]))+'\n')
        driver.find_element(AppiumBy.NAME, selected_rows[0]).click()
        time.sleep(2)
        selected = snapshot(driver, out/'selected.xml')
        if any(n.get('name','').startswith(name+', ') and
               'selected' in n.get('states','') for n in selected.findall('.//list_item')):
            checks.append('external-accessibility-row-press-selection')
        else:
            failures.append('external row click did not select the address; see row-actions.json/selected.xml')
        if mode == 'production':
            fetch(base, '/api/details/ang-mo-kio-121-ang-mo-kio-ave-3', out/'detail.json')
        about = driver.find_element(AppiumBy.NAME, 'About HDB Resale Explorer')
        about.click()
        opened = snapshot(driver, out/'about.xml')
        assert opened.find('.//dialog') is not None
        assert manifest['generatedAt'] in ET.tostring(opened, encoding='unicode')
        driver.find_element(AppiumBy.NAME, 'Close').click()
        wait.until(lambda d: not ET.fromstring(d.page_source).findall('.//dialog'))
        closed = snapshot(driver, out/'about-closed.xml')
        assert driver.find_element(AppiumBy.NAME, 'Search addresses').text == (
            '472748' if mode == 'fixture' else '560121')
        assert any(n.get('name') == 'About HDB Resale Explorer' and
                   n.tag == 'button' and 'focused' in n.get('states','') for n in closed.iter())
        checks.append('About-accessibility-press-close-preserves-query-and-button-focus')
        search.clear()
        wait.until(lambda d: len(row_names(ET.fromstring(d.page_source))) > 1)
        driver.set_value(driver.find_element(AppiumBy.NAME, 'Scroll addresses'), '0.0468')
        time.sleep(.5)
        scrolled = snapshot(driver, out/'scrolled.xml')
        names = row_names(scrolled)
        if mode == 'production':
            missing = ['40 TANGLIN HALT RD','34 TANGLIN HALT RD','41 TANGLIN HALT RD',
                       '15 HOLLAND DR','82 MACPHERSON LANE']
            assert all(any(n.startswith(a+', ') for n in names) for a in missing), names
            checks.append('original-five-rows-present-after-external-value-scroll')
        else:
            assert len(names) == expected_count, names
            checks.append('fixture-row-exposure')
    except Exception as error:
        failures.append(repr(error))
        if driver:
            try:
                snapshot(driver,out/'failure.xml')
            except Exception:
                pass
    finally:
        if driver:
            driver.quit()
        stop(app); stop(fixture)
        if mode == 'production' and manifest:
            try:
                after = fetch(base, '/api/manifest', out/'manifest-final.json')
                if after['generatedAt'] != manifest['generatedAt']:
                    failures.append('snapshot changed; discard production observations')
            except Exception as error:
                failures.append('final snapshot coherence: '+repr(error))
        report = {'mode': mode, 'checks': checks, 'failures': failures,
                  'gates': 'unset', 'snapshot': manifest and manifest['generatedAt'],
                  'transport': 'external AT-SPI Action/EditableText/Value via KDE driver',
                  'compositorInput': False, 'compactResize': 'UNVERIFIED; VM required'}
        (out/'result.json').write_text(json.dumps(report, indent=2)+'\n')
    return report


def run_reduction(qml, extension, out):
    out.mkdir()
    env = os.environ.copy()
    env.update(QT_QPA_PLATFORM='wayland', QT_LINUX_ACCESSIBILITY_ALWAYS_ON='1',
               QT_FORCE_STDERR_LOGGING='1', QT_LOGGING_RULES='qml.debug=true')
    app = subprocess.Popen([str(qml), str(Path(__file__).with_name('ListRepro.qml'))],
        cwd=out, env=env, stdout=(out/'app.log').open('w'), stderr=subprocess.STDOUT,
        start_new_session=True)
    driver = None
    reports = []
    errors = []
    try:
        driver = attach(app.pid, extension)
        stages = [('initial', []), ('scroll', [('value',.5)]),
            ('restore', [('click','Filter'),('click','Hide'),('click','All'),
                         ('click','Small'),('click','Large'),('click','Hide'),('value',.5)]),
            ('end', [('value',100)])]
        for stage, actions in stages:
            for action,value in actions:
                if action == 'click':
                    driver.find_element(AppiumBy.NAME,value).click()
                else:
                    driver.set_value(driver.find_element(AppiumBy.NAME,'Scroll rows'),str(value))
                time.sleep(.3)
            time.sleep(.5)
            root = snapshot(driver,out/(stage+'.xml'))
            log = (out/'app.log').read_text()
            visible = json.loads([line.split('HDB_QT_REPRO_ROWS ',1)[1] for line in
                                  log.splitlines() if 'HDB_QT_REPRO_ROWS ' in line][-1])
            names = row_names(root)
            missing = ['row '+str(r['index']) for r in visible['rows']
                       if 'row '+str(r['index']) not in names]
            report = {'stage':stage,'visible':visible,'externalNames':names,'missing':missing}
            if stage == 'restore':
                # Independent fresh client using the existing read-only probe:
                # distinguish driver cache behavior from external Qt exposure.
                code = ('import json,sys;sys.path.insert(0,sys.argv[1]);'
                        'from atspi_probe import application_tree;'
                        'print(json.dumps(application_tree(int(sys.argv[2]))[1]))')
                fresh = subprocess.run(['/usr/bin/python3','-c',code,
                    str(Path(__file__).parent),str(app.pid)],capture_output=True,
                    text=True,check=True,timeout=15)
                (out/'restore-fresh-tree.json').write_text(fresh.stdout)
                fresh_names = [n['name'] for n in json.loads(fresh.stdout)
                               if n['role']=='list item']
                report['freshReaderMissing'] = ['row '+str(r['index']) for r in visible['rows']
                                               if 'row '+str(r['index']) not in fresh_names]
            (out/(stage+'.json')).write_text(json.dumps(report,indent=2)+'\n')
            reports.append(report)
            print('Qt reduction',qml,stage,'missing',missing,flush=True)
    except Exception as error:
        errors.append(repr(error))
    finally:
        if driver:
            driver.quit()
        stop(app)
        (out/'result.json').write_text(json.dumps({'checkpoints':reports,'errors':errors},indent=2)+'\n')
    return {'qml':str(qml),'checkpoints':reports,'errors':errors}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--kde-source',type=Path,required=True)
    parser.add_argument('--input-bin',type=Path,required=True)
    parser.add_argument('--qml',type=Path,action='append',required=True)
    parser.add_argument('--executable',type=Path,required=True)
    parser.add_argument('--fixture-helper',type=Path,required=True)
    parser.add_argument('--output',type=Path,required=True)
    args = parser.parse_args()
    for name in ('kde_source','input_bin','executable','fixture_helper','output'):
        setattr(args,name,getattr(args,name).resolve())
    args.qml = [path.resolve() for path in args.qml]
    args.output.mkdir(parents=True,exist_ok=False)
    ref = subprocess.check_output(['git','-C',str(args.kde_source),'rev-parse','HEAD'],text=True).strip()
    assert ref == KDE_REF, f'unvalidated KDE source ref: {ref}'
    assert not subprocess.check_output(['git','-C',str(args.kde_source),'diff','HEAD'],text=True)
    sys.path.insert(0,str(args.kde_source/'autotests'))
    from valuetest import SetValueCommand
    probe = socket.socket()
    probe.bind(('127.0.0.1',4723)); probe.close()  # Never replace someone else's server.
    env = os.environ.copy()
    for name in OVERRIDES:
        env.pop(name,None)
    env.update(QT_QPA_PLATFORM='wayland',GDK_BACKEND='wayland',PYTHONUNBUFFERED='1',
        WAYLAND_DEBUG='1',FLASK_APP=str(args.kde_source/'selenium-webdriver-at-spi.py'),
        PATH=str(args.input_bin)+os.pathsep+env['PATH'],
        KWIN_PID=subprocess.check_output(['pgrep','-x','kwin_wayland'],text=True).strip())
    # Do not use upstream run.rb's nested-compositor permission-check bypasses.
    server = subprocess.Popen([sys.executable,'-m','flask','run','--host','127.0.0.1',
        '--port','4723','--no-reload'],env=env,cwd=args.kde_source,
        stdout=(args.output/'driver.log').open('w'),stderr=subprocess.STDOUT,start_new_session=True)
    result = {'driverRef':ref,'upstream':[],'hdb':[],'reductions':[],
              'settingsChanged':False,'persistentGrants':False,
              'executableSha256':hashlib.sha256(args.executable.read_bytes()).hexdigest()}
    try:
        for _ in range(300):
            with socket.socket() as sock:
                if sock.connect_ex(('127.0.0.1',4723)) == 0: break
            assert server.poll() is None,'KDE driver failed to start'
            time.sleep(.1)
        else:
            raise TimeoutError('KDE driver did not listen within 30 seconds')
        tests = [(None,'examples/calculatortest.py')]
        tests += [(qml,'autotests/'+test+'.py') for qml in args.qml
                  for test in ('valuetest','textinputtest','pointerinputtest')]
        for i,(qml,test) in enumerate(tests):
            test_env = env.copy()
            if qml: test_env['QML_EXEC'] = str(qml)
            run = subprocess.run([sys.executable,str(args.kde_source/test),'-v'],env=test_env,
                capture_output=True,text=True,timeout=70)
            (args.output/f'upstream-{i}.log').write_text(run.stdout+run.stderr)
            result['upstream'].append({'test':test,'qml':str(qml),'exit':run.returncode})
            print('upstream',test,qml,'exit',run.returncode,flush=True)
        validated = [r for r in result['upstream'] if r['test'].endswith(('calculatortest.py','valuetest.py'))]
        assert validated and all(r['exit']==0 for r in validated),'AT-SPI prerequisite failed'
        for i,qml in enumerate(args.qml):
            result['reductions'].append(
                run_reduction(qml,SetValueCommand,args.output/f'qt-reduction-{i}'))
        for mode in ('fixture','production'):
            report = run_hdb(args,mode,SetValueCommand,args.output/mode)
            result['hdb'].append(report)
            print('HDB external',mode,report['checks'],report['failures'],flush=True)
    except Exception as error:
        result['runnerError'] = repr(error)
    finally:
        stop(server)
        result['failures'] = ([r for r in result['upstream'] if r['exit']] +
            [r for r in result['hdb'] if r['failures']] +
            [r for r in result['reductions'] if r['errors'] or any(c['missing'] for c in r['checkpoints'])])
        if result.get('runnerError'):
            result['failures'].append({'runnerError':result['runnerError']})
        (args.output/'result.json').write_text(json.dumps(result,indent=2)+'\n')
    return 1 if result['failures'] else 0


if __name__ == '__main__':
    raise SystemExit(main())
