using System;
using Autodesk.Revit.UI;

namespace LECG.Core
{
    public abstract class ExternalEventCommand<THandler> : RevitCommand
        where THandler : class, IExternalEventHandler, new()
    {
        private static ExternalEvent? _externalEvent;
        private static THandler? _handler;

        protected THandler GetOrCreateHandler()
        {
            _handler ??= new THandler();
            _externalEvent ??= ExternalEvent.Create(_handler);
            return _handler;
        }

        protected void RaiseExternalEvent()
        {
            if (_externalEvent == null)
            {
                throw new InvalidOperationException("External event has not been initialized.");
            }

            _externalEvent.Raise();
        }
    }
}
