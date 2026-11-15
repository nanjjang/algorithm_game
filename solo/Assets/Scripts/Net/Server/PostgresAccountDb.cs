using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Npgsql;
using NpgsqlTypes;

namespace Wizard.Solo.Net
{
    /// <summary>
    /// PostgreSQL 계정 저장소(docs/spec/08 8.7절, 11-online.md 7절). 표:
    ///   accounts(계정·인증·서버 프로필 숫자, 지팡이 상태는 jsonb), items(창고, 수량 &gt; 0), friends, friend_requests,
    ///   sessions(로그인 토큰의 SHA-256), listings(장터), enhance_log(강화 기록, 지우지 않는다), meta(다음 id).
    /// 서버 프로세스 하나가 메모리 사본을 들고 있고, Save() 한 번 = 트랜잭션 한 번(손댄 계정 줄을 통째로 다시 쓴다).
    /// 골드 ≥ 0, 수량 &gt; 0, 이름·이메일·Google sub 유일은 DB 제약으로도 막는다(코드가 잘못해도 커밋이 실패한다).
    /// </summary>
    public sealed class PostgresAccountDb : IAccountDb
    {
        public const int SchemaVersion = 1;

        readonly string _cs;
        NpgsqlConnection _conn;

        public PostgresAccountDb(string connectionString)
        {
            _cs = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        NpgsqlConnection Conn()
        {
            if (_conn != null && _conn.State == System.Data.ConnectionState.Open) return _conn;
            try { _conn?.Dispose(); } catch (Exception) { }
            _conn = new NpgsqlConnection(_cs);
            _conn.Open();
            return _conn;
        }

        static void Exec(NpgsqlConnection c, NpgsqlTransaction tx, string sql)
        {
            using (var cmd = new NpgsqlCommand(sql, c, tx)) cmd.ExecuteNonQuery();
        }

        const string SchemaV1 = @"
create table if not exists accounts (
  id bigint primary key,
  name text not null,
  name_key text not null unique,
  email text,
  email_key text unique,
  email_verified boolean not null default false,
  pw_salt text, pw_hash text, pw_iter integer not null default 0,
  google_sub text unique,
  device_salt text, device_hash text,
  created_at text, last_seen_at text,
  gold bigint not null default 0 check (gold >= 0),
  rating integer not null default 1000,
  duel_wins integer not null default 0, duel_losses integer not null default 0, duel_draws integer not null default 0,
  coop_runs integer not null default 0, coop_best_wave integer not null default 0,
  enhance_tries integer not null default 0,
  next_attempt_seq bigint not null default 1,
  wand_state jsonb not null default '{}'::jsonb
);
create table if not exists items (
  account_id bigint not null references accounts(id) on delete cascade,
  item_id text not null,
  qty integer not null check (qty > 0),
  primary key (account_id, item_id)
);
create table if not exists friends (
  account_id bigint not null references accounts(id) on delete cascade,
  friend_id bigint not null references accounts(id) on delete cascade,
  primary key (account_id, friend_id)
);
create table if not exists friend_requests (
  to_id bigint not null references accounts(id) on delete cascade,
  from_id bigint not null references accounts(id) on delete cascade,
  ord integer not null default 0,
  primary key (to_id, from_id)
);
create table if not exists sessions (
  token_hash text primary key,
  account_id bigint not null references accounts(id) on delete cascade,
  created_ticks bigint not null,
  expires_ticks bigint not null
);
create index if not exists sessions_account on sessions(account_id);
create table if not exists listings (
  id bigint primary key,
  seller_id bigint not null references accounts(id) on delete cascade,
  item_id text not null,
  qty integer not null check (qty > 0),
  price bigint not null check (price > 0),
  deposit bigint not null check (deposit >= 0),
  created_ticks bigint not null
);
create index if not exists listings_item on listings(item_id);
create index if not exists listings_seller on listings(seller_id);
create table if not exists enhance_log (
  id bigserial primary key,
  account_id bigint not null references accounts(id) on delete cascade,
  character integer not null,
  stage_before integer not null,
  stage_after integer not null,
  outcome text not null,
  at_ticks bigint not null
);
create index if not exists enhance_log_account on enhance_log(account_id);
create table if not exists meta (key text primary key, value bigint not null);
";

        /// <summary>표가 없으면 만든다(판 번호는 schema_version 표).</summary>
        public void Migrate()
        {
            var c = Conn();
            using (var tx = c.BeginTransaction())
            {
                Exec(c, tx, "create table if not exists schema_version (v integer not null)");
                int v = 0;
                using (var cmd = new NpgsqlCommand("select coalesce(max(v), 0) from schema_version", c, tx)) v = Convert.ToInt32(cmd.ExecuteScalar());
                if (v < 1)
                {
                    Exec(c, tx, SchemaV1);
                    Exec(c, tx, "insert into schema_version(v) values (1)");
                }
                tx.Commit();
            }
        }

        sealed class WandState
        {
            public int[] WandStages, Auras, PeakStages, SlotsExtra;
            public List<List<Wizard.Solo.Meta.MutationEntry>> Mutations;
            public List<ServerMutationOffer> PendingOffers;
            public List<ServerLoadout> Loadouts;
        }

        static string Str(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);

        public AccountData Load()
        {
            Migrate();
            var c = Conn();
            var d = new AccountData();
            var byId = new Dictionary<long, AccountRecord>();
            using (var cmd = new NpgsqlCommand(@"select id, name, email, email_verified, pw_salt, pw_hash, pw_iter, google_sub, device_salt, device_hash,
  created_at, last_seen_at, gold, rating, duel_wins, duel_losses, duel_draws, coop_runs, coop_best_wave, enhance_tries, next_attempt_seq, wand_state::text
  from accounts order by id", c))
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    var a = new AccountRecord
                    {
                        Id = r.GetInt64(0), Name = r.GetString(1), Email = Str(r, 2), EmailVerified = r.GetBoolean(3),
                        PwSalt = Str(r, 4), PwHash = Str(r, 5), PwIter = r.GetInt32(6), GoogleSub = Str(r, 7), Salt = Str(r, 8), SecretHash = Str(r, 9),
                    };
                    var p = a.Profile;
                    p.AccountId = a.Id;
                    p.Name = a.Name;
                    p.CreatedAt = Str(r, 10);
                    p.LastSeenAt = Str(r, 11);
                    p.Gold = r.GetInt64(12);
                    p.Rating = r.GetInt32(13);
                    p.DuelWins = r.GetInt32(14); p.DuelLosses = r.GetInt32(15); p.DuelDraws = r.GetInt32(16);
                    p.CoopRuns = r.GetInt32(17); p.CoopBestWave = r.GetInt32(18);
                    p.EnhanceTries = r.GetInt32(19);
                    p.NextAttemptSeq = r.GetInt64(20);
                    var w = JsonConvert.DeserializeObject<WandState>(r.GetString(21)) ?? new WandState();
                    if (w.WandStages != null) p.WandStages = w.WandStages;
                    if (w.Auras != null) p.Auras = w.Auras;
                    if (w.PeakStages != null) p.PeakStages = w.PeakStages;
                    if (w.SlotsExtra != null) p.SlotsExtra = w.SlotsExtra;
                    if (w.Mutations != null) p.Mutations = w.Mutations;
                    if (w.PendingOffers != null) p.PendingOffers = w.PendingOffers;
                    if (w.Loadouts != null) p.Loadouts = w.Loadouts;
                    d.Accounts.Add(a);
                    byId[a.Id] = a;
                }
            }
            using (var cmd = new NpgsqlCommand("select account_id, item_id, qty from items", c))
            using (var r = cmd.ExecuteReader())
                while (r.Read()) if (byId.TryGetValue(r.GetInt64(0), out var a)) a.Profile.Items[r.GetString(1)] = r.GetInt32(2);
            using (var cmd = new NpgsqlCommand("select account_id, friend_id from friends order by account_id, friend_id", c))
            using (var r = cmd.ExecuteReader())
                while (r.Read()) if (byId.TryGetValue(r.GetInt64(0), out var a)) a.Profile.Friends.Add(r.GetInt64(1));
            using (var cmd = new NpgsqlCommand("select to_id, from_id from friend_requests order by to_id, ord", c))
            using (var r = cmd.ExecuteReader())
                while (r.Read()) if (byId.TryGetValue(r.GetInt64(0), out var a)) a.Profile.FriendRequests.Add(r.GetInt64(1));
            using (var cmd = new NpgsqlCommand("select account_id, token_hash, created_ticks, expires_ticks from sessions", c))
            using (var r = cmd.ExecuteReader())
                while (r.Read())
                    if (byId.TryGetValue(r.GetInt64(0), out var a))
                        a.Sessions.Add(new SessionRecord { TokenHash = r.GetString(1), CreatedTicks = r.GetInt64(2), ExpiresTicks = r.GetInt64(3) });
            using (var cmd = new NpgsqlCommand("select id, seller_id, item_id, qty, price, deposit, created_ticks from listings order by id", c))
            using (var r = cmd.ExecuteReader())
                while (r.Read())
                {
                    long seller = r.GetInt64(1);
                    d.Listings.Add(new MarketListing
                    {
                        Id = r.GetInt64(0), SellerId = seller, SellerName = byId.TryGetValue(seller, out var s) ? s.Name : string.Empty,
                        ItemId = r.GetString(2), Qty = r.GetInt32(3), Price = r.GetInt64(4), Deposit = r.GetInt64(5), CreatedTicks = r.GetInt64(6),
                    });
                }
            long maxId = 0, maxListing = 0;
            foreach (var a in d.Accounts) maxId = Math.Max(maxId, a.Id);
            foreach (var l in d.Listings) maxListing = Math.Max(maxListing, l.Id);
            d.NextId = Math.Max(maxId + 1, MetaValue(c, "next_account_id"));
            d.NextListingId = Math.Max(maxListing + 1, MetaValue(c, "next_listing_id"));
            return d;
        }

        static long MetaValue(NpgsqlConnection c, string key)
        {
            using (var cmd = new NpgsqlCommand("select value from meta where key = @k", c))
            {
                cmd.Parameters.AddWithValue("k", key);
                var v = cmd.ExecuteScalar();
                return v == null || v is DBNull ? 1 : Convert.ToInt64(v);
            }
        }

        static NpgsqlParameter P(string name, object v) => new NpgsqlParameter(name, v ?? DBNull.Value);

        public void Commit(AccountData all, AccountDbChanges ch)
        {
            if (ch == null || ch.IsEmpty) return;
            var c = Conn();
            using (var tx = c.BeginTransaction())
            {
                foreach (var a in ch.Accounts) UpsertAccount(c, tx, a);
                foreach (var a in ch.Accounts) WriteChildren(c, tx, a);
                foreach (var id in ch.RemovedListings)
                    using (var cmd = new NpgsqlCommand("delete from listings where id = @id", c, tx)) { cmd.Parameters.Add(P("id", id)); cmd.ExecuteNonQuery(); }
                foreach (var l in ch.AddedListings)
                    using (var cmd = new NpgsqlCommand(@"insert into listings(id, seller_id, item_id, qty, price, deposit, created_ticks)
  values (@id, @s, @i, @q, @p, @d, @t) on conflict (id) do nothing", c, tx))
                    {
                        cmd.Parameters.Add(P("id", l.Id)); cmd.Parameters.Add(P("s", l.SellerId)); cmd.Parameters.Add(P("i", l.ItemId));
                        cmd.Parameters.Add(P("q", l.Qty)); cmd.Parameters.Add(P("p", l.Price)); cmd.Parameters.Add(P("d", l.Deposit)); cmd.Parameters.Add(P("t", l.CreatedTicks));
                        cmd.ExecuteNonQuery();
                    }
                foreach (var e in ch.EnhanceLog)
                    using (var cmd = new NpgsqlCommand(@"insert into enhance_log(account_id, character, stage_before, stage_after, outcome, at_ticks)
  values (@a, @c, @b, @f, @o, @t)", c, tx))
                    {
                        cmd.Parameters.Add(P("a", e.AccountId)); cmd.Parameters.Add(P("c", e.Character)); cmd.Parameters.Add(P("b", e.StageBefore));
                        cmd.Parameters.Add(P("f", e.StageAfter)); cmd.Parameters.Add(P("o", e.Outcome ?? string.Empty)); cmd.Parameters.Add(P("t", e.AtTicks));
                        cmd.ExecuteNonQuery();
                    }
                foreach (var kv in new[] { ("next_account_id", ch.NextId), ("next_listing_id", ch.NextListingId) })
                    using (var cmd = new NpgsqlCommand("insert into meta(key, value) values (@k, @v) on conflict (key) do update set value = greatest(meta.value, excluded.value)", c, tx))
                    {
                        cmd.Parameters.Add(P("k", kv.Item1)); cmd.Parameters.Add(P("v", kv.Item2));
                        cmd.ExecuteNonQuery();
                    }
                tx.Commit();
            }
        }

        static string NameKey(string n) => n == null ? null : n.Trim().ToLowerInvariant();

        static void UpsertAccount(NpgsqlConnection c, NpgsqlTransaction tx, AccountRecord a)
        {
            var p = a.Profile;
            var w = new WandState
            {
                WandStages = p.WandStages, Auras = p.Auras, PeakStages = p.PeakStages, SlotsExtra = p.SlotsExtra,
                Mutations = p.Mutations, PendingOffers = p.PendingOffers, Loadouts = p.Loadouts,
            };
            using (var cmd = new NpgsqlCommand(@"insert into accounts(id, name, name_key, email, email_key, email_verified, pw_salt, pw_hash, pw_iter, google_sub,
  device_salt, device_hash, created_at, last_seen_at, gold, rating, duel_wins, duel_losses, duel_draws, coop_runs, coop_best_wave, enhance_tries,
  next_attempt_seq, wand_state)
  values (@id, @name, @nk, @em, @ek, @ev, @ps, @ph, @pi, @gs, @ds, @dh, @ca, @ls, @gold, @rt, @dw, @dl, @dd, @cr, @cb, @et, @na, @ws)
  on conflict (id) do update set name = excluded.name, name_key = excluded.name_key, email = excluded.email, email_key = excluded.email_key,
  email_verified = excluded.email_verified, pw_salt = excluded.pw_salt, pw_hash = excluded.pw_hash, pw_iter = excluded.pw_iter,
  google_sub = excluded.google_sub, device_salt = excluded.device_salt, device_hash = excluded.device_hash, created_at = excluded.created_at,
  last_seen_at = excluded.last_seen_at, gold = excluded.gold, rating = excluded.rating, duel_wins = excluded.duel_wins,
  duel_losses = excluded.duel_losses, duel_draws = excluded.duel_draws, coop_runs = excluded.coop_runs, coop_best_wave = excluded.coop_best_wave,
  enhance_tries = excluded.enhance_tries, next_attempt_seq = excluded.next_attempt_seq, wand_state = excluded.wand_state", c, tx))
            {
                cmd.Parameters.Add(P("id", a.Id));
                cmd.Parameters.Add(P("name", a.Name));
                cmd.Parameters.Add(P("nk", NameKey(a.Name)));
                cmd.Parameters.Add(P("em", a.Email));
                cmd.Parameters.Add(P("ek", string.IsNullOrEmpty(a.Email) ? null : AccountStore.EmailKey(a.Email)));
                cmd.Parameters.Add(P("ev", a.EmailVerified));
                cmd.Parameters.Add(P("ps", a.PwSalt));
                cmd.Parameters.Add(P("ph", a.PwHash));
                cmd.Parameters.Add(P("pi", a.PwIter));
                cmd.Parameters.Add(P("gs", a.GoogleSub));
                cmd.Parameters.Add(P("ds", a.Salt));
                cmd.Parameters.Add(P("dh", a.SecretHash));
                cmd.Parameters.Add(P("ca", p.CreatedAt));
                cmd.Parameters.Add(P("ls", p.LastSeenAt));
                cmd.Parameters.Add(P("gold", p.Gold));
                cmd.Parameters.Add(P("rt", p.Rating));
                cmd.Parameters.Add(P("dw", p.DuelWins));
                cmd.Parameters.Add(P("dl", p.DuelLosses));
                cmd.Parameters.Add(P("dd", p.DuelDraws));
                cmd.Parameters.Add(P("cr", p.CoopRuns));
                cmd.Parameters.Add(P("cb", p.CoopBestWave));
                cmd.Parameters.Add(P("et", p.EnhanceTries));
                cmd.Parameters.Add(P("na", p.NextAttemptSeq));
                cmd.Parameters.Add(new NpgsqlParameter("ws", NpgsqlDbType.Jsonb) { Value = JsonConvert.SerializeObject(w) });
                cmd.ExecuteNonQuery();
            }
        }

        static void WriteChildren(NpgsqlConnection c, NpgsqlTransaction tx, AccountRecord a)
        {
            var p = a.Profile;
            void Del(string sql)
            {
                using (var cmd = new NpgsqlCommand(sql, c, tx)) { cmd.Parameters.Add(P("id", a.Id)); cmd.ExecuteNonQuery(); }
            }
            Del("delete from items where account_id = @id");
            if (p.Items != null)
                foreach (var kv in p.Items)
                {
                    if (kv.Value <= 0) continue;
                    using (var cmd = new NpgsqlCommand("insert into items(account_id, item_id, qty) values (@id, @i, @q)", c, tx))
                    { cmd.Parameters.Add(P("id", a.Id)); cmd.Parameters.Add(P("i", kv.Key)); cmd.Parameters.Add(P("q", kv.Value)); cmd.ExecuteNonQuery(); }
                }
            Del("delete from friends where account_id = @id");
            if (p.Friends != null)
                foreach (var f in new HashSet<long>(p.Friends))
                    using (var cmd = new NpgsqlCommand("insert into friends(account_id, friend_id) select @id, @f where exists (select 1 from accounts where id = @f)", c, tx))
                    { cmd.Parameters.Add(P("id", a.Id)); cmd.Parameters.Add(P("f", f)); cmd.ExecuteNonQuery(); }
            Del("delete from friend_requests where to_id = @id");
            if (p.FriendRequests != null)
            {
                int ord = 0;
                foreach (var f in p.FriendRequests)
                    using (var cmd = new NpgsqlCommand("insert into friend_requests(to_id, from_id, ord) select @id, @f, @o where exists (select 1 from accounts where id = @f) on conflict do nothing", c, tx))
                    { cmd.Parameters.Add(P("id", a.Id)); cmd.Parameters.Add(P("f", f)); cmd.Parameters.Add(P("o", ord++)); cmd.ExecuteNonQuery(); }
            }
            Del("delete from sessions where account_id = @id");
            if (a.Sessions != null)
                foreach (var s in a.Sessions)
                    using (var cmd = new NpgsqlCommand("insert into sessions(token_hash, account_id, created_ticks, expires_ticks) values (@h, @id, @c, @e) on conflict (token_hash) do nothing", c, tx))
                    { cmd.Parameters.Add(P("h", s.TokenHash)); cmd.Parameters.Add(P("id", a.Id)); cmd.Parameters.Add(P("c", s.CreatedTicks)); cmd.Parameters.Add(P("e", s.ExpiresTicks)); cmd.ExecuteNonQuery(); }
        }

        /// <summary>표가 비어 있나(JSON에서 옮겨 올지 정할 때).</summary>
        public bool IsEmpty()
        {
            Migrate();
            using (var cmd = new NpgsqlCommand("select count(*) from accounts", Conn())) return Convert.ToInt64(cmd.ExecuteScalar()) == 0;
        }

        /// <summary>JSON 저장 파일을 통째로 옮긴다(한 트랜잭션). 빈 DB에서만.</summary>
        public void Import(AccountData data)
        {
            var ch = new AccountDbChanges { NextId = data.NextId, NextListingId = data.NextListingId };
            ch.Accounts.AddRange(data.Accounts);
            ch.AddedListings.AddRange(data.Listings);
            Commit(data, ch);
        }

        /// <summary>테스트용: 표를 전부 지운다.</summary>
        public void DropAllForTests()
        {
            var c = Conn();
            using (var tx = c.BeginTransaction())
            {
                Exec(c, tx, "drop table if exists enhance_log, listings, sessions, friend_requests, friends, items, accounts, meta, schema_version cascade");
                tx.Commit();
            }
        }

        public void Dispose()
        {
            try { _conn?.Dispose(); } catch (Exception) { }
            _conn = null;
        }
    }
}
