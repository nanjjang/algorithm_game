# 게임플레이 모듈 구현자 최종 보고 (G01~G25)

각 모듈은 Unity를 띄우지 않고 자기 폴더에서만 구현했다. 통합 때 "계약 변경 필요"와 "다른 모듈에 기대한 가정"을 먼저 본다.

## G01 — 플레이어: 입력 적용, 이동·시야·자세, 피격·무적, 과열, 스침, 콤보, 결정

G01(플레이어 모듈) 결과 보고

## 만든 파일
- `D:\algorithm_game\solo\Assets\Scripts\Sim\Player\PlayerSystem.cs` (판 시작 SetupPlayer, RecompileSpell, 11단계) + `.Input.cs`(2·3단계, 자세 상태 기계, 대각 유지 창) / `.Move.cs`(4단계, MoveByArt) / `.Heat.cs`(1·6단계, 냉각, EnterLock/BreakLock/ConsumeHeatForVent/ReduceHeat, RingState) / `.Hit.cs`(QueueHit, Stage10, ForceDown, GrantInvuln, Heal, Rise, ResetForWaveEnd) / `.Graze.cs`(9단계, AwardGraze) / `.Combo.cs`(콤보, 결정, 피격 손실)
- `...\Sim\Player\PlayerRules.cs`(순수 함수: 입력 분류, 축 해석, SnapCardinal, CritShotCount, Ring), `PlayerTuning.cs`(수치 모음), `SimModuleFactory.Player.cs`(CreatePlayer 등록)
- `D:\algorithm_game\solo\Assets\Resources\DataSolo\G01.json` (열 링 15·95, 빚 +1칸, 스침 묶음 기억 10초[솔로 임시])
- 테스트 `D:\algorithm_game\solo\Assets\Tests\EditMode\Player\`: `PlayerTestKit.cs`(가짜 Spells/Arts/Traits/Threats/Run), `PlayerMoveTests.cs`, `PlayerInputTests.cs`, `PlayerHeatTests.cs`, `PlayerHitTests.cs`, `PlayerGrazeTests.cs`, `PlayerComboCrystalTests.cs`
- `.meta`는 만들지 않았다(Unity가 처음 들일 때 만든다).

## 확인
- `bash Tools/compile.sh -f Assets/Scripts/Sim/Player/,Assets/Tests/EditMode/Player/` → **끝 코드 0**, 필터 안 오류 0. (그때 남의 파일 3개 `Assets/Scripts/View/Effects/{SoundEventMap,ViewRegistry.Effects,AudioLayer}.cs`가 깨져 런타임 단위는 reduced였다.)
- Unity 테스트 러너로는 돌리지 않았다. 대신 compile.sh가 만든 dll을 Unity 밖 반사 실행기(스크래치패드의 임시 dotnet 콘솔, `SOLO_ASSETS_DIR` 지정)로 돌려 `Wizard.Solo.Tests.Player` **222건 통과, 실패 0**. Unity 러너에서의 통과는 확인하지 못함.

## 옮긴 벡터
T-M14·M15 + 4.5절 속도표, T-F16(+1,229u/1,230u 경계, 창 만료), T-F19(자세 진입·반전·해제의 초기화 1회씩), 4.4절 표 전 행·상대 방향 표, T-P01·P02·P05, T-H01~H09, H10·H11, H12~H15, H17·H18, H19~H24(가짜 컴파일 결과로 TryFire만), 6.6절 임계 횟수(4/6/3)·링, S04 15.5-71 불가마, T-G01~G19, 10 1절 콤보 규칙 1~8, 2절 결정 함수(외상 예 포함), 5.3절 불변식, 계약 벡터 전부(QueueHit 반환값, AttackId 작은 것, HasPendingHit이면 스침 0, HitThisTick 꺼짐, ForceDown, RollUnstableCore 발사당 1회, AttackAllowed 거짓 단계, 열 고정·결정 무한).
- T-H10은 명세의 틱 값(69.6/73.8/71.8/65.6)에 ±1%로 단언했다. 정책의 "다시 쏘는 틱" 세부가 명세에 없어 내 정책 구현으로는 기준값이 약 69.2(연속식 값)로 나온다. 명세 스스로 연속식과 ±1% 안이라고 적은 범위다.

## 계약 변경 필요 (Core는 고치지 않고 우회함)
1. `PlayerState`에 플레이어 모듈용 scratch 자리가 없다 → `public object PlayerScratch;` 추가 요청.
2. `PlayerModule.CanAfford(PlayerState p, int cost, bool allowCredit)`와 `RingState(PlayerState p)`에 `SimWorld`가 없어 연습 옵션(결정 무한)과 G01.json 수치를 읽을 길이 없다 → `CanAfford(SimWorld w, PlayerState p, int cost, bool allowCredit)`, `RingState(SimWorld w, PlayerState p)` 요청.
- 우회: `PlayerSystem` 안의 `static readonly ConditionalWeakTable<PlayerState, Extra>`로 PlayerState 객체마다 덧상태(소속 세계 참조, 버퍼)를 매달았다. 열쇠가 PlayerState 객체라 세계끼리 섞이지 않지만, "정적 가변 필드 금지" 규칙의 글자에는 걸린다. 위 두 변경이 들어오면 걷어낼 수 있다. 이 덕에 `ctx.Rules.Player`처럼 세계의 것이 아닌 모듈 객체로 불러도 값이 같다(테스트 있음).

## 다른 모듈에 기대한 가정
- **Traits**: `PlayerDerived`가 최종값이다 — `LockTicks`(과민 회로 +90 포함), `CoolHuPerTick`(탐식 절반 포함), `CritStartHu`(min 반영), `HasCredit`/`MaxDebt`/`KeepCrystal`/`MirrorImage`. Player는 다시 더하지 않는다. `UnstableCoreWarn`/`UnstableCoreExploded` 이벤트는 Traits가 낸다(Player는 자폭 때 잠금·콤보 0·`Traits.OnPlayerFired`만). 끓어넘침의 자폭 제외(D-04)는 `OnHeatLockEntered(fromUnstableCore)`로 Traits가 가른다. `OnHeatLockEntered`는 새로 잠길 때만 부른다.
- **Spells**: 임계 판정이 Execute보다 먼저라 발사 열은 Player가 `Spell.HeatPerShotHu × (MirrorImage ? mirror_image_heat_mul : 1)`로 직접 셈하고 `ExecuteResult.HeatToAddHu`는 쓰지 않는다. 거울상 조건이 Spells와 같아야 한다.
- **Arts**: `CommandReset`·`CommandDirInput`·`PlayerMovedByArt` 이벤트는 Arts가 낸다(Player는 `OnCommandReset` 등 호출만). `UnlockedArts`는 `Arts.SetupPlayer`가 채운다. `KilnAbsorbed` 이벤트는 Player가 낸다(Arts가 또 내면 두 번). 자폭 발사에는 `Arts.OnPlayerFired`를 부르지 않는다(탄이 없음). `ResetForWaveEnd`는 계약 주석대로 `KilnAbsorbLeftHu = 0`을 쓴다.
- **Threats**: `CollectGraze`의 `SurfaceDistance`·`ThreatId`를 채운다. 구역 후보는 터지는 틱에만 준다. `EnvSpeedMul`은 매 틱 다시 정한다.
- **Run**: `SetupPlayer` 전에 `w.Run.Config`·연습 옵션을 채운다. `p.Kills`는 Player가 세지 않는다.
- **Bosses/세션**: `InputLocked`가 켜지면 Player는 눌린 키를 전부 뗀 것으로 두고 자세를 푼다(초기화 StanceExit 1회). 풀린 뒤에는 새 누름부터 받는다 — 세션이 Resync로 다시 보내지 않으면 누르고 있던 키는 다시 눌러야 한다.

## 알릴 것 / 구현하지 못한 것
- 구현하지 못한 계약 메서드는 없다.
- 원문 알고리즘 그대로라 생기는 동작: 발사 중이 아닐 때 스치면 열이 임계 시작으로 오른 뒤 **같은 틱 끝에 냉각 1회(0.75)가 먹어**, 쉬다가 스친 직후의 면제 발사는 임계가 아닐 수 있다(원문 `HeatMachine.Cool`도 같다). T-G02 테스트는 "방금 쏜 상태"로 세워 통과시켰다. 스침이 냉각 대기를 다시 채워야 하는지는 사용자 판단이 필요하다.
- 솔로 임시로 정한 것: 스침 묶음(CastId) 기억은 그 시전의 위협이 마지막으로 보인 뒤 600틱에 지운다(G01.json). 한 틱 입력 상한 8개는 큐에 들어온 차례로 자른다. Reverse → Stance 복귀 때 `StanceEntered(A = Stance)`를 낸다. 열 고정 중에는 스침 올림·방열·탐식으로도 열이 변하지 않는다. `ForceDown`은 결정 손실과 Traits·Run 피격 훅을 부르지 않는다.
- 열 끝 100은 단위 눈금으로 보아 `PlayerTuning.HeatScaleMax` 상수로 두었다(JSON에 자리가 없음).

## G02 — 주문: 컴파일러, 실행, 투사체, 피해 공식, 상태이상 적용, 약점·저항 알아냄

G02(주문 모듈) 결과 보고

## 만든 파일
- D:\algorithm_game\solo\Assets\Scripts\Sim\Spells\SimModuleFactory.Spells.cs — `CreateSpells` 등록
- D:\algorithm_game\solo\Assets\Scripts\Sim\Spells\SpellSystem.cs — `SpellSystem : SpellModule`(Compile, Execute, StandardT, ComputeStats, ClearProjectiles)
- D:\algorithm_game\solo\Assets\Scripts\Sim\Spells\SpellSystem.Projectiles.cs — Stage9_Projectiles(상태 기계, 블록별 동작, 선분 쓸기, 벽·장애물, 유도, 즉발 연쇄, 폭발, 트리거)
- D:\algorithm_game\solo\Assets\Scripts\Sim\Spells\SpellSystem.Damage.cs — 피해 한 함수(00 5.4 (나)), DealDirect, 상태 적용, 약점·저항 알아냄
- D:\algorithm_game\solo\Assets\Scripts\Sim\Spells\SpellSystem.Debug.cs — DebugDump(10 9절 형식, 정본 id)
- D:\algorithm_game\solo\Assets\Scripts\Sim\Spells\SpellCompiler.cs — 순수 컴파일러(03 4.3절)
- D:\algorithm_game\solo\Assets\Scripts\Sim\Spells\SpellTuning.cs — 수치 묶음(const.json, elements.json, 블록 params, art.recoil, mutations.json, G02.json)
- D:\algorithm_game\solo\Assets\Scripts\Sim\Spells\SpellLoadoutText.cs — 10 9절 입력 문법 읽기/쓰기(시험용)
- D:\algorithm_game\solo\Assets\Resources\DataSolo\G02.json — `crit_multiplier` 2, `heat_max` 100
- 테스트: D:\algorithm_game\solo\Assets\Tests\EditMode\Spells\ 의 SpellTestKit.cs, SpellCompilerTests.cs, SpellProjectileTests.cs, SpellDamageTests.cs, SpellStatsTests.cs, SpellContractTests.cs
- `.meta`는 만들지 않았다(Unity가 처음 열 때 만든다).

## 확인 결과
- `bash Tools/compile.sh -f Assets/Scripts/Sim/Spells/,Assets/Tests/EditMode/Spells/` → **끝 코드 0** (마지막 실행. 단위 상태는 Assets/Scripts=reduced, Tests/EditMode=reduced — 빠진 파일은 전부 남의 것: View/Effects 3개, Tests/EditMode/Hud 1개. 그 전 두 번은 남의 파일 때문에 끝 코드 3이었다).
- Unity 테스트 러너로는 돌리지 않았다. 대신 Unity 밖에서 기반 폴더 + Spells만 따로 컴파일해 반사 실행기(.NET 9, 진짜 nunit.framework + 실제 JSON)로 돌렸다: **142개 통과, 실패 0**(일부러 넣은 실패 테스트 2개가 잡히는 것도 확인 후 뺐다). Unity EditMode 실행은 통합 단계에서 확인이 필요하다.

## 옮긴 벡터
- 03 15절: C01~C18 전부(+ C04, C07, C10, C13, C20, C21), G02, G05, G09, G12 (+ G01, G03, G04, G06, G07, G10, G11, G13, G15, G16)
- 5절 열(S02 16.4): 4 / 8.8 / 11.2 / 22 / 43.6 / 26.8 / 갈피 17·8.0·22·22 / 거울상 6, 밑줄 표 9개
- 8절 수(16.5): 12, 75→21, 60, 120→12, 예산 5/6, TV-E07(10발·보전 2.1·피해 21)
- 9절 피해(16.8): 10 / 61.528 / 20 / 30 / 30 / 40 / 마력 방출 20 / 12.5·8 / 잠식 10·11.5·11.5와 만료 / 직접 피해 ×1.15 / 34·28.9 / 36.8·33.856 / 13 / 12.1 / 16 / 50.284
- TV-G01(43틱째 소멸), TV-I01·I02·I12(가짜 Enemies가 SlowEntry를 적음), TV-A02, 10 9절 디버그 출력 예(글자 일치)
- 계약 벡터: 같은 틱 둘째 탄이 죽어 가는 적을 지나감(EnemyDamaged 1번, KillingHit 유지, 관통 횟수 그대로), DealDirect의 PendingDeath·땅속 Immune, AffinityDiscovered 첫 적중 1번·둘째부터 없음·이미 아는 것은 없음

## 계약 변경 필요(고치지 않고 우회함)
1. `ProjectileEntity`에 발사 순간 스냅숏 자리가 없다 → `ConditionalWeakTable<ProjectileEntity, ShotContext>`로 붙였다. 요청: `public float BaseMul;`(강화 × 변이 × 비기 × 캐릭터), `public int CritBonus;`
2. `SpellModule.DebugDump(CompiledSpell)`에 콘텐츠가 없어 `count=n/64`의 상한을 알 수 없다 → 이 모듈이 마지막으로 읽은 콘텐츠 값을 쓴다(아무것도 컴파일 안 했으면 `?`). 요청: `CompiledSpell`에 `public int TreeShotCap;` 또는 `DebugDump(IContentDb db, CompiledSpell spell)`
3. `SimEventType` 줄 주석 보탬 요청: `ProjectileSpawned`의 `A = 슬롯, B = 스코프 깊이, P = 생긴 자리`(HUD 회로·트리거 표시용으로 채워 넣었다), `Explosion`·`ChainLightningArc`의 `E = 탄 id`, `EnemyDamaged`·`ProjectileExpired`·`ProjectileBounced`의 `S = 쏜 슬롯`
4. `SpellScope`에 변형 슬롯 목록이 없다(트리 미리보기 G21이 필요할 수 있다). 요청: `public readonly List<int> ModifierSlots`. 지금은 `SlotBlockIds`와 `TriggerSlot`으로 되짚을 수 있다.

## 다른 모듈에 기대한 가정
- Player(G01): `ExecuteRequest.DamageMul`·`CharacterMul`을 1 이상으로 채운다(0 이하는 1로 본다). 불안정 핵 자폭은 Player가 Execute를 부르기 전에 거른다(D-05). `IsCrit`은 Source가 Fire일 때만 임계 배율이 된다(비기·사본은 무시).
- Traits(G04): `RollSpreadDegrees`는 `Derived.TwistedBarrel && !NoSpread`일 때만 루트 탄마다 한 번 부른다(상한에 잘려 안 나가는 탄 몫도 굴린다). `ExplosionRadiusMul`은 터지는 순간 부른다. `Derived.EnhanceMul`·`MutationDamageMul`이 0이면 1로 본다.
- Enemies(G06): `ModifyIncomingDamage`가 0 이하를 주면 그 적중은 Immune(훅·이벤트·트리거 없음, 탄은 소모). 보스 감속 절반·땅속 무시는 `ApplySlow` 안에서. 얼음 송곳 감속은 `SlowSlot.IceSpike`, 서리는 `SlowSlot.Frost`, 출처 `CcSource.BlockSlow`. 반동은 `ApplyCc(Knockback, art.recoil knockback, Recoil, 탄 방향)` 뒤 `TryCancelDash(stagger 틱)`.
- Threats(G05): `SegmentHitsObstacle`이 준 장애물을 원(Pos, Radius)으로 보고 진입점을 셈한다. 튕김이 남은 탄은 장애물에서도 튕긴다.
- Run(G10): `MaxHp ≤ 0`인 표적(허수아비)은 죽지 않고 Hp만 음수로 내려간다. `HpFloor`가 있으면 그 밑으로 안 내려가고 죽지 않는다.
- 내 탄의 적 충돌·폭발 피해는 `w.CombatActive`일 때만 돈다(그 밖에는 적을 지나가고 벽·사거리만 처리). `DealDirect`는 이 문을 보지 않는다(부르는 쪽이 판단).
- 죽은 적에게는 상태(감속·잠식·넉백)를 걸지 않는다. 훅(`Player.OnPlayerHitEnemy` → `Traits.OnEnemyHit`)은 부른다. 트리거 발동은 피해 함수 바로 뒤(이벤트 순서만 5.4 (나)의 7·8번과 다르다. 규칙 영향 없음).

## 명세가 비어 있어 내가 고른 해석(사용자 확인 필요)
- 허수아비 DPS(M8): 열 0에서 J를 누르고만 있을 때의 1주기 DPS(발사 수 × 간격 + 잠금 틱). 실행되는 스코프의 탄이 전부 한 번씩 맞는다고 본다(속성·캐릭터 배율 제외). 검산 44.93 / 47.83 / 48.44 / 41.89 / 43.48 일치.
- 조립 수치의 "발사당 열"·임계 사격 횟수·투사체 수는 거울상이면 실제 J 발사 값(열 ×1.5, 수 ×2)으로 낸다.
- 부메랑은 적을 지나가며 다리마다 적 하나당 1회(그림자 구체처럼). 벽·사거리 끝에서 돌아오고, 돌아오는 중 장애물에 막히면 사라진다.
- 하위 탄이 "자기를 만든 적을 맞히지 않는다"는 직접 충돌(과 연쇄의 첫 대상)에만 적용했다. 그 탄의 폭발 범위·연쇄 전이에는 그 적이 들어간다.
- 반동 넉백(`HitEffect`)은 루트 탄에만 붙는다(하위 스코프 탄에는 없음).
- 폭발 판정 거리 = 적 중심 거리 ≤ 반경 + 적 반지름, 연쇄 전이 = 중심 거리 ≤ 3, 벽 충돌은 탄 중심 기준.
- 컴파일 오류는 처음 것 하나만 낸다(칸 수 → 코어 빔 → 코어 비투사체). 모르는 블록 id의 연결 칸은 빈 칸으로 본다. `ctx.DepthDecay ≤ 0`이면 const.json 기본 감쇠.

## 구현하지 못했거나 확인하지 못한 것
- Unity 러너의 EditMode 실행(위 "컴파일 + Unity 밖 실행"만).
- 덤불 늑대 회피 무시(TV-G08)는 Enemies 쪽 동작이라 주문 모듈 테스트로 옮기지 못했다(연쇄 번개가 생긴 틱에 즉발 판정하는 것까지만 확인).
- 결투·허상 복사(TV-F04~07, F16~25, H07, H13, H24, I10·I11, E08·E09)는 솔로 제외 또는 Bosses·Threats 몫이라 옮기지 않았다.
- 참고: 스크래치패드의 `runner/` 폴더(다른 작업 것)에 실수로 `Program.cs`·`runner.csproj`·`bin`·`obj`를 만들었다가 내 것만 `g02runner/`로 옮기고 `bin`·`obj`를 지웠다. 그 폴더에 원래 `obj`가 있었다면 같이 지워졌을 수 있다(`Runner.cs`, `Runner.exe`, dll들은 그대로).

## G03 — 비기: 트라이, 커맨드 판정기, 결제, 비기 15종, 플레이어 지대, 해금 차례

G03(비기) 최종 보고

## 만든 파일
구현 (`D:\algorithm_game\solo\Assets\Scripts\Sim\Arts\`)
- `CommandTrie.cs` — ParserTimings, TrieNode, CommandTrie(아호-코라식 생성, BFS 번호, 실패 링크, Next, EdgeGap 2.4절 순서 그대로)
- `CommandParser.cs` — 순수 판정기(long u), ArtDecision, IArtWallet, Preview, 보관 K, 최근 입력 5개, LateText(둘째 자리 올림)
- `ArtSystem.cs` — `ArtSystem : ArtModule` 본체(SetupPlayer, OnCommandReset, OnDirectionPress, OnSkillPress, Stage5_Arts, Preview, GetRecentInputs, BuildOffer, Unlock, DescribeTrie)
- `ArtSystem.Effects.cs` — 공용 10종 + 벼락 질주·그림자 손
- `ArtSystem.Zones.cs` — 불가마·사본 마법진·얼음 병풍(AbsorbKilnHeat, OnPlayerFired, SegmentHitsScreen, OnEnemyTouchedScreen, ClearZones)
- `SimModuleFactory.Arts.cs` — `CreateArts` 등록

테스트 (`D:\algorithm_game\solo\Assets\Tests\EditMode\Arts\`)
- `ArtTestKit.cs`(가짜 Player·Spells·Enemies·Traits·Threats·Bosses), `TrieTests.cs`, `ParserVectorATests.cs`(A군 57개), `ParserRuleBTests.cs`, `UnlockTests.cs`, `ArtEffectTests.cs`

`Assets/Resources/DataSolo/G03.json`은 만들지 않았다. 필요한 수치가 전부 arts.json, tries.json, camp.json(`ArtCandidates`), const.json(`ArtShotCap`)에 있었다. `.meta`는 만들지 않았다(Unity가 임포트 때 만든다).

## compile.sh 결과
- `bash Tools/compile.sh -f Assets/Scripts/Sim/Arts/,Assets/Tests/EditMode/Arts/` → **끝 코드 0**, 필터 안 오류 0개. `Assets/Scripts` 단위는 `reduced`였다(남의 깨진 파일 3개를 빼고 dll 생성).
- Unity 테스트 러너로는 돌리지 않았다. 대신 compile.sh가 만든 dll을 Unity 밖 반사 실행기(스크래치패드의 net9 콘솔)로 돌려 `Wizard.Solo.Tests.Arts` **164개 전부 통과**. 기대값 하나를 일부러 틀리게 해 실패가 잡히는지 확인한 뒤 되돌렸다. 이 실행기는 테스트마다 새 객체를 만든다(Arts 테스트 클래스에는 필드 상태가 없다).

## 계약 변경 필요 (Core는 고치지 않고 자기 폴더에서 우회)
1. 연습 화면의 "K가 x초 늦음"과 늦은 양(u)을 계약으로 읽을 길이 없다. 우회: `public static bool ArtSystem.TryGetLastDecision(PlayerState p, out ArtDecision decision)` (`JustLateByU`, `LateByU`, `LateIndex`, `Reason`). 요청: `ArtModule`에 `virtual bool TryGetLastResult(PlayerState p, out ArtLastResult r)` 같은 자리, 또는 `ArtFired`에 섬 초과량 필드.
2. `ArtTrieNodeView.State`가 하나뿐이라 FF 노드의 섬 기술(뇌광창)이 잠김·후보인지 실을 수 없다. 지금은 K 기술(꿰뚫는 창) 기준 상태만 준다. 요청: `public ArtNodeState JustState;` 추가.
3. `RunState.BloomCasts`를 올릴 훅이 없어 Arts가 만개 발동 때 `w.Run.BloomCasts++`를 직접 한다(주인은 Run). 요청: `RunModule.OnArtFired(SimWorld w, PlayerState p, string artId)` 또는 5.2절 표에 "BloomCasts = Arts" 예외 명시.

## 다른 모듈에 기대한 가정
- **G01 Player**
  - 자세 진입·해제·반전 때 `OnCommandReset`을 부르고 그 `CommandReset` 이벤트는 Player가 낸다. Arts는 발동·불발(Fired/Fizzled)의 `CommandReset`과 `CommandDirInput`만 낸다.
  - K 시점에 `p.Stance`가 이미 반영돼 있고, 자세 중 `p.Facing`은 잠긴 4방향이다.
  - `CanAfford / SpendCrystals / SpendAllCrystals`가 10 요약서 2.2절대로 돈다(결정 무한 옵션 포함). 만개는 `CanAfford(p, min_cost, false)` 뒤 `SpendAllCrystals`로 치른다.
  - `MoveByArt`가 벽·적·장애물 멈춤을 처리한다. `ConsumeHeatForVent`는 0~100을 돌려준다.
- **G02 Spells**
  - 비기·사본의 `ExecuteRequest.ShotId`는 **음수**(Arts 자체 셈)다. `p.NextShotId`를 건드리지 않으려는 선택이므로, ShotId를 배열 첨자로 쓰면 안 된다.
  - `ProjectileBudget`을 지키고 `ExecuteResult.Spawned`를 채운다(160 예산을 이것으로 깎는다).
  - 반동 사격의 넉백·돌진 취소는 `HitEffect.RecoilKnockback`으로 Spells가 건다.
  - `DealDirect`가 Targetable·속성·콤보 제외를 처리한다.
- **G06 Enemies**
  - `ApplyCc`의 amount는 기본값으로 넘긴다(기절 60틱, 경직 36·30·90틱, 당김 `min(3, 중심까지 거리)`칸). 꼬리표별 절반·면역은 Enemies가 한다.
  - `ApplySlow(…, SlowSlot.Kiln, 0.2, ticks: 1)`을 지대 안 적에게 매 틱 5단계에 다시 건다. 그 틱 9단계 이동에 먹어야 한다(UntilTick = w.Tick + ticks 가정).
  - 걷는 일반·엘리트가 병풍에 닿으면 `OnEnemyTouchedScreen(…, dash: false)`는 false(안 깨짐)이고, 못 지나가게 막는 것은 Enemies 몫이다. 중량·보스는 dash와 무관하게 true(부숨, 경직 없음).
- **G07·G08 Bosses**: 뇌광창은 보스에게 `ApplyCc(Stun)` 대신 `ActionHalt(18틱)`만 부른다.
- **G10 Run**
  - `ArtOffered` 이벤트, `PendingArtTurns`, `CurrentArtOffer`, `NewArtsThisRun`은 Run이 다룬다. `Unlock`은 `p.UnlockedArts`에 더하고 `ArtUnlocked`만 낸다.
  - `SetupPlayer`는 `w.Run.Config.UnlockedArts`(있으면)와 at_start 비기를 `p.UnlockedArts`에 더한다.
- **G17·G21 화면**: `GetRecentInputs`는 `into`를 **비우고** 채운다.

## 임시 해석으로 정한 것 (확인 필요)
- 공격이 허용되지 않는 틱의 5단계에서 보관된 K뿐 아니라 **아직 안 나간 효과(창·방열 준비, 만개 남은 파동)도 버린다**. 클리어 뒤 탄이 생기지 않게 하려는 것이다.
- 최근 입력 표시는 발동·불발 뒤에도 남고, 다음 방향 입력이 새로 시작한다. 0.3초 초과로 끊기면 앞 입력을 남긴 채 `Chained = false`, `AllowedGapU = 4608`.
- 만개 i번째 파동 틱 = 판정 틱 + 준비 15틱 + round(i × 7.2). brief의 "판정 틱 + round(i × 7.2)"에 8.1절의 준비 시간을 더한 해석이다.
- 준비 시간이 있는 공용 비기(창·방열·만개)는 효과가 나가는 틱의 위치·시야를 쓴다. 고유 비기는 시전 틱 값이다.
- 벼락 질주는 판정 틱 즉시 이동(S03 [임시])이다. 12틱에 걸친 이동은 넣지 않았다.
- 범위 판정: 원(방열·불가마·그림자 손)은 적 중심 거리, 선·직사각형(창·질주 길·병풍 파편)은 몸 반지름을 더한 겹침.
- 창은 벽에서만 끊고 장애물은 보지 않는다.

## 구현하지 못했거나 확인하지 못한 것
- Unity EditMode 러너로는 돌리지 않았다(위의 Unity 밖 실행으로만 확인).
- 실제 Player·Spells·Enemies 구현과 묶은 `SimWorld.Step` 통합 동작은 확인하지 못했다(가짜 모듈로만 검증).
- 벡터 옮김 범위
  - 옮긴 것: A군 57개 전부, S군 S01~S13(S07~S09 표는 원문 `docs/spec/03-command-parser.md` 5.6절에서), 2.6절 검사표, B16, B17~B22·B24~B26, T-F19, 02 8.3절 검산(불가마 흡수량, 사본 12회), brief의 계약 벡터.
  - Arts 인터페이스 수준으로만 옮긴 것: B02~B07(키 누름 유지, Shift·Space 처리)은 초기화 호출로 대신했다. 실제 키 입력은 G01 범위다.
  - 차례 줄 없이 옮긴 것: B22는 후보 규칙만 검사했다(차례 6개를 줄 세우는 것은 Run).
  - B23은 04 요약서 표에 없다(M-7).
- 작업 중 실수 하나: 공유 스크래치패드의 다른 에이전트 폴더 `scratchpad\runner\`에 `Program.cs`, `runner.csproj`, `bin`, `obj`를 잠깐 만들었다가 지웠다. 그 폴더에 원래 `Program.cs`가 있었다면 내가 덮어쓴 것이다(원래 `Runner.cs`, `Runner.exe`는 그대로다). 또 첫 컴파일 출력을 공용 이름 `scratchpad\out`에 한 번 썼다. 이후로는 `g03`, `g03out`, `g03run`만 썼다.

## G04 — 캐릭터 패시브 5종, 지팡이 특성 5종, 변이 6종, PlayerDerived

G04 결과 보고

## 만든 파일
- D:\algorithm_game\solo\Assets\Scripts\Sim\Traits\TraitSystem.cs — `Wizard.Solo.Sim.Traits.TraitSystem : TraitModule`
- D:\algorithm_game\solo\Assets\Scripts\Sim\Traits\TraitScratch.cs — `PlayerState.TraitScratch`에 두는 상태(불안정 핵 미리 굴림·예고 틱, 탐식 창, 유리 지팡이 웨이브당 횟수, 대상 임시 목록)
- D:\algorithm_game\solo\Assets\Scripts\Sim\Traits\SimModuleFactory.Traits.cs — `CreateTraits` 등록
- D:\algorithm_game\solo\Assets\Tests\EditMode\Traits\TraitFakes.cs, TraitDerivedTests.cs, TraitPassiveTests.cs, TraitMutationTests.cs
- `Assets/Resources/DataSolo/G04.json`은 만들지 않았다. 필요한 수치가 전부 characters.json, solo/const.json, solo/mutations.json, solo/enhance.json, tries.json에 있다.
- `.meta` 파일은 만들지 않았다(Unity가 열 때 만든다).

## 확인 결과
- `bash Tools/compile.sh -f Assets/Scripts/Sim/Traits/,Assets/Tests/EditMode/Traits/` 끝 코드 0. 필터 안 오류 0, EditMode 단위 ok. 그 시점에 `Assets/Scripts` 단위는 남의 깨진 파일 7개를 뺀 `reduced` 상태였다(그 전 여러 번은 남의 폴더 오류로 끝 코드 3).
- 따로 확인: 기반 폴더 + Traits만 복사한 스크래치 사본에서 compile.sh가 전 단위 ok, 끝 코드 0.
- 테스트는 Unity 테스트 러너로 돌리지 않았다. 대신 Unity 밖 반사 실행기(Unity 동봉 mono, `SOLO_ASSETS_DIR`로 실제 JSON 로드)로 `Wizard.Solo.Tests.Traits` 113건을 돌려 113건 통과했다. Unity EditMode 실행은 통합 단계 몫이다.

## 계약 변경 필요
없음. Core는 건드리지 않았다.

## 택한 방식 — 불안정 핵
- 판의 첫 발사분은 `RecomputeDerived`에서, 그 뒤는 `OnPlayerFired`에서 다음 발사분을 굴린다(`RngStream.Combat`, `RollPercent(5)`, 발사 1번에 난수 1번). 유리 지팡이 하락 뒤의 `RecomputeDerived`는 다시 굴리지 않는다.
- `RollUnstableCore`는 굴려 둔 결과만 돌려준다. 같은 틱에는 몇 번 불러도 같은 값이고 난수를 쓰지 않는다.
- 코어가 `explodes` 투사체일 때만 굴린다. 판정은 `p.Loadout.Core`가 기준이고, 비어 있으면 `p.Spell.Core`를 본다.
- 예고(`UnstableCoreWarn`): 간격 ≤ 18틱이면 굴린 그 틱에, 길면 "지금 + 간격 − 18" 틱의 `Stage11_TickEnd`에서 낸다.
- brief에 없는 덧붙임 세 가지(사용자 확인이 필요하면 빼기 쉽다):
  1. 자폭이 정해진 채 발사가 늦어지면 `AttackAllowed`인 동안 18틱마다 예고를 다시 낸다. 실제 자폭 앞 0.3초 안에 예고가 하나는 있게 하려는 것이다. 야영 등에서는 내지 않는다.
  2. `RollUnstableCore`가 돌려준 결과가 `OnPlayerFired` 없이 다음 틱으로 넘어가면 그 결과를 버리고 다음 물음에서 새로 굴린다. 자폭 결과가 남아 매 발사 터지는 일을 막는 방어다.
  3. 야영에서 코어를 폭발 블록으로 바꾼 직후처럼 굴려 둔 것이 없으면 `RollUnstableCore` 안에서 한 번 굴린다.

## 다른 모듈에 기대한 가정
- **Player(G01)**: `SetupPlayer`가 `p.Loadout`을 채운 뒤 `RecomputeDerived`를 부른다(현재 코드가 그렇다). 자폭 발사에서도 `OnPlayerFired`를 부른다(현재 그렇다).
- **Player(G01)**: `Derived.LockTicks`에 과민 회로 +90틱이 이미 들어 있으니 Player가 다시 더하면 안 된다. `Derived.CoolHuPerTick`도 탐식 절반이 반영된 값이다(45/50).
- **Player(G01)**: `Derived.MaxDebt`는 외상 특성(char.5)일 때만 2이고 나머지는 0이다.
- **Player(G01)**: `ReduceHeat`는 잠금 중이면 아무 일도 하지 않는다. 탐식은 잠금 중 처치를 창에도 넣지 않고, 실제 깎이는 양 `min(480, 남은 상한, 현재 열)`만 센다.
- **Spells(G02)**: `OnEnemyHit`은 폭발·관통 1회당 적마다 1번 넘어온다. `DealDirect`가 속성·잠식·훅을 처리한다. 패시브 피해 `Amount` = n × `StandardT` × `CharacterMul(p.Crystals)`, Origin `Passive`, Element는 캐릭터 속성.
- **Enemies(G06)**: 꼬리표별 변환은 `ApplyCc`가 한다. Traits는 항상 기본 값을 넘긴다 — 끓어넘침 `Knockback` 2칸 / `Overflow` / 플레이어→적 방향, 살얼음 `Freeze` 60틱 / `ThinIce` / dir 0.
- **Enemies(G06)**: `EnemyState.ThinIceCount`와 `FreezeImmuneUntilTick`은 Traits가 쓴다. 빙결을 청한 뒤 횟수 0, 내성 = max(기존, 지금 + 360). Enemies가 `FreezeImmuneUntilTick`으로 빙결을 거절한다면 `ApplyCc` 호출이 내성 설정보다 먼저라 문제없다.
- **Enemies(G06)**: `ThinIceFroze` 이벤트의 A는 `ApplyCc`가 돌려준 값(틱)의 반올림이다. 중량·보스처럼 감속으로 바뀌어 틱이 아닌 값이 오면 0이 된다.
- **대상 고르기**: 끓어넘침·피뢰침은 `Targetable`이고 `Ai != Spawning`인 적만 고른다(05 4절 "Spawning은 피해를 주고받지 않음"). `EnemyState.Ai`의 기본값이 `Spawning`이라, 허수아비 등을 직접 만드는 쪽이 `Ai`를 `Act`로 두지 않으면 패시브 대상에서 빠진다.
- **Run(G10)**: 웨이브 시작마다 `Traits.OnWaveStart`를 부른다(유리 지팡이 웨이브당 1회 초기화. 현재 코드가 그렇다).
- **킬·적중 훅**: `HitInfo.AttackerSlot`이 유효한 활동 중 플레이어 슬롯이어야 탐식·살얼음이 돈다. 살얼음은 Origin `Environment`만 빼고 전부 센다.
- `ExplosionRadiusMul(p)`은 세계 인자가 없어 `RecomputeDerived`가 `TraitScratch`에 적어 둔 값을 읽는다. `RecomputeDerived` 없이 `p.Derived`만 채우면 1이 나온다.

## 구현하지 못했거나 확인하지 못한 것
- Unity EditMode 러너 실행은 하지 않았다.
- 살얼음 검산의 "주기 6.9초": 규칙대로면 9틱 간격 적중에서 내성이 끝나는 틱의 적중이 바로 세어져 주기가 405틱(6.75초)이다. 요약서의 6.9초는 6초 + 0.9초 어림값으로 보고, 테스트는 주기를 405~414틱 범위로 단언했다. 정확히 414틱이어야 한다면 명세 확인이 필요하다.
- 8.3절 밑줄 표는 brief대로 Spells 소관이라 옮기지 않았다. 끓어넘침·살얼음의 꼬리표별 결과값(엘리트 1칸, 0.5초 등)은 Enemies 소관이라 Traits 테스트는 넘기는 인자만 확인했다.
- 벡터 id가 요약서에 없는 항목은 테스트 이름에 명세 절 번호를 썼다(`S04_4_1__…`, `S01_7_6__…`, `S05_6__…`, 계약 벡터는 `Contract__…`).
- 스크래치의 컴파일용 사본(`…\scratchpad\mirror`, `out`, `runner`)이 남아 있다. Library 연결(junction)은 지웠다.

## G05 — 위협: 예고 구역, 동시 예고 상한·대기 줄, R5, 적 탄, 남는 구역, 장애물

G05(위협 모듈) 결과 보고

## 만든 파일
- D:\algorithm_game\solo\Assets\Scripts\Sim\Threats\SimModuleFactory.Threats.cs — `CreateThreats` 등록
- ...\Sim\Threats\ThreatSystem.cs — 본체(세계별 속 상태, `DamageCells`, `ClearAll`, `IsClear`)
- ...\Sim\Threats\ThreatSystem.Telegraphs.cs — 표·대기 줄(`Stage8_Queue`), 구역 수명·판정, `CheckEscape`
- ...\Sim\Threats\ThreatSystem.Shots.cs — `SpawnShot`, `Stage9_Shots`, `ClearShots`, `CollectGraze`
- ...\Sim\Threats\ThreatSystem.Hazards.cs — `Stage9_Zones`, 남는 구역, 장애물
- ...\Sim\Threats\EscapeSolver.cs — R5(줄마다 ulong 비트셋, 48×28)
- ...\Sim\Threats\ThreatGeometry.cs, ThreatConfig.cs, ThreatWorldData.cs, ThreatSpecs.cs(AttackDef → ZoneSpec)
- D:\algorithm_game\solo\Assets\Resources\DataSolo\G05.json — 굶음 120틱, 표 보관 600틱, 번쩍 2틱, 만료 경고 1초, R5 상수
- D:\algorithm_game\solo\Assets\Tests\EditMode\Threats\ — ThreatTestKit.cs, ThreatEscapeTests.cs, ThreatTelegraphTests.cs, ThreatDamageTests.cs, ThreatShotTests.cs, ThreatHazardTests.cs
- .meta 파일은 만들지 않았다(Unity가 임포트 때 만든다).

## compile.sh 결과
- `bash Tools/compile.sh -f Assets/Scripts/Sim/Threats/,Assets/Tests/EditMode/Threats/` 끝 코드 0. 마지막 실행은 오류 0개, 네 단위 전부 ok.
- 테스트는 Unity 러너로 돌리지 않았다. compile.sh가 만든 dll을 Unity 밖 반사 실행기(스크래치패드, SetUp 호출, `SOLO_ASSETS_DIR` 지정)로 돌려 `Wizard.Solo.Tests.Threats` 145개 중 145개 통과. Unity EditMode 실행은 통합 단계에서 확인해야 한다.

## 옮긴 벡터
- TV-R5-01~28 전부(단독 14종 중 enemies.json에 있는 공격은 AttackDef에서 읽고, 엘리트 공격은 표의 수치).
- TV-TEL-01~09, TV-CAP-01~09(06은 뺌 — 아래), TV-SPC-36/37, TV-CON-05.
- 피해 식: w 30/31/60/61, 엘리트·보스, 최종 격노, 상한 3, Dash = Telegraph, Boss null/Dead, Scripted.
- T-G12~G19, 탄 150 상한, 병풍에 막힌 탄, 포물선.
- 계약 벡터: 같은 틱이 s, 판정 뒤 600틱 뒤 Unknown, 상한 0, 마지막 숨 12틱, 무적 중 쿨다운 미소비, 맞는 순간의 최종 격노.

## 계약 변경 필요 (Core는 고치지 않았다)
1. 남는 구역을 지울 방법이 없다. 가마등의 불씨 소화(05 9.1절)에 필요하다. 제안: `public virtual int RemoveHazards(SimWorld w, HazardKind kind, Vector2 lineFrom, Vector2 lineTo, float margin)`. 그 전에는 Bosses가 `HazardEntity.Dead = true`를 직접 써도 이 구현은 견딘다.
2. `HazardSpec.MaxCount` 주석은 "같은 주인·종류"인데 명세(05 6.10절, S5 TV-SPC-37)는 "전장 동시 3개"다. 종류만으로 전장 전체를 세게 구현했다. 주석을 고쳐야 한다.
3. `SimEventType.TelegraphFlash` 주석은 `E`뿐인데 `A = 번쩍 틱 수(2)`를 실었다. 이벤트는 s+N−2에 한 번만 낸다(작업 지시). 원문은 s+N−2와 s+N−1 두 틱이라, 표시 층이 A만큼 그려야 한다.
4. `ZoneSpec.DamageSource`의 열거형 기본값이 Contact다. 구역에 Contact는 뜻이 없어 Telegraph로 바꿔 센다. 기본값을 Telegraph로 두는 편이 낫다.
5. `SegmentHitsObstacle(forPlayerShot: false)`는 `BlocksEnemyShots`인 장애물만 본다. 이동용으로 쓰려면 따로 인자가 필요하다.

## 다른 모듈에 기대한 가정
- **Player**: `QueueHit`이 true일 때만 탄을 없애고 남는 구역 쿨다운을 쓴다. false(무적)면 탄은 지나간다. `CollectGraze`는 `hit_radius < SurfaceDistance ≤ graze_radius`로 이미 걸러 주고, `GrazedCasts`와 무적은 Player가 본다.
- **Enemies/Bosses**:
  - 구역 명세에 `DamageSource = Telegraph`와 `BaseDamage`를 넣는다. `BaseDamage ≤ 0`인 구역·탄은 피해도 스침도 없다(고슴도치 조준선이 그렇다. R5와 상한에는 든다).
  - `Length ≤ 0`은 벽까지, 탄의 `Range ≤ 0`도 벽까지다.
  - `ZoneSpec.TargetSlot`(기본 0)의 플레이어가 서 있으면 그 사람, 아니면 가장 가까운 사람을 겨눈다.
  - `Anchor = Owner`인데 주인이 없으면 `Pos`를 절대 좌표로 본다.
  - 판정 틱에 주인이 스스로 죽어도(꽈리) 구역은 터진다. 취소는 `CancelTelegraphsOf`와 `CancelTicket`만 한다.
  - `CancelTicket`은 표를 지운다(이후 Unknown). `CancelTelegraphsOf(0)`은 아무것도 안 한다.
- **Arts**: `SegmentHitsScreen(w, 앞 위치, 다음 위치, out id)`를 직선 탄마다 매 틱 부른다. 포물선은 묻지 않는다.
- **Waves**: 마지막 숨의 원은 `OwnerEnemyId = 0`, `StaggerIfBlockedTicks = 12`로 청한다. 8단계 뒤에 청하면 다음 틱 8단계에 시작한다. 느린 탄막 배율은 포물선에 적용하지 않는다(비행 시간이 예고에 묶여 있다).
- **남는 구역**:
  - `SlowPercent`는 0.3과 30 둘 다 받고, 0이면 const.json의 30%를 쓴다.
  - `BaseDamage`가 0이고 SlowOnly가 아니면 접촉 1칸, `ContactCooldownTicks`가 0이면 90틱이다.
  - 구역이 남기는 구역은 `HazardDef.Radius > 0`이면 그 원, 아니면 구역과 같은 모양이다.
  - 새로 생긴 남는 구역은 다음 틱부터 닿는다.
- `EnemyShotEntity.Vel`의 단위는 칸/초다.

## 구현하지 못했거나 확인하지 못한 것
- Unity EditMode 러너 실행은 하지 않았다.
- **TV-CAP-06**(박쥐 접촉은 상한과 무관)은 접촉이 Enemies 소관이라 옮기지 않았다.
- **TV-TEL-10**(엘리트는 빙결에도 예고 유지)은 취소를 부르지 않는 쪽이 Enemies라 옮기지 않았다.
- **TV-TEL-11**(차오름 소리 2개 고르기)은 표시 층 일이라 옮기지 않았다.
- R5는 명세대로 "시작 칸만 막힘 예외"다. 그래서 플레이어가 가시밭·포자 안개 한가운데(이웃 칸이 전부 막힘)에 서 있으면 그 자리를 덮는 새 예고가 계속 R5에 걸린다. 일반 적은 굶음 규칙으로 줄이 멎고, 보스는 R5Blocked가 된다. 명세의 귀결이지만 QA에서 볼 만하다.
- 서리밭을 남기는 구역이 터진 뒤의 느림은 R5에 넣지 않았다(이미 깔린 서리밭만 본다).
- 탄이 한 틱 안에 병풍과 플레이어를 둘 다 지나는 경우 병풍을 먼저 본다(병풍 앞에 선 플레이어가 그 틱에는 안 맞을 수 있다. 0.27칸 이내의 일이다).
- R5의 구역 줄임은 요약서의 "0.10칸" 대신 원문 S5 3.5절의 정수 꼴 25/256칸을 썼다(TV-R5가 그 값으로 검산됐다).

## G06 — 적 AI: 일반 12종 + 졸개 + 엘리트 12종 + 허수아비, 이동·접촉·군중 제어·사망

G06 (적 AI) 결과 보고

## 만든 파일
- D:\algorithm_game\solo\Assets\Scripts\Sim\Enemies\SimModuleFactory.Enemies.cs — `CreateEnemies` 등록
- D:\algorithm_game\solo\Assets\Scripts\Sim\Enemies\EnemySystem.cs — Spawn, Stage7_Ai, 공통 상태 기계(표 요청·허가·판정 틱), 탄 쏘기(느린 탄막), 지연 유폭
- D:\algorithm_game\solo\Assets\Scripts\Sim\Enemies\EnemySystem.Species.cs — 12종 + 졸개 + 허수아비 두뇌, 엘리트 능력 12종
- D:\algorithm_game\solo\Assets\Scripts\Sim\Enemies\EnemySystem.Move.cs — Stage9_Move(이동, 병풍·장애물, 돌진 몸 충돌, 멧돼지 경로, 접촉)
- D:\algorithm_game\solo\Assets\Scripts\Sim\Enemies\EnemySystem.Cc.cs — ModifyIncomingDamage, ApplyCc, ApplySlow, ApplyCorrosion, TryCancelDash, Stage10_Deaths, RemoveAll
- D:\algorithm_game\solo\Assets\Scripts\Sim\Enemies\EnemyRules.cs, EnemyBrain.cs, EnemyTuning.cs
- D:\algorithm_game\solo\Assets\Resources\DataSolo\G06.json
- D:\algorithm_game\solo\Assets\Tests\EditMode\Enemies\EnemyTestKit.cs, EnemyRulesTests.cs, EnemyContractTests.cs, EnemySpeciesTests.cs, EnemyEliteTests.cs
- .meta 파일은 만들지 않았다(Unity가 임포트 때 만든다).

## 확인
- `bash D:/algorithm_game/solo/Tools/compile.sh -f Assets/Scripts/Sim/Enemies/,Assets/Tests/EditMode/Enemies/` 끝 코드 0(마지막 실행 기준. 남의 깨진 파일 4개가 빠진 reduced 상태였지만 필터 안 진단은 없음).
- Unity 러너로는 돌리지 않았다. 대신 Unity 밖에서 Unity 동봉 mono + 반사 실행기(스크래치패드 `g06run/`)로 `Wizard.Solo.Tests.Enemies` 205건을 돌려 205건 통과. 이 실행기는 테스트마다 새 객체를 만들고 [SetUp]은 없다(필드 상태를 쓰는 테스트는 없음).
- 임시 탐침으로 진짜 ThreatSystem(G05)과 붙여 봤다(탐침 파일은 지움): 늑대 돌진은 Dash 피격 1번만(구역 판정 피격 없음), 선돌지기 Telegraph 2칸, 고슴도치 가시 Projectile, 가시덩굴 가시밭 생성, 꽈리 점화 뒤 사망, 멧돼지 벽 기절, 상한 2에서 나머지 Waiting — 기대대로.

## 계약 변경 필요
없음. 아래는 계약 안에서 정한 것이라 알아 둘 것:
1. 꽈리의 지연 유폭(사망 24틱 뒤)을 둘 세계 상태 자리가 없어, 모듈 객체 안의 `ConditionalWeakTable<SimWorld, …>`에 세계별로 둔다(정적 아님). 세계 상태로 올리려면 `SimWorld`에 적 모듈용 scratch 필드가 필요하다(요청은 아님).
2. `EnemyState.PendingDeath/KillingHit/Hp`의 주인은 Spells인데, 꽈리 점화 자폭과 멧돼지 돌진에 걸린 꽈리는 Enemies가 직접 세운다(`KillingHit.AttackerSlot = -1`, Origin Environment).
3. 소환 수 세기 `EnemySystem.CountSummonedBy(w, summonerId)`는 구현 클래스에만 있다. Bosses가 쓰려면 `EnemyModule`에 `public virtual int CountSummonedBy(SimWorld w, int summonerId)` 추가가 필요하다.

## 다른 모듈에 기대한 가정
- Threats: `BaseDamage == 0`인 구역은 판정 때 QueueHit을 부르지 않는다(지금 G05 구현과 일치). 돌진(늑대·멧돼지·급강하)과 조준선 구역을 피해 0·Grazeable false·Harmless false로 청하고, 피해는 몸 충돌(Source Dash)과 탄이 낸다. G05의 `ThreatSpecs.FromAttack`은 쓰지 않았다(돌진 구역에 피해를 싣기 때문).
- Threats: `SpawnShot`이 `EnemyShotSpeedMul`을 곱하고 `EnemyShotFired`를 낸다(Enemies는 원래 속도를 넘기고 이벤트를 내지 않는다). `GetZones`의 첫 구역 `Pos/Dir`로 고정 방향·중심을 읽는다(없으면 자체 계산).
- Spells: `DealDirect`가 `AttackerSlot = -1`, `Origin = Environment`(유폭, 멧돼지 경로 피해, IgnoresArmor true)를 받는다(G02 구현은 범위 검사 있음).
- Arts: `OnEnemyTouchedScreen`이 병풍 파괴 여부를 돌려주고 경직을 `Enemies.ApplyCc(Stagger, IceScreen)`으로 건다. 걷는 적은 false면 멈추고 true면 지나간다(중량이 부수는지는 Arts가 답한다). 돌진은 중량·보스만 계속 달리고 나머지는 끝난다. 엘리트 45틱은 ApplyCc의 절반 규칙으로 나온다.
- Bosses: 보스 몸통은 `IsBoss`이고 Brain이 EnemyBrain이 아니다(7·9단계에서 건너뛴다. 사망은 같은 10단계 길). `ApplyCc(Stun, ThunderSpear)`가 보스면 `Bosses.ActionHalt(18틱)`을 부른다 — Arts가 ActionHalt를 따로도 부르면 두 번 불린다.
- Run: 허수아비는 `Spawn(DefId = Ids.Enemy.Dummy)`로 만든다. 죽지 않게 MaxHp = G06.json 값, HpFloor = 1, 매 틱 체력을 가득 채운다. 수치판은 Hp 차이가 아니라 EnemyDamaged나 DamageResult로 세야 한다.
- Waves/Bosses: 보스가 부른 졸개는 `SummonerId = 보스 몸통 Id`로 넘기면 꼬리표·상성을 지운다.
- 7·9단계는 `w.CombatActive`일 때만 돈다(10단계 사망 처리와 지연 유폭은 항상).

## 솔로 임시 값 (G06.json, 사용자 확인 대기)
- 허수아비 탄: 간격 2.0초, 속도 6.5, 반지름 0.12, 피해 1(practice.json에 값 없음). 허수아비 표시용 체력 1,000,000.
- 박쥐 여왕 급강하 시작 거리 5칸(원문에 발동 거리 없음, 띠 길이와 같게).
- ApplyCc(Slow)의 기본 시간 1.0초(인자에 시간이 없음), 목표 자리 도착 여유 0.05칸.
- 명세 값을 옮긴 것: 중량 빙결 변환 감속 0.3, 느린 탄막 최소 간격 1.0칸.

## 해석해서 정한 것
- 느린 탄막: 부채는 이웃 각을 그대로 두고 수만 늘린다(3 → 4발은 ±10·±30도, 가운데 탄 없음). 고리는 8방향을 유지하고 넘치는 4발을 앞의 4방향으로 ceil(1.0 ÷ (속도 × 0.8))틱 뒤에 이어 쏜다(도깨비불 17틱).
- 꽈리 점화 자폭은 구역이 터진 다음 틱(판정 틱 + 1)에 죽는다(같은 틱에 죽이면 구역 판정 전에 주인이 죽은 것으로 보일 수 있어서).
- 재사용 대기는 타격·발사 틱 기준(가시덩굴만 허가 틱 기준). 멧돼지는 기절 중 대기가 끝나 풀린 틱에 바로 다시 준비한다.
- 빙결·기절의 예고 취소는 꼬리표 없는 일반 적과 뿌리만(비행·엘리트·중량 제외). 경직은 예고를 취소하지 않고 달리던 돌진만 끊는다.
- 도깨비불 깜박의 "Gone(맞지 않음)"은 순간 이동으로만 구현했다(사라지기·나타나기 18틱씩은 맞을 수 있다).
- 꽃 핀 가시덩굴의 둘째 씨앗은 요청 틱의 플레이어 위치로 고정한다(대기 줄에서 기다리면 첫째와 기준 시점이 어긋날 수 있다).
- 대기 줄의 늑대는 추적을 계속하고 발동 거리를 벗어나면 요청을 버린다.
- EnemyKilled 이벤트의 A(골드) = 비용 × drops.json gold_per_cost, 보스·NoReward는 0.

## 구현하지 못했거나 확인하지 못한 것
- Unity EditMode 러너 실행, PlayMode는 하지 않았다.
- R2 런타임 상한(min(speed, V−ε))은 넣지 않았다. 버프 상한 3.7만 데이터로 지킨다.
- 얼음 병풍 행의 실제 파괴·경직 수치는 Arts 소관이라 가짜 Arts로 호출만 확인했다.
- 05 13.3절 미정 2번(울부짖음 빈도)은 Q-24대로 매 돌진 묶음 앞.

## 스크래치패드 사고
스크래치패드가 다른 작업과 공유돼 있는 줄 모르고 처음에 `scratchpad/out`에 compile.sh 산출물을 썼고, `scratchpad/runner/`에 Runner.cs·Runner.exe를 만들었다가 지웠다. `runner/`는 다른 작업의 실행기 폴더였다(내가 봤을 때 csproj 없이 bin이 비어 있었다). 같은 이름 파일을 덮어썼는지는 확인하지 못했다. 그 뒤로는 `g06out/`, `g06run/`만 썼다.

## G07 — 보스 공통 뼈대(싸움 상태 시계의 주인) + 가마등 + 스승의 허상(첫 만남·복수)

G07 결과 보고 (보스 공통 뼈대 + 가마등 + 스승의 허상)

## 만든 파일 (전부 D:\algorithm_game\solo\Assets 아래, .meta는 만들지 않음 — Unity가 만든다)
- Scripts/Sim/Bosses/Common/: BossSystem.cs(등장·Dismiss·피해 조정·ActionHalt), BossSystem.Ai.cs(7단계 시계·격노·패턴 고르기·접촉), BossSystem.Runner.cs(데이터 구동 패턴 실행기·돌진), BossSystem.Phases.cs(10단계: 단계 전환·사망·첫 만남·BeginCollapse), BossRules.cs(순수 함수), BossPatternData.cs(overrides·second_cycle 반영), BossContext.cs, IBossBrain.cs(+BossBrainBase, DefaultBossBrain), BossBrainRegistry.cs(partial 등록 자리 6개), BossTuning.cs, SimModuleFactory.Bosses.cs(CreateBosses), README.md(G08용 서명·등록·틱 규칙·예)
- Scripts/Sim/Bosses/KilnBoar/: KilnBoarBrain.cs, BossBrainRegistry.KilnBoar.cs
- Scripts/Sim/Bosses/MirrorPhantom/: MirrorPhantomBrain.cs, PhantomCopyRules.cs, BossBrainRegistry.MirrorPhantom.cs
- Resources/DataSolo/G07.json (bosses.json에 없는 수치)
- Tests/EditMode/Bosses/Common/: BossTestFakes.cs(FakeThreats/FakePlayer/FakeEnemies/BossWorld), BossRulesTests.cs, PhantomCopyRulesTests.cs, BossSystemTests.cs, PhantomEncounterTests.cs

## 검사 결과
- `bash Tools/compile.sh -f Assets/Scripts/Sim/Bosses/Common/,Assets/Scripts/Sim/Bosses/KilnBoar/,Assets/Scripts/Sim/Bosses/MirrorPhantom/,Assets/Tests/EditMode/Bosses/Common/` → 끝 코드 0 (마지막 실행: 오류 0, 네 단위 전부 ok).
- Unity 러너로는 돌리지 않았다(Unity.exe 금지). 대신 compile.sh가 만든 dll을 Unity 밖의 임시 반사 실행기(스크래치패드의 dotnet 콘솔, SOLO_ASSETS_DIR 사용)로 돌렸다: 내 네임스페이스(Wizard.Solo.Tests.Bosses.Common) 91개 통과, 실패 0. 이 실행기는 테스트마다 새 객체를 만들고 [SetUp]을 부르지 않는다(내 테스트 클래스에는 필드·SetUp이 없다). **Unity EditMode 러너에서의 통과는 확인하지 못함.**

## 계약 변경 필요 (지금은 자기 폴더에서 우회)
1. `ThreatModule`에 남의 개체를 걷는 메서드가 없다 → BossContext.ExpireHazard / BreakObstacle이 `HazardEntity.Dead`/`ObstacleEntity.Dead = true`를 직접 쓰고 HazardExpired / ObstacleDestroyed(A = 1)를 직접 낸다(불씨 소화, 돌진이 깨는 바위). 요청: `public virtual void RemoveHazard(SimWorld w, HazardEntity h)`, `public virtual void DestroyObstacle(SimWorld w, ObstacleEntity o, bool broken)`.
2. 돌진 예고 띠의 "판정 없음"을 나타낼 필드가 ZoneSpec에 없다 → `BaseDamage = 0, DamageSource = Dash, Grazeable = false`로 청한다. 요청: `ZoneSpec.NoJudge`(bool) 또는 "DamageSource == Dash인 구역은 Threats가 판정하지 않는다"를 계약에 명시.
3. `EnemySpawnSpec`에 상성 제거 표시가 없다(허상이 부르는 슬라임은 꼬리표·상성 없음) → 지금은 NoReward·SummonerId만 넘긴다. 요청: `EnemySpawnSpec.Minion`(bool: 약점·저항 neutral).
4. `EnemyShotSpec`/`EnemyShotEntity`에 스코프 깊이(탄 가운데 흰 점 1~3)와 "플레이어를 맞혔다" 통지가 없다 → 허상 복사 탄은 쏜 탄을 지켜보다 사라진 자리(플레이어 곁/벽/사거리 끝)로 폭발·부메랑 귀환·하위 스코프를 처리한다(어림). 요청: `EnemyShotSpec.ScopeDepth`(int), 그리고 탄 종료 사유(`EnemyShotEntity.EndReason`: HitPlayer/Wall/Range/Cleared/Blocked).
5. `SimEventType.BossPatternStarted`의 A·B를 쓴다(주석에는 E, Id만): 허상 따라 쓰기 A = 비기 번호(Ids.Art.All 순서, 1부터)·B = 예고 틱, 되비침 만개 연출은 Id = "reflection_bloom"·A = 1~3(판정 없음). 주석 보탬 또는 전용 이벤트(PhantomMimicWarn, PhantomReflectionBloom) 요청.

## 다른 모듈에 기대한 가정
- Run: BossIntro에 들어가는 틱에 SpawnBoss(plan)(plan.PhantomFirst는 첫 만남 웨이브에서만 참이면 된다 — 웨이브 10·허상일 때만 첫 만남으로 본다), w.Boss.State만 읽어 전이, 첫 만남에서 플레이어 체력 0이면 OnPlayerDowned → BeginCollapse, CollapseDone 뒤 Rise → Dismiss, 보스 웨이브 클리어 끝에 Dismiss. 복수 흠집은 w.Boss.HpMin(사망 시 0)과 BossRules.DentPercent로 계산 가능.
- Enemies: IsBoss 몸통의 AI·이동·접촉을 하지 않는다(접촉·돌진 충돌은 Bosses가 7단계에 QueueHit). Stage10_Deaths가 PendingDeath인 보스 몸통을 처리한다(안 해도 Bosses가 Dead로 만든다). ModifyIncomingDamage는 보스면 Bosses로 넘기고 EnemyState.DamageTakenMul을 따로 곱하지 않는다(나는 이 값을 쓰지 않고 Recovery 배율을 Bosses 쪽에서 곱한다). RemoveAll(minionsOnly: true)는 보스 몸통을 남긴다.
- 몸통 EnemyState.Def는 enemies.json에 없어 BossDef에서 만든 EnemyDef(Role "boss")를 넣었다(다른 모듈이 e.Def를 읽어도 null이 아니게).
- Threats: RequestTelegraph 뒤 같은 틱 8단계에 허가되면 다음 틱 TryGetTiming이 참, GetZones의 첫 구역 Dir이 고정된 방향, DamageCells가 최종 격노(w.Boss.FinalRage)를 반영, ClearShots(radius ≤ 0) = 전부, BaseDamage 0인 Dash 구역은 피해를 주지 않음, 돌진 중인 몸(EnemyState.Dash*)의 스침 후보를 Threats가 만든다. 허상 복사의 폭발 예고 원·직선 번개는 DamageSource = Projectile로 청한다(피해 1 고정, 웨이브 성장 없음).
- Spells: p.Spell(CompiledSpell)의 Scopes[n].Projectiles/SplitCount/FanHalfAngleDegrees/Mods, FireIntervalTicks, MaxDepthNormal이 채워져 있다. 허상 복사는 Spells.Execute를 쓰지 않고 트리를 직접 펴서 Threats.SpawnShot으로 쏜다(Execute에 진영 인자가 없다).
- Arts: 돌진이 병풍에 닿으면 OnEnemyTouchedScreen(dash: true)가 보스에게 경직 없이 병풍만 부순다.
- 표시(G15·G25): BossPatternStarted의 A·B, 몸통의 EnemySpawned(Id = 보스 id), AnimClip(가마등 walk/charge_warn/charge/stomp/roar/stun, 허상 hover/fire/cast/burst/phase)을 읽는다.

## 요약서와 다르게 처리했거나 확인하지 못한 것
- 복사 갈래 각: 03 요약서·brief는 ±19.19도(현 길이), 05 요약서·bosses.json은 split_min_angle_degrees 19.1(호 길이). 둘 중 큰 쪽(2·asin(1/6) = 19.19, G07.json의 r3_check_distance 3·r3_min_gap 1로 계산)을 쓴다 — 두 조건을 다 만족한다. 정본 값 확인 필요.
- 원문에 없어 G07.json에 넣은 솔로 임시: 보스 등장 자리(가로 가운데, 세로 1/4·3/4 중 플레이어에게서 먼 쪽), 쫓기 멈춤 0.1칸, 허상 거리 유지 허용 폭 0.25칸, 슬라임 졸개 고리 반경 1.5칸, 불씨 조각 1칸, 복사 탄 적중 여유 0.1칸, 패턴 끊기 한도 600틱. 사용자 확인 대기.
- 허상 복사의 하위 스코프(트리거)·폭발·부메랑 귀환은 위 4번의 어림으로 구현했다(탄이 사라진 자리 기준). 소거·병풍에 막힌 탄은 뒷일이 없다. 탄 가운데 흰 점(스코프 깊이)은 넘길 필드가 없어 표시로 전달하지 못함.
- 허상 2회차 변이 복사는 거울상(뒤로도 쏨)과 뒤틀린 총열(흔들림)만 옮겼다. 나머지 변이(불안정 핵, 유리 지팡이 등)는 적 탄에 뜻이 없어 옮기지 않음.
- 동작 멈춤(ActionHalt)은 이동·대기·빈틈·반복 간격·구역 없는 예고만 멈춘다. 이미 뜬 예고의 판정 틱은 Threats 소유라 멈추지 못한다.
- R5 통과는 실제 Threats.CheckEscape로 확인하지 못했다(가짜 Threats). R1은 BossRules.CheckR1로 가마등·허상 전 패턴 통과를 테스트로 확인.
- 가마등 "가마등 발구르기 54틱 통과/48틱 실패"(TV-R5)는 Threats 벡터라 옮기지 않았다.
- 스크래치패드에 임시 실행기와 출력이 남아 있다(프로젝트 밖): C:\Users\lovey\AppData\Local\Temp\claude\afd9546682f5c3c2\d0fd5536-302e-40ee-8ada-6ceaddd9e028\scratchpad\g07runner, g07out, g07run.sh

## G08 — 보스 넷: 천 갓, 밤눈, 흰뿔, 밤삼킴

G08 (보스 넷: 천 갓, 밤눈, 흰뿔, 밤삼킴) 결과 보고

## 만든 파일 (전부 소유 폴더 안)
- D:\algorithm_game\solo\Assets\Scripts\Sim\Bosses\SporeQueen\SporeQueenBrain.cs, BossBrainRegistry.SporeQueen.cs
- D:\algorithm_game\solo\Assets\Scripts\Sim\Bosses\StormOwl\StormOwlBrain.cs, BossBrainRegistry.StormOwl.cs
- D:\algorithm_game\solo\Assets\Scripts\Sim\Bosses\FrostGoat\FrostGoatBrain.cs, BossBrainRegistry.FrostGoat.cs
- D:\algorithm_game\solo\Assets\Scripts\Sim\Bosses\NightSerpent\NightSerpentBrain.cs, SerpentRules.cs(SerpentBodyDef, 경로 따라가기·부위·OnHitSerpent 배율·고리 기하), BossBrainRegistry.NightSerpent.cs
- D:\algorithm_game\solo\Assets\Resources\DataSolo\G08.json (bosses.json에 없는 보충 수치: 새싹 금 자리 2~4칸·간격 1.5·시도 32 [솔로 임시], 기둥 파편 피해 1, 뱀 경로 표본 0.1칸·부위 판정 여유 0.1칸 [솔로 임시])
- D:\algorithm_game\solo\Assets\Tests\EditMode\Bosses\Four\ : FourTestKit.cs(진짜 두뇌를 감싸 한 패턴만 고르게 하는 ForcedBrain), FourBossDataTests.cs, SporeQueenTests.cs, StormOwlTests.cs, FrostGoatTests.cs, NightSerpentTests.cs, FourBossEscapeTests.cs

등록: Common이 선언한 partial 넷(RegisterSporeQueen/StormOwl/FrostGoat/NightSerpent)을 각 폴더에서 구현. Core와 Bosses/Common은 고치지 않았다.

## 확인
- `bash D:/algorithm_game/solo/Tools/compile.sh -f Assets/Scripts/Sim/Bosses/SporeQueen/,Assets/Scripts/Sim/Bosses/StormOwl/,Assets/Scripts/Sim/Bosses/FrostGoat/,Assets/Scripts/Sim/Bosses/NightSerpent/,Assets/Tests/EditMode/Bosses/Four/` → 끝 코드 0 (오류 0, 네 단위 전부 ok).
- Unity 테스트 러너는 돌리지 않았다(Unity.exe 금지). 대신 compile.sh 산출 dll을 Unity 밖 반사 실행기(스크래치패드의 dotnet 콘솔, SOLO_ASSETS_DIR 지정)로 돌렸다: `Wizard.Solo.Tests.Bosses.Four` 139개(TestCase 펼친 수) 통과 0 실패, G07의 `Wizard.Solo.Tests.Bosses.Common` 91개도 통과(내 등록이 들어간 뒤에도 깨지지 않음). 반사 실행기는 테스트마다 새 객체를 만든다 — Unity NUnit 실행은 통합 단계에서 확인 필요.
- 옮긴 벡터: 보스 체력 4종(±1), R1 검산 11줄 + 십자 0.22(1.63) + 서리밭 위 휘두르기 0.11, 단계 진입 %, 예고·시전·빈틈 틱 전부, 졸개 상한 8, 구름 300틱·6개, 기둥 80·6개, 서리밭 480틱·3개·−30%, 마디 10·간격 1.0·배율 0.5·max 3, 숨결 120틱 8발/160틱 11발, 밭갈이 T178(0~60 / 108~168 / 빈틈 60), 고르기 규칙(S6 7.4), second_cycle 4보스 × 두 단계, 단계 덮어쓰기. 계약 벡터: 돌진 시작 틱 = 요청 틱 + 예고 틱(활강 54, 뿔 돌진 54, 무는 돌진 48, 가짜 Threats), 돌진 충돌 Source = Dash·AttackId = 몸통 Id, 새싹 상한이 SummonerId 기준(남의 새싹 8마리는 세지 않음), 마디 여럿이 같은 틱에 닿아도 QueueHit 1회·AttackId = 몸통 Id·쿨다운은 true일 때만, 대상 없으면 가만히.
- R5: 등록된 진짜 Threats(`SimModuleSet.CreateDefault().Threats`)의 CheckEscape로 네 보스의 모든 예고 step × 두 단계 × 플레이어 자리 5곳이 단독 통과. S5 TV-R5 "서리밭 칸 휘두르기 60틱 실패 / 72틱 통과"도 진짜 CheckEscape로 통과.

## 계약 변경 필요 (고치지 않고 두뇌 안에서 우회)
1. **밤삼킴 마디가 플레이어 탄에 맞지 않는다.** Spells의 탄 충돌은 `e.Pos`·`e.BodyRadius`(머리 원) 하나만 본다. 필요: Spells가 보스 몸통이면 `w.Boss.Segments`(마디 반지름 0.5)도 충돌 대상으로 보고, 맞은 자리와 탄 식별자를 넘길 것. 제안 시그니처: `BossModule.ModifyIncomingDamage(SimWorld w, EnemyState e, float amount, bool isCrit, Vector2 fromPos, Vector2 hitPos, int shotId)` (또는 `IBossBrain.ModifyIncomingDamage`에 `hitPos, shotId` 추가). 지금은 `fromPos`가 마디·꼬리 원(+0.1) 안이면 ×0.5, 아니면 머리 ×1.0으로 가린다. max_segment_hits(3)는 훅에 탄 식별자가 없어 셀 수 없다 — 몸이 EnemyState 하나라 탄 한 발이 한 번만 맞히므로 넘지는 않는다. 규칙의 정본은 `SerpentRules.HitMul(body, part, ref segHits)`(테스트됨).
2. **남는 구역을 옮기는 메서드가 없다.** 천 갓 2회차 안개 흐름(0.8칸/초)을 `HazardEntity.Pos`를 두뇌가 직접 써서 처리(G07의 ExpireHazard와 같은 우회). 제안: `ThreatModule.MoveHazard(SimWorld w, HazardEntity h, Vector2 pos)`.
3. **HazardSpec/HazardEntity에 InnerRadius가 없어 고리 모양 남는 구역을 못 만든다.** 또아리의 닫힌 고리(240틱, 접촉 피해 1)는 Threats 구역이 아니라 두뇌의 `TouchesPlayer`(고리 띠 안 4.0~바깥 5.0 포함)로 처리 → 뼈대의 접촉(Source = Contact, AttackId = 몸통 Id, 같은 적 쿨다운 1.5초)이 된다. 그래서 고리는 R5 풀이에 "지나갈 수 없는 칸"으로 들어가지 않는다(또아리 중에는 다른 예고 패턴이 없고 비늘 탄만 나가서 실해는 없음). 제안: `HazardSpec.InnerRadius` + `HazardEntity.InnerRadius`.
4. **십자 활강의 둘째 줄은 몸이 하나라 스침 후보가 없다.** 둘째 줄은 두뇌가 같은 속도·반지름의 판정점을 옮겨 `Player.QueueHit(Dash, AttackId = 몸통 Id)`만 올린다(플레이어당 한 번은 inst.DashHitMask 공유). Threats.CollectGraze는 `EnemyState.Dash*`만 읽으므로 둘째 줄 스침·표시 위치가 없다. 제안: 보스용 둘째 돌진 판정을 알릴 자리(예: `BossState`에 `List<Vector2> ExtraDashPoints`).
5. 비늘 탄 깜박임: 표시(G15)가 `e.WeaponTint && boss.CurrentPattern == "scale_volley"`로 그리는데 상시 행동은 CurrentPattern을 갖지 않는다 → 다른 패턴이 없을 때만 두뇌가 `BossState.CurrentPattern`을 "scale_volley"로 잠깐 빌리고 발사 틱에 null로 돌린다. 패턴 중(또아리 등)의 깜박임은 `BossPatternStarted(Id = "scale_volley", B = 36)` 이벤트로만 알 수 있다. 제안: `BossState.PassiveWarnUntilTick`(또는 상시 행동용 이벤트).

## 다른 모듈에 기대한 가정
- Threats: 구역의 `LeavesHazard`(포자 안개)는 Threats가 판정 틱에 깐다(주인 = 몸통 Id). 포자 안개 6개 상한은 두뇌가 구역 수를 줄여 지킨다(Threats의 MaxCount는 가장 오래된 것을 지우는 방식이라 "넘는 새 구름 생략"과 달라, 넘지 않게만 청한다). 서리밭은 구역의 LeavesHazard를 비우고 두뇌가 판정 틱 7단계에 `SpawnHazard(SlowOnly, SlowPercent 0.3 = 1 − hazard_speed_ratio, 480틱)`로 깐다(두 번 깔리지 않게). 길이 > 0인 띠는 그 길이 그대로 쓴다.
- Threats.DamageObstacle이 체력 0인 기둥을 `Dead = true`(Hp ≤ 0)로 치운다고 본다 — 두뇌가 다음 틱에 그것을 보고 파편을 쏜다(그냥 걷힌 기둥은 Hp > 0이라 쏘지 않음).
- Enemies.Spawn이 `Instant = true`면 등장 예고 없이 바로 만든다(새싹은 두뇌가 `EnemySpawnNotice`(A = 60)로 금 4곳을 패턴 시작 틱에 알린 뒤 60틱 뒤 그 자리에 부른다).
- 표시(G15): 밤눈 착지 빈틈에는 클립 "land"(monsters manifest에서는 `boss.storm_owl.ground` 항목)와 비행 꼬리표 없음으로 알린다. 흰뿔 숨결의 테두리·방향 화살표는 `BossPatternStarted(frost_breath, A = ±1 방향, B = 쓸 각)` + 예고 동안 `Body.Facing = 시작 변`으로 그릴 수 있다. BossState.Segments는 11개(마디 10 + 마지막이 꼬리).

## 요약서에 없어 정한 것 (솔로 임시, 근거는 주석)
- 천 갓: 뿌리 단계면 Init에서 전장 가운데로 옮겨 시작. 도약은 판정 틱에 그림자 자리로 순간 착지(공중 동안의 무적·비표적은 요약서에 없어 넣지 않음).
- 밤눈: 활강은 "화면 밖"이 아니라 띠가 들어오는 벽 안쪽(몸 반지름)에서 반대쪽 벽까지. 진입 방향 부호는 난수. 십자는 직전 십자의 두 축을 다 피한다.
- 흰뿔: 기둥 원 예고 4개는 그대로 띄우고 기둥만 6개 상한으로 생략. 제 기둥에 막히면 옆으로 돌아 걷는다. 숨결 간격은 숨결 시작 때의 격노로 고정.
- 밤삼킴: 또아리 중 머리는 고리 위를 이동 속도로 돌고 12부위를 고르게 배치. 고리 예고 동안 머리가 고리 자리로 다가간다. 무는 돌진은 길이 ÷ (16/60) 올림 틱(최대 0.27칸 넘음).
- frost_field 원(r2.5, 60틱)은 뼈대의 CheckR1에 "frost_field.field"로 걸린다(여유 0.045초). 피해 0이라 R1 대상이 아니고 9.7절 목록에도 없어 테스트에서 그 한 줄만 제외했다 — Common의 CheckR1이 damage 0을 건너뛰게 하면 깔끔하다(G07 참고).

## 구현하지 못했거나 확인하지 못한 것
- Unity EditMode 러너 실행(통합 단계 몫). 위 반사 실행기 결과만 있다.
- 마디의 탄 피격과 탄 한 발당 3마디 상한의 실제 동작(계약 1번이 풀려야 한다).
- 진짜 Enemies·Player·Spells·Run과 함께 도는 통합 플레이, 표시 층에서의 클립·깜박임 확인.

## 참고
- 스크래치패드는 다른 작업과 공유되고 있었다. 반사 실행기를 만들다 남의 `scratchpad/runner/runner.csproj`를 한 번 덮어썼다(내용은 net8.0 + RollForward Major로 원래 빌드 설정과 같고, 그 폴더의 bin은 건드리지 않았다). 내 실행기는 `scratchpad/g08runner`, 실행 스크립트는 `C:\Users\lovey\AppData\Local\Temp\claude\afd9546682f5c3c2\d0fd5536-302e-40ee-8ada-6ceaddd9e028\scratchpad\g08run.sh`.

## G09 — 웨이브 디렉터: 곡선, 구성 생성, 묶음 방출, 변덕, 지체 방지, 별똥, 날씨

G09 웨이브 디렉터 — 결과 보고

## 만든 파일
- D:\algorithm_game\solo\Assets\Scripts\Sim\Waves\SimModuleFactory.Waves.cs — `CreateWaves` 등록
- D:\algorithm_game\solo\Assets\Scripts\Sim\Waves\WaveDirector.cs — `WaveDirector : WaveModule` (BuildPlan, BeginWave, Stage7_Spawns, Stage11_TickEnd, IsCleared, OnEnemyKilled, EndWave, PickSpawnPoint, HpMul/Budget/TelegraphCap)
- D:\algorithm_game\solo\Assets\Scripts\Sim\Waves\WavePlanner.cs — 구성 생성(06 3.2절 순서 1~11), 틀·변덕 고르기, 가용·가중치·종 상한. 테스트용 `PlanOverrides`, `PlanTrace`
- D:\algorithm_game\solo\Assets\Scripts\Sim\Waves\WaveCurves.cs — 곡선 순수 함수(Budget, BundleCount, TelegraphCap, EliteCount, ModifierCount, HpMicro 정수 내림 반복, EnemyHp, BossHp)
- D:\algorithm_game\solo\Assets\Scripts\Sim\Waves\WaveTuning.cs — G09.json 읽기
- D:\algorithm_game\solo\Assets\Resources\DataSolo\G09.json — 종 기본 가중치 100, 스폰 모서리·개체 1칸, 다시 뽑기 8회(솔로 임시), 가시덩굴 벽 2칸·플레이어 3칸, 소품 배치 6벌
- D:\algorithm_game\solo\Assets\Tests\EditMode\Waves\ — WaveTestKit.cs(가짜 Threats·Enemies·Player), WaveCurveTests.cs, WavePlanTests.cs, WaveRuntimeTests.cs, WaveWeatherStarfallTests.cs
- .meta 파일은 만들지 않았다(Unity가 들일 때 만든다).

## 확인
- `bash Tools/compile.sh -f Assets/Scripts/Sim/Waves/,Assets/Tests/EditMode/Waves/` 끝 코드 **0** (두 번 확인). 그때 `Assets/Scripts` 단위는 남의 깨진 파일 6개(Sim/Enemies 4개, View/Effects 2개)를 뺀 reduced 상태였다.
- Unity 러너로는 돌리지 않았다. 대신 compile.sh가 만든 dll을 스크래치패드의 반사 실행기(.NET 9, `SOLO_ASSETS_DIR` 지정)로 돌려 **Waves 테스트 195개 전부 통과**를 봤다. Unity EditMode 러너에서의 통과는 확인하지 못함.

## 계약 변경 필요
1. `WaveModule.HpMul(int wave)`, `Budget(int wave)`, `TelegraphCap(int wave)`에 콘텐츠 인자가 없고 `SimModuleFactory.CreateWaves(ref WaveModule m)`도 db를 못 받는다. 우회: 세계를 받는 메서드에 들어올 때마다 `w.Content`를 붙잡는다. 붙기 전에는 HpMul = 1, Budget = 0, TelegraphCap = int.MaxValue. 연습 방에서 Run이 Step 전에 허수아비를 Spawn해도 H(1) = 1이라 결과는 같다. 요청 시그니처: `public virtual float HpMul(IContentDb db, int wave)`, `int Budget(IContentDb db, int wave)`, `int TelegraphCap(IContentDb db, int wave)` (또는 `CreateWaves(IContentDb db, ref WaveModule m)`).
2. `WaveState`에 웨이브별 보조 상태 자리가 없다(가시 띠 표, 마지막 숨 표 목록, 진행 바 분모). 우회: 디렉터 안의 `ConditionalWeakTable<WaveState, …>`에 매달았다. 요청: `public object WaveState.Scratch;` (PlayerState.ArtScratch와 같은 꼴).
3. `StarfallState.ScheduledTick`의 기준이 계약에 없다. **웨이브 시계(WaveTick) 기준 값**으로 넣었다(예고 구간은 시간에 안 넣으므로). `PhaseStartTick`·`NextRetryTick`은 절대 틱. 주석 추가 요청.
4. 좁은 들 가시밭은 띠 모양이라 `ZoneSpec.LeavesHazard`(HazardDef: 반경뿐)로 못 만든다. 우회: Bypass 예고(Band 4개) + 예고가 끝나는 틱 7단계에 `Threats.SpawnHazard(HazardSpec { Kind = ThornBorder, Shape = Band, DurationTicks = 0 })` 4개. 계약 변경 없이 돈다.

## 다른 모듈에 기대한 가정
- **Run(G10)**: BuildPlan은 야영 진입 때(또는 첫 웨이브 전에) 부르고, BeginWave가 돌려준 틱만큼 WavePrelude에 머문다. 방출·웨이브 시계·별똥은 `Phase == WaveCombat`일 때만 돈다(Practice는 BeginWave를 안 거쳐 아무 일도 없다). 클리어 처리에서 `Threats.ClearAll` → `Waves.EndWave` 순서.
- **Enemies(G06)**: `Spawn`이 EnemySpawnNotice·EnemySpawned를 낸다(디렉터는 안 낸다). 사망 처리에서 `Waves.OnEnemyKilled(w, e)`를 부를 때 `e.Pos`가 사망 자리다. 구성으로 나온 개체는 `NoReward == false && SummonerId == 0`(진행 바가 이것으로 센다).
- **Threats(G05)**: Band 구역·남는 구역은 `Pos` = 시작점, `Dir` 방향으로 `Length`, 폭 `Width`(중심선 기준). `TryGetTiming`이 false면 청한 틱 + 180틱으로 센다. 마지막 숨 원은 `OwnerEnemyId = 0`(죽은 주인 때문에 요청이 버려지지 않게), `StaggerIfBlockedTicks = 12`, `Bypass = false`. `IsClear`가 적·예고·남는 구역을 다 본다.
- **Bosses(G07)**: `SelectBoss`가 null을 주면 bosses.json 순환 순서(첫 만남이면 허상)로 채운다.
- **Player(G01)**: `GainCrystal`이 false면 별똥을 남겨 둔다.
- **표시(G14·G15)**: 버린 물건은 `WeatherStarted(Id = event.discarded_item, A = 변형 번호)`로 나간다. 별똥은 예고 구역을 청하지 않고 `w.Starfall` + Starfall* 이벤트만 쓴다.

## 내가 정한 해석(요약서가 모호했던 곳)
- **스폰 변**: 06 2.2절 글자는 "y < 7 → 위 변"인데 원문은 "위 변(y = 14 쪽)" = 먼 변이다. "플레이어에게서 먼 두 변"을 따랐다: 플레이어 y < H/2 → y = H 변, x ≤ W/2 → x = W 변. 정확히 가운데면 y = 0(화면 위)·오른쪽. 가로 변 하나 + 세로 변 하나를 길이 비례로 균등.
- **구성 난수**: `w.Rng(WavePlan)`을 이어 쓰지 않고 `Pcg32.Derive(w.Seed, WavePlan, 웨이브 × 3 + 갈래)`로 웨이브마다 갈랐다(원문 S07 14.2와 같은 꼴). BuildPlan을 몇 번 불러도 같은 구성이고, 시작 웨이브 11·21에서도 틀을 13부터 차례로 세어 같은 값이 나온다. 스폰·날씨·별똥은 `w.Rng(Spawn/Weather/Starfall)`.
- **마지막 숨·가시 띠 예고의 피해 분류**: `PlayerDamageSource.Lingering`(명세 "피해 1" 그대로, 웨이브 61+ 성장 없음). Telegraph로 셀지는 사용자·설계자 확인이 필요하다.
- **묶음 나누기**: 한도 = ceil(전체 비용(엘리트 포함) ÷ 묶음 수). 빈 묶음은 방출 때 건너뛴다.
- **가까운 적 ≤ 3 조기 방출**은 대기열이 비었을 때만 본다(플레이어가 없어 방출이 밀린 틱에 묶음이 줄줄이 풀리지 않게).
- **지체 방지**: 11단계에서 시계가 7,200이 되는 틱에 Stalled·대기열 이동, 실제 등장은 다음 틱(웨이브 시계 7,200인 틱) 7단계.
- **TodChanged**는 직전 웨이브와 시간대가 다를 때 BeginWave에서 낸다. DayChanged는 하루의 첫 웨이브(웨이브 1 제외)에서, 소품 배치 = (날 − 1) % 6.

## 구현하지 못했거나 확인하지 못한 것
- Unity EditMode 러너 실행(통합 단계 몫).
- 옮기지 않은 벡터: TV-108(변덕 점수 배율 — Score/G11 몫), TV-109(데이터 로더 거부 — F1 몫), TV-106의 "12틱씩 미룸" 자체(Threats/G05 몫. 여기서는 요청 인자와 요청 수만 확인).
- 느린 탄막의 탄 간격 벌리기는 G06 몫이라 배율(`EnemyShotCountMul` 1.5, `EnemyShotSpeedMul` 0.8)만 적는다.
- 스크래치패드 `…\scratchpad\runner\`(다른 작업의 폴더)에 Program.cs·runner.csproj를 잘못 만들었다가 바로 지웠다. 그 폴더의 기존 파일은 건드리지 않았다.

## G10 — 판 상태 기계, 전리품, 야영(모닥불·출발 표식), 원정 지갑, 판 중 드랍, 연습·튜토리얼 방

G10 (판 상태 기계 · 전리품 · 야영 · 지갑 · 드랍 · 연습/튜토리얼 방) 구현 끝. compile.sh 끝 코드 0, Run 테스트 130개는 Unity 밖 반사 실행기로 돌려 전부 통과했다(Unity 테스트 러너로는 돌리지 않았다).

## 만든 파일
- `D:\algorithm_game\solo\Assets\Scripts\Sim\Run\RunFlow.cs` — StartRun, Stage11_Phase, 클리어 처리, 훅, BuildSummary
- `D:\algorithm_game\solo\Assets\Scripts\Sim\Run\RunFlow.Camp.cs` — 야영 진입, 모닥불·출발 표식, HealCost, 해금 차례
- `D:\algorithm_game\solo\Assets\Scripts\Sim\Run\RunFlow.Loot.cs` — 전리품 카드
- `D:\algorithm_game\solo\Assets\Scripts\Sim\Run\RunFlow.Commands.cs` — Stage0_Commands(RunCommandKind 전부), 조립 SlotRef, 대기 칸
- `D:\algorithm_game\solo\Assets\Scripts\Sim\Run\RunFlow.Practice.cs` — 연습·튜토리얼 방, 수치판, 완료 판정
- `D:\algorithm_game\solo\Assets\Scripts\Sim\Run\RunScratch.cs` — 세계별 보조 상태, G10.json 읽기
- `D:\algorithm_game\solo\Assets\Scripts\Sim\Run\SimModuleFactory.Run.cs` — `CreateRun` 등록
- `D:\algorithm_game\solo\Assets\Resources\DataSolo\G10.json` — 보충 수치
- `D:\algorithm_game\solo\Assets\Tests\EditMode\Run\` — `RunTestRig.cs`(가짜 모듈), `RunFlowPhaseTests.cs`, `RunFlowEconomyTests.cs`, `RunFlowCampTests.cs`, `RunFlowLoadoutTests.cs`, `RunFlowPracticeTests.cs`
- `.meta` 파일은 만들지 않았다.

## 확인한 것
- `bash Tools/compile.sh -f Assets/Scripts/Sim/Run/,Assets/Tests/EditMode/Run/` → 끝 코드 0. 마지막 실행에서 `Assets/Scripts`는 남의 파일 1개가 빠진 `reduced`였고, 그 직전 실행은 네 단위 모두 `ok`였다.
- 테스트: 컴파일된 테스트 dll을 진짜 `nunit.framework.dll`과 실제 JSON(`SOLO_ASSETS_DIR`)으로 dotnet 반사 실행기에서 실행 → `passed 130, failed 0`. 일부러 틀린 단언 3개를 넣어 실패가 잡히는 것을 확인하고 지웠다.
- brief의 벡터는 전부 테스트로 옮겼다. TV-267~270은 요약서에 번호별 내용이 없어 06 4.5절 규칙 문장에서 기대값을 잡았다.

## 계약 변경 필요 (Core는 고치지 않고 우회함)
1. **결정 사용 횟수**: 수치판 "결정 1개당 피해"를 `PlayerState.Crystals` 감소와 `Debt` 증가로 센다. 결정 무한 옵션이 켜지면 값이 변하지 않아 0으로 나온다. 제안: `public virtual void RunModule.OnCrystalsSpent(SimWorld w, PlayerState p, int count)` 또는 `PlayerState.CrystalsSpentTotal`.
2. **반동 사격 감지**: 튜토리얼 ④를 `w.Projectiles`에 `HitEffect == ShotHitEffect.RecoilKnockback`인 탄이 생겼는지로 본다. 제안: `public virtual void RunModule.OnArtFired(SimWorld w, PlayerState p, string artId)`.
3. **전리품 명령 인자**가 `RunTypes.cs`에 없다. 이렇게 정했다(G21이 맞춰야 한다):
   - `LootPick`/`LootUnpick`: A = 카드 번호.
   - `LootConfirm`: 고른 카드와 빌린 블록이 바로 `Storage`에 들어간다. 12칸을 넘으면 `Loot.OverflowToDiscard = 넘친 수`로 두고 LootPick 단계에 머문다.
   - `LootDiscard`: A = 보관함 자리(100 + i 또는 i). 넘친 수가 0이 되면 자동으로 Camp.
   - `CampHeal`·`LoadoutMove`·`LoadoutRemoveAll`·`ArtPick`: `Slot` = 플레이어. `ArtPick`은 `Id` = 비기 id.
4. **`RunState.Wave`의 야영 중 값**: 야영(Camp·StartCamp) 동안은 다음에 싸울 웨이브로 뒀다. WaveClearing·LootPick 동안은 방금 넘긴 웨이브다. 주석에 못 박아 주면 좋다.
5. **`RunState.Score`**는 건너뛴 점수를 포함한 합계로 뒀다.

## 다른 모듈에 기대한 가정
- **Player(G01)**
  - `JPressedThisTick`과 `Keys`를 WaveClearing·야영에서도 갱신한다.
  - `SetupPlayer`가 조립을 채운다. 비어 있을 때만 Run이 `cfg.Loadout`으로 채운다.
  - `LastFireTick == w.Tick`이면 그 틱에 J 발사가 있었다고 본다.
  - 임계 판정은 "발사 뒤 열 ≥ CritStartHu 또는 그 틱에 잠김"으로 근사했다. 불가마 흡수로 열이 깎인 임계 발사는 놓칠 수 있다.
  - 결정 환전과 PracticeReset에서 Run이 `p.Crystals = 0`(리셋은 `p.Debt = 0`도)을 직접 쓴다. 환전액은 `max(0, 결정 − 빚) × 5`이고 빚은 건드리지 않는다.
- **Enemies(G06)**
  - 처치 골드·점수의 비용은 `e.Def.Cost`(없으면 `e.Cost`)에 엘리트 배율을 Run이 곱한다. `e.Cost`에 배율을 미리 곱해 두면 안 된다.
  - 허수아비: Run이 `Spawn(DefId = Dummy, HpOverride = 1,000,000, NoReward, Instant)` 뒤 `MaxHp`/`Hp`/`HpFloor(1)`를 직접 덮고 매 틱 끝에 가득으로 되돌린다. G06이 틱 중간에 허수아비 체력을 채우면 수치판이 0이 된다.
- **Waves(G09)**
  - `WaveStarted` 이벤트는 Waves가 낸다(Run은 내지 않는다).
  - `Stalled` 또는 `WaveTick ≥ stall_seconds`면 처치 점수 절반.
  - `w.Wave.HealthShardDropped`는 Run이 쓴다.
- **Bosses(G07)**
  - `SpawnBoss`가 `w.Boss`를 만든다. null이면 Run은 바로 WaveCombat으로 넘기고 `Waves.IsCleared`로 판정한다.
  - 흠집 %는 몸통 `MaxHp`와 `w.Boss.HpMin`으로 계산한다.
  - "복수 대상 싸움"은 웨이브 20 + `boss.mirror_phantom` + 첫 만남 아님.
- **Arts(G03)**
  - `ArtOffered`는 Run이 새 후보를 세울 때 낸다(G03도 내면 중복된다).
  - 고르는 중인 차례는 `PendingArtTurns` 맨 앞에 남는다. 남은 차례 여부 = `Count > 0`.
  - 후보는 첫 Active 플레이어 기준이다.
- **Score(G11)**: 첫 만남 3,648 고정은 `stats.PhantomFirst`를 보고 G11이 돌려준다.

## 솔로 임시 값과 스스로 정한 규칙 (사용자 확인 대기)
- `G10.json`의 임시 값: 허수아비 자리(전장 가운데 + (0, 3)칸), 체력 조각 줍기 반경 0.5칸, DPS 분모 하한 1초.
- 연습 방에서 체력이 0이 되면 판 종료 대신 바로 `Player.Rise`.
- 야생 블록을 칸에서 뺄 때 보관함이 12칸이면 거절한다(맞바꾸기는 된다).
- 출발 누름은 누른 틱을 1로 세어 60번째 틱에 출발한다. `DepartHold` 이벤트의 A는 `max(1, 틱)`.
- DPS는 첫 적중부터의 평균이다.
- 재료 꾸러미·골드 조각은 연출용 `DropEntity`로 만들고 LootPick에 들어갈 때 전부 걷는다.

## 구현하지 못했거나 확인하지 못한 것
- 구현하지 못한 것은 없다.
- Unity 테스트 러너 실행과 실제 Player·Enemies·Bosses·Waves 구현과의 맞물림은 확인하지 못했다(가짜 모듈로만 검증).
- 공유 스크래치패드 사고: 처음에 `scratchpad/out`에 `compile.sh -o`를 돌리고 `scratchpad/run`에 dll 세 개를 복사했는데, 둘 다 다른 작업이 쓰던 폴더였다. 그 폴더의 dll·rsp가 내 빌드로 덮였을 수 있다. 내가 만든 `runner.cs`·`runner.exe`는 지웠고, 이후로는 `g10out`·`g10run`만 썼다. 소스 트리에는 영향 없다.

## G11 — 점수 규칙과 정산(귀환 가방, 재료 추첨, 기록·칭호·판 목록, 시작 웨이브, 중간 지점)

G11 (점수 규칙과 정산) 결과 보고

## 만든 파일 (전부 소유 폴더 안)
- D:\algorithm_game\solo\Assets\Scripts\Sim\Score\ScoreCalculator.cs — `Wizard.Solo.Sim.Score.ScoreCalculator : ScoreRules` (WaveScore, SkipScore, 공개 보조 `S10`, `Budget`). Meta 참조 없음.
- D:\algorithm_game\solo\Assets\Scripts\Sim\Score\SimModuleFactory.Score.cs — `CreateScore` 등록.
- D:\algorithm_game\solo\Assets\Scripts\Meta\Settlement\SettlementService.cs — `ISettlementService` 전부 + 공개 보조 `IsValidStartSkip(best, skip)`, `static DentPercent(hpMax, hpMin)`, `static DrawMaterials(table, chances, crowAllowed, rollBp)`.
- D:\algorithm_game\solo\Assets\Scripts\Meta\Settlement\SettlementTuning.cs — G11.json 읽기.
- D:\algorithm_game\solo\Assets\Scripts\Meta\Settlement\CheckpointCodec.cs — RunSummary ↔ run_checkpoint.summary.
- D:\algorithm_game\solo\Assets\Scripts\Meta\Settlement\MetaFactory.Settlement.cs — `CreateSettlement` 등록(기기 난수 씨앗).
- D:\algorithm_game\solo\Assets\Resources\DataSolo\G11.json — skip_score_percent 60, start_skip_step 10 / limit 7:10, phantom_first_wave 10, run_history_cap 100, record_titles(10·30·100), bloom_title_count 100, flawless_night_slots [8,9], wild_stain_total_waves [0,100,500,2000].
- D:\algorithm_game\solo\Assets\Tests\EditMode\Settlement\ScoreCalculatorTests.cs, SettlementServiceTests.cs — 테스트 사례 131개.
- .meta 파일은 만들지 않았다(Unity가 처음 열 때 만든다).

## compile.sh 결과
- `bash Tools/compile.sh -f Assets/Scripts/Sim/Score/,Assets/Scripts/Meta/Settlement/,Assets/Tests/EditMode/Settlement/` → **끝 코드 0** (필터 안 오류 0). 앞선 여러 번은 남의 폴더(Sim/Threats, Bosses, View/Hud 등)가 깨져 3이었고 6번째 재시도에서 0. 그때도 Assets/Scripts는 남의 파일 5개를 뺀 `reduced` 상태였다.
- 테스트 실행: **Unity 러너로는 돌리지 못했다(컴파일만 확인).** 대신 스크래치패드에서 Core+Data+Save+Audio+Art+내 폴더만 따로 컴파일하고, 직접 쓴 NUnit 흉내(Assert 몇 종 + 반사 실행기)로 .NET 8에서 돌려 131개 통과 0개 실패를 보았다. 진짜 NUnit·Unity 실행이 아니므로 통합 단계의 `unity-test-edit.sh` 확인이 필요하다.

## 계약 변경 필요
없음(Core는 고치지 않았다). 참고로 올릴 것:
1. `Core/Sim/Modules.cs`의 `ScoreRules` 주석이 "구현 폴더 Meta/Settlement"로 적혀 있다. 실제는 `Sim/Score`(00 문서 2절)다. 주석만 어긋난다.
2. `ISettlementService`에 시작 웨이브 검증 메서드가 없다(T085 "skip 15 거절"). 구현 클래스에 `bool IsValidStartSkip(int bestWave, int skip)`를 두었다. 화면·IWandService.BuildRunConfig가 인터페이스로 쓰려면 `bool IsValidStartSkip(int bestWave, int skip);` 추가 요청.
3. "하급 강화석을 처음 받은 정산"을 가릴 저장 필드가 없어 `ProfileFlags.Extra["low_stone_received"]`(덧필드)에 적는다. 정식 필드로 하려면 `ProfileFlags.LowStoneReceived` (bool) 추가 요청.
4. 복수 보상 재료의 `MaterialGain.Source`에 맞는 값이 없어 `BossKill` + `Bound = true`로 낸다. 구분이 필요하면 `DropChanceSource.Revenge` 추가 요청.

## 다른 모듈에 기대한 가정
- Run(G10): `WaveStats.KillScoreX2` = Σ cost × 10 × (지체 뒤 ? 1 : 2), 엘리트는 cost에 이미 3배. `ElapsedTicks`를 채운 뒤 WaveScore를 부른다. 최종 격노 뒤 스침은 Run이 `Grazes`에 넣지 않는다(점수 쪽은 상한 20/40만 건다).
- Waves(G09): `Stats.Wave/IsBoss/PhantomFirst/Whims`를 채운다. 보스 여부는 `IsBoss || wave % 10 == 0`으로 읽는다.
- `RunSummary.Score`가 건너뛴 점수를 포함하든 안 하든 같은 답이 나오게, 판 점수 = max(summary.Score, Σ WaveResults.Score.Total + SkippedScore)로 셈한다.
- `RunSummary.LastClearedWave`가 넘긴 최고 웨이브(최고 기록·칭호·phantom_met 판정), `ClearedDirect`가 직접 넘긴 수(가방·누적 웨이브).
- `DropChances`: EliteKill의 Count = 마리 수, 그 밖은 한 번. 첫 만남 웨이브 10은 BossKill이 아니라 PhantomFirst 기회로 적는다. CrowBundle 기회가 목록에 없어도 `CrowBundleEarned`면 지급한다.
- `PhantomAttempted`·`PhantomDentPercent`는 Bosses/Run이 채운다(흠집 % 식은 `SettlementService.DentPercent`와 같아야 한다). 흠집 최댓값은 진 판(처치하지 못한 판)만 반영한다.
- WildBlock.Rarity가 비면 blocks.json 등급을 쓴다. 서고 블록 판별 = `db.Block(id) != null`, 한도 = library_base_cap + library_expansions × shop_library_step_blocks (G13과 같은 식이어야 한다).
- 중간 지점: Run/화면이 웨이브 5의 배수 클리어 때 `SaveCheckpoint(Run.BuildSummary(w))`를 부른다(서비스는 배수 여부를 검사하지 않는다). Bootstrap이 `SettlePendingCheckpoint()`를 부른다.
- ProfileStore가 `RunCheckpointData.Summary`(JObject, 필드 이름 PascalCase 그대로)를 그대로 되살린다(임시 폴더 저장소로 왕복 확인함).

## 이 빌드에서 고른 해석 (명세에 세부가 없던 것)
- 점수 내역 줄: 줄마다 내리면 합이 어긋나므로 내림에서 남은 점수는 처치 줄에 얹어 Kills+Boss+Clear+Fast+NoHit+Graze+Variance = Total.
- `GoldGained` = 프로필에 더한 전체(지갑 + 남은 블록), `GoldFromLeftovers`는 그 일부. 서고가 차서 못 담은 가방 블록도 남은 블록 골드로 간다(가방 순서대로 담는다).
- 오류 코드: Mode != Endless와 같은 RunId 재정산 = `NotEligible`, 가방 검증 실패·모르는 캐릭터 = `InvalidArgument`. RunId가 빈 판끼리는 같은 판으로 보지 않는다.
- 추적기: `runs_since_first_meet`는 첫 만남이 든 판부터 복수 판까지 정산마다 +1, 복수 때 `revenge_runs`에 복사(S06 T267: 같은 판이면 1). 복수 완료 뒤에는 tries·runs를 더 세지 않는다.
- 자동 가방 동률: 등급 내림 → drops.json rarity_order 순서 → uid 오름.
- 이미 정산한 판이거나 읽을 수 없는 중간 지점은 버리고 null을 돌려준다.
- 기록 칭호는 캐릭터의 최고 웨이브(이번 판 포함) 기준, 야생 물 단계는 올라가기만 한다.

## 구현하지 못했거나 확인하지 못한 것
- Unity EditMode 러너 실행은 하지 않았다(위 참조).
- 06 6.1절 TV-141·142·143·147·148(535 / 5,740 / 18,008 / 13,806 / 2,070)은 요약서에 입력값이 없어 테스트로 옮기지 못했다(brief가 지정한 01 6.4절 벡터 T020~T040은 전부 옮김. T035는 요약서 표에 없다).
- 07 12.9절 TV-G-01~06(웨이브 골드·보스 골드·결정 환전)은 Run의 지갑 계산이라 여기서는 TV-G-07·08·09만 옮겼다.
- 주간 도전의 "4번째 판부터 가방 0칸"은 D-24(메뉴에 없음)에 따라 넣지 않았다.
- `MetaFactory.Fill`을 통한 등록은 테스트로 확인하지 않았다(다른 메타 서비스 생성자에 묶인다). partial 메서드 시그니처는 컴파일로만 확인.

## G12 — 강화 규칙: 확률·비용·판정·분기·변이·새기기·흔적·기댓값

G12 (강화 규칙) 최종 보고

## 만든 파일
런타임 — D:\algorithm_game\solo\Assets\Scripts\Meta\Enhance\
- EnhanceRules.cs — 순수 정적 규칙: 확률 분해(기본/제물/기운/상한), 판정 구간 bp와 Judge, 비용·강화석 등급, 하락 바닥·DropPossible·실패 안내(FailHint), 일반 실패 분기(ResolveFail), 대성공/성공 단계, 흔적 수, 마일스톤·칭호 묶음, 변이 후보 목록·추첨·밀어내기, 새기기 확률·비용·평균 시도, StageDamageMul, 불변식 검사(CheckInvariants)
- EnhanceRules.Expected.cs — 기댓값 마르코프(가우스 소거 정확 풀이, 제물 고정·부적·바닥 없음 옵션), ExpectedTable, "보유 재화로 평균 N번"(AttemptsWithOwned, 07 6절 앞으로 굴리기)
- EnhanceService.cs — IEnhanceService 구현(전제 검사 → ErrorCode, 귀속분 먼저 차감, 제물 차감·끼운 블록/마지막 투사체 보호, 난수 한 번, WandData 갱신 + Loadout.Extra 칸 추가, pending_offer + fallback 미리 계산, EnhanceResult, enhance_log_tail, next_attempt_id, 매번 Save, ServerNotice 항상 null)
- EnhanceFlowRules.cs — 건너뛰기/빠른 강화/이어서 강화의 규칙 쪽(CanSkip, UsesFullRitual, StopAfter/StopBefore, PickSacrifices, EnhanceTally)
- EnhanceRandom.cs — IEnhanceRandom, Pcg32EnhanceRandom(기본은 OS 암호 난수로 씨앗)
- EnhanceTuning.cs — DataSolo/G12.json 읽기
- MetaFactory.Enhance.cs — CreateEnhance 등록
데이터 — D:\algorithm_game\solo\Assets\Resources\DataSolo\G12.json (scar_max 2, owned_forecast_max_attempts 1000, skip_ignore_ticks 18, short_result_ticks 30, fast_charge_ticks 18, fast_attempt_ticks 48, fast_full_ritual_min_stage 9, plus13_first_lock_ticks 120, auto_stop_on_drop_default true, log_tail_max 50[솔로 임시: 원문에 값 없음])
테스트 — D:\algorithm_game\solo\Assets\Tests\EditMode\Enhance\
- EnhanceTestKit.cs, EnhanceProbabilityTests.cs(12.1·12.2·12.3 + 08 10.4 TV-084~096), EnhanceBranchTests.cs(12.4 TV-B-01~20, 4.6 표 모든 줄, 4.1 전제, 4.11, 4.12, TV-N-05, 4.9, 4.10), EnhanceMutationCarveTests.cs(12.5 TV-M-01~09, 12.7 TV-S-01~11), EnhanceFlowAndInvariantTests.cs(12.12 TV-Q-01~10, 3.1 불변식 6,000걸음 무작위 걷기 — 매 시도 뒤 검사), EnhanceExpectedTests.cs(12.6 TV-MK-01~14·20~22, TV-MC-01·02[Slow]), EnhanceEconModel.cs + EnhanceEconomyTests.cs(12.8 TV-E-01~16, 20~28, 30~32, 40~44, 50, 60 — docs/sim/online_econ3*.py 세 개를 C#으로 옮김, [Category("Slow")], 건너뛰기 표시 없음), EnhanceLockAndRegistrationTests.cs(WandLocked, MetaFactory 등록, 데이터 값)
(.meta는 만들지 않았다 — Unity가 만든다.)

## compile.sh 결과
`bash D:/algorithm_game/solo/Tools/compile.sh -w -f Assets/Scripts/Meta/Enhance/,Assets/Tests/EditMode/Enhance/` → 끝 코드 0, 오류 0·경고 0, 네 단위 전부 ok(마지막 실행. 중간에 남의 파일이 깨져 3이 한 번 나왔고 다시 돌려 0).
테스트 실행: Unity 러너로는 돌리지 않았다(확인하지 못함). 대신 Unity 밖 반사 실행기(dotnet, 스크래치 폴더)로 compile.sh가 만든 전체 dll에서 내 테스트 189개를 돌려 189개 통과를 봤다(실패하는 단언이 잡히는 것도 확인). 마르코프 벡터는 전부 명세 자릿수까지 일치(요약서가 "미확인"이라 한 MK-07·08·13·14·21·22 포함).

## 계약 변경 필요 (Core는 고치지 않고 우회함)
1. `ProbBreakdown`에 같은 속성 줄이 없다 → 추가 요청: `public int SameElementPct;` (지금은 SacrificePct = 등급+속성 합계(상한 뒤)이고, 내역 두 줄은 `EnhanceService.SacrificeOf(long wandId, IReadOnlyList<string> sacrifices)` → `SacrificeBreakdown { GradeLinePct, SameElementPct }` 로 우회).
2. `IEnhanceService`에 없는 것(구현 클래스에만 public으로 둠. 화면은 `ctx.Enhance as EnhanceService` 또는 정적 EnhanceRules/EnhanceFlowRules를 불러야 한다) → 추가 요청:
   - `int AttemptsWithOwned(long wandId, int targetStage);` (str.enhance.expect.with_owned)
   - `FailHint HintFor(long wandId, bool useCharm);` (실패 안내 한 줄)
   - `int ScarCount(WandData wand);`
   - `SacrificeBreakdown SacrificeOf(long wandId, IReadOnlyList<string> sacrifices);`
   - `bool IsLocked(long wandId);`
3. `CarveResult`에 강화석 잔액이 없다 → `public int StoneHighAfter;` 요청(선택).
4. 변이 결정(ResolveMutation)의 꾸러미는 그 시도의 RollBp·Breakdown을 enhance_log_tail에서 되살린다(기록에 없으면 RollBp = −1). 정식으로 하려면 `MutationOffer`에 `RollBp`, `SuccessPct` 필드 추가 요청.

## 명세 문제(올림 — 구현에 맞춰 고치지 않음)
- 07 13절 M-1 ↔ 08 10.4절 TV-089: 같은 속성 +5가 제물 상한(+40) 안인가 밖인가. 07(S8, TV-P-04 = 88)을 따랐다. 08 TV-089는 93을 요구한다. 테스트 `S13_TV_089__…AgreedParts`는 두 문서가 같이 말하는 부분(등급 합 50→40, 같은 속성 5, 기본 3, 기운 45)만 단언하고 합계는 단언하지 않았다. 사용자 결정 필요.
- TV-E-41(1→14 부적 0.07/시간), TV-E-50 +3단(0.06): 모형의 정확값은 판당 2% ÷ 23.3분 = 0.0515/시간이고 명세 값은 원본 파이썬의 4,000·3,000판 표본 잡음이다(원본을 직접 돌려 0.07/0.06 재현 확인). 부적 열만 "±10% + 원본 표본의 3σ"로 견주게 했다(기대값은 명세 값 그대로). 다른 TV-E 값은 전부 ±10%(낚시 ±3%) 안.
- TV-E-15·16 슬롯 분포는 명세에 허용 폭이 없어 ±5%p로 봤다. 중앙값·상위 10%도 평균과 같은 ±10%로 봤다(전부 통과).
- 이름 표 `str.enhance.line.roll` "이하면 성공"은 판정(미만)과 어긋난다(07 M-3, 이름 표 소관).
- 제물 "+15"는 실제 블록 조합으로 만들 수 없는 값이다(4/8/14/+5 조합에 15 없음) — MK-20·MC는 고정 보너스로 계산.

## 다른 모듈에 기대한 가정
- 원정 중 잠금: `ctx.Session.MainWorld.Run.Config`(Mode == Endless, WandId 일치)이고 `Run.Phase != RunEnded`일 때만 WandLocked. 정산 뒤 강화 화면으로 가는 길에서 판이 RunEnded이거나 EndRun 되어 있어야 한다(G10/F5/G22). ctx.Session이 null이면 잠금 없음.
- G13(SetLoadout·상점): `Loadout.Extra.Count == SlotsExtra`를 유지하고, 블록 `ItemStack.Qty`에 끼운 수(QtySocketed)가 포함된다. 제물은 `Qty − QtySocketed`만 쓴다. 떠나보내기는 pending_offer가 있으면 G13이 막는다.
- G11(정산): 귀속 재료는 `Qty`에 포함한 채 `QtyBound`로 표시한다(강화가 귀속분부터 깎는다).
- 변이 결정 대기 중 거절하면 탈 부적은 "맡아 둔 것"으로 보고 다른 지팡이의 부적 사용 가능 수에서 뺀다(NotEnoughCharm). 상점·정산은 부적을 줄이지 않는다고 가정.
- 변이 상한은 characters.json max_mutations(없으면 enhance.json mutation_cap).
- G23(강화 화면): 이어서 강화/건너뛰기/빠른 강화 판단은 EnhanceFlowRules + EnhanceTuning(DataSolo/G12.json)을 쓴다. 의식 시간표(5.3절)는 화면 몫이라 만들지 않았다.
- 전제 순서: MaxStage → WandLocked → MutationPending → NotEnoughGold → NotEnoughStone → InvalidSacrifice → LastProjectile → NotEnoughCharm → CharmNotNeeded. 없는 지팡이 NotFound, 없는 변이 제거·attemptId 불일치 InvalidArgument.

## 구현하지 못했거나 확인하지 못한 것
- Unity EditMode 러너 실행은 하지 않았다(규칙상 Unity를 띄우지 않음). 위 189개 통과는 Unity 밖 반사 실행기 결과다.
- TV-M-10·11(유리 지팡이 판 안 임시 하락)은 전투 쪽(Traits) 벡터라 옮기지 않았다. 12.11절은 TV-N-05만(나머지 솔로 제외).
- TV-089 합계(93 vs 88)는 위 모순 때문에 단언하지 않았다.
- TV-MC-01·02의 "규칙 엔진"은 규칙 함수(EnhanceRules)를 그대로 돌린 것이다(서비스 경유는 별도 테스트 `S07_6__ServiceWalkMatchesExpectedTable`로 +0→+7 평균을 기댓값 표와 ±3%로 확인).
- `S00_2_1__MetaFactoryRegistersEnhanceService`는 MetaFactory.Fill 전체를 부른다 — 다른 메타 서비스의 생성자가 최소 GameContext(Content, Profiles, Clock만)에서 예외를 던지면 깨진다(현재 전체 dll에서는 통과).

## G13 — 지팡이·서고·떠나보내기·감정가, 판 설정 만들기(원정·연습), 골드 상점

G13 결과 보고 (지팡이·서고·떠나보내기·감정가, 판 설정 만들기, 골드 상점)

## 만든 파일
- D:\algorithm_game\solo\Assets\Scripts\Meta\Inventory\WandService.cs: IWandService 구현. List, Equip, SetLoadout, Release, Appraise, LibraryFree/Total/Cap, BuildRunConfig, BuildPracticeConfig를 넣었다. 계약 밖으로 IsLocked, Release(ids, typedName), NeedsNameConfirm, MultiCheckable, RefundOf, IsLastProjectile, WandDisplayName도 있다. 확장 메서드 `WandReleaseExtensions.Release(this IWandService, ids, typedName)`도 이 파일에 있다.
- D:\algorithm_game\solo\Assets\Scripts\Meta\Inventory\InventoryRules.cs: 순수 규칙 모음.
  - 프로필 조립을 LoadoutSpec으로 바꾸는 `ToSpec`은 이 한 곳에만 있다.
  - CheckShape는 코어가 투사체인지, 칸 수 = L인지 본다.
  - CheckCompile은 ctx.Rules.Traits.ComputeDerived → CompileContext → Spells.Compile 순서로만 검사한다. Rules가 null이거나 Spells가 기본 클래스 그대로면 건너뛴다.
  - 그 밖에 서고 한도·개수, 마지막 투사체, 감정가·환급, 이름 비교, 여러 자루 체크, 다음 든 지팡이 고르기, 상점 +0 지팡이 만들기가 있다.
- D:\algorithm_game\solo\Assets\Scripts\Meta\Inventory\InventoryTuning.cs: G13.json을 읽는다.
- D:\algorithm_game\solo\Assets\Scripts\Meta\Inventory\MetaFactory.Inventory.cs: CreateWands 등록.
- D:\algorithm_game\solo\Assets\Scripts\Meta\Shop\ShopService.cs: IShopService 구현.
  - 품목은 부적(주 3개), item.wand_box, item.library_expansion 셋이다. 꾸미기 sku와 유료 품목은 없다.
  - 주 경계는 ctx.Clock.Now 기준이고, 요일은 economy.json shop_week_start_day_of_week(월요일)를 읽는다.
- D:\algorithm_game\solo\Assets\Scripts\Meta\Shop\MetaFactory.Shop.cs: CreateShop 등록.
- D:\algorithm_game\solo\Assets\Resources\DataSolo\G13.json: Def에 없는 명세 수치. +7 이상 이름 입력, 여러 자루는 +3 이하·변이 0·확장 0.
- D:\algorithm_game\solo\Assets\Tests\EditMode\Inventory\InventoryFakes.cs, WandServiceTests.cs, RunConfigTests.cs, ShopServiceTests.cs: 테스트 80개.
  - 옮긴 벡터: 07 12.10의 TV-SH-01~10, TV-AP-01~04, TV-RL-01·04·05·06(원정 중)·08·09. 수수료(TV-F)·거래(TV-T)·TV-RL-02·03·07은 솔로 제외라 뺐다.
  - 12.13의 TV-PF-01·06, 12.14의 TV-PAY-03·13, 3.1 불변식, 3.3 마지막 한 자루, 01 3.2 마지막 투사체, 03 SL-3·4·6, 08 TV-029·030·031·032와 5.4 원정 중 잠금, 주 경계(일요일 23:59 → 월요일 00:00), 작업 지시의 계약 벡터 전부.
  - 테스트 폴더에는 .meta 파일을 만들지 않았다(Unity가 만든다).

## compile.sh 결과: 확인하지 못함
- `bash Tools/compile.sh -f Assets/Scripts/Meta/Inventory/,Assets/Scripts/Meta/Shop/,Assets/Tests/EditMode/Inventory/`를 15번 넘게 돌렸는데 끝 코드가 매번 3이었다. 내 필터 안의 오류는 0개다.
- 원인은 남의 폴더가 깨져 있어서다(Sim/Bosses, Sim/Enemies, View/Hud, View/Effects, View/World. 남은 오류 10~11개). 4번 빼고 다시 해도 Assets/Scripts 단위가 dll을 못 만든다.
- 대신 확인한 것(compile.sh와 같은 csc·rsp·define):
  1. Assets/Scripts 전체 244개 중 남의 깨진 파일 11개를 빼고 컴파일하면 내 파일 6개를 포함해 오류 0으로 dll이 나온다.
  2. Core+Data+Save+Audio+Art+내 폴더만 따로 컴파일하고 내 테스트를 컴파일하면 오류·경고 0이다.
  3. 그 dll을 Unity 밖에서 내가 만든 반사 실행기로 돌리면 80개가 전부 통과한다. 실행기가 실패를 잡는 것은 일부러 틀린 Assert 2개로 확인했다.
- Unity 테스트 러너로는 돌리지 않았다. 그러니 Unity 기준으로는 "컴파일만 확인"도 아니다.

## 계약 변경 필요
1. `IWandService.Release(IReadOnlyList<long> wandIds)`에는 이름 입력 인자가 없다. 그래서 TV-RL-04(+7 이상 이름 확인)를 서비스에서 지키려면 `Result<long> Release(IReadOnlyList<long> wandIds, string typedName)`가 필요하다(S8 9.1 `ReleaseWands(ids, typedName)`).
   - 지금 우회: 계약의 Release는 +7 이상이면 NameConfirmRequired를 돌려준다.
   - 화면은 `using Wizard.Solo.Meta.Inventory;` 뒤에 `ctx.Wands.Release(ids, typedName)` 확장 메서드를 쓴다.
2. 추가하면 좋은 것(선택): `bool IsLocked(long wandId)`, `bool MultiCheckable(WandData)`, `bool NeedsNameConfirm(WandData)`, `long RefundOf(WandData)`, `bool IsLastProjectile(string blockId)`. 지금은 WandService·InventoryRules의 public 멤버로만 있다.

## 다른 모듈에 기대한 가정
- 원정 중 잠금은 프로필에 필드가 없어서 이렇게 판단한다: `ctx.Session.MainWorld.Run.Config`가 Mode = Endless이고 WandId가 같고 Phase가 RunEnded가 아니면 잠김. G10(RunModule.StartRun)이 `w.Run.Config = cfg`를 채운다고 가정했다. 연습은 잠그지 않는다.
- 시작 웨이브는 `ctx.Settlement.StartSkipOptions(CharacterRecord.BestWave)`가 돌려주는 skip 값에서 skip + 1만 허용한다(G11). Settlement가 null이면 웨이브 1만 된다.
- 컴파일 오류는 `SpellDiagnostic.IsError`로 판단한다. E_CORE_EMPTY는 CoreEmpty, 나머지 오류는 InvalidLoadout이다(G02).
- 강화로 확장 칸이 늘었는데(G12) 저장된 Extra가 짧으면 ToSpec과 SetLoadout이 빈 칸으로 채운다.
- 마지막 투사체 보호의 실제 거절(LastProjectile)은 강화(G12)가 `InventoryRules.IsLastProjectile`을 불러 해야 한다.
- TV-SH-09에서 서고가 차서 블록이 골드로 바뀌는 처리는 정산(G11)이 한다. 나는 LibraryCap − LibraryTotal = 0까지만 시험했다.

## 지시에 없던 동작, 확인하지 못한 것
- BuildRunConfig는 runs_started++ 말고도 `profile.last_character`를 그 캐릭터로 적는다(D-56). 원치 않으면 그 한 줄을 빼면 된다.
- GetState는 주가 바뀌어 부적 구매 수를 0으로 되돌렸을 때 Save를 부른다.
- 정렬의 동률 깨기:
  - 단계 정렬은 acquired_at 최근 → id 오름차순이다(IL은 솔로에 없어 뺐다).
  - 최근 정렬은 같은 시각이면 id가 큰 쪽(나중에 만든 것)을 앞에 둔다.
- 오류 코드는 요약서에 정해진 값이 없어 이렇게 골랐다:
  - 남의 캐릭터 지팡이 Equip·출발, 허용되지 않은 시작 웨이브, 모르는 sku → InvalidArgument
  - 서고 수량 부족 → InvalidLoadout
  - 여러 자루 조건 위반 → NotEligible
- 상점 지팡이의 acquired_at은 SystemClock.Iso와 같은 꼴("yyyy-MM-dd'T'HH:mm:sszzz")로 직접 쓴다.
- 작업 공간 사고 하나: 공유 임시 폴더(scratchpad)의 남의 `runner/` 폴더에 내 Program.cs와 runner.csproj를 잠깐 썼다. Program.cs는 내 폴더(scratchpad\g13runner)로 옮겼다. 원래 runner.csproj가 있었다면 내가 덮어써서 원본 내용은 되살릴 수 없다. 지금 그 파일은 net9.0 콘솔 프로젝트로 맞춰 두었고 Runner.exe 등 나머지 파일은 건드리지 않았다.

## G14 — 표시: 전장·시간대·날씨·모닥불과 출발 표식, 캐릭터와 지팡이

G14 is implemented: the arena layer and the character/wand layer. compile.sh returned exit code 0. I have not run the tests (no Unity), so only compilation is confirmed.

**Files created** (all inside my owned paths)
- `D:\algorithm_game\solo\Assets\Scripts\View\World\`
  - `ViewRegistry.World.cs` — fills in `AddArena` and `AddCharacters` (namespace `Wizard.Solo.View`).
  - `ArenaLayer.cs` — what it draws:
    - 24×14 floor tiles from the time-of-day sheet; the map (field.01–06) follows `Weather.PropVariant`.
    - Walls, spawn bushes, props (anything inside the centre 20×10 is skipped), the tower and its window.
    - Bush shake for 0.5 s on `EnemySpawnNotice`, larger in fog.
    - Dusk amber edge light; night/boss vignette and moonlight edge; boss-wave moon and moonlight circle.
    - Weather: rain at most 12% opacity and not over the centre, fog on the edges, puddles, discarded-item prop.
    - Campfire, interaction rings and depart marker at the `RunState` positions and radii only; the depart-hold ring fills by `(Tick − DepartHoldStartTick) / 60`, with a short fade on cancel.
    - Time-of-day crossfade over 4 s, in camp only.
    - Everything is chosen from state alone, so `Build` works with no events.
  - `CharacterLayer.cs` and `PlayerFigure.cs` — what each player gets:
    - Body clip chosen from state plus events (`ShotFired`, `ArtFired`, `PlayerHit`, `PlayerDowned`, `PlayerRose`). Feet sit 0.4 cells below the hit centre, using `ViewMath.Lerp`.
    - Shadow, 8 Hz invulnerability blink (reduced/minimal: 60% transparent plus a white outline), 0.12 s white hit flash, rim strength from `tod.json`.
    - Three graze afterimages in the element colour at 30%.
    - Wand: effective stage, aim vs hold, attached at the grip point, layer order by facing, char.2 split when facing up.
    - Wand heat reaction: white glow at critical, iron colour when locked that re-brightens every 0.5 s.
    - Idle particles reduced by `WandFxLevel` and multiplied by `WandFxOpacity`. Also the kiln line, detached parts, debt rings.
    - Mutation/expansion marks, scars or glass-wand white cracks, wild-stain overlay stages and the cracked mirror shard (read from `ctx.Game.Profile`).
  - Pure logic in `WorldViewRules.cs`, `ArenaScene` and `TodTransition`. Helpers: `ArenaMap.cs`, `WorldTuning.cs` (reads G14.json), `ShapeMeshes.cs`, `WorldDraw.cs`.
- `D:\algorithm_game\solo\Assets\Resources\DataSolo\G14.json` — display values with sources. Values the spec doesn't give are marked [임시].
- `D:\algorithm_game\solo\Assets\Tests\EditMode\ViewWorld\` — `WorldViewRulesTests.cs`, `ArenaSceneTests.cs`, `WorldLayerBuildTests.cs`, `TestArt.cs`.
  - Every test vector in the brief is ported; expected values come from the spec summaries.
  - The layer-build tests use the real ArtLibrary with the test assembly's `DiskArtSource` and the real `App.SpritePool`.

**compile.sh result**
`bash Tools/compile.sh -f Assets/Scripts/View/World/,Assets/Tests/EditMode/ViewWorld/` → exit 0, also 0 errors and 0 warnings with `-w`. `Assets/Scripts` compiled in "reduced" mode: 6 files from other teams (Hud, Bosses) were excluded because they are currently broken.

**Contract changes needed:** none.

**Assumptions about other modules**
- Run fills these when entering camp (Camp/StartCamp): `CampFirePos`, `DepartMarkerPos`, the two radii, and `NextPlan.Tod`. It also fills `DepartHoldStartTick` during DepartHold.
- Displayed time of day is read from state only:
  - In camp: `NextPlan.Tod`, falling back to `DayCycle.Tod(LastClearedWave + 1)`.
  - During a wave: `Wave.Plan.Tod` or `DayCycle.Tod(Wave.Wave)`.
  - The `TodChanged` event only triggers the transition to start.
- Waves keeps `Weather.Active`, `PropVariant` and `DiscardedItemVariant` up to date.
- In stance, `p.Facing` already holds the locked (and, in reverse stance, flipped) direction.
- The profile's `Cosmetics.Equipped` may hold `cosmetic.headwear.cracked_mirror_shard` under any key; I only check the values.

**Not implemented or not confirmed**
- The EditMode tests have not been run.
- Character reactive parts (steam, lid, lantern, eyes, etc. from `layers/reactive`) are not drawn. The brief's overlay list only named wild stain and the mirror shard.
- There is no art for the depart marker, so it is a ring plus a filled circle.
- Several values are guesses and need checking on screen (all [임시] in G14.json):
  - The tower window offset and the window's frame numbers (the window sprite has no pose frames).
  - Which of the three rim-light frames goes on which wall: I just alternate them.
  - Puddle and discarded-item positions, fog opacity, vignette strength, particle colours and counts.
- Rising after the first-encounter collapse has no clip of its own, so it plays the death clip in reverse.
- Diagonals use left/right first: NE and SE face right; NW and SW face right, flipped.

## G15 — 표시: 적·보스 몸, 예고 구역, 적 탄, 남는 구역, 장애물, 별똥

## G15 결과 보고

G15를 다 만들었습니다. compile.sh는 끝 코드 0입니다. EditMode 테스트는 Unity 러너로 돌리지 못했습니다(컴파일만 확인). 다만 순수 규칙 테스트 35개는 Unity 밖에서 반사 실행기로 따로 돌려 모두 통과했습니다.

**만든 파일**
- `D:\algorithm_game\solo\Assets\Scripts\View\Enemies\`
  - `ViewRegistry.Enemies.cs`: 네임스페이스 `Wizard.Solo.View`. `AddEnemies`는 `EnemyLayer`를, `AddThreats`는 `ThreatLayer`를 등록합니다.
  - `EnemyLayer.cs`
    - 적을 id로 추적합니다. 매 프레임 `w.Enemies`와 대조하므로 Build 직후 이벤트 없이도 집어 듭니다.
    - 몸 하나마다 SortingGroup 하나를 둡니다(`Bodies + YOrder`).
    - 클립: MonsterClip을 8fps로 돌리고, 빙결 중에는 멈춥니다. 좌우 반전, 비행 띄우기, 땅속 둔덕, 무기 자홍(덮기 색)을 넣었습니다.
    - 엘리트 금색 2px 외곽선은 MaterialPropertyBlock으로 넣습니다. 그 밖의 적은 시간대 림(tod.json의 %)을 씁니다.
    - 엘리트: 장식, 뿔, 작은 체력 바, 등장 기둥 빛.
    - 상태이상: 감속 고리 한 줄/두 줄(남은 시간만큼 짧아짐), 잠식 점선 테(메시)와 보라 금, 빙결 껍질과 깨짐, 기절 별, 북두꺼비 버프, 살얼음 눈금.
    - 등장 예고: Spawning 상태의 덤불 흔들림, 가시덩굴은 흙 금, EnemySpawnNotice도 받습니다.
    - 적중 2프레임 흰 번쩍과 2px 밀림.
    - 보스: 격노 림 2→6px(0.3초), 검은 김(초당 2×스택), 밤삼킴 마디·꼬리, 1.0초 느린 사망 연출 뒤 흰 번쩍. `w.Boss`가 null이 되면 접습니다.
    - 허수아비는 `facility.training_dummy` 그림을 씁니다.
  - `ThreatLayer.cs`
    - 예고 5모양을 두 층에 그립니다. TelegraphFill 층: 바닥 40% 어둡게, 빨강 채움, 색각 줄무늬, 앞선, 번쩍. TelegraphEdge 층: 3겹 테두리, 앞선 한 번 더, 번쩍 한도에 걸렸을 때의 흰 8px 테두리.
    - 채움 진하기와 색각 보조 옵션을 읽습니다. 고정 순간 떨림, 취소 흐림, 판정 터짐 그림, Harmless 구역은 위험색 없이 그립니다.
    - 적 탄: SpriteKey가 없으면 소 0.12/대 0.18 기본 탄. EnemyShots 띠, 포물선 높이 호, 막힌 탄 표시, 색각 보조 주황 줄무늬.
    - 남는 구역: 바닥 그림, 빨강 빗금, 3겹 테두리, 만료 1초 전 초당 2회 깜박. 서리밭은 자홍 테두리와 흰 빗금.
    - 장애물(boulder, ice_pillar)은 솟음 → 서 있음 → 깨짐/흐려짐 순서로 그립니다.
    - 별똥: 흰 금빛 점선 고리가 돌고, 떨어진 뒤 마지막 2초 깜빡입니다.
  - 보조 파일: `TelegraphPainter.cs`, `TelegraphGeometry.cs`, `FlashLimiter.cs`, `ShapeMeshBuilder.cs`, `DangerColor.cs`, `EnemyLook.cs`, `ViewParts.cs`, `EnemyViewTuning.cs`
- `D:\algorithm_game\solo\Assets\Resources\DataSolo\G15.json`: 표시 수치. 명세 값은 `_src`에 출처 절을 적었고, 나머지는 솔로 임시로 표시했습니다.
- `D:\algorithm_game\solo\Assets\Tests\EditMode\ViewEnemies\`
  - `TelegraphRulesTests.cs`: 순수 규칙 35개.
  - `ThreatLayerTests.cs`: 층 단위 13개(가짜 풀·그림 창고 위에서).
  - `ViewEnemiesFakes.cs`: FakeArt, FakePool, ViewRig.

**compile.sh 결과**
- `bash Tools/compile.sh -w -f Assets/Scripts/View/Enemies/,Assets/Tests/EditMode/ViewEnemies/` → 끝 코드 0, 오류 0, 경고 0. 네 단위 모두 ok.

**계약 변경 필요**
없습니다.

**다른 모듈에 기대한 가정**
1. **적중 흰 번쩍(G16과 겹침 주의):** EnemyDamaged의 적 몸 2프레임 흰 번쩍과 2px 밀림은 몸을 가진 이 층(G15)이 합니다. G16은 몸 번쩍을 하지 말아야 합니다.
2. **EnemySpawnNotice의 A:** e.Tick보다 크면 끝 틱으로, 아니면 길이(틱)로 읽습니다.
3. **HazardEntity Ring의 안쪽 반지름:** 안 반지름 필드가 없어서 안쪽 = Radius − Width로 봅니다.
4. **밤삼킴 꼬리:** `BossState.Segments`의 마지막 칸을 꼬리로 그립니다.
5. **보스 처치 신호:** EnemyKilled(B = 2) 또는 BossKilled 뒤에 `w.Boss`가 WaveClearing 동안 남아 있다고 봅니다.
6. **예고 표시 시각:** 마지막으로 처리한 틱 = `w.Tick − 1`로 봅니다. 차오름은 그 틱 + alpha로 이어지고, 번쩍은 처리한 틱이 D−2, D−1일 때입니다.
7. **번쩍 한도의 해석:** 이번 구역 넓이를 더해 518,400px²를 넘으면 테두리 강조로 바꿉니다. 그래서 한 장만으로 25%를 넘는 구역도 막힙니다.
8. **그림 이름:** 장애물 Kind `boulder`/`ice_pillar`는 effects의 `enemy/rock`, `enemy/ice_pillar`에 대응시켰습니다. 별똥은 world의 `field.meteor.fall`/`sparkle`과 status의 `meteor_ground`를 씁니다.

**구현하지 못했거나 확인하지 못한 것**
- **테스트를 Unity 러너로 돌리지 않았습니다.** Unity를 띄우지 않는다는 지시 때문입니다.
  - 순수 규칙 35개는 Unity 밖의 반사 실행기(dotnet, `SOLO_ASSETS_DIR` 지정)로 돌려 35개 모두 통과했습니다.
  - 층 단위 13개는 GameObject, SortingGroup, MaterialPropertyBlock이 필요해서 컴파일만 확인했습니다.
- **화면에서 본 것은 없습니다.** 다음은 통합 단계에서 실제 화면으로 확인해야 합니다:
  - SortingGroup 안의 그리기 순서
  - Tiled 모드로 그리는 남는 구역 띠 그림
  - 엘리트 장식(overlay)과 뿔의 붙는 위치(요약서에 위치 값이 없음)
  - 정점 색 메시의 색 공간
- **빠진 `.meta`:** 새 폴더와 파일의 `.meta`는 만들지 않았습니다. Unity가 임포트할 때 만듭니다.
- **솔로 임시 값:** 요약서에 값이 없는 것은 G15.json에 솔로 임시로 넣었습니다. QA에서 사용자가 정할 값입니다:
  - 깜박의 꺼진 쪽 진하기 0.3
  - 색각 줄무늬 색과 굵기·간격
  - 포물선 높이
  - 별똥 회전 속도
  - 장식이 머리에 묻히는 정도

## G16 — 표시: 내 탄, 적중·폭발·처치, 비기 연출, 피해 숫자, 약점 알아냄, 드랍, 소리 연결

G16 is done: the shot, effect and audio layers are registered and `compile.sh` exits 0. Of the 66 tests, the 49 pure-rule tests ran and passed outside Unity. The 17 layer tests are compile-only, because they need the Unity engine to run.

**Files** (all inside the owned folders)
- `D:\algorithm_game\solo\Assets\Scripts\View\Effects\`
  - `ViewRegistry.Effects.cs`: registers `AddShots`, `AddEffects` and `AddAudio`. `AddEffects` finds the ShotLayer already in the list and takes its shared state, so the particle budget and the overlap count are one per run.
  - Layers: `ShotLayer.cs`, `EffectLayer.cs`, `AudioLayer.cs`.
  - Pure rule classes: `EventBindings.cs`, `ParticleBudgetConfig.cs`, `ParticleBudget.cs`, `FlashGovernor.cs` (14.1 flash limits), `EffectTimers.cs` (graze pitch, hit-stop budget, gap gate, multi-kill), `DamageNumberRules.cs`, `OcclusionField.cs` (overlap counted by area), `EffectOptionRules.cs`, `ShotSpriteTable.cs`, `SoundEventMap.cs` (with `MusicRules`).
  - Runtime helpers: `EffectsTuning.cs`, `PresentationData.cs`, `EffectsShared.cs`, `FxParticles.cs`, `FxSprites.cs`, `WorldTextPopups.cs`, `ScreenOverlay.cs`, `FxUtil.cs`.
- `D:\algorithm_game\solo\Assets\Resources\DataSolo\G16.json`
- `D:\algorithm_game\solo\Assets\Tests\EditMode\ViewEffects\`: `ViewEffectsTestData.cs`, `ViewEffectsRuleTests.cs`, `ViewEffectsLayerTests.cs`.

**compile.sh**
- `bash Tools/compile.sh -f Assets/Scripts/View/Effects/,Assets/Tests/EditMode/ViewEffects/` exits 0, with no errors or warnings in my folders.
- On the last run, Assets/Scripts was "reduced": compile.sh left out other modules' broken files in Screens/Enhance and Sim/Bosses. None of them are mine.

**Tests**
- I built a throwaway reflection runner in the scratchpad (`scratchpad\g16runner`) and ran the rule tests against the compiled dlls: 49/49 pass. This was not the Unity test runner.
- The 17 layer tests need Unity (they stop with SecurityException outside it). They cover:
  - building from current state only (shots in flight, zones already placed, drops)
  - shot and zone opacity options
  - trail lengths and depth dots
  - the affinity glyph lasting 90 ticks
  - damage-number modes, ground marks off, kill shards smoke-only, art name difficulty-5-only
  - the player-hit display
  - hit-stop 30 / 80 ms
  - sound buses and music
- They need a run with `Tools/unity-test-edit.sh`.
- I fixed one test whose expected value I had calculated wrongly myself. No expected values were copied from implementation output.

**Contract change needed** (Core untouched; worked around in my own folder)
1. `SimEventType.EnemyDamaged` should also fill `Slot = HitInfo.AttackerSlot`. For now hit-particle counts use player slot 0's wand stage.
2. There is no event for the recoil shot breaking a wolf's dash. Request: `EnemyDashCancelled` (E = enemy id, S = slot). Without it, the "끊음" mark, its small shake and sound T-18 cannot be connected.
3. There is no event for the shadow orb passing through an enemy. Request: `ProjectilePassed` (E = projectile id, A = enemy id, P). Without it, the per-enemy ripple and sound C-24 are missing.
4. The full-screen flash governor lives in G16. Other modules that flash the whole screen (G25 first meeting and revenge) would have to share it. Proposal: `ViewContext.Flash` as `System.Func<FlashRequest, FlashResult>`, with the governor owned by the host.

**Assumptions about other modules**
- `ArtFired.Id` is the art that actually fired. Bloom's `ArtFired.F` is its wave count, and `ArtPulse.A` is a 1-based wave number; that is how the last wave is detected.
- Spear, thunder lance and bolt dash emit `ArtLine`.
- `GoldGained.Pos` is (−1, −1) when gold has no place on the field.
- Arts sets `PlayerZoneEntity.Radius`, `Width` and `Facing`.
- G15 draws the enemy's white hit flash and 2px push, and stun/freeze states.
- G14 draws wand-tip and afterimage effects. I put the crit muzzle at player position + facing.
- G17 draws the red corners at 1 HP and the HUD.
- The host already applies shake scale, hit-stop on/off and effects level. G16 enforces the 0.1 s per second hit-stop budget itself; player hit bypasses it.
- Sound ids are event_bindings keys and only play when the line has sfx. Two choices to check: graze lock-break plays `art.vent_burst.lock_broken`, and armor-blocked hits play `art.piercing_lance.armor_break`, because those are the only lines carrying sfx_c11 and sfx_h02.
- Music ids: `bgm.` + tod id (e.g. `bgm.tod.day`), plus `bgm.boss`, `bgm.phantom_first`, `bgm.camp`, `bgm.practice` and `bgm.run_end`.

**Solo-temp values in G16.json** (no source value)
- damage number lifetime 0.8 s, rise 0.6 cells, fade 0.25 s
- 3 gold coins per kill
- chain-hop pitch +1 semitone per hop
- neutral element secondary colour; neutral uses the arcane effect art
- when crit and weak land together, the crit style wins

**Not done or not checked**
- The layer tests have not run in Unity, and nothing has been seen on screen.
- The recoil-break and shadow-orb-pass effects are missing until events 2 and 3 exist.
- The skill-preparation visuals (spear aim line, bloom gather) are missing because no event marks that moment.
- I created no `.meta` files; Unity will generate them on import.

## G17 — 전투 HUD와 캐릭터 둘레 표시

G17 (전투 HUD와 캐릭터 둘레 표시) 구현을 마쳤다. compile.sh는 끝 코드 0이고, 테스트는 컴파일만 확인했다(Unity를 띄우지 않아 실행하지 않음).

**만든 파일** (D:\algorithm_game\solo\Assets\ 아래)
- Scripts/View/Hud/ViewRegistry.Hud.cs: `AddHud` → `layers.Add(new Hud.HudLayer())` (네임스페이스 Wizard.Solo.View)
- Scripts/View/Hud/HudLayer.cs: IWorldViewLayer. Build 때 이벤트 없이 지금 상태로 한 번 그린다. HUD는 전부 vc.HudRoot 아래 "BattleHud" 노드에 있다.
- Scripts/View/Hud/HudModel.cs: 순수 계산. `HudFrame Compute(w, profile, db, options)`, 열 링 상태, 늦음 올림, 지금 K 글, 콤보 막대·결정 직전, 보스 눈금, 출발 안내, Practice 숨김, 허상까지 n웨이브(`RevengeTracker.Build(..., metThisRun: w.Run.RevengeActive).HudWavesLeft`).
- Scripts/View/Hud/HudLayout.cs: 08 요약서 6.1절 좌표. 요약서에 있는 값만 둔다.
- Scripts/View/Hud/HudTuning.cs, Resources/DataSolo/G17.json: 요약서에 값이 없는 솔로 임시 값.
- Scripts/View/Hud/HudColors.cs, HudArt.cs, HudMeshBuilder.cs
- 패널: HudTopPanel.cs(진행 바, 날·웨이브, 시간대·변덕 아이콘, 경과 시간, 허상 줄, 훈수), HudBossPanel.cs, HudStatusPanel.cs(체력, 초상·단계, 지갑·점수·처치), HudCircuitPanel.cs, HudNoticePanel.cs
- 캐릭터 둘레: CharacterMarksView.cs(결정 칸과 2.3절 사건 연출, 최근 입력 5개, 지금 K 두 줄·대기 테두리·보관 점, 머리 위 늦음 문구, 웨이브 끝 환전), ComboView.cs. 둘 다 WorldTextRoot 아래 WorldToHud 자리에 있다. 열 링과 시야 화살표는 HeatRingView.cs가 전장 층 PlayerMarks에 정점 색 메시로 그린다(EffectsLevel과 무관).
- 테스트 Tests/EditMode/Hud/: HudModelTests.cs, HudLayerTests.cs, HudFakes.cs
  - 옮긴 벡터: TV-042~049, 02 6.6절 열 링 경계(임계 시작 − 15, 95, 정확히 100), 04 6.1절 늦음 올림(153/307/1075/1u), 콤보 막대 비율, 결정 직전.
  - 계약 벡터: 프로필은 만나기 전이고 RevengeActive가 켜진 판의 웨이브 14 → 6, Boss가 null이면 보스 표시 없음, ComboPosition 두 자리, 출발 안내는 반경 안에서만, Practice에서 웨이브·지갑 숨김.
- 새 이름 열쇠는 필요 없어서 NamesSolo/G17.ko.json은 만들지 않았다.

**compile.sh 결과**
`bash Tools/compile.sh -f Assets/Scripts/View/Hud/,Assets/Tests/EditMode/Hud/` → 끝 코드 0, 필터 안 오류 0. `-w`로 돌렸을 때 내 폴더의 경고도 0이었다. Wizard.Solo 단위는 "reduced"로 나온다: 남의 깨진 파일 3개(View/Effects 쪽)를 빼고 만들었다.

**계약 변경 필요**
- 없음.
- 덧붙일 만한 것: 밑줄 칸을 담은 상태 필드가 없다. 그래서 Build 직후(보조 판에서 돌아올 때) 밑줄 표식은 다음 `UnderlineSlotChanged` 이벤트가 올 때까지 안 보인다. 필요하면 `PlayerState.UnderlineSlot`(int, -1 = 없음)을 더해 달라.

**다른 모듈에 기대한 가정**
- G03: `Arts.Preview(w, p, w.NowU)`와 `GetRecentInputs`를 매 프레임 부른다. `LateSeconds`와 `ArtFizzled.F`는 초 단위로 본다(올림 전 값이든 후 값이든 둘 다 처리한다).
- G01: 열 링 상태는 상태 필드(`HeatHu`, `Derived.CritStartHu`, 없으면 `Def.CritStart`, `Locked`)로 HUD가 직접 계산한다. `Player.RingState` 기본 구현이 늘 Cold를 돌려줘서다. 경계는 요약서 그대로다.
- G16: 처치 수는 활동 중인 플레이어들의 `p.Kills` 합이고, 0이면 `Run.TotalKills`를 쓴다. 머리 위 "n번째 입력이 x초 늦음"은 HUD가 그린다. G16의 기술 이름 팝업과 자리가 겹치면 조정이 필요하다.
- G09: 날·웨이브 줄은 `w.Run.Wave`, 시간대와 변덕은 `w.Wave.Plan` 또는 `Run.NextPlan`에서 읽는다(Wave가 같은 쪽).
- G14: 출발 표식 판정은 `p.Pos`와 `DepartMarkerPos`의 거리 ≤ `DepartMarkerRadius`(경계 포함)이다.
- G05/G04: `UnderlineSlotChanged`의 A는 회로 칸 번호(0 = 코어)로 본다.
- 소리: `ui.hud.*`를 `ctx.Sound.Play(id, SoundBus.Ui)`로 직접 낸다. 단 `tutorial_step`, `phantom_line`, `damage_number`는 다른 작업 몫이라 내지 않는다.

**구현하지 못했거나 확인하지 못한 것**
- EditMode 테스트를 실제로 돌리지 않았다. 레이어 테스트는 GameObject와 uGUI를 만든다.
- 화면에서 눈으로 본 적이 없다: 좌표, 메시 링, 대체 도형.
- 요약서끼리 겹치는 곳: 보스 웨이브의 날·웨이브 줄 (760,76) 400×24가 경과 시간 (912,80)과 겹친다. 솔로 임시로 보스 웨이브에서는 경과 시간을 y 104로 내렸다(G17.json `boss_wave_elapsed_y`).
- 시간대와 변덕 아이콘은 그림이 없어 창고의 대체 그림(바둑판)으로 나온다.
- 요약서에 값이 없어 솔로 임시로 정한 것(전부 G17.json):
  - 안내 길이
  - 훈수 줄의 자리와 길이
  - 콤보 HUD 자리, 결정 줄 높이
  - 회로가 켜지는 간격, 깨짐·서리 방패·환전 길이
  - 화살표 반지름·크기, 보통 시야 반투명 0.5
  - 콤보 "속성 보조색"은 값이 없어 흰색과 속성색의 중간으로 만들었다(코드).
- 하지 않은 것: 제자 1의 잠금 대사(`heat_lock_line.char.1`)와 그믐 눈빛·결정 그림자(캐릭터 그림 쪽이라 G14 몫으로 봄). 캐릭터 대사는 요약서 6절 범위 밖이다.

## G18 — 화면: 첫 실행, 타이틀, 프로필 고르기, 오프라인 안내, 메인 메뉴(자동 정산 알림)

G18 is done. `compile.sh -f Assets/Scripts/Screens/Start/,Assets/Tests/EditMode/ScreensStart/` exits 0, with all four units ok. I only compiled the tests; none have been run, because there is no test runner outside Unity and I did not launch it.

**Files made**
- `Assets/Scripts/Screens/Start/` (namespace `Wizard.Solo.Screens.Start`):
  - `ScreenRegistry.Start.cs`: `RegisterStart` in `Wizard.Solo.UI`, registering the 5 screens.
  - Models with no engine code: `StartFlow`, `StartNav`, `FirstRunPromptModel`, `TitleModel`, `ProfileSelectModel`, `OfflineGuideModel`, `MainMenuModel` (with `MainMenuAction`, `MainMenuEntry`, `MenuTimeOfDay`), `PendingSettledModel`, `StartTuning`, `StartNameKeys` (also holds `StartSounds`).
  - Views: `FirstRunPromptScreen`, `TitleScreen`, `ProfileSelectScreen`, `OfflineGuideScreen`, `MainMenuScreen`, `PendingSettledPanel`.
- `Assets/Resources/NamesSolo/G18.ko.json`: the `str.main_menu.pending_settled.*` keys and `str.main_menu.exit_confirm`.
- `Assets/Tests/EditMode/ScreensStart/`: `StartTestFakes.cs`, `MainMenuModelTests.cs`, `StartFlowTests.cs`, `PendingSettledModelTests.cs`.
  - Brief vectors: TV-001/003, TV-002, TV-005, TV-026, TV-028, TV-133, TV-237.
  - Other cases: tracker progress capped at 19; no tracker line before meeting or after revenge is done; best-wave line; menu order; no duel-practice entry (D-51); each entry's target screen and typed args; PendingSettled passing through to MainMenu and alerting once; an empty settlement still gives the alert line; profile-select cards; title pulse; menu time of day; G18 keys present.

**Behaviour you might not expect**
- **Settlement alert is not a separate screen.** It is an overlay inside MainMenu (dim layer plus paper panel), because there is no `ScreenId` for it. MainMenu sends keys to it first.
- **Main menu is always the bottom screen.** The start flow reaches MainMenu with `PopTo`. Esc on MainMenu and Title does nothing.
- **Exit asks first.** [나가기] opens a confirm dialog, then calls `Application.Quit` (N-02 / D-36).
- **ProfileSelect carries PendingSettled.** The 8.1 table lists no args for ProfileSelect, but I pass it through (same key and type) so the alert isn't lost when the user goes Title → ProfileSelect → MainMenu.
- **Pop doesn't re-enter MainMenu.** `OnEnter` isn't called again when a child screen pops, so MainMenu redraws on `ctx.Profiles.Saved`.

**Contract changes needed**
- None blocking.
- Optional: add `UiEntry.CompositeAnchor` (`PixelPoint`, `HasCompositeAnchor`) for the manifest's `composite_anchor`. The menu campfire position (1236,780) is hard-coded in `MainMenuScreen` for now; `sprite_origin` is read from the manifest.
- Optional: a `ScreenId.PendingSettled` if the alert should become its own screen.

**Assumptions about other modules**
- Bootstrap (F5) opens the first screen with `ProfileRestored` and `PendingSettled`. I checked `Bootstrap.FirstScreen` and `FirstScreenArgs`.
- WandPicker (G23) reads `Character` (int). Camp (G21) defaults to `last_character` when it gets only `CampMode.Library`. DummyPractice (G21) handles the empty-core case (D-56).
- Whoever ends a run calls `Session.EndRun()` before returning to MainMenu. If `HasRun` stays true, the enhance item is hidden (TV-133).
- `SettlementResult.Record.ReachedWave` is the checkpoint wave shown in the alert's first line.

**Not done or not checked**
- `Resources/DataSolo/G18.json` is outside my owned paths, so I couldn't create it. The pulse period (2s), the minimum brightness (0.5, my own temporary value) and the day/dusk/night hours (6/17/20) are defaults in `StartTuning`. They are read from `SoloExtra("G18")["start"]` if that file is added.
- Not checked: whether the I-F12 badge image and the S-0202 title image already contain text. I put a name-table label on the badge; the title shows the image, or a label if there's no art.
- No `.meta` files were made; Unity will generate them at integration.

## G19 — 화면: 원정 준비(캐릭터·지팡이·출발 조립·시작 웨이브)

G19 (원정 준비 화면) 결과 보고

만든 파일
- D:\algorithm_game\solo\Assets\Scripts\Screens\Prep\ScreenRegistry.Prep.cs — RegisterPrep → ExpeditionPrepScreen 등록 (namespace Wizard.Solo.UI)
- D:\algorithm_game\solo\Assets\Scripts\Screens\Prep\ExpeditionPrepScreen.cs — 화면 뷰(ScreenBase). 08 5.2절 배치: 캐릭터 카드 5장(선택 그림, 속성 문양 20%, 이름, 단계 칭호, 확정 줄), 능력치 11줄 + 패시브·지팡이 특성·고유 비기, 지팡이 판(목록 스크롤, 정렬 버튼, 큰 그림, 이름·별명·단계·변이·확장 슬롯·흔적, [든 지팡이로]), 출발 조립 판(코어·연결·확장 칸, 코어 빔 경고, [조립 바꾸기] → Push(Camp, LoadoutEdit + WandId long), OnResult와 Tick으로 돌아오면 다시 읽음), 복수 추적기 한 줄, 시작 웨이브 ◀▶ 줄(선택지가 0뿐이면 숨김, 점수 안내), 출발. Q/E = 캐릭터 바꾸기, K = 닫기. ui.prep.* 소리를 그 시점에 냄.
- D:\algorithm_game\solo\Assets\Scripts\Screens\Prep\ExpeditionPrepModel.cs — 엔진 없는 모델: 선택 상태, last_character 저장, 목록 다시 읽기, Equip, 정렬, 시작 웨이브 선택지/거절, SkippedScore(ctx.Rules.Score.SkipScore를 1..skip까지 합), CanDepart(코어 → 원정 잠금 순), Depart(BuildRunConfig(int, long, skip+1, seed) → Tutorial 또는 Loading→Battle 인자), ChangeLoadoutArgs, 추적기(RevengeTracker.Build), 칭호·별명 열쇠
- D:\algorithm_game\solo\Assets\Scripts\Screens\Prep\WandListModel.cs — SortRows(든 지팡이 맨 위, 동률 깨기 단계→얻은 시각→id)
- D:\algorithm_game\solo\Assets\Scripts\Screens\Prep\PrepStats.cs, PrepKeys.cs, PrepErrorKeys.cs
- D:\algorithm_game\solo\Assets\Resources\NamesSolo\G19.ko.json — 새 열쇠 2개: str.expedition_prep.mutations, str.expedition_prep.scars
- D:\algorithm_game\solo\Assets\Tests\EditMode\ScreensPrep\ExpeditionPrepModelTests.cs, PrepStatsTests.cs, PrepFakes.cs — 48개 테스트: TV-029~037, TV-237, 01 T080~T083·T085, T084(최고 40에서 skip 20 허용만. 시즌·순위 부분은 솔로에 순위가 없어서 뺐고, 까닭은 주석에 적음), 5.4 CanDepart 표의 모든 줄, 계약 벡터(RunConfig 열쇠와 형, Loading의 NextScreen=Battle, 튜토리얼 전이면 Tutorial, CampMode.LoadoutEdit와 WandId long, last_character 저장과 Save, BuildRunConfig에 int 캐릭터, 추적기 줄이 보이는지/숨는지), 서비스가 null일 때, 능력치 값(02 8.2절), 칭호 단계 구간. TV-038(DuelEntry)은 D-51 때문에 뺐고, 까닭은 테스트 클래스 주석에 적음.

compile.sh 결과
- `bash Tools/compile.sh -f Assets/Scripts/Screens/Prep/,Assets/Tests/EditMode/ScreensPrep/` → 끝 코드 0 (오류 0, 네 단위 모두 ok).
- 테스트는 Unity 러너로 돌리지 않았다. 대신 스크래치패드에 만든 dotnet 반사 실행기(C:\Users\lovey\AppData\Local\Temp\claude\afd9546682f5c3c2\d0fd5536-302e-40ee-8ada-6ceaddd9e028\scratchpad\g19\runner)로 compile.sh 출력 dll을 돌려 48개 전부 통과했다(SOLO_ASSETS_DIR로 실제 JSON과 이름 표를 읽음). Unity EditMode 러너에서의 확인은 통합 단계에 남아 있다.
- 실수 보고: 스크래치패드의 runner\ 폴더가 다른 에이전트 것인 줄 모르고 그 안에 Program.cs와 runner.csproj를 썼다가 g19\runner\로 옮겼다. 그 폴더에서 한 번 실패한 빌드가 obj\를 만들었을 수 있다(원래 있던 것인지 내가 만든 것인지 가리지 못해 지우지 않았다). 또 compile.sh 출력을 scratchpad\out에 한 번 썼는데, 원래 다른 에이전트 것이었다면 덮어썼을 수 있다.

계약 변경 필요
- 지팡이 목록 정렬을 기기에 기억하는 자리가 없다(08 5.3절 "키 wand_list.sort"). SettingsOptions 표에 열쇠가 없고 Set은 표에 없는 열쇠를 무시한다. 그래서 지금은 화면 객체가 살아 있는 동안만 기억한다. 요청: `OptionKeys.WandListSort = "wand_list.sort"`(고르기형, Choices = recent/stage_desc/stage_asc, 기본 stage_desc)를 SettingsOptions 표에 더한다(설정 화면에는 줄을 그리지 않는 숨김 옵션).
- WandSort에 IlDesc가 없다. 08 5.3절에는 있지만 솔로에는 IL이 없어 정렬은 셋만 쓴다. 바꿀 필요는 없고 알림만 남긴다.

다른 모듈에 기대한 가정
- IWandService.List(int, sort)는 그 캐릭터의 줄만 주고 Equipped/Locked/CoreEmpty를 채운다. 모델은 받은 목록에 순서 규칙을 한 번 더 걸고, 코어 빔은 Loadout.Core를 직접 본다.
- ISettlementService.StartSkipOptions(bestWave)가 01 4.6절 규칙(0 포함)대로 준다. 0이 없으면 모델이 0을 앞에 넣는다.
- Rules.Score.SkipScore(db, w)는 웨이브 하나의 점수다. 합은 모델이 1..skip으로 낸다(G11).
- Camp(LoadoutEdit)는 Pop으로 닫힌다. 그때 args.OnResult가 불리고, 그게 없어도 Top이 다시 ExpeditionPrep이 되면 목록을 다시 읽는다(G21).
- Tutorial은 RunConfig 인자를 받아 끝나면 Loading→Battle로 간다(G25). 씨앗은 ctx.Clock.UtcNow.Ticks이고, 시계가 없으면 DateTime.UtcNow를 쓴다.
- BuildRunConfig가 실패하면 "error.<snake_case>" 문구를 토스트로 띄운다.
- UI 키트의 변형 이름은 manifest 파일 이름에서 추측했다: S-0601 character_card normal/selected, S-0602 wand_pedestal, K-62 wand_slot filled/expedition_locked, K-63 core_slot, K-60 item_slot, K-64 extension_slot open, I-S1/<stat>, I-G2/scar. 그림이 없으면 단색으로 그린다.

구현하지 못했거나 확인하지 못한 것
- 화면 뷰(ExpeditionPrepScreen)는 컴파일만 확인했다. 실제 그려진 모습, 커서 이동, 마우스 동작은 Unity에서 보지 못했다(PlayMode나 통합 단계에서 봐야 한다).
- 새 파일의 .meta는 만들지 않았다(Unity가 임포트할 때 만든다).
- [출발]이 꺼진 상태에서 J를 누르면 ui.prep.depart_blocked를 내고 [조립 바꾸기]로 커서를 옮긴다. 마우스로 꺼진 버튼을 누르면 위젯 기본 소리(disabled_press)만 난다.
- 지팡이 IL 표시는 솔로에 없어 넣지 않았다.

## G20 — 화면: 판 진행(Battle), 일시정지(판 요약), 조작 보기

G20 (Battle, Pause, ControlsView) is built and compiles. I could not run the tests: there is no runner outside Unity, so they are compiled only, not run.

**compile.sh**: `bash D:/algorithm_game/solo/Tools/compile.sh -f Assets/Scripts/Screens/Battle/,Assets/Tests/EditMode/ScreensBattle/` ended with code 0 (0 errors in the filter). Other teams' files were left out of that build: Prep ×4, Meta/RecordsModel.cs and Tests/EditMode/ScreensPrep/ExpeditionPrepModelTests.cs. None of mine were.

**Files made**
- Under D:\algorithm_game\solo\Assets\Scripts\Screens\Battle\:
  - `ScreenRegistry.Battle.cs` — `RegisterBattle` registers Battle, Pause and ControlsView.
  - `BattleFlowModel.cs` — pure rules: when to open or close LootPick and Camp/ArtTrie, checkpoint waves, `RecordArtUnlock` with an int character, settlement after HpZero (2.0 s, D-21) or Abandon (no delay), and `CanRequestPause` (D-20).
  - `BattleController.cs` — pure. On enter: `StartRun`, `InputScreen` = Battle (None on exit), subscribes to `SimEventRaised` and `PauseRequested`. It ignores events from the sub-run world. `RequestPause()` is the only pause path; it sets `Paused = true`, pushes Pause, and never calls Arts.
  - `BattleScreen.cs` — draws no frame, only the death line. `OnBack` calls `RequestPause` and always returns true.
  - `PauseMenuModel.cs` — the five items and `PauseController` (Resume, Settings, ControlsView, give-up confirm leading to the GiveUp command, Paused = false and Pop; "to main menu" takes the same path).
  - `PauseSummaryModel.cs` and `PauseScreen.cs` — Modal: 480×560 menu in the centre and a 400×560 summary on the left. The paused-time line refreshes every frame. Esc and K both act as Resume.
  - `ControlsViewModel.cs` and `ControlsViewScreen.cs` — Modal 960×640: 9 rows showing the current key layout from settings; the pause row shows Esc plus the rebound key.
- Tests, under D:\algorithm_game\solo\Assets\Tests\EditMode\ScreensBattle\: `BattleFakes.cs`, `BattleFlowModelTests.cs` and `BattleControllerTests.cs` (the last also holds `ControlsViewModelTests`).
  - They cover every vector in the brief: the transition table, the 264-tick WaveClearing case, saves at waves 5/10/15/20, the pause menu items, Esc and `PauseRequested` in one frame pushing Pause once, no Arts call, Esc in Pause doing Paused false + Pop, sub-run events being ignored, `InputScreen`, int character, the paused-time text, give up leading to settlement, and the key table.
  - Vector ids are my own (`S00_FLOW01`, `S08_7_PAUSE01` and so on). Spec 08 section 19 has no solo pause vector numbers.
- D:\algorithm_game\solo\Assets\Resources\NamesSolo\G20.ko.json — five new keys: `str.pause.summary.character/loadout/title/wand` and `str.pause.time.min_sec` ("{m}분 {s}초"). Under 60 seconds the existing `str.format.seconds` is used.

**Contract changes needed**: none.

**Data request**: the checkpoint interval (D-23, "multiple of wave 5") is not in any data file, and `Resources/DataSolo/G20.json` is outside my folders. The code reads `ctx.Content.SoloExtra("G20")["checkpoint_wave_interval"]` (int) and falls back to the constant 5 if it is missing. Adding `Resources/DataSolo/G20.json` with `{"checkpoint_wave_interval": 5}` would move the value into data.

**What I assumed about other modules**
- Session (F5): `SimEventRaised(RunEnded)` fires before `LastSummary` is set. I go to Settlement on the next Battle `Tick` and read `LastSummary` then, falling back to `MainWorld.Mods.Run.BuildSummary` if it is null. Calling `Paused` set already resets commands and resyncs.
- Run (G10):
  - It sends `PhaseChanged` with A = new phase and B = old phase.
  - It sets `CampSub = CampScreen` and sends `CampChanged`, and keeps `RunState.Score` current.
  - The `GiveUp` command produces `RunEnded(A = Abandon)`.
- Bosses: reflection and collapse set `InputLocked`. I also block pause in the PhantomFinale and PhantomDowned phases.
- ArtUnlocked: `Slot` is the player slot. The player is read from `Players[Slot].CharId`, falling back to `Config.CharId`.
- Settlement (G22) calls `EndRun` after Commit; Battle does not call it.
- Camp (G21): when the Camp screen closes itself it sends `CampClose`. Battle only does `PopTo(Battle)` when the sim says the camp screen should be shut.
- `ConfirmDialog` closes itself before calling `onResult`, so Pause is on top when the confirm result arrives.
- I add the `Settings` and `ControlsView` arguments as `ScreenArgs.From(Pause)` with no keys.
- ControlsView builds key labels with `UiKeyLabels` and widens the cap for long labels, using sprite K-51 "keycap".

**Not implemented or not checked**
- No EditMode test or Unity layout check was run.
- `.meta` files for the new folders and files were not created; Unity will create them.
- The pause and controls screens draw no frame or picture of their own, apart from the existing portrait, wand icon and block icons.

## G21 — 화면: 전리품 고르기, 야영·서고 조립(모드 넷, 트리 미리보기), 비기 트라이, 허수아비 연습

G21 is in place. `compile.sh -f Assets/Scripts/Screens/Camp/,Assets/Tests/EditMode/ScreensCamp/` exits 0, and every unit compiled (`ok`).

**Tests**
- 53 model and session tests pass. I ran them with a reflection runner I wrote in my scratchpad, outside Unity, so the Unity test runner has not run them yet.
- 4 screen smoke tests (`CampScreenSmokeTests`) build real GameObjects, so they cannot run outside Unity. They are compile-checked only and are still to be run in the integration step.

**Files I made**
- `Assets/Scripts/Screens/Camp/` (namespace `Wizard.Solo.Screens.Camp`):
  - Registration: `ScreenRegistry.Camp.cs` implements `RegisterCamp`, registering LootPick, Camp, ArtTrie and DummyPractice.
  - Screens: `LootPickScreen.cs`, `CampScreen.cs` (all four modes), `ArtTrieScreen.cs`, `DummyPracticeScreen.cs`.
  - Shared views: `ArtTrieView.cs`, `CampViews.cs` (wand circuit, tree preview, block cells, dashed frame).
  - Engine-free models: `LootPickModel.cs`, `CampModel.cs` (mode layout, heal, bag preview, next wave, guaranteed materials, stat highlight), `CampAssembly.cs` (in-run edits by `RunCommand`; out-of-run edits saved with `SetLoadout`), `TreePreviewModel.cs`, `ArtTrieModel.cs`, `PracticeModel.cs`, `PracticeSession.cs` (session calls).
  - Helpers: `SpellPreview.cs` (the single preview path ComputeDerived → CompileContext → Compile → ComputeStats), `CampFormat.cs`, `SlotRefs.cs`.
- `Assets/Tests/EditMode/ScreensCamp/`: `LootPickModelTests.cs`, `CampModelTests.cs`, `TreePreviewTests.cs`, `ArtTrieAndPracticeTests.cs`, `CampScreenSmokeTests.cs`, `CampFakes.cs`.
- `Assets/Resources/NamesSolo/G21.ko.json`: 41 new keys, sorted, none duplicate an existing key.

**Contract changes needed:** none. One thing for review: `SlotRef 200 + k` is used only inside this screen, for the out-of-run library cells, and is never sent as a command.

**What I assumed about other modules (checked against the code that exists now)**
- **Run:** `LootDiscard` A = 100 + i, `LootPick` A = card index, `ArtPick` takes Slot and Id, `CampHeal` takes Slot. The heal button shows `world.Mods.Run.HealCost`.
- **Battle:** it opens and closes LootPick and Camp on `PhaseChanged`/`CampChanged`. In Camp mode, Esc and K only send `CampClose`; the Pop is Battle's job.
- **Arts:**
  - Lock and unlock state comes from `DescribeTrie`.
  - For late K I use the `ArtFired` event: B = 1 means a just-timing hit and shows the white star.
  - "K was late" is detected when a normal art fires on a node whose just art is unlocked. The K time is approximated as the start of that event's tick.
- **Art unlock:** after sending `ArtPick`, the screen also calls `ctx.Settlement.RecordArtUnlock` so the profile is saved at once.
- **Session:**
  - While a practice sub-run is open, `World` is the sub world and `MainWorld` is the main run.
  - The Camp screen always reads `MainWorld`.
  - DummyPractice is a frameless Fullscreen screen, so the battlefield shows through. It treats J, K and arrows as battle keys and swallows them; only Q/E (switch character, MainMenu entry only) and Esc (exit) work as menu keys.

**Not done or not verified**
- None of the UI layout, drag-and-drop or rendering has been run; it is compile-checked only.
- TV-055–058 (card counts), TV-062's actual purse deduction, and TV-072 (59/60-tick depart hold) are Run-side rules. On the screen side, the counts are just whatever the loot offer holds, and I test only the reroll enable state and the 100 → 70 arithmetic.
- The 0.5-second stat highlight (30 ticks), the 0.1-second card reveal, and the layout pixel values are constants in code citing the spec section. `DataSolo/G21.json` was outside my owned folders, so I could not put them in data.
- The arts-tab effect lines are labels I made up from the non-zero numbers in `arts.json`, because the name table has no effect description text.
- The duel input-delay option and the bot-duel practice button are left out on purpose (D-51).

## G22 — 화면: 정산(기록, 귀환 가방, 가져가는 것)

G22 (정산 화면) is done. compile.sh exits 0 on both of my folders, and all 39 of my EditMode tests pass. I did not run them with the Unity runner: I used a small .NET reflection runner I wrote in the scratchpad, against the dll that compile.sh produced. They still need a run through `Tools/unity-test-edit.sh` at integration.

**Files made**
- `D:\algorithm_game\solo\Assets\Scripts\Screens\Settlement\ScreenRegistry.Settlement.cs` — `RegisterSettlement` registers `SettlementScreen`.
- `D:\algorithm_game\solo\Assets\Scripts\Screens\Settlement\SettlementScreen.cs` — the three-page view (record, return bag, takeaways) and the end buttons.
- `D:\algorithm_game\solo\Assets\Scripts\Screens\Settlement\SettlementModel.cs` — engine-free rules:
  - page changes, with Commit called exactly once even when going back;
  - bag toggle checks (borrowed, full, hero limit, locked) and auto-fill;
  - leftover gold preview;
  - revenge tracker, built only through `RevengeTracker.Build`;
  - `ui.*` sounds and Esc handling.
- `D:\algorithm_game\solo\Assets\Scripts\Screens\Settlement\SettlementRules.cs` — score rows, record line, 8-per-row bag layout, leftover gold, material split.
- `D:\algorithm_game\solo\Assets\Scripts\Screens\Settlement\SettlementNav.cs` — end-button navigation.
- `D:\algorithm_game\solo\Assets\Scripts\Screens\Settlement\SettlementNameKeys.cs` — name keys and sound ids.
- `D:\algorithm_game\solo\Assets\Resources\NamesSolo\G22.ko.json` — 16 new keys, sorted, none duplicating the original names file.
- `D:\algorithm_game\solo\Assets\Tests\EditMode\ScreensSettlement\SettlementFakes.cs` and `SettlementModelTests.cs`.
- Vectors moved to tests: S13 TV-154 to 164, TV-234/235, S09 T090/T100, S07 TV-236, plus every contract vector in the brief.

No `.meta` files were created; Unity will generate them.

**compile.sh result**
`bash Tools/compile.sh -f Assets/Scripts/Screens/Settlement/,Assets/Tests/EditMode/ScreensSettlement/` exits 0, and also with `-w` there are no diagnostics in my folders. Two other teams' broken files were left out of that build: `Screens/Story/StoryLayer.cs` and `Tests/EditMode/ViewEffects/ViewEffectsRuleTests.cs`.

**Contract changes needed**: none.

**Assumptions about other modules**
- **Battle (G20)** opens Settlement with `ScreenArgKeys.RunSummary`. If the argument is missing, the screen falls back to `Session.LastSummary`. The run is still alive when Settlement opens, and I call `Session.EndRun()` right after Commit.
- **Settlement service (G11)**:
  - `Prepare().BagCandidates` leaves out borrowed and library blocks.
  - Revenge reward materials come back with `Bound = true`, crow bundle materials with `Source == CrowBundle`, and drawn materials with `Guaranteed = false`.
  - `NewTitles` includes the mirror-breaker title, which I hide from the general new-titles row because it already shows in the revenge row.
  - A failed Commit is not retried: the screen goes to page 3 with no result and the end buttons still work.
- **Navigation**: every end button first does `PopTo(MainMenu)` and then pushes:
  - "다시 원정" goes through Loading (`ViaLoading`) to ExpeditionPrep;
  - "강화 화면으로" pushes WandPicker with `ReturnTo = MainMenu` (ScreenId) and `Character` as an int;
  - "메인 메뉴" stops at MainMenu.
- **G23** opens EnhanceTutorial from its profile flag. This screen only highlights the enhance button once and shows a one-line hint (D-67).

**Choices where the summaries were silent**
- Kills, boss, clear, fast, no-hit, graze and variance rows always show, even at 0. The phantom-first row only appears in a first-meeting run, and the skipped row only when the start wave is above 1.
- Before Commit, Esc does nothing (disabled sound). After Commit on page 3, Esc goes to the main menu. K/Q goes back a page, E is "다음".
- My owned folders don't include `Resources/DataSolo`, so I couldn't add timing values. The material bundle "opening" is therefore a "꾸러미 열기" button press, not a timed animation.
- The "복수 완료" form shows in the revenge reward row at the top of page 3. Page 1's tracker also switches to that form after Commit. Its sound plays once.

**Not implemented or not confirmed**
- The screen view itself (layout, uGUI drawing, focus movement) is only compiled. It has not been run or drawn on screen; that needs a PlayMode check at integration.
- The art IDs I used are confirmed in the manifest, but I haven't seen them drawn: S-2603 mirror, K-41 confirmation check, I-F9 lock, K-60 item slot.
- The weekly-challenge "0 bag slots" rule and the "pending verification" badge are not drawn. The first is out of scope (D-24), the second is excluded in solo.

## G23 — 화면: 강화(지팡이 고르기, 의식 상태 기계, 변이·새기기·떠나보내기, 첫 안내)

G23 (강화 화면 묶음) 구현을 끝냈다. compile.sh 끝 코드는 0이다. EditMode 테스트 100개 가운데 엔진 없이 돌릴 수 있는 88개는 scratchpad의 반사 실행기로 실제로 돌려 88개 모두 통과했다. GameObject로 화면을 조립하는 나머지 12개는 Unity 러너가 필요해 컴파일만 확인했다.

**만든 파일**
- `D:\algorithm_game\solo\Assets\Scripts\Screens\Enhance\`
  - 화면: `WandPickerScreen.cs`, `EnhanceScreen.cs`, `EnhanceScreen.View.cs`, `EnhanceScreen.Fx.cs`, `WandReleaseConfirmScreen.cs`, `MutationRemoveScreen.cs`, `SlotEngraveScreen.cs`, `EnhanceTutorialScreen.cs`
  - 엔진 없는 모델: `EnhanceScreenModel.cs`(상태 기계), `EnhanceRitualTimeline.cs`(의식 시간표), `EnhanceResultPlan.cs`(결과별 연출 구간), `ProbabilityDisplayModel.cs`, `ContinuousEnhanceModel.cs`, `EnhanceTutorialGate.cs`(EnhanceTutorialModel·EnhanceNav 포함), `EnhanceResultCardModel.cs`, `WandPickerModel.cs`(WandReleaseRules 포함), `EnhanceFlashGuard.cs`(번쩍·흔들림 한도), `EnhanceOwnedForecast.cs`("보유 재화로 평균 N번")
  - 보조: `EnhanceScreenTuning.cs`, `EnhanceSoundIds.cs`, `EnhanceNameKeys.cs`, `EnhanceFxPlayer.cs`, `EnhanceUi.cs`
  - 등록: `ScreenRegistry.Enhance.cs`(RegisterEnhance가 6개 화면을 등록)
- `D:\algorithm_game\solo\Assets\Resources\DataSolo\G23.json`: 시간표·광과민 한도·표시 수치. 출처는 `_note`에 적었다.
- `D:\algorithm_game\solo\Assets\Resources\NamesSolo\G23.ko.json`: 새 열쇠 27개.
  - 캐릭터 한마디(`str.enhance.char_line.*`)와 안내 본문(`str.enhance_tutorial.page.N.body`)은 값을 넣지 않았다. 값이 없으면 그 줄을 숨긴다.
- `D:\algorithm_game\solo\Assets\Tests\EditMode\ScreensEnhance\`: `EnhanceTimelineTests.cs`, `EnhanceStateMachineTests.cs`, `EnhanceDisplayTests.cs`, `EnhanceScreenContractTests.cs`, `EnhanceScreenFakes.cs`

**compile.sh 결과**
`bash Tools/compile.sh -f Assets/Scripts/Screens/Enhance/,Assets/Tests/EditMode/ScreensEnhance/` → 끝 코드 0. 오류 0개, 네 단위 모두 ok.

**계약 변경 필요**
1. `Result<long> IWandService.Release(IReadOnlyList<long> wandIds, string typedName)`: 계약의 `Release(ids)`는 이름을 받지 않아 +7 이상이면 늘 NameConfirmRequired가 난다. 지금은 G13의 확장 메서드 `Wizard.Solo.Meta.Inventory.WandReleaseExtensions.Release(this IWandService, ids, typedName)`를 쓴다.
2. `int IEnhanceService.AttemptsWithOwned(long wandId, int targetStage)`: G12 구현체에만 있다. 지금은 내 폴더의 `EnhanceOwnedForecast`가 고정 씨앗 몬테카를로로 따로 셈한다.
3. `OptionKeys.WandListSort`(값: stage_desc/stage_asc/recent): 고른 정렬을 기기에 기억하는 자리(`wand_list.sort`)가 없다. 지금은 화면 객체 안에만 남는다.

**다른 모듈에 기대한 가정**
- IEnhanceService(G12):
  - Preview의 SacrificePct에는 같은 속성 보너스가 들어 있다고 본다. 화면이 "제물 합"과 "같은 속성" 두 줄로 나눈다.
  - 변이 제시는 `PendingOffer`로 저장되고, ResolveMutation이 fallback 결과를 돌려준다고 본다.
- IWandService(G13): List가 든 지팡이를 맨 위에 둔다고 본다.
- 떠나보내기 환급 표시는 `Economy.ReleaseRefundByStage` 표를 읽는다.
- Art(F2): `Effect("enhance"/"ritual", 열쇠)`, `EffectInfo`(null이면 그림을 그리지 않음), `Background("enhance.workbench")`, `WandInspection`, `CharacterPortrait`.
- MainMenu와 Settlement가 WandPicker나 Enhance를 연다. 정산에서 열 때 ReturnTo가 없어도 Entry가 Settlement면 MainMenu로 돌아간다(D-34).

**구현하지 못했거나 확인하지 못한 것**
- 화면 조립 테스트 12개는 Unity 러너 없이는 돌지 않는다. 컴파일만 확인했다: 첫 안내 문, 화면 Esc, OnExit에서 Release("ritual") 1번, 시도·빠른 강화·변이 대기 화면 연기 시험, 강화 화면 확률 값, 고르기 화면 캐릭터 인자.
- 요약서끼리 맞지 않는 곳이 둘이다.
  - TV-089: 08은 93, 07(M-1, TV-P-04)은 88이다. 규칙(G12)과 같은 88로 시험했다. 사용자 확인이 필요하다.
  - 뽑힌 값 줄: TV-131을 따라 소수 한 자리로 보이고 뽑힌 값은 버림한다. 다만 이름 표 문구가 "이하면 성공"이고 실제 판정은 "미만"이라 경계값에서 안내가 틀린다(07 M-3). 문구 고치기는 이름 표 원본 쪽 일이다.
- `il_desc` 정렬(아이템 레벨 높은 순)은 넣지 않았다. 솔로에는 아이템 레벨 식이 정해져 있지 않다(07 3.4 미정).
- 변이 이득·대가 설명은 열쇠(`str.enhance.mutation_gain.<id>` / `str.enhance.mutation_cost.<id>`)만 정했고 값이 없어 줄이 숨는다.
- 대성공 둘째 숫자 내려찍기의 "3도 위" 소리는 맞는 사건 id가 없어 따로 내지 않는다. 제물 다섯 음은 음높이 인자로만 구분한다.
- 원문에 값이 없어 솔로 임시로 넣은 수치(G23.json `_note`에 표시): `mutation_accept_seconds` 0.5, `cost_shake_seconds` 0.3, `slam_settle_seconds` 0.2, `white_gold_mix` 0.5, `offer_semitones`.
- TV-135~140(기댓값 표·새기기 수치)은 규칙 서비스 몫이라 G12에 맡겼다. 화면은 그 값을 그대로 보여 준다. TV-143·145·147·151은 솔로나 이 화면의 범위가 아니다.
- 반사 실행기: `C:\Users\lovey\AppData\Local\Temp\claude\afd9546682f5c3c2\d0fd5536-302e-40ee-8ada-6ceaddd9e028\scratchpad\g23\run.sh`

## G24 — 화면: 기록(판 목록)·칭호 고르기·옷장·상점, 설정(옵션 표·미리보기·계정)·키 재설정

G24 is built: all seven screens (Records, TitlePicker, Wardrobe, Shop, ShopBuyConfirm, Settings, KeyRebind) with their models, registration and tests. compile.sh exits 0. 61 of the 62 model tests pass in a runner I wrote outside Unity. The tests that build actual screens have only been compiled, not run.

**Warning about another agent's files:** I ran `mkdir -p .../scratchpad/runner` and that folder already existed, holding another agent's DLL copies and its `runner.cs`. My `cat >` and a python replace overwrote that agent's `runner.cs`. Whoever owns it needs to recreate it. The DLLs were not touched. My own copy now lives in `scratchpad/r2`.

**Files created**
- `Assets/Scripts/Screens/Meta/`: `ScreenRegistry.Meta.cs` (RegisterMeta), `MetaKeys.cs`, `MetaUi.cs`, `RecordsModel.cs`, `RecordsScreen.cs`, `TitleModel.cs`, `TitlePickerScreen.cs`, `WardrobeModel.cs`, `WardrobeScreen.cs`, `ShopModel.cs` (includes ShopBuyConfirmModel), `ShopScreen.cs`, `ShopBuyConfirmScreen.cs`
- `Assets/Scripts/Screens/Settings/`: `ScreenRegistry.Settings.cs` (RegisterSettings), `SettingsKeys.cs`, `SettingsModel.cs`, `SettingsPreviewModel.cs`, `SettingsPreviewView.cs`, `SettingsScreen.cs`, `ChoiceStepper.cs` (the ◀value▶ widget, built on UiWidget), `KeyRebindModel.cs`, `KeyRebindScreen.cs`
- `Assets/Tests/EditMode/ScreensMeta/`: `ScreensMetaFakes.cs`, `RecordsTitleWardrobeTests.cs`, `ShopModelTests.cs`, `SettingsModelTests.cs`, `KeyRebindModelTests.cs`, `ScreenTreeTests.cs`
- `Assets/Resources/NamesSolo/G24.ko.json`: new keys only, checked for no overlap with the main table or other fragments. It holds the `str.settings.desc.*` descriptions, account restore confirm/done/failed, resolution value and fullscreen reason, the key-rebind pause_fixed/save_blocked/help lines, and the ShopBuyConfirm minus/plus/total.
- No `.meta` files were made; Unity generates them.

**compile.sh**
- `bash Tools/compile.sh -f Assets/Scripts/Screens/Meta/,Assets/Scripts/Screens/Settings/,Assets/Tests/EditMode/ScreensMeta/` exits 0: no errors, all four units ok.
- A run with `-w` shows no warnings in my folders.

**Tests**
- The runner (`scratchpad/r2/runner.cs`, .NET 10, reflection, `SOLO_ASSETS_DIR=Assets`) ran the model tests and passed 61 of 62.
- The one failure is `S08_12_2__RestoreSaveExceptionIsFailure`. It uses `LogAssert.Expect`, which needs Unity's log scope ("No log scope is available"), so it can only pass inside the Unity runner.
- `ScreenTreeTests` (8 tests) need native GameObjects, so they are compiled but not run. They cover: no paid elements or cosmetics tab in the shop, wand cards follow gold, no duel record / season / replay in Records, no revenge slot before first meeting, settings row counts match the model per category and the save path text appears, a widget change saves once, the nine KeyRebind rows with fixed Esc, and registration.
- The full EditMode suite has not been run in Unity.

**Contract changes needed:** none. All Core contracts were used as they are.

**Assumptions about other modules**
- **Equipped stage title:** `titles.equipped` stores a stage title as `title.stage.N.bK` at the time it is picked. TitleModel.Resolve recomputes the band from the current wand stage when read. MainMenu (G18) reads `Titles.Equipped` directly, so it will show the old band after a stage drop unless it goes through Resolve.
- **Cosmetic keys:** `cosmetics.equipped` is keyed by category (`headwear`, `cloak`, …), with ids shaped `cosmetic.<category>.<name>`. PlayerFigure (G14) only checks ContainsValue, so this is compatible.
- **Wild stain:** stage 1 is the original look. Thresholds come from `DataSolo/G11.json` `wild_stain_total_waves`. The highest stage is the larger of the saved max and the stage computed from total waves.
- **Settings reload:** applying volume, ViewOptions and key layouts is left to whoever subscribes to `ISettingsStore.Changed`. Applying window mode and resolution at boot belongs to App, not me; I only call `Screen.SetResolution` when the value changes on the settings screen.
- **Pause key:** the pause row's secondary cell is the fixed Esc. A reassigned pause key is stored in `KeyPrimary[8]`.

**Not implemented or not confirmed**
- None of the screens' layout or input has been checked inside Unity.
- **Cosmetic source:** the profile does not record where a cosmetic came from. Only the cracked mirror shard shows "얻은 곳: 기록" and "계정에 묶임"; every other item shows no source line.
- **Hardcoded value:** the 5-second capture timeout is a `const` with a citation to 08 §12.3. DataSolo is outside my folders, so it is not in JSON.
- **Peddler line:** the shop header shows only the peddler's name (`npc.peddler`). There is no name-table key for the one-line remark.
- **Small devices:** if the device is smaller than 1920×1080, the windowed resolution list holds only the device's own size. No spec vector covers this case.
- **Keyboard drawing:** the keyboard picture is 1432 wide and about 272 tall, slightly under the 280 in the spec, to fit inside the 880-high panel.
- **Not tested:** the "남의 지팡이 효과" and "서버 강화 알림" parts of TV-168, because those rows are hidden in solo and are not in the table.

## G25 — 튜토리얼(허수아비 방 5단계), 허상 첫 만남·복수 연출, 복수 추적기 쪽지

G25 작업을 다 만들었고, compile.sh는 끝 코드 0입니다. Unity를 띄우지 않았기 때문에 테스트는 한 번도 돌리지 않았습니다. 확인한 것은 컴파일뿐입니다.

**compile.sh 결과**
- `bash Tools/compile.sh -f Assets/Scripts/Screens/Story/,Assets/Tests/EditMode/Story/` → 끝 코드 0, 오류 0, 네 단위 모두 ok.

**만든 파일** (모두 D:\algorithm_game\solo\Assets 아래)
- `Scripts/Screens/Story/`
  - `ScreenRegistry.Story.cs`: `RegisterTutorial`에서 `TutorialScreen`을 등록한다.
  - `ViewRegistry.Story.cs`: `AddStory`에서 `StoryLayer`를 넣는다.
  - `TutorialScreen.cs`: HudOverlay 층, BlocksInput = true. 그리는 것은 풍선 하나다.
  - `TutorialController.cs`: 튜토리얼 흐름 전부. 가짜 서비스로 시험할 수 있게 그리기와 떼었다.
  - `TutorialFlowModel.cs`: 순수 모델. 풍선 내용, 완료 뒤 넘김, 끝 처리를 정한다.
  - `StoryTimeline.cs`: 순수 함수. 기준 틱과 경과로 보일 것을 정한다.
  - `StoryFlashGate.cs`: 번쩍 관리기(08 14.1절 한도).
  - `StoryDirector.cs`: 이벤트를 받아 매 프레임 그릴 것과 소리·흔들림 요청을 낸다. uGUI는 쓰지 않는다.
  - `StoryLayer.cs`: `vc.HudRoot` 아래 uGUI로 그린다.
  - `StoryTuning.cs`: G25.json을 읽는다. camp.json(264·84틱)과 bosses.json(114·240틱, 등장 1.2초)에 같은 값이 있으면 그쪽을 쓴다.
  - `StoryColors.cs`
- `Tests/EditMode/Story/`: `StoryFakes.cs`, `TutorialFlowTests.cs`, `StoryTimelineTests.cs`. 벡터를 테스트 34개로 옮겼다(테스트는 아래 「다른 모듈에 기대한 가정」 앞 목록 참고).
  - 단계 진행표 1→5와 끝(6)
  - 계약 벡터: `BuildPracticeConfig(…, Tutorial, null, …)`만 부르고 `BuildRunConfig`는 0회, Battle로 가는 RunConfig가 받은 realCfg와 같은 객체이고 Mode가 Endless, InputScreen이 Tutorial이었다가 되돌아감, `TutorialStepDone(n)` → `TutorialAdvance(n+1)`, ⑤에서 Camp를 CampMode.Tutorial로 엶, Esc → 확인 창(일시정지 아님), 서비스 없음·설정 실패면 진짜 판으로, 끝의 순서(저장 → EndRun → InputScreen → Loading)
  - 복수 연출 틱 경계 0/59/60/83/84/143/144/263/264, WaveClearing을 벗어나면 접힘
  - 대사 판은 PhantomMasterLine 이벤트로만 뜸(층이 72틱을 따로 세지 않음), 처음 쪽지 240틱(TV-236)은 RevengeActive일 때만
  - 복수 꼬리표는 BossIntroBegin의 A로만, 되비침 만개 24/54/84틱과 +114틱 번쩍, 번쩍 한도(0.35, 0.15, 테두리, 0.175, 1초 2회, 간격 24틱)
  - 최종 격노 테, Build 뒤 일회 연출을 다시 틀지 않음
- `Resources/DataSolo/G25.json`: 요약서 값과 솔로 임시 값. 임시 값은 `_note`에 적었다.
- `Resources/NamesSolo/G25.ko.json`: 새 열쇠 여섯 개(`str.tutorial.skip_confirm.title/body`, `str.tutorial.open_loadout`, `str.tutorial.skip`, `str.story.skip`, `str.story.input_locked`).

**계약 변경 필요**: 없음.

**다른 모듈에 기대한 가정**
- **G19(원정 준비):** 첫 판 전에는 `Replace(ScreenId.Tutorial, args.With(ScreenArgKeys.RunConfig, realCfg))`로 연다고 가정했다. Push로 열면 원정 준비 화면이 튜토리얼 아래에 그대로 보인다. 튜토리얼 층이 HudOverlay라서다.
- **G21(Camp, CampMode.Tutorial):** 블록 끼우기는 `LoadoutMove` 명령을 세계에 넣는다고 가정했다. ⑤를 완료로 판정하는 것은 G10 Run이다.
- **G10(Run):** Practice 단계에서 `TutorialAdvance`를 받아 `w.Run.TutorialStep`을 바꾸고, 첫 만남에서 일어날 때 `PlayerRose` 이벤트를 꺼내기 전에 `RevengeActive`를 켠다고 가정했다. 코드를 보니 둘 다 그렇게 되어 있다.
- **G17(HUD):** 복수 꼬리표 소리 `ui.hud.revenge_tag`와 보스 바의 빨강 테는 G17 몫으로 두었다.
- **G16(소리 연결):** 이벤트 단위 소리는 G16이 낸다. 이벤트가 없는 시간표 중간의 소리만 이 층이 낸다(`phantom_first.reflection_burst`·`reflection_hearts_break`·`collapse_phantom_dissolve`, `revenge.ornament_attach`·`skippable`·`title_band`·`intro_tag`).
- **흔들림 중복 가능성:** 복수 60틱과 되비침 +114틱에 이 층이 `vc.Shake(12px, 0.35초)`를 부른다. G16이 같은 순간에 흔들림을 걸면 두 번 겹친다.
- **처음 쪽지:** `ctx.Screens.Toast`를 쓰지 않고 이 층이 직접 그린다. 오른쪽 위 480×96이고, 자리 값은 ScreenManager의 Toast 상수를 쓴다.

**구현하지 못했거나 확인하지 못한 것**
- EditMode 테스트를 한 번도 돌리지 않았다. Unity 배치 모드는 통합 단계 몫이다.
- 쓰러짐의 무채색 전환은 채도만 빼는 셰이더가 없어 회색 덮개(불투명도 0.35, 솔로 임시)로 대신했다. 셰이더는 F2 소유라 손대지 않았다.
- "허상이 플레이어 앞까지 와 내려다봄"과 보스 몸의 은색 가루 동작은 보스 그림(G15) 몫이라 그리지 않았다. 이 층은 2.60초에 은색 가루 24개만 그린다.
- 튜토리얼 풍선 본문 `str.tutorial.step.N.body`는 문구가 미정(N-05)이라 값을 넣지 않았다. 본문 줄은 숨겨진다. 값이 정해지면 G25.ko.json에 더하면 된다.
- 설정을 만들지 못해(지팡이 서비스 없음 포함) 튜토리얼 없이 진짜 판으로 갈 때는 `tutorial_dummy_done`을 적지 않는다. 튜토리얼을 보여 주지 않았기 때문이다.
- 솔로 임시 값은 G25.json `_note`에 적었다: 넘김 지연 1.0초, 등장 이름 띠·칭호 띠 자리, 복수 번쩍 세기 0.25, 입자 크기와 흩어지는 거리, 입력 무시 표시 자리.
- Unity가 만드는 `.meta` 파일은 만들지 않았다.
