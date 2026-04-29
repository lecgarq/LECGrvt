using System;
using System.Collections.Generic;
using Serilog.Context;

namespace LECG.Services.Logging
{
    internal static class CommandLogContext
    {
        public static IDisposable Begin(string commandName, string? documentTitle, string? revitVersion)
        {
            Scope scope = new Scope();
            scope.Add(LogContext.PushProperty("CommandName", commandName));

            if (!string.IsNullOrWhiteSpace(documentTitle))
            {
                scope.Add(LogContext.PushProperty("DocumentTitle", documentTitle));
            }

            if (!string.IsNullOrWhiteSpace(revitVersion))
            {
                scope.Add(LogContext.PushProperty("RevitVersion", revitVersion));
            }

            return scope;
        }

        private sealed class Scope : IDisposable
        {
            private readonly List<IDisposable> _tokens = new List<IDisposable>();

            public void Add(IDisposable token)
            {
                _tokens.Add(token);
            }

            public void Dispose()
            {
                for (int i = _tokens.Count - 1; i >= 0; i--)
                {
                    _tokens[i].Dispose();
                }
            }
        }
    }
}
