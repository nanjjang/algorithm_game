using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Sim;

namespace Wizard.Solo.View.Effects
{
    /// <summary>
    /// 소리 연결 층(AddAudio, 00 문서 7절, D-72). 전투 이벤트 → 소리 사건 id → ctx.Game.Sound.Play(id, 묶음, 음량, 음높이),
    /// 판 중 배경음 전환(SetMusic): 시간대 바뀜·웨이브 시작·보스 등장·야영 진입·판 끝. 섞는 규칙은 재생기(F4)가 맡는다.
    /// Build는 지금 상태만 보고 곡을 고른다(보조 판에서 돌아올 때 — 지난 일회 소리는 다시 내지 않는다).
    /// </summary>
    public sealed class AudioLayer : IWorldViewLayer
    {
        readonly List<SoundCue> _cues = new List<SoundCue>();
        PresentationData _data;
        SoundEventMap _map;
        MusicRules _music;
        ISoundPlayer _sound;
        string _currentMusic;

        public AudioLayer() { }

        /// <summary>테스트: 데이터와 난수를 직접 준다.</summary>
        public AudioLayer(PresentationData data, System.Func<float, float, float> random = null)
        {
            _data = data;
            if (data != null) _map = new SoundEventMap(data.Bindings, data.Tuning, random);
        }

        public SoundEventMap Map => _map;
        public string CurrentMusic => _currentMusic;

        public void Build(ViewContext vc, SimWorld w)
        {
            _sound = vc != null && vc.Game != null ? vc.Game.Sound : null;
            if (_data == null) _data = PresentationData.Load(vc != null && vc.Game != null ? vc.Game.Content : (w != null ? w.Content : null));
            if (_map == null) _map = new SoundEventMap(_data.Bindings, _data.Tuning);
            else _map.Reset();
            _music = new MusicRules(_data.Tuning);
            UpdateSoundContext(w);
            string track = _music.ForState(w);
            if (track != null) SetMusic(track);
        }

        public void OnEvent(SimWorld w, in SimEvent e)
        {
            if (_map == null) return;
            _cues.Clear();
            // 캐릭터 전용 소리는 그 캐릭터일 때만(Audio.json). 유리 지팡이의 판 안 하락도 따라가게 발사 때 단계를 다시 넣는다.
            if (e.Type == SimEventType.ShotFired) UpdateSoundContext(w);
            _map.Map(w, e, _cues);
            if (_sound != null)
                for (int i = 0; i < _cues.Count; i++) _sound.Play(_cues[i].Id, _cues[i].Bus, _cues[i].Volume, _cues[i].Pitch);
            if (_music == null) return;
            bool changed;
            string track = _music.ForEvent(w, e, out changed);
            if (changed) SetMusic(track);
        }

        void UpdateSoundContext(SimWorld w)
        {
            if (_sound == null || w == null) return;
            var p = w.Me;
            if (p == null) return;
            _sound.SetSoundContext(p.CharId, p.Wand != null ? p.Wand.EffectiveStage : 0);
        }

        void SetMusic(string track)
        {
            if (track == _currentMusic) return;
            _currentMusic = track;
            if (_sound != null) _sound.SetMusic(track);
        }

        // ---- 기본 공격 유형의 반복음(불꽃 방사·눈보라·번개 구체 — G16 sound.style_loops): 그 블록 탄이 살아 있는 동안 슬롯마다 하나.
        Dictionary<string, string> _loopEvents;
        readonly Dictionary<string, float> _loopSeen = new Dictionary<string, float>();
        readonly List<string> _loopStop = new List<string>();
        float _loopClock;

        public void Render(SimWorld w, float alpha, float dt)
        {
            if (w == null || _data == null) return;
            if (_loopEvents == null) _loopEvents = _data.Tuning.StringMap("sound", "style_loops") ?? new Dictionary<string, string>();
            if (_loopEvents.Count == 0) return;
            _loopClock += Mathf.Max(dt, 1e-4f);
            for (int i = 0; i < w.Projectiles.Count; i++)
            {
                var p = w.Projectiles[i];
                if (p.Dead || p.Block == null) continue;
                string ev;
                if (!_loopEvents.TryGetValue(p.Block.Id, out ev)) continue;
                string key = ev + "#" + p.OwnerSlot;
                if (!_loopSeen.ContainsKey(key) && _sound != null) _sound.StartLoop(key, ev, SoundBus.Sfx);
                _loopSeen[key] = _loopClock;
            }
            float grace = _data.Tuning.F("sound", "style_loop_grace_seconds");
            if (grace <= 0f) grace = 0.15f;
            _loopStop.Clear();
            foreach (var kv in _loopSeen) if (_loopClock - kv.Value > grace) _loopStop.Add(kv.Key);
            for (int i = 0; i < _loopStop.Count; i++)
            {
                if (_sound != null) _sound.StopLoop(_loopStop[i]);
                _loopSeen.Remove(_loopStop[i]);
            }
        }

        /// <summary>테스트: 지금 도는 유형 반복음 열쇠 수.</summary>
        public int StyleLoopCount => _loopSeen.Count;

        public void Dispose()
        {
            if (_sound != null) foreach (var kv in _loopSeen) _sound.StopLoop(kv.Key);
            _loopSeen.Clear();
            _cues.Clear();
            _sound = null;
        }
    }
}
