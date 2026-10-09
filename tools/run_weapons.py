"""Локальный запускатель: обнаружение Blender, API-проба, пилот, затем полный набор."""
import argparse
import json
import os
import shutil
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]


def find_blender(explicit=None):
    if explicit:
        path=Path(explicit).expanduser()
        if not path.is_file():raise ValueError('Blender не найден по указанному пути')
        return str(path.resolve())
    found=shutil.which('blender')
    if found:return found
    candidates=[]
    for folder in ('ProgramFiles','ProgramFiles(x86)'):
        base=os.environ.get(folder)
        if base:candidates.extend(Path(base,'Blender Foundation').glob('Blender */blender.exe'))
    candidates.extend([Path('/Applications/Blender.app/Contents/MacOS/Blender')])
    candidates=sorted({p.resolve() for p in candidates if p.is_file()})
    if len(candidates)==1:return str(candidates[0])
    if len(candidates)>1:raise ValueError('Найдено несколько Blender; выберите точный путь через --blender')
    raise ValueError('Blender не найден. Укажите --blender /полный/путь/к/blender')


def version_allowed(version,allow_newer=False):
    if not isinstance(version,list) or len(version)!=3 or any(type(v)is not int or v<0 for v in version):return False
    return tuple(version[:2])==(4,5) or (allow_newer and tuple(version[:2])>(4,5))


def command(exe,script,args):
    return [exe,'--background','--factory-startup','--python-exit-code','1',
            '--python',str(ROOT/'tools/blender'/script),'--',*map(str,args)]


def run_checked(cmd,log,timeout=600):
    # Не использовать shell; потоковый лог не растёт в памяти Python.
    with Path(log).open('w',encoding='utf-8') as stream:
        try:
            result=subprocess.run(cmd,cwd=ROOT,stdout=stream,stderr=subprocess.STDOUT,
                                  timeout=timeout,check=False,shell=False)
        except subprocess.TimeoutExpired as error:
            raise RuntimeError('Время ожидания истекло; см. '+str(log)) from error
    if result.returncode:raise RuntimeError('Ошибка процесса; см. '+str(log))


def check_manifest(folder,expected):
    data=json.loads((folder/'manifest.json').read_text(encoding='utf-8'))
    if data.get('status')!='validated':raise ValueError('Генератор не подтвердил экспорт')
    ids=[a['id'] for a in data['assets']]
    if len(ids)!=len(set(ids)) or set(ids)!=set(expected):raise ValueError('Неполный или повторяющийся набор моделей')
    for entry in data['assets']:
        if len(entry['lods'])!=3:raise ValueError('Нет трёх LOD')
    return data


def pipeline(exe,out,full=False,allow_newer=False,runner=run_checked):
    if out.exists():raise ValueError('Нужен новый каталог результата: перезапись запрещена')
    out.mkdir(parents=True)
    report={'started':datetime.now(timezone.utc).isoformat(),'status':'running',
            'blenderExecutable':exe,'steps':[],'gameBuildTested':False,'visualReview':False}
    def step(name,cmd,timeout=600):
        runner(cmd,out/(name+'.log'),timeout)
        report['steps'].append(name)
    try:
        step('tests',[sys.executable,'-m','unittest','discover','-s','tests','-v'],120)
        step('api',command(exe,'probe_api.py',['--report',out/'api.json']),120)
        api=json.loads((out/'api.json').read_text(encoding='utf-8'))
        if api.get('apiCompatible')is not True:raise ValueError('API Blender не прошёл проверку')
        if not version_allowed(api.get('version'),allow_newer):
            raise ValueError('Проверяемая ветка — 4.5.x. Для более новой версии явно добавьте --allow-newer; совместимость экспериментальная')
        report['api']=api
        extra=['--allow-newer'] if allow_newer else []
        step('pilot',command(exe,'generate_weapons.py',['--out',out/'pilot','--only','pistol_klyn',*extra]))
        check_manifest(out/'pilot',['pistol_klyn'])
        step('pilot_check',[sys.executable,str(ROOT/'tools/check_exports.py'),str(out/'pilot'),'--only','pistol_klyn'],120)
        report['status']='pilotValidated'
        if full:
            step('generate_all',command(exe,'generate_weapons.py',['--out',out/'full',*extra]),1800)
            catalog=json.loads((ROOT/'assets/weapons/catalog.json').read_text(encoding='utf-8'))
            check_manifest(out/'full',[x['id'] for x in catalog['assets']])
            step('full_check',[sys.executable,str(ROOT/'tools/check_exports.py'),str(out/'full')],120)
            report['status']='exportsValidated'
    except Exception as error:
        report['status']='failed';report['error']=str(error)
        raise
    finally:
        report['finished']=datetime.now(timezone.utc).isoformat()
        (out/'session-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    return report


def main():
    parser=argparse.ArgumentParser(description='Подготовить оружие, не меняя пользовательские настройки Blender')
    parser.add_argument('--blender',help='Полный путь к Blender; иначе автообнаружение')
    parser.add_argument('--out',required=True,type=Path,help='Новый каталог результатов и журналов')
    parser.add_argument('--full',action='store_true',help='После успешного пилота создать весь набор')
    parser.add_argument('--allow-newer',action='store_true',help='Экспериментально разрешить версии новее 4.5 после API-пробы')
    args=parser.parse_args()
    try:
        report=pipeline(find_blender(args.blender),args.out.expanduser().resolve(),args.full,args.allow_newer)
        print('Проверка завершена: '+report['status']+'; осмотр моделей и импорт в Godot ещё обязательны.')
    except Exception as error:
        print('Остановлено: '+str(error),file=sys.stderr);return 1
    return 0

if __name__=='__main__':sys.exit(main())
