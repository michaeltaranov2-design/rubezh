"""Синтетические GLB для проверки требований; это не Blender export."""
import copy
import json
import struct
import tempfile
import unittest
import zlib
from pathlib import Path
from tools.validate_glb import validate_glb
SOCKETS=['socket_muzzle','socket_eject','socket_hand_r','socket_hand_l']
CLIPS=['idle','fire','reload','inspect']

def fixture():
    binary=bytearray();views=[];accessors=[]
    def add(data):
        while len(binary)%4:binary.append(0)
        views.append({'buffer':0,'byteOffset':len(binary),'byteLength':len(data)})
        binary.extend(data);return len(views)-1
    def acc(data,typ,count,component=5126):
        vi=add(data);accessors.append({'bufferView':vi,'componentType':component,'count':count,'type':typ})
        return len(accessors)-1
    pos=acc(struct.pack('<9f',0,0,0,1,0,0,0,1,0),'VEC3',3)
    norm=acc(struct.pack('<9f',0,0,1,0,0,1,0,0,1),'VEC3',3)
    uv=acc(struct.pack('<6f',0.1,0.1,0.2,0.1,0.1,0.2),'VEC2',3)
    idx=acc(struct.pack('<3H',0,1,2),'SCALAR',3,5123)
    times=acc(struct.pack('<2f',0,1),'SCALAR',2)
    values=acc(struct.pack('<6f',0,0,0,0,0,0.01),'VEC3',2)
    colors=acc(struct.pack('<12f',0.72,0.72,0.72,1,0.8,0.8,0.8,1,0.95,0.95,0.95,1),'VEC4',3)
    def chunk(kind,data):return struct.pack('>I',len(data))+kind+data+struct.pack('>I',zlib.crc32(kind+data)&0xffffffff)
    png=b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',128,128,8,2,0,0,0))
    png+=chunk(b'IDAT',zlib.compress((b'\x00'+b'\x80\x80\x80'*128)*128))+chunk(b'IEND',b'')
    image=add(png)
    doc={'asset':{'version':'2.0'},'buffers':[{'byteLength':len(binary)}],'bufferViews':views,'accessors':accessors,
         'materials':[{'pbrMetallicRoughness':{'baseColorTexture':{'index':0}}}],
         'textures':[{'source':0}],'images':[{'bufferView':image,'mimeType':'image/png'}],
         'nodes':[{'name':'body','mesh':0}]+[{'name':s} for s in SOCKETS],
         'scenes':[{'nodes':list(range(5))}],'scene':0,
         'meshes':[{'primitives':[{'material':0,'indices':idx,'attributes':{'POSITION':pos,'NORMAL':norm,'TEXCOORD_0':uv,'COLOR_0':colors}}]}],
         'animations':[{'name':clip,'channels':[{'sampler':0,'target':{'node':0,'path':'translation'}}],
                        'samplers':[{'input':times,'output':values,'interpolation':'LINEAR'}]} for clip in CLIPS]}
    return doc,bytes(binary)

def write_glb(path,doc,binary):
    j=json.dumps(doc).encode();j+=b' '*((-len(j))%4);binary+=b'\x00'*((-len(binary))%4)
    data=struct.pack('<III',0x46546c67,2,12+8+len(j)+8+len(binary))
    path.write_bytes(data+struct.pack('<II',len(j),0x4e4f534a)+j+struct.pack('<II',len(binary),0x004e4942)+binary)

class GLBContractTests(unittest.TestCase):
    def check(self,doc,binary,budget=10,ao=False):
        with tempfile.TemporaryDirectory() as td:
            path=Path(td)/'sample.glb';write_glb(path,doc,binary)
            return validate_glb(path,budget,SOCKETS,CLIPS,require_ao=ao)
    def test_valid_fixture(self):
        self.assertEqual(self.check(*fixture())['triangles'],1)
    def test_material_atlas_socket_clip(self):
        for change in ('material','atlas','socket','clip'):
            doc,binary=fixture()
            if change=='material':doc['materials'].append({})
            if change=='atlas':doc['images'].append({})
            if change=='socket':doc['nodes'][1]['name']='wrong'
            if change=='clip':doc['animations'].pop()
            with self.subTest(change=change),self.assertRaises(ValueError):self.check(doc,binary)
    def test_ao_requires_baked_vertex_colors(self):
        doc,binary=fixture()
        self.check(doc,binary,ao=True)
        del doc['meshes'][0]['primitives'][0]['attributes']['COLOR_0']
        with self.assertRaises(ValueError):self.check(doc,binary,ao=True)
    def test_triangle_budget(self):
        with self.assertRaises(ValueError):self.check(*fixture(),budget=0)
    def test_constant_channel_allowed_in_nonzero_clip(self):
        doc,binary=fixture()
        first=len(doc['accessors'])
        doc['accessors'].extend([dict(doc['accessors'][4],count=1),dict(doc['accessors'][5],count=1)])
        for a in doc['animations']:
            a['samplers'].append({'input':first,'output':first+1})
            a['channels'].append({'sampler':1,'target':{'node':1,'path':'translation'}})
        self.check(doc,binary)
    def test_zero_duration_rejected(self):
        doc,binary=fixture();doc['accessors'][4]['count']=1;doc['accessors'][5]['count']=1
        with self.assertRaises(ValueError):self.check(doc,binary)
    def test_invalid_animation_reference(self):
        doc,binary=fixture();doc['animations'][0]['channels'][0]['sampler']=-1
        with self.assertRaises(ValueError):self.check(doc,binary)

if __name__=='__main__':unittest.main()
