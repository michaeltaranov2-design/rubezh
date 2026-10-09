"""Проверки данных и эталон решений; НЕ игровой сервер и НЕ C# runtime."""
import math
import re

class ContractError(ValueError):
    pass

def require(ok, message):
    if not ok:
        raise ContractError(message)

def value_ok(p, value):
    kind = p.get('type')
    if kind in ('int', 'float'):
        if type(value) not in (int, float):
            return False
        if type(value) is float and not math.isfinite(value):
            return False
        return (kind != 'int' or type(value) is int) and p['min'] <= value <= p['max']
    if kind == 'bool':
        return type(value) is bool
    if kind == 'enum':
        return type(value) is str and value in p['values']
    if kind == 'color':
        return type(value) is str and re.fullmatch(p['pattern'], value) is not None
    return False

def validate_catalog(c):
    require(c.get('schemaVersion') == 2, 'Требуется схема 2')
    names = set()
    for m in c['modules']:
        for name in [m['name'], *m['aliases']]:
            require(name not in names, 'Дублирующийся модуль или алиас')
            names.add(name)
        require(m['maxActivationsPerMatch'] == 3, 'Неверная квота')
        require(m['lifetime'] in ('oneShot','roundLease'), 'Неизвестный жизненный цикл')
        require(m['durationRounds'] == (None if m['lifetime'] == 'oneShot' else 2), 'Неверный срок')
        require(m['scope'] in ('player', 'server'), 'Неверная область')
        require(m['requiresSvCheats'] is True, 'Обход sv_cheats')
        for p in m['parameters'].values():
            require(bool(p.get('description')) and value_ok(p, p['default']), 'Неверное значение по умолчанию')
        if m['scope'] == 'server':
            require(m['capability'] == 'server.economy', 'Неверное право серверного модуля')
    by = {m['name']: m for m in c['modules']}
    for name in ('wallhack', 'rage', 'noclip', 'rethrow', 'maxmoney', 'startmoney'):
        require('Competitive' not in by[name]['modes'], 'Запрещённый модуль в Competitive')
    for name in ('aimbot', 'triggerbot'):
        require(by[name]['modeOverrides']['Competitive']['visibleOnly']['const'] is True, 'Обход видимости')
    require(c['policy']['competitiveHiddenEnemyReplication'] is False, 'Утечка скрытых врагов')
    p = by['impacts']['replicationPolicy']
    require(p['ownShotsOnly'] and p['competitiveGeometry'] == 'visibleStaticOnly'
            and p['hiddenDynamicHits'] is False and p['reuseVisibilityFilter'], 'Утечка impacts')
    a = c['policy']['authorization']
    require(a['giveSelf'] == 'modifier.self' and a['giveOthers'] == 'modifier.others'
            and a['revokeSelf'] == 'authenticated' and a['revokeOthers'] == 'modifier.others'
            and a['serverScope'] == 'server.economy' and a['serverTarget'] == 'all'
            and a['clientClaimsAccepted'] is False, 'Неверная политика прав')
    d = c['policy']['idempotency']
    require(d['key'] == ['matchId', 'accountId', 'requestId'] and d['bindCanonicalPayloadHash']
            and d['conflict'] == 'reject' and d['requestIdBytes'] == 16
            and d['mutationLedgerRetention'] == 'untilMatchClosedAndArchived'
            and d['replayAfterEviction'] == 'lookupDurableLedger'
            and d['overLimit'] == 'rejectNewAllowExisting' and d['sameTransactionAsEffects'], 'Обход повторов')
    require(type(d['memoryResultCachePerAccount']) is int and type(d['maxNewMutationIdsPerAccountPerMatch']) is int
            and 1 <= d['memoryResultCachePerAccount'] <= 64
            and 1 <= d['maxNewMutationIdsPerAccountPerMatch'] <= 2048, 'Неограниченное хранение')
    for key, maximum in [('maxUtf8Bytes',1024),('maxTokens',32),('maxParameters',16),
                         ('maxQueuedPerAccount',8),('maxQueuedGlobal',256),
                         ('mutationRate',2),('mutationBurst',4),('readRate',5),('readBurst',10)]:
        require(type(c['policy']['transport'][key]) is int and 0 < c['policy']['transport'][key] <= maximum, 'Неограниченный транспорт')
    s = c['policy']['statusPrivacy']
    require(s['public'] == ['activeModuleNames','expiryRound'] and s['otherPlayerDetails'] == 'modifier.audit'
            and s['private'] == ['quota','parameters','requestHistory']
            and s['spectatorVisibility'] == 'sameOrStricter', 'Утечка status')
    e = c['policy']['economy']
    require(e['onExpiry'] == 'removeExpiredLayersThenClampStartToMax'
            and e['replacement'] == 'oneLayerPerModule' and e['orderIndependentExpiry']
            and e['scopeQuota'] == 'issuer', 'Нестабильное истечение экономики')
    return True

def authorized(operation, scope, target, capabilities, authenticated=True):
    if not authenticated or operation not in ('give', 'revoke'):
        return False
    if scope == 'server':
        return target == 'all' and 'server.economy' in capabilities
    if scope != 'player':
        return False
    return (operation == 'revoke' or 'modifier.self' in capabilities) if target == 'self' else 'modifier.others' in capabilities

def impact_visible(competitive, own_shot, static_geometry, visible):
    return own_shot and (not competitive or (static_geometry and visible))

def economy_after_expiry(base_start, base_max, start_layer=None, max_layer=None):
    maximum = base_max if max_layer is None else max_layer
    start = base_start if start_layer is None else start_layer
    require(type(start) is int and type(maximum) is int and min(start, maximum) >= 0, 'Неверные деньги')
    return min(start, maximum), maximum
