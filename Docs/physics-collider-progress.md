# 바닥 충돌 검증 — 진행 현황과 재개 지점

최종 갱신: 2026-11-10 (Asia/Seoul). 작업자: Claude Code 세션(M3·M4·M5, 리뷰 r1·r2·r3 반영). M1/M2는 이전 Codex 세션. 아래는 실제 작업 상태이며, 실행 지침의 예정 산출물과 구분한다.

## 지금 이어받을 사람이 볼 요약

- **다음 후보 M12(사용자 결정 2026-11-10, 미착수):** 출시 후 리팩토링 때 플레이어 이동을 동적 Rigidbody → sweep 기반 키네마틱 컨트롤러로 전환하고 같은 하네스·case(M6 연석 34·E2_C 24·M10 80)·같은 PC로 원본/CURBFIX/키네마틱(+CURBFIX) 비교. 접합부 G07·G08(CURBFIX 10/20 동일)이 핵심 대조군. 시작 전 준비: PlayerMovement 변경 범위, 하네스 접촉 기록 수정 필요 여부, freeze 초안, `fix/curb-collider-bottom` 병합 결정, 새 브랜치는 사용자에게 먼저 묻기. 방식: Unity CharacterController 우선(내장 키네마틱 캡슐, sweep으로 속도 관통 구조적 제거), 작은 크기(r 0.0094)에서 skinWidth·minMoveDistance 0·접지 안정성 확인, 중력·점프·Roll·Boost는 Move 속도로 직접 구현, Rigidbody와 상호작용 없음(넉백 있으면 직접 구현). 불안정하면 직접 만든 sweep 컨트롤러 검토. **순서(사용자 지시 2026-11-10): ① 전환 후 핵심 게임 사이클이 전처럼 플레이되는지 먼저 확인(이동·Sprint·점프·Roll·앉기·Hide, E 픽업·미션 진행·귀가 클리어, NPC·차량 밟힘 피해·무적·체력 회복·안전지대, 속도 부스트 아이템, 카메라·근접 피드백) → ② 제대로 되면 그때 연석 끼임 포트폴리오 측정(같은 하네스·case)**
- **M11 완료(2026-11-10, 사용자 "M10 포함해서 M11 진행")** — `Docs/physics-collider-portfolio-evidence.md`. 남은 일: 커밋(요청 시), 접합부 수정 여부, 화단 버그, main 병합
- **M10 완료 — 판정 partial(2026-11-10)**
  - 결과 `Evidence/PhysicsBench-M10-Results-20261110T192633.zip`(SHA-256 `11eff87f…6b39`), 6 run 모두 완료·exit 0·CK 4/4, 출처(빌드 ID·씬 SHA·case SHA) 전부 일치, 같은 PC. 집계 `Evidence/M10/M10-r1-summary.json`, 해석 `Evidence/M10/M10-r1-interpretation.json`
  - 동결 판정 partial: 실패 조건 BASE 19/79 → CURBFIX 10/76, CURBFIX에서만 실패 0, 대응 개선 21 trial·악화 0. off-face 제외가 빌드마다 달라(BASE 실패 2조건이 CURBFIX에서만 빠짐) 공통 76조건 기준 17 → 10도 함께 씀
  - 층별(공통 조건): A 직선 4/30 → 0/30, B 곡선·사선 3/26 → 0/26, **C 접합부 10/20 → 10/20(G07·G08 수정 전후 동일)**. 남은 실패는 모두 접합부 이음매를 정확히 겨눈 c00·a30(±2cm 옆은 통과 없음), 모두 후진으로 이탈
  - 사후 진단(결과 본 뒤, 가설): 두 road_020 박스 끝면이 틈 없이 맞닿은 이음매에서 가장 가까운 면이 끝면이라 침투 해소가 옆으로 밀림 → 밑면 연장과 무관. 도구 `project-H7B` `PhysicsBenchJointDump.cs`·`PhysicsBenchPointProbe.cs`(BASE 씬, 읽기 전용), 출력 `Evidence/M10/post/`
  - 보조: CURBFIX 최대 침투 3.27cm(G05 Boost m02, M6 2.6cm보다 큼)
  - 다음: M11 포트폴리오 문장(해석 파일 portfolioGuidance). 접합부 수정(이음매 겹침·병합)은 하려면 별도 freeze
- **M10 묶음 완료 — 상태: DONE(위 결과로 대체, 2026-11-10)**
  - 목적: ⑤ 연석 수정이 설계에 쓴 S2(road_009)·S3(road_019) 밖의 연석에서도 관통·끼임을 없애는지(일반화)
  - 지점: 원본 씬 기하 질의(`project-H7B` `PhysicsBenchM10Sites.cs`, 물리·씬 저장 없음)로 도로 쪽 연석 면 후보 225 → 적격 83 → seed 20261110 층별 선정 8지점: A 직선 road_001·020·022, B 곡선·사선 road_013 ×2·road_003, C 접합부 road_020 ×2. 지점당 10조건(Sprint·Boost × c00·p02·m02·a30, BoostSprint a30, Move c00), case `Harness/Cases/M10-r1/m10-curb.json`(84건)
  - freeze `Evidence/M10/M10-r1-freeze.json`(revision 2, SHA `851959ea…`; 초안은 면 범위 확인 추가로 대체). 집계 `Tools/summarize_M10.py`(분석기 통과 판정이 끝없는 평면 기준이라 면 범위 밖 통과는 passed_off_face로 따로 보고)
  - 묶음 `Benchmarks/PhysicsValidation/Builds/M10-r1.zip`(340,713,743 bytes, SHA-256 `70cccb16…090d`), M6 BASE·CURBFIX 빌드 재사용, `RunM10.cmd` 6 run 약 1시간. 기록 `Evidence/M10/M10-r1-freeze-addendum-bundle.json`
  - 다음: 결과 ZIP `PhysicsBench-M10-Results-*.zip` → 분석기 → summarize_M10 → 판정(generalize / no_regression_only / partial / against)
- **M9 감사·대응 분석 완료(2026-11-10)**
  - 보고 `Benchmarks/PhysicsValidation/Evidence/M6/audit/M9-audit-report.md`, 표 `M9-audit.json`(모든 칸에 run ID·실패 caseId), 규칙 `M9-audit-rules.json`(계산 전 기록), 스크립트 `Tools/audit_M9.py`
  - 감사 a~e(실행 완료·입력·초기 상태·지형 적용·오류 로그) 48 run 전부 통과. 원시 ZIP SHA M8과 같음. 연석 오브젝트 오류 0
  - 잡음: 같은 seed a1↔a2 불일치 0~1건, P4 0.01/CCD↔P5c BASE 2/174 → 효과(수십 pp)보다 훨씬 작음
  - ④ 0.01/CCD 끼임 18 = 0.02/D 통과였던 연석 지점 S2 Sprint·Boost 6 case가 완전 끼임으로 바뀐 것(실패→실패 전이)
  - ⑤ 대응 174쌍 개선 39·악화 0·범주 변화 0, CURBFIX recovered 전부 첫 후진에서 1.2초 안에 이탈
  - ③ S3 오차 94%·단차 소실 전부가 C07 남쪽 `road_009_floor` → 연결 문구 유지. 기록상 ③ = 지지 아님(런타임 조건만 충족)
  - S0p 낙하 case·시점이 S0a와 완전히 같음(Read/Write 켬 = H0식 상태로 복귀)
  - 다음: M10(반복·배포 조건 확인) 또는 M11(포트폴리오 문장)
- **M8 완료 — 48 run 최종 집계(2026-11-10)**
  - 결과 A `Evidence/PhysicsBench-M6A-Results-20261110T002814.zip`(`1bdf17a0…65e1`), B `Evidence/PhysicsBench-M6B-Results-20261110T145343.zip`(`3b5c84bb…89e7`). 48 run 모두 exit 0, 같은 PC(i5-12500). 출처 확인 48/48, 조작 체크 통과, CK 제외 0
  - 최종 집계 `Evidence/M6/M6-r1-summary-final.json`, 최종 해석 `Evidence/M6/M6-r1-interpretation-final.json`(부분 집계·해석 A는 대체됨)
  - ① 지지: 바닥 낙하 H0식 S0a 42/72·H1식 S0b 72/72·S0b+Read/Write S0p 42/72(설정만으로 해결 안 됨), S3 0. H0 24 case 중 6 case 낙하(4/6~6/6)
  - ② 지지: S2 연석 p95 200mm, 단차 90/90 소실, 런타임 낙하 12/72
  - ③ 지지 아님(런타임만 충족, 서술상 '부분'): S3 낙하 0/72, 단차 84/90 보존, 연석 p95 100mm로 기준 5mm 미충족(남쪽 도로 0.1m 단차와 같은 크기). "완전히 맞는다"는 쓰지 않음
  - ④ 관통 72→19/102(dt만 38, CCD만 48), 끼임 0→18/102 — "줄였지만 없애지 못함, 끼임은 늘림"
  - ⑤ 지지(제거): 통제 21·18/102 → 0·0, 게임 20·18/102 → 0·0. 최대 2.6cm 파고든 뒤 밀려남
- **M7 정식 묶음 완료 — 상태: WAITING_USER_RUN(2026-11-09 밤, 사용자 "M7 진행해줘")**
  - `Benchmarks/PhysicsValidation/Builds/M6-r1-A.zip`(340,715,274 bytes, SHA-256 `dc90208a7f63675168604d3daf243434de3d6dcb8a1fada993b8de8b83d1541b`): ④ 물리 설정 2×2 + ⑤ 연석 수정(통제·게임 조건), BASE·CURBFIX, `RunM6A.cmd` 24 run 약 2시간 50분
  - `Builds/M6-r1-B.zip`(1,105,338,535 bytes, SHA-256 `2b648dfa302fb6da82d52048214e16085c17e5a7376cf5df3d8133dddf2af580`): ①②③ 바닥 6 변형 + H0, `RunM6B.cmd` 24 run 약 1시간 10분. 결과 ZIP `PhysicsBench-M6{A,B}-Results-<시각>.zip`을 `Evidence/`에 넣으면 분석
  - 빌드: `Tools/build_m6.sh`, 기록 `Evidence/M6/build/`. S3·BASE·CURBFIX 씬 SHA는 M5 기대값과 바이트 동일. **S0a·S0b·S0p·S1·S2는 콜라이더·받침판을 새로 추가하는 변형이라 Unity가 무작위 fileID를 붙여 씬 바이트가 매번 달라짐**(S0a 3회 적용 SHA 모두 다름, 변형 기록은 M5와 동일) → 결과 전 보완 기록 `Evidence/M6/M6-r1-freeze-addendum-scene.json`으로 이 5개는 "변형 기록이 M5와 같음"으로 판정 기준 변경. 첫 시도 기록은 `Evidence/M6/build/invalid-attempt1/`
  - ②③ 기하 재측정 `Evidence/M6/geom/`(동결 도구) → M5와 결과 값 전부 동일
  - 집계 `Tools/summarize_M6.py`(SHA-256 `185241a0…`)를 묶음 전달 전에 동결, M5 결과로 만든 시험 폴더에서 M5 집계와 일치 확인(인용 안 함). 묶음 도구 `Tools/make_bundle_m6.py`, 실행 스크립트 `Harness/Windows/RunM6{A,B}.cmd`
  - 실행 PC: 지난번과 같은 i5-12500. Mac 점검 run은 하지 않음(실행 스크립트는 Windows에서 처음 실행됨)
- **M6 정식 조건 동결 완료(2026-11-09 밤)**
  - 승인: "커밋하고 e 진행해줘" → 범위 질문에 "권장안대로 진행해줘"(주장 ①~④ + ⑤ 연석 수정만 정식 측정, ⑤ 추가 승인). 연석 수정 커밋 `6c26d2a`(브랜치 `fix/curb-collider-bottom`, main 미병합, 사용자 확인 후 새 브랜치 생성)
  - 동결 `Evidence/M6/M6-r1-freeze.json`(SHA-256 `b1b532f593b7…d378`, 초안 `M6-r1-freeze-draft.json`은 SUPERSEDED). 실험 ID `M6-r1`, case `Harness/Cases/M6-r1/` 8개 파일(정의는 M5 원본과 동일, 실험 ID만 변경)
  - run 48개: F(①②③ 바닥 6 변형 × 3, 0.02/Discrete) 18 · P4(④ 원본 씬 BASE dt×CCD 2×2 × 3) 12 · P5c(⑤ BASE·CURBFIX 기본 설정 × 3) 6 · P5g(⑤ 게임 조건 × 3) 6 · H0(① 역사 버전 E2_C × 6) 6. ②③ 기하는 Mac 에디터에서 동결 도구로 재측정
  - ④는 원본 씬에서 측정(트랙 B Zone 통제가 끼임을 관통으로 바꿨기 때문). 주장별 지지 기준·대체 문장, 배타적 연석 범주(passed_unclassified 포함), 침투 깊이 보조 지표, 무효 규칙을 결과 전에 고정
  - 예외: H0은 M5-H0-win-01 바이너리 재사용(repeat-01 묶음과 게임 파일 292개 동일). 범위 밖: H2~H6, 다른 PC, 건물 관통, 화단, cooking·소품, 30/120 FPS
  - M7 규칙: 새 빌드 씬 SHA가 M5 기록값과 같아야 함. 집계 스크립트 `summarize_M6.py`는 묶음 ZIP을 사용자에게 알리기 전에 SHA 동결
- **원본 적용 진행 중(2026-11-09 밤, 사용자 "d는 건너뛰고 원본 적용 진행")** — 상태: 적용·사용자 플레이 확인 완료(2026-11-09), 미커밋
  - 결정: d 건너뜀(결과는 PC 1대 한계로 남김), 에디터 종료 후 배치 적용, 연석 130개는 씬 override, **연석만 적용**(cooking은 차량 변형 6개 포함 빌드로 재검증 후 따로 결정)
  - 준비 완료: 복제본에서 `PhysicsBenchCurbFix.ApplyAndSave` 재실행(`fix/variant-M5-F-CURBFIX-origapply-01.json`) → 씬 SHA-256 `12ba8273…d043` = Windows 검증 빌드 CURBFIX-win-01 씬과 바이트 동일. 사본 `Benchmarks/.physics-work/origapply/AntDemoScene.CURBFIX.unity`. 원본 씬은 9d0bb583…(BASE와 동일)이므로 파일 교체만으로 적용, 원본에 하네스 코드는 넣지 않음
  - **적용 완료(2026-11-09):** 사용자 에디터 종료 확인 후 원본 `Assets/Scenes/AntDemoScene.unity`를 교체(9d0bb583… → 12ba8273…). 교체 전 사본 `Benchmarks/.physics-work/origapply/AntDemoScene.BEFORE-9d0bb583.unity`(git HEAD에도 있음). batchmode로 원본 프로젝트 열기 exit 0·컴파일 오류 0(`fix/origapply-import-01.log`, 씬 로드는 이 단계에서 하지 않음). git 변경은 씬 1개뿐
  - **에디터 플레이 확인(Coplay MCP, 2026-11-09 22:42):** 편집 모드 연석 Box 820개 모두 윗면 0.1·밑면 -0.3, 수정 전 모양 0. 플레이 모드에서 개미를 fix1 연석 지점으로 옮기고 PlayerMovement 입력 필드(_inputDir·_isSprint·SetSpeedBoost)를 리플렉션으로 설정해 2초 돌진 4건: S2 Sprint 정면·S2 Boost 정면(BASE 빌드 완전 끼임 case) → 연석 면 1.6cm 앞에서 멈춤, S2 Sprint 30°·S3 BoostSprint 30°(BASE 관통 case) → 연석 면을 따라 미끄러짐. 4건 모두 관통·끼임·낙하 없음(y -0.0987 유지). 오류 로그 0(경고는 기존 NPCMovementZone 음수 스케일 등). 실제 키 입력이 아니고 표본 4건뿐, 기록 `Evidence/M5/fix/origapply-editor-playtest-01/`. 플레이 후 씬 파일 변경 없음(12ba8273…)
  - 사용자 직접 플레이 확인 완료(2026-11-09, "테스트 완료"): 연석은 시각적으로 파고든 듯 보여도 다시 빠져나올 수 있음, 이전처럼 갇혀 못 나오는 버그 없음. **새 버그 보고: 긴 화단 오브젝트에 빠르게 돌진하면 통과하며 개미 몸체가 바닥 아래로 내려갔다가 다시 지면 위로 올라옴**(사실 확인만 하고 보류, 사용자 지시 "기록만 해두고 지금은 포트폴리오 테스트 측정에만 집중"). 대상: 사용자가 선택한 `PostProcessingVolume/Store2/fir tree group 2`(같은 계열 10개, PostProcessingVolume.prefab 소속, Ground 레이어). 콜라이더는 BoxCollider 하나(월드 x -76.41~-68.59, z 14.11~15.29, y 0~0.205, 길이 7.8m·폭 1.2m·높이 0.2m, 밑면이 지면 y 0에 붙음). 연석 수정 대상(윗면 0.1) 아님 → 이번 씬 변경으로 모양 바뀌지 않음, 즉 **연석 수정이 만든 버그가 아니라 기존 버그**. 모양이 수정 전 연석(지면에 붙은 높이 0.2 Box)과 같아 같은 기전 가설(침투 해소가 아래로 밀어냄). 재현 1회(Coplay, 남쪽 면 정면 z-방향 Boost 3.5+Sprint 2초)는 면 1.1cm 앞에서 막혀 미재현(표본 약 10Hz라 순간 하강을 놓쳤을 수 있음, 사용자 진입 각도·위치와 다를 수 있음). 기록 `Evidence/M5/fix/origapply-editor-playtest-01/`(firtree.txt, dash-firtree2_*). 후보 수정: 연석과 같은 밑면 연장(미검증). 남은 일: 커밋은 요청 시(브랜치는 먼저 묻기). cooking(79개+차량 변형 6개)은 재검증 후 별도
- **b·c Windows 결과 분석 완료(2026-11-09 밤)**
  - 원본 `Evidence/PhysicsBench-Fix1-Results-20261109T194748.zip`(SHA-256 `63eb9c09…b6e2`), 추출 `Evidence/M5/windows/PhysicsBench-Fix1-Results-20261109T194748/`, 판정 `Evidence/M5/fix/analysis/W-F-*-a1`, 집계 `Evidence/M5/fix/M5-fix1-windows-summary-a1.json`(`Tools/summarize_M5_fix1.py`), 해석 `Evidence/M5/fix/M5-fix1-windows-interpretation.json`. 같은 PC(i5-12500), 21 run 모두 완료·CK 4/4, 통제 조건은 3 attempt 결과가 case별로 동일
  - **b 지지(이 case 범위):** 연석 102 trial 관통 21→0, 완전 끼임 18→0, E2_C 낙하 0=0. 직진(관통 12·끼임 18→0)·비스듬(관통 9→0)·정면 Roll·Move(변화 없음) 따로. 게임 조건 3 run 합 관통 18→0·끼임 18→0, 회귀 신호 없음. 단 바뀐 case는 막힘이 아니라 연석 안으로 약 2r(최대 2.6cm) 들어갔다 밀려나는 recovered → '해결'이라고 쓰지 않음
  - **c 부분 지지:** 동결 원 지표(passed_curb)는 차이 없음. c2 지표로 Trash_Can_06·(1)은 BASE 직진 통과 → COOK30 막힘(예측 적중). P03은 COOK30 3건 무효(출발 겹침), P00·P04는 두 빌드 모두 통과(예측 틀림), P05·P06은 BASE부터 막힘(틀림), P07은 판정 불가. Read/Write 오류 경로 68→6
  - **조작 확인 일부 실패:** 남은 6개는 실제로 생성되는 차량 변형 `Assets/Prefabs/CarPrefabs/*.prefab`(Car_06·13·16·19·Futuristic_Car_1·Van)의 cooking 26 override(루트 7aecf5f). cook79·audit 목록 밖이라 COOK30에서 안 고쳐짐 → 게임 조건 회귀(관통 19 vs 18, 끼임 18 vs 18)는 차량 일부만 복원된 비교. pizzeria는 BASE에서도 오류 없음
  - **트랙 B ④ 정정:** `Evidence/M5/trackB/M5-trackB-correction-01.json`. 원본 씬(Zone 통제 없음)에서 S2 Sprint·Boost 6건 끼임 3/3 → '끼임 재현 안 됨·인용 불가'는 공통 통제 탓. ④는 관통·끼임 모두 지지(통제 조건)
  - **원본 적용 시 결정할 것:** 연석 130개(PostProcessingVolume.prefab 소속) 프리팹/씬 override, cooking은 79개 + 차량 변형 6개(재검증 필요), 원본 에디터 저장·종료
- **b·c 진행(2026-11-09 저녁, 사용자 "b, c, d 순서대로 진행, d 전에 알림")**
  - 상태(완료): Windows 실행 대기였던 `Builds/M5-fix1.zip`(510,616,373 bytes, SHA-256 `d907324e5b8dd043d3fd899345099171d11ad32bf852107a0040f15d12ccebb1`, `RunFix1.cmd` 21 run 약 2시간 30분). 동결 `Evidence/M5/fix/M5-fix1-freeze.json` + addendum-c(소품별 예측) + addendum-c2(소품 지표 수정: Mac 점검에서 '돌아서 지나감'이 passed_curb로 잡힘)
  - 빌드(복제 공간 `project-H7B`, 원본 씬 SHA 9d0bb583… 기준): BASE(원본 그대로), CURBFIX(연석 Box 820개 밑면 -0.3, 테스트용 통제 없음, `Harness/Editor/PhysicsBenchCurbFix.cs`, 검증 `curbfix-verify-02.json`), COOK30(de8bfd48 79개 프리팹 cooking 26→30, 빌드 후 원본과 대조해 복원). profile: 통제 `profile-H7.json` / 게임 `profile-H7-game.json`(NPC·고양이·차량 켬)
  - **Mac 점검(인용 안 함, `M5-fix1-macsmoke-b.json`):** 원본 씬 BASE는 NPC 유무와 상관없이 S2 Sprint·Boost 6건이 완전 끼임 → **'연석 끼임'은 원본 씬 구성에서 재현됨.** 트랙 B는 Zone 콜라이더를 트리거로 바꾼 통제 때문에 같은 case가 관통으로 나왔던 것으로 보임(trackB ④ '끼임 인용 불가' 해석은 fix1 Windows 결과로 정정 예정). CURBFIX는 통제·게임 모두 관통 0·끼임 0, E2_C 변화 없음
  - cooking: 런타임 cooking 필요 씬 46→7(남은 7 = pizzeria 비균일 스케일), 프리팹 104→0. Mac 점검에서 소품 P01·P02·P03은 BASE 관통 → COOK30 막힘, P00·P04는 두 빌드 모두 관통(원인 미조사), pizzeria P05·P06은 BASE에서도 막힘
  - **원본 적용은 아직 안 함**(원본 에디터가 열려 있음). 연석 130개가 `PostProcessingVolume.prefab` 인스턴스 소속이라 프리팹/씬 override 선택 필요. 사용자 확인 후 진행
  - d(다른 PC 반복)는 기존 묶음 `M5-repeat-01`·`M5-trackB-01`·`M5-trackB-02`를 다른 PC에서 실행. 사용자에게 알림 후 진행
- **한눈에 보기(2026-11-09 결과 반영)**
  - **현재 단계: M5 IN_PROGRESS / 결과 분석 완료, 다음 단계 사용자 결정 대기.** repeat-01·trackB-01·trackB-02 Windows 결과 회수·분석 완료(52 run, 같은 PC i5-12500 = 02c PC)
  - 산출물: 추출 `Evidence/M5/windows/PhysicsBench-{Repeat01,TrackB01,TrackB02}-Results-*/`, 판정 `Evidence/M5/analysis/W-H0-r1-*-a1`·`Evidence/M5/trackB/analysis/W-B-*-a1`(동결 분석기 그대로), 집계 `Evidence/M5/M5-windows-20261109-summary-a1.json`(`Tools/summarize_M5_20261109.py`), 해석 `Evidence/M5/M5-windows-20261109-interpretation.json`
  - **repeat-01(H0):** E2_C 낙하 7/24, 5경로 6/6 안정 재현(C01_jump·C06·C08·C11 walk·C11_jump), C09 jump·walk 2/6 간헐. E2_K 7/24 모두 6/6. 같은 seed 차이 0. P11R 6/10(비결정, 인위 조건이라 제외 유지)
  - **트랙 B 바닥(dt 0.02/Discrete, E2_C 72 trial):** S0a 기본 Mesh 42 · S0b H1식 72(조작 확인 통과) · S0p 42(S0a와 같은 case) · S1 받침판 0 · S2 Convex 12 · **S3 Box 0**
  - **연석(S3, 54 trial):** 관통 0.02/Discrete 54 → 0.02/CCD 36 → 0.01/Discrete 28 → 0.01/CCD 27. 끼임 4 설정 모두 0. **S3c(0.01/CCD) 0/54**, 끼임 0
  - **주장 판정:** ① 조건부 지지(수정 전 물리 설정에서 Mesh가 하강·착지 때 뚫림, Box는 0) ② 지지(기하+런타임 12/72) ③ 지지 ④ 관통 감소로 지지하지만 효과는 주로 dt 0.01, 현재 설정도 관통 50%. **'연석 끼임'은 재현 안 됨 → 10/23 에디터 결과와 상충, ④의 이유로 인용 불가**(→ fix1 결과로 정정: `trackB/M5-trackB-correction-01.json`). S3c는 관통 원인 가설 지지(통제 조건, 테스트 빌드 한정)
  - cooking 질문 답: S0a=S0p → EnableMeshCleaning 제거 자체는 낙하에 영향 없음. H1 구성 100% 낙하는 Read/Write 꺼짐으로 콜라이더가 빈 것
  - **범위 한계:** PC 1대, 트랙 B 바닥은 한 물리 설정만, 탐색 결과(M6 정식 아님). S3c는 실제 게임에 적용·회귀 확인 전 '고쳤다'고 쓰지 않음
  - **정리 B 삭제 완료(2026-11-09), 디스크 여유 32GiB.** **사용자 결정 대기:** (b) S3c 원본 적용·회귀 테스트 (c) 79개 프리팹 cooking 30 복원 검증 (d) 다른 PC 반복 (e) M6 정식 동결로 이동 (f) 체크포인트 M5h
  - 관련 문서: 트랙 B 설계 `Docs/physics-collider-trackB-plan.md`, 받침판·cooking 이력 `Docs/physics-collider-ground-plate-history.md`, 정리 후보 `Docs/physics-collider-cleanup-candidates.md`
- (상세, 2026-11-08) 실행 대기 묶음 기록: Windows 묶음 3개 실행 대기: `Builds/M5-trackB-02.zip`(170,208,799 bytes, SHA-256 `e9820792797877427cb5aff987968e86e47598672d04fbe123f1ae4cc4405826`, S3c 연석 연장 테스트 빌드 6 run 약 15분, trackB-01 다음 같은 PC, 동결 `Evidence/M5/trackB/M5-trackB02-freeze.json`), `Builds/M5-repeat-01.zip`(약 40분), `Builds/M5-trackB-01.zip`(1,021,249,682 bytes, SHA-256 `11f8422d90c67a200e05114442fcf0c12008d9c260330fdfea629468dfce6308`, 30 run, 약 80분, 동결 `Evidence/M5/trackB/M5-trackB-freeze.json`). pilot-02c 분석 완료 후 사용자 "1번으로 진행"(H0 재현성 확인). Windows 묶음 `Builds/M5-repeat-01.zip` 실행 대기.
- **repeat-01(2026-11-08):** 동결 `Evidence/M5/M5-repeat-r1-freeze.json`(묶음 제작 전), 준비 `Evidence/M5/M5-repeat-r1-prep.json`. 빌드 M5-H0-win-01 재사용, case r1 = e2c H0의 E2_C 24·E2_K 24·CK 4·E1_P11R_hold(정의 불변, `Tools/make_cases_r1.py`). full 6 run(seed 1,1,2,3,4,5) + P11R 단독 10 run(60fps 6·30fps 2·120fps 2), 약 40분. 묶음 83,175,485 bytes, SHA-256 `f2217de87bf46decb75f21fb3222d100a3c1a1b33c8c94b2b0cd85b2686cc4ca`, `RunRepeat01.cmd`. 가능하면 두 PC에서 실행, PC별 집계
  - P11R 사전 확인: pilot-01·02c 모두 convex `road_001` 안 0.0605m 침투 상태로 시작. pilot-01은 위로(hull 안에 머묾), 02c는 아래로 빠져나가 낙하. 이동 중 접촉 0 낙하 13건과 기전이 달라 따로 집계 M4 DONE. pilot-02b는 전달하지 않고 보존만 한다.
- **pilot-02c Windows 결과(2026-11-08 회수, `Evidence/M5/M5-windows-pilot-02c-summary.json`):** 원본 `Evidence/PhysicsBench-Pilot02c-Results-20261108T163441.zip`(SHA-256 `85d383cd…e515`), 추출 `Evidence/M5/windows/PhysicsBench-Pilot02c-Results-20261108T163441/`, 분석 `Evidence/M5/analysis/W-*-p02c-20261108T163441-a1/`. 실행 PC는 smoke PC(Win10, i5-12500)로 pilot-01 PC와 다르다. 6 run 완료·종료 코드 0, case 해시 동결값 일치, 미지원 동작 trial 0, 하네스 오류는 의도한 CK08·CK13뿐, yaw_align_failed 0
  - **H0(수정 전 원본)에서 Windows 바닥 관통 낙하 첫 관측:** 146건 중 fall 15. 미리 정한 연석 횡단 경로 C 24건 중 6건(C06·C08·C11 walk, C01·C09·C11 jump), K 사후 표적 24건 중 7건, E1 지점 1건(P11R), CK03(기대). 이동 중 13건은 접촉 0, 메시 전환 뒤 0.07~0.39m에서 바닥 통과 후 -20~-237m까지 계속 낙하. pilot-01의 0건은 횡단 경로 case가 없었던 범위 차이. 한계: case당 1회·PC 1대·H0 e2c Mac 대조 없음·원래 exe 동일성 미확정
  - **H0 E1_P11R_hold:** 정지 상태에서 convex `road_001` 접촉이 기록된 채 정착 3스텝에 통과·낙하. pilot-01(다른 PC, 같은 빌드·case 정의)은 pass. PC 의존 또는 비결정 낙하, 원인 미확정
  - **Hm1 116/112/80(공통 완료 118건):** 낙하 0/29/29, 112와 80의 낙하 집합 동일 → Y 위치 고정 해제만으로 낙하가 모두 나타남. 80에서만 `unsupported_end` 7건(공중 정지 종료, 원인 미조사). 경로 안 12·경로 끝 지난 뒤 8·K/E1 지점 8·CK03 1. Windows 112는 Mac 112와 113/120 일치, 차이 7건은 모두 Mac 쪽 yaw_align_failed
  - **H1:** fall 2 = CK03 + C11_jump. C11_jump는 숨은 Ground 받침판 위로 4cm 일시 하강 후 회복(관통 낙하 아님)
  - **H7:** fall 2 = CK03 + P24T(시작 높이가 비충돌 장식면 0.42에서 잡힌 case 결함, 바닥 정상). 소품 관통 8건·Box 단차 4건이 Mac과 case별로 일치 → Windows에서 확인
- **pilot-02c(2026-11-02, 사용자 "B로 진행"):** r3 리뷰 세 항목을 해석뿐 아니라 묶음에도 반영. 근거 `Evidence/M5/M5-pilot02c-prep.json`, 동결 기록 `Evidence/M5/M5-pilot02c-freeze.json`
  - case e2c(`Harness/Cases/e2c/`, 생성 `Tools/make_cases_e2c.py`, e2b를 입력으로 파생): Hm1 jump 26건 제외(같은 경로 walk가 남음), Hm1 연석 4건 Sprint→Move·구제 점프 끔, H0 연석 4건 Sprint→Move. H1·H7은 e2b 바이트 복사. case ID 유지
  - Hm1 통제 profile 추가: `profile-Hm1-unfreezePosY.json`(constraints 112 = 116에서 Y 위치 고정만 해제). 116/112/80으로 Y 위치 해제와 Y 회전 해제를 나눠 본다. 84(Y 위치 고정·Y 회전만 해제)는 Y가 고정돼 구조적으로 낙하할 수 없어 낙하 지표에서는 넣지 않았다
  - 공중 고정 pass: 판정 규칙(thresholds-r4·분석기)은 그대로. 분석기 `passEvidence`로만 분리한다. Hm1 base의 정지 pass는 `pass_rest_inferred_y_frozen`이 정상
  - 재빌드 없음: 02b와 같은 빌드(M5-Hm1-win-03, H0·H1·H7 win-01)에 외부 case·profile 파일만 바꿈. H0·H1·H7 빌드의 하네스 revision이 이전 판인 점은 그대로
  - Mac 확인(Windows 결과 아님): Hm1 e2c 연석 4건 Move만 요청·`unsupportedTrials` 없음(e2b에서는 같은 4건이 Sprint 미수신 하네스 오류). Hm1 112 전체 120건 11.9분, `override:constraints=116->112`, 판정 검증 4/4(CK03 fall). `yaw_align_failed` 7건이 약 4초 안에 몰려 발생 후 회복(원인 미확정, 실행 중 마우스 움직임 가능성, 하네스 오류로 분리됨). H0 연석 Move 변환은 H0 Mac 빌드가 없어 정적 확인만
  - 묶음 `Builds/M5-pilot-02c.zip`(445,606,099 bytes, SHA-256 `e388092a8bc72478ec8d6f007a56aee29d0b785eb101b29af4ebf18178370833`): 6 run(Hm1 base·80·112, H0, H1, H7), `RunPilot02c.cmd`, 약 110분, run ID `W-<V>-<variant>-p02c-<시각>`
- **M5 2차(2026-10-30 밤) 요약:**
  - **H-1(`Hm1`) 추가 복원(사용자 승인):** H0 커밋 메시지가 "수직 낙하 **수정 중**"이고, 직전 `d3a9856`은 `MovePosition` 이동 + Rigidbody constraints 116(FreezePositionY 포함)이다. H0에서 constraints가 80(X·Z 회전만 고정)으로 바뀌었다. 116→80은 Y 위치 고정(4)과 Y 회전 고정(32)을 함께 푼다(116 & ~80 = 36). Y 위치만 푼 값은 112다. 근거 `Evidence/M5/M5-audit-04-Hm1.json`
    - 연표 보충: `4d73f8d`(05-22, 플레이어 y 하드코딩), `e4b5c50`(05-26, "road 타일 공중 뜸·바닥 타일 경계 버그" 수정, 접지 판정 교체)가 H1 이전에 있었다
    - Hm1 커밋 상태에서는 Y가 고정돼 구조적으로 낙하 불가(Mac 확인). 원래 낙하는 H-1→H0 사이 미커밋 상태였을 가능성(가설)
  - **재구성 통제 Hm1-unfreezeY(Mac 진단):** Hm1 코드에 H0의 constraints(80)를 넣은 조건. 116→80은 FreezePositionY와 FreezeRotationY를 함께 푼다(단일 요인 아님, r3 리뷰). 경로 안 낙하 22건, 모두 접촉 0. (a) 연석 바깥 경계 0.2m 뒤 6곳, (b) 도로→인도 타일 메시 경계 0.25~0.5m 뒤. Hm1에는 점프가 없어 jump case 낙하 12건은 보행과 같다(보행에서 재현). `Evidence/M5/M5-mac-explore-e2-summary.json`. 역사 버전이 아니며 H0의 다른 차이(convex 2개·레이어)는 넣지 않았다
  - **r3 리뷰 반영(`Evidence/Review-20261030-codex-r3/review.md`, 대응 `Evidence/M5/review-r3-response.json`):** 버전별 지원 동작 표 `Harness/Cases/capabilities-r1.json`(Hm1 Move만, H0 Move·Look·Jump, H1·H7 전부). 분석기 `--capabilities`가 outcome을 바꾸지 않고 `action_unsupported:<동작>`·`y_position_frozen` 플래그와 pass 지지 근거 분리(`passEvidence`)를 더한다. test_analyzer 11/11. pilot-02b 묶음은 그대로 두고 해석에서 분리
  - **case e2/e2b:** 버전별 시각 메시 probe로 시작 높이·minAllowedY 재설정(남은 일 2번 해결). 장거리 도로 6·연석 횡단 12(+이동 점프)·타일 이음매 보행 2·이음매 정지/1m 낙하(스트레스)·H7 Box 단차 4·H7 소품 8·사후 표적 K01~K12. 생성기 `Tools/make_explore_e2.py`
  - **H7 감사(남은 일 3·4번):** `Evidence/M5/M5-audit-05-H7-support.json`
    - 남쪽 도로 `road_009_floor` Box 3개가 시각 도로보다 0.1m 높아 도로 중앙에 보이지 않는 단차 → Mac에서 Sprint 접근 시 1.0r 침투 후 막힘, 후진 이탈
    - `CarDrivingZone/Zone2·3`는 비트리거 지지면(실접촉), `NPCMovementZone`은 런타임 트리거
    - Read/Write 오류 소품 5개는 Mac에서 접촉 0으로 관통, 오류 없는 소품은 정상 접촉
  - **Windows 묶음 `Builds/M5-pilot-02b.zip`**(445,604,785 bytes, SHA-256 `f118b17236f7e3534ac06934344f92b73592d7d83dfeef0e7993196aaaf5fd31`): Hm1 base·Hm1-unfreezeY·H0·H1·H7, 약 100분. `M5-pilot-02.zip`(SHA-256 `7375fea4…f5f0c`)은 사후 표적 추가 전 판으로 보존, 전달하지 않음
- **M5 지금까지(2026-10-30):**
  - 연표(`Evidence/M5/M5-audit-02-timeline.json`)
    - 05-18 H0: 기본 cooking, 받침판 없음
    - 05-22 07:20 에셋 `de8bfd48`: 프리팹 79개 cooking 30→26(EnableMeshCleaning 제거)
    - 05-22 07:21 루트 `5859367`: 맵 전체 Ground 받침판 추가
    - 05-29 H1 "[2nd Build]"
    - 이후 Box 전환
  - H0 복원 완료(`project-H0`, 에셋 `23de6f3c` 추정, 원본 2,429개 일치, 컴파일 0 오류). H0 관측용 어댑터 `PHYSICS_BENCH_H0` 추가
  - Mac 탐색 e1(`Evidence/M5/M5-mac-explore-e1-summary.json`)
    - H0: 68건 낙하 0
    - H1: 2지점(P27T·P30T)이 10cm 떨어져 받침판에 착지. **정정(pilot-01 뒤):** 두 지점의 시각 타일면은 H1·H7에서 y=0이고 y=0.1은 H0 높이. case 시작 높이 문제이며 MeshCollider 근거 아님(`probe-H1-tiles01.json`·`probe-H7-tiles01.json`)
    - **H1 받침판 제거 통제**: 68건 중 35건이 정착 2~9스텝에 수직 낙하. 버틴 곳은 모두 보조 BoxCollider가 받침. 도로·타일 MeshCollider 접촉 0
    - → H1 빌드에서 바닥 MeshCollider가 작동하지 않음(Mac). H0의 원래 낙하는 아직 미재현
  - H7 S3 연석: 고속 접근 시 도로면 아래로 파고들어 연석 아래로 끼어든 뒤 후진 이탈(`Evidence/M5/M5-audit-01.json`)
  - H7 런타임 cooking 필요 MeshCollider 46개(`Evidence/M5/M5-audit-03-H7-runtime-cooking.json`): 모두 소품(광고판·버스정류장·쓰레기통·분수·피자 가게·테이블 등)이며 도로·타일·연석 없음. Windows H7 log의 Read/Write 오류와 대응(런타임 생성 차량 31개 추가). A·B 직접 원인 후보에서 제외, 소품 관통은 별도 관찰 항목
  - **Windows 탐색 묶음 준비 완료:** `Benchmarks/PhysicsValidation/Builds/M5-pilot-01.zip`
    - 362,411,339 bytes, SHA-256 `afb06546caf25bd3e9f646ce4edb0484206cb7772b57fa952489d29b301a5cc8`
    - 빌드 M5-H0-win-01(H0 어댑터)·M5-H1-win-01·M5-H7-win-01, case e1 72건
    - 실행 `RunPilot.cmd`: H0 → H1 → H1 받침판 제거 → H7 순서, 약 30분
  - **Windows pilot-01 회수·분석 완료**(`Evidence/M5/M5-windows-pilot-01-summary.json`, 분석 `analysis/W-*-pilot-a1`, thresholds r4, test_analyzer 6/6)
    - 실행 PC: Win11 Pro, i9-14900HX, RTX 4060 Laptop + Parsec 가상 디스플레이(M4 smoke PC와 다름)
    - 4 run 모두 72/72 완료, 판정 검증 4/4. H0·H1·H1 받침판 제거는 Mac과 case별 결과 72건 모두 일치
    - H1 받침판 제거: Windows에서도 35/68 수직 낙하 → H1 빌드 바닥 MeshCollider 비작동을 Windows에서 재현
    - H0: Read/Write 오류 0, 낙하 0. 원래 H0 낙하는 이 탐색 범위에서 미재현
    - H7: 낙하 0(P27T·P30T는 위 case 시작 높이 문제). Read/Write 오류는 소품만
    - case 문제: e1의 y=0.1 지점(P27T·P30T·W09T·W10T)은 H0 맵 기준. 다음 묶음에서는 버전별 probe로 시작 높이를 다시 잡는다
  - H0 복원본 참고: Windows 빌드 중 Unity가 렌더링·프로젝트 설정 원본 5개를 다시 저장했다.
    - URP 설정 3개, `ProjectSettings.asset` 정적 배칭, `UnityConnectSettings` 활성화
    - 씬·프리팹·TimeManager·DynamicsManager 등 물리 관련 원본은 변경 없음
    - 근거: `Evidence/M5/H0-source-after-pilot01.json`. H1·H7은 변경 0
- (M4) smoke-01·smoke-02 모두 Windows 실행·회수 완료. (현재) M5 탐색 pilot-01 Windows 완료, pilot-02c 대기(02b는 보존만), M6 이후 정식 Windows 실행은 미수행.
- **M4 smoke-02 Windows 결과:** H1·H7 22/22, 종료 코드 0. 판정 검증 H1 17/17, H7 16/17(CK12 H7 물리 차이). 22건 모두 Mac 판정과 일치. 입력 미전달은 의도한 CK08·CK16에서만 검출. GPU 드라이버 32.0.15.6094. 요약 `Evidence/M4/windows-smoke-02-summary.json`.
- **M4 실제 수행(2026-10-29):**
  - Windows Mono 모듈은 2026-05-11부터 설치돼 있었음(968M, M1 기록 정정). 추가 설치 없음
  - H1 Windows 빌드 M4-H1-win-01(Mono2x, 오류 0), H7 복원(`Benchmarks/.physics-work/project-H7`, 원본 16,766개 blob 일치)·임포트·Windows 빌드 M4-H7-win-01(Mono2x, dt 0.01, 오류 0)
  - smoke case `Harness/Cases/smoke-s1.json`: 판정 검증 15건 + 실제 맵 5건(S2/S3 도로·인도 정지, 연석 돌진). 기준 높이는 시각 메시에서 읽음(`Evidence/M4/probe-H1-*.json`)
  - 묶음 `Benchmarks/PhysicsValidation/Builds/M4-smoke-01.zip`(+`.sha256`), 실행 `RunSmoke.cmd`·안내 `README-Windows.txt`
  - Mac 진단(Windows 결과 아님, `Evidence/M4/runs/M4-H1-mac-map-01`): H1 빌드에서 개미는 road_009 MeshCollider가 아닌 숨은 `PostProcessingVolume/Ground` BoxCollider(윗면 y=0, 맵 전체 덮음)에 지지되고, 높이 0.2m 연석을 접촉 없이 통과. H1 바닥 MeshCollider 100개 모두 Read/Write 꺼짐 + EnableMeshCleaning 없음(비기본 cooking)
  - H7 profile Mac 사전 점검 완료(`Evidence/M4/runs/M4-H7-mac-smoke-01`, 빌드 M4-H7-mac-01): 20건 실행, 하네스 오류는 의도한 3건뿐, 통제 비활성(TutorialManager·HangoverEffect)·dt 0.01·ContinuousDynamic 확인. 판정 검증 14/15 일치 — CK12는 H7에서 접촉 첫 스텝 수평 침투 1.0r 후 회복으로 `recovered_after_penetration`(궤적과 판정 일치, 기대값이 H1 물리 기준이라 생긴 차이). **CK05·CK07·CK12 같은 물리 의존 case의 기대 결과는 H1 기준**으로 해석한다
  - H7 Mac 실제 맵 진단: 도로 정지는 시각 도로면 y=-0.1에서 지지. S2 연석은 막힌 뒤 이탈. **S3 연석 Sprint 돌진은 통과(벽면 너머 16r)** — Box 연석 배치와 시각 연석 위치 차이인지 실제 관통인지 M5에서 확인
  - (완료) smoke-01은 2026-10-30 사용자가 Windows에서 실행했고 결과 ZIP을 회수했다. 아래 smoke-01 결과 참고.
  - 남은 일: Ground Box 출처(프리팹) 확인 등 M5 감사
- (이전) M3 DONE. (2026-10-29 Claude Code 세션) 1차 검증 뒤 리뷰 보완 항목을 모두 반영했다: 자식 body 관측·자세 복원, 기준 벽 법선 판정, watchdog 실행 검증, 연석 통과 사례, H1 원본 재대조, H7 profile.
- **M4 Windows smoke-01 결과(2026-10-30 회수):** 사용자가 `Evidence/PhysicsBench-Results-20261030T002951.zip`을 넣어 줌.
  - H1·H7 모두 20/20 완료, 종료 코드 0. 판정 검증은 H1 15/15, H7 14/15(CK12는 H7 물리 차이).
  - Windows 10 / i5-12500 / GTX 1660 SUPER / D3D12, 60fps 도달. 창 포커스 없이도 입력 수신.
  - 실제 맵 양상은 Mac 진단과 같다. 요약: `Evidence/M4/windows-smoke-01-summary.json`.
  - smoke-01 하네스에 리뷰 결함 R1~R5가 있으므로, 이 결과는 실행 경로 검증으로만 쓴다.
- **M4-smoke-02 준비 완료(2026-10-30):** `Benchmarks/PhysicsValidation/Builds/M4-smoke-02.zip`
  - 크기 279,241,318 bytes, SHA-256 `02da12a5c4538c7c3aee2979e139ff729badd7ffbab24b53b92ac2f9f7b6b02b`
  - 빌드 M4-H1-win-02·M4-H7-win-02(x64, Mono2x, 오류 0), 케이스 smoke-s2 22건
  - Mac 사전 검증: H1 판정 검증 17/17, H7 16/17(CK12 H7 물리 차이). 입력 감사 거짓 경보 없음(`Evidence/M4/analysis/*-smoke02-01-a3`, 임계값 r3, 2차 리뷰 반영 분석기)
  - 원본 재대조: H1 5,889개·H7 16,766개 일치
- (11/08 기록, 위 '한눈에 보기'의 실행 순서로 통합) trackB-01: Windows에서 `Builds/M5-trackB-01.zip`의 `RunTrackB01.cmd`(약 80분)도 실행 → `PhysicsBench-TrackB01-Results-<시각>.zip` 회수 → 동결(`M5-trackB-freeze.json` + `-addendum.json`)대로 rootOutcome 기준 분석. repeat-01은 아래 그대로
- (11/08 기록, 위로 통합) repeat-01: 사용자가 Windows에서 `Builds/M5-repeat-01.zip`의 `RunRepeat01.cmd` 실행(약 40분, 가능하면 두 PC) → 결과 ZIP `PhysicsBench-Repeat01-Results-<시각>.zip`을 `Evidence/`에 넣음 → 별도 폴더에 풀고 동결 규칙(`M5-repeat-r1-freeze.json`)대로 PC별 집계
- (참고, 02c 분석 직후 선택지 — 사용자가 (a) 선택): (a) H0 낙하 경로(C06·C08·C11, jump C01·C09)와 P11R의 반복 실행·다른 PC 실행으로 재현성 확인 (b) M5를 마무리하고 M6 정식 조건 동결로 이동(입력 항목은 02c 요약 `m6Inputs`) (c) H2~H6 복원(디스크 정리 승인 필요). 아래 2026-11-02 문구는 완료된 이력이다
- **(완료, 2026-11-08) 이전 다음 한 작업(2026-11-02 기준):** 사용자가 Windows에서 `Builds/M5-pilot-02c.zip`의 `RunPilot02c.cmd` 실행(약 110분). 아래 02b 기준 문구는 02c에 그대로 적용한다(run 6개, case e2c). 원래 문구: `Builds/M5-pilot-02b.zip`의 `RunPilot02.cmd` 실행(약 100분) → 결과 ZIP 회수 → 별도 폴더에 풀고 `analyze_physics_bench.py`(thresholds-r4)로 run 5개 판정 → `Tools/summarize_falls_e2.py`로 낙하 경로 집계(보조) → Mac 진단과 대조
  - 확인할 것: Hm1 112(Y 위치만 해제)와 80(Y 위치+Y 회전 해제)의 낙하 차이, Hm1-unfreezeY 낙하가 Windows에서도 같은 경계에서 나는지, H0가 사후 표적 K01~K12에서 떨어지는지, H7 소품 관통·Box 단차가 Windows에서도 같은지
  - 분석은 `--capabilities Harness/Cases/capabilities-r1.json`을 붙여 미지원 동작 trial과 정지 추론 pass를 분리한다
  - **Hm1 116/80/112 비교 규칙(02c 결과 보기 전 2026-11-02 기록):** (1) 세 run 모두에서 하네스 오류 없이 완료된 case ID만 비교한다. (2) 사후 표적 K01~K12는 Mac 80 낙하 지점에서 골랐으므로 80에 유리하다. 미리 정한 경로(E1·E2 L/C/M/S/B)와 K 표적의 결과를 따로 보고한다. (3) Hm1 base는 Y 고정이라 CK03(구멍 낙하 기대)이 pass가 되어 판정 검증 3/4가 정상이다. (4) Mac 수치(112 e2c 27건, 80 e2 22건)는 case 집합이 달라 비교하지 않는다
  - Windows 결과 전까지 thresholds-r4·case e2b·e2c·`profile-Hm1-unfreezePosY.json`·`summarize_falls_e2.py`(SHA-256 `57285099…ce7e`)·`analyze_physics_bench.py`(SHA-256 `c9950f0b…3d97`)·`capabilities-r1.json`(SHA-256 `7b06581f…42ac`)을 바꾸지 않는다
  - (해결) e1 시작 높이 재설정 → e2. (해결·정적) H7 지지 Box·연석 Box 배치 → audit-05. (Mac 진단) H7 소품 관통 → Windows 확인 대기
  - 남은 M5: H2~H6 복원(디스크 허락 필요, 현재 여유 약 12GiB), 지면 경사는 맵에 없음(4m 격자 경사면은 모두 차량·소품 윗면)
- (과거 계획 기록, 2026-10-29 M3 종료 시점) M4 순서로 모듈 설치·H1 Windows 빌드·디스크 부족 시 H1 정리·H7 복원·기준값 도구·smoke 구성을 계획했다. 모듈은 이미 설치돼 있었고, 디스크 확보로 H1 정리는 생략했으며, 나머지는 모두 수행했다(위 M4 실제 수행 참고).
- **M5 우선 확인 단서(가설, 미확정):** H1 Mac 빌드 로그에 MeshCollider Read/Write 미설정 오류 140개 오브젝트(`road_009`·`road_019`·`Set_B_Tiles_*` 등 바닥 포함). 에디터에서는 메시를 항상 읽을 수 있으므로 "에디터 정상·빌드 낙하"와 맞는 후보다. 빌드에서 해당 충돌체가 실제로 비는지 M5에서 확인 전까지 원인으로 쓰지 않는다.
- 지금까지 수행한 작업: 실행 승인·baseline 고정(16,891개/약 1.61GB 해시), H1 별도 복원, Unity 임포트/컴파일 성공, Edit Mode 설정 감사, 원본 5,889개 Git blob 불변 확인.
- M3에서 수행한 일:
  - 하네스·판정기 구현, H1 Mac 개발 빌드 M3-H1-mac-08(오류 0)
  - 합성 지형 판정 검증 15/15(최종 ck-09). 이전 빌드에서도 14/14(seed 6·7·8)
  - 동작 확인: 같은 run ID 재실행 거부, SIGTERM 중단, watchdog 2경로와 다음 trial 복구, 20초 씬 로드, 입력 미전달, 캡처
  - 출발 상태 재현성 측정, H1 원본 재대조
- 아직 수행하지 않은 작업: H0 낙하의 반복·다른 PC 재현 확인(pilot-02c에서 1회 관측), H2~H6 복원·계측, 정식 조건 동결(M6)과 정식 Windows 실행 이후 단계. (smoke-01·02, M5 탐색 pilot-01 Windows 실행·회수는 완료)
- Windows PC는 사용자가 보유한다. Unity 개발환경은 없으며 설치 시간이 부담이다. **Mac에서 빌드하고 Windows에서는 실행만** 하는 경로를 우선한다. Windows 원격 연결은 현재 없다.
- 원본 프로젝트 및 기존 미추적 작업은 보존되어 있다. 문서는 로컬 미커밋 상태이며 다른 기기로 자동 동기화되지 않는다.
- M1/M2 증거·복원 도구까지 옮길 때는 `Benchmarks/PhysicsValidation/physics-validation-checkpoint.zip`을 사용한다. 원본 에셋·임포트 캐시·Unity 원시 로그는 포함하지 않으며, 같은 Git 객체에 접근할 수 있어야 환경을 재구성할 수 있다.

## 1. 승인 기록

| 항목 | 현재 상태 | 근거 |
|---|---|---|
| 계획·커밋 감사·용량 검토·인계 문서 작성 | 허용됨 | 사용자가 계획을 먼저 작성하고 확인받도록 요청, 커밋/용량 검토와 마일스톤 실행 문서 추가 요청 |
| 테스트 스크립트·전용 환경 제작/수정 | **승인됨** | 2026-10-29 사용자: “그럼 m1부터 진행 시작해줘.” 현재 계획 실행 승인으로 기록 |
| Windows 빌드 모듈 설치 | **승인됨 / 설치 불필요** | 같은 지시로 현재 계획 5.4절/M4 범위 승인. 2026-10-29 확인 결과 2026-05-11부터 이미 설치돼 있어 추가 설치하지 않음 |
| 복원 프로젝트 실행·빌드·테스트 | **승인됨 / 수행 중** | 같은 지시로 단계별 수행 승인. H1·H7 복원·Mac/Windows 빌드·Mac 실행 수행. Windows 실행은 사용자가 직접 수행(원격 조작 미포함) |
| H-1(`d3a9856`, 문서 ID Hm1) 추가 복원 | **승인됨 / 수행** | 2026-10-30 사용자가 질문 "수정 전 버전 d3a9856을 'H-1'로 추가 복원해 탐색할까요?"에 "H-1 복원 (Recommended)" 선택. 작업 공간 `Benchmarks/.physics-work/project-Hm1` |
| 원격 PC 조작·CI/유료 클라우드 생성 | 승인 근거 없음 | 현재 기본 계획에 필요하지 않음. IL2CPP 예외 시 별도 조건 검토 |
| 기존 원시 결과/사용자 파일 삭제 | 승인 근거 없음 | 보존 원칙 적용 |

승인을 받으면 여기의 발언/범위/일시 근거를 실제로 갱신한다. 기존 기록은 덮어 지우지 말고 변경 이력에 남긴다.

## 2. 마일스톤 상태판

| ID | 상태 | 완료 근거 / 남은 일 |
|---|---|---|
| M0 | `DONE` | 설계·커밋 감사·실행 지침·이 현황 파일 작성. 코드/씬 변경·새 실행 없음 |
| M1 | `DONE` | `Benchmarks/PhysicsValidation/Evidence/baseline-manifest.json`, `baseline-files.json`, SHA 목록. 양 저장소 tracked diff 없음, 에디터 비실행 확인 |
| M2 | `DONE` | `restore-index.json`, `H1-source-after-import.json`, `H1-editor-resolved.json`, `M2-completion.json`. H1 원본 임포트·컴파일·정적 에디터 감사 통과 |
| M3 | `DONE` | `Evidence/M3-completion.json`(최종). 1차 초안은 `Evidence/M3/M3-completion-draft1-before-review.json`. 하네스 원본 `Harness/`, 최종 빌드 M3-H1-mac-08(Mac, Mono2x, PHYSICS_BENCH). 판정 검증 `Evidence/M3/analysis/M3-H1-mac-ck-09-a2` 15/15(이전 빌드 ck-06~08 각 14/14). **Mac 검증이며 Windows 결과 아님** |
| M4 | `DONE` | `Evidence/M4/M4-status.json`, `windows-smoke-01-summary.json`, `windows-smoke-02-summary.json`. Windows smoke-02(H1·H7 22/22, exit 0, 하네스 경로 검증 기대 일치, Mac 판정과 일치). smoke 결과는 개선율 근거 아님 |
| M5 | `IN_PROGRESS` | **2026-11-09 Windows repeat-01·trackB-01·02 분석 완료**(`M5-windows-20261109-summary-a1.json`, `-interpretation.json`). 이전: `Evidence/M5/M5-audit-01.json`, `M5-audit-02-timeline.json`, `M5-mac-explore-e1-summary.json`. `M5-audit-03-H7-runtime-cooking.json`, `M5-windows-pilot-01-summary.json`, `M5-audit-04-Hm1.json`, `M5-audit-05-H7-support.json`, `M5-mac-explore-e2-summary.json`. H0 복원·Mac 탐색·Windows pilot-01 완료. Hm1 복원, case e2/e2b, Mac에서 재구성 통제 낙하 재현. `M5-pilot02c-prep.json`. `M5-windows-pilot-02c-summary.json`(2026-11-08, H0 Windows 낙하 첫 관측). `M5-repeat-r1-freeze.json`·`M5-repeat-r1-prep.json`. 트랙 B: `Evidence/M5/trackB/`(`M5-trackB-geom-summary.json`, `M5-trackB-freeze.json`+addendum, `M5-trackB-curb-mechanism.json`, `M5-trackB02-presmoke.json`, `M5-trackB02-freeze.json`). **WAITING_USER_RUN:** `Builds/M5-repeat-01.zip`, `M5-trackB-01.zip`, `M5-trackB-02.zip` |
| M6 | `DONE`(2026-11-09) | 동결 `Benchmarks/PhysicsValidation/Evidence/M6/M6-r1-freeze.json`(SHA-256 `b1b532f5…d378`). 범위: 포트폴리오 주장 ①~⑤만(사용자 "권장안대로 진행해줘"). case `Harness/Cases/M6-r1/`(`Tools/make_cases_m6.py`, M5 정의 그대로) 48 run 설계 |
| M7 | `DONE`(A·B 실행 2026-11-10) | 정식 빌드 8개(`.physics-work/builds/M6-*-win-01`) + H0 재사용. 묶음 `Builds/M6-r1-A.zip`(SHA-256 `dc90208a…541b`, 24 run 약 2시간 50분)·`Builds/M6-r1-B.zip`(SHA-256 `2b648dfa…f580`, 24 run 약 1시간 10분). 집계 `Tools/summarize_M6.py` SHA 동결(`M6-r1-freeze-addendum-summarizer.json`) |
| M8 | `DONE`(2026-11-10) | 48 run 판정·집계 `Evidence/M6/M6-r1-summary-final.json`, 해석 `Evidence/M6/M6-r1-interpretation-final.json`. ①②⑤ 지지, ③ 지지 아님(런타임만 충족), ④ 관통 감소·끼임 증가 |
| M9 | `DONE`(2026-11-10) | 감사 a~e 48 run 통과, 대응표·위치군·시간·잡음·기하 위치 `Evidence/M6/audit/M9-audit-report.md` |
| M10 | DONE(2026-11-10) | ⑤ 연석 수정 일반화(설계에 쓰지 않은 연석 8지점): 판정 partial, 실패 조건 19/79 → 10/76(공통 17/76 → 10/76), 직선·곡선 7 → 0, 접합부 10 → 10. 해석 `Evidence/M10/M10-r1-interpretation.json` |
| M11 | DONE(2026-11-10) | 포트폴리오 근거 `Docs/physics-collider-portfolio-evidence.md`: 주장 ①~⑤ 문장(조건 수 분모, 쓰지 않을 표현), 면접 문단, 남은 문제, 출처 SHA, 재실행 방법. M10 포함(사용자 결정) |

## 3. 기준 저장소와 전체 SHA 등록표

조사 시점 프로젝트 루트: `/Users/ihaneul/unityProject/HoneyI-mHome`.

- 루트 origin: `https://github.com/smflsmfqh/HoneyI-mHome.git`
- 에셋 origin: `https://github.com/Devel-Rocket-ClassRoom/minigame-project-assets-smflsmfqh.git`
- 에셋 위치: `Assets/minigame-project-assets-smflsmfqh` (별도 Git 저장소)
- Unity: `6000.3.15f1`, changeset `c1aa84e375f6`

| ID | 루트 commit SHA | 에셋 commit SHA | 이번 문서 작업 시 객체 확인 |
|---|---|---|---|
| Hm1(H-1) | `d3a98563b6dae62d68f5f08c689ac144b4885886` | 추정 `23de6f3c207d54acf193d8961e54da0ee48d68a2`(H0와 같음) | 루트 있음. gitlink 없음, `MiniGameProject/` 하위. `Evidence/M5/M5-audit-04-Hm1.json` |
| H0 | `1a4474108e7430e6ed854bb914d36e598c57ea66` | 미확정(추정 `23de6f3c`) | 루트 있음. 에셋 gitlink 미확정 |
| H1 | `9b83a248d281173446c0640ae1d6740506150c4d` | `ffd2c2e0be38f63f7ff4ad6d88514f8ffb3d93aa` | 둘 다 로컬에 있음 |
| H2 | `442b99cebe6b0405ed0d762018d40a20ba40f30d` | `8fd1b6c726fc85c3750fcbe0ca860cf0017501d5` | 둘 다 로컬에 있음 |
| H3 | `7cca6106f171560aafeafa88576e314c8213bab4` | `5617eccdef11a084b36dbea14f4022b376b61c1c` | 둘 다 로컬에 있음 |
| H4 | `dd2d8801a9231d8a856390f50eb1354b17e4e2f1` | `71b2dc714cf655d2755c547a9742b1ef94b333a8` | 둘 다 로컬에 있음 |
| H5 | `9c7114415fbd04353c33a10594a5490beb92df9b` | `71b2dc714cf655d2755c547a9742b1ef94b333a8` | 둘 다 로컬에 있음 |
| H6 | `d746a7b51c1b642557613051c8c3908701d5461f` | `a160f46591e2af970f47d05603d64ddad4781e59` | 둘 다 로컬에 있음 |
| H7 | `b8efce3526d3ef85eb0c85a580884369f3f5f48a` | `a160f46591e2af970f47d05603d64ddad4781e59` | 조사 시점 HEAD, 둘 다 로컬에 있음 |

H0의 프로젝트 경로는 `MiniGameProject/` 아래다. 다른 버전은 루트다. H7 이후 사용자 코드/씬 변경이 생기면 별도 revision과 diff/hash를 기록한다. 이 표의 H7을 조용히 업데이트하지 않는다.

## 4. 환경·용량과 아직 모르는 것

| 항목 | 마지막 확인 상태 |
|---|---|
| Mac CPU 아키텍처 | arm64 |
| 에디터 경로 | `/Applications/Unity/Hub/Editor/6000.3.15f1/Unity.app` |
| 설치된 PlaybackEngines | **정정(2026-10-29 M4):** `Editor/6000.3.15f1/PlaybackEngines/WindowsStandaloneSupport`(968M, 2026-05-11 설치, win64 Mono dev/nondev 포함)가 이미 있었다. Mac 지원은 에디터 앱 내장. M1의 "Mac만 확인"은 확인 위치 착오 |
| Windows Mono 모듈 크기 | 다운로드 381,878,545 bytes / 설치 989,461,000 bytes. 설치된 에디터 `modules.json`의 값 |
| 디스크 여유 | M1 20.15GB → M3 종료 11GiB → 사용자 정리 후 19.89GiB → M4 종료 12GiB(H7 작업 공간 6.0G·빌드 3개 생성 후). 큰 작업 전 재측정 → **2026-11-09 4.0GiB**(트랙 B 빌드 13개·복제 공간 `project-H7B` 7.4G 뒤). 정리 후보 `Docs/physics-collider-cleanup-candidates.md` |
| 프로젝트/Library | 약 7.6GiB / 5.1GiB |
| 실행 방식 | Mac에서 exe 전체 출력 묶음 제작 → Windows 사용자 실행 → 결과 ZIP 회수 |
| Standalone backend | H1·H7 빌드 설정 Mono2x(빌드 시 Unity API로 읽음, `Evidence/M4/*.build-result.json`). H2~H6 및 원래 Windows exe의 실제 backend는 추가 확인 대상 |
| Windows PC 상세 | smoke-01 기록(2026-10-30): Windows 10 10.0.19045 64bit, i5-12500, NVIDIA GeForce GTX 1660 SUPER, Direct3D12, 143.995Hz 모니터. Windows 10 Education, GPU 드라이버 32.0.15.6094(smoke-02 환경 파일), 내장 Intel UHD 770 병존. 원래 낙하를 본 PC와 같은지는 미확인 |
| Unity/Coplay 연결 | M1에서 비실행 확인 후 M2 전용 프로젝트를 배치 실행·정상 종료함. Coplay 연결을 사용하지 않음 |
| 마지막 저장된 씬 구성 | `Library/LastSceneManagerSetup.txt`는 AntDemoScene loaded/active. 현재 열린 씬/dirty 상태는 에디터 비실행으로 해당 없음 |
| 전용 작업 공간·새 프로세스 | `Benchmarks/.physics-work/project`는 H1 + 감사 스크립트 + `Assets/PhysicsBench/`(하네스 사본, `Evidence/M3/harness-sync-08.json`) + 빌드 시 생성된 `Assets/StreamingAssets/PhysicsBench/`. 작업 공간 4.2GB + 임시 빌드 `Benchmarks/.physics-work/builds/M3-H1-mac-08` 약 447MB. mac-08 후 원본 5,889개 Git blob 일치(`Evidence/M3/H1-source-after-mac-08.json`). 실행 중 Unity/빌드 프로세스 없음 |
| H7 작업 공간 | `Benchmarks/.physics-work/project-H7`(6.0G). 원본 16,766개 Git blob 일치(`Evidence/H7/H7-source-after-import.json`, 추가 파일은 Coplay 패키지 로그 2개). 하네스 사본 `Evidence/M4/harness-sync-H7-01.json` |
| Hm1 작업 공간 | `Benchmarks/.physics-work/project-Hm1`. 원본 2,378개 중 Unity 재저장 5개(URP 3·ProjectSettings 정적 배칭·UnityConnect, H0와 같은 종류, 물리 무관) 외 일치(`Evidence/M5/Hm1-source-after-e2.json`). 하네스 사본 `Evidence/M5/harness-sync-Hm1-04.json` |
| 하네스 사본 상태(2026-10-30 밤) | H0·H1·H7 작업 공간은 `harness-sync-*-03`(Hm1 어댑터 1판·probe 범위 기록)로 최신 원본보다 이전이다. pilot-02b의 H0·H1·H7 빌드는 pilot-01의 win-01을 재사용(파일 943개 CRC 일치 확인). 이 빌드들의 runner에는 Hm1 분기·`playerConstraints`가 없지만 해당 버전 동작은 같다(build-info harness SHA는 다름). 다음 빌드 전 재동기화 필요 |
| 임시 빌드 | **M5(2026-10-30 밤):** M5-H0-win-01, M5-H1-win-01, M5-H7-win-01(pilot-01·02b), M5-Hm1-win-03(pilot-02b), M5-Hm1-mac-03, M5-H7-mac-02. 삭제: M5-Hm1-mac-01·win-01·mac-02·win-02(대체됨, build-result 사본 `Evidence/M5/M5-Hm1-*.build-result.json`). 디스크 여유 약 12GiB. 이전 기록: `Benchmarks/.physics-work/builds/`: M4-H1-mac-02, M4-H7-mac-02, M4-H1-win-02(smoke-02), M4-H7-win-02(smoke-02). 삭제함: M3-H1-mac-08, M4-H1-win-01, M4-H7-win-01, M4-H7-mac-01(모두 build-result 사본이 `Evidence/M3·M4`에 있고 재생성 가능, smoke-01 묶음 ZIP은 보존) |

원래 낙하 좌표·동작·당시 Windows 실행 파일의 정확한 소스 버전은 미확정이다. H1은 Box 전환 직전이지 원래 증상을 본 exe와 동일하다고 확정된 상태가 아니다.

## 5. 보호해야 할 기존 로컬 작업

문서 작성 전부터 다음 미추적 작업이 존재했다. 새로운 담당자는 자신의 작업으로 간주하거나 삭제/덮어쓰기/일괄 stage하지 않는다. 실제 목록은 `git status --short`로 다시 확인한다.

- `Assets/_Recovery.meta`, `Assets/_Recovery/`
- `Benchmarks/AnalyzeCurbStuckRuntime.py`
- `Benchmarks/CURB_RUNTIME_TESTS.md`, `Benchmarks/FLOOR_BOUNDARY_TESTS.md`
- `Benchmarks/RunCurbStuckRuntimeMatrix.cs`, `RunCurbStuckRuntimePilot.cs`, `RunFloorBoundaryBenchmark.cs`
- `Benchmarks/CurbStuckRuntime/`, `Benchmarks/CurbStuckRuntimeMatrix/`, `Benchmarks/youtube-icon.png`
- **2026-11-09 사용자 승인으로 위 목록 중 `Benchmarks/CurbStuckRuntimeMatrix/20261023_023059/`만 남기고 삭제했다.** 상세 `Docs/physics-collider-cleanup-candidates.md`
- `Docs/portfolio-assets/`, `Docs/portfolio-code-snippets/`

현재 작성한 네 문서도 미추적 상태다. 루트 tracked diff와 에셋 저장소 status는 마지막 읽기 전용 확인 시 비어 있었다. 미래 세션에서 동일하다고 가정하지 않는다.

기존 `Benchmarks/Tunneling/`, `CurbStuckRuntime/`, `CurbStuckRuntimeMatrix/20261023_023059/` 등의 자료는 과거 탐색/에디터 측정이다. 새 계획 M4/M5/M8의 완료 증거가 아니다. 현재 설정을 파악하는 참고와 판정 문제 검토에만 사용한다.

## 6. 실제 산출물·실행 등록

| 종류 | 현재 등록 상태 |
|---|---|
| 문서 | 설계 / 커밋 감사 / 실행 지침 / 이 현황 파일 |
| 문서 이동용 묶음 | `Docs/physics-collider-handoff.zip` 및 `.zip.sha256`. 문서 4개와 내부 체크섬 목록만 포함하며 소스·실험 결과는 포함하지 않음 |
| 인계 묶음 | **M5g판**(2026-11-08, pilot-02c Windows 실행 전. M5f + case e2c·`profile-Hm1-unfreezePosY.json`·`make_cases_e2c.py`·`RunPilot02c.cmd`·README-Windows-pilot02c·02c prep/freeze 기록·Mac e2c run 2개와 분석·`M5-pilot-02c.zip.sha256`·면접 학습 문서 `physics-collider-interview-study-guide-20261102.md`) `Benchmarks/PhysicsValidation/physics-validation-checkpoint.zip`. 직전 M5f판(2026-10-31, 02c 미포함)은 `-M5f.zip`으로 보존(SHA-256은 같은 폴더 `.zip.sha256`, 내부 `SHA256SUMS.txt`, `CHECKPOINT-NOTES.txt`): 문서·도구·하네스 원본(H0 어댑터 포함)·M1~M5/H0/H7 JSON·run·분석·리뷰 증거·Windows smoke 원시 기록. *.log·*.png·ZIP 본체(체크섬만)·원본 에셋·Library·빌드 바이너리 제외. 이전 판은 `-M2`·`-M3`·`-M4a`·`-M4c`·`-M4done`·`-M5a`·`-M5b`·`-M5c`·`-M5d`·`-M5e`·`-M5f`로 보존. 문서만의 `Docs/physics-collider-handoff.zip`도 같은 시점으로 갱신(이전 `-M2.zip`·`-M5c.zip`·`-M5d.zip`·`-M5e.zip` 보존) |
| 승인 baseline-manifest | `Benchmarks/PhysicsValidation/Evidence/baseline-manifest.json` 및 `baseline-files.json`, `baseline-manifest.sha256`. baseline ID `physics-baseline-20261029-01` |
| 복원 작업 공간·restore-index | `Benchmarks/.physics-work/project`, `Benchmarks/PhysicsValidation/Evidence/restore-index.json` |
| M2 감사·컴파일 | `Evidence/H1-import.log` 및 `H1-editor-audit.log` 정상 종료. `H1-editor-resolved.json`, `H1-source-after-import.json`, `M2-completion.json` |
| 재현 가능한 복원/감사 도구 | `Benchmarks/PhysicsValidation/Tools/prepare_snapshot.py`, `Tools/Editor/PhysicsRestoreAudit.cs`. 기존 작업 공간 덮어쓰기 거부 확인은 `Evidence/restore-tool-checks.json` |
| 새 하네스 소스·판정 검증 | `Benchmarks/PhysicsValidation/Harness/`(Runtime 4·Editor 1·분석기·Cases), `Tools/sync_harness.py`. 파일 SHA는 `Evidence/M3-completion.json`. 판정 검증 run `Evidence/M3/runs/M3-H1-mac-ck-0{1..5}`, 중단 검증 `M3-H1-mac-int-01`, 분석 `Evidence/M3/analysis/`. run 이력:
  - ck-01: 접촉 기록 결함 수정 전, 8/10
  - ck-02~05: r1 10건, 각 10/10
  - ck-06: r2 14건, 자세 복원 전 빌드
  - ck-07·08: mac-07, 각 14/14
  - ck-09: 최종 mac-08, 15/15(분석 a2)
  - 재현성 비교: `trace-repeatability-*.json`
  
  모두 보존하며 정식 실험 근거가 아니다. |
| 동결 experiment ID / Cases 해시 | 없음 |
| 새 Windows build ID / ZIP / 전달 기록 | **smoke-01:** 빌드 M4-H1-win-01·M4-H7-win-01, `Builds/M4-smoke-01.zip`(279,235,113 bytes, SHA-256 `7c901435f43a5ce7a5025bb9fd94a9970ee4d19f4e75f9485240297b60eacc59`). 사용자가 Google Drive로 직접 전달. **smoke-02:** 빌드 M4-H1-win-02·M4-H7-win-02, `Builds/M4-smoke-02.zip`(279,241,318 bytes, SHA-256 `02da12a5c4538c7c3aee2979e139ff729badd7ffbab24b53b92ac2f9f7b6b02b`), 케이스 smoke-s2 22건. 사용자 전달·실행 완료(2026-10-30) |
| 새 Windows pilot/formal run ID / 결과 ZIP | **smoke-01 결과:** run `W-H1-smoke-20261030T002951`·`W-H7-smoke-20261030T002951`. 원본 `Evidence/PhysicsBench-Results-20261030T002951.zip`(SHA-256 `673b3af2…6aa2`, 보존), 추출본 `Evidence/M4/windows/PhysicsBench-Results-20261030T002951/`, 분석 `Evidence/M4/analysis/W-H*-smoke-20261030T002951-a1`, 요약 `Evidence/M4/windows-smoke-01-summary.json`. **smoke-02 결과:** run `W-H1-smoke-20261030T014743`·`W-H7-smoke-20261030T014743`, 원본 `Evidence/PhysicsBench-Results-20261030T014743.zip`(SHA-256 `9f8da19f…c1b3`, 보존), 추출본 `Evidence/M4/windows/PhysicsBench-Results-20261030T014743/`, 분석 `Evidence/M4/analysis/W-H*-smoke-20261030T014743-a1`, 요약 `Evidence/M4/windows-smoke-02-summary.json`. **M5 탐색 pilot-01:** run `W-H0-base/W-H1-base/W-H1-nocatch/W-H7-base-pilot-20261030T150726`, 원본 `Evidence/PhysicsBench-Pilot-Results-20261030T150726.zip`, 요약 `Evidence/M5/M5-windows-pilot-01-summary.json`. **M5 탐색 pilot-02c:** run `W-Hm1-base/W-Hm1-unfreezeY/W-Hm1-unfreezePosY/W-H0-base/W-H1-base/W-H7-base-p02c-20261108T163441`, 원본 `Evidence/PhysicsBench-Pilot02c-Results-20261108T163441.zip`(SHA-256 `85d383cdcd3023cdf7bbc412a862fd719612438a9fb0e89fe5713079067ce515`, 보존), 요약 `Evidence/M5/M5-windows-pilot-02c-summary.json`. M6 이후 정식 run 없음 |
| M5 pilot-02 묶음 | **전달 대상은 `Builds/M5-pilot-02c.zip`**(SHA-256 `e388092a…0833`, 445,606,099 bytes, case e2c, `RunPilot02c.cmd`, 6 run, 상세 `Evidence/M5/M5-pilot02c-prep.json`). 이하 02b는 r3 리뷰 반영 전 판으로 **보존만**: `Builds/M5-pilot-02b.zip`( SHA-256 `f118b17236f7e3534ac06934344f92b73592d7d83dfeef0e7993196aaaf5fd31`, 445,604,785 bytes): 빌드 M5-Hm1-win-03·M5-H0-win-01·M5-H1-win-01·M5-H7-win-01, case `Harness/Cases/e2b/`, `RunPilot02.cmd`(Hm1 base → Hm1 unfreezeY → H0 → H1 → H7), `README-Windows-pilot02.txt`. 보존만: `Builds/M5-pilot-02.zip`(case e2, 사후 표적 없음). run ID 형식 `W-<V>-<variant>-p02-<시각>` |
| M5 repeat-01 묶음 | `Builds/M5-repeat-01.zip`(83,175,485 bytes, SHA-256 `f2217de87bf46decb75f21fb3222d100a3c1a1b33c8c94b2b0cd85b2686cc4ca`), 빌드 M5-H0-win-01 재사용, `RunRepeat01.cmd`. 동결 `Evidence/M5/M5-repeat-r1-freeze.json`. Windows 실행 대기 |
| M5 트랙 B 묶음 | **trackB-01** `Builds/M5-trackB-01.zip`(1,021,249,682 bytes, SHA-256 `11f8422d90c67a200e05114442fcf0c12008d9c260330fdfea629468dfce6308`), 빌드 M5-B-{S0a,S0b,S0p,S1,S2,S3}-win-01, 30 run, `RunTrackB01.cmd`, 동결 `Evidence/M5/trackB/M5-trackB-freeze.json`+addendum. **trackB-02** `Builds/M5-trackB-02.zip`(170,208,799 bytes, SHA-256 `e9820792797877427cb5aff987968e86e47598672d04fbe123f1ae4cc4405826`), 빌드 M5-B-S3c-win-01, 6 run, `RunTrackB02.cmd`, 동결 `M5-trackB02-freeze.json`. Mac 확인 빌드 M5-B-{S0b,S0p,S3,S3c}-mac-01(결과 인용 안 함, `Evidence/M5/trackB/runs/`). 둘 다 Windows 실행 대기 |
| 새 정식 분석·재실행·최종 보고 | 없음 |

앞으로 산출물마다 상대 경로·실제 보관 위치·SHA-256·관련 milestone/experiment/build/run ID와 완료 여부를 추가한다. 파일이 다른 기기로 이동하면 이전 경로와 새 위치를 기록한다.

## 7. 변경·의사결정 기록

| 날짜 | 결정/실제 수행 | 영향 |
|---|---|---|
| 2026-10-29 | 원래 낙하 환경은 Windows 빌드라고 사용자 확인 | 에디터 수치만으로 바닥 개선을 주장하지 않음 |
| 2026-10-29 | 커밋·에셋·플레이어 설정을 정적 감사 | H1~H7 역사 복원 비교 필수, A/B 통제 실험 별도 |
| 2026-10-29 | Windows PC는 있으나 개발환경 설치 시간이 부담이라고 사용자 확인 | Windows에서는 실행만 하는 인계 묶음 계획 |
| 2026-10-29 | 모듈/디스크 메타데이터 읽기 전용 확인 | 모듈 설치 공간 충분, 버전별 순차 복원. 삭제·설치 안 함 |
| 2026-10-29 | 마일스톤 실행 지침과 이 현황 파일 작성 | M0 완료. 구현 승인은 아직 대기 |
| 2026-10-29 | 사용자 “그럼 m1부터 진행 시작해줘.” 실행 지시 | 계획 실행 승인 기록, M1 시작 |
| 2026-10-29 | M1 원본 상태 고정 완료. tracked/untracked 16,891개 항목 해시, tracked diff 없음 | M1 DONE, M2의 독립 복원 준비 시작. 에디터를 열거나 원본 씬을 저장하지 않음 |
| 2026-10-29 | H1 복원 원본 임포트·컴파일·Edit Mode 감사 통과. 원본 5,889개 파일은 Git blob과 일치 | M2 DONE, M3 READY. Standalone backend Mono2x 확인. 물리 테스트/Windows 빌드는 미수행 |
| 2026-10-29 | M3 설계 결정: 런타임은 원시 기록만, 판정은 Python+임계값 JSON. trial마다 씬 재로드, 리플렉션 없이 H1~H7 공통 공개 API만 사용(git show로 확인), 가상 키보드·마우스만 페어링, yaw는 실제 Look 경로로 정렬 | 임계값 교정 시 Windows 재빌드 불필요. 역사 버전 이식성 확보 |
| 2026-10-29 | M3 검증 중 하네스 결함 4건 발견·수정(자식 body 접촉 누락·자기 접촉 오인, 초기 침투 누락, 자식 속도 미초기화, 캡처 가림). 임계값은 바꾸지 않음 | 수정 후 판정 검증 10/10, seed 3종 반복 일치. M3 DONE, M4 READY |
| 2026-10-29 | H1 Mac 빌드 로그에서 MeshCollider Read/Write 오류 140개 오브젝트 관찰(바닥 포함). H1 `bakeCollisionMeshes: 0` | M5 우선 확인 가설로만 기록. M5 감사에 버전별 Prebake Collision Meshes·비균일 스케일·cooking 옵션·Read/Write 추가. 원인 확정 아님 |
| 2026-10-29 | 리뷰 반영 보완(재빌드 mac-06·07): 자식 콜라이더 침투·자기 침투·상대 위치 관측, 자식 자세 복원, 기준 벽 법선, no_wall_contact·벽면 교차 통과, 물리 시간 입력 기준, sabotage time_scale_zero·hang, Trigger 연석·60° 사례 | 판정 검증 14/14(seed 7·8). 정착 종료 위치 재현 1~5mm(이전 1~12cm). 0번 스텝 속도 차 0.03~0.14m/s 잔존(순서 무관) — M6 반복 횟수 근거 |
| 2026-10-29 | 최종 보완(mac-08): 씬 로드 시간을 stall로 오인하던 감시 수정, 관측 버퍼 포화 플래그, slow_load 사례. 개미 스킨 메시가 자식 body 본을 따름을 확인해 child_fall / child_transient_dip / child_beyond_wall을 별도 보고 | 판정 검증 15/15. 점프 착지에서 자식 body 1스텝 3cm 바닥 침투(재현 3/3)를 M5 후보 기전으로 기록 |
| 2026-10-29 | M4 전 디스크 재검토(읽기 전용): 여유 11.28GiB, H7 원본 1.46GiB, H7 임포트 추정 약 6.4GiB(현재 프로젝트 Library 5.1G 기준). H1을 그대로 두면 H7 복원 시점 여유 약 9.9GiB로 도구 조건(원본+10GiB=11.46GiB) 미달, 완료 후 여유 약 2.4GiB로 5GiB 기준 미달. 대안: (A) H7 Library를 현재 프로젝트 Library에서 APFS 복제(cp -c, HEAD=H7·tracked diff 0 확인) (B) H1 PackageCache·Bee만 정리(2.36G) (C) 사용자 공간 확보 | 사용자 결정 대기. 삭제·설치 없음 |
| 2026-10-29 | 디스크 재측정: 여유 19.89GiB(20,857,940 KiB). 사용자 쪽에서 약 8.6GiB 확보, 휴지통 0B. H1 작업 공간 4.2G·mac-08 빌드 그대로 | H1 보존한 채 M4 진행 가능: 모듈 설치 후 약 19.0, H7 복원 시점 약 18.5(도구 조건 11.46 충족), H7 빌드·묶음 후 약 11GiB 예상. A/B/C 대안 불필요. M5에서 H2 이후를 추가 복원할 때는 H1/H7 중 정리 필요 가능성 재검토 |
| 2026-10-30 | 독립 리뷰(`Evidence/Review-20261030-codex/review.md`) 검토. R1~R6 모두 코드·원시 기록으로 확인: R1 SM05 첫 벽 접촉이 `V_surface2`(대상 연석 아님), R2 조합 입력 부분 수신 미검출, R3 child_fall 시 outcome=pass 가능, R4 법선 평균으로 벽 접점 소실 가능, R5 fixture Ground 레이어 코드가 `return` 뒤라 미실행(Default 레이어), R6 본 문서 상단 오래된 M4 지시 | R6은 즉시 수정. R1~R5는 런타임/분석기 수정 대상이며 새 revision·build ID로 재검증 예정. 현재 M4-smoke-01은 실행 경로 확인용 smoke로만 해석 |
| 2026-10-30 | Windows smoke-01 회수·분석(위 요약). 리뷰 R1~R5 반영 하네스(케이스 r4/s2 22건: CK16 부분 입력, CK17 단일 MeshCollider 연석 추가). H1 Mac 검증 `M4-H1-mac-smoke02-01` 판정 검증 17/17. RunSmoke.cmd의 환경 파일 인코딩·결과 ZIP 경로 수정 | smoke-02 빌드(H1/H7 Windows, H7 Mac 검증)와 묶음 제작 진행 |
| 2026-10-30 | H7 CK17에서 CCD 접촉 콜백이 1스텝 늦어 런타임 preContactSpeed=0 확인 → 분석기가 궤적의 첫 접촉 전 0.06초 최대 수평 속도(`approachSpeedBeforeContact`)를 따로 계산(thresholds-r3). smoke-02 묶음 제작 | 런타임 재빌드 불필요. 구 빌드 디렉터리(win-01, H7 mac-01) 삭제, build-result 사본 보존 |
| 2026-10-30 | 2차 리뷰(`Evidence/Review-20261030-codex-r2/review.md`) 반영: 연석 case 자식 낙하 예외를 단계 시작이 아닌 실제 이탈 확정 스텝(`escapeStep`) 기준으로 수정, 이탈 뒤 낙하는 `child_fall_after_escape` 플래그. 분석기 회귀 테스트 `Harness/Tools/test_analyzer.py` 6건 통과(`Evidence/M4/analyzer-regression-r3.txt`). smoke-02 Mac 기록 재분석(a3) 결과 변화 없음. 문서 모순(최종 갱신일, 승인 표, Windows PC 상세, README) 정리 | 분석기만 변경, 런타임·smoke-02 묶음 그대로 유효 |
| 2026-10-30 | smoke-02 Windows 결과 회수·분석(위 요약). 하네스 경로 검증 기대 일치, Mac 판정과 22건 일치, 환경 파일 UTF-8·결과 ZIP '/' 경로 정상 | M4 DONE, M5 READY |
| 2026-10-30 | M5 1차 감사: (1) H1에는 `PostProcessingVolume.prefab/Ground`의 맵 전체 BoxCollider(윗면 y=0)가 있어 Mesh 바닥 실패를 가림. 5859367(05-22)에 추가, 2dd8695(05-29)부터 제거. H0에는 없음 (2) H0 에셋은 gitlink가 없어 23de6f3c(05-18 20:06)로 추정, 참조 GUID 대조로 물리 관련 누락 없음 (3) H7 S3 연석은 Curb_18 Box가 시각면보다 0.111m 돌출, smoke에서 고속 접근 시 도로면 아래로 1.5cm 내려가 연석 아래로 파고든 뒤 후진 이탈 | 실험 A의 Mesh 기준선을 H0로 재검토. H0 복원에는 디스크 결정 필요 |
| 2026-10-30 | M5: H0 복원(prepare_snapshot·verify_snapshot에 H0 하위 경로·추정 에셋 지원 추가), 관측 어댑터(PHYSICS_BENCH_H0), profile `deactivatePaths`(통제 조건 전용), 분석기 정지 지지 추론(thresholds-r4). Mac 탐색 e1 결과 기록. 받침판 제거 첫 실행(nocatch-e1-01)은 zsh 인수 분리 오류로 무효 표시·보존, e1-02로 재실행 | Windows pilot 필요 |
| 2026-10-30 | M5 감사 03: H7 런타임 cooking 필요 MeshCollider 46개(고유 경로 44)는 모두 소품, 바닥·연석 없음. Windows smoke log 오류 경로와 대조(피자 가게 7개는 log에 없음, 런타임 차량 31개는 log에만 있음) | A·B 원인 후보에서 제외. Windows pilot 결과 대기 |
| 2026-10-30 밤 | r3 리뷰 검토: P2 미지원 동작(확인), P2 공중 고정 pass(확인), 116→80 = Y 위치+Y 회전 해제(확인), 이전 표현(확인). capabilities-r1·분석기 플래그/passEvidence(outcome 불변)·test_analyzer 11/11, Mac Hm1 두 run 재분석 a2(판정 a1과 동일), 요약·audit-04 정정 필드, 문서 표현 통일. thresholds-r4·case e2b·pilot-02b 묶음 변경 없음 | pilot-02b 그대로 실행 가능 |
| 2026-11-02 | 사용자 "B로 진행": r3 리뷰 세 항목을 묶음에 반영한 pilot-02c 제작. case e2c(미지원 동작 제거, e2b에서 파생), Hm1 constraints 112 통제 추가, 동결 기록 후 Mac 확인 2 run(`M5-Hm1-base-mac-e2c-E2_B-01`, `M5-Hm1-unfreezePosY-mac-e2c-01`). 진행 문서 9행에 116→80 = Y 위치+Y 회전 해제 명시. 분석기·thresholds-r4·e2b·capabilities-r1·02b 묶음 변경 없음 | 02b 대신 02c를 Windows에서 실행 |
| 2026-11-08 | 사용자 요청으로 인계 묶음 M5g판 제작. 포함 규칙은 M5f와 같음(*.log·*.png·ZIP 본체·*.pyc·.DS_Store 제외). 직전 판은 `physics-validation-checkpoint-M5f.zip`으로 이름 변경·보존 | 문서·묶음만 변경. 하네스·case·분석기·02c 실행 묶음 변경 없음 |
| 2026-11-08 | pilot-02c Windows 결과 회수·분석(동결 도구 그대로). 6 run 완료. H0에서 Windows 바닥 관통 낙하 첫 관측(연석 횡단 경로 C 6/24, K 7/24, P11R 정지 낙하). Hm1 112=80 낙하 집합 동일. H1 C11_jump는 받침판 위 일시 하강, H7 P24T는 case 시작 높이 결함으로 분리. H7 소품 관통·Box 단차 Windows 확인. 요약 `Evidence/M5/M5-windows-pilot-02c-summary.json` | M5 해석 갱신("H0 미재현" → "case 범위 확대 후 1회 관측"). 반복 확인 또는 M6 이동은 사용자 결정 대기. M5g 체크포인트에는 이 결과가 없음 |
| 2026-11-08 | 사용자 "1번으로 진행해줘": H0 재현성 확인 repeat-01 준비. case r1(e2c H0 부분집합 53건, 정의 불변), full 6 run(같은 seed 2회 포함) + P11R 단독 10 run(fps 30/60/120). 동결 기록을 묶음 제작 전에 저장. 재빌드 없음, 동결 도구 변경 없음 | Windows 실행 대기 |
| 2026-11-08 | 사용자 확인: H1의 맵 전체 받침판(`PostProcessingVolume.prefab/Ground` BoxCollider, `5859367` 05-22 추가)은 사용자가 직접 깐 박스 콜라이더(낙하 대응). 이후 문서의 "숨은 받침판"은 게임 화면에 보이지 않는다는 뜻이며 출처 불명이라는 뜻이 아니다. 정적 감사 `Evidence/M5/M5-audit-06-ground-plate-history.json`: `e4b5c50`(05-26 "공중에 뜨는 버그" 수정)에서 받침판을 0.1m 내려 도로면(-0.1)과 맞췄고, 같은 커밋에 접지 판정 교체·`m_Convex=0` override 43개 명시(실효 변화는 H0의 convex 2개 이하로 추정)가 함께 있음. `9b83a24`(H1 [2nd Build])에서 받침판이 윗면 y=0으로 되돌아감 | 해석 갱신: 받침판 = 사용자의 임시 대응이 Mesh 바닥 실패를 가린 구성. '공중 뜸'의 원인을 받침판 하나로 특정하지 않음 |
| 2026-11-08 | 이력 반영 해석 검토. H0 이동 중 낙하 13건은 초기 침투 0·직전 지지 일반 MeshCollider(convex 아님) → 결론 유지. P11R은 convex 내부 시작 인위 조건으로 분리. H1 기본 run 45건이 받침판 안 0.1m 시작(2nd Build에서 받침판 윗면 y=0 복귀 영향) → H1 기본 run을 기준선으로 쓰지 않음. H7 K03·K04는 Box 안 시작 → 수정 전후 비교에서 제외. `e4b5c50` convex 43개 해제 표현 정정(실효 2개 이하 추정). 포트폴리오용 이력 문서 `Docs/physics-collider-ground-plate-history.md`, 결과 전 해석 보충 `Evidence/M5/M5-repeat-r1-freeze-addendum.json` | repeat-01 묶음 변경 없음. M6 입력: 충돌면 기준 시작 높이, start_embedded 플래그 |
| 2026-11-08 | 사용자 진술: 에디터에서 콜라이더 모양을 보려고 convex를 켰다 끄고 커밋했을 수 있음. `e4b5c50`의 값 0 override 43개·H0 실효 convex 2개와 맞음 | H0 convex 2개는 잔여 설정으로 추정, P11R은 포트폴리오 제외. 'Convex 시도'는 에디터 검토로 정리. H0 이동 중 낙하 결론 영향 없음 |
| 2026-11-08 | 사용자: 테스트를 수정 이력 흐름(문제→근거→수정→개선)과 주장 ①~④(Mesh 불안정·Convex 부적합·Box 안정·물리 설정)에 맞추도록 요청. 어드바이저 검토 반영해 트랙 B(H7 코드 고정, 바닥·물리 설정만 변경) 설계 초안 `Docs/physics-collider-trackB-plan.md` 작성. 발견: ④에 반대 데이터 있음(10/23 에디터, Box+CCD S2 끼임 6/9, Discrete 0/9) → 2×2로 설계. H0·H1 바닥 MeshCollider 실패 방식이 다름(H0 기본 cooking·런타임 cooking 불필요 / H1 EnableMeshCleaning 제거·런타임 cooking 필요) → S0a·S0b로 분리. 에셋 cooking 변경 `de8bfd48`(05-22 07:20)과 받침판 `5859367`(07:21) 1분 차이 | 설계 확정 후 기하 감사부터 진행. 아직 빌드·실행 없음 |
| 2026-11-08 | 트랙 B 기하 감사 완료(Mac 에디터, 복제 공간 `project-H7B`, 씬 저장 안 함). 판정 규칙 고정 후 분석. ② Convex 부적합 지지(연석 단차 90/90 소실, 도로가 연석 높이로 메워지고 가장자리 경사로). ③ 받침판 한계 지지(단차 84~87/90 소실). ③ 현재 Box는 C07 남쪽 도로 0.1m 단차 한 구간 때문에 규칙상 미달, 나머지 단면 오차 ≈0. 요약 `Evidence/M5/trackB/M5-trackB-geom-summary.json` | 다음: 변형 빌드·런타임 case(①·④) |
| 2026-11-08 | 트랙 B 런타임 준비: 변형 빌드 Windows 6(S0a·S0b·S0p·S1·S2·S3)·Mac 3, case `Harness/Cases/trackB/`(생성 `Tools/make_cases_trackB.py`), 판정 thresholds-B1(0.5m 낙하). Mac smoke(결과로 인용 안 함): 현재 코드는 dt 0.02에서 자식 body가 스텝당 3.9mm 가라앉음(CK01 포함, dt 0.01이면 0, CCD 무관) → 트랙 B 1차 판정을 rootOutcome으로 결과 전 확정. 현재 설정(0.01/CCD) 연석 돌진 9/18이 도로면 아래로 파고든 뒤 연석 Box 아래로 관통(위로 넘음 아님, 판정 착시 아님). S0b 바닥 24/24 낙하·바닥 Read/Write 오류 확인, S0p(Read/Write 켬)도 14/24 낙하. 동결 `Evidence/M5/trackB/M5-trackB-freeze.json`, 묶음 `Builds/M5-trackB-01.zip` | Windows 실행 대기. 복제 공간 Read/Write는 false로 복원 |
| 2026-11-08 | 연석 관통 기전 기록(`Evidence/M5/trackB/M5-trackB-curb-mechanism.json`): 지름 1.9cm 충돌체가 스텝당 2.5cm 이동으로 연석 Box 밑면 근처에 한 스텝에 진입 → PhysX가 최단 방향(아래)으로 밀어 도로 Box 속에 끼인 채 연석 밑 통과. 사용자 승인으로 가설 검증용 S3c(테스트 복제 빌드만, 연석 Box 820개 밑면 -0.3) 제작. 윗면 S3와 일치. Mac 확인: 관통 9/18 → 0/18, 끼임 0(진입은 여전, 뒤로 밀려남). trackB-02 동결·묶음 | 실제 게임 미변경. '고쳤다'는 표현은 실제 적용·회귀 확인 뒤에만 |
| 2026-11-09 | 사용자 요청으로 진행 문서 갱신(요약 첫머리에 '한눈에 보기' 추가, 상태판·디스크·산출물 행 보완)과 정리 후보 목록 작성(`Docs/physics-collider-cleanup-candidates.md`). 구 체크포인트 ZIP 11개를 M5g와 대조: M3·M4a 전용 PNG 157개 외 전부 포함, PNG 원본은 Evidence에 존재. 디스크 여유 4.0GiB 측정 | 문서만 변경. 삭제·실행·하네스·case·분석기 변경 없음 |
| 2026-11-09 | 사용자 승인으로 정리 A·D와 미사용 Benchmarks 루트 파일 삭제(목록은 `physics-collider-cleanup-candidates.md` 첫 절). 인용 중인 `CurbStuckRuntimeMatrix/20261023_023059/`와 git 추적 파일, `tmp/pdfs/`는 유지. 디스크 4.0→5.0GiB | 실행 묶음(repeat-01·trackB-01·02c·trackB-02)과 Evidence는 그대로. 직전 체크포인트 판은 M5f만 남음 |
| 2026-11-09 | 사용자 지시로 Benchmarks 루트의 git 추적 파일 40개(Diag*·FindSpots·Refresh·RunTunneling*·`Tunneling/`) 작업 트리에서 삭제. stage·커밋 안 함 | 게임 저장소 tracked diff가 생김(삭제 40). 하네스·실행 묶음 영향 없음. Benchmarks 루트에는 `CurbStuckRuntimeMatrix/20261023_023059/`와 `PhysicsValidation/`만 남음 |
| 2026-11-09 | 삭제 커밋 `ceb0bd9`를 main에 fast-forward 병합, 브랜치 삭제. Windows 결과 3개(repeat-01 SHA `af3d52ea…3588`, trackB-01 `77854506…69ee`, trackB-02 `0ea72810…ae1e`) 추출·동결 분석기로 판정·집계. 어드바이저 검토 반영(CK 문구, C09 해석 '구분 불가', ④ 끼임 상충 명시, 0.02 관통은 도로면 높이 수평 관통 확인) | WAITING_USER_RUN 해제. 주장 ①~③ 지지(①은 조건부), ④는 관통만 지지·끼임 인용 불가. 다음 단계 사용자 결정 대기 |
| 2026-11-09 | 사용자 "a 진행": 정리 후보 B 삭제(Mac 빌드 6·트랙 B Windows 빌드 폴더 7·M4-smoke-01/02·M5-pilot-01 ZIP). 디스크 27→32GiB | Evidence·실행 묶음 4개·역사 빌드 4개 유지 |
| 2026-11-09 | 사용자 "b, c, d 순서대로 진행": CurbFix 스크립트·검증, cooking 감사, case fix1(b 62·c 12)·게임 profile, 동결 후 빌드 6개(Mac·Win × BASE·CURBFIX·COOK30), Mac 점검 6 run, Windows 묶음 `M5-fix1.zip`. 어드바이저 검토 반영(S3c 통제를 원본 수정에 섞지 않음, 원본 적용은 사용자 확인 후) | Mac 점검에서 원본 씬 S2 끼임 재현 → 트랙 B ④ 해석 정정 후보. 소품 지표 수정 addendum-c2 |
| 2026-10-31 | `physics-collider-history-audit.md`를 진행 문서·M5 JSON에 맞춤: M5 보충 단락, 기준표 Hm1 행 추가, H0 "최초 낙하 기록"→"낙하 수정 중" 정정(에셋 `23de6f3c` 추정, constraints 116→80), 맵 cooking 30→26·받침판 연표, 이동 변경표 Hm1·`4d73f8d` 행, 남은 감사의 M5 기준 상태. 새 사실 추가 없음 | 문서만 변경 |
| 2026-10-30 밤 | 사용자 "리뷰는 됐고 마일스톤 다음 단계 진행" → 갱신 review.md 반영 생략. Hm1 복원(승인), 어댑터 PHYSICS_BENCH_HM1(입력 읽기·카메라 yaw 준비 쓰기), profile `playerConstraints` 통제 옵션, probe에 MeshCollider 범위·지점 파일 옵션, case e2/e2b 생성기, 지지면 감사·낙하 경로 집계 도구 추가. 무효 run `M5-Hm1-base-mac-e2-01`(카메라 yaw 미설정, `.INVALID.txt`) 보존. thresholds-r4·분석기 변경 없음 | Windows pilot-02b 대기 |
| 2026-10-30 | M5 Windows pilot-01 회수(다른 PC: Win11, i9-14900HX, RTX 4060 Laptop). H0·H1·H1 받침판 제거 Mac과 72건 일치, 받침판 제거 35/68 낙하 Windows 재현, H0 낙하 0. H1·H7 probe로 P27T·P30T의 시각 타일면이 y=0임을 확인 | 앞선 "H1 타일이 받침판보다 높음" 해석 정정. e1 시작 높이를 버전별로 다시 잡을 것 |
| 2026-10-29 | H1~H7 키 바인딩·공개 API 동일 확인. H7 profile에서 HangoverEffect·TutorialManager 비활성(통제), Destination 트리거는 watchdog 처리 | M4/M5 역사 빌드에 같은 하네스 사용 가능 |
| 2026-10-29 | 디스크 여유 11GiB(M3 종료 시). 이 작업 공간·빌드 약 4.7GB, 이전 빌드 3개는 결과 JSON 보관 후 삭제 | M4 모듈 예산 2~3GB 후 약 8GiB 예상. 설치 직전 재측정 |

## 8. 다음 세션의 갱신 템플릿

아래를 복사해 실제 수행 직후 작성한다. 빈 템플릿을 실행 증거로 사용하지 않는다.

```text
갱신 일시 / 시간대:
담당 세션/작업자:
적용 승인 근거와 범위:
현재 milestone / 상태:
실제 마지막 완료 작업:
작업 루트/에셋 SHA 및 사용자 변경:
실제 작업 공간 / Unity 실행 상태 / 프로세스·로그:
생성·수정 파일 / 보존한 원본:
experiment ID / case hash / build ID / run ID / attempt:
결과 보관 위치·SHA / 전달한 Windows 묶음:
완료 조건을 충족한 증거:
오류·중단·미재현·미회수 항목:
남은 디스크 / 다음 작업 예상 용량:
지금 기다리는 사용자 행동:
다음 담당자가 할 구체적인 한 작업:
```
