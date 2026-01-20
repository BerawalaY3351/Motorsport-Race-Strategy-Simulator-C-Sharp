using System;
using System.Collections.Generic;
using System.Linq;

namespace MotorsportStrategySim.Core
{
    public enum Compound { Soft, Medium, Hard }

    public sealed class RaceConfig
    {
        public int TotalLaps { get; init; }
        public double PitLossSeconds { get; init; }          // time lost for a pit stop under green
        public double FuelEffectPerLapSeconds { get; init; } // optional: laps get quicker as fuel burns off
        public double TrafficPenaltySeconds { get; init; }   // optional: constant penalty if you want
    }

    public sealed class TyreModel
    {
        public Compound Compound { get; init; }

        // Base lap time on fresh tyres at start of race (you can tune per track)
        public double BaseLapSeconds { get; init; }

        // Simple degradation model:
        // lapTime = base + (LinearDeg * stintLap) + (QuadraticDeg * stintLap^2)
        public double LinearDegSecondsPerLap { get; init; }
        public double QuadraticDegSecondsPerLap2 { get; init; }

        // Optional cliff: after this stint lap, add extra seconds per lap
        public int? CliffStartLap { get; init; }
        public double CliffExtraSecondsPerLap { get; init; }

        public double LapTimeSeconds(int raceLapIndex1Based, int stintLapIndex1Based, RaceConfig cfg)
        {
            double t = BaseLapSeconds;

            // Degradation within the stint
            t += LinearDegSecondsPerLap * stintLapIndex1Based;
            t += QuadraticDegSecondsPerLap2 * stintLapIndex1Based * stintLapIndex1Based;

            // Optional tyre cliff
            if (CliffStartLap.HasValue && stintLapIndex1Based >= CliffStartLap.Value)
            {
                int over = stintLapIndex1Based - CliffStartLap.Value + 1;
                t += CliffExtraSecondsPerLap * over;
            }

            // Fuel burn-off: later laps a bit quicker (negative effect)
            // Example: 0.03 means each lap later is 0.03s quicker than lap 1
            t -= cfg.FuelEffectPerLapSeconds * (raceLapIndex1Based - 1);

            // Optional constant traffic penalty (keep simple for MVP)
            t += cfg.TrafficPenaltySeconds;

            return t;
        }
    }

    public sealed record StintPlan(Compound Compound, int Laps);

    public sealed class StrategyPlan
    {
        public string Name { get; }
        public IReadOnlyList<StintPlan> Stints { get; }

        public StrategyPlan(string name, IEnumerable<StintPlan> stints)
        {
            Name = name;
            Stints = stints.ToList();
            if (Stints.Count == 0) throw new ArgumentException("Strategy must have at least one stint.");
            if (Stints.Any(s => s.Laps <= 0)) throw new ArgumentException("Each stint must have > 0 laps.");
        }

        public int TotalPlannedLaps => Stints.Sum(s => s.Laps);
        public int PitStops => Math.Max(0, Stints.Count - 1);
    }

    public sealed class LapDetail
    {
        public int RaceLap { get; init; }              // 1..TotalLaps
        public int StintNumber { get; init; }          // 1..N
        public int StintLap { get; init; }             // 1..stint laps
        public Compound Compound { get; init; }
        public double LapTimeSeconds { get; init; }    // pure lap time (no pit loss)
        public bool PitAfterLap { get; init; }         // pit occurs after completing this lap
    }

    public sealed class SimulationResult
    {
        public string StrategyName { get; init; } = "";
        public double TotalRaceSeconds { get; init; }
        public IReadOnlyList<LapDetail> Laps { get; init; } = Array.Empty<LapDetail>();
        public IReadOnlyList<int> PitLaps { get; init; } = Array.Empty<int>(); // pit after these laps
    }

    public sealed class RaceSimulator
    {
        private readonly RaceConfig _cfg;
        private readonly IReadOnlyDictionary<Compound, TyreModel> _tyres;

        public RaceSimulator(RaceConfig cfg, IEnumerable<TyreModel> tyreModels)
        {
            _cfg = cfg;
            _tyres = tyreModels.ToDictionary(t => t.Compound, t => t);
        }

        public SimulationResult Run(StrategyPlan plan)
        {
            if (plan.TotalPlannedLaps != _cfg.TotalLaps)
                throw new ArgumentException($"Strategy laps ({plan.TotalPlannedLaps}) must equal race laps ({_cfg.TotalLaps}).");

            var laps = new List<LapDetail>(_cfg.TotalLaps);
            var pitLaps = new List<int>();

            double total = 0.0;
            int raceLap = 0;

            for (int stintIdx = 0; stintIdx < plan.Stints.Count; stintIdx++)
            {
                var stint = plan.Stints[stintIdx];
                if (!_tyres.TryGetValue(stint.Compound, out var tyre))
                    throw new ArgumentException($"No tyre model provided for {stint.Compound}.");

                for (int stintLap = 1; stintLap <= stint.Laps; stintLap++)
                {
                    raceLap++;
                    double lapTime = tyre.LapTimeSeconds(raceLap, stintLap, _cfg);
                    total += lapTime;

                    bool pitAfter = (stintIdx < plan.Stints.Count - 1) && (stintLap == stint.Laps);
                    if (pitAfter)
                    {
                        total += _cfg.PitLossSeconds;
                        pitLaps.Add(raceLap);
                    }

                    laps.Add(new LapDetail
                    {
                        RaceLap = raceLap,
                        StintNumber = stintIdx + 1,
                        StintLap = stintLap,
                        Compound = stint.Compound,
                        LapTimeSeconds = lapTime,
                        PitAfterLap = pitAfter
                    });
                }
            }

            return new SimulationResult
            {
                StrategyName = plan.Name,
                TotalRaceSeconds = total,
                Laps = laps,
                PitLaps = pitLaps
            };
        }
    }
}
