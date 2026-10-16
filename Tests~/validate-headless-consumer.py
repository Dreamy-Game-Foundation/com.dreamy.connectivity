#!/usr/bin/env python3
"""Compile and exercise Connectivity in a fresh Unity consumer without UI packages.
Requires an installed/licensed Unity Editor and a resolved UniTask in the current consumer.
Artifacts are preserved in the printed temporary directory for inspection.
"""
import argparse
import json
from pathlib import Path
import shutil
import subprocess
import tempfile

parser = argparse.ArgumentParser()
parser.add_argument('--unity', required=True, type=Path, help='Unity Editor executable')
args = parser.parse_args()
consumer = Path.cwd()
package = Path(__file__).resolve().parents[1]
unitask = next((consumer/'Library/PackageCache').glob('com.cysharp.unitask*'), None)
if unitask is None: raise SystemExit('Resolve UniTask in the consumer first.')
fixture = Path(tempfile.mkdtemp(prefix='dreamy-connectivity-headless-'))
for directory in ['Assets/Editor', 'Packages', 'ProjectSettings']:
    (fixture/directory).mkdir(parents=True, exist_ok=True)
(fixture/'Packages/manifest.json').write_text(json.dumps({'dependencies': {
    'com.dreamy.connectivity':'file:'+str(package),
    'com.cysharp.unitask':'file:'+str(unitask),
}}, indent=2)+'\n')
shutil.copyfile(consumer/'ProjectSettings/ProjectVersion.txt', fixture/'ProjectSettings/ProjectVersion.txt')
shutil.copyfile(package/'Tests~/HeadlessConsumer/HeadlessConnectivityCheck.cs', fixture/'Assets/Editor/HeadlessConnectivityCheck.cs')
log = fixture/'validation.log'
print('Headless consumer fixture: '+str(fixture), flush=True)
result = subprocess.run([str(args.unity), '-batchmode', '-nographics', '-projectPath', str(fixture),
    '-executeMethod', 'HeadlessConnectivityCheck.Run', '-quit', '-logFile', str(log)])
output = log.read_text(errors='replace') if log.exists() else ''
if result.returncode or 'PASS_HEADLESS_CONNECTIVITY_NO_UI:' not in output:
    print('\n'.join(output.splitlines()[-40:]))
    raise SystemExit(result.returncode or 1)
resolved = json.loads((fixture/'Packages/packages-lock.json').read_text())['dependencies']
for name in ['com.dreamy.ui','com.dreamy.feature','com.unity.ugui']:
    assert name not in resolved, ('Unexpected optional UI dependency', name)
print('PASS clean Unity consumer compile and gateway check without Dreamy UI/Feature/UGUI', flush=True)
