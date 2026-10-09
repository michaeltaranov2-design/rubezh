"""Повторная проверка всех экспортов вне Blender."""
import argparse
import json
import struct
import sys
from pathlib import Path
from validate_glb import validate_glb

ROOT=Path(__file__).resolve().parents[1]

def main():
    parser=argparse.ArgumentParser(description='Проверить 13 моделей и 39 GLB')
    parser.add_argument('folder',type=Path)
    parser.add_argument('--only',help='Проверить одну модель по ID')
    args=parser.parse_args()
    catalog=json.loads((ROOT/'assets/weapons/catalog.json').read_text(encoding='utf-8'))
    if args.only and args.only not in {s['id'] for s in catalog['assets']}:raise ValueError('Неизвестный ID')
    output=[]
    for spec in catalog['assets']:
        if args.only and spec['id']!=args.only:continue
        folder=args.folder/spec['id']
        if not (folder/(spec['id']+'.blend')).is_file():raise ValueError('Нет исходного blend: '+spec['id'])
        atlas_path=folder/(spec['id']+'_atlas.png')
        if not atlas_path.is_file():raise ValueError('Нет атласа: '+spec['id'])
        atlas_bytes=atlas_path.read_bytes()
        if atlas_bytes[:8]!=b'\x89PNG\r\n\x1a\n' or struct.unpack_from('>II',atlas_bytes,16)!=(catalog['atlasSize'],catalog['atlasSize']):
            raise ValueError('Неверный PNG атлас: '+spec['id'])
        skin_path=folder/'skins.json'
        if not skin_path.is_file() or len(json.loads(skin_path.read_text(encoding='utf-8')).get('variants',[]))!=3:
            raise ValueError('Нужны три варианта палитры: '+spec['id'])
        reports=[validate_glb(folder/(spec['id']+f'_LOD{lod}.glb'),limit,catalog['sockets'],catalog['clips'],
                              atlas_size=catalog['atlasSize'],require_ao=True)
                 for lod,limit in enumerate(spec['triangleBudgets'])]
        counts=[x['triangles'] for x in reports]
        ranges=spec['triangleRanges']
        for lod,(count,limits) in enumerate(zip(counts,ranges)):
            if not limits[0]<=count<=limits[1]:raise ValueError(f'Треугольный диапазон LOD{lod}: {spec["id"]}: {count}')
        if not counts[0]>counts[1]>counts[2]:raise ValueError('LOD не уменьшается: '+spec['id'])
        output.append({'id':spec['id'],'lods':reports})
    (args.folder/'independent-validation.json').write_text(json.dumps(output,ensure_ascii=False,indent=2),encoding='utf-8')
    print(f'Проверены {len(output)} комплектов, {len(output)*3} GLB. Визуальная проверка в Blender/Godot обязательна.')

if __name__=='__main__':main()
