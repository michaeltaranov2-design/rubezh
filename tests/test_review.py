import copy
import json
import unittest
from pathlib import Path
from tools.catalog_contract import ContractError, validate_catalog, value_ok, authorized, impact_visible, economy_after_expiry
ROOT = Path(__file__).resolve().parents[1]

class ReviewTests(unittest.TestCase):
    def setUp(self):
        self.c = json.loads((ROOT / 'specs/console-catalog.json').read_text(encoding='utf-8'))
    def test_valid(self):
        self.assertTrue(validate_catalog(self.c))
    def test_r01_hidden_impacts(self):
        for own, static, visible in [(True,False,True),(True,True,False),(False,True,True)]:
            self.assertFalse(impact_visible(True, own, static, visible))
        self.assertTrue(impact_visible(True, True, True, True))
        self.assertTrue(impact_visible(False, True, False, False))
        next(m for m in self.c['modules'] if m['name']=='impacts')['replicationPolicy']['hiddenDynamicHits'] = True
        with self.assertRaises(ContractError): validate_catalog(self.c)
    def test_r02_revoke_authority(self):
        self.assertTrue(authorized('revoke','player','self',set()))
        self.assertFalse(authorized('revoke','player','other',set()))
        self.assertFalse(authorized('revoke','server','all',{'modifier.others'}))
        self.assertTrue(authorized('revoke','server','all',{'server.economy'}))
        self.assertFalse(authorized('give','server','self',{'server.economy'}))
        self.assertFalse(authorized('revoke','player','self',set(),False))
        self.c['policy']['authorization']['revokeOthers']='authenticated'
        with self.assertRaises(ContractError): validate_catalog(self.c)
    def test_r03_idempotency_invariants(self):
        for key, value in [('bindCanonicalPayloadHash',False),('memoryResultCachePerAccount',100000),
                           ('replayAfterEviction','executeAgain'),('sameTransactionAsEffects',False),
                           ('mutationLedgerRetention','60seconds'),('maxNewMutationIdsPerAccountPerMatch',100000)]:
            c=copy.deepcopy(self.c); c['policy']['idempotency'][key]=value
            with self.subTest(key=key), self.assertRaises(ContractError): validate_catalog(c)
    def test_r04_bounded_transport(self):
        for key in self.c['policy']['transport']:
            c=copy.deepcopy(self.c); c['policy']['transport'][key]=10**9
            with self.subTest(key=key), self.assertRaises(ContractError): validate_catalog(c)
    def test_r05_status_privacy(self):
        self.c['policy']['statusPrivacy']['public'].append('parameters')
        with self.assertRaises(ContractError): validate_catalog(self.c)
    def test_r06_economy_expiry(self):
        self.assertEqual(economy_after_expiry(800,16000,20000,None),(16000,16000))
        self.assertEqual(economy_after_expiry(800,16000,None,0),(0,0))
        self.assertEqual(economy_after_expiry(800,16000),(800,16000))
        self.c['policy']['economy']['onExpiry']='rejectExpiry'
        with self.assertRaises(ContractError): validate_catalog(self.c)
    def test_r07_invalid_types(self):
        for value in [True,float('nan'),float('inf'),'5']:
            self.assertFalse(value_ok({'type':'float','min':0,'max':10},value))
        self.assertFalse(value_ok({'type':'int','min':0,'max':10},1.5))
    def test_r07_alias_collision(self):
        self.c['modules'][1]['aliases'].append(self.c['modules'][0]['name'])
        with self.assertRaises(ContractError): validate_catalog(self.c)
    def test_r08_existing_behavior_preserved(self):
        base=json.loads((ROOT / 'review/baseline/specs/console-catalog.json').read_text(encoding='utf-8'))
        for old, new in zip(base['modules'],self.c['modules']):
            self.assertEqual({k:new[k] for k in old}, old)
        self.assertEqual(base['commands'],self.c['commands'])
        self.assertEqual(base['match'],self.c['match'])
        for k,v in base['policy'].items(): self.assertEqual(self.c['policy'][k],v)


class StrictPolicyTests(unittest.TestCase):
    def test_unknown_lifetime(self):
        c=json.loads((ROOT/'specs/console-catalog.json').read_text(encoding='utf-8'))
        c['modules'][0]['lifetime']='forever'
        with self.assertRaises(ContractError):validate_catalog(c)
    def test_boolean_is_not_capacity(self):
        c=json.loads((ROOT/'specs/console-catalog.json').read_text(encoding='utf-8'))
        c['policy']['idempotency']['memoryResultCachePerAccount']=True
        with self.assertRaises(ContractError):validate_catalog(c)

class LargeNumberTests(unittest.TestCase):
    def test_huge_integer_rejected_without_float_conversion(self):
        self.assertFalse(value_ok({'type':'float','min':0,'max':10},10**1000))

if __name__ == '__main__': unittest.main()
