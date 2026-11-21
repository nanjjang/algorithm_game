using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace Wizard.Solo.Net
{
    /// <summary>
    /// TCP 통로(11-online.md 3단계). 메시지 = [길이 int32 LE][몸]. 블로킹 없는 소켓: Send는 바로 쓰고(작은 메시지),
    /// TryReceive는 와 있는 바이트만 읽는다. 순서·전달은 TCP가 지킨다. 연결이 끊기면 Connected = false.
    /// UDP(Unity Transport)로 바꾸는 것은 지연을 재 본 뒤에 한다 — INetPipe만 바꾸면 된다.
    /// </summary>
    public sealed class TcpPipe : INetPipe
    {
        public const int MaxMessageBytes = 4 * 1024 * 1024;

        readonly Socket _socket;
        readonly Queue<byte[]> _inbox = new Queue<byte[]>();
        byte[] _buf = new byte[64 * 1024];
        int _len;
        bool _closed;

        public TcpPipe(Socket connected)
        {
            _socket = connected ?? throw new ArgumentNullException(nameof(connected));
            _socket.NoDelay = true;
            _socket.Blocking = false;
        }

        /// <summary>서버에 붙는다(블로킹 연결, timeoutMs 안에). 실패하면 null.</summary>
        public static TcpPipe Connect(string host, int port, int timeoutMs = 3000)
        {
            try
            {
                var addrs = Dns.GetHostAddresses(host);
                foreach (var a in addrs)
                {
                    if (a.AddressFamily != AddressFamily.InterNetwork && a.AddressFamily != AddressFamily.InterNetworkV6) continue;
                    var s = new Socket(a.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                    var ar = s.BeginConnect(new IPEndPoint(a, port), null, null);
                    if (ar.AsyncWaitHandle.WaitOne(timeoutMs) && s.Connected)
                    {
                        s.EndConnect(ar);
                        return new TcpPipe(s);
                    }
                    s.Close();
                }
            }
            catch (Exception) { }
            return null;
        }

        public bool Connected => !_closed;

        public void Send(byte[] message)
        {
            if (_closed || message == null) return;
            var frame = new byte[4 + message.Length];
            int n = message.Length;
            frame[0] = (byte)n; frame[1] = (byte)(n >> 8); frame[2] = (byte)(n >> 16); frame[3] = (byte)(n >> 24);
            Buffer.BlockCopy(message, 0, frame, 4, n);
            int off = 0;
            try
            {
                // 보내기 버퍼가 차면 잠깐 기다린다(스냅숏 몇 KB라 거의 없다). 오래 막히면 끊는다.
                var deadline = DateTime.UtcNow.AddSeconds(2);
                while (off < frame.Length)
                {
                    int sent = _socket.Send(frame, off, frame.Length - off, SocketFlags.None, out SocketError err);
                    if (err == SocketError.WouldBlock) { if (DateTime.UtcNow > deadline) { Close(); return; } _socket.Poll(1000, SelectMode.SelectWrite); continue; }
                    if (err != SocketError.Success) { Close(); return; }
                    off += sent;
                }
            }
            catch (Exception) { Close(); }
        }

        public bool TryReceive(out byte[] message)
        {
            if (_inbox.Count == 0) Fill();
            if (_inbox.Count > 0) { message = _inbox.Dequeue(); return true; }
            message = null;
            return false;
        }

        void Fill()
        {
            if (_closed) return;
            try
            {
                while (true)
                {
                    if (_len == _buf.Length) Array.Resize(ref _buf, _buf.Length * 2);
                    int got = _socket.Receive(_buf, _len, _buf.Length - _len, SocketFlags.None, out SocketError err);
                    if (err == SocketError.WouldBlock) break;
                    if (err != SocketError.Success || got == 0) { Close(); break; }
                    _len += got;
                }
            }
            catch (Exception) { Close(); }

            int pos = 0;
            while (_len - pos >= 4)
            {
                int n = _buf[pos] | (_buf[pos + 1] << 8) | (_buf[pos + 2] << 16) | (_buf[pos + 3] << 24);
                if (n < 0 || n > MaxMessageBytes) { Close(); return; }
                if (_len - pos - 4 < n) break;
                var msg = new byte[n];
                Buffer.BlockCopy(_buf, pos + 4, msg, 0, n);
                _inbox.Enqueue(msg);
                pos += 4 + n;
            }
            if (pos > 0)
            {
                Buffer.BlockCopy(_buf, pos, _buf, 0, _len - pos);
                _len -= pos;
            }
        }

        public void Close()
        {
            if (_closed) return;
            _closed = true;
            try { _socket.Shutdown(SocketShutdown.Both); } catch (Exception) { }
            try { _socket.Close(); } catch (Exception) { }
        }
    }

    /// <summary>TCP 받는 쪽. Poll()이 새 연결을 TcpPipe로 돌려준다(블로킹 없음).</summary>
    public sealed class TcpPipeListener : IPipeListener
    {
        readonly TcpListener _listener;

        public int Port { get; }

        /// <summary>port 0 = 빈 포트를 고른다(테스트). bindAll = 모든 주소(서버), 아니면 127.0.0.1만.</summary>
        public TcpPipeListener(int port, bool bindAll = true)
        {
            _listener = new TcpListener(bindAll ? IPAddress.Any : IPAddress.Loopback, port);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        }

        INetPipe IPipeListener.Poll() => Poll();

        public TcpPipe Poll()
        {
            try
            {
                if (!_listener.Pending()) return null;
                return new TcpPipe(_listener.AcceptSocket());
            }
            catch (Exception) { return null; }
        }

        public void Stop()
        {
            try { _listener.Stop(); } catch (Exception) { }
        }
    }
}
