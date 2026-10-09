"""Pure procedural weapon geometry for Rubezh; coordinates in meters, +Y forward."""
import math

GUNS = {'pistol', 'smg', 'rifle', 'sniper', 'shotgun'}
GRENADES = {'frag', 'flash', 'smoke', 'molotov'}
LOD_SEGMENTS = (16, 10, 6)


def _vadd(a, b):
    return tuple(x + y for x, y in zip(a, b))


class Geometry:
    """Polygon builder with flat/chamfered boxes, profiled cylinders and torus parts."""
    def __init__(self):
        self.vertices = []
        self.faces = []
        self.tiles = []
        self.smooth = []

    def part(self, vertices, faces, tile=0, smooth=False):
        offset = len(self.vertices)
        self.vertices.extend(tuple(float(v) for v in p) for p in vertices)
        for face in faces:
            self.faces.append(tuple(i + offset for i in face))
            self.tiles.append(int(tile))
            self.smooth.append(bool(smooth))

    @property
    def triangles(self):
        return sum(len(face) - 2 for face in self.faces)

    def box(self, center, size, tile=0, bevel=0.0, tilt=0.0):
        x, y, z = center
        hx, hy, hz = (max(0.00001, abs(float(v)) / 2) for v in size)
        b = min(max(0.0, bevel), hx * 0.22, hy * 0.22, hz * 0.22)
        if b <= 1e-8:
            verts = [(x + dx * hx, y + dy * hy + tilt * dz * hz, z + dz * hz)
                     for dx, dy, dz in [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),
                                        (-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]]
            self.part(verts, [(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),
                              (2,3,7,6),(3,0,4,7)], tile)
            return
        half = (hx, hy, hz)
        coords = {}
        verts = []
        def ix(p):
            p = (p[0], p[1] + tilt * p[2], p[2])
            key = tuple(round(q, 9) for q in p)
            if key not in coords:
                coords[key] = len(verts)
                verts.append((x + p[0], y + p[1], z + p[2]))
            return coords[key]
        faces = []
        # Six broad octagonal faces, twelve one-segment chamfers and eight corners.
        for axis in range(3):
            others = [k for k in range(3) if k != axis]
            for sign in (-1, 1):
                ring = []
                for s0, s1 in [(-1,-1),(1,-1),(1,1),(-1,1)]:
                    a = [0.0, 0.0, 0.0]
                    a[axis] = sign * half[axis]
                    a[others[0]] = s0 * half[others[0]]
                    a[others[1]] = s1 * (half[others[1]] - b)
                    ring.append(ix(a))
                    c = a.copy()
                    c[others[0]] = s0 * (half[others[0]] - b)
                    c[others[1]] = s1 * half[others[1]]
                    ring.append(ix(c))
                center2 = [sum(verts[i][k] for i in ring) / len(ring) for k in range(3)]
                u, v = others
                ring.sort(key=lambda i: math.atan2(verts[i][v] - center2[v], verts[i][u] - center2[u]))
                faces.append(tuple(ring))
        for free in range(3):
            a, c = [k for k in range(3) if k != free]
            for sa in (-1,1):
                for sc in (-1,1):
                    ring = []
                    for sf in (-1,1):
                        p = [0.0,0.0,0.0]
                        p[free] = sf * (half[free] - b)
                        p[a] = sa * (half[a] - b)
                        p[c] = sc * half[c]
                        ring.append(ix(p))
                        q = p.copy()
                        q[a] = sa * half[a]
                        q[c] = sc * (half[c] - b)
                        ring.append(ix(q))
                    faces.append(tuple(ring))
        for sx in (-1,1):
            for sy in (-1,1):
                for sz in (-1,1):
                    faces.append((ix((sx*(hx-b),sy*hy,sz*hz)),
                                  ix((sx*hx,sy*(hy-b),sz*hz)),
                                  ix((sx*hx,sy*hy,sz*(hz-b)))))
        self.part(verts, faces, tile)

    def tube(self, center, radius, length, segments=12, tile=1, axis='Y', profile=None, smooth=True):
        if profile is None:
            profile = [(-0.5, 1.0), (0.5, 1.0)]
        x,y,z = center
        rings=[]
        verts=[]
        for along, scale in profile:
            ring=[]
            for i in range(max(3, segments)):
                a=2*math.pi*i/max(3,segments)
                ca,sa=math.cos(a),math.sin(a)
                if axis=='Y': p=(x+radius*scale*ca,y+along*length,z+radius*scale*sa)
                elif axis=='X': p=(x+along*length,y+radius*scale*ca,z+radius*scale*sa)
                else: p=(x+radius*scale*ca,y+radius*scale*sa,z+along*length)
                ring.append(len(verts));verts.append(p)
            rings.append(ring)
        faces=[]
        faces.append(tuple(reversed(rings[0])))
        faces.append(tuple(rings[-1]))
        n=max(3,segments)
        for r0,r1 in zip(rings,rings[1:]):
            for i in range(n):faces.append((r0[i],r0[(i+1)%n],r1[(i+1)%n],r1[i]))
        self.part(verts,faces,tile,smooth=smooth)

    def torus(self, center, major_radius, minor_radius, segments=16, minor_segments=6, tile=2, axis='X'):
        x,y,z=center;n=max(6,segments);m=max(4,minor_segments);verts=[];faces=[]
        for i in range(n):
            a=2*math.pi*i/n
            for j in range(m):
                b=2*math.pi*j/m
                radial=major_radius+minor_radius*math.cos(b)
                off=minor_radius*math.sin(b)
                if axis=='X':p=(x+off,y+radial*math.cos(a),z+radial*math.sin(a))
                elif axis=='Y':p=(x+radial*math.cos(a),y+off,z+radial*math.sin(a))
                else:p=(x+radial*math.cos(a),y+radial*math.sin(a),z+off)
                verts.append(p)
        for i in range(n):
            for j in range(m):
                a=i*m+j;b=i*m+(j+1)%m;c=((i+1)%n)*m+(j+1)%m;d=((i+1)%n)*m+j
                faces.append((a,b,c,d))
        self.part(verts,faces,tile,smooth=True)

    def prism_x(self, center, outline_yz, thickness, tile=1):
        x,y,z=center;n=len(outline_yz);verts=[]
        for side in (-1,1):
            for py,pz in outline_yz:verts.append((x+side*thickness/2,y+py,z+pz))
        faces=[tuple(reversed(range(n))),tuple(range(n,2*n))]
        for i in range(n):faces.append((i,(i+1)%n,(i+1)%n+n,i+n))
        self.part(verts,faces,tile)


class Component:
    def __init__(self, name, origin=(0,0,0)):
        self.name=name
        self.origin=tuple(float(x) for x in origin)
        self.geometry=Geometry()

    def local(self, point):
        return tuple(float(p)-o for p,o in zip(point,self.origin))

    def box(self, center, size, tile=0, bevel=0.0, tilt=0.0):
        self.geometry.box(self.local(center),size,tile,bevel,tilt)

    def tube(self, center, radius, length, segments, tile=1, axis='Y', profile=None, smooth=True):
        self.geometry.tube(self.local(center),radius,length,segments,tile,axis,profile,smooth)

    def torus(self, center, major_radius, minor_radius, segments, minor_segments=6, tile=2, axis='X'):
        self.geometry.torus(self.local(center),major_radius,minor_radius,segments,minor_segments,tile,axis)

    def prism_x(self, center, outline_yz, thickness, tile=1):
        self.geometry.prism_x(self.local(center),outline_yz,thickness,tile)


def _segments(lod):
    return LOD_SEGMENTS[lod]


def _bevel(lod, size, major=False):
    if lod == 0 and major:
        return min(abs(v) for v in size) * 0.12
    if lod == 1 and major:
        return min(abs(v) for v in size) * 0.06
    return 0.0


def _component(parts, name, origin=(0,0,0)):
    if name not in parts:parts[name]=Component(name,origin)
    return parts[name]


def _box(parts,name,center,size,tile,lod,major=False,tilt=0.0):
    c=_component(parts,name,center)
    c.box(center,size,tile,_bevel(lod,size,major),tilt)
    return c


def _tube(parts,name,center,radius,length,lod,tile=1,axis='Y',profile=None,segments=None):
    c=_component(parts,name,center)
    c.tube(center,radius,length,segments or _segments(lod),tile,axis,profile)
    return c


def _rail(parts,name,y0,y1,z,width,lod,tile=3,side='top'):
    length=max(0.015,y1-y0)
    comp=_component(parts,name,(0,(y0+y1)/2,z))
    comp.box((0,(y0+y1)/2,z),(width,length,0.012),tile,_bevel(lod,(width,length,0.012),lod<2))
    count=(14,8,3)[lod]
    for i in range(count):
        y=y0+(i+0.5)*length/count
        comp.box((0,y,z+0.008),(width*0.92,max(0.002,length/count*0.42),0.012),2 if i%4==0 else 3,0)
    return comp


def _screws(comp, x, ys, z, lod, radius=0.004):
    count=(12,8,6)[lod]
    for y in ys:
        comp.tube((x,y,z),radius,0.0025,count,3,axis='X',smooth=False)
        if lod<2:
            # Slot recesses are geometry, not a decal copied from any game.
            comp.box((x+(0.001 if x>=0 else -0.001),y,z),(0.0015,radius*1.35,0.0012),0)


def _add_handgun(spec,lod,parts):
    L=spec['length'];W=spec['width'];s=_segments(lod)
    receiver=_component(parts,'receiver',(0,-L*0.015,0))
    receiver.box((0,-L*0.015,-0.006),(W*0.71,L*0.48,0.066),0,_bevel(lod,(W*0.71,L*0.48,0.066),True))
    receiver.box((0,-L*0.015,0.028),(W*0.79,L*0.40,0.045),1,_bevel(lod,(W*0.79,L*0.40,0.045),True))
    # Distinctive faceted receiver cheeks and a continuous accent chevron.
    for side in (-1,1):
        receiver.box((side*W*0.36,-L*0.025,0.032),(W*0.055,L*0.24,0.018),2,0,tilt=side*0.08)
        receiver.box((side*W*0.365,L*0.075,0.030),(W*0.022,L*0.055,0.012),3)
    slide=_component(parts,'slide',(0,L*0.075,0.045))
    slide.box((0,L*0.075,0.045),(W*0.78,L*0.52,0.048),0,_bevel(lod,(W*0.78,L*0.52,0.048),True))
    slide.box((0,L*0.075,0.072),(W*0.66,L*0.37,0.010),1)
    # Slide serrations, paired and tapered rather than relying on a flat texture.
    serrations=(9,5,2)[lod]
    for side in (-1,1):
        for i in range(serrations):
            y=-L*0.075+i*(L*0.0065)
            slide.box((side*W*0.397,y,0.046),(W*0.028,0.0045,0.028),2 if i%3==0 else 3)
    barrel=_tube(parts,'barrel',(0,L*0.355,0.024),W*0.13,L*0.34,lod,1,
                 profile=[(-.5,.82),(-.42,1),(.37,1),(.44,1.12),(.5,.9)])
    brake=_tube(parts,'muzzle_brake',(0,L*0.474,0.024),W*0.19,L*0.055,lod,3,
                profile=[(-.5,.78),(-.34,1),(.25,1),(.5,.84)])
    brake.torus((0,L*0.474,0.024),W*0.19,L*0.014,s,5,2,'Y')
    grip=_component(parts,'grip',(0,-L*0.17,-0.073))
    grip.box((0,-L*0.17,-0.073),(W*0.61,L*0.35,0.13),0,_bevel(lod,(W*0.61,L*0.35,0.13),True),tilt=0.12)
    for side in (-1,1):
        grip.box((side*W*0.29,-L*0.17,-0.074),(W*0.045,L*0.25,0.092),1,_bevel(lod,(W*0.045,L*0.25,0.092),True))
        for i in range((8,4,2)[lod]):
            grip.box((side*W*0.32,-L*0.275+i*L*0.026,-0.074),(W*0.014,L*0.008,0.095),3)
    mag_origin=(0,-L*0.19,-0.13)
    mag=_component(parts,'magazine',mag_origin)
    mag.box(mag_origin,(W*0.39,L*0.14,0.075),1,_bevel(lod,(W*0.39,L*0.14,0.075),True))
    mag.box((0,mag_origin[1]-L*.035,mag_origin[2]-.042),(W*.47,L*.018,.012),3)
    for i in range((7,4,2)[lod]):mag.box((0,mag_origin[1]-L*.045+i*L*.018,mag_origin[2]),(W*.4,L*.004,.038),2 if i==0 else 3)
    trigger=_component(parts,'trigger',(0,-L*.075,-.064))
    trigger.box((0,-L*.075,-.064),(W*.10,.012,.04),3)
    guard=_component(parts,'trigger_guard',(0,-L*.075,-.086))
    for side in (-1,1):guard.box((side*W*.16,-L*.085,-.084),(W*.018,.072,.008),1)
    guard.box((0,-L*.12,-.088),(W*.32,.012,.008),1)
    safety=_component(parts,'safety_lever',(0,-L*.02,.04))
    safety.box((W*.41,-L*.015,.04),(W*.018,.038,.012),2,_bevel(lod,(W*.018,.038,.012),True))
    sight=_component(parts,'optic_sight',(0,L*.14,.093))
    sight.box((0,L*.14,.091),(W*.17,.016,.026),3)
    sight.box((0,L*.365,.095),(W*.10,.012,.022),2)
    if spec['variant']:
        sight.box((0,L*.14,.109),(W*.24,.075,.012),2,_bevel(lod,(W*.24,.075,.012),True))
        sight.box((0,L*.14,.122),(W*.18,.040,.014),0)
    # High-contrast micro-controls and hex fasteners on both receiver sides.
    for side in (-1,1):
        _screws(receiver,side*W*.405,[-L*.12,-L*.035,L*.045],.034,lod,W*.052)
        receiver.tube((side*W*.42,-L*.04,.012),W*.065,.004,s,2,axis='X',smooth=False)
        receiver.box((side*W*.44,-L*.04,.012),(W*.014,.032,.012),3)
    # Variant B carries a hooked heel and a dorsal optic saddle; IDs stay unchanged.
    if spec['variant']:
        grip.box((0,-L*.305,-.092),(W*.50,.036,.052),2,_bevel(lod,(W*.50,.036,.052),True))
        _rail(parts,'optic_mount',L*.08,L*.30,.088,W*.34,lod,3)
    else:
        _rail(parts,'optic_mount',L*.02,L*.18,.088,W*.26,lod,3)
    socket={'socket_muzzle':(0,L*.505,.024),'socket_eject':(W*.43,L*.04,.055),
            'socket_hand_r':(0,-L*.18,-.105),'socket_hand_l':(0,L*.12,-.03)}
    moving={'slide':slide,'magazine':mag,'trigger':trigger,'safety_lever':safety}
    return socket,moving


def _add_long_gun(spec,lod,parts):
    kind=spec['kind'];L=spec['length'];W=spec['width'];s=_segments(lod)
    bull=bool(spec['variant'] and kind in ('smg','rifle'))
    receiver_y=(-L*.12 if bull else -L*.01)
    receiver=_component(parts,'receiver',(0,receiver_y,0))
    receiver.box((0,receiver_y,0),(W*.78,L*.26,.10),0,_bevel(lod,(W*.78,L*.26,.10),True))
    receiver.box((0,receiver_y,.046),(W*.72,L*.19,.035),1,_bevel(lod,(W*.72,L*.19,.035),True))
    # Angular side armor, recessed hex-shaped panels and fasteners.
    for side in (-1,1):
        receiver.box((side*W*.38,receiver_y-.005,.004),(W*.055,L*.17,.065),2,_bevel(lod,(W*.055,L*.17,.065),True))
        receiver.box((side*W*.415,receiver_y+.018,.006),(W*.008,L*.065,.018),3)
        _screws(receiver,side*W*.424,[receiver_y-L*.075,receiver_y-L*.015,receiver_y+L*.055],.018,lod,W*.043)
    # Class layout distinguishes conventional carbines, bullpup SMG and long precision rifle.
    if kind=='sniper':
        hg0=L*.04;hg1=L*.30;stock_y=-L*.24;barrel0=L*.25;barrel_len=L*.36
    elif kind=='shotgun':
        hg0=L*.015;hg1=L*.27;stock_y=-L*.24;barrel0=L*.22;barrel_len=L*.34
    elif bull:
        hg0=receiver_y+L*.10;hg1=L*.40;stock_y=-L*.30;barrel0=L*.37;barrel_len=L*.18
    else:
        hg0=L*.02;hg1=L*.28;stock_y=-L*.24;barrel0=L*.29;barrel_len=L*.25
    # Handguard and repeated hex vents.
    handguard=_component(parts,'handguard',(0,(hg0+hg1)/2,.002))
    handguard.box((0,(hg0+hg1)/2,.002),(W*.76,hg1-hg0,.085),0,_bevel(lod,(W*.76,hg1-hg0,.085),True))
    for side in (-1,1):
        handguard.box((side*W*.39,(hg0+hg1)/2,.002),(W*.045,(hg1-hg0)*.75,.065),1,_bevel(lod,(W*.045,(hg1-hg0)*.75,.065),True))
        vent_count=(9,5,2)[lod]
        for i in range(vent_count):
            yy=hg0+.02+(hg1-hg0-.04)*(i+.5)/vent_count
            handguard.box((side*W*.42,yy,.004),(W*.012,.012,.030),2 if i%3==0 else 3)
            if lod==0:
                handguard.box((side*W*.426,yy,.005),(W*.005,.005,.019),0)
    # 3-sided rail system with visible lugs at LOD0/LOD1.
    _rail(parts,'optic_mount',receiver_y-L*.08,hg1,.066,W*.42,lod,3)
    rail_count=(12,7,3)[lod]
    side_rail=_component(parts,'side_rail',(0,(hg0+hg1)/2,.025))
    for side in (-1,1):
        side_rail.box((side*W*.44,(hg0+hg1)/2,.025),(W*.025,hg1-hg0,.022),3)
        for i in range(rail_count):
            yy=hg0+(i+.5)*(hg1-hg0)/rail_count
            side_rail.box((side*W*.455,yy,.025),(W*.035,.008,.027),2 if i%4==0 else 3)
    # Long bore and a sculpted compensator; no trade dress is duplicated.
    barrel_center=barrel0+barrel_len*.5
    barrel=_tube(parts,'barrel',(0,barrel_center,.010),W*.145,barrel_len,lod,1,
                 profile=[(-.5,.78),(-.43,1),(.34,1),(.40,1.12),(.47,.95),(.5,.82)])
    for pos,rad in [(barrel0+.018,W*.18),(barrel0+barrel_len-.025,W*.19)]:
        barrel.torus((0,pos,.010),rad,.0025,s,5,3,'Y')
    brake=_tube(parts,'muzzle_brake',(0,L*.478,.010),W*.20,L*.048,lod,3,
                profile=[(-.5,.75),(-.30,1),(.30,1),(.5,.82)])
    for side in (-1,1):
        if lod<2:
            brake.box((side*W*.18,L*.478,.010),(W*.04,L*.018,.012),0)
    # Stock: profile-specific folding/adjustable or precision cheek riser.
    stock=_component(parts,'stock',(0,stock_y,-.005))
    stock.box((0,stock_y,-.005),(W*.70,L*.22,.090),0,_bevel(lod,(W*.70,L*.22,.090),True))
    stock.box((0,stock_y-L*.092,-.005),(W*.82,.026,.105),3,_bevel(lod,(W*.82,.026,.105),True))
    if kind=='sniper':
        stock.box((0,stock_y+.03,.054),(W*.58,.12,.024),2,_bevel(lod,(W*.58,.12,.024),True))
        stock.box((0,stock_y-.005,-.056),(W*.20,.11,.028),1)
    elif spec['variant']:
        stock.box((0,stock_y+.01,.032),(W*.45,.12,.018),2)
        for i in range((5,3,1)[lod]):stock.box((0,stock_y-.07+i*.025,-.045),(W*.55,.008,.012),1)
    else:
        for side in (-1,1):stock.box((side*W*.32,stock_y,-.005),(W*.06,.15,.055),2)
    grip=_component(parts,'grip',(0,receiver_y-L*.095,-.075))
    grip.box((0,receiver_y-L*.095,-.075),(W*.46,.095,.135),0,_bevel(lod,(W*.46,.095,.135),True),tilt=.10)
    for side in (-1,1):
        grip.box((side*W*.235,receiver_y-L*.095,-.075),(W*.025,.075,.100),1,_bevel(lod,(W*.025,.075,.100),True))
        for i in range((7,4,2)[lod]):grip.box((side*W*.25,receiver_y-L*.125+i*.018,-.075),(W*.008,.006,.085),3)
    trigger_y=receiver_y-L*.045
    guard=_component(parts,'trigger_guard',(0,trigger_y,-.11))
    for side in (-1,1):guard.box((side*W*.12,trigger_y,-.105),(W*.018,.063,.010),3)
    guard.box((0,trigger_y-.030,-.11),(W*.24,.014,.010),1)
    trigger=_component(parts,'trigger',(0,trigger_y,-.09))
    trigger.prism_x((0,trigger_y,-.09),[(-.014,.025),(0,.012),(.016,-.018),(.012,-.024),(-.008,-.008)],W*.08,2)
    # Magazine position varies by architecture; its own origin permits reload animation.
    magazine_y=(receiver_y+.085 if bull else receiver_y-.025)
    mag_origin=(0,magazine_y,-.095)
    mag=_component(parts,'magazine',mag_origin)
    mag.box(mag_origin,(W*.48,L*.17,.080),0,_bevel(lod,(W*.48,L*.17,.080),True),tilt=.05 if spec['variant'] else 0)
    mag.box((0,magazine_y-L*.085,mag_origin[2]-.044),(W*.56,.025,.014),3)
    for i in range((8,5,2)[lod]):mag.box((0,magazine_y-L*.075+i*L*.018,mag_origin[2]+.001),(W*.50,.006,.057),2 if i==0 else 3)
    # Separate cocking and selector hardware.
    charging=_component(parts,'charging_handle',(W*.43,receiver_y+.02,.050))
    charging.box((W*.43,receiver_y+.02,.050),(W*.08,.060,.018),2,_bevel(lod,(W*.08,.060,.018),True))
    charging.tube((W*.48,receiver_y+.02,.050),W*.035,.025,s,3,'X')
    safety=_component(parts,'safety_lever',(-W*.43,receiver_y-.025,.018))
    safety.box((-W*.43,receiver_y-.025,.018),(W*.075,.042,.018),2,_bevel(lod,(W*.075,.042,.018),True))
    # Optic: scopes on sniper, aperture reflex / iron sights on other long guns.
    sight_y=(receiver_y+.01 if kind=='sniper' else (receiver_y+.05 if bull else L*.13))
    sight=_component(parts,'optic_sight',(0,sight_y,.115))
    sight.box((0,sight_y,.096),(W*.16,.055,.036),1)
    if kind=='sniper':
        for yy in (sight_y-L*.035,sight_y+L*.035):
            sight.box((0,yy,.100),(W*.44,.028,.055),2)
        sight.tube((0,sight_y,.145),W*.22,L*.22,s,0,'Y',[(-.5,.74),(-.38,1),(.34,1),(.5,.78)])
        sight.tube((0,sight_y-L*.115,.145),W*.29,L*.055,s,3,'Y',[(-.5,.78),(-.32,1),(.32,1),(.5,.78)])
        sight.tube((0,sight_y+L*.12,.145),W*.32,L*.060,s,2,'Y',[(-.5,.80),(-.3,1),(.3,1),(.5,.80)])
        for yy in (sight_y-L*.035,sight_y+L*.035):sight.torus((0,yy,.145),W*.23,.004,s,6,3,'Y')
        sight.tube((0,sight_y,.195),W*.09,.035,s,2,'Z')
        sight.torus((0,sight_y,.214),W*.09,.006,s,6,2,'Z')
        sight.box((0,sight_y,.115),(W*.18,.075,.026),1)
    else:
        sight.box((0,sight_y,.102),(W*.26,.036,.033),1,_bevel(lod,(W*.26,.036,.033),True))
        sight.box((0,sight_y,.125),(W*.18,.018,.010),2)
        sight.box((0,L*.40,.055),(W*.09,.014,.025),3)
    # Barrel heat vents, patterned side panels and fastener pairs.
    for side in (-1,1):
        for i in range((10,6,2)[lod]):
            yy=hg0+.025+(hg1-hg0-.05)*(i+.5)/(10 if lod==0 else (6 if lod==1 else 2))
            handguard.box((side*W*.40,yy,.046),(W*.012,.010,.018),2 if i%3==0 else 3)
        _screws(handguard,side*W*.435,[hg0+(hg1-hg0)*.2,hg0+(hg1-hg0)*.5,hg0+(hg1-hg0)*.8],.015,lod,W*.04)
    # Shotgun's sliding pump and under-barrel tube remain independent animated objects.
    moving={'magazine':mag,'trigger':trigger,'safety_lever':safety,'charging_handle':charging}
    if kind=='shotgun':
        pump_y=(hg0+hg1)*.58
        pump=_component(parts,'pump',(0,pump_y,-.058))
        pump.box((0,pump_y,-.058),(W*.72,.105,.052),2,_bevel(lod,(W*.72,.105,.052),True))
        for i in range((8,5,2)[lod]):pump.box((0,pump_y-.044+i*.012,-.058),(W*.73,.005,.055),3)
        tube2=_tube(parts,'magazine_tube',(0,(hg0+L*.47)/2,-.055),W*.11,L*.47-hg0,lod,1)
        moving['pump']=pump
        moving['magazine_tube']=tube2
    if kind=='sniper':
        bipod=_component(parts,'bipod',(0,L*.22,-.075))
        for side in (-1,1):
            bipod.tube((side*W*.20,L*.20,-.10),W*.025,L*.27,s,3,'Z',[(-.5,.8),(.5,1)])
            bipod.tube((side*W*.24,L*.20,-.235),W*.035,.05,s,2,'Y')
        moving['bipod']=bipod
    socket={'socket_muzzle':(0,L*.505,.010),'socket_eject':(W*.46,receiver_y+.04,.065),
            'socket_hand_r':(0,receiver_y-L*.10,-.10),'socket_hand_l':(0,(hg0+hg1)*.52,-.045)}
    return socket,moving


def _add_grenade(spec,lod,parts):
    kind=spec['kind'];L=spec['length'];W=spec['width'];s=12 if lod==0 else _segments(lod)
    body=_component(parts,'body',(0,0,0))
    if kind=='frag':
        profile=([(-.50,.70),(-.40,.86),(-.27,1.0),(.20,1.0),(.39,.82),(.50,.58)] if lod==0 else
                 [(-.50,.70),(-.42,.84),(-.28,1.0),(-.10,1.0),(.12,1.0),(.31,.92),(.43,.76),(.50,.58)])
    elif kind=='flash':
        profile=[(-.50,.76),(-.43,.92),(-.34,1),(.32,1),(.43,.92),(.50,.70)]
    elif kind=='smoke':
        profile=[(-.50,.62),(-.40,.84),(-.30,1),(.26,1),(.39,.86),(.50,.62)]
    else:
        profile=[(-.50,.56),(-.42,.68),(-.22,.86),(.12,.92),(.34,.72),(.44,.48),(.50,.32)]
    body.tube((0,0,0),W*.5,L,s,0,'Y',profile)
    # End caps and a class-coded collar; each grenade has its own silhouette language.
    cap=_component(parts,'cap',(0,L*.43,0))
    cap.tube((0,L*.43,0),W*.29,L*.16,s,1,'Y',[(-.5,.78),(-.32,1),(.32,1),(.5,.74)])
    cap.torus((0,L*.43,0),W*.30,W*.022,s,5,3,'Y')
    collar=_component(parts,'collar',(0,-L*.39,0))
    collar.tube((0,-L*.39,0),W*.49,L*.09,s,3,'Y',[(-.5,.92),(-.25,1),(.25,1),(.5,.92)])
    collar.torus((0,-L*.39,0),W*.49,W*.017,s,5,2,'Y')
    if kind=='frag':
        # Deep hexagonal armor cells around the body, an original repeated motif.
        count=(6,8,5)[lod]
        for i in range(count):
            y=-L*.27+i*(L*.54/max(1,count-1))
            for angle in (0,math.pi/2,math.pi,3*math.pi/2):
                x=math.cos(angle)*W*.48;z=math.sin(angle)*W*.48
                body.box((x,y,z),(W*.11,L*.024,W*.09),2 if i%4==0 else 3,0)
    elif kind=='smoke':
        for y in ((-L*.25,L*.25) if lod==0 else (-L*.25,-L*.08,L*.12,L*.29)):
            body.torus((0,y,0),W*.49,W*.012,s,5,2,'Y')
        # Offset cross-hatch plates on opposing faces.
        for side in (-1,1):
            for i in range((4 if lod==0 else (7,4,2)[lod])):
                body.box((side*W*.48,-L*.20+i*L*.066,0),(W*.018,.026,W*.16),1 if i%2 else 2)
    elif kind=='flash':
        body.torus((0,0,0),W*.50,W*.010,s,4,2,'Y')
        for i in range(6 if lod==0 else (8,5,2)[lod]):
            body.box((0,-L*.30+i*L*.085,W*.47),(W*.20,.012,.009),2 if i==0 else 3)
    else:
        # Bottle-like shoulder and neck with a guarded ignition cap.
        neck=_component(parts,'neck',(0,L*.44,0))
        neck.tube((0,L*.44,0),W*.22,L*.17,s,3,'Y',[(-.5,.9),(-.25,1),(.30,.95),(.5,.75)])
        neck.torus((0,L*.49,0),W*.22,W*.016,s,5,2,'Y')
        body.box((W*.22,L*.39,0),(W*.07,.012,.012),2,0)
    # Lever, pull ring and retaining pin are independent meshes and animated on use.
    lever=_component(parts,'safety_lever',(0,L*.39,0))
    lever.box((0,L*.39,0),(W*.78,.025,.013),2,_bevel(lod,(W*.78,.025,.013),True))
    pin=_component(parts,'pull_ring',(W*.20,L*.43,0))
    pin.torus((W*.20,L*.43,0),W*.105,W*.017,s,6,3,'X')
    pin.tube((W*.12,L*.40,0),W*.018,W*.11,s,1,'Z')
    release=_component(parts,'retaining_pin',(-W*.17,L*.40,0))
    release.tube((-W*.17,L*.40,0),W*.026,W*.035,s,3,'X')
    # Raised color-coded bands and small service marks.
    for i,y in enumerate((-L*.22,L*.22)):
        body.torus((0,y,0),W*.497,W*.009,s,5,2 if i==0 else 3,'Y')
    socket={'socket_muzzle':(0,L*.50,0),'socket_eject':(W*.49,0,0),
            'socket_hand_r':(0,-W*.35,-L*.18),'socket_hand_l':(0,L*.38,0)}
    return socket,{'safety_lever':lever,'pull_ring':pin,'retaining_pin':release}


def _add_knife(spec,lod,parts):
    L=spec['length'];W=spec['width'];s=_segments(lod)
    blade_len=L*.58;handle_len=L*.34
    blade=_component(parts,'blade',(0,L*.17,0))
    # Sculpted spear/harpoon outline with a raised spine, double bevel and shallow fuller.
    outline=[(-blade_len*.50,-.012),(-blade_len*.31,-.024),(blade_len*.20,-.025),
             (blade_len*.43,-.016),(blade_len*.50,0),(blade_len*.27,.025),
             (-blade_len*.33,.023),(-blade_len*.50,.010)]
    blade.prism_x((0,L*.17,0),outline,W*.34,1)
    blade.box((0,L*.17,W*.18),(W*.10,blade_len*.72,.006),3)
    blade.box((W*.09,L*.17,W*.145),(W*.06,blade_len*.56,.004),2)
    # Scalloped back edge, notched in a deliberate fictional pattern.
    for i in range((14,8,0)[lod]):
        y=L*.17+blade_len*.15+i*blade_len*.022
        blade.box((0,y,W*.17),(W*.32,.004,.006),3)
    guard=_component(parts,'guard',(0,-L*.11,0))
    guard.box((0,-L*.11,0),(W*1.45,.026,.040),2,_bevel(lod,(W*1.45,.026,.040),True))
    for side in (-1,1):
        guard.box((side*W*.53,-L*.11,.014),(W*.28,.020,.018),3,_bevel(lod,(W*.28,.020,.018),True))
    handle=_component(parts,'handle',(0,-L*.30,-.004))
    handle.box((0,-L*.30,-.004),(W*.72,handle_len,.052),0,_bevel(lod,(W*.72,handle_len,.052),True),tilt=.025)
    for side in (-1,1):
        handle.box((side*W*.35,-L*.30,-.004),(W*.06,handle_len*.88,.043),1,_bevel(lod,(W*.06,handle_len*.88,.043),True))
        count=(12,7,3)[lod]
        for i in range(count):
            yy=-L*.30-handle_len*.39+i*handle_len*.78/max(1,count-1)
            handle.box((side*W*.39,yy,-.004),(W*.014,.008,.046),3)
    pommel=_component(parts,'pommel',(0,-L*.43,0))
    pommel.tube((0,-L*.43,0),W*.39,.024,s,3,'Y',[(-.5,.8),(-.25,1),(.25,1),(.5,.8)])
    pommel.torus((0,-L*.43,0),W*.33,.006,s,5,2,'Y')
    for side in (-1,1):
        for y in (-L*.22,-L*.36):
            handle.tube((side*W*.39,y,0),W*.065,.004,s,2,'X',smooth=False)
            handle.box((side*W*.41,y,0),(W*.018,.012,.003),0)
    # Textured guard transitions and patterned spine plates add facet readability.
    for i in range((10,6,2)[lod]):
        y=-L*.22+i*L*.018
        guard.box((0,y,.021),(W*.48,.005,.004),2 if i%3==0 else 3)
    socket={'socket_muzzle':(0,L*.49,0),'socket_eject':(W*.49,0,0),
            'socket_hand_r':(0,-L*.31,-.035),'socket_hand_l':(0,-L*.15,-.025)}
    return socket,{}


def _add_gun_lod1(spec, parts):
    """Readable third-person silhouette: broad facets, only signature controls."""
    L=spec['length'];W=spec['width'];kind=spec['kind'];moving={}
    receiver=_component(parts,'receiver',(0,0,0))
    receiver.box((0,0,0),(W*.72,L*.25,.075),0,.004)
    receiver.box((0,L*.035,.025),(W*.78,L*.16,.026),1)
    is_pistol=kind=='pistol'
    if is_pistol:
        slide=_component(parts,'slide',(0,0,.04))
        slide.box((0,L*.12,.045),(W*.78,L*.47,.052),0,.003)
        slide.box((0,L*.13,.073),(W*.57,L*.31,.008),1)
        for side in (-1,1):
            for i in range(4):slide.box((side*W*.395,L*.035+i*L*.013,.045),(W*.024,.004,.025),2 if i==0 else 3)
        barrel=_component(parts,'barrel',(0,L*.35,.02))
        barrel.tube((0,L*.35,.02),W*.13,L*.29,8,1,'Y',[(-.5,.82),(-.40,1),(.38,1),(.5,.9)])
        brake=_component(parts,'muzzle_brake',(0,L*.49,.02))
        brake.tube((0,L*.49,.02),W*.19,L*.055,8,3,'Y',[(-.5,.8),(-.3,1),(.3,1),(.5,.8)])
        brake.torus((0,L*.49,.02),W*.19,.0028,8,4,2,'Y')
        grip=_component(parts,'grip',(0,-L*.19,-.07));grip.box((0,-L*.19,-.07),(W*.60,L*.34,.12),0,.006,tilt=.1)
        grip.box((0,-L*.19,-.005),(W*.48,L*.24,.012),2)
        mag=_component(parts,'magazine',(0,-L*.20,-.13));mag.box((0,-L*.20,-.13),(W*.40,L*.15,.065),1,.003)
        mag.box((0,-L*.24,-.17),(W*.46,.012,.01),3)
        mount=_component(parts,'optic_mount',(0,L*.06,.084));mount.box((0,L*.06,.084),(W*.42,.075,.012),2)
        sight=_component(parts,'optic_sight',(0,L*.17,.105));sight.box((0,L*.17,.105),(W*.12,.016,.022),3)
        sight.box((0,L*.39,.108),(W*.08,.012,.018),2)
        safety=_component(parts,'safety_lever',(W*.39,-L*.04,.025));safety.box((W*.39,-L*.04,.025),(W*.018,.034,.012),2)
        trigger=_component(parts,'trigger',(0,-L*.07,-.045));trigger.box((0,-L*.07,-.045),(W*.09,.012,.035),3)
        guard=_component(parts,'trigger_guard',(0,-L*.07,-.067))
        guard.box((0,-L*.105,-.068),(W*.30,.010,.008),1)
        for side in (-1,1):guard.box((side*W*.15,-L*.07,-.068),(W*.016,.055,.008),1)
        moving.update(magazine=mag,trigger=trigger,safety_lever=safety)
    else:
        receiver.box((0,-L*.045,-.005),(W*.70,L*.24,.075),0,.004)
        barrel=_component(parts,'barrel',(0,L*.34,.025))
        barrel.tube((0,L*.34,.025),W*.095,L*.45,8,1,'Y',[(-.5,.84),(-.4,1),(.4,1),(.5,.92)])
        brake=_component(parts,'muzzle_brake',(0,L*.49,.025))
        brake.tube((0,L*.49,.025),W*.15,L*.045,8,3,'Y',[(-.5,.8),(-.3,1),(.3,1),(.5,.8)])
        brake.torus((0,L*.49,.025),W*.15,.0028,8,4,2,'Y')
        hg0=-L*.05;hg1=L*.22
        hand=_component(parts,'handguard',(0,(hg0+hg1)/2,.015))
        hand.box((0,(hg0+hg1)/2,.015),(W*.72,hg1-hg0,.065),0,.004)
        hand.box((0,(hg0+hg1)/2,.051),(W*.58,(hg1-hg0)*.72,.01),1)
        for i in range(4):hand.box((0,hg0+.045+i*(hg1-hg0-.09)/3,.052),(W*.20,.010,.003),2 if i==0 else 3)
        stock=_component(parts,'stock',(0,-L*.24,-.005))
        stock.box((0,-L*.24,-.005),(W*.60,L*.21,.083),0,.004)
        stock.box((0,-L*.33,-.005),(W*.65,L*.07,.085),1)
        grip=_component(parts,'grip',(0,-L*.11,-.08));grip.box((0,-L*.11,-.08),(W*.52,L*.13,.12),0,.006,tilt=.08)
        mag=_component(parts,'magazine',(0,-L*.04,-.075));mag.box((0,-L*.04,-.075),(W*.40,L*.18,.075),1,.004)
        mag.box((0,-L*.10,-.13),(W*.44,.014,.012),3)
        mount=_component(parts,'optic_mount',(0,L*.015,.085));mount.box((0,L*.015,.085),(W*.40,.10,.012),2)
        sight=_component(parts,'optic_sight',(0,L*.13,.12));sight.box((0,L*.13,.095),(W*.16,.065,.035),1)
        sight.box((0,L*.13,.12),(W*.15,.055,.05),3)
        sight.box((0,L*.13,.149),(W*.12,.04,.008),2)
        charging=_component(parts,'charging_handle',(W*.40,-L*.02,.06));charging.box((W*.40,-L*.02,.06),(W*.12,.065,.018),2)
        safety=_component(parts,'safety_lever',(W*.38,-L*.12,.02));safety.box((W*.38,-L*.12,.02),(W*.018,.035,.012),2)
        trigger=_component(parts,'trigger',(0,-L*.18,-.042));trigger.box((0,-L*.18,-.042),(W*.09,.014,.035),3)
        guard=_component(parts,'trigger_guard',(0,-L*.18,-.065))
        guard.box((0,-L*.215,-.067),(W*.30,.010,.008),1)
        for side in (-1,1):guard.box((side*W*.15,-L*.18,-.067),(W*.016,.055,.008),1)
        rail=_component(parts,'top_rail',(0,(hg0+L*.17)/2,.067))
        rail.box((0,(hg0+L*.17)/2,.067),(W*.27,L*.33,.012),3)
        for i in range(5):rail.box((0,hg0+.02+i*L*.055,.075),(W*.25,.009,.012),2 if i==0 else 3)
        side_rail=_component(parts,'side_rail',(W*.38,L*.06,.01))
        side_rail.box((W*.38,L*.06,.01),(W*.012,L*.14,.032),3)
        for i in range(3):side_rail.box((W*.39,L*.01+i*L*.05,.01),(W*.014,.009,.036),2)
        moving.update(magazine=mag,trigger=trigger,safety_lever=safety,charging_handle=charging)
        if kind=='shotgun':
            pump=_component(parts,'pump',(0,L*.10,-.055));pump.box((0,L*.10,-.055),(W*.72,.10,.05),2,.003)
            pump.box((0,L*.10,-.026),(W*.56,.055,.008),3)
            tube2=_component(parts,'magazine_tube',(0,L*.28,-.065))
            tube2.tube((0,L*.28,-.065),W*.09,L*.34,8,1,'Y')
            moving.update(pump=pump,magazine_tube=tube2)
        if kind=='sniper':
            scope=_component(parts,'optic_sight',(0,L*.09,.16))
            for yy in (-L*.01,L*.19):scope.box((0,yy,.124),(W*.44,.028,.085),2)
            scope.tube((0,L*.09,.16),W*.13,L*.20,8,2,'Y',[(-.5,.75),(-.35,1),(.35,1),(.5,.75)])
            scope.torus((0,-L*.01,.16),W*.13,.003,8,4,3,'Y')
            scope.torus((0,L*.19,.16),W*.13,.003,8,4,3,'Y')
            scope.box((0,L*.09,.105),(W*.10,.12,.035),1)
            bipod=_component(parts,'bipod',(0,L*.20,-.08))
            for side in (-1,1):bipod.tube((side*W*.20,L*.18,-.14),W*.022,L*.20,6,3,'Z',[(-.5,.8),(.5,1)])
            moving['bipod']=bipod
    sockets={'socket_muzzle':(0,L*.505,.01),'socket_eject':(W*.46,-L*.02,.07),
             'socket_hand_r':(0,-L*.16,-.10),'socket_hand_l':(0,L*.10,-.045)}
    return sockets,moving


def _add_grenade_lod(spec, parts, lod):
    kind=spec['kind'];L=spec['length'];W=spec['width'];n=8 if lod==1 else 6
    body=_component(parts,'body',(0,0,0))
    if kind=='frag':profile=[(-.50,.70),(-.34,.98),(.24,.98),(.45,.76)]
    elif kind=='flash':profile=[(-.50,.76),(-.38,1),(.38,1),(.50,.72)]
    elif kind=='smoke':profile=[(-.50,.62),(-.34,1),(.34,1),(.50,.62)]
    else:profile=[(-.50,.56),(-.34,.82),(.24,.88),(.45,.46)]
    body.tube((0,0,0),W*.5,L,n,0,'Y',profile)
    cap=_component(parts,'cap',(0,L*.42,0));cap.tube((0,L*.42,0),W*.30,L*.14,n,1,'Y',[(-.5,.8),(-.3,1),(.3,1),(.5,.75)])
    collar=_component(parts,'collar',(0,-L*.39,0));collar.tube((0,-L*.39,0),W*.48,L*.085,n,3,'Y',[(-.5,.9),(-.25,1),(.25,1),(.5,.9)])
    if lod==1:
        if kind=='frag':
            # Four broad shield ribs preserve the segmented silhouette without micro-panels.
            for i in range(4):
                y=-L*.24+i*L*.16
                for angle in (0,math.pi/2,math.pi,3*math.pi/2):
                    body.box((math.cos(angle)*W*.47,y,math.sin(angle)*W*.47),(W*.095,L*.10,W*.07),2 if i==0 else 3)
        elif kind=='smoke':
            for y in (-L*.20,L*.16):body.torus((0,y,0),W*.49,.002,n,4,2,'Y')
        elif kind=='flash':
            body.torus((0,0,0),W*.50,.002,n,4,2,'Y')
            for i in range(4):body.box((0,-L*.23+i*L*.15,W*.47),(W*.16,.012,.008),2 if i==0 else 3)
        else:
            neck=_component(parts,'neck',(0,L*.43,0));neck.tube((0,L*.43,0),W*.20,L*.14,n,3,'Y',[(-.5,.9),(-.2,1),(.5,.76)])
            body.box((W*.22,L*.38,0),(W*.06,.012,.012),2)
    else:
        if kind=='frag':
            body.box((W*.46,0,0),(W*.025,L*.46,W*.055),2)
            body.box((-W*.46,0,0),(W*.025,L*.46,W*.055),2)
        elif kind=='smoke':
            body.torus((0,0,0),W*.49,.002,n,4,2,'Y')
        elif kind=='molotov':
            neck=_component(parts,'neck',(0,L*.43,0));neck.tube((0,L*.43,0),W*.20,L*.14,n,3,'Y')
    lever=_component(parts,'safety_lever',(0,L*.40,0));lever.box((0,L*.40,0),(W*.76,.022,.012),2 if lod==1 else 1)
    ring=_component(parts,'pull_ring',(W*.19,L*.44,0));ring.torus((W*.19,L*.44,0),W*.10,.004,n,4,3,'X')
    ring.tube((W*.12,L*.40,0),W*.016,W*.08,n,1,'Z')
    pin=_component(parts,'retaining_pin',(-W*.16,L*.40,0));pin.tube((-W*.16,L*.40,0),W*.024,W*.03,n,3,'X')
    if lod==1:
        for y in (-L*.22,L*.22):body.torus((0,y,0),W*.495,.0018,n,4,2,'Y')
    sockets={'socket_muzzle':(0,L*.50,0),'socket_eject':(W*.49,0,0),
             'socket_hand_r':(0,-W*.35,-L*.18),'socket_hand_l':(0,L*.38,0)}
    return sockets,{'safety_lever':lever,'pull_ring':ring,'retaining_pin':pin}


def _add_knife_lod2(spec,parts):
    L=spec['length'];W=spec['width'];blade_len=L*.58
    blade=_component(parts,'blade',(0,L*.17,0))
    outline=[(-blade_len*.50,-.012),(-blade_len*.30,-.022),(blade_len*.22,-.02),(blade_len*.50,0),
             (blade_len*.22,.020),(-blade_len*.30,.018)]
    blade.prism_x((0,L*.17,0),outline,W*.24,1)
    handle=_component(parts,'handle',(0,-L*.31,-.002));handle.box((0,-L*.31,-.002),(W*.58,L*.31,.034),0)
    guard=_component(parts,'guard',(0,-L*.01,.002));guard.box((0,-L*.01,.002),(W*1.25,.025,.012),2)
    pommel=_component(parts,'pommel',(0,-L*.44,0));pommel.tube((0,-L*.44,0),W*.35,.018,6,3,'Y')
    sockets={'socket_muzzle':(0,L*.49,0),'socket_eject':(W*.49,0,0),
             'socket_hand_r':(0,-L*.31,-.035),'socket_hand_l':(0,-L*.15,-.025)}
    return sockets,{}


def _add_gun_lod2(spec,parts):
    L=spec['length'];W=spec['width'];kind=spec['kind'];moving={}
    receiver=_component(parts,'receiver',(0,0,0));receiver.box((0,0,0),(W*.74,L*.23,.07),0)
    if kind=='pistol':
        slide=_component(parts,'slide',(0,L*.12,.04));slide.box((0,L*.12,.04),(W*.78,L*.42,.045),0)
        barrel=_component(parts,'barrel',(0,L*.38,.02));barrel.tube((0,L*.38,.02),W*.13,L*.26,6,1,'Y')
        brake=_component(parts,'muzzle_brake',(0,L*.49,.02));brake.tube((0,L*.49,.02),W*.18,L*.04,6,3,'Y')
        grip=_component(parts,'grip',(0,-L*.19,-.07));grip.box((0,-L*.19,-.07),(W*.58,L*.31,.11),0)
        mag=_component(parts,'magazine',(0,-L*.21,-.13));mag.box((0,-L*.21,-.13),(W*.39,L*.13,.06),1)
        mount=_component(parts,'optic_mount',(0,L*.12,.065));mount.box((0,L*.12,.065),(W*.30,.20,.020),2)
        sight=_component(parts,'optic_sight',(0,L*.19,.08));sight.box((0,L*.19,.08),(W*.10,.20,.018),3)
        trigger=_component(parts,'trigger',(0,-L*.07,-.05));trigger.box((0,-L*.07,-.05),(W*.09,.02,.03),2)
        moving['magazine']=mag;moving['trigger']=trigger
    else:
        barrel=_component(parts,'barrel',(0,L*.36,.02));barrel.tube((0,L*.36,.02),W*.09,L*.42,6,1,'Y')
        brake=_component(parts,'muzzle_brake',(0,L*.49,.02));brake.tube((0,L*.49,.02),W*.15,L*.04,6,3,'Y')
        hand=_component(parts,'handguard',(0,L*.08,.01));hand.box((0,L*.08,.01),(W*.68,L*.27,.055),0)
        stock=_component(parts,'stock',(0,-L*.24,0));stock.box((0,-L*.24,0),(W*.58,L*.20,.07),1)
        grip=_component(parts,'grip',(0,-L*.12,-.07));grip.box((0,-L*.12,-.07),(W*.50,L*.12,.10),0)
        mag=_component(parts,'magazine',(0,-L*.02,-.06));mag.box((0,-L*.02,-.06),(W*.37,L*.17,.065),1)
        mount=_component(parts,'optic_mount',(0,L*.12,.052));mount.box((0,L*.12,.052),(W*.24,L*.23,.035),2)
        sight=_component(parts,'optic_sight',(0,L*.14,.09));sight.box((0,L*.14,.09),(W*.12,.065,.04),3)
        trigger=_component(parts,'trigger',(0,-L*.18,-.04));trigger.box((0,-L*.18,-.04),(W*.08,.02,.03),2)
        moving.update(magazine=mag,trigger=trigger)
        if kind=='sniper':
            scope=_component(parts,'optic_sight',(0,L*.11,.15))
            scope.tube((0,L*.11,.15),W*.13,L*.18,6,2,'Y',[(-.5,.78),(.5,1)])
            mount=_component(parts,'optic_mount',(0,L*.10,.105));mount.box((0,L*.10,.105),(W*.12,.11,.018),2)
            mount.box((0,L*.11,.132),(W*.38,.025,.054),3)
        if kind=='shotgun':
            pump=_component(parts,'pump',(0,L*.10,-.05));pump.box((0,L*.10,-.05),(W*.70,.09,.05),2)
            tube2=_component(parts,'magazine_tube',(0,L*.29,-.06));tube2.tube((0,L*.29,-.06),W*.085,L*.33,6,1,'Y')
            moving['pump']=pump
    sockets={'socket_muzzle':(0,L*.505,.01),'socket_eject':(W*.46,-L*.02,.07),
             'socket_hand_r':(0,-L*.16,-.10),'socket_hand_l':(0,L*.10,-.045)}
    return sockets,moving


def build_geometry(spec, lod):
    """Return component meshes, socket transforms, and moving component names."""
    if lod not in (0,1,2):raise ValueError('LOD must be 0, 1 or 2')
    parts={}
    if lod==2:
        if spec['kind']=='knife':sockets,moving=_add_knife_lod2(spec,parts)
        elif spec['kind'] in GRENADES:sockets,moving=_add_grenade_lod(spec,parts,lod)
        else:sockets,moving=_add_gun_lod2(spec,parts)
    elif lod==1:
        if spec['kind']=='knife':sockets,moving=_add_knife(spec,lod,parts)
        elif spec['kind'] in GRENADES:sockets,moving=_add_grenade_lod(spec,parts,lod)
        else:sockets,moving=_add_gun_lod1(spec,parts)
    elif spec['kind']=='pistol':sockets,moving=_add_handgun(spec,lod,parts)
    elif spec['kind'] in ('smg','rifle','sniper','shotgun'):sockets,moving=_add_long_gun(spec,lod,parts)
    elif spec['kind'] in GRENADES:sockets,moving=_add_grenade(spec,lod,parts)
    elif spec['kind']=='knife':sockets,moving=_add_knife(spec,lod,parts)
    else:raise ValueError('Unknown weapon kind: '+spec['kind'])
    # Keep stable object naming and four socket identifiers across all three LODs.
    return parts,sockets,moving
