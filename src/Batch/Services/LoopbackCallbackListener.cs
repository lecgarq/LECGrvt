using System.Net;
using System.Text;

namespace LECG.Batch.Services
{
    internal sealed class LoopbackCallbackListener : IDisposable
    {
        private readonly HttpListener _listener;
        private bool _disposed;

        public LoopbackCallbackListener(string redirectUri)
        {
            _listener = new HttpListener();
            string prefix = redirectUri.EndsWith('/') ? redirectUri : redirectUri + "/";
            _listener.Prefixes.Add(prefix);
            _listener.Start();
        }

        public async Task<string> WaitForCodeAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);

            HttpListenerContext context = await Task.Run(() => _listener.GetContext(), cts.Token);

            string responseHtml = "<html><body><h2>Sign-in complete. You can close this tab.</h2></body></html>";
            byte[] buffer = Encoding.UTF8.GetBytes(responseHtml);
            context.Response.ContentLength64 = buffer.Length;
            await context.Response.OutputStream.WriteAsync(buffer, cts.Token);
            context.Response.Close();

            string? code = ParseQueryString(context.Request.Url!.Query, "code");
            if (string.IsNullOrWhiteSpace(code))
                throw new InvalidOperationException("OAuth callback did not contain a code parameter.");

            return code;
        }

        private static string? ParseQueryString(string query, string key)
        {
            foreach (string part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] kv = part.Split('=', 2);
                if (kv.Length == 2 && string.Equals(kv[0], key, StringComparison.OrdinalIgnoreCase))
                    return Uri.UnescapeDataString(kv[1]);
            }
            return null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { _listener.Stop(); } catch { }
            try { _listener.Close(); } catch { }
        }
    }
}
