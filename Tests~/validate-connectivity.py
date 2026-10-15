#!/usr/bin/env python3
"""Validate package/consumer assets and compile actual C# Android/iOS branches.
Run from the Unity consumer root after Unity has generated project files.
Does not replace Unity tests, native linking or device verification.
"""
from pathlib import Path
import argparse
import json
import re
import subprocess
import tempfile
import xml.etree.ElementTree as ET

parser = argparse.ArgumentParser()
parser.add_argument('--compile-platforms', action='store_true')
args = parser.parse_args()
root = Path.cwd()
package = Path(__file__).resolve().parents[1]
metadata = json.loads((package / 'package.json').read_text())
assemblies = {}
for path in package.rglob('*.asmdef'):
    data = json.loads(path.read_text())
    assert data['name'] not in assemblies, ('duplicate assembly', path)
    assemblies[data['name']] = data
    if not '/Editor/' in str(path) and not '/Tests/' in str(path):
        assert not any('.Editor' in reference for reference in data['references']), path
for source in (package / 'Runtime').rglob('*.cs'):
    assert not re.search(r'\bUnity(?:Engine|Editor)\b', source.read_text()), source
assert assemblies['Dreamy.Connectivity.Runtime']['noEngineReferences']
for sample in metadata['samples']:
    directory = package / sample['path']
    assert directory.is_dir() and (directory / 'README.md').exists(), directory
    imported = root / 'Assets/Samples/Dreamy Connectivity/0.1.0' / sample['displayName']
    for file in directory.rglob('*'):
        if file.is_file() and file.suffix in ('.cs', '.asmdef', '.prefab', '.unity', '.meta'):
            assert file.with_name(file.name + '.meta').exists() or file.suffix == '.meta', file
            if imported.exists():
                assert (imported / file.relative_to(directory)).read_bytes() == file.read_bytes(), ('source/import drift', file)
for file in (package / 'Samples~').rglob('*.prefab'):
    text = file.read_text()
    assert not re.search(r'm_Script: \{fileID: 0\}', text), file
assert 'Diagnostic' not in (package / 'Samples~/Connectivity Feature/Runtime/ConnectivityRoot.cs').read_text()
# Validate declared dependencies against source-owned assembly edges.
assert 'com.dreamy.ui' in metadata['dependencies'] and 'com.cysharp.unitask' in metadata['dependencies']
print('PASS package boundaries, sample declarations, source/import equality and asset metadata', flush=True)
if args.compile_platforms:
    with tempfile.TemporaryDirectory(prefix='dreamy-connectivity-') as temp:
        temp = Path(temp)
        (temp / 'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>')
        references = {}
        for name in ['Dreamy.Connectivity.Unity.csproj', 'Dreamy.UI.Runtime.csproj']:
            for reference in ET.parse(root / name).findall('.//Reference'):
                hint = reference.find('HintPath')
                if hint is not None:
                    dll = Path(hint.text)
                    if not dll.is_absolute(): dll = root / dll
                    if dll.exists(): references[dll.stem] = dll
        references['UniTask'] = root / 'Library/ScriptAssemblies/UniTask.dll'
        for platform in ['UNITY_ANDROID', 'UNITY_IOS']:
            project = ET.Element('Project', Sdk='Microsoft.NET.Sdk')
            props = ET.SubElement(project, 'PropertyGroup')
            for key, value in {'TargetFramework':'net10.0','EnableDefaultCompileItems':'false','GenerateAssemblyInfo':'false',
                               'DefineConstants':platform, 'NoWarn':'0649;0067','AssemblyName':'ConnectivityPlatformCheck'}.items():
                ET.SubElement(props,key).text=value
            items = ET.SubElement(project,'ItemGroup')
            for directory in ['Runtime','Integration/Unity']:
                for source in (package / directory).rglob('*.cs'): ET.SubElement(items,'Compile',Include=str(source))
            for name,dll in references.items():
                if name.startswith('Dreamy.Connectivity'): continue
                ref = ET.SubElement(items,'Reference',Include=name)
                ET.SubElement(ref,'HintPath').text=str(dll)
            path=temp / (platform + '.csproj');ET.indent(project);ET.ElementTree(project).write(path,encoding='unicode')
            result = subprocess.run(['dotnet','build',str(path),'--nologo','-v:q','--configfile',str(temp/'NuGet.Config')],cwd=root,capture_output=True,text=True)
            if result.returncode: print(result.stdout+result.stderr);raise SystemExit(result.returncode)
            print('PASS C# compile '+platform+' (native linking/device behavior not tested)',flush=True)
