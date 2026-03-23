using System;

namespace LECG.Core
{
    internal interface IRevitSliceJob : IDisposable
    {
        bool ExecuteNextSlice();

        void Complete();

        void Fail(Exception ex);
    }
}
