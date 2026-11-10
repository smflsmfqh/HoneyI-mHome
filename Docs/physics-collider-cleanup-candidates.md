# 바닥 충돌 검증 — 정리 후보 파일 목록

작성: 2026-11-09 (Asia/Seoul), Claude Code 세션.

## 삭제 실행 기록 (2026-11-09)

사용자 승인: "A와 D 지워줘. tmp/pdfs는 지우지말고, benchmarks 루트의 기존 파일들은 현재 테스트에서 사용한 적이 없고 앞으로 사용할 일 없으면 지워도 돼"

- **A 전부 삭제:** `Builds/M5-pilot-02.zip`·`M5-pilot-02b.zip`(+sha256), 체크포인트 M2·M3·M4a·M4c·M4done·M5a~M5e(+sha256), `Docs/physics-collider-handoff-M2·M5c·M5d·M5e.zip`(+sha256)
- **D 삭제:** `Assets/_Recovery/`, `Assets/_Recovery.meta`. `tmp/pdfs/`는 사용자 지시에 따라 남김
- **Benchmarks 루트(git 미추적) 삭제:** `AnalyzeCurbStuckRuntime.py`, `CURB_RUNTIME_TESTS.md`, `FLOOR_BOUNDARY_TESTS.md`, `RunCurbStuckRuntimeMatrix.cs`, `RunCurbStuckRuntimePilot.cs`, `RunFloorBoundaryBenchmark.cs`, `youtube-icon.png`, `CurbStuckRuntime/`(10/23 pilot 3개), `CurbStuckRuntimeMatrix/20261023_022929/`. 스크립트는 하네스가 대체했다(`Harness/README.md` 109~111행). 결과 폴더는 인용한 적이 없다
- **남긴 것:** `Benchmarks/CurbStuckRuntimeMatrix/20261023_023059/`. 트랙 B 설계(`physics-collider-trackB-plan.md` 61행, 주장 ④의 반대 데이터 → 2×2 설계 근거)와 `physics-collider-history-audit.md` 91행이 인용한다. 생성 스크립트 사본(`runner_source.cs`)과 `RESULTS.md`·`summary.csv`가 폴더 안에 있어 단독으로도 읽을 수 있다
- **git 추적 파일도 삭제(같은 날 사용자 추가 지시 "git 추적 파일들도 지워줘"):** `Benchmarks/DiagGap.cs`·`DiagRoadMesh.cs`·`DiagTrial.cs`·`FindSpots.cs`·`Refresh.cs`·`RunTunnelingBenchmark.cs`·`RunTunnelingSpots.cs`, `Benchmarks/Tunneling/`(33개), 모두 40개. 작업 트리에서만 지웠고 stage·커밋은 하지 않았다. git 기록에는 남아 있어 `git checkout -- Benchmarks`로 되돌릴 수 있다
- 디스크 여유: 4.0GiB → 5.0GiB

## B 삭제 실행 기록 (2026-11-09)

사용자 "a 진행". 조건(Windows 결과 회수·분석 완료, Mac smoke와 run당 일치) 충족 후 삭제:
- `.physics-work/builds/` Mac 6개(M5-Hm1-mac-03, M5-H7-mac-02, M5-B-S0b·S0p·S3·S3c-mac-01), 트랙 B Windows 7개(M5-B-S0a·S0b·S0p·S1·S2·S3·S3c-win-01). build-result·variant 기록·player log 사본은 `Evidence/`에, Windows 빌드 내용은 `Builds/M5-trackB-01.zip`·`-02.zip`에 남음
- `Builds/M4-smoke-01.zip`·`M4-smoke-02.zip`·`M5-pilot-01.zip`(+sha256). SHA는 진행 문서 6절에 남음
- 남은 빌드 폴더: M5-H0-win-01, M5-H1-win-01, M5-H7-win-01, M5-Hm1-win-03. 남은 묶음: pilot-02c, repeat-01, trackB-01, trackB-02
- 디스크 여유: 삭제 직전 27GiB(그사이 사용자 쪽 정리로 증가) → 32GiB
- 첫 실행은 zsh가 경로 목록 변수를 나누지 않아 아무것도 지우지 않았고(존재하지 않는 단일 경로), 배열로 다시 실행했다

아래 목록은 삭제 전 작성한 원래 판단이다.

측정 시점 디스크 여유: **4.0GiB**(228GiB 중 98% 사용). Unity 배치 빌드 하나에 0.4~0.6GiB, 임포트는 수 GiB가 필요하므로, 새 빌드를 하기 전에 정리가 필요하다.

크기는 `du` 값이다. `.physics-work/project-H7B`는 `project-H7`에서 APFS 복제로 만들었기 때문에, 둘 중 하나만 지우면 실제로 확보되는 공간은 표보다 훨씬 적을 수 있다.

## A. 지금 지워도 결과·재현에 영향 없음 (약 1.1GB)

| 파일 | 크기 | 근거 |
|---|---|---|
| `Benchmarks/PhysicsValidation/Builds/M5-pilot-02.zip` (+`.sha256`) | 446MB | 사후 표적을 넣기 전 판. 전달하지 않았다. SHA는 진행 문서에 남아 있다 |
| `Benchmarks/PhysicsValidation/Builds/M5-pilot-02b.zip` (+`.sha256`) | 446MB | r3 리뷰 반영 전 판. 전달하지 않았고 02c로 대체됐다. 들어 있는 빌드(H0·H1·H7 win-01, Hm1-win-03)는 `.physics-work/builds/`에 그대로 있다 |
| `Benchmarks/PhysicsValidation/physics-validation-checkpoint-M2·M3·M4a·M4c·M4done·M5a~M5e.zip` 10개 (+`.sha256`) | 약 205MB | 최신 M5g(`physics-validation-checkpoint.zip`)와 파일 목록을 대조했다. M2·M4c~M5e는 모든 파일이 M5g에 들어 있다. M3·M4a에만 있는 것은 PNG 67·90개뿐이고, 이 PNG 원본은 모두 `Evidence/`에 남아 있다(2026-11-09 확인) |
| `Docs/physics-collider-handoff-M2·M5c·M5d·M5e.zip` (+`.sha256`) | 약 180KB | 문서 묶음의 이전 판. 용량에는 영향이 거의 없고, 헷갈림만 줄여 준다 |

`physics-validation-checkpoint-M5f.zip`은 직전 판이라서 M5h를 만들 때까지 남겨 둔다(목록상으로는 M5g에 모두 포함된다).

## B. Windows 결과(repeat-01·trackB-01·trackB-02)를 회수하고 분석한 뒤 지울 수 있음 (약 7.1GB)

| 파일 | 크기 | 조건·근거 |
|---|---|---|
| `.physics-work/builds/*-mac-*` 6개 (M5-Hm1-mac-03, M5-H7-mac-02, M5-B-S0b·S0p·S3·S3c-mac-01) | 약 3.2GB | Mac 확인용이며 결과로 인용하지 않는다. Windows 결과가 Mac smoke와 크게 다를 때 다시 돌려 볼 수 있으니, 그때까지는 남긴다. build-result·player log 사본은 `Evidence/`에 있다 |
| `.physics-work/builds/M5-B-*-win-01` 7개 (S0a·S0b·S0p·S1·S2·S3·S3c) | 약 3.0GB | 같은 내용이 `Builds/M5-trackB-01.zip`·`M5-trackB-02.zip` 안에 들어 있다. 결과에 문제가 없어 묶음을 다시 만들 일이 없다는 게 확인되면 지운다 |
| `Builds/M4-smoke-01.zip`·`M4-smoke-02.zip` | 558MB | Windows 실행과 회수가 끝났다. 결과 원본 ZIP은 `Evidence/`에 있다. 묶음 SHA는 진행 문서 6절에 있다. 같은 묶음을 다시 돌릴 계획은 없다 |
| `Builds/M5-pilot-01.zip` | 362MB | 위와 같다. 들어 있는 빌드 3개도 `.physics-work/builds/`에 있다 |

`Builds/M5-pilot-02c.zip`(446MB)은 포트폴리오에서 인용할 H0 낙하 관측의 실행 묶음이다. repeat-01 결과가 나오기 전까지는 남긴다.

## C. 남겨 두는 것

| 파일 | 이유 |
|---|---|
| `Builds/M5-repeat-01.zip`, `M5-trackB-01.zip`, `M5-trackB-02.zip` | Windows에서 아직 실행하지 않았다 |
| `.physics-work/builds/M5-H0-win-01`, `M5-H1-win-01`, `M5-H7-win-01`, `M5-Hm1-win-03` (약 1.1GB) | repeat-01이 H0 win-01을 쓴다. M6 정식 묶음을 만들 때 다시 쓸 수 있다 |
| `.physics-work/project`(H1), `project-H0`, `project-Hm1`, `project-H7`, `project-H7B` (du 합 약 25GB, 그중 Library 약 21GB) | 역사 버전을 다시 빌드하려면 필요하다. Library는 지울 수 있지만, 다시 임포트하는 데 시간이 오래 걸린다. 공간이 더 필요해지면 H1(`project`)과 Hm1부터 Library만 지우는 쪽을 먼저 검토한다 |
| `.physics-work/H7B-AntDemoScene.unity.orig` | S3c 빌드 스크립트가 복제본 씬을 되돌릴 때 쓰는 원본이다(SHA 9d0bb583…) |
| `Benchmarks/PhysicsValidation/Evidence/` 전체(약 1GB), INVALID·PARTIAL 표시 파일 포함 | 원시 결과는 보존 원칙에 따라 남긴다. 무효로 표시한 기록도 왜 무효인지 보여 주는 근거라서 지우지 않는다 |
| `Harness/`, `Tools/`, `README.md`, 최신 체크포인트 `physics-validation-checkpoint.zip` | 현재 쓰고 있다 |

## D. 이 검증과 관계없음 — 사용자 판단 (손대지 않음)

| 파일 | 관찰 |
|---|---|
| `Assets/_Recovery/`, `Assets/_Recovery.meta` (2.9MB) | 10/16 Unity 크래시 복구 씬(`0.unity`, `0 (1).unity`). 진행 문서 5절의 보호 목록에 있다 |
| `tmp/pdfs/` (8MB) | 11/02 면접 학습 문서를 PDF로 렌더링할 때 나온 중간물(페이지 PNG, 렌더 로그, `build_interview_artifact.py`)로 보인다. 최종 문서는 `Docs/physics-collider-interview-study-guide-20261102.md`이다 |
| `Benchmarks/` 루트의 기존 파일(CurbStuckRuntime*, RunFloorBoundaryBenchmark.cs, *_TESTS.md, youtube-icon.png 등) | 진행 문서 5절의 보호 목록. 과거 에디터 측정 자료다 |

## 권장 순서

1. A를 지금 지운다 → 약 1.1GB 확보(여유 약 5.1GiB)
2. Windows 결과 3개를 분석한 뒤 B를 지운다 → 약 7.1GB 더 확보
3. 79개 프리팹 cooking 복원 검증이나 S3c 원본 적용 같은 새 빌드 작업을 하려면 2까지 끝낸 뒤 시작한다
