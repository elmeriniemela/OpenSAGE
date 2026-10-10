using System;
using System.Collections.Generic;
using OpenSage.Data.Ini;
using OpenSage.Diagnostics;

namespace OpenSage.Scripting;

internal static partial class ScriptConditions
{
    private static NLog.Logger Logger => NLog.LogManager.GetCurrentClassLogger();

    private static readonly Dictionary<string, Enum> CachedEnumMap;

    static ScriptConditions()
    {
        CachedEnumMap = IniParser.GetEnumMap<ScriptConditionType>();
    }

    public static bool Evaluate(ScriptExecutionContext context, ScriptCondition condition)
    {
        // A disabled condition contributes no restriction to its AND clause.
        if (!condition.Enabled)
        {
            return true;
        }
        var conditionType = condition.ContentType;

        if (condition.InternalName != null)
        {
            if (CachedEnumMap.TryGetValue(condition.InternalName.Name, out var untypedConditionType))
            {
                conditionType = (ScriptConditionType)untypedConditionType;
            }
        }

        try
        {
            context.Scripting.ValidateCondition(conditionType);
            var result = EvaluateImpl(context, condition, conditionType);
            return condition.IsInverted ? !result : result;
        }
        catch (Exception exception) when (exception is not SimulationFeatureException)
        {
            throw new SimulationFeatureException(context.ScriptName, null, $"ScriptCondition:{conditionType}", exception);
        }
    }
}
