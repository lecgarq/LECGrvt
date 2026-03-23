using System;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;

namespace LECG.Core
{
    internal sealed class RevitIdlingRunner
    {
        private readonly UIApplication _application;
        private readonly IRevitSliceJob _job;
        private bool _isSubscribed;

        public RevitIdlingRunner(UIApplication application, IRevitSliceJob job)
        {
            _application = application ?? throw new ArgumentNullException(nameof(application));
            _job = job ?? throw new ArgumentNullException(nameof(job));
        }

        public void Start()
        {
            if (_isSubscribed)
            {
                return;
            }

            _application.Idling += OnIdling;
            _isSubscribed = true;
        }

        private void OnIdling(object? sender, IdlingEventArgs e)
        {
            bool shouldDisposeJob = false;

            try
            {
                if (_job.ExecuteNextSlice())
                {
                    e.SetRaiseWithoutDelay();
                    return;
                }

                Unsubscribe();
                shouldDisposeJob = true;
                _job.Complete();
            }
            catch (Exception ex)
            {
                Unsubscribe();
                shouldDisposeJob = true;

                try
                {
                    _job.Fail(ex);
                }
                catch
                {
                }
            }
            finally
            {
                if (shouldDisposeJob)
                {
                    _job.Dispose();
                }
            }
        }

        private void Unsubscribe()
        {
            if (!_isSubscribed)
            {
                return;
            }

            _application.Idling -= OnIdling;
            _isSubscribed = false;
        }
    }
}
