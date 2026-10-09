"""Тестирование запуска без локального Blender; процессы заменены явным тестовым исполнителем."""
import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch
from tools.run_weapons import version_allowed,command,find_blender,pipeline,check_manifest,ROOT

class LauncherTests(unittest.TestCase):
    def test_version_gate(self):
        self.assertTrue(version_allowed([4,5,0]))
        self.assertTrue(version_allowed([4,5,9]))
        self.assertFalse(version_allowed([5,0,0]))
        self.assertTrue(version_allowed([5,0,0],True))
        for v in ([4,4,9],[True,5,0],[4,5],None,[4,5,-1]):
            self.assertFalse(version_allowed(v,True))
    def test_background_isolated_command(self):
        cmd=command('/path with spaces/blender','probe_api.py',['--report','/tmp/my report.json'])
        self.assertEqual(cmd[0],'/path with spaces/blender')
        self.assertIn('--background',cmd);self.assertIn('--factory-startup',cmd)
        self.assertEqual(cmd[-1],'/tmp/my report.json')
        self.assertEqual(cmd[cmd.index('--python-exit-code')+1],'1')
    def test_explicit_path(self):
        with tempfile.TemporaryDirectory() as td:
            exe=Path(td)/'blender';exe.touch()
            self.assertEqual(find_blender(str(exe)),str(exe.resolve()))
            with self.assertRaises(ValueError):find_blender(str(exe)+'-missing')
    def test_discover_path(self):
        with patch('tools.run_weapons.shutil.which',return_value='/bin/blender'):
            self.assertEqual(find_blender(),'/bin/blender')
    def fake_runner(self,root,fail=None,version=None):
        calls=[]
        def run(cmd,log,timeout):
            name=log.stem;calls.append(name)
            if name==fail:raise RuntimeError('Синтетический отказ '+name)
            log.write_text('Синтетический тест, не Blender',encoding='utf-8')
            if name=='api':
                (root/'api.json').write_text(json.dumps({'apiCompatible':True,'version':version or [4,5,0]}),encoding='utf-8')
            if name in ('pilot','generate_all'):
                dest=root/('pilot' if name=='pilot' else 'full');dest.mkdir()
                ids=['pistol_klyn'] if name=='pilot' else [x['id'] for x in json.loads((ROOT/'assets/weapons/catalog.json').read_text(encoding='utf-8'))['assets']]
                (dest/'manifest.json').write_text(json.dumps({'status':'validated','assets':[{'id':x,'lods':[{}, {}, {}]} for x in ids]}),encoding='utf-8')
        return calls,run
    def test_pilot_only(self):
        with tempfile.TemporaryDirectory() as td:
            root=Path(td)/'run';calls,run=self.fake_runner(root)
            report=pipeline('fake-blender',root,runner=run)
            self.assertEqual(calls,['tests','api','pilot','pilot_check'])
            self.assertEqual(report['status'],'pilotValidated')
            self.assertFalse(report['gameBuildTested']);self.assertFalse(report['visualReview'])
    def test_full_after_pilot(self):
        with tempfile.TemporaryDirectory() as td:
            root=Path(td)/'run';calls,run=self.fake_runner(root)
            report=pipeline('fake-blender',root,full=True,runner=run)
            self.assertEqual(calls,['tests','api','pilot','pilot_check','generate_all','full_check'])
            self.assertEqual(report['status'],'exportsValidated')
    def test_failures_stop_pipeline(self):
        for failed in ('tests','api','pilot','pilot_check','generate_all','full_check'):
            with self.subTest(failed=failed),tempfile.TemporaryDirectory() as td:
                root=Path(td)/'run';calls,run=self.fake_runner(root,fail=failed)
                with self.assertRaises(RuntimeError):pipeline('fake-blender',root,full=True,runner=run)
                self.assertEqual(calls[-1],failed)
                report=json.loads((root/'session-report.json').read_text(encoding='utf-8'))
                self.assertEqual(report['status'],'failed')
    def test_no_overwrite(self):
        with tempfile.TemporaryDirectory() as td:
            with self.assertRaises(ValueError):pipeline('fake-blender',Path(td),runner=lambda *a: self.fail('Процесс не должен запускаться'))
    def test_newer_requires_opt_in(self):
        with tempfile.TemporaryDirectory() as td:
            root=Path(td)/'run';calls,run=self.fake_runner(root,version=[5,0,0])
            with self.assertRaises(ValueError):pipeline('fake-blender',root,runner=run)
            self.assertEqual(calls,['tests','api'])
        with tempfile.TemporaryDirectory() as td:
            root=Path(td)/'run';calls,run=self.fake_runner(root,version=[5,0,0])
            self.assertEqual(pipeline('fake-blender',root,allow_newer=True,runner=run)['status'],'pilotValidated')
    def test_manifest_fail_closed(self):
        with tempfile.TemporaryDirectory() as td:
            path=Path(td)
            for data in ({'status':'failed'}, {'status':'validated','assets':[]},
                         {'status':'validated','assets':[{'id':'a','lods':[]}]},
                         {'status':'validated','assets':[{'id':'a','lods':[1,2,3]}]*2}):
                (path/'manifest.json').write_text(json.dumps(data),encoding='utf-8')
                with self.assertRaises(ValueError):check_manifest(path,['a'])

if __name__=='__main__':unittest.main()
