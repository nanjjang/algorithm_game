using System;
using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Sim;

namespace Wizard.Solo.View.Effects
{
    /// <summary>소리 한 번(사건 id = event_bindings.json 열쇠).</summary>
    public struct SoundCue
    {
        public string Id;
        public SoundBus Bus;
        public float Volume;
        public float Pitch;
        public override string ToString() => Id + "@" + Bus;
    }

    /// <summary>
    /// 표시 이벤트 → 소리 사건(순수에 가깝다: 시간 셈은 이 객체가 갖고, 난수는 주입한다). 10 요약서 5절, D-72.
    /// - 사건 id는 event_bindings.json의 열쇠이고, 그 줄에 sfx가 묶여 있을 때만 낸다(빈 sfx = 나중에 꽂을 자리).
    /// - 묶음: 내 소리 = Sfx, 지팡이 소리(발사·임계 사격·잠금) = Wand, 적 소리(예고·적 탄·적 울음·보스) = Enemy — G16.json sound.buses.
    /// - 음높이: 연속 스침 +1반음씩 5단(2초 창, 정확히 2초도 이어짐), 결정 생김·만개 파동은 5음 음계 번호, 적중 ±2반음 흩음,
    ///   약점 +2반음, 저항 −3반음 −3dB, 연쇄 전이마다 높아짐.
    /// - 솔로 보충 사건(G16 sound.solo_events — 원문 bindings에서 sfx가 빈 줄 + 솔로 새 열쇠): 클립은 DataSolo/Audio.json의 events가 준다.
    ///   발사 블록음(heat.shot_block.블록, C-01 블록별), 적 종별 울음(공격 직전 WindUp, sound.enemy_windup), 처치 음색 6묶음
    ///   (enemy.death.묶음, H-05, sound.kill_groups), 줍기(drop.*), 웨이브 시작·클리어(wave.started·wave.cleared), 불가마 꺼짐(art.kiln.end).
    /// - 섞는 규칙(동시 수·초당 횟수·우선순위·누르기)은 재생기(F4)가 한다 — 여기서 다시 세지 않는다.
    ///   다만 사건 자체의 규칙(0.3초 안 5마리 처치는 한꺼번에 처치 소리 하나, 감속 걸림은 같은 적 0.5초 1번)은 여기서 센다.
    /// </summary>
    public sealed class SoundEventMap
    {
        readonly EventBindings _bind;
        readonly EffectsTuning _t;
        readonly Func<float, float, float> _random;
        readonly List<KeyValuePair<string, SoundBus>> _busRules = new List<KeyValuePair<string, SoundBus>>();
        readonly SoundBus _defaultBus;
        readonly Dictionary<string, string> _artFire;
        readonly int[] _pentatonic;
        readonly GrazePitchCounter _graze;
        readonly KillBurstCounter _kills;
        readonly GapGate _slowGate;
        readonly Dictionary<int, float> _bloomRepeats = new Dictionary<int, float>();
        readonly HashSet<string> _soloEvents = new HashSet<string>();
        readonly List<string> _soloPrefixes = new List<string>();
        readonly Dictionary<string, string> _enemyWindup;
        readonly Dictionary<string, string> _killGroups;
        readonly string _shotBlockPrefix;
        // 슬롯 → (틱, 맨 위 스코프 첫 탄의 블록 id). 같은 틱에 ProjectileSpawned가 ShotFired보다 먼저 온다.
        readonly Dictionary<int, KeyValuePair<int, string>> _rootBlock = new Dictionary<int, KeyValuePair<int, string>>();
        int _bossKilledTick = int.MinValue;

        /// <param name="random">(최소, 최대) → 표시용 난수. null이면 UnityEngine.Random.Range.</param>
        public SoundEventMap(EventBindings bindings, EffectsTuning tuning, Func<float, float, float> random = null)
        {
            _bind = bindings ?? new EventBindings();
            _t = tuning ?? new EffectsTuning(null);
            _random = random ?? ((a, b) => UnityEngine.Random.Range(a, b));
            var rules = _t.Token("sound", "buses") as Newtonsoft.Json.Linq.JArray;
            if (rules != null)
                foreach (var r in rules)
                {
                    var a = r as Newtonsoft.Json.Linq.JArray;
                    if (a == null || a.Count < 2) continue;
                    _busRules.Add(new KeyValuePair<string, SoundBus>((string)a[0], ParseBus((string)a[1])));
                }
            _defaultBus = ParseBus(_t.S("sound", "default_bus"));
            _artFire = _t.StringMap("sound", "art_fire");
            _pentatonic = _t.Ints("sound", "pentatonic_semitones");
            _graze = new GrazePitchCounter(_bind.GrazeChainWindowSeconds, _bind.GrazeChainMaxStep);
            _kills = new KillBurstCounter(_t.F("multi_kill", "window_seconds"), _t.I("multi_kill", "count"));
            _slowGate = new GapGate(_t.F("sound", "slow_applied_gap_seconds"));
            _enemyWindup = _t.StringMap("sound", "enemy_windup") ?? new Dictionary<string, string>();
            _killGroups = _t.StringMap("sound", "kill_groups") ?? new Dictionary<string, string>();
            _shotBlockPrefix = _t.S("sound", "shot_block_prefix");
            var solo = _t.Token("sound", "solo_events") as Newtonsoft.Json.Linq.JArray;
            if (solo != null)
                foreach (var x in solo)
                {
                    if (x.Type != Newtonsoft.Json.Linq.JTokenType.String) continue;
                    string k = (string)x;
                    if (string.IsNullOrEmpty(k)) continue;
                    if (k[k.Length - 1] == '.') _soloPrefixes.Add(k); else _soloEvents.Add(k);
                }
            foreach (var kv in _enemyWindup) _soloEvents.Add(kv.Value);
            foreach (var kv in _killGroups) _soloEvents.Add(kv.Value);
            if (!string.IsNullOrEmpty(_shotBlockPrefix)) _soloPrefixes.Add(_shotBlockPrefix);
        }

        /// <summary>원문 bindings에 sfx가 없어도 내는 솔로 보충 사건인가(G16 sound.solo_events, '.'으로 끝나면 앞머리).</summary>
        public bool IsSoloEvent(string id)
        {
            if (id == null) return false;
            if (_soloEvents.Contains(id)) return true;
            for (int i = 0; i < _soloPrefixes.Count; i++) if (id.StartsWith(_soloPrefixes[i], StringComparison.Ordinal)) return true;
            return false;
        }

        public GrazePitchCounter Graze => _graze;

        public static SoundBus ParseBus(string s)
        {
            switch (s)
            {
                case "wand": return SoundBus.Wand;
                case "enemy": return SoundBus.Enemy;
                case "ui": return SoundBus.Ui;
                case "bgm": return SoundBus.Bgm;
                default: return SoundBus.Sfx;
            }
        }

        /// <summary>사건 id → 묶음(정확한 id 또는 '.'으로 끝나는 앞머리, 위에서부터).</summary>
        public SoundBus BusOf(string id)
        {
            if (id == null) return _defaultBus;
            for (int i = 0; i < _busRules.Count; i++)
            {
                string k = _busRules[i].Key;
                if (string.IsNullOrEmpty(k)) continue;
                bool prefix = k[k.Length - 1] == '.';
                if (prefix ? id.StartsWith(k, StringComparison.Ordinal) : id == k) return _busRules[i].Value;
            }
            return _defaultBus;
        }

        public static float SemitonesToPitch(float semitones) => Mathf.Pow(2f, semitones / 12f);
        public static float DbToVolume(float db) => Mathf.Pow(10f, db / 20f);

        /// <summary>5음 음계의 n번째 음(0부터, 한 바퀴 돌면 한 옥타브 위) → 반음.</summary>
        public int PentatonicSemitones(int index)
        {
            if (_pentatonic == null || _pentatonic.Length == 0 || index < 0) return 0;
            int n = _pentatonic.Length;
            return (index / n) * 12 + _pentatonic[index % n];
        }

        /// <summary>그 열쇠에 소리가 묶여 있으면 하나 더한다.</summary>
        bool Add(List<SoundCue> into, string id, float semitones = 0f, float db = 0f)
        {
            if (!_bind.HasSound(id) && !IsSoloEvent(id)) return false;
            into.Add(new SoundCue { Id = id, Bus = BusOf(id), Volume = db == 0f ? 1f : DbToVolume(db), Pitch = semitones == 0f ? 1f : SemitonesToPitch(semitones) });
            return true;
        }

        /// <summary>이벤트 하나 → 낼 소리(0개 이상)를 into에 더한다.</summary>
        public void Map(SimWorld w, in SimEvent e, List<SoundCue> into)
        {
            switch (e.Type)
            {
                case SimEventType.ShotFired:
                {
                    Add(into, "heat.shot_fired");
                    // C-01 발사 블록별 소리(캐릭터 덧소리 C-02 아래에 깐다).
                    string blk = RootBlockOf(w, e);
                    if (e.B != 0) Add(into, "heat.crit_shot_fired");
                    if (blk != null && !string.IsNullOrEmpty(_shotBlockPrefix))
                        Add(into, _shotBlockPrefix + (blk.StartsWith("block.", StringComparison.Ordinal) ? blk.Substring(6) : blk));
                    break;
                }
                case SimEventType.FireDryWhileLocked: Add(into, "heat.fire_dry_while_locked"); break;
                case SimEventType.HeatRingChanged:
                    switch ((HeatRingState)e.A)
                    {
                        case HeatRingState.Imminent: Add(into, "heat.entered_imminent"); break;
                        case HeatRingState.Critical: Add(into, "heat.entered_critical"); break;
                        case HeatRingState.Brink: Add(into, "heat.entered_brink"); break;
                    }
                    break;
                case SimEventType.HeatLockEntered: Add(into, "heat.lock_entered"); break;
                case SimEventType.HeatUnlocked:
                    if ((HeatUnlockReason)e.A == HeatUnlockReason.VentBlast) Add(into, "art.vent_burst.lock_broken");
                    else if ((HeatUnlockReason)e.A != HeatUnlockReason.Graze) Add(into, "heat.unlocked");
                    break;
                // C-11 스침 잠금 해제: 묶음에서 sfx_c11을 가진 줄이 vent_burst.lock_broken 하나뿐이다.
                case SimEventType.LockBrokenByGraze: Add(into, "art.vent_burst.lock_broken"); break;
                case SimEventType.GrazeAwarded:
                {
                    int step = _graze.Next(e.Slot, e.Tick);
                    float semi = step * _t.F("sound", "graze_semitones_per_step");
                    string id = (GrazeKind)e.B == GrazeKind.BoltDash ? "art.bolt_dash.graze" : "graze.awarded";
                    Add(into, id, semi);
                    break;
                }
                case SimEventType.PlayerHit: Add(into, "player.hit"); break;
                case SimEventType.InvulnEnded: Add(into, "player.invuln_ended"); break;
                case SimEventType.PlayerDowned:
                    Add(into, w != null && w.Boss != null && w.Boss.FirstEncounter ? "phantom_first.downed" : "player.downed");
                    break;
                case SimEventType.PlayerRose: Add(into, "player.rose"); break;
                case SimEventType.ComboReset: if (e.B > 0) Add(into, "combo.reset"); break;
                case SimEventType.CrystalGained: Add(into, "crystal.gained", PentatonicSemitones(e.A - 1)); break;
                case SimEventType.CrystalsFull: Add(into, "crystal.full"); break;
                case SimEventType.CrystalLost: Add(into, "crystal.shattered"); break;
                case SimEventType.CrystalProtected: Add(into, "crystal.protected"); break;
                case SimEventType.DebtAdded: Add(into, "crystal.debt_created"); break;
                case SimEventType.DebtRepaid: Add(into, "crystal.debt_repaid"); break;

                case SimEventType.ArtFired:
                {
                    Add(into, "art.cast");
                    if (e.Id == Ids.Art.ThunderSpear && e.B != 0) Add(into, "art.thunder_lance.just_signal");
                    string k;
                    if (e.Id != null && _artFire.TryGetValue(e.Id, out k)) Add(into, k);
                    if (e.Id == Ids.Art.Bloom) _bloomRepeats[e.Slot] = e.F;
                    break;
                }
                case SimEventType.ArtFizzled:
                    if ((ArtFizzleReason)e.A == ArtFizzleReason.NoCrystal) Add(into, "art.fizzle_no_crystal");
                    else Add(into, "art.fizzle_late");
                    break;
                case SimEventType.ArtPulse:
                    if (e.Id == Ids.Art.Bloom)
                    {
                        float reps;
                        bool last = _bloomRepeats.TryGetValue(e.Slot, out reps) && reps > 0f && e.A >= Mathf.RoundToInt(reps);
                        if (last) Add(into, "art.full_bloom.final_wave");
                        else Add(into, "art.full_bloom.wave", PentatonicSemitones(e.A > 0 ? e.A - 1 : 0));
                    }
                    break;
                case SimEventType.PlayerZoneExpired:
                    switch ((PlayerZoneKind)e.A)
                    {
                        case PlayerZoneKind.CopyCircle: Add(into, "art.transcribe.end"); break;
                        case PlayerZoneKind.IceScreen: Add(into, e.B != 0 ? "art.ice_screen.shatter" : "art.ice_screen.expire"); break;
                        case PlayerZoneKind.Kiln: Add(into, "art.kiln.end"); break;
                    }
                    break;
                case SimEventType.PlayerMovedByArt:
                    if (e.Id == Ids.Art.BoltDash) Add(into, "art.bolt_dash.arrive");
                    break;
                case SimEventType.BoiloverTriggered: Add(into, "heat.boilover_triggered"); break;
                case SimEventType.LightningRodStrike: Add(into, "graze.lightning_rod_strike"); break;
                case SimEventType.UnstableCoreExploded: Add(into, "mutation.unstable_core.self_detonate"); break;
                case SimEventType.ThinIceFroze: Add(into, "char.thin_ice_frozen"); break;

                case SimEventType.Explosion:
                    // 기본 공격 유형이 낸 폭발(B = 1)은 유형 소리(StyleBlast)가 대신한다 — 두 번 내지 않는다.
                    if (!StyleFxRules.IsStyleExplosion(e)) Add(into, "explosion.detonated");
                    break;
                // ---- 기본 공격 유형·고유 비기 강화(Docs/12-attack-styles.md 3·4절). 클립은 Audio.json common.events style.*
                case SimEventType.StyleBlast:
                    switch ((StyleBlastKind)e.B)
                    {
                        case StyleBlastKind.MortarLand: Add(into, "style.lob.land", e.A > 0 ? 0f : -1f); break;
                        case StyleBlastKind.SkyStrike: Add(into, "style.strike.land"); break;
                        case StyleBlastKind.IcicleDrop: Add(into, "style.icicle.land", _random(-1.5f, 1.5f)); break;
                        case StyleBlastKind.RuneBurst: Add(into, "style.rune.burst"); break;
                    }
                    break;
                case SimEventType.StyleGroundSpawned: if (e.B == 0) Add(into, "style.floor.start"); break;
                case SimEventType.StyleMineArmed: Add(into, "style.rune.arm"); break;
                case SimEventType.StyleRicochet: Add(into, "style.ricochet", _random(-1f, 2f)); break;
                case SimEventType.StyleZap: Add(into, "style.zap", _random(-1f, 1f)); break;
                case SimEventType.StyleShatter: Add(into, "style.shatter"); break;
                case SimEventType.StyleSplit: Add(into, "style.split"); break;
                case SimEventType.ArtStage: StyleArtStage(e, into); break;
                case SimEventType.ChainLightningArc: Add(into, "chain.hop", e.A * _t.F("sound", "chain_hop_semitones")); break;
                case SimEventType.EnemyDamaged:
                {
                    float jitter = _t.F("sound", "hit_pitch_jitter_semitones");
                    if ((e.B & DamageFlags.Weak) != 0) Add(into, "hit.weakness", _t.F("sound", "weak_pitch_semitones"));
                    else if ((e.B & DamageFlags.Resist) != 0) Add(into, "hit.resistance", _t.F("sound", "resist_pitch_semitones"), _t.F("sound", "resist_volume_db"));
                    else if ((e.B & DamageFlags.Armored) != 0) Add(into, "art.piercing_lance.armor_break");
                    else Add(into, "hit.enemy", jitter > 0f ? _random(-jitter, jitter) : 0f);
                    break;
                }
                case SimEventType.AffinityDiscovered: Add(into, "hit.affinity_discovered"); break;
                case SimEventType.ProjectilePassed: Add(into, "projectile.passed"); break;
                case SimEventType.EnemyDashCancelled: Add(into, "art.recoil_shot.charge_broken"); break;

                case SimEventType.EnemySpawnNotice: Add(into, "enemy.spawn_notice"); break;
                case SimEventType.ProjectileSpawned:
                    if (e.B == 0 && e.Id != null)
                    {
                        KeyValuePair<int, string> prev;
                        if (!_rootBlock.TryGetValue(e.Slot, out prev) || prev.Key != e.Tick)
                            _rootBlock[e.Slot] = new KeyValuePair<int, string>(e.Tick, e.Id);
                    }
                    break;
                case SimEventType.EnemyStateChanged:
                    if ((EnemyAiState)e.A == EnemyAiState.WindUp)
                    {
                        // 적 종별 울음: 공격 직전(예고와 같은 때). 보스는 자기 포효(N-07)가 따로 있다.
                        var en = w != null ? w.FindEnemy(e.EntityId) : null;
                        string cry;
                        if (en != null && !en.IsBoss && en.DefId != null && _enemyWindup.TryGetValue(en.DefId, out cry)) Add(into, cry);
                    }
                    break;
                case SimEventType.DropPicked:
                    switch ((DropKind)e.A)
                    {
                        case DropKind.GoldCoin: Add(into, "drop.gold_collected"); break;
                        case DropKind.HealthShard: Add(into, "drop.hp_shard_picked"); break;
                        case DropKind.MaterialBundle: Add(into, "drop.material_bundle_picked"); break;
                    }
                    break;
                case SimEventType.WaveStarted: Add(into, "wave.started"); break;
                case SimEventType.WaveCleared: Add(into, "wave.cleared"); break;
                case SimEventType.EnemyKilled:
                {
                    if (e.B == 2) break; // 보스는 BossKilled가 낸다
                    var r = _kills.Kill(e.Tick);
                    if (r == KillBurstCounter.Result.Merged) Add(into, "enemy.multi_kill");
                    else if (r == KillBurstCounter.Result.Single)
                    {
                        // H-05 종마다 음색(6묶음). 묶음이 없는 종은 공용 처치음.
                        string g;
                        if (e.B == 1) Add(into, "elite.death");
                        else if (e.Id == null || !_killGroups.TryGetValue(e.Id, out g) || !Add(into, g)) Add(into, "enemy.death");
                    }
                    break;
                }
                case SimEventType.EnemyCc:
                    if ((CcKind)e.A == CcKind.Slow && _slowGate.TryPass(e.EntityId, e.Tick)) Add(into, "status.slow.applied");
                    break;
                case SimEventType.EnemyShotFired:
                {
                    var owner = w != null ? w.FindEnemy(e.A) : null;
                    Add(into, owner != null && owner.IsBoss ? "boss.bullet_fired" : "enemy.projectile_fire");
                    break;
                }
                case SimEventType.EnemyShotsCleared:
                    // 비기(방열·만개)의 탄 소거 소리. 보스가 죽어 탄이 지워질 때는 내지 않는다(그 틱의 BossKilled).
                    if (e.A > 0 && e.Tick != _bossKilledTick) Add(into, "art.vent_burst.bullets_cleared");
                    break;

                case SimEventType.TelegraphStarted:
                {
                    var z = FindZone(w, e.EntityId);
                    if (z != null && z.Harmless) break;
                    var owner = z != null && w != null ? w.FindEnemy(z.OwnerEnemyId) : null;
                    if (owner != null && owner.IsBoss) Add(into, "boss.telegraph_begin");
                    else { Add(into, "telegraph.start"); Add(into, "telegraph.fill"); }
                    break;
                }
                case SimEventType.TelegraphDetonated:
                {
                    var z = FindZone(w, e.EntityId);
                    if (z != null && z.Harmless) break;
                    var owner = z != null && w != null ? w.FindEnemy(z.OwnerEnemyId) : null;
                    Add(into, owner != null && owner.IsBoss ? "boss.telegraph_resolve" : "telegraph.detonate");
                    break;
                }

                case SimEventType.BossIntroBegin:
                    Add(into, w != null && w.Boss != null && w.Boss.FirstEncounter ? "phantom_first.intro" : "boss.intro_begin");
                    break;
                case SimEventType.BossPhaseChanged: Add(into, "boss.phase_changed"); break;
                case SimEventType.BossRageStack: Add(into, "boss.rage_stack_gained"); break;
                case SimEventType.BossFinalRage: Add(into, "boss.final_rage_begin"); break;
                case SimEventType.BossKilled: _bossKilledTick = e.Tick; Add(into, "boss.killed"); break;
                case SimEventType.PhantomCollapse: Add(into, "phantom_first.collapse_begin"); break;
                case SimEventType.PhantomMirrorCrack: Add(into, "phantom_first.mirror_crack"); break;
                case SimEventType.RevengeKill: Add(into, "revenge.mirror_shatter"); break;

                case SimEventType.TodChanged: Add(into, "tod.transition"); break;
                case SimEventType.StarfallLanded: Add(into, "starfall.landed"); break;
                case SimEventType.StarfallPicked: Add(into, "starfall.picked"); break;
            }
        }

        /// <summary>고유 비기 강화 단계(ArtStage) → 소리. 큰 순간(내리침·펼침·짓누름)과 단계마다의 작은 소리.</summary>
        void StyleArtStage(in SimEvent e, List<SoundCue> into)
        {
            var kind = (ArtStageKind)e.A;
            switch (e.Id)
            {
                case Ids.Art.Kiln:
                    if (kind != ArtStageKind.Rise) break;
                    if (e.B == 0) Add(into, "style.art.kiln.slam");
                    Add(into, "style.art.kiln.pillar", e.B * 2f);
                    break;
                case Ids.Art.BoltDash:
                    if (kind == ArtStageKind.Land) Add(into, "style.art.bolt_dash.land", PentatonicSemitones(e.B));
                    break;
                case Ids.Art.CopyCircle:
                    if (kind == ArtStageKind.Pulse) Add(into, e.B == 0 ? "style.art.copy.open" : "style.art.copy.pulse");
                    else if (kind == ArtStageKind.End) Add(into, "style.art.copy.end");
                    break;
                case Ids.Art.IceScreen:
                    if (kind != ArtStageKind.Rise) break;
                    if (e.B == 0) Add(into, "style.art.ice.slam");
                    Add(into, "style.art.ice.spike", e.B * 1.5f);
                    break;
                case Ids.Art.ShadowHand:
                    if (kind == ArtStageKind.Grip) Add(into, "style.art.hand.grip");
                    else if (kind == ArtStageKind.Crush) Add(into, "style.art.hand.crush");
                    break;
            }
        }

        /// <summary>이 발사의 맨 위 스코프 첫 탄 블록 id(없으면 null).</summary>
        string RootBlockOf(SimWorld w, in SimEvent e)
        {
            if (w != null)
            {
                ProjectileEntity best = null;
                for (int i = 0; i < w.Projectiles.Count; i++)
                {
                    var p = w.Projectiles[i];
                    if (p.OwnerSlot != e.Slot || p.ShotId != e.A || p.Source != ShotSource.Fire || p.ScopeDepth != 0 || p.Block == null) continue;
                    if (best == null || p.Id < best.Id) best = p;
                }
                if (best != null) return best.Block.Id;
            }
            KeyValuePair<int, string> kv;
            return _rootBlock.TryGetValue(e.Slot, out kv) && kv.Key == e.Tick ? kv.Value : null;
        }

        static ZoneEntity FindZone(SimWorld w, int id)
        {
            if (w == null) return null;
            for (int i = 0; i < w.Zones.Count; i++) if (w.Zones[i].Id == id) return w.Zones[i];
            return null;
        }

        public void Reset()
        {
            _graze.Reset();
            _kills.Reset();
            _slowGate.Reset();
            _bloomRepeats.Clear();
            _rootBlock.Clear();
            _bossKilledTick = int.MinValue;
        }
    }

    /// <summary>
    /// 판 중 배경음 id 규칙(곡 목록 미정 — id만 흘린다. G16.json music, D-72).
    /// 시간대 = tod_prefix + tod id("bgm.tod.day"), 보스 = "bgm.boss", 허상 첫 만남 = "bgm.phantom_first", 야영 = "bgm.camp", 연습 = "bgm.practice", 판 끝 = "bgm.run_end".
    /// </summary>
    public sealed class MusicRules
    {
        readonly EffectsTuning _t;
        public MusicRules(EffectsTuning t) { _t = t ?? new EffectsTuning(null); }

        public string ForTod(string todId)
        {
            if (todId == Ids.Tod.Boss) return Boss;
            return (_t.S("music", "tod_prefix") ?? string.Empty) + todId;
        }
        public string Boss => _t.S("music", "boss");
        public string PhantomFirst => _t.S("music", "phantom_first");
        public string Camp => _t.S("music", "camp");
        public string Practice => _t.S("music", "practice");
        public string RunEnd => _t.S("music", "run_end");

        /// <summary>지금 상태만 보고 고른 곡(층을 다시 세울 때 — 이벤트 없이). 판이 없으면 null(바꾸지 않음).</summary>
        public string ForState(SimWorld w)
        {
            if (w == null) return null;
            var ph = w.Run.Phase;
            switch (ph)
            {
                case RunPhase.NotStarted: return null;
                case RunPhase.RunEnded: return RunEnd;
                case RunPhase.Practice: return Practice;
                case RunPhase.Camp:
                case RunPhase.StartCamp:
                case RunPhase.LootPick:
                    return Camp;
            }
            if (w.Boss != null) return w.Boss.FirstEncounter ? PhantomFirst : Boss;
            int wave = w.Wave != null ? w.Wave.Wave : 0;
            return wave > 0 ? ForTod(DayCycle.Tod(wave)) : null;
        }

        /// <summary>이벤트 → 바꿀 곡(바꾸지 않으면 null, changed = false).</summary>
        public string ForEvent(SimWorld w, in SimEvent e, out bool changed)
        {
            changed = true;
            switch (e.Type)
            {
                case SimEventType.TodChanged:
                    if (w != null && w.Boss != null) break;
                    return ForTod(e.Id);
                case SimEventType.BossIntroBegin:
                    return w != null && w.Boss != null && w.Boss.FirstEncounter ? PhantomFirst : Boss;
                case SimEventType.CampEntered: return Camp;
                case SimEventType.WaveStarted:
                    // 야영에서 돌아온 같은 시간대 웨이브도 곡을 되돌린다. 보스 웨이브는 BossIntroBegin이 바꾼다.
                    if (e.A > 0 && !DayCycle.IsBossWave(e.A)) return ForTod(DayCycle.Tod(e.A));
                    break;
                case SimEventType.RunEnded: return RunEnd;
            }
            changed = false;
            return null;
        }
    }
}
