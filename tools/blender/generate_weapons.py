"""Процедурный генератор авторских моделей Рубеж. Фоновый Blender, координаты — метры, вперёд +Y."""
import argparse
import json
import math
import sys
from array import array
from pathlib import Path

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'tools'))
sys.path.insert(0, str(Path(__file__).resolve().parent))
from validate_glb import validate_glb
from weapon_geometry import build_geometry

CATALOG = json.loads((ROOT / 'assets/weapons/catalog.json').read_text(encoding='utf-8'))
GUNS = {'pistol', 'smg', 'rifle', 'sniper', 'shotgun'}
SKIN_NAMES = ('Графитовый контур', 'Полярный импульс', 'Пыльный код')
SKIN_PATTERNS = ('шестиугольная насечка', 'диагональный шеврон', 'ломаная сервисная линия')


def clear_scene():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for action in list(bpy.data.actions):
        bpy.data.actions.remove(action, do_unlink=True)
    for collection in (bpy.data.meshes, bpy.data.materials, bpy.data.images):
        for item in list(collection):
            if item.users == 0:
                collection.remove(item)


def _palette(spec, index):
    accents = {
        'pistol': (0.95, 0.34, 0.12), 'smg': (0.08, 0.72, 0.72),
        'rifle': (0.78, 0.66, 0.16), 'sniper': (0.63, 0.39, 0.92),
        'shotgun': (0.96, 0.48, 0.12), 'frag': (0.84, 0.66, 0.15),
        'flash': (0.94, 0.82, 0.22), 'smoke': (0.28, 0.72, 0.56),
        'molotov': (0.91, 0.28, 0.10), 'knife': (0.12, 0.76, 0.72),
    }
    accent = accents[spec['kind']]
    palettes = [
        ((0.075,0.095,0.115),(0.26,0.31,0.35),accent,(0.68,0.72,0.72)),
        ((0.055,0.095,0.15),(0.25,0.39,0.51),(0.18,0.80,0.94),(0.78,0.87,0.89)),
        ((0.15,0.12,0.10),(0.43,0.35,0.26),(0.93,0.55,0.19),(0.76,0.70,0.57)),
    ]
    return palettes[index]


def _paint_atlas(spec, folder):
    size = CATALOG['atlasSize']
    cell_w = size // 4
    row_h = size // 3
    image = bpy.data.images.new(spec['id'] + '_atlas', width=size, height=size, alpha=False, float_buffer=False)
    pixels = array('f')
    # Три самостоятельные палитры, ручная процедурная покраска с градиентами и мотивом игры.
    for y in range(size):
        row = min(2, y // row_h)
        local_v = (y - row * row_h) / max(1, row_h - 1)
        for x in range(size):
            tile = min(3, x // cell_w)
            u = (x - tile * cell_w) / max(1, cell_w - 1)
            base = _palette(spec, row)[tile]
            edge = min(u, 1.0-u, local_v, 1.0-local_v)
            shade = 0.78 + 0.20 * local_v + 0.06 * math.sin(u * math.pi)
            if edge < 0.025:
                shade *= 0.70 + edge * 8.0
            wave = (math.sin((u * 7.0 + local_v * 1.4) * math.pi) + 1.0) * 0.5
            if tile == 2 and (int((u + local_v * 0.55) * 13) % 5 == 0):
                shade *= 1.13
            elif tile == 1 and (int(u * 18 + local_v * 2) % 7 == 0):
                shade *= 1.10
            elif tile == 3 and (int((u - local_v) * 20) % 9 == 0):
                shade *= 1.12
            # Тонкий направленный градиент добавляет читаемость фасок без PBR-зависимостей.
            shade *= 0.97 + 0.06 * wave
            pixels.extend((max(0.0,min(1.0,base[0]*shade)),
                           max(0.0,min(1.0,base[1]*shade)),
                           max(0.0,min(1.0,base[2]*shade)),1.0))
    image.pixels.foreach_set(pixels)
    image.filepath_raw = str(folder / (spec['id'] + '_atlas.png'))
    image.file_format = 'PNG'
    image.save()
    image.pack()
    return image


def _make_material(spec, image, skin_index):
    suffix = ('graphite_outline','polar_pulse','dust_code')[skin_index]
    material = bpy.data.materials.new(spec['id'] + '_skin_' + suffix)
    material.use_fake_user = (skin_index > 0)
    material.use_nodes = True
    material.diffuse_color = (*_palette(spec, skin_index)[2], 1.0)
    nodes = material.node_tree.nodes
    nodes.clear()
    out = nodes.new('ShaderNodeOutputMaterial')
    shader = nodes.new('ShaderNodeBsdfPrincipled')
    shader.inputs['Roughness'].default_value = 0.62
    shader.inputs['Metallic'].default_value = 0.18
    shader.inputs['Specular IOR Level'].default_value = 0.28
    uv = nodes.new('ShaderNodeTexCoord')
    offset = nodes.new('ShaderNodeVectorMath')
    offset.operation = 'ADD'
    offset.inputs[1].default_value = (0.0, skin_index / 3.0, 0.0)
    image_node = nodes.new('ShaderNodeTexImage')
    image_node.image = image
    image_node.interpolation = 'Linear'
    image_node.extension = 'REPEAT'
    material.node_tree.links.new(uv.outputs['UV'], offset.inputs[0])
    material.node_tree.links.new(offset.outputs['Vector'], image_node.inputs['Vector'])
    color = nodes.new('ShaderNodeVertexColor')
    color.layer_name = 'AO_Baked'
    multiply = nodes.new('ShaderNodeMix')
    multiply.data_type = 'RGBA'
    multiply.blend_type = 'MULTIPLY'
    multiply.inputs[0].default_value = 1.0
    input_a = next(socket for socket in multiply.inputs if socket.identifier == 'A_Color')
    input_b = next(socket for socket in multiply.inputs if socket.identifier == 'B_Color')
    material.node_tree.links.new(image_node.outputs['Color'], input_a)
    material.node_tree.links.new(color.outputs['Color'], input_b)
    material.node_tree.links.new(multiply.outputs['Result'], shader.inputs['Base Color'])
    # The exporter recognizes Color Attribute -> RGBA Multiply -> Base Color and writes COLOR_0.
    material.node_tree.links.new(color.outputs['Color'], shader.inputs['Emission Color'])
    shader.inputs['Emission Strength'].default_value = 0.0
    material.node_tree.links.new(shader.outputs['BSDF'], out.inputs['Surface'])
    return material


def _create_component_mesh(component, parent, material):
    geom = component.geometry
    mesh = bpy.data.meshes.new(component.name + '_mesh')
    mesh.from_pydata(geom.vertices, [], geom.faces)
    mesh.update()
    obj = bpy.data.objects.new(component.name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.parent = parent
    obj.location = component.origin
    obj.rotation_euler = (0.0,0.0,0.0)
    obj.scale = (1.0,1.0,1.0)
    mesh.materials.append(material)
    uv_layer = mesh.uv_layers.new(name='AtlasUV')
    for poly, tile in zip(mesh.polygons, geom.tiles):
        cx = (int(tile) + 0.5) / 4.0
        cy = 0.165
        rx, ry = 0.105, 0.125
        count = len(poly.loop_indices)
        for index, loop_index in enumerate(poly.loop_indices):
            angle = 2.0 * math.pi * index / count
            uv_layer.data[loop_index].uv = (cx + rx*math.cos(angle), cy + ry*math.sin(angle))
    mesh.calc_loop_triangles()
    mesh.color_attributes.new(name='AO_Baked', type='FLOAT_COLOR', domain='POINT')
    mesh.color_attributes.active_color = mesh.color_attributes['AO_Baked']
    # Если bake не заполнит какую-либо точку, отсутствие AO не превратится в чёрный цвет.
    attr = mesh.color_attributes['AO_Baked']
    attr.data.foreach_set('color', array('f',[1.0,1.0,1.0,1.0]) * len(attr.data))
    return obj


def _bake_ao(mesh_objects, spec):
    if not mesh_objects:
        return {'baked':False,'reason':'no_meshes'}
    scene = bpy.context.scene
    old_engine = scene.render.engine
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = 12
    scene.render.bake.target = 'VERTEX_COLORS'
    max_distance=min(0.08,max(0.025,spec['length']*0.12))
    scene.render.bake.max_ray_distance=max_distance
    scene.render.bake.use_clear = True
    scene.render.bake.margin = 2
    baked_objects = 0
    for obj in mesh_objects:
        bpy.ops.object.select_all(action='DESELECT')
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.bake(type='AO')
        baked_objects += 1
    values=[];source_values=[]
    for obj in mesh_objects:
        attr=obj.data.color_attributes.get('AO_Baked')
        if attr:
            raw=array('f',[0.0])*(len(attr.data)*4)
            attr.data.foreach_get('color',raw)
            for index in range(len(attr.data)):
                offset=index*4
                source=raw[offset]
                source_values.append(source)
                softened=0.38+0.62*source
                raw[offset]=softened;raw[offset+1]=softened;raw[offset+2]=softened;raw[offset+3]=1.0
                values.append(softened)
            attr.data.foreach_set('color',raw)
    scene.render.engine = old_engine
    if not source_values or min(source_values) >= 0.999:
        raise RuntimeError('AO bake завершился без затемнения вершин')
    return {'baked':True,'target':'VERTEX_COLORS','type':'AO','objects':baked_objects,
            'maxRayDistanceMeters':max_distance,'contrastRemap':'0.38 + 0.62 * rawAO',
            'sourceVertexMin':min(source_values),'sourceVertexMean':sum(source_values)/len(source_values),
            'vertexMin':min(values),'vertexMean':sum(values)/len(values),'vertexMax':max(values)}


def _empty(name, parent=None, location=(0.0,0.0,0.0)):
    obj=bpy.data.objects.new(name,None)
    bpy.context.collection.objects.link(obj)
    obj.parent=parent
    obj.location=location
    obj.empty_display_size=0.025
    return obj


def _build_scene(spec, lod, materials):
    root=_empty(spec['id'])
    root['asset_id']=spec['id'];root['lod']=lod;root['units']='meters';root['forward_blender']='+Y'
    root['skin_materials']=json.dumps([m.name for m in materials],ensure_ascii=False)
    rig=_empty('animation_root',root)
    components,sockets,moving=build_geometry(spec,lod)
    objects={name:_create_component_mesh(component,rig,materials[0]) for name,component in components.items()}
    for socket_name,position in sockets.items():
        socket=_empty(socket_name,rig,position)
        socket['role']=socket_name
        socket['applicable']=not (socket_name=='socket_eject' and spec['kind'] not in GUNS)
        socket['axis_notes']='Сохранён исходный socket API; прицел — объект optic_sight/optic_mount.'
        if socket_name=='socket_eject':socket.rotation_euler.z=-math.pi/2
        if socket_name=='socket_muzzle' and spec['kind'] not in GUNS and spec['kind']!='knife':
            socket.rotation_euler.x=math.pi/2
    return root,rig,objects,moving


def _animate_object(obj, clips):
    obj.rotation_mode='XYZ'
    obj.animation_data_create()
    base_location=obj.location.copy()
    for name, keys in clips.items():
        action=bpy.data.actions.new(obj.name+'_'+name)
        obj.animation_data.action=action
        for frame,offset,rotation in keys:
            obj.location=base_location+Vector(offset)
            obj.rotation_euler=rotation
            obj.keyframe_insert(data_path='location',frame=frame,group=name)
            obj.keyframe_insert(data_path='rotation_euler',frame=frame,group=name)
        slot=obj.animation_data.action_slot
        obj.animation_data.action=None
        track=obj.animation_data.nla_tracks.new()
        track.name=name
        strip=track.strips.new(name,1,action)
        strip.action_slot=slot
        strip.extrapolation='NOTHING'
        strip.blend_type='REPLACE'
        action.use_fake_user=True
        obj.location=base_location
        obj.rotation_euler=(0.0,0.0,0.0)


def _animate(rig, objects, moving, kind):
    zero=(0.0,0.0,0.0)
    def key(frame,pos=zero,rot=zero):return (frame,pos,rot)
    fire=[key(1),key(3,(0.0,-0.022,0.006),(0.045,0.0,0.0)),key(8)]
    if kind=='knife':fire=[key(1),key(5,(0.0,0.075,0.0),(0.0,0.0,0.72)),key(12)]
    elif kind not in GUNS:fire=[key(1),key(6,(0.0,0.07,0.02),(0.55,0.0,0.0)),key(15)]
    root_clips={'idle':[key(1),key(16,(0.0,0.0,0.001),(0.0,0.003,0.0)),key(31)],
        'fire':fire,'reload':[key(1),key(18,(0.0,0.0,0.003),(0.0,0.04,0.0)),key(38)],
        'inspect':[key(1),key(20,(0.0,0.02,0.012),(0.11,0.48,0.23)),
                   key(42,(0.0,0.018,0.008),(-0.07,-0.30,-0.15)),key(61)]}
    _animate_object(rig,root_clips)
    for component_name in moving:
        obj=objects.get(component_name)
        if obj is None:continue
        clips={
            'idle':[key(1),key(31)],
            'fire':[key(1),key(3,(0.0,-0.004,0.002),(0.045,0.0,0.0)),key(9)],
            'reload':[key(1),key(12),key(20,(0.0,0.0,-0.075),(0.0,0.0,0.0)),
                      key(29,(0.0,0.0,-0.075),(0.0,0.0,0.0)),key(39)],
            'inspect':[key(1),key(31)]}
        if component_name in ('safety_lever','pull_ring','retaining_pin'):
            clips['reload']=[key(1),key(12),key(20,(0.0,0.0,0.012),(0.2,0.0,0.0)),key(34)]
        if component_name=='pump':
            clips['fire']=[key(1),key(4,(0.0,0.055,0.0)),key(9)]
            clips['reload']=[key(1),key(12,(0.0,-0.04,0.0)),key(21,(0.0,0.04,0.0)),key(32)]
        if component_name=='trigger':
            clips['fire']=[key(1),key(3,(0.0,0.0,0.0),(0.15,0.0,0.0)),key(8)]
        _animate_object(obj,clips)


def _write_skins(spec, folder, materials):
    entries=[]
    for index,material in enumerate(materials):
        entries.append({'index':index,'name':spec['name']+' — '+SKIN_NAMES[index],
            'pattern':SKIN_PATTERNS[index],'material':material.name,
            'atlasUvOffset':[0.0,index/3.0],'atlasUvScale':[1.0,1.0],
            'atlasRow':[index/3.0,(index+1)/3.0]})
    (folder/'skins.json').write_text(json.dumps({'assetId':spec['id'],'atlas':spec['id']+'_atlas.png',
        'atlasSize':CATALOG['atlasSize'],'variants':entries},ensure_ascii=False,indent=2),encoding='utf-8')
    return entries


def export_one(spec,lod,folder):
    clear_scene()
    scene=bpy.context.scene
    scene.render.fps=30;scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1.0
    scene.frame_start=1;scene.frame_end=61
    image=_paint_atlas(spec,folder)
    materials=[_make_material(spec,image,index) for index in range(3)]
    root,rig,objects,moving=_build_scene(spec,lod,materials)
    _animate(rig,objects,moving,spec['kind'])
    mesh_objects=list(objects.values())
    ao_report=_bake_ao(mesh_objects,spec)
    scene.frame_set(1)
    triangles=sum(len(obj.data.loop_triangles) for obj in mesh_objects)
    limit=spec['triangleBudgets'][lod]
    if triangles>limit:raise RuntimeError(f'{spec["id"]} LOD{lod}: {triangles} треугольников > {limit}')
    path=folder/(spec['id']+f'_LOD{lod}.glb')
    bpy.ops.object.select_all(action='SELECT')
    result=bpy.ops.export_scene.gltf(filepath=str(path),export_format='GLB',use_selection=True,
        export_materials='EXPORT',export_texcoords=True,export_normals=True,
        export_yup=True,export_extras=True,export_animations=True,
        export_animation_mode='NLA_TRACKS',export_merge_animation='NLA_TRACK',
        export_force_sampling=True,export_frame_range=False,export_anim_slide_to_zero=True,
        export_optimize_animation_keep_anim_object=True,export_cameras=False,export_lights=False)
    if 'FINISHED' not in result:raise RuntimeError('Экспорт GLB не завершён')
    report=validate_glb(path,limit,CATALOG['sockets'],CATALOG['clips'],
                        atlas_size=CATALOG['atlasSize'],require_ao=True)
    report['aoBake']=ao_report
    report['meshObjects']=len(mesh_objects)
    report['components']=sorted(objects)
    if lod==0:
        skin_entries=_write_skins(spec,folder,materials)
        root['skin_variants']=json.dumps(skin_entries,ensure_ascii=False)
        scene.render.engine='BLENDER_EEVEE'
        for obj in (rig,*objects.values()):
            if obj.animation_data:
                for track in obj.animation_data.nla_tracks:
                    track.mute=(track.name!='idle')
        scene.frame_set(1)
        bpy.ops.wm.save_as_mainfile(filepath=str(folder/(spec['id']+'.blend')))
    return report


def main():
    parser=argparse.ArgumentParser(description='Авторские процедурные модели оружия Рубеж')
    parser.add_argument('--out',required=True,help='Новый пустой каталог результата')
    parser.add_argument('--only',choices=[x['id'] for x in CATALOG['assets']])
    parser.add_argument('--allow-newer',action='store_true',help='Разрешить Blender новее проверенной ветки')
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    if not bpy.app.background:raise RuntimeError('Запуск разрешён только в отдельном фоновом процессе Blender')
    from run_weapons import version_allowed
    if not version_allowed(list(bpy.app.version),args.allow_newer):
        raise RuntimeError('Нужен Blender 4.5.x или явный --allow-newer после проверки API')
    from probe_api import inspect_api
    inspect_api()
    out=Path(args.out).expanduser().resolve()
    if out.exists() and any(out.iterdir()):raise RuntimeError('Каталог не пуст; перезапись запрещена')
    out.mkdir(parents=True,exist_ok=True)
    manifest={'blender':bpy.app.version_string,'status':'building','assets':[],
              'atlasSize':CATALOG['atlasSize'],'aoBakeTarget':'VERTEX_COLORS'}
    all_skins=[]
    try:
        for spec in CATALOG['assets']:
            if args.only and spec['id']!=args.only:continue
            folder=out/spec['id'];folder.mkdir()
            reports=[export_one(spec,lod,folder) for lod in range(3)]
            counts=[report['triangles'] for report in reports]
            if not counts[0]>counts[1]>counts[2]:raise RuntimeError('LOD не уменьшает число треугольников')
            skin_doc=json.loads((folder/'skins.json').read_text(encoding='utf-8')) if (folder/'skins.json').exists() else None
            if skin_doc:all_skins.append(skin_doc)
            manifest['assets'].append({'id':spec['id'],'name':spec['name'],'lods':reports,
                'meshObjects':reports[0].get('meshObjects'),
                'lodDistancesMeters':[0,12,30],'lodSwitching':'настраивается в проекте Godot',
                'skins':skin_doc['variants'] if skin_doc else []})
        manifest['skinsFile']='skins.json'
        manifest['status']='validated'
    except Exception as error:
        manifest['status']='failed';manifest['error']=str(error)
        raise
    finally:
        (out/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
        (out/'skins.json').write_text(json.dumps(all_skins,ensure_ascii=False,indent=2),encoding='utf-8')
    print('Модели экспортированы и структурно проверены: '+str(out))

if __name__=='__main__':main()
