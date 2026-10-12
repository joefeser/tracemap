#!/usr/bin/env python3
"""Public SQL matrix build/scan only. Never invoke fixture methods or a database."""
import argparse
import json
import pathlib
import shutil
import subprocess
from sql_matrix_expectations import load_expectations, assert_calls, assert_compiled_cases

parser = argparse.ArgumentParser()
parser.add_argument('--output',type=pathlib.Path,required=True)
parser.add_argument('--configuration',default='Debug')
args = parser.parse_args()
root = pathlib.Path(__file__).resolve().parents[2]
args.output.mkdir(parents=True,exist_ok=True)
cli = ['dotnet',str(root / f'src/dotnet/TraceMap.Cli/bin/{args.configuration}/net10.0/tracemap.dll')]
outputs = []
def run(command):
    p = subprocess.run(command,cwd=root,capture_output=True,text=True)
    if p.returncode: raise RuntimeError(p.stdout+p.stderr)
for tfm in ['net20','net35','net40','net45','net452','net472','net48','net10.0']:
    for language,extension in [('csharp','.cs'),('vb','.vb')]:
        sample = root/'samples/sql-database-matrix'/tfm/language
        cases,controls = load_expectations(sample,language,sample.parent/'OVERLOADS.md',root/'samples/sql-database-matrix/CONTROL-EXPECTATIONS.json')
        for mode in ['semantic','projectless','compiled']:
            prefix = f'{tfm}-{language}-{mode}'.replace('.','-')
            repo = sample; options = []
            if mode != 'semantic':
                repo = args.output/(prefix+'-source'); repo.mkdir(exist_ok=True)
                run(['git','init','-q',str(repo)])
                run(['git','-C',str(repo),'config','user.name','Public Fixture'])
                run(['git','-C',str(repo),'config','user.email','synthetic@example.invalid'])
                if mode == 'projectless':
                    for file in sample.glob('*'+extension): shutil.copyfile(file,repo/file.name)
                else: options = ['--compiled-input',str(sample/f'bin/{args.configuration}/{tfm}/SqlMatrix.{tfm}.{language}.dll'),'--il-body-evidence']
                run(['git','-C',str(repo),'add','.']);run(['git','-C',str(repo),'commit','-qm','Public SQL fixture','--allow-empty'])
            pair=[]
            for repeat in ['a','b']:
                out=args.output/(prefix+'-'+repeat);pair.append(out);outputs.append(str(out))
                run(cli+['scan','--repo',str(repo),'--out',str(out)]+options)
            for artifact in ['facts.ndjson','report.md']:
                assert (pair[0]/artifact).read_bytes()==(pair[1]/artifact).read_bytes(),(prefix,artifact)
            facts=[json.loads(line) for line in (pair[0]/'facts.ndjson').read_text(encoding='utf-8').splitlines()]
            if mode=='compiled':
                assert not any(f['factType']=='DatabaseOperationCandidate' for f in facts)
                assert_compiled_cases(facts,cases)
            else:
                assert_calls(facts,mode,cases,controls)
                if mode=='semantic':
                    assert json.loads((pair[0]/'scan-manifest.json').read_text(encoding='utf-8'))['buildStatus']=='Succeeded',prefix
                surface = 'sql-query' if '.Fill(' in next(iter(cases.values())) else 'sql-persistence'
                run(cli+['paths','--index',str(pair[0]/'index.sqlite'),'--out',str(args.output/(prefix+'-paths')),'--from-symbol',next(iter(cases)),'--to-surface',surface,'--format','json'])
                report=json.loads((args.output/(prefix+'-paths/paths-report.json')).read_text(encoding='utf-8'))
                assert 'databaseCallEvidence' in json.dumps(report),prefix
            print(prefix+' individual cases and controls passed',flush=True)
run(['python' if shutil.which('python') else 'python3',str(root/'scripts/validate-adapter-artifacts.py'),*outputs])
print(f'{len(outputs)} deterministic SQL CLI outputs validated; no methods executed.')
