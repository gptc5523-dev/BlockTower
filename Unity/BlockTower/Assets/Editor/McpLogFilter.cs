using System;
using UnityEditor;
using UnityEngine;

/// Drops MCP for Unity's "Suppressed duplicate command resent on a new connection" warning.
/// It fires whenever the MCP server loses its socket mid-command (a play mode change, or a second
/// Claude session driving the editor) and resends: the bridge already runs the command once only,
/// so the warning is noise (upstream CoplayDev/unity-mcp #1434). Every other log passes through.
[InitializeOnLoad]
static class McpLogFilter
{
    const string Noise = "Suppressed duplicate command resent on a new connection";

    static McpLogFilter()
    {
        if (Debug.unityLogger.logHandler is not Handler)
            Debug.unityLogger.logHandler = new Handler(Debug.unityLogger.logHandler);
    }

    sealed class Handler : ILogHandler
    {
        readonly ILogHandler inner;
        public Handler(ILogHandler inner) => this.inner = inner;

        public void LogFormat(LogType type, UnityEngine.Object context, string format, params object[] args)
        {
            if (type == LogType.Warning && (args is { Length: > 0 } && args[0] is string s ? s : format).Contains(Noise))
                return;
            inner.LogFormat(type, context, format, args);
        }

        public void LogException(Exception exception, UnityEngine.Object context) => inner.LogException(exception, context);
    }
}
