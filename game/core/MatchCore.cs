using System;

namespace Rubezh.Core;

public enum RoundPhase { Freeze, Live, Bomb, End, MatchOver }
public enum TeamId { T = 0, Ct = 1 }
public enum WinReason { None, Elimination, BombExplode, Defuse, Time }

public sealed class MatchSettings
{
    public int Rounds = 16;
    public float FreezeSeconds = 15f;
    public float LiveSeconds = 120f;
    public float PlantSeconds = 3f;
    public float BombSeconds = 35f;
    public float DefuseSeconds = 7f;
    public float LastPlantSecond = 85f;
    public int SideSwapAfter = 8;
    public int StartMoney = 800;
    public int MaxMoney = 16000;
    public int KillReward = 300;
    public int WinReward = 3250;
    public int LossBase = 1400;
    public int LossIncrement = 500;
    public int LossCap = 3400;
    public int PlantReward = 800;
    public bool Overtime;
}

public struct EconomyState
{
    public int Money;
    public int LossStreak;
}

/// <summary>Серверная логика раундов и экономики. Без Godot, без сети.</summary>
public sealed class MatchState
{
    public readonly MatchSettings Cfg;
    public int RoundIndex;
    public RoundPhase Phase = RoundPhase.Freeze;
    public float PhaseTime;
    public int ScoreT, ScoreCt;
    public int AliveT, AliveCt;
    public bool BombPlanted, BombDefused;
    public float PlantProgress, DefuseProgress, BombLeft;
    public WinReason LastWin;
    public TeamId LastWinner;
    public bool SidesSwapped;
    public EconomyState EcoT, EcoCt;

    public MatchState(MatchSettings? cfg = null)
    {
        Cfg = cfg ?? new MatchSettings();
        EcoT.Money = Cfg.StartMoney;
        EcoCt.Money = Cfg.StartMoney;
        AliveT = AliveCt = 5;
    }

    public bool BuyOpen => Phase == RoundPhase.Freeze;
    public bool CanPlant => Phase == RoundPhase.Live && PhaseTime <= Cfg.LastPlantSecond && AliveT > 0;
    public bool MatchOver => Phase == RoundPhase.MatchOver;

    public void Kill(TeamId victim)
    {
        if (Phase is RoundPhase.End or RoundPhase.MatchOver) return;
        if (victim == TeamId.T) AliveT = Math.Max(0, AliveT - 1);
        else AliveCt = Math.Max(0, AliveCt - 1);
        Award(victim == TeamId.T ? TeamId.Ct : TeamId.T, Cfg.KillReward);
        TryElim();
    }

    public bool TryBuy(TeamId team, int price)
    {
        if (!BuyOpen || price < 0) return false;
        ref EconomyState e = ref Slot(team);
        if (e.Money < price) return false;
        e.Money -= price;
        return true;
    }

    public void Tick(float dt)
    {
        if (dt <= 0f) throw new ArgumentOutOfRangeException(nameof(dt));
        if (Phase == RoundPhase.MatchOver) return;
        PhaseTime += dt;
        switch (Phase)
        {
            case RoundPhase.Freeze:
                if (PhaseTime >= Cfg.FreezeSeconds) BeginLive();
                break;
            case RoundPhase.Live:
                if (PhaseTime >= Cfg.LiveSeconds) EndRound(TeamId.Ct, WinReason.Time);
                break;
            case RoundPhase.Bomb:
                BombLeft -= dt;
                if (BombDefused) EndRound(TeamId.Ct, WinReason.Defuse);
                else if (BombLeft <= 0f) EndRound(TeamId.T, WinReason.BombExplode);
                break;
        }
    }

    public bool BeginPlant(float dt)
    {
        if (!CanPlant) { PlantProgress = 0f; return false; }
        PlantProgress += dt;
        if (PlantProgress < Cfg.PlantSeconds) return false;
        BombPlanted = true;
        PlantProgress = 0f;
        Phase = RoundPhase.Bomb;
        PhaseTime = 0f;
        BombLeft = Cfg.BombSeconds;
        Award(TeamId.T, Cfg.PlantReward);
        return true;
    }

    public bool BeginDefuse(float dt)
    {
        if (Phase != RoundPhase.Bomb || BombDefused) { DefuseProgress = 0f; return false; }
        DefuseProgress += dt;
        if (DefuseProgress < Cfg.DefuseSeconds) return false;
        BombDefused = true;
        EndRound(TeamId.Ct, WinReason.Defuse);
        return true;
    }

    public void CancelPlant() => PlantProgress = 0f;
    public void CancelDefuse() => DefuseProgress = 0f;

    void BeginLive()
    {
        Phase = RoundPhase.Live;
        PhaseTime = 0f;
        PlantProgress = DefuseProgress = 0f;
        BombPlanted = BombDefused = false;
    }

    void TryElim()
    {
        if (Phase is RoundPhase.End or RoundPhase.MatchOver) return;
        if (AliveT == 0 && (!BombPlanted || BombDefused)) EndRound(TeamId.Ct, WinReason.Elimination);
        else if (AliveCt == 0 && Phase != RoundPhase.Bomb) EndRound(TeamId.T, WinReason.Elimination);
    }

    void EndRound(TeamId winner, WinReason reason)
    {
        if (Phase is RoundPhase.End or RoundPhase.MatchOver) return;
        Phase = RoundPhase.End;
        LastWinner = winner;
        LastWin = reason;
        if (winner == TeamId.T) ScoreT++; else ScoreCt++;
        Award(winner, Cfg.WinReward);
        TeamId loser = winner == TeamId.T ? TeamId.Ct : TeamId.T;
        ref EconomyState le = ref Slot(loser);
        le.LossStreak = Math.Min(le.LossStreak + 1, 4);
        Award(loser, Math.Min(Cfg.LossCap, Cfg.LossBase + (le.LossStreak - 1) * Cfg.LossIncrement));
        Slot(winner).LossStreak = 0;
    }

    public bool NextRound()
    {
        if (Phase != RoundPhase.End) return false;
        int played = ScoreT + ScoreCt;
        if (played >= Cfg.Rounds && !Cfg.Overtime)
        {
            Phase = RoundPhase.MatchOver;
            return false;
        }
        if (!SidesSwapped && played == Cfg.SideSwapAfter) SwapSides();
        RoundIndex++;
        Phase = RoundPhase.Freeze;
        PhaseTime = 0f;
        AliveT = AliveCt = 5;
        BombPlanted = BombDefused = false;
        PlantProgress = DefuseProgress = BombLeft = 0f;
        LastWin = WinReason.None;
        return true;
    }

    void SwapSides()
    {
        SidesSwapped = true;
        (ScoreT, ScoreCt) = (ScoreCt, ScoreT);
        (EcoT, EcoCt) = (EcoCt, EcoT);
        EcoT.Money = Math.Min(EcoT.Money, Cfg.MaxMoney);
        EcoCt.Money = Math.Min(EcoCt.Money, Cfg.MaxMoney);
    }

    void Award(TeamId team, int amount)
    {
        ref EconomyState e = ref Slot(team);
        e.Money = Math.Clamp(e.Money + amount, 0, Cfg.MaxMoney);
    }

    ref EconomyState Slot(TeamId team) => ref team == TeamId.T ? ref EcoT : ref EcoCt;
}
