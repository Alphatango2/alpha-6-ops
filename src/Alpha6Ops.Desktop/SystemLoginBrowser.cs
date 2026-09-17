using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Duende.IdentityModel.OidcClient.Browser;

namespace Alpha6Ops.Desktop;

// Reserve the registered loopback port for the full login, without a release/rebind race.
internal sealed class SystemLoginBrowser : IBrowser, IDisposable
{
    private readonly TcpListener listener;
    internal string RedirectUri { get; }
    internal SystemLoginBrowser(int port)
    {
        listener = new TcpListener(IPAddress.Loopback, port);
        listener.Server.ExclusiveAddressUse = true;
        listener.Start();
        RedirectUri = $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/callback/";
    }

    public async Task<BrowserResult> InvokeAsync(BrowserOptions options, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(3));
        try
        {
            Process.Start(new ProcessStartInfo(options.StartUrl) { UseShellExecute = true });
            while (true)
            {
                using var connection = await listener.AcceptTcpClientAsync(timeout.Token);
                using var stream = connection.GetStream();
                using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
                requestTimeout.CancelAfter(TimeSpan.FromSeconds(3));
                var bytes = new byte[16384]; var count = 0;
                while (count < bytes.Length)
                {
                    var read = await stream.ReadAsync(bytes.AsMemory(count, 1), requestTimeout.Token);
                    if (read == 0) break;
                    count += read;
                    if (count >= 4 && bytes[count - 4] == 13 && bytes[count - 3] == 10 && bytes[count - 2] == 13 && bytes[count - 1] == 10) break;
                }
                var headers = Encoding.ASCII.GetString(bytes, 0, count);
                var line = headers.Split("\r\n")[0].Split(' ');
                var host = new Uri(RedirectUri).Authority;
                if (line.Length != 3 || line[0] != "GET" || !line[1].StartsWith("/callback/?", StringComparison.Ordinal)
                    || !headers.Contains("\r\nHost: " + host + "\r\n", StringComparison.OrdinalIgnoreCase))
                {
                    await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 400 Bad Request\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"), timeout.Token);
                    continue;
                }
                const string body = "Sign-in response received. You can return to Alpha 6 OPS.";
                var reply = $"HTTP/1.1 200 OK\r\nContent-Type: text/plain; charset=utf-8\r\nContent-Length: {body.Length}\r\nCache-Control: no-store\r\nReferrer-Policy: no-referrer\r\nConnection: close\r\n\r\n{body}";
                await stream.WriteAsync(Encoding.UTF8.GetBytes(reply), timeout.Token);
                return new BrowserResult { ResultType = BrowserResultType.Success, Response = RedirectUri.TrimEnd('/') + "/" + line[1]["/callback/".Length..] };
            }
        }
        catch (OperationCanceledException) { return new BrowserResult { ResultType = BrowserResultType.UserCancel }; }
        finally { listener.Stop(); }
    }
    public void Dispose() => listener.Stop();
}
