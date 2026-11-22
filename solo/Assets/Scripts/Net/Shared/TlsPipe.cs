using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

namespace Wizard.Solo.Net
{
    /// <summary>암호화된 통로인지(비밀번호·Google 로그인은 이런 통로에서만 받는다).</summary>
    public interface ISecurePipe { bool IsSecure { get; } }

    /// <summary>새 연결을 내주는 쪽(평문 TCP 또는 TLS).</summary>
    public interface IPipeListener
    {
        int Port { get; }
        INetPipe Poll();
        void Stop();
    }

    /// <summary>
    /// TLS 1.2 통로(11-online.md 7절). 메시지 꼴은 TcpPipe와 같다([길이 int32 LE][몸]). 연결마다 읽는 스레드와 쓰는 스레드가 하나씩 —
    /// 받은 메시지는 큐에 쌓이고 TryReceive가 꺼낸다(게임 루프는 막히지 않는다). 클라이언트는 서버 인증서를 이름이 아니라
    /// SHA-256 지문(핀)으로 확인한다: 맥미니가 스스로 만든 인증서를 쓰고, 지문은 Online.json의 tls_pins에 둔다.
    /// </summary>
    public sealed class TlsPipe : INetPipe, ISecurePipe
    {
        readonly Socket _socket;
        SslStream _ssl;
        readonly ConcurrentQueue<byte[]> _inbox = new ConcurrentQueue<byte[]>();
        readonly BlockingCollection<byte[]> _outbox = new BlockingCollection<byte[]>(new ConcurrentQueue<byte[]>(), 1024);
        volatile bool _closed;
        volatile bool _ready;
        volatile bool _writerStarted;
        int _disposed;
        readonly ManualResetEventSlim _writerDone = new ManualResetEventSlim(false);

        public bool IsSecure => true;
        public bool Connected => !_closed;
        /// <summary>서버 쪽: 손잡기가 끝났다(리스너가 이것을 보고 내준다).</summary>
        internal bool Ready => _ready;

        TlsPipe(Socket s)
        {
            _socket = s;
            _socket.NoDelay = true;
        }

        /// <summary>인증서 DER의 SHA-256(소문자 16진).</summary>
        public static string Fingerprint(X509Certificate cert)
        {
            if (cert == null) return null;
            using (var sha = SHA256.Create())
            {
                var h = sha.ComputeHash(cert.GetRawCertData());
                var sb = new System.Text.StringBuilder(h.Length * 2);
                foreach (var b in h) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        static bool PinMatches(X509Certificate cert, IList<string> pins)
        {
            string f = Fingerprint(cert);
            if (f == null || pins == null) return false;
            foreach (var p in pins)
                if (p != null && string.Equals(p.Replace(":", string.Empty).Trim(), f, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>서버에 붙고 TLS 손잡기까지 마친다(블로킹, timeoutMs 안). 지문이 핀에 없으면 실패. 실패면 null.</summary>
        public static TlsPipe Connect(string host, int port, IList<string> pins, int timeoutMs = 3000)
        {
            if (pins == null || pins.Count == 0) return null;
            try
            {
                foreach (var a in Dns.GetHostAddresses(host))
                {
                    if (a.AddressFamily != AddressFamily.InterNetwork && a.AddressFamily != AddressFamily.InterNetworkV6) continue;
                    var s = new Socket(a.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                    var ar = s.BeginConnect(new IPEndPoint(a, port), null, null);
                    if (!ar.AsyncWaitHandle.WaitOne(timeoutMs) || !s.Connected) { s.Close(); continue; }
                    s.EndConnect(ar);
                    var pipe = new TlsPipe(s);
                    try
                    {
                        s.ReceiveTimeout = timeoutMs * 2;
                        s.SendTimeout = timeoutMs * 2;
                        pipe._ssl = new SslStream(new NetworkStream(s, true), false, (o, cert, chain, err) => PinMatches(cert, pins));
                        pipe._ssl.AuthenticateAsClient(host, null, SslProtocols.Tls12, false);
                        s.ReceiveTimeout = 0;
                        s.SendTimeout = 0;
                    }
                    catch (Exception) { pipe.Close(); continue; }
                    pipe.StartThreads();
                    return pipe;
                }
            }
            catch (Exception) { }
            return null;
        }

        /// <summary>서버 쪽: 받은 소켓에서 손잡기를 따로 스레드로 시작한다. 끝나면 Ready.</summary>
        internal static TlsPipe Accept(Socket s, X509Certificate2 cert, int handshakeTimeoutMs)
        {
            var pipe = new TlsPipe(s);
            var t = new Thread(() =>
            {
                try
                {
                    s.ReceiveTimeout = handshakeTimeoutMs;
                    s.SendTimeout = handshakeTimeoutMs;
                    pipe._ssl = new SslStream(new NetworkStream(s, true), false);
                    pipe._ssl.AuthenticateAsServer(cert, false, SslProtocols.Tls12, false);
                    s.ReceiveTimeout = 0;
                    s.SendTimeout = 10000;
                    pipe.StartThreads();
                    pipe._ready = true;
                }
                catch (Exception) { pipe.Close(); }
            }) { IsBackground = true, Name = "tls-accept" };
            t.Start();
            return pipe;
        }

        void StartThreads()
        {
            _writerStarted = true;
            new Thread(ReadLoop) { IsBackground = true, Name = "tls-read" }.Start();
            new Thread(WriteLoop) { IsBackground = true, Name = "tls-write" }.Start();
        }

        bool ReadExactly(byte[] buf, int n)
        {
            int got = 0;
            while (got < n)
            {
                int r = _ssl.Read(buf, got, n - got);
                if (r <= 0) return false;
                got += r;
            }
            return true;
        }

        void ReadLoop()
        {
            var head = new byte[4];
            try
            {
                while (!_closed)
                {
                    if (!ReadExactly(head, 4)) break;
                    int n = head[0] | (head[1] << 8) | (head[2] << 16) | (head[3] << 24);
                    if (n < 0 || n > TcpPipe.MaxMessageBytes) break;
                    var msg = new byte[n];
                    if (n > 0 && !ReadExactly(msg, n)) break;
                    _inbox.Enqueue(msg);
                }
            }
            catch (Exception) { }
            Close();
        }

        void WriteLoop()
        {
            try
            {
                // 닫힌 뒤에도(CompleteAdding) 큐에 남은 것은 다 보낸다 — "실패 답을 보내고 끊기"가 답을 잃지 않게.
                foreach (var frame in _outbox.GetConsumingEnumerable())
                {
                    _ssl.Write(frame, 0, frame.Length);
                    // 큐에 더 없으면 그때 내보낸다(여러 메시지를 한 번에).
                    if (_outbox.Count == 0) _ssl.Flush();
                }
                _ssl.Flush();
            }
            catch (Exception) { }
            _writerDone.Set();
            Close();
            DisposeAll();
        }

        public void Send(byte[] message)
        {
            if (_closed || message == null) return;
            var frame = new byte[4 + message.Length];
            int n = message.Length;
            frame[0] = (byte)n; frame[1] = (byte)(n >> 8); frame[2] = (byte)(n >> 16); frame[3] = (byte)(n >> 24);
            Buffer.BlockCopy(message, 0, frame, 4, n);
            // 상대가 너무 오래 안 받으면(큐 1024개가 참) 끊는다.
            try { if (!_outbox.TryAdd(frame)) Close(); } catch (Exception) { }
        }

        public bool TryReceive(out byte[] message) => _inbox.TryDequeue(out message);

        /// <summary>더 보내지 않고 닫는다. 이미 큐에 넣은 메시지는 쓰는 스레드가 마저 보낸 뒤 소켓을 닫는다(최대 2초).</summary>
        public void Close()
        {
            if (_closed) return;
            _closed = true;
            try { _outbox.CompleteAdding(); } catch (Exception) { }
            if (!_writerStarted) { DisposeAll(); return; }
            ThreadPool.QueueUserWorkItem(_ => { _writerDone.Wait(2000); DisposeAll(); });
        }

        void DisposeAll()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            try { _ssl?.Dispose(); } catch (Exception) { }
            try { _socket.Shutdown(SocketShutdown.Both); } catch (Exception) { }
            try { _socket.Close(); } catch (Exception) { }
        }
    }

    /// <summary>TLS 받는 쪽. Poll()은 손잡기가 끝난 통로만 내준다(블로킹 없음).</summary>
    public sealed class TlsPipeListener : IPipeListener
    {
        readonly TcpListener _listener;
        readonly X509Certificate2 _cert;
        readonly List<TlsPipe> _handshaking = new List<TlsPipe>();

        public int Port { get; }

        public TlsPipeListener(int port, X509Certificate2 cert, bool bindAll = true)
        {
            _cert = cert ?? throw new ArgumentNullException(nameof(cert));
            _listener = new TcpListener(bindAll ? IPAddress.Any : IPAddress.Loopback, port);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        }

        public static X509Certificate2 LoadCertificate(string pfxPath, string password)
            => new X509Certificate2(System.IO.File.ReadAllBytes(pfxPath), password ?? string.Empty, X509KeyStorageFlags.Exportable);

        public INetPipe Poll()
        {
            try
            {
                while (_listener.Pending()) _handshaking.Add(TlsPipe.Accept(_listener.AcceptSocket(), _cert, 10000));
            }
            catch (Exception) { }
            for (int i = 0; i < _handshaking.Count; i++)
            {
                var p = _handshaking[i];
                if (!p.Connected) { _handshaking.RemoveAt(i--); continue; }
                if (p.Ready) { _handshaking.RemoveAt(i); return p; }
            }
            return null;
        }

        public void Stop()
        {
            try { _listener.Stop(); } catch (Exception) { }
            foreach (var p in _handshaking) p.Close();
            _handshaking.Clear();
        }
    }
}
