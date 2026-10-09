"""Проверка сгенерированных GLB; не замена Khronos glTF Validator."""
import json
import math
import struct
from pathlib import Path

COMPONENT = {5120: ('b',1),5121:('B',1),5122:('h',2),5123:('H',2),5125:('I',4),5126:('f',4)}
WIDTH = {'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4,'MAT4':16}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def read_glb(path):
    data = Path(path).read_bytes()
    require(len(data) >= 20, 'Слишком короткий GLB')
    magic, version, size = struct.unpack_from('<III', data)
    require(magic == 0x46546c67 and version == 2 and size == len(data), 'Некорректный заголовок GLB')
    chunks=[]; offset=12
    while offset < len(data):
        require(offset + 8 <= len(data), 'Обрезанный заголовок chunk')
        length, kind=struct.unpack_from('<II',data,offset);offset+=8
        require(length % 4 == 0 and offset+length <= len(data), 'Обрезанный chunk или выравнивание')
        chunks.append((kind,data[offset:offset+length]));offset+=length
    require(len(chunks) == 2 and chunks[0][0] == 0x4e4f534a and chunks[1][0] == 0x004e4942, 'Ожидаются JSON и BIN')
    def reject_constant(value):raise ValueError('Нечисловая константа JSON: '+value)
    doc=json.loads(chunks[0][1].decode('utf-8'),parse_constant=reject_constant)
    require(doc.get('asset',{}).get('version') == '2.0', 'Нужен glTF 2.0')
    buffers=doc.get('buffers',[])
    require(len(buffers)==1 and 'uri' not in buffers[0], 'Нужен один встроенный буфер')
    declared=buffers[0]['byteLength']
    require(isinstance(declared,int) and 0<=declared<=len(chunks[1][1]) and len(chunks[1][1])-declared<=3,'Размер буфера')
    binary=chunks[1][1][:declared]
    for v in doc.get('bufferViews',[]):
        require(v.get('buffer') == 0 and v.get('byteOffset',0)>=0 and v['byteLength']>=0 and v.get('byteOffset',0)+v['byteLength']<=len(binary),'bufferView вне BIN')
    return doc,binary


def ref(items,index):
    require(type(index) is int and 0<=index<len(items),'Недопустимая ссылка массива')
    return items[index]


def accessor(doc,binary,index):
    a=ref(doc['accessors'],index)
    require('sparse' not in a and 'bufferView' in a,'Разреженные данные не поддержаны этой проверкой')
    require(a['componentType'] in COMPONENT and a['type'] in WIDTH,'Тип accessor')
    fmt,unit=COMPONENT[a['componentType']];count=a['count'];width=WIDTH[a['type']]
    require(type(count) is int and count>0,'Пустой accessor')
    v=ref(doc['bufferViews'],a['bufferView']);stride=v.get('byteStride',unit*width)
    start=a.get('byteOffset',0)
    require(stride>=unit*width and stride%unit==0 and start>=0 and start%unit==0,'Шаг accessor')
    require(start+(count-1)*stride+unit*width<=v['byteLength'],'Accessor выходит за bufferView')
    base=v.get('byteOffset',0)+start
    out=[struct.unpack_from('<'+fmt*width,binary,base+i*stride) for i in range(count)]
    require(all(math.isfinite(n) for row in out for n in row),'NaN/Infinity в accessor')
    return out


def validate_glb(path,budget,sockets,clips,atlas_size=128,require_ao=False):
    doc,binary=read_glb(path)
    require(len(doc.get('materials',[]))==1,'Должен быть один материал')
    require(len(doc.get('images',[]))==1 and len(doc.get('textures',[]))==1,'Нужен один атлас')
    image=doc['images'][0];require(image.get('mimeType')=='image/png' and 'uri' not in image,'Атлас должен быть встроенным PNG')
    view=ref(doc['bufferViews'],image['bufferView']);begin=view.get('byteOffset',0)
    png=binary[begin:begin+view['byteLength']]
    require(png[:8]==b'\x89PNG\r\n\x1a\n' and len(png)>=24,'Сигнатура PNG')
    require(struct.unpack_from('>II',png,16)==(atlas_size,atlas_size),f'Размер атласа должен быть {atlas_size}x{atlas_size}')
    require(doc['textures'][0].get('source')==0,'Источник атласа')
    texture=doc['materials'][0].get('pbrMetallicRoughness',{}).get('baseColorTexture',{})
    require(texture.get('index')==0,'Атлас не подключён к материалу')
    nodes=doc.get('nodes',[]); names=[x.get('name','') for x in nodes]
    for socket in sockets:require(names.count(socket)==1,'Отсутствует или дублируется точка '+socket)
    for node in nodes:
        for key in ('translation','rotation','scale','matrix'):
            require(all(isinstance(n,(int,float)) and math.isfinite(n) for n in node.get(key,[])),'Некорректная трансформация')
        for child in node.get('children',[]):require(type(child)is int and 0<=child<len(nodes),'Некорректный дочерний узел')
    triangles=0
    color_seen=False
    ao_darkened=False
    for mesh in doc.get('meshes',[]):
        for p in mesh['primitives']:
            require(p.get('mode',4)==4 and p.get('material')==0,'Ожидаются треугольники одного материала')
            attrs=p['attributes'];require('POSITION'in attrs and 'TEXCOORD_0'in attrs and 'NORMAL'in attrs,'Нет геометрии, нормалей или UV')
            pos=accessor(doc,binary,attrs['POSITION']);uv=accessor(doc,binary,attrs['TEXCOORD_0'])
            normals=accessor(doc,binary,attrs['NORMAL'])
            require(len(pos)==len(uv)==len(normals),'Размеры атрибутов отличаются')
            require(all(len(p)==3 for p in pos) and all(len(n)==3 for n in normals),'Неверная размерность векторов')
            require(all(0.8<sum(x*x for x in n)<1.2 for n in normals),'Некорректные нормали')
            require(all(len(v)==2 and all(0<=x<=1 for x in v) for v in uv),'UV вне атласа')
            if 'COLOR_0' in attrs:
                colors=accessor(doc,binary,attrs['COLOR_0'])
                require(len(colors)==len(pos) and all(len(c) in (3,4) for c in colors),'Размер vertex color не совпадает с вершинами')
                color_seen=True
                ao_darkened=ao_darkened or any(min(c[:3])<0.999 for c in colors)
            elif require_ao:
                raise ValueError('Нет COLOR_0 — отсутствует vertex-color AO')
            ids=[x[0] for x in accessor(doc,binary,p['indices'])] if 'indices'in p else list(range(len(pos)))
            require(len(ids)%3==0 and all(type(i)is int and 0<=i<len(pos) for i in ids),'Индексы вне меша')
            triangles+=len(ids)//3
    if require_ao:
        require(color_seen and ao_darkened,'COLOR_0 пустой или не содержит затемнения запечённого AO')
    require(0<triangles<=budget,'Превышен бюджет треугольников')
    animations=doc.get('animations',[])
    require(sorted(a.get('name','') for a in animations)==sorted(clips),'Неверные имена/количество клипов')
    for animation in animations:
        require(bool(animation.get('channels')),'Пустой клип')
        duration=0.0
        for channel in animation['channels']:
            target=channel['target'];require(type(target['node']) is int and 0<=target['node']<len(nodes),'Цель анимации вне сцены')
            require(target['path'] in ('translation','rotation','scale'),'Неожиданный канал')
            sampler=ref(animation['samplers'],channel['sampler'])
            times=[x[0] for x in accessor(doc,binary,sampler['input'])]
            values=accessor(doc,binary,sampler['output'])
            require(len(times)>=1 and times[0]>=0 and all(a<b for a,b in zip(times,times[1:])),'Время ключей не возрастает')
            duration=max(duration,times[-1])
            factor=3 if sampler.get('interpolation')=='CUBICSPLINE' else 1
            require(len(values)==len(times)*factor,'Размеры ключей не совпадают')
        require(duration>0,'Клип нулевой длительности')
    return {'triangles':triangles,'budget':budget,'bytes':Path(path).stat().st_size,'clips':sorted(clips),'sockets':list(sockets)}
