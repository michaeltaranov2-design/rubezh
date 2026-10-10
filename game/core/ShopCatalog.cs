using System;

namespace Rubezh.Core;

public static class ShopCatalog
{
    public static readonly (string Id, int Price, bool T, bool Ct)[] Items =
    {
        ("pistol_klyn", 0, true, true),
        ("pistol_duga", 500, true, true),
        ("smg_potok", 1200, true, true),
        ("smg_uzel", 1500, true, true),
        ("rifle_gran", 2700, true, true),
        ("rifle_vector", 3100, true, true),
        ("sniper_horda", 4750, true, true),
        ("shotgun_shov", 1800, true, true),
        ("knife_kasat", 0, true, true),
        ("grenade_frag", 300, true, true),
        ("grenade_flash", 200, true, true),
        ("grenade_smoke", 300, true, true),
        ("grenade_molotov", 400, true, false),
        ("grenade_molotov_ct", 400, false, true)
    };

    public static int Price(string id)
    {
        foreach (var it in Items) if (it.Id == id) return it.Price;
        return -1;
    }

    public static bool CanTeam(string id, TeamId team)
    {
        foreach (var it in Items)
            if (it.Id == id) return team == TeamId.T ? it.T : it.Ct;
        return false;
    }
}