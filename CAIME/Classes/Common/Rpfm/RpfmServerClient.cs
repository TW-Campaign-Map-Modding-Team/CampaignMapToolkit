using System;
using System.Diagnostics;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CAIME.Rpfm
{
    /// <summary>
    /// One session with <c>rpfm_server.exe</c>, the headless backend RPFM 5 runs its UI on. Owns the
    /// transport only: starting the server when none is listening, the <c>{ "id", "data" }</c>
    /// message envelope, and matching each response to its command. It knows nothing about which
    /// commands exist; <see cref="RpfmService"/> does.
    ///
    /// Every connection is its own server session with its own open packs, so CAIME neither sees nor
    /// disturbs anything the user has open in the RPFM UI, even when both share one server process.
    /// The protocol is documented at https://frodo45127.github.io/rpfm/manual/server/ws-protocol.html.
    /// </summary>
    public sealed class RpfmServerClient : IDisposable
    {
        public const string ServerExecutableName = "rpfm_server.exe";

        // The address rpfm_server listens on, and the one the RPFM UI itself connects to.
        private static readonly Uri ServerUri = new Uri("ws://127.0.0.1:45127/ws");

        private static readonly TimeSpan ConnectAttemptTimeout = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan ServerStartupTimeout  = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan ConnectRetryDelay     = TimeSpan.FromMilliseconds(250);
        private static readonly TimeSpan DisconnectTimeout     = TimeSpan.FromSeconds(5);

        private const int ReceiveChunkSize = 64 * 1024;

        private static readonly JsonSerializer Serializer = new JsonSerializer();

        private readonly ClientWebSocket _socket;
        private long _lastCommandId;

        private RpfmServerClient(ClientWebSocket socket)
        {
            _socket = socket;
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
        /// Sends <paramref name="command"/> and returns the payload of the
        /// <paramref name="expectedResponse"/> variant the server answers with, deserialized as
        /// <typeparamref name="T"/>. The payload is read as a stream, so a <typeparamref name="T"/>
        /// that declares only the members it needs never materializes the rest of a large response.
        /// Throws <see cref="RpfmException"/>, naming <paramref name="operation"/>, when the server
        /// reports an error, answers with anything else, times out, or drops the connection.
        /// </summary>
        /// <param name="command">
        /// Serialized the way the server's serde enums expect: a bare string for a command without
        /// parameters, otherwise an object whose only property is the command name.
        /// </param>
        public T Send<T>(string operation, object command, string expectedResponse, TimeSpan timeout)
        {
            var id = ++_lastCommandId;

            using (var cancellation = new CancellationTokenSource(timeout))
            {
                try
                {
                    // Off the calling thread so no await inside can need a UI thread that is blocked here.
                    using (var response = Task.Run(() => ExchangeAsync(id, command, cancellation.Token)).GetAwaiter().GetResult())
                    {
                        return ReadPayload<T>(response, operation, expectedResponse);
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
        }

        /// <summary>
        /// <see cref="Send{T}"/> for a command whose answer carries nothing CAIME needs, such as the
        /// bare <c>"Success"</c> many commands reply with.
        /// </summary>
        public void Send(string operation, object command, string expectedResponse, TimeSpan timeout)
        {
            Send<JToken>(operation, command, expectedResponse, timeout);
        }

        /// <summary>
        /// Ends the session. Telling the server first makes it release the session's packs at once
        /// rather than holding them through its five-minute reconnect grace period.
        /// </summary>
        public void Dispose()
        {
            try
            {
                if (_socket.State == WebSocketState.Open)
                {
                    using (var cancellation = new CancellationTokenSource(DisconnectTimeout))
                    {
                        // The server closes the socket in answer rather than replying, so nothing is awaited back.
                        Task.Run(() => SendEnvelopeAsync(++_lastCommandId, "ClientDisconnecting", cancellation.Token))
                            .GetAwaiter().GetResult();
                    }
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

        // The server greets every new connection with an unsolicited SessionConnected message. Waiting
        // for it confirms that whatever answered on the port really is an RPFM server.
        private void ExpectSessionConnected()
        {
            const string Operation = "connect to the RPFM server";

            try
            {
                using (var cancellation = new CancellationTokenSource(ConnectAttemptTimeout))
                using (var greeting = Task.Run(() => ReceiveMessageAsync(cancellation.Token)).GetAwaiter().GetResult())
                {
                    ReadPayload<long>(greeting, Operation, "SessionConnected");
                }
            }
            catch (Exception ex) when (ex is WebSocketException || ex is OperationCanceledException || ex is JsonException)
            {
                throw new RpfmException(Operation, $"no session greeting was received. {ex.Message}");
            }
        }

        private async Task<MemoryStream> ExchangeAsync(long id, object command, CancellationToken cancellation)
        {
            await SendEnvelopeAsync(id, command, cancellation).ConfigureAwait(false);

            while (true)
            {
                var message = await ReceiveMessageAsync(cancellation).ConfigureAwait(false);

                // Anything else is an unsolicited broadcast (id 0, e.g. SettingsChanged) CAIME has no use for.
                if (ReadId(message) == id)
                {
                    return message;
                }

                message.Dispose();
            }
        }

        private Task SendEnvelopeAsync(long id, object command, CancellationToken cancellation)
        {
            var envelope = new JObject
            {
                ["id"]   = id,
                ["data"] = JToken.FromObject(command),
            };

            var bytes = Encoding.UTF8.GetBytes(envelope.ToString(Formatting.None));
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

        private static long ReadId(MemoryStream message)
        {
            using (var reader = OpenReader(message))
            {
                if (!MoveToEnvelopeMember(reader, "id"))
                {
                    throw new JsonSerializationException("A message from the RPFM server has no id.");
                }

                return Convert.ToInt64(reader.Value);
            }
        }

        // The payload sits at data.<variant> for a variant with content, or is data itself, a bare
        // string, for one without (such as "Success").
        private static T ReadPayload<T>(MemoryStream message, string operation, string expectedResponse)
        {
            using (var reader = OpenReader(message))
            {
                if (!MoveToEnvelopeMember(reader, "data"))
                {
                    throw new RpfmException(operation, "the RPFM server sent a message with no data.");
                }

                if (reader.TokenType == JsonToken.String && (string)reader.Value == expectedResponse)
                {
                    return default(T);
                }

                if (reader.TokenType != JsonToken.StartObject || !reader.Read() || reader.TokenType != JsonToken.PropertyName)
                {
                    throw new RpfmException(operation, $"the RPFM server sent an unexpected response: {reader.TokenType} {reader.Value}");
                }

                var variant = (string)reader.Value;
                reader.Read();

                if (variant == "Error")
                {
                    throw new RpfmException(operation, reader.Value as string ?? Abbreviate(reader));
                }

                if (variant != expectedResponse)
                {
                    throw new RpfmException(operation, $"the RPFM server sent an unexpected response: {variant}");
                }

                return Serializer.Deserialize<T>(reader);
            }
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

        // Leaves the reader on the value of the envelope's top-level member called name, skipping
        // every other member's value unread.
        private static bool MoveToEnvelopeMember(JsonReader reader, string name)
        {
            while (reader.Read())
            {
                if (reader.TokenType != JsonToken.PropertyName)
                {
                    continue;
                }

                if ((string)reader.Value == name)
                {
                    return reader.Read();
                }

                reader.Skip();
            }

            return false;
        }

        private static string Abbreviate(JsonReader reader)
        {
            const int MaxLength = 300;
            var text = JToken.ReadFrom(reader).ToString(Formatting.None);
            return text.Length <= MaxLength ? text : text.Substring(0, MaxLength) + "...";
        }
    }

    /// <summary>Raised when an RPFM server command fails; the message names the operation and why.</summary>
    public sealed class RpfmException : Exception
    {
        public RpfmException(string operation, string reason)
            : base($"RPFM failed to {operation}: {reason}")
        {
        }
    }
}
