using System;
using Autodesk.Revit.DB;
using LECG.Core;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class TransactionService : ITransactionService
    {
        public void Run(Document doc, string name, Action<Document> action)
        {
            ArgumentNullException.ThrowIfNull(action);
            RunInternal(doc, name, null, action);
        }

        public T Run<T>(Document doc, string name, Func<Document, T> action)
        {
            ArgumentNullException.ThrowIfNull(action);

            T result = default!;
            RunInternal(doc, name, null, currentDoc => result = action(currentDoc));
            return result;
        }

        public bool RunConditional(Document doc, string name, Func<Document, bool> action)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ArgumentNullException.ThrowIfNull(action);

            using Transaction transaction = new Transaction(doc, name);
            transaction.Start();

            try
            {
                bool shouldCommit = action(doc);

                if (shouldCommit)
                {
                    transaction.Commit();
                }
                else
                {
                    transaction.RollBack();
                }

                return shouldCommit;
            }
            catch
            {
                if (transaction.GetStatus() == TransactionStatus.Started)
                {
                    transaction.RollBack();
                }

                throw;
            }
        }

        public void RunRollbackOnly(Document doc, string name, Action<Document> action)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ArgumentNullException.ThrowIfNull(action);

            using Transaction transaction = new Transaction(doc, name);
            transaction.Start();

            try
            {
                action(doc);
            }
            finally
            {
                if (transaction.GetStatus() == TransactionStatus.Started)
                {
                    transaction.RollBack();
                }
            }
        }

        public void RunWithOptions(Document doc, string name, Action<Document> action, Action<FailureHandlingOptions> configureOptions)
        {
            ArgumentNullException.ThrowIfNull(action);
            ArgumentNullException.ThrowIfNull(configureOptions);
            RunInternal(doc, name, configureOptions, action);
        }

        public void RunWithWarningHandler(Document doc, string name, Action<Document> action, IFailuresPreprocessor? preprocessor = null)
        {
            ArgumentNullException.ThrowIfNull(action);
            RunInternal(doc, name, options => options.SetFailuresPreprocessor(preprocessor ?? new WarningSwallower()), action);
        }

        private static void RunInternal(Document doc, string name, Action<FailureHandlingOptions>? configureOptions, Action<Document> action)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ArgumentNullException.ThrowIfNull(action);

            using Transaction transaction = new Transaction(doc, name);
            transaction.Start();

            if (configureOptions != null)
            {
                FailureHandlingOptions options = transaction.GetFailureHandlingOptions();
                configureOptions(options);
                transaction.SetFailureHandlingOptions(options);
            }

            try
            {
                action(doc);
                transaction.Commit();
            }
            catch
            {
                if (transaction.GetStatus() == TransactionStatus.Started)
                {
                    transaction.RollBack();
                }

                throw;
            }
        }
    }
}
