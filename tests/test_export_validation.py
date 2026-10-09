import copy
import json
import struct
import sys
import tempfile
import unittest
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'tools'))
from validate_glb import read_glb,accessor

class ExportValidationTests(unittest.TestCase):
    def make(self,doc,binary=b'\0'*12):
        j=json.dumps(doc).encode();j+=b' ' *((-len(j))%4);binary+=b'\0'*((-len(binary))%4)
        return struct.pack('<III',0x46546c67,2,28+len(j)+len(binary))+struct.pack('<II',len(j),0x4e4f534a)+j+struct.pack('<II',len(binary),0x004e4942)+binary
    def doc(self):
        return {'asset':{'version':'2.0'},'buffers':[{'byteLength':12}], 'bufferViews':[{'buffer':0,'byteLength':12}], 'accessors':[{'bufferView':0,'componentType':5126,'count':1,'type':'VEC3'}]}
    def test_valid_accessor(self):
        d=self.doc();self.assertEqual(accessor(d,b'\0'*12,0),[(0.,0.,0.)])
    def test_accessor_overflow(self):
        d=self.doc();d['accessors'][0]['count']=2
        with self.assertRaises(ValueError):accessor(d,b'\0'*12,0)
    def test_nonfinite(self):
        with self.assertRaises(ValueError):accessor(self.doc(),struct.pack('<fff',float('nan'),0,0),0)
    def test_truncated_glb(self):
        with tempfile.TemporaryDirectory() as td:
            p=Path(td)/'a.glb';p.write_bytes(self.make(self.doc())[:-1])
            with self.assertRaises(ValueError):read_glb(p)
    def test_embedded_only(self):
        d=self.doc();d['buffers'][0]['uri']='external.bin'
        with tempfile.TemporaryDirectory() as td:
            p=Path(td)/'a.glb';p.write_bytes(self.make(d))
            with self.assertRaises(ValueError):read_glb(p)
    def test_valid_container(self):
        with tempfile.TemporaryDirectory() as td:
            p=Path(td)/'a.glb';p.write_bytes(self.make(self.doc()))
            d,b=read_glb(p);self.assertEqual(len(b),12)
    def test_python_syntax(self):
        for path in list((ROOT/'tools').rglob('*.py'))+list((ROOT/'tests').glob('*.py')):
            compile(path.read_text(encoding='utf-8'),str(path),'exec')


class ReferenceTests(unittest.TestCase):
    def test_negative_reference_rejected(self):
        from tools.validate_glb import ref
        for bad in (-1,True,1,1.0):
            with self.assertRaises(ValueError):ref([{}],bad)
        self.assertEqual(ref([{'a':1}],0),{'a':1})

if __name__ == '__main__': unittest.main()
