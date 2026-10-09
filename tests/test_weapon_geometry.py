"""Чистые проверки авторской геометрии, LOD и неизменённого socket API."""
import json
import math
import unittest
from pathlib import Path
from tools.weapon_geometry import build_geometry

ROOT=Path(__file__).resolve().parents[1]
CATALOG=json.loads((ROOT/'assets/weapons/catalog.json').read_text(encoding='utf-8'))
SOCKETS=set(CATALOG['sockets'])


def triangles(parts):
    return sum(component.geometry.triangles for component in parts.values())


class WeaponGeometryTests(unittest.TestCase):
    def test_roster_and_style_families(self):
        from collections import Counter
        self.assertEqual(Counter(a['kind'] for a in CATALOG['assets']),
            {'pistol':2,'smg':2,'rifle':2,'sniper':1,'shotgun':1,'frag':1,'flash':1,'smoke':1,'molotov':1,'knife':1})
        self.assertEqual(len(CATALOG['skinPalettes']),3)
        self.assertEqual(CATALOG['atlasSize'],1024)

    def test_all_lods_fit_class_ranges_and_reduce(self):
        for spec in CATALOG['assets']:
            counts=[]
            for lod in range(3):
                parts,sockets,moving=build_geometry(spec,lod)
                count=triangles(parts);counts.append(count)
                low,high=spec['triangleRanges'][lod]
                self.assertLessEqual(low,count,(spec['id'],lod,count,'below range'))
                self.assertLessEqual(count,high,(spec['id'],lod,count,'above range'))
                self.assertEqual(set(sockets),SOCKETS,(spec['id'],lod))
                self.assertTrue(parts,(spec['id'],lod,'empty model'))
                self.assertEqual(len(parts),len(set(parts)),(spec['id'],lod,'duplicate component'))
                for name,component in parts.items():
                    self.assertEqual(component.name,name)
                    geom=component.geometry
                    self.assertEqual(len(geom.faces),len(geom.tiles),(spec['id'],lod,name))
                    self.assertTrue(geom.vertices,(spec['id'],lod,name,'no vertices'))
                    self.assertTrue(all(math.isfinite(v) for point in geom.vertices for v in point))
                    for face in geom.faces:
                        self.assertGreaterEqual(len(face),3)
                        self.assertEqual(len(face),len(set(face)))
                        self.assertTrue(all(type(i) is int and 0<=i<len(geom.vertices) for i in face))
                        a,b,c=(geom.vertices[face[i]] for i in range(3))
                        cross=((b[1]-a[1])*(c[2]-a[2])-(b[2]-a[2])*(c[1]-a[1]),
                               (b[2]-a[2])*(c[0]-a[0])-(b[0]-a[0])*(c[2]-a[2]),
                               (b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0]))
                        self.assertGreater(sum(x*x for x in cross),1e-18,(spec['id'],lod,name,'degenerate face'))
            self.assertGreater(counts[0],counts[1],spec['id'])
            self.assertGreater(counts[1],counts[2],spec['id'])

    def test_semantic_components_are_separate(self):
        for spec in CATALOG['assets']:
            parts,_,moving=build_geometry(spec,0)
            names=set(parts)
            if spec['kind']=='pistol':
                self.assertTrue({'receiver','slide','barrel','magazine','grip','trigger','safety_lever','optic_sight','optic_mount'}<=names)
                self.assertTrue({'magazine','trigger','safety_lever'}<=set(moving))
            elif spec['kind'] in ('smg','rifle','sniper','shotgun'):
                self.assertTrue({'receiver','barrel','handguard','magazine','grip','trigger','safety_lever','optic_sight','optic_mount'}<=names)
                self.assertTrue({'magazine','trigger','safety_lever','charging_handle'}<=set(moving))
                if spec['kind']=='shotgun':self.assertTrue({'pump','magazine_tube'}<=names)
                if spec['kind']=='sniper':self.assertIn('bipod',names)
            elif spec['kind'] in ('frag','flash','smoke','molotov'):
                self.assertTrue({'body','cap','collar','safety_lever','pull_ring','retaining_pin'}<=names)
                self.assertTrue({'safety_lever','pull_ring','retaining_pin'}<=set(moving))
            else:
                self.assertTrue({'blade','guard','handle','pommel'}<=names)

    def test_sockets_remain_exact_and_finite(self):
        for spec in CATALOG['assets']:
            _,sockets,_=build_geometry(spec,0)
            self.assertEqual(set(sockets),SOCKETS)
            for position in sockets.values():
                self.assertEqual(len(position),3)
                self.assertTrue(all(math.isfinite(value) for value in position))
        self.assertNotIn('socket_sight',CATALOG['sockets'])

if __name__=='__main__':unittest.main()
