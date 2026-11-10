# 물리 검증 진행 사항 독립 리뷰

검토일: 2026-10-30 (Asia/Seoul). 대상: 다른 에이전트가 수행한 M3/M4 코드·빌드·기존 원시 기록·인계 문서. 원본/복원 프로젝트의 게임 코드, 하네스, case, 임계값, 기존 원시 결과는 수정하지 않았다. Unity·게임 실행 파일을 새로 실행하지 않았고, Git 대조·ZIP 검증·기존 결과 재분석·분석기 수준의 반례 확인만 수행했다.

## 결론

**실제 진행은 M3 구현 및 Mac 사전 검증, M4 Windows smoke 묶음 제작까지다. Windows 실행 결과는 아직 없다.** 원본 복원과 빌드 추적은 잘 유지되어 있다. 현재 ZIP은 실행 경로·관측을 확인하는 smoke 용도로 구분해야 하며, 아래 판정 문제를 보완하기 전 정식 개선 수치로 사용하면 안 된다.

M3 완료 표시는 구현·기존 smoke 완료로 이해할 수 있지만 판정기 검증이 충분하다는 최종 승인으로 볼 수는 없다. 수정 시 기존 M3/M4 증거와 빌드를 보존하고 새 revision/build ID로 재검증해야 한다. 이 리뷰는 진행 상태판을 임의 변경하거나 다른 작업을 중단시키지 않는다.

## 1. 독립적으로 확인한 완료 증거

| 확인 항목 | 검토 결과 | 이번 검토의 증거 |
|---|---|---|
| 사용자 원본 저장소 | 루트 HEAD `b8efce3`, 에셋 HEAD `a160f465`, tracked 변경 없음 | 검토 시 Git status/diff |
| H1 복원 원본 | 5,889개 파일, 변경 0·누락 0. 추가 계측 파일 25개는 별도 목록 | `H1-source-review.json` |
| H7 복원 원본 | 16,766개 파일, 변경 0·누락 0. 추가 파일 26개 별도 목록 | `H7-source-review.json` |
| M3 H1 판정 검증 | 기존 `M3-H1-mac-ck-09`를 현재 분석기로 재분석해 15/15 일치, 누락/중복 없음 | `reanalysis-M3-H1-ck09/summary.json` |
| 의도한 하네스 오류 | 15개 중 3개는 no_input / time_scale_zero / fixed_step_stalled 검증이다. 물리 테스트 성공 15건이라는 뜻은 아님 | 같은 재분석 결과 |
| Windows 빌드 | H1/H7 모두 PE machine `0x8664`인 x64 실행 파일, 빌드 보고서 오류 0 | `bundle-integrity.json`, 기존 M4 build-result |
| 전달 ZIP | `Builds/M4-smoke-01.zip`, 279,235,113 bytes. 외부 SHA·내부 654개 파일 SHA·ZIP CRC 모두 정상 | `bundle-integrity.json` |
| 빌드의 case/profile | H1/H7 모두 20개 case. 내장 case/profile SHA와 build-info가 일치 | `bundle-integrity.json` |
| 인계 ZIP | 문서 ZIP·checkpoint ZIP의 progress 문서는 현재 디스크 파일과 일치한다. 내용 자체에는 오래된 문장이 남아 있음 | `handoff-freshness.json` |

H7 Mac smoke의 기존 집계는 20건 완료, 의도한 오류 3건, 판정 기대값 14/15다. CK12는 `blocked_escaped` 기대에 대해 `recovered_after_penetration`으로 나왔다. H1 기준의 물리 의존 기대값과 H7 차이를 구분해야 하며, H7도 15/15 통과했다고 보고하면 안 된다.

## 2. 수정이 필요한 발견 사항

### R1 — P1: 연석 대상 식별 없이 법선만으로 첫 접촉을 결정함

위치: `Harness/Runtime/PhysicsBenchRunner.cs:454-459`, `:512-519`, `:313-325`.

`_wallContact`는 접촉한 Collider가 시험 대상 연석인지 확인하지 않고 법선 방향만 검사한다. 이후 이 값을 기준으로 첫 접촉 시각·직전 속도·밀기 시작을 정한다.

이는 실제 원시 기록에서도 확인됐다. `M4-H1-mac-map-01`의 `SM05_S3_curb_sprint` 첫 벽 접촉 대상은 `road_019` 연석이 아니라 **`Surface/NotWalkableSurface/V_surface2`**다. 이 값으로 기록된 preContactSpeed는 약 2.265m/s다. 따라서 그 시점·밀기 시간을 대상 연석에 대한 측정으로 사용할 수 없다.

조치: case/지형 manifest에 대상 Collider/구역의 안정된 식별자를 연결하고, 해당 대상의 개별 접점을 기준으로 접촉을 시작한다. 인접 장애물 접촉은 별도 기록한다. 역사 원본 지형은 보존하며, 현재 smoke의 결과는 탐색 관측으로만 남긴다.

근거: `wall-contact-and-support.json`.

### R2 — P1: 조합 입력의 일부만 수신해도 전체 입력이 확인된 것으로 처리됨

위치: `Harness/Runtime/PhysicsBenchInput.cs:111-132`, `Harness/Tools/analyze_physics_bench.py:59-68`.

`Move+Sprint` 요청에 대해 Move 또는 Sprint 중 하나의 콜백만 와도 단일 `received*` 값이 채워진다. 분석기는 이 단일 플래그만 보므로 Sprint가 전달되지 않았어도 유효한 Sprint 시도로 남을 수 있다. Move+Roll·Move+Jump 계열도 같은 구조다. 전체 입력 미전달 CK08만으로 이 부분 실패를 검증할 수 없다.

조치: 액션별 요청/수신과 값·방향을 각각 기록하고 필요한 모든 액션을 확인한다. 의도한 이동 모드의 실제 상태/접촉 직전 속도도 따로 감사하되 충돌 후 저속을 무효로 버리지는 않는다. “Move는 전달되지만 Sprint/Jump/Roll은 전달되지 않음” 사례를 추가한다.

근거: `classifier-counterexamples.json`의 조합 입력 항목. 이 항목은 코드 흐름/분석 데이터의 정보 손실 확인이며 Unity에서 실제 Sprint를 차단해 재실행한 결과는 아니다.

### R3 — P2: 시각적으로 개미가 지속 낙하해도 주 결과가 pass일 수 있음

위치: `Harness/Tools/analyze_physics_bench.py:102-123`, `:138-144`.

자식 body의 지속 낙하는 `child_fall` 플래그로만 남고 hold/walk/jump의 주 성공 판정에는 반영되지 않는다. 따라서 주 캡슐이 지지된 상태에서 화면의 개미가 따라가는 자식 body가 떨어지면 결과가 **`outcome=pass`, `flags=[child_fall]`**가 될 수 있다.

기존 CK01의 주 캡슐·지지 기록은 유지하고 자식 body 위치만 메모리에서 낙하 상태로 바꾼 분석기 반례로 이 결과를 확인했다. 이는 새로운 Unity 물리 재현이나 기존 run이 잘못 기록됐다는 주장은 아니다.

조치: A의 평가 대상을 M5/M6에서 명확히 정한다. 주 캡슐과 시각 body의 낙하율을 각각 보고하거나, 어느 쪽의 지속 낙하도 종합 성공에서 제외해야 한다. 순간 침투와 지속 낙하의 구분은 유지한다. 플래그를 빼고 outcome만 집계한 “낙하 0건”을 포트폴리오에 쓰면 안 된다.

근거: `classifier-counterexamples.json`.

### R4 — P2: 접촉 법선 평균이 벽 접촉을 지워 버릴 수 있음

위치: `Harness/Runtime/PhysicsBenchRunner.cs:1104-1115`, `:512-519`.

동일 Collider의 모든 접점 법선을 더해 정규화한 한 벡터만 남긴다. 예를 들어 수직 벽 법선 `(1,0,0)`과 바닥 법선 `(0,1,0)`은 평균 뒤 y≈0.707이 되고, 벽 판정의 `abs(y) >= 0.7` 조건에서 제외된다. 실제 벽 접점이 있는데도 첫 접촉을 누락하거나 늦출 수 있다.

바닥·연석이 한 Mesh에 묶였을 때와 여러 Box에 나뉘었을 때 접촉 집계가 달라질 수 있어 역사 비교에도 영향을 준다.

조치: 개별 접점의 법선과 대상 Collider/body ID를 보존하고 벽·바닥 접점을 각각 분류한다. 같은 Collider에 바닥과 벽 접점이 함께 발생하는 검증 사례를 추가한다.

근거: 코드와 `classifier-counterexamples.json`의 수학적 반례. 이 반례는 실제 Unity 다중 접점 실행 결과가 아니다.

### R5 — P2: 합성 fixture의 Ground 레이어 설정이 실행되지 않음

위치: `Harness/Runtime/PhysicsBenchFixtures.cs:97-100`.

`return go` 뒤에 레이어 설정 코드가 있어 도달할 수 없다. 생성한 Cube는 Default 레이어로 남는다. Ground 태그는 붙지만 Ground 레이어는 적용되지 않는다.

조치: 반환 전에 레이어를 설정하고 실제 생성된 layer를 검증한다. 레이어 변경이 충돌 행렬에 영향을 줄 수 있으므로 기존 15/15 결과를 수정된 fixture의 검증 결과로 재사용하지 않는다.

### R6 — P2: 진행 문서가 실제 완료 상태와 충돌함

위치: `Docs/physics-collider-progress.md:19-46`, `:57-58`, 산출물 표의 Windows build 없음 항목. `Benchmarks/PhysicsValidation/README.md`의 “Windows 빌드·실제 맵 case·H2~H7 계측은 아직 없다” 문장 및 다음 담당자 지침.

최상단 M4 실적과 상태표는 Windows H1/H7 빌드 및 H7 Mac 실행을 적고 있는데, 아래에는 모듈 추가·H7 profile 첫 실행·빌드 수행을 다음 행동으로 다시 지시한다. checkpoint ZIP에도 같은 텍스트가 들어 있으므로 새 담당자가 완료된 작업을 반복할 수 있다.

조치: “현재 사실”과 “이전 계획/과거 기록”을 구분하고 다음 작업을 Windows smoke 실행·결과 회수로 통일한다. 원래 버그 재현, 정식 실험 동결과 H2~H6 검증은 미완료로 유지한다. 기존 완료 기록 자체는 지우지 않는다.

## 3. M5에서 추가 확인해야 할 단서

1. H1 Mac 원시 trace에서 SM01/SM02/SM03의 지지는 `PostProcessingVolume/Ground` Box로 확인된다. road Mesh 자체가 안정적으로 받쳤다는 증거가 아니다. Read/Write/cooking 오류와 별도 바닥의 관계를 조사해야 한다.
2. **H7에도 별도 지지가 있다.** SM01/SM04의 위쪽 접촉에는 `Zone/CarDrivingZone/Zone3`(Default, Untagged, non-trigger Box)와 `road_009_road`가 동시에 기록된다. 숨은 바닥 감사는 H1 Ground뿐 아니라 H7의 이런 구역 Collider까지 포함해야 한다. 근거: `wall-contact-and-support.json`.
3. H7 S3의 벽면 교차는 현재 “기준 시각 벽면을 넘음” 관측이다. Box 배치 차이인지 실제 물리 면 관통인지 구분하기 전 터널링/해결 실패율로 확정하지 않는다.
4. Read/Write 미설정 관련 Mac 빌드 로그는 유효한 원인 후보다. Windows 실행·충돌면 유효성 검증 전에 원인을 터널링 또는 Read/Write 하나로 확정하지 않는다.
5. 현재 5개 map case는 하네스·재현 탐색용이다. 낙하 부재와 연석 통과 두 결과만으로 Mesh→Box 개선율이나 CCD/dt 개선율을 계산할 수 없다.

## 4. 이전 모듈 안내 정정

이전 Codex 검토는 `.app/Contents/PlaybackEngines`만 확인하여 Windows 모듈 미설치로 안내했다. 실제 모듈은 에디터 버전 폴더 바로 아래 **`/Applications/Unity/Hub/Editor/6000.3.15f1/PlaybackEngines/WindowsStandaloneSupport`**에 있었다. 현재 해당 폴더와 실제 Windows x64 빌드를 확인했다. 이전의 “추가 설치 필요” 판단은 경로 확인 오류였으며, 현재는 추가 설치가 필요 없다. 설치 시점은 폴더 메타데이터상의 2026-05-11과 일치하지만 메타데이터만으로 설치 과정을 재구성한 것은 아니다.

## 5. 권장 순서

1. R1/R2/R4/R5를 하네스 새 revision에서 수정하고 해당 경계 사례로 재검증한다.
2. R3의 주지표/종합 성공 조건과 fixture 기대값의 버전 의존성을 명시한다.
3. R6의 현황·인계 문서를 정리하고 필요한 경우 새 Windows smoke build/bundle ID를 만든다. 이전 ZIP은 보존한다.
4. Windows 원시 결과를 회수해 M4를 완료한 뒤 M5에서 실제 버그 재현·숨은 지지면·원본 코드 경로를 확인한다.
5. 그 뒤 정식 조건 M6를 동결하고 역사 비교와 A/B 통제 비교를 수행한다.

현재 리뷰가 추가한 파일은 이 디렉터리의 검토 증거뿐이다. M3/M4 코드·raw run·기존 보고서는 변경하지 않았다.
