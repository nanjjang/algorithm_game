using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Wizard.Solo.Data;

namespace Wizard.Solo.Net
{
    /// <summary>
    /// 이 기기의 온라인 로그인(11-online.md 7절). 서버가 준 계정 id와 비밀을 파일 하나에 둔다 — 비밀은 이메일·Google로 들어왔을 때
    /// 서버가 준 로그인 표(토큰), 옛 기기 계정이면 기기가 만든 32바이트. 비밀번호는 저장하지 않는다.
    /// 오프라인 프로필 파일과 따로다(서로 옮기지 않는다).
    /// </summary>
    public sealed class OnlineAccount
    {
        public long Id;
        public string Name;
        public string Secret;
        public AccountKind Kind;
        public string Email;

        /// <summary>로그인 결과를 이 기기에 저장한다(토큰이 있을 때만).</summary>
        public static OnlineAccount SaveLogin(long id, ServerProfile profile, string token, string email, AccountKind kind)
        {
            if (id <= 0 || string.IsNullOrEmpty(token)) return null;
            var a = new OnlineAccount { Id = id, Name = profile != null ? profile.Name : null, Secret = token, Email = email, Kind = kind };
            a.Save();
            return a;
        }

        /// <summary>저장 파일. 기본 = Application.persistentDataPath/online_account.json. 테스트는 바꾼다.</summary>
        public static string FilePath;

        static string _defaultPath;

        /// <summary>
        /// Application.persistentDataPath는 메인 스레드에서만 읽힌다. 로그인 응답은 Task.Run(백그라운드)에서 저장되므로,
        /// 예전에는 읽기에 실패해 상대 경로("online_account.json" = 프로젝트 폴더)에 저장되고 화면은 다른 경로를 읽어
        /// 로그인에 성공해도 계정이 없는 걸로 보였다. 부팅 때 메인 스레드에서 경로를 한 번 잡아 둔다.
        /// </summary>
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void CachePath()
        {
            try { _defaultPath = System.IO.Path.Combine(UnityEngine.Application.persistentDataPath, "online_account.json"); }
            catch (Exception) { }
        }

        static string PathOrDefault()
        {
            if (!string.IsNullOrEmpty(FilePath)) return FilePath;
            if (_defaultPath == null) CachePath();
            return _defaultPath ?? "online_account.json";
        }

        public static OnlineAccount Load()
        {
            try
            {
                string p = PathOrDefault();
                if (!File.Exists(p)) return null;
                var a = JsonConvert.DeserializeObject<OnlineAccount>(File.ReadAllText(p, Encoding.UTF8));
                return a != null && a.Id > 0 && !string.IsNullOrEmpty(a.Secret) ? a : null;
            }
            catch (Exception) { return null; }
        }

        void Save()
        {
            string p = PathOrDefault();
            var dir = System.IO.Path.GetDirectoryName(p);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(p, JsonConvert.SerializeObject(this, Formatting.Indented), Encoding.UTF8);
        }

        public static void Forget()
        {
            try { File.Delete(PathOrDefault()); } catch (Exception) { }
        }

        static string NewSecret()
        {
            var b = new byte[32];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(b);
            return Convert.ToBase64String(b);
        }

        /// <summary>서버에 이름으로 계정을 만들고 이 기기에 저장한다(최대 timeoutMs 기다림). 실패면 null과 이유.</summary>
        public static OnlineAccount Register(IContentDb db, string name, out AuthError? error, out ServerProfile profile, int timeoutMs = 4000)
        {
            error = null;
            profile = null;
            var pipe = OnlineServer.Connect(db);
            if (pipe == null) return null;
            string secret = NewSecret();
            var m = new NetWriter(NetMsg.Register);
            m.Str(name);
            m.Str(secret);
            pipe.Send(m.ToArray());
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            try
            {
                while (DateTime.UtcNow < deadline && pipe.Connected)
                {
                    if (!pipe.TryReceive(out var data)) { Thread.Sleep(5); continue; }
                    var r = new NetReader(data);
                    var kind = (NetMsg)r.Byte();
                    if (kind == NetMsg.AuthFail) { error = (AuthError)r.Byte(); return null; }
                    if (kind != NetMsg.AuthOk) continue;
                    long id = r.Long();
                    profile = NetCodec.ReadProfile(r);
                    var a = new OnlineAccount { Id = id, Name = profile.Name, Secret = secret };
                    a.Save();
                    return a;
                }
            }
            finally { pipe.Close(); }
            return null;
        }

        /// <summary>통로 맨 앞에 Login을 보낸다(응답 AuthOk/AuthFail은 이어 읽는 쪽이 받는다).</summary>
        public void SendLogin(INetPipe pipe)
        {
            var m = new NetWriter(NetMsg.Login);
            m.Long(Id);
            m.Str(Secret);
            pipe.Send(m.ToArray());
        }
    }
}
