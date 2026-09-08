using System;
using System.Collections.Generic;
using System.Text;
using REmind.Charting;

namespace REmind.Gameplay.Effects
{
    public static class ChartEffectPreparation
    {
        public static PreparedEffectPlan Prepare(PlayableChartSnapshot snapshot,
            IReadOnlyList<ChartHolder> holders, string gimmickId)
        {
            var registry = EffectRegistry.CreateDefault();
            var parameters = ChartEffectJsonCodec.BuildParameterMap(holders,
                gimmickId, registry);
            var result = EffectPreparation.Prepare(snapshot.EffectEvents, parameters,
                registry, gimmickId);
            if (!result.Succeeded)
            {
                var errors = new StringBuilder();
                foreach (CompileIssue issue in result.Issues)
                    errors.AppendLine(issue.Code + ": " + issue.Message);
                throw new FormatException(errors.ToString());
            }
            return result.Plan;
        }
    }
}
