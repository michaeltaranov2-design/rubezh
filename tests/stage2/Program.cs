using System;
using System.Text;
using Rubezh.Core;

namespace Rubezh.Core.Stage2Tests;

public static class Program
{
    static int _ok, _fail;
    static void Check(bool c, string n) { if (c) { _ok++; Console.WriteLine("  OK   " + n); } else { _fail++; Console.WriteLine("  FAIL " + n); } }
    public static int Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Match(); Maps(); Nades(); Loader(); Nav(); Shop();
        Console.WriteLine("Итого: пройдено " + _ok + ", провалено " + _fail);
        return _fail == 0 && _ok > 0 ? 0 : 1;
    }

    static void Match()
    {
        Console.WriteLine("[Раунды]");
        var m = new MatchState();
        Check(m.BuyOpen && m.EcoT.Money == 800, "заморозка и стартовые 800");
        Check(m.TryBuy(TeamId.T, 2700) == false, "нельзя купить дороже денег");
        Check(m.TryBuy(TeamId.T, 500) && m.EcoT.Money == 300, "покупка в заморозке");
        m.Tick(15f);
        Check(m.Phase == RoundPhase.Live && !m.BuyOpen, "после 15 с — живая фаза");
        Check(!m.TryBuy(TeamId.T, 300), "покупка закрыта в live");
        m.Kill(TeamId.Ct); m.Kill(TeamId.Ct); m.Kill(TeamId.Ct); m.Kill(TeamId.Ct); m.Kill(TeamId.Ct);
        Check(m.Phase == RoundPhase.End && m.LastWin == WinReason.Elimination, "T выигрывает зачисткой");
        Check(m.NextRound() && m.RoundIndex == 1, "следующий раунд");
        var p = new MatchState();
        p.Tick(15f);
        Check(p.BeginPlant(3.1f) && p.Phase == RoundPhase.Bomb, "закладка бомбы");
        Check(p.BeginDefuse(7.1f) && p.LastWin == WinReason.Defuse, "разминирование");
        var e = new MatchState(); e.Tick(15f); e.BeginPlant(3.1f); e.Tick(35f);
        Check(e.LastWin == WinReason.BombExplode, "взрыв бомбы");
        var t = new MatchState(); t.Tick(15f); t.Tick(120f);
        Check(t.LastWin == WinReason.Time && t.LastWinner == TeamId.Ct, "таймаут — CT");
        var s = new MatchState();
        for (int i = 0; i < 8; i++) { s.Tick(15f); s.Kill(TeamId.Ct); s.Kill(TeamId.Ct); s.Kill(TeamId.Ct); s.Kill(TeamId.Ct); s.Kill(TeamId.Ct); s.NextRound(); }
        Check(s.SidesSwapped, "смена сторон после 8");
        Check(s.Cfg.Rounds == 16 && !s.Cfg.Overtime, "16 раундов, без овертайма");
    }

    static void Maps()
    {
        Console.WriteLine("[Карты]");
        var all = MapCatalog.All();
        Check(all.Length == 3, "три карты");
        Check(all[0].SizeClass == "compact" && all[1].SizeClass == "medium" && all[2].SizeClass == "large", "компактная/средняя/большая");
        foreach (var map in all)
        {
            var err = MapChecks.Validate(map);
            Check(err == null, map.DisplayName + (err == null ? " проходима" : ": " + err));
            Check(map.License == "CC0-1.0" && map.Author == "Рубеж", map.Id + " лицензия");
        }
    }

    static void Nades()
    {
        Console.WriteLine("[Гранаты]");
        var f = GrenadeCatalog.Frag;
        Check(GrenadeCore.BlastDamage(f, default, default) == f.Damage, "урон в эпицентре");
        Check(GrenadeCore.BlastDamage(f, default, new System.Numerics.Vector3(f.Radius, 0, 0)) == 0, "за радиусом 0");
        Check(GrenadeCatalog.Flash.FlashDuration > 0 && GrenadeCatalog.Smoke.SmokeDuration > 0, "флеш и смок");
        Check(GrenadeCatalog.Molotov.Damage > 0, "молотов");
    }

    static void Loader()
    {
        Console.WriteLine("[Загрузчик]");
        Check(MapLoader.Inspect(Array.Empty<byte>(), out _) != null, "пустой файл отклонён");
        Check(MapLoader.Inspect(new byte[32], out _) != null, "мусор отклонён");
        byte[] json = Encoding.UTF8.GetBytes("{\"asset\":{\"version\":\"2.0\"},\"buffers\":[{\"uri\":\"http://x\"}]}");
        Check(MapLoader.Inspect(json, out _) != null, "внешний URI отклонён");
        byte[] ok = Encoding.UTF8.GetBytes("{\"asset\":{\"version\":\"2.0\"},\"extras\":{\"rubezhMapId\":\"klin_greybox\"}}");
        string? id;
        Check(MapLoader.Inspect(ok, out id) == null && id == "klin_greybox", "валидный glTF JSON принят");
    }

    static void Nav()
    {
        Console.WriteLine("[Боты 20 раундов]");
        foreach (var map in MapCatalog.All())
        {
            var r = BotNavSim.Run(map, 20, 7);
            Check(r.Ok, map.DisplayName + " 20 раундов stuck=" + r.Stuck + " fall=" + r.Falls);
        }
    }

    static void Shop()
    {
        Console.WriteLine("[Магазин]");
        Check(ShopCatalog.Price("rifle_gran") == 2700, "цена винтовки");
        Check(ShopCatalog.CanTeam("grenade_molotov", TeamId.T) && !ShopCatalog.CanTeam("grenade_molotov", TeamId.Ct), "молотов только T");
        Check(ShopCatalog.Items.Length >= 13, "полный набор оружия и гранат");
    }
}
