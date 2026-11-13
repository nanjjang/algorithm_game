using System.Collections.Generic;
using Wizard.ViewLogic.Assets.Json;
using Wizard.ViewLogic.Common;
using Wizard.ViewLogic.Presentation;

namespace Wizard.ViewLogic.Assets
{
    // 통합 에셋 레지스트리의 기록 하나(spec 14 5.7절): 파일 + 프레임 사각형 + 기준점 + 9조각 + 프레임 시간.
    // 다섯 매니페스트 형식의 차이는 로더가 여기로 모아서 지운다. 게임 코드는 매니페스트를 직접 보지 않는다.
    //   File        art/production 기준 경로("ui/kit/k-20_button_large_48x64_hover.png", "players/<폴더>/body.png")
    //   AtlasPage   UI 의 아틀라스 페이지(art/production 기준). 있으면 Frames 는 그 페이지 안의 좌표다(없으면 File 안)
    //   Frames      프레임별 사각형(가로 띠의 프레임 순서, 지팡이 시트는 행 우선)
    //   Pivot       기본 기준점(프레임 안 px). 없으면 null(효과는 왼쪽 위가 원점, MAN-fx notes.origin)
    //   FrameOrigins 프레임마다 기준점이 다른 묶음(몬스터 origin, 캐릭터 feet)의 프레임별 값
    public sealed class AssetRecord
    {
        private static readonly IReadOnlyList<PixelRect> NoFrames = new PixelRect[0];

        public AssetRecord(AssetKey key, string file, PixelSize frameSize, IReadOnlyList<PixelRect> frames)
        {
            Key = key;
            File = file;
            FrameSize = frameSize;
            Frames = frames ?? NoFrames;
        }

        public AssetKey Key { get; }

        public string File { get; }

        public PixelSize FrameSize { get; }

        public IReadOnlyList<PixelRect> Frames { get; }

        public int FrameCount => Frames.Count;

        public string? AtlasPage { get; internal set; }

        // 텍스처를 쥐는 파일: 아틀라스가 있으면 그 페이지, 없으면 낱장
        public string TextureFile => AtlasPage ?? File;

        public PixelPoint? Pivot { get; internal set; }

        public IReadOnlyList<PixelPoint>? FrameOrigins { get; internal set; }

        public NineSlice? NineSlice { get; internal set; }

        // 프레임 시간. UI 는 frame_ms(밀리초), 캐릭터·효과는 fps. 몬스터는 둘 다 없다(게임이 정한다, spec 14 5.5절). 0 = 없음
        public int FrameMs { get; internal set; }

        public int Fps { get; internal set; }

        // 반복 여부. 매니페스트가 정하지 않았으면 null
        public bool? Loop { get; internal set; }

        // 캐릭터 프레임의 붙는 점(spec 14 5.3절). 캐릭터 클립만
        public IReadOnlyList<CharacterFramePoints>? CharacterPoints { get; internal set; }

        // UI 아이콘의 속성(정식 키: neutral, fire, lightning, arcane, ice, dark — spec 14 3.1절 별칭을 거친 값)
        public string? Element { get; internal set; }

        // UI 블록 아이콘의 종류(projectile, control, modifier)
        public string? BlockKind { get; internal set; }

        // 매니페스트의 원래 항목(로더가 옮기지 않은 필드: tint, frame_offsets, hotspot, glyph_map …)
        public ManifestJson? Source { get; internal set; }

        // 없는 열쇠 대신 돌려준 대체 기록인가(spec 14 5.7절 "없는 그림의 대체")
        public bool IsPlaceholder { get; internal set; }

        // 대체 기록이 찍을 글자(없는 열쇠의 Id). 실제 기록은 null
        public string? PlaceholderLabel { get; internal set; }

        // 한 바퀴의 길이(밀리초). frame_ms 가 있을 때만(TV-187: 6 × 100 = 600)
        public int CycleMs => FrameMs * FrameCount;

        public PixelRect FrameRect(int frame) => Frames[frame];

        // 프레임의 기준점: 프레임별 값 → 기본값 → 왼쪽 위
        public PixelPoint OriginOf(int frame)
        {
            if (FrameOrigins != null && frame >= 0 && frame < FrameOrigins.Count)
            {
                return FrameOrigins[frame];
            }
            return Pivot ?? new PixelPoint(0, 0);
        }
    }
}
