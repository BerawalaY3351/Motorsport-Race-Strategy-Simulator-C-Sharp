using System;
using MotorsportStrategySim.Core;

class Program
{
    static void Main()
    {
        var cfg = new RaceConfig
        {
            TotalLaps = 78,
            PitLossSeconds = 21.5,
            FuelEffectPerLapSeconds = 0.03,  // laps get ~0.03s quicker each lap (tweak per track)
            TrafficPenaltySeconds = 0.00
        };

        var tyres = new[]
        {
            new TyreModel { Compound = Compound.Soft,   BaseLapSeconds = 74.8, LinearDegSecondsPerLap = 0.060, QuadraticDegSecondsPerLap2 = 0.0006, CliffStartLap = 18, CliffExtraSecondsPerLap = 0.08 },
            new TyreModel { Compound = Compound.Medium, BaseLapSeconds = 75.5, LinearDegSecondsPerLap = 0.040, QuadraticDegSecondsPerLap2 = 0.0004, CliffStartLap = 28, CliffExtraSecondsPerLap = 0.05 },
            new TyreModel { Compound = Compound.Hard,   BaseLapSeconds = 76.2, LinearDegSecondsPerLap = 0.030, QuadraticDegSecondsPerLap2 = 0.0003, CliffStartLap = 40, CliffExtraSecondsPerLap = 0.03 }
        };

        var sim = new RaceSimulator(cfg, tyres);

        var stratA = new StrategyPlan(
            "S-M-H",
            new[] { new StintPlan(Compound.Soft, 18), new StintPlan(Compound.Medium, 30), new StintPlan(Compound.Hard, 30) }
        );

        var stratB = new StrategyPlan(
            "M-H-H",
            new[] { new StintPlan(Compound.Medium, 25), new StintPlan(Compound.Hard, 27), new StintPlan(Compound.Hard, 26) }
        );

        var resA = sim.Run(stratA);
        var resB = sim.Run(stratB);

        Print(resA);
        Print(resB);

        Console.WriteLine();
        Console.WriteLine($"Delta (B - A): {resB.TotalRaceSeconds - resA.TotalRaceSeconds:0.000}s");
    }

    static void Print(SimulationResult r)
    {
        Console.WriteLine($"{r.StrategyName} | Total: {r.TotalRaceSeconds:0.000}s | Pits after laps: {(r.PitLaps.Count == 0 ? "none" : string.Join(", ", r.PitLaps))}");
    }
}
