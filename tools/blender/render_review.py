"""Render four neutral review views for every generated LOD0 .blend."""
import argparse
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


def bounds(mesh_objects):
    points=[]
    for obj in mesh_objects:
        points.extend(obj.matrix_world @ Vector(vertex.co) for vertex in obj.data.vertices)
    if not points:
        raise RuntimeError('В Blend нет mesh геометрии')
    lo=Vector(tuple(min(p[i] for p in points) for i in range(3)))
    hi=Vector(tuple(max(p[i] for p in points) for i in range(3)))
    return lo,hi,(lo+hi)*0.5,hi-lo


def aim(camera,target):
    camera.rotation_euler=(Vector(target)-camera.location).to_track_quat('-Z','Y').to_euler()


def add_render_rig(center, size):
    scene=bpy.context.scene
    for obj in list(scene.objects):
        if obj.type in ('CAMERA','LIGHT'):
            bpy.data.objects.remove(obj,do_unlink=True)
    world=bpy.data.worlds.new('Review Neutral World') if scene.world is None else scene.world
    scene.world=world;world.use_nodes=True
    bg=world.node_tree.nodes.get('Background')
    bg.inputs['Color'].default_value=(0.20,0.22,0.25,1.0)
    bg.inputs['Strength'].default_value=0.30
    camera_data=bpy.data.cameras.new('Review Camera')
    camera=bpy.data.objects.new('Review Camera',camera_data)
    scene.collection.objects.link(camera);scene.camera=camera
    camera_data.type='ORTHO'
    extent=max(size.x,size.y,size.z,0.12)
    camera_data.ortho_scale=extent*1.42
    camera_data.lens=50
    camera_data.clip_start=0.001;camera_data.clip_end=100
    dist=extent*2.6+0.6
    views={
        'front':Vector((0,1,0)),
        'side':Vector((1,0,0)),
        'top':Vector((0,0,1)),
        'three_quarter':Vector((0.78,0.78,0.46)).normalized(),
    }
    view_data=[]
    for name,direction in views.items():
        location=center+direction*dist
        camera.location=location
        aim(camera,center)
        view_data.append((name,location.copy(),camera.rotation_euler.copy()))
    for name,loc,energy,color,size_factor in (
        ('Key',center+Vector((1.7,-1.4,2.1))*extent,32,(1.0,0.91,0.82),1.8),
        ('Fill',center+Vector((-1.8,0.7,0.9))*extent,18,(0.70,0.82,1.0),2.0),
        ('Rim',center+Vector((0.5,1.8,1.2))*extent,24,(1.0,0.73,0.46),1.5),
    ):
        data=bpy.data.lights.new(name,'AREA');data.energy=energy;data.color=color;data.shape='DISK';data.size=extent*size_factor
        obj=bpy.data.objects.new(name,data);scene.collection.objects.link(obj);obj.location=loc;aim(obj,center)
    scene.render.engine='BLENDER_EEVEE'
    scene.eevee.taa_render_samples=32
    scene.render.resolution_x=512;scene.render.resolution_y=512;scene.render.resolution_percentage=100
    scene.render.image_settings.file_format='PNG';scene.render.image_settings.color_mode='RGBA';scene.render.image_settings.compression=20
    scene.view_settings.view_transform='Standard';scene.view_settings.look='Medium High Contrast'
    scene.view_settings.exposure=0;scene.view_settings.gamma=1
    scene.render.film_transparent=False
    return camera,view_data


def render_blend(blend_path,out_dir):
    bpy.ops.wm.open_mainfile(filepath=str(blend_path))
    scene=bpy.context.scene
    mesh_objects=[obj for obj in scene.objects if obj.type=='MESH']
    lo,hi,center,size=bounds(mesh_objects)
    out_dir.mkdir(parents=True,exist_ok=True)
    camera,view_cameras=add_render_rig(center,size)
    records=[]
    for view,location,rotation in view_cameras:
        scene.camera=camera
        camera.location=location;camera.rotation_euler=rotation
        scene.render.filepath=str(out_dir/(view+'.png'))
        bpy.ops.render.render(write_still=True)
        records.append({'view':view,'image':view+'.png','size':[512,512]})
    root=next((obj for obj in scene.objects if obj.name==blend_path.stem),None)
    components=sorted(obj.name for obj in mesh_objects)
    return {'assetId':blend_path.stem,'source':str(blend_path),'boundsMeters':{
        'min':list(lo),'max':list(hi),'size':list(size)},'meshObjects':len(mesh_objects),
        'components':components,'views':records}


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--blend-dir',required=True,type=Path,help='Каталог с подпапками моделей и их .blend')
    parser.add_argument('--out',required=True,type=Path)
    parser.add_argument('--only')
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    project_root=Path(__file__).resolve().parents[2]
    args.blend_dir=args.blend_dir if args.blend_dir.is_absolute() else (project_root/args.blend_dir)
    args.out=args.out if args.out.is_absolute() else (project_root/args.out)
    args.blend_dir=args.blend_dir.resolve();args.out=args.out.resolve()
    args.out.mkdir(parents=True,exist_ok=True)
    reports=[]
    for folder in sorted(args.blend_dir.iterdir()):
        if not folder.is_dir():continue
        blend=folder/(folder.name+'.blend')
        if not blend.is_file() or (args.only and folder.name!=args.only):continue
        reports.append(render_blend(blend,args.out/folder.name))
    (args.out/'review.json').write_text(json.dumps({'resolution':[512,512],'viewsPerAsset':4,'assets':reports},
        ensure_ascii=False,indent=2),encoding='utf-8')
    print(f"Rendered {len(reports)} models, four views each: {args.out}")

if __name__=='__main__':main()
