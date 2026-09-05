namespace LECG.Batch.Configuration
{
    public class ApsAuthSettings
    {
        public string ClientId { get; set; } = "";
        public string ClientSecret { get; set; } = "";
        public string RedirectUri { get; set; } = "";
        public string[] Scopes { get; set; } = [];
    }
}
