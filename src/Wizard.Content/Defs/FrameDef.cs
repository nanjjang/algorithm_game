using System;

namespace Wizard.Content
{
    // CONTRACTS 2.2. spec 07 2.4(틀 5행), 7.2(고정 각본), 7.4(예산 배분 절차). 팩에서 읽은 뒤 바뀌지 않는다.
    // 원본 data/sim/frames.json(ContentDb.Frames, FrameId 오름차순)과 data/sim/waves.json 의 script(WaveCurveDef.ScriptPlans).
    // 고정 각본(웨이브 1~12, 13, 15)도 틀과 같은 꼴(고정 개수 → 주 묶음 → 나머지)이라 같은 타입으로 든다.
    // 종 배열은 전부 EnemyTypeId 오름차순(= spec 07 2.2 표 순서. 7.4 "가중치 추첨의 종 순서는 2.2 표 순서로 고정")이고 겹치지 않는다.
    // 예산 배분의 차례(spec 07 7.4): 엘리트 → 고정 개수(Fixed*) → 주 묶음(Group*) → 나머지(RestMixed) → 남은 예산은 WaveCurveDef.FillSpecies.
    public sealed class FrameDef
    {
        public FrameId Id;                                          // 각본의 표기 틀(1~12, 13, 15는 frame.mixed, spec 07 7.1)

        public EnemyTypeId[] GroupSpecies = Array.Empty<EnemyTypeId>();   // 주 묶음. 비었으면 주 묶음이 없다(혼합)
        public int GroupSharePct;                                   // 주 묶음 몫(예산의 %, 0~100). target = B × GroupSharePct ÷ 100 내림. 주 묶음이 없으면 0

        // 고정 개수(spec 07 2.4 "고정 개수", 7.2 각본). 종마다 [FixedMin, FixedMax] 마리(같으면 고정, 다르면 rng.wave_comp 로 고른다).
        // 무리로 나오는 종(해거름 박쥐, SpeciesDef.spawn_pack)은 한 마리가 아니라 무리 하나로 센다(spec 07 7.4 8번)
        public EnemyTypeId[] FixedSpecies = Array.Empty<EnemyTypeId>();
        public int[] FixedMin = Array.Empty<int>();
        public int[] FixedMax = Array.Empty<int>();

        // 주 묶음 뒤의 나머지 예산: true 면 혼합(주 묶음 밖의 가용 종 전체, spec 07 7.4 4번),
        // false 면 혼합을 건너뛰고 남은 예산 전부를 WaveCurveDef.FillSpecies 로 채운다(각본 웨이브 1~4의 "나머지 slime", spec 07 7.2).
        // 틀(frames.json)은 전부 true 다
        public bool RestMixed;
    }
}
