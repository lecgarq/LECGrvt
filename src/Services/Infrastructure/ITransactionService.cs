using System;
using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface ITransactionService
    {
        void Run(Document doc, string name, Action<Document> action);

        T Run<T>(Document doc, string name, Func<Document, T> action);

        bool RunConditional(Document doc, string name, Func<Document, bool> action);

        void RunRollbackOnly(Document doc, string name, Action<Document> action);

        void RunWithOptions(Document doc, string name, Action<Document> action, Action<FailureHandlingOptions> configureOptions);

        void RunWithWarningHandler(Document doc, string name, Action<Document> action, IFailuresPreprocessor? preprocessor = null);
    }
}
