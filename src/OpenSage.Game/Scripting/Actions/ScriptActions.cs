using System;
using System.Collections.Generic;
using System.Diagnostics;
using OpenSage.Data.Ini;
using OpenSage.Diagnostics;

namespace OpenSage.Scripting;

internal static partial class ScriptActions
{
    private static NLog.Logger Logger => NLog.LogManager.GetCurrentClassLogger();

    private static readonly Dictionary<string, Enum> CachedEnumMap;

    static ScriptActions()
    {
        CachedEnumMap = IniParser.GetEnumMap<ScriptActionType>();
    }

    public static void Execute(ScriptExecutionContext context, ScriptAction action)
    {
        if (!action.Enabled)
        {
            return;
        }
        var actionType = action.ContentType;

        if (action.InternalName != null)
        {
            if (CachedEnumMap.TryGetValue(action.InternalName.Name, out var untypedActionType))
            {
                actionType = (ScriptActionType)untypedActionType;
            }
        }

        try
        {
            context.Scripting.ValidateAction(actionType);
            ExecuteImpl(context, action, actionType);
        }
        catch (Exception exception) when (exception is not SimulationFeatureException)
        {
            throw new SimulationFeatureException(context.ScriptName, null, $"ScriptAction:{actionType}", exception);
        }
    }
}
