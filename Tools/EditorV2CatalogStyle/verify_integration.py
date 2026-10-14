"""Exercise the installer twice on a scratch checkout; never change the active worktree."""
import argparse
import hashlib
import importlib.util
import json
import pathlib
import shutil
import subprocess
import sys

BASE=pathlib.Path(__file__).resolve().parent
parser=argparse.ArgumentParser();parser.add_argument('--project',type=pathlib.Path,required=True)
args=parser.parse_args()
spec=importlib.util.spec_from_file_location('catalog_install',BASE/'integrate_catalog_style.py')
install=importlib.util.module_from_spec(spec);spec.loader.exec_module(install)
scratch=BASE.parents[1]/'.tmp/catalog-options-r8-check/project'
for rel in [install.CATALOG,install.LAYOUT]:
    dest=scratch/rel;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(args.project/rel,dest)
command=[sys.executable,str(BASE/'integrate_catalog_style.py'),'--project',str(scratch)]
first=subprocess.run(command,capture_output=True,text=True,check=True)
before={str(p.relative_to(scratch)):hashlib.sha256(p.read_bytes()).hexdigest() for p in (scratch/'Assets').rglob('*') if p.is_file()}
second=subprocess.run(command,capture_output=True,text=True,check=True)
after={str(p.relative_to(scratch)):hashlib.sha256(p.read_bytes()).hexdigest() for p in (scratch/'Assets').rglob('*') if p.is_file()}
assert before==after,'Second integration changed the installed result.'
assert json.loads(second.stdout)['changed']==[],'Second integration must be a no-op.'
catalog=(scratch/install.CATALOG).read_text(encoding='utf-8')
assert '() => SetTab(captured)' in catalog and '() => SelectCategory(captured)' in catalog
assert 'caption.fontSizeMax=i==categoryIndex?17:i==5?10:14;' not in catalog
assert '(i == categoryIndex ? recoleta : inter)' not in catalog
assert catalog.count('BistroBuilderCatalogOptionStyle.SetSelected(')==2
layout=(scratch/install.LAYOUT).read_text(encoding='utf-8')
assert 'Times New Roman' not in layout and 'Arial' not in layout
result={'installer':'PASS','idempotence':'PASS','preserved_native_callbacks':True,
    'uniform_control_fonts':True,'file_hashes':after}
(BASE/'evidence/integration-check.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in result.items() if k!='file_hashes'}))
