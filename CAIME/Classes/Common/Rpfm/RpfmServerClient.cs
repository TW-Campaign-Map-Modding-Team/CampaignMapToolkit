using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CAIME.Rpfm
{
    /// <summary>
    /// One session with <c>rpfm_server.exe</c>, the headless backend RPFM runs its UI on. Owns the
    /// transport only: starting the server when none is listening, JSON-RPC 2.0 requests and their
    /// responses, and waiting for the methods that run as jobs. It knows nothing about which methods
    /// exist; <see cref="RpfmService"/> does. The protocol and methods are documented in RPFM's
    /// docs/server.
    ///
    /// Every connection is its own server session with its own open packs, so CAIME neither sees nor
    /// disturbs anything the user has open in the RPFM UI, even when both share one server process.
    /// </summary>
    public sealed class RpfmServerClient : IDisposable
    {
        public const string ServerExecutableName = "rpfm_server.exe";

        // The address rpfm_server listens on, and the one the RPFM UI itself connects to.
        private static readonly Uri ServerUri = new Uri("ws://127.0.0.1:45127/ws");

        // Plain HTTP endpoints next to the WebSocket: the server's version and process id, and its sessions.
        private static readonly Uri VersionUri  = new Uri("http://127.0.0.1:45127/version");
        private static readonly Uri SessionsUri = new Uri("http://127.0.0.1:45127/sessions");

        private static readonly TimeSpan ConnectAttemptTimeout = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan ServerStartupTimeout  = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan ConnectRetryDelay     = TimeSpan.FromMilliseconds(250);
        private static readonly TimeSpan DisconnectTimeout     = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan ServerExitTimeout     = TimeSpan.FromSeconds(10);

        // How long one job.wait may block; a longer job is waited for again until its own timeout.
        private const int JobWaitSeconds = 30;

        private const int ReceiveChunkSize = 64 * 1024;

        private static readonly JsonSerializer Serializer = new JsonSerializer();

        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };

        private readonly ClientWebSocket _socket;
        private readonly int? _serverProcessId;
        private long _lastRequestId;

        private RpfmServerClient(ClientWebSocket socket)
        {
            _socket = socket;
            _serverProcessId = TryGetServerProcessId();
        }

        /// <summary>
        /// Opens a new session on the running rpfm_server, first starting the one in
        /// <paramref name="rpfmFolder"/> when none is listening. The server shuts itself down once its
        /// last session ends, so a server started here does not outlive CAIME's use of it.
        /// </summary>
        public static RpfmServerClient Connect(string rpfmFolder)
        {
            var socket = TryConnect() ?? StartServerAndConnect(rpfmFolder);
            var client = new RpfmServerClient(socket);

            try
            {
                client.ExpectSessionConnected();
                return client;
            }
            catch
            {
                client.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Calls <paramref name="method"/> with <paramref name="parameters"/> (null for none) and returns
        /// its result deserialized as <typeparamref name="T"/>. The result is read as a stream, so a
        /// <typeparamref name="T"/> that declares only the members it needs never materializes the rest
        /// of a large one. Throws <see cref="RpfmException"/>, naming <paramref name="operation"/>, when
        /// the server answers with an error, times out, or drops the connection.
        /// </summary>
        public T Call<T>(string operation, string method, object parameters, TimeSpan timeout)
        {
            using (var cancellation = new CancellationTokenSource(timeout))
            {
                return Call<T>(operation, method, parameters, cancellation);
            }
        }

        /// <summary><see cref="Call{T}"/> for a method whose result CAIME has no use for.</summary>
        public void Call(string operation, string method, object parameters, TimeSpan timeout)
        {
            Call<JToken>(operation, method, parameters, timeout);
        }

        /// <summary>
        /// Calls <paramref name="method"/>, one of the methods RPFM runs as a job, and waits for the job
        /// to end. Returns the job's result; throws <see cref="RpfmException"/> when it fails, is
        /// cancelled, or has not ended within <paramref name="timeout"/>.
        /// </summary>
        public T CallJob<T>(string operation, string method, object parameters, TimeSpan timeout)
        {
            using (var cancellation = new CancellationTokenSource(timeout))
            {
                var job = Call<JobStarted>(operation, method, parameters, cancellation).Job;

                while (true)
                {
                    var status = Call<JObject>(operation, "job.wait", new { job, timeout_secs = JobWaitSeconds }, cancellation);

                    switch ((string)status["state"])
                    {
                        case "finished":
                            var result = status["result"];
                            return result == null || result.Type == JTokenType.Null ? default(T) : result.ToObject<T>(Serializer);
                        case "failed":
                            throw new RpfmException(operation, (string)status["error"]?["message"] ?? "the RPFM server reported a failure.");
                        case "cancelled":
                            throw new RpfmException(operation, "the RPFM server cancelled it.");
                    }
                }
            }
        }

        /// <summary>
        /// Ends the session. Telling the server first makes it release the session's packs at once
        /// rather than holding them through its five-minute reconnect grace period. When this was the
        /// server's last session, waits for the server to exit, so the next <see cref="Connect"/>
        /// starts a new one rather than joining one that is shutting down and losing it mid-request.
        /// </summary>
        public void Dispose()
        {
            try
            {
                if (_socket.State == WebSocketState.Open)
                {
                    using (var cancellation = new CancellationTokenSource(DisconnectTimeout))
                    {
                        // The server may close the socket rather than answer; either way the session is gone.
                        Task.Run(async () =>
                        {
                            await SendRequestAsync(++_lastRequestId, "session.disconnect", null, cancellation.Token).ConfigureAwait(false);
                            await DrainUntilClosedAsync(cancellation.Token).ConfigureAwait(false);
                        }).GetAwaiter().GetResult();
                    }

                    WaitForServerExitIfIdle();
                }
            }
            catch (Exception ex)
            {
                LoggerViewModel.Log($"RPFM - could not end the server session cleanly: {ex.Message}", LogLevel.Warning);
            }
            finally
            {
                _socket.Dispose();
            }
        }

        private T Call<T>(string operation, string method, object parameters, CancellationTokenSource cancellation)
        {
            var id = ++_lastRequestId;

            try
            {
                // Off the calling thread so no await inside can need a UI thread that is blocked here.
                using (var response = Task.Run(() => ExchangeAsync(id, method, parameters, cancellation.Token)).GetAwaiter().GetResult())
                {
                    return ReadResult<T>(response, operation);
                }
            }
            // A cancelled socket operation can surface as either exception, so the token decides which it was.
            catch (Exception ex) when (ex is OperationCanceledException || ex is WebSocketException)
            {
                var reason = cancellation.IsCancellationRequested
                    ? "the RPFM server did not respond in time."
                    : $"the connection to the RPFM server was lost. {ex.Message}";
                throw new RpfmException(operation, reason);
            }
            catch (JsonException ex)
            {
                throw new RpfmException(operation, $"the RPFM server sent a response CAIME could not read. {ex.Message}");
            }
        }

        private static ClientWebSocket TryConnect()
        {
            var socket = new ClientWebSocket();

            try
            {
                using (var cancellation = new CancellationTokenSource(ConnectAttemptTimeout))
                {
                    Task.Run(() => socket.ConnectAsync(ServerUri, cancellation.Token)).GetAwaiter().GetResult();
                }

                return socket;
            }
            catch (Exception ex) when (ex is WebSocketException || ex is OperationCanceledException)
            {
                socket.Dispose();
                return null;
            }
        }

        private static ClientWebSocket StartServerAndConnect(string rpfmFolder)
        {
            var serverPath = Path.Combine(rpfmFolder ?? string.Empty, ServerExecutableName);
            if (!File.Exists(serverPath))
            {
                throw new RpfmException("start the RPFM server", $"{ServerExecutableName} was not found in '{rpfmFolder}'.");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName         = serverPath,
                WorkingDirectory = rpfmFolder,
                UseShellExecute  = false,
                CreateNoWindow   = true,
            };

            using (var server = Process.Start(startInfo))
            {
                var startup = Stopwatch.StartNew();

                while (startup.Elapsed < ServerStartupTimeout)
                {
                    if (server.HasExited)
                    {
                        // Usually a second server losing the race for the port to one started meanwhile
                        // (by the RPFM UI, say) - which is as good as this one starting, so keep trying.
                        if (TryConnect() is ClientWebSocket raced)
                        {
                            return raced;
                        }

                        throw new RpfmException("start the RPFM server", $"{ServerExecutableName} exited with code {server.ExitCode}.");
                    }

                    if (TryConnect() is ClientWebSocket socket)
                    {
                        return socket;
                    }

                    Thread.Sleep(ConnectRetryDelay);
                }

                try { server.Kill(); } catch { /* best effort */ }
                throw new RpfmException("start the RPFM server", $"{ServerExecutableName} did not start listening within {(int)ServerStartupTimeout.TotalSeconds} seconds.");
            }
        }

        // The server greets every new connection with a session.connected notification. Waiting for
        // it confirms that whatever answered on the port really is an RPFM server.
        private void ExpectSessionConnected()
        {
            const string Operation = "connect to the RPFM server";

            try
            {
                using (var cancellation = new CancellationTokenSource(ConnectAttemptTimeout))
                using (var greeting = Task.Run(() => ReceiveMessageAsync(cancellation.Token)).GetAwaiter().GetResult())
                using (var reader = OpenReader(greeting))
                {
                    if (!MoveToMember(reader, "method") || (string)reader.Value != "session.connected")
                    {
                        throw new RpfmException(Operation,
                            "whatever answered did not greet as an RPFM server. CAIME needs RPFM 5.3 or later.");
                    }
                }
            }
            catch (Exception ex) when (ex is WebSocketException || ex is OperationCanceledException || ex is JsonException)
            {
                throw new RpfmException(Operation, $"no session greeting was received. {ex.Message}");
            }
        }

        private static int? TryGetServerProcessId()
        {
            try
            {
                var version = JObject.Parse(Task.Run(() => Http.GetStringAsync(VersionUri)).GetAwaiter().GetResult());
                return (int?)version["pid"];
            }
            // Only an optimisation for Dispose, so a server without the endpoint is no failure.
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException || ex is JsonException)
            {
                return null;
            }
        }

        // The server exits as soon as its last session ends, but keeps accepting connections for a
        // moment while it shuts down.
        private void WaitForServerExitIfIdle()
        {
            if (_serverProcessId == null)
            {
                return;
            }

            try
            {
                if (JArray.Parse(Task.Run(() => Http.GetStringAsync(SessionsUri)).GetAwaiter().GetResult()).Count > 0)
                {
                    return;
                }
            }
            // Already stopped answering: it is on its way out.
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException || ex is JsonException)
            {
            }

            try
            {
                using (var server = Process.GetProcessById(_serverProcessId.Value))
                {
                    server.WaitForExit((int)ServerExitTimeout.TotalMilliseconds);
                }
            }
            // Already exited.
            catch (ArgumentException)
            {
            }
        }

        private async Task DrainUntilClosedAsync(CancellationToken cancellation)
        {
            try
            {
                while (true)
                {
                    (await ReceiveMessageAsync(cancellation).ConfigureAwait(false)).Dispose();
                }
            }
            // How the server's close arrives; it is the answer awaited here.
            catch (WebSocketException)
            {
            }
        }

        private async Task<MemoryStream> ExchangeAsync(long id, string method, object parameters, CancellationToken cancellation)
        {
            await SendRequestAsync(id, method, parameters, cancellation).ConfigureAwait(false);

            while (true)
            {
                var message = await ReceiveMessageAsync(cancellation).ConfigureAwait(false);

                // Anything else is a notification (no id, e.g. job.updated) or another request's response.
                if (ReadId(message) == id)
                {
                    return message;
                }

                message.Dispose();
            }
        }

        private Task SendRequestAsync(long id, string method, object parameters, CancellationToken cancellation)
        {
            var request = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"]      = id,
                ["method"]  = method,
            };

            if (parameters != null)
            {
                request["params"] = JToken.FromObject(parameters);
            }

            var bytes = Encoding.UTF8.GetBytes(request.ToString(Formatting.None));
            return _socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellation);
        }

        private async Task<MemoryStream> ReceiveMessageAsync(CancellationToken cancellation)
        {
            var chunk = new ArraySegment<byte>(new byte[ReceiveChunkSize]);
            var message = new MemoryStream();

            try
            {
                WebSocketReceiveResult result;
                do
                {
                    result = await _socket.ReceiveAsync(chunk, cancellation).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        throw new WebSocketException("The RPFM server closed the connection.");
                    }

                    message.Write(chunk.Array, 0, result.Count);
                }
                while (!result.EndOfMessage);

                return message;
            }
            catch
            {
                message.Dispose();
                throw;
            }
        }

        // A notification has no id, which no request of this session uses.
        private static long? ReadId(MemoryStream message)
        {
            using (var reader = OpenReader(message))
            {
                return MoveToMember(reader, "id") && reader.TokenType == JsonToken.Integer ? Convert.ToInt64(reader.Value) : (long?)null;
            }
        }

        // A response holds either a result or an error with a message.
        private static T ReadResult<T>(MemoryStream message, string operation)
        {
            using (var reader = OpenReader(message))
            {
                while (reader.Read())
                {
                    if (reader.Depth != 1 || reader.TokenType != JsonToken.PropertyName)
                    {
                        continue;
                    }

                    var member = (string)reader.Value;
                    reader.Read();

                    if (member == "result")
                    {
                        return Serializer.Deserialize<T>(reader);
                    }

                    if (member == "error")
                    {
                        var error = JObject.Load(reader);
                        throw new RpfmException(operation, (string)error["message"] ?? error.ToString(Formatting.None));
                    }

                    reader.Skip();
                }
            }

            throw new RpfmException(operation, "the RPFM server sent a response with neither a result nor an error.");
        }

        private static JsonTextReader OpenReader(MemoryStream message)
        {
            message.Position = 0;
            return new JsonTextReader(new StreamReader(message, Encoding.UTF8, false, ReceiveChunkSize, leaveOpen: true))
            {
                // Pack paths are data, not dates; leave every string exactly as the server sent it.
                DateParseHandling = DateParseHandling.None,
            };
        }

        // Leaves the reader on the value of the message's top-level member called name, skipping
        // every other member's value unread.
        private static bool MoveToMember(JsonReader reader, string name)
        {
            while (reader.Read())
            {
                if (reader.Depth != 1 || reader.TokenType != JsonToken.PropertyName)
                {
                    continue;
                }

                if ((string)reader.Value == name)
                {
                    return reader.Read();
                }

                reader.Read();
                reader.Skip();
            }

            return false;
        }

        private sealed class JobStarted
        {
            [JsonProperty("job")]
            public long Job { get; set; }
        }
    }

    /// <summary>Raised when an RPFM server request fails; the message names the operation and why.</summary>
    public sealed class RpfmException : Exception
    {
        public RpfmException(string operation, string reason)
            : base($"RPFM failed to {operation}: {reason}")
        {
        }
    }
}
