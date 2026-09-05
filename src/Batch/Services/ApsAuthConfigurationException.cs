namespace LECG.Batch.Services
{
    public sealed class ApsAuthConfigurationException : InvalidOperationException
    {
        public ApsAuthConfigurationException(string message)
            : base(message)
        {
        }
    }
}
