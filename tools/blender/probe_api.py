"""Проверка установленного Blender в отдельном фоновом процессе."""
import argparse
import json
import sys
from pathlib import Path
import bpy

REQUIRED = {'filepath','export_format','use_selection','export_materials','export_texcoords',
            'export_normals','export_yup','export_extras','export_animations','export_animation_mode',
            'export_merge_animation','export_force_sampling','export_frame_range','export_anim_slide_to_zero',
            'export_optimize_animation_keep_anim_object','export_cameras','export_lights'}

def inspect_api():
    if not bpy.app.background:raise RuntimeError('Нужен фоновый процесс')
    props=bpy.ops.export_scene.gltf.get_rna_type().properties
    missing=sorted(REQUIRED-set(props.keys()))
    if missing:raise RuntimeError('Отсутствуют параметры glTF: '+', '.join(missing))
    for key,value in [('export_animation_mode','NLA_TRACKS'),('export_merge_animation','NLA_TRACK')]:
        if value not in props[key].enum_items.keys():raise RuntimeError('Нет варианта '+key+'='+value)
    if 'action_slot' not in bpy.types.NlaStrip.bl_rna.properties.keys():
        raise RuntimeError('NLA action_slot не поддерживается')
    obj=bpy.data.objects.new('probe',None)
    bpy.context.scene.collection.objects.link(obj)
    action=bpy.data.actions.new('probe_action')
    try:
        obj.animation_data_create();obj.animation_data.action=action
        obj.keyframe_insert(data_path='location',frame=1)
        obj.location.z=0.01;obj.keyframe_insert(data_path='location',frame=2)
        slot=obj.animation_data.action_slot
        if slot is None:raise RuntimeError('Не создан action slot')
        obj.animation_data.action=None
        track=obj.animation_data.nla_tracks.new()
        strip=track.strips.new('probe',1,action)
        strip.action_slot=slot
    finally:
        bpy.data.objects.remove(obj,do_unlink=True)
        bpy.data.actions.remove(action,do_unlink=True)
    return {'apiCompatible':True,'version':list(bpy.app.version),'versionString':bpy.app.version_string,
            'background':bpy.app.background,'exported':False}

def main():
    parser=argparse.ArgumentParser(description='Проверить API Blender без экспорта')
    parser.add_argument('--report',required=True)
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    report={'apiCompatible':False}
    try:report=inspect_api()
    except Exception as error:
        report['error']=str(error)
        raise
    finally:
        Path(args.report).write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')

if __name__=='__main__':main()
