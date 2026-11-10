# 물리 검증 실행 증거와 복원 도구

현재 단계·승인·다음 행동은 [진행 현황](../../Docs/physics-collider-progress.md), 전체 순서는 [실행 지침](../../Docs/physics-collider-execution-plan.md)을 확인한다.

다른 세션/기기로 옮길 `physics-validation-checkpoint.zip`에는 문서·이 README·복원/감사 도구·JSON/완료 증거와 체크섬을 담는다. 원본 에셋·Library·Unity 원시 로그는 제외한다. 원본은 등록된 Git SHA에서 재구성하며 private 에셋 접근 권한이 필요하다. ZIP은 자동 업로드되거나 Git에 반영되지 않는다.

## 실제 완료한 범위

- M1: `Evidence/baseline-manifest.json` / `baseline-files.json` / `baseline-manifest.sha256`. 실행 승인과 원본 상태, tracked/untracked 16,891개 항목을 기록했다.
- M2: H1 루트 `9b83a248d281173446c0640ae1d6740506150c4d`와 에셋 `ffd2c2e0be38f63f7ff4ad6d88514f8ffb3d93aa`를 `Benchmarks/.physics-work/project`에 별도 복원했다.
- Unity 6000.3.15f1의 배치 임포트/컴파일과 Edit Mode 감사가 각각 종료 코드 0으로 완료됐다. 원본 5,889개 파일은 임포트/감사 후에도 Git blob과 일치한다.
- H1 주 Rigidbody는 Continuous/Interpolate, 자식 body는 Discrete, Standalone backend는 Mono2x, dt는 0.0199999921초였다. `Evidence/H1-editor-resolved.json` 참고.
- M3: 하네스 원본은 `Harness/`다(사용법·결과 스키마·판정 규칙은 [Harness/README.md](Harness/README.md)).
  - `Tools/sync_harness.py`로 H1 복원본 `Assets/PhysicsBench/`에 복사했다.
  - Mac 개발 빌드 `M3-H1-mac-08`(Mono2x, `PHYSICS_BENCH`)로 합성 지형 판정 검증 15건을 실행해 15/15가 기대와 일치했다(이전 빌드 seed 6·7·8에서도 14/14).
  - 재실행 거부, 강제 중단 시 완료분 보존, watchdog 중단과 다음 trial 복구, 입력 미전달 검출, 화면 캡처를 확인했다.
  - mac-08 빌드 후에도 원본 5,889개가 Git blob과 일치한다.
  - 근거는 `Evidence/M3-completion.json`과 `Evidence/M3/`다.
- M4(DONE, 2026-10-30):
  - H1·H7 Windows x64 빌드를 만들었다(Mono2x, 오류 0). H7은 `Benchmarks/.physics-work/project-H7`에 복원했다.
  - smoke 묶음: `Builds/M4-smoke-01.zip`(Windows 실행·회수 완료, 실행 경로 검증), 리뷰 반영판 `Builds/M4-smoke-02.zip`(22건, Windows 실행·회수 완료, `Evidence/M4/windows-smoke-02-summary.json`).
  - Mac 사전 진단은 `Evidence/M4/`에 있다.
- **Windows 결과:** smoke-01·smoke-02 실행·회수 완료(`Evidence/M4/windows-smoke-0*-summary.json`). M5 탐색 pilot-01 완료(`Evidence/M5/M5-windows-pilot-01-summary.json`), pilot-02c(`Builds/M5-pilot-02c.zip`, r3 리뷰 반영판) 2026-11-08 실행·회수·분석 완료(`Evidence/M5/M5-windows-pilot-02c-summary.json`): H0에서 Windows 바닥 관통 낙하를 처음 관측(case당 1회, 반복 확인 전). 반복 확인 묶음 `Builds/M5-repeat-01.zip`(H0 단독, 16 run, 약 40분) 실행 대기(`Evidence/M5/M5-repeat-r1-freeze.json`). 트랙 B(바닥 구성·물리 설정 통제 비교) 기하 감사 완료(`Evidence/M5/trackB/M5-trackB-geom-summary.json`), 런타임 묶음 `Builds/M5-trackB-01.zip`(30 run, 약 80분) 실행 대기(`Evidence/M5/trackB/M5-trackB-freeze.json`). pilot-02b는 보존만 하고 전달하지 않는다. M6 이후 정식 Windows 실행, H0 낙하의 반복·다른 PC 확인, H2~H6 계측은 아직 없다. Mac 결과는 하네스 검증·진단이며 Windows 결과로 쓰지 않는다.

## 파일 안내

| 파일 | 용도 |
|---|---|
| `Evidence/restore-index.json` | H1~H7 전체 SHA·트리·패키지 해시·객체 존재와 현재 복원 상태 |
| `Evidence/H1-import.log` | 계측 코드를 추가하기 전 원본의 최초 임포트/컴파일 |
| `Evidence/H1-source-after-import.json` | 원본 파일 변경 없음 확인 |
| `Evidence/H1-editor-audit.log` | 감사 스크립트 컴파일·실행 로그 |
| `Evidence/H1-editor-resolved.json` | 씬/프리팹 상속이 반영된 Edit Mode 설정. Awake 전 상태 |
| `Evidence/M1-completion.md`, `M2-completion.json` | 마일스톤 완료 증거 |
| `Tools/prepare_snapshot.py` | SHA가 고정된 원본 추출. 기존 폴더를 절대 덮어쓰지 않음 |
| `Tools/Editor/PhysicsRestoreAudit.cs` | 별도 복원본의 Assets/Editor에만 복사하는 읽기 전용 Unity 감사 |
| `Tools/verify_snapshot.py` | 복원본 원본 파일의 Git blob 재대조·추가 파일 목록(읽기 전용) |
| `Tools/make_smoke_bundle.py` | 빌드 출력 전체 + RunSmoke.cmd + 안내 + MANIFEST/SHA256SUMS로 Windows 전달 ZIP 생성. 기존 묶음 덮어쓰기 거부 |
| `Builds/` | Windows 전달 묶음과 `.sha256` |
| `Evidence/H7/`, `Evidence/M4/` | H7 복원·임포트 기록, M4 빌드 결과·기준 높이 조사·Mac 진단 run |
| `Tools/sync_harness.py` | 하네스 원본을 복원본 `Assets/PhysicsBench/`로 복사하고 SHA 기록. 원본 프로젝트 설치 거부 |
| `Harness/` | M3 런타임 하네스·빌드 도구·분석기·case/profile/임계값 |
| `Evidence/M3-completion.json`, `Evidence/M3/` | M3 빌드 결과·판정 검증 run·분석·중단 검증·MeshCollider 빌드 오류 목록 |

원본 프로젝트에 새 Assets/Editor 스크립트를 넣지 않았다. H1 복원본에는 `Assets/Editor/PhysicsRestoreAudit.cs` 한 파일을 계측용으로 추가했으며 `restore-index.json`에 SHA와 목적을 기록했다. 이 파일은 게임 이동/충돌 로직을 바꾸거나 씬을 저장하지 않는다.

## 다음 담당자의 시작점

1. 진행 현황과 새 사용자 변경을 확인한다. 구현은 승인되었으므로 같은 승인을 다시 요청하지 않는다.
2. 복원 프로젝트는 H1(`project`)과 H7(`project-H7`)이며 둘 다 하네스 사본이 들어 있다. Windows Mono 모듈은 이미 설치되어 있다. 다음은 M5(숨은 지지면·대상 연석 콜라이더 감사, 원래 낙하 재현 탐색)다.
3. 기존 탐색 CSV와 M3 합성 지형 결과는 정식 수치로 가져오지 않는다.
4. H2 이후로 넘어갈 때는 H1의 필요한 빌드·계측 patch·증거를 먼저 확보한다. 디스크 예산 때문에 여러 버전을 동시 임포트하지 않는다.

Git 객체만 읽는 복원 원본 확인(프로젝트 루트에서):

```sh
python3 Benchmarks/PhysicsValidation/Tools/prepare_snapshot.py H1 --inventory-only
```

새 빈 작업 공간을 준비할 때만 다음 명령을 사용한다. 현재 기본 작업 공간은 이미 존재하므로 도구가 **의도적으로 거부**한다. 덮어쓰기 옵션은 없다. 자체 작업 공간 정리는 소유·보관 증거를 확인한 뒤 별도로 수행한다.

```sh
python3 Benchmarks/PhysicsValidation/Tools/prepare_snapshot.py H1
```

도구는 원본 Git checkout을 바꾸지 않고 프로젝트의 Assets/Packages/ProjectSettings와 정확한 에셋 Git 객체를 추출한다. 추출 바이트를 Git blob과 대조하고 원본 파일 인덱스를 기록한다. H0는 에셋 참조가 미확정이므로 지원 목록에서 제외되어 있다.

에디터 감사의 재실행에는 새 출력 경로와 새 로그 경로를 사용한다. 감사 도구는 기존 결과 파일을 덮어쓰지 않는다. 원래 씬·프리팹·계측 코드 해시를 보존하며, 단순한 성공 로그만으로 Windows 버그 개선을 주장하지 않는다.
