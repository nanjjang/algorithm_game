using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Data;
using Wizard.Solo.Meta;
using Wizard.Solo.Sim;
using Wizard.Solo.View;

namespace Wizard.Solo.Tests.Hud
{
    /// <summary>열쇠를 그대로 돌려주는 이름 표. Format은 "열쇠{이름=값,…}"(어느 열쇠에 어떤 값을 넣었는지 확인용).</summary>
    public sealed class HudFakeNames : INameTable
    {
        public string Get(string key) => key;
        public bool Has(string key) => true;
        public string Format(string key, params object[] nameValuePairs)
        {
            var sb = new StringBuilder(key);
            sb.Append('{');
            for (int i = 0; i + 1 < nameValuePairs.Length; i += 2)
            {
                if (i > 0) sb.Append(',');
                sb.Append(nameValuePairs[i]).Append('=').Append(System.Convert.ToString(nameValuePairs[i + 1], CultureInfo.InvariantCulture));
            }
            sb.Append('}');
            return sb.ToString();
        }
        public string Number(long value) => value.ToString("#,0", CultureInfo.InvariantCulture);
    }

    public sealed class HudFakeSound : ISoundPlayer
    {
        public readonly List<string> Played = new List<string>();
        public void Play(string eventId, SoundBus bus = SoundBus.Sfx, float volume = 1f, float pitch = 1f) => Played.Add(eventId);
        public void SetMusic(string trackId) { }
        public void SetVolume(SoundBus bus, float volume01) { }
    }

    /// <summary>GameObject를 만들어 빌려주고 돌려받으면 지우는 풀(그림 창고 없이).</summary>
    public sealed class HudFakePool : ISpritePool
    {
        public readonly List<GameObject> Created = new List<GameObject>();
        public int RentedSprites, RentedMeshes;

        public SpriteRenderer Rent(Sprite sprite, int sortingOrder, SpriteMaterialKind material = SpriteMaterialKind.Sprite)
        {
            var go = new GameObject("FakeSprite");
            Created.Add(go);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = sortingOrder;
            RentedSprites++;
            return sr;
        }

        public void Return(SpriteRenderer renderer) { if (renderer != null) { RentedSprites--; Object.DestroyImmediate(renderer.gameObject); } }

        public MeshRenderer RentMesh(int sortingOrder, SpriteMaterialKind material = SpriteMaterialKind.Shape)
        {
            var go = new GameObject("FakeMesh");
            Created.Add(go);
            go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sortingOrder = sortingOrder;
            RentedMeshes++;
            return mr;
        }

        public void Return(MeshRenderer renderer) { if (renderer != null) { RentedMeshes--; Object.DestroyImmediate(renderer.gameObject); } }

        public void DestroyAll()
        {
            for (int i = 0; i < Created.Count; i++) if (Created[i] != null) Object.DestroyImmediate(Created[i]);
            Created.Clear();
        }
    }

    /// <summary>미리보기·최근 입력을 정해 둔 비기 모듈.</summary>
    public sealed class HudFakeArts : ArtModule
    {
        public ArtPreview NextPreview;
        public readonly List<RecentInput> Recent = new List<RecentInput>();
        public override ArtPreview Preview(SimWorld w, PlayerState p, long nowU) => NextPreview;
        public override void GetRecentInputs(PlayerState p, List<RecentInput> into) => into.AddRange(Recent);
    }

    public static class HudTestWorlds
    {
        /// <summary>콘텐츠 없이 도는 세계(슬롯 0만 활동). 캐릭터 정의는 요약서 값으로 직접 채운다.</summary>
        public static SimWorld World(RunPhase phase, int wave, SimModuleSet mods = null)
        {
            var w = new SimWorld(null, mods ?? SimModuleSet.Stubs());
            w.Run.Phase = phase;
            w.Run.Wave = wave;
            w.Wave.Wave = wave;
            var p = w.P0;
            p.Active = true;
            p.CharId = Ids.Char.C1;
            // char.1: 체력 6칸, 결정 상한 5, 15마다 결정, 임계 시작 85(characters.json 사본 값은 Data 테스트가 지킨다)
            p.Def = new CharacterDef { Id = Ids.Char.C1, Element = Ids.Element.Fire, MaxHpCells = 6, CrystalCap = 5, ComboPerCrystal = 15, CritStart = 85f };
            p.MaxHp = 6 * SimConst.HpPerCell;
            p.Hp = p.MaxHp;
            p.Pos = new Vector2(12, 7);
            p.PrevPos = p.Pos;
            return w;
        }

        public static ProfileData Profile(bool phantomMet)
        {
            var pr = new ProfileData();
            pr.Flags.PhantomMet = phantomMet;
            return pr;
        }
    }
}
