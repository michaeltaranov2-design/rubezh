"""Статические регрессии защит offline-генератора, не тест bpy."""
import ast
import unittest
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]

class GeneratorGuardTests(unittest.TestCase):
    def setUp(self):
        self.source=(ROOT/'tools/blender/generate_weapons.py').read_text(encoding='utf-8')
        self.tree=ast.parse(self.source)
    def test_cleanup_actions(self):
        fn=next(n for n in self.tree.body if isinstance(n,ast.FunctionDef) and n.name=='clear_scene')
        code=ast.unparse(fn)
        self.assertIn('bpy.data.actions.remove(action, do_unlink=True)',code)
        self.assertIn('bpy.data.images',code)
    def test_background_and_no_overwrite(self):
        self.assertIn('if not bpy.app.background:',self.source)
        self.assertIn('if out.exists() and any(out.iterdir()):',self.source)
    def test_fail_closed_export(self):
        self.assertIn('report=validate_glb(',self.source)
        self.assertIn("if 'FINISHED' not in result:raise RuntimeError",self.source)
        self.assertIn("manifest['status']='failed'",self.source)
    def test_animation_contract(self):
        self.assertIn("export_animation_mode='NLA_TRACKS'",self.source)
        self.assertIn("export_merge_animation='NLA_TRACK'",self.source)
        self.assertIn("strip.action_slot=slot",self.source)
        self.assertIn("export_anim_slide_to_zero=True",self.source)

if __name__=='__main__':unittest.main()
