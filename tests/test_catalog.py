import json
import re
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

class CatalogTests(unittest.TestCase):
    def setUp(self):
        self.c = json.loads((ROOT / 'specs/console-catalog.json').read_text(encoding='utf-8'))

    def test_names(self):
        names = [v for m in self.c['modules'] for v in [m['name'], *m['aliases']]]
        self.assertEqual(len(names), len(set(names)))
        self.assertEqual(len(self.c['modules']), 15)

    def test_defaults(self):
        for m in self.c['modules']:
            self.assertEqual(m['maxActivationsPerMatch'], 3)
            self.assertEqual(m['durationRounds'], None if m['lifetime'] == 'oneShot' else 2)
            for p in m['parameters'].values():
                self.assertTrue(p['description'])
                v = p['default']
                if p['type'] in ('int', 'float'):
                    self.assertLessEqual(p['min'], v)
                    self.assertLessEqual(v, p['max'])
                    if p['type'] == 'int':
                        self.assertIs(type(v), int)
                elif p['type'] == 'enum':
                    self.assertIn(v, p['values'])
                elif p['type'] == 'bool':
                    self.assertIs(type(v), bool)
                elif p['type'] == 'color':
                    self.assertIsNotNone(re.fullmatch(p['pattern'], v))
                else:
                    self.fail('Неизвестный тип')

    def test_competitive(self):
        by = {m['name']: m for m in self.c['modules']}
        for name in ('wallhack', 'rage', 'noclip', 'rethrow', 'maxmoney', 'startmoney'):
            self.assertNotIn('Competitive', by[name]['modes'])
        for name in ('aimbot', 'triggerbot'):
            self.assertIs(by[name]['modeOverrides']['Competitive']['visibleOnly']['const'], True)
        self.assertFalse(self.c['policy']['competitiveHiddenEnemyReplication'])

if __name__ == '__main__':
    unittest.main()
