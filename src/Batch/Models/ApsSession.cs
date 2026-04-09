namespace LECG.Batch.Models
{
    public class ApsSession
    {
        public string AccessToken { get; set; } = "";
        public string RefreshToken { get; set; } = "";
        public DateTime ExpiresAt { get; set; }
        public string Scope { get; set; } = "";
        public string UserId { get; set; } = "";

        public static ApsSession FromTokenResponse(string accessToken, string refreshToken, int expiresInSeconds, string scope, string userId)
        {
            return new ApsSession
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddSeconds(expiresInSeconds),
                Scope = scope,
                UserId = userId
            };
        }

        public bool IsExpired(int bufferMinutes = 5) =>
            DateTime.UtcNow >= ExpiresAt.AddMinutes(-bufferMinutes);
    }
}
