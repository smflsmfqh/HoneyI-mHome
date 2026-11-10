# M9 감사·대응 분석 보고 (M6-r1)

작성: 2026-11-10. M8 판정 뒤에 하는 서술 감사이며, M8 판정은 바꾸지 않는다.

- 규칙: `M9-audit-rules.json`(SHA `aa901953…`). 계산 전에 기록했다.
- 스크립트: `Tools/audit_M9.py`(SHA `2884c71e…`)
- 출력: `M9-audit.json`(SHA `645c29b4…`)
- 모든 칸의 run ID 목록과 실패 caseId(attempt) 목록은 `M9-audit.json`의 `tables.*`에 있다.

## 1. 감사 결과 — 48 run, 모든 trial

| 항목 | 결과 |
|---|---|
| 원시 결과 보존 | 결과 ZIP A `1bdf17a0…`, B `3b5c84bb…`가 M8 기록과 같다. 압축을 푼 원시 파일은 수정하지 않았다 |
| a. 실행 완료 | 48/48. state가 completed이고 planned = finished = trials 행 수 = case 파일 case 수다. order.json ID 집합이 case 파일과 같고 중복이 없다 |
| b. 입력 전달 | 입력 누락과 harness_error는 의도된 CK08·CK13에만 있다(각 48건 = run당 1건). 의도 밖은 0건이고, 분모에서 뺀 trial도 0건이다 |
| c. 초기 상태 | 초기 침투가 0.005m를 넘은 trial은 0건이다. 시작 위치·yaw는 case 파일과 모두 같다. fixedDeltaTime·CCD도 그룹 기대값과 모두 같다: F 0.02/Discrete, p4 override, p5 0.01/CCD, H0 0.02/Discrete(당시 값) |
| d. 지형 적용 | E2_C 시작 지점 콜라이더가 변형마다 기대와 모두 같다(아래 목록). 연석 수정은 시작 콜라이더에 나타나지 않으므로 씬 SHA `12ba8273…`가 유일한 증거다. BASE·CURBFIX의 시작 콜라이더 집합은 6쌍 모두 같다 |
| e. 오류 로그 | player.log 전체 기준으로 연석 오브젝트 오류는 48 run 모두 0이다. 바닥 Read/Write 오류는 S0b에만 있다(32 오브젝트). 차량 오류는 run마다 25~31로 다른데, 스폰된 차량 수 차이 때문이다(차량 cooking은 범위 밖). p5g와 p5c 사이 오류 오브젝트 차이는 없다 |

d. 변형별 시작 지점 콜라이더:
- S0a·S0p·H0: non-convex Mesh
- S0b: 바닥 콜라이더 없음
- S1: `PhysicsBenchPlate`
- S2: convex Mesh
- S3: `FloorColliders` Box

trial 기록(`trials.jsonl`)의 `logErrors`는 50개에서 잘린다. 그래서 오류 감사는 player.log로 했다.

## 2. 잡음 수준 (같은 빌드·같은 seed)

| 비교 | 불일치 |
|---|---|
| a1↔a2(seed 1), 13개 그룹 | 11개 그룹은 0/58 또는 0/24 |
| P4 0.01/CCD | 1/58 (B_S3_Boost_m02 passThrough→recovered) |
| H0 | 1/24 (E2_C09_jump pass→fall) |
| P4 0.01/CCD(override) ↔ P5c BASE(기본값), 같은 attempt | 2/174 (recovered→passThrough 2건) |

결과는 거의 결정적이다. 아래 효과(수십 pp)는 이 잡음(1~2건)보다 훨씬 크다.

## 3. ④ 물리 설정 — BASE, 연석 34 case × 3 = 102

| 칸 | 통과 | 끼임(rescue_failed+escaped_by_rescue) | 낙하 | 종합 실패 | 종합 pp(대 0.02/D) |
|---|---|---|---|---|---|
| 0.02/Discrete | 72 | 0 | 0 | 72 (70.6%) | — |
| 0.01/Discrete | 38 | 0 | 0 | 38 | −33.3 |
| 0.02/CCD | 48 | 0 | 0 | 48 | −23.5 |
| 0.01/CCD | 19 | 18 | 0 | 37 (36.3%) | −34.3 |

E2_C 낙하는 네 칸 모두 0/72다.

대응표(0.02/D 기준, 102쌍):
- 0.01/D: 개선 34, 악화 0
- 0.02/CCD: 개선 24, 악화 0
- 0.01/CCD: 개선 35, 악화 0
  - 실패끼리 범주만 바뀐 것: **passThrough → rescue_failed 18건**
  - 해당 case: 연석 지점 S2의 Sprint·Boost × c00·p02·m02, 각 3/3

18건은 실패에서 실패로 옮긴 것이라 규칙상 "악화"에는 들어가지 않는다. 하지만 통과(일시적)가 완전 끼임(탈출 불가)으로 바뀐 것이므로, 사용자에게는 더 나쁜 부작용으로 보고한다.

## 4. ⑤ 연석 수정 — 연석 34 + E2_C 24 case × 3

| 조건 | BASE 통과 / 끼임 / 낙하 | CURBFIX | pp(종합) |
|---|---|---|---|
| 통제 p5c | 21 / 18 / 0 (종합 39/102) | 0 / 0 / 0 | −38.2 |
| 게임 p5g | 20 / 18 / 0 (종합 38/102) | 0 / 0 / 0 | −37.3 |

통제 대응표(174쌍):
- passThrough → recovered: 21
- rescue_failed → recovered: 18
- 나머지는 그대로 유지: blocked_escaped 30, recovered 33, E2_C pass 72
- **악화 0건, 범주 변화 0건**
- 회귀가 없다. 수정은 BASE에서 실패하던 39건만 바꿨다

CURBFIX recovered 144건(통제+게임):
- 모두 첫 후진(reverse) 단계에서 빠져나왔다
- 접촉 후 1.19~1.24초에 빠져나왔으며, BASE recovered와 같은 시점이다
- 옆 이동이나 구조 동작이 필요한 trial은 없다

## 5. 위치군 (연석 지점 × 접근 × 각도, 각 칸 3회)

- **끼임 18건**: 모두 연석 지점 S2에서 나왔다(Sprint·Boost × c00·p02·m02, 6 case × 3). 0.01/CCD 칸과 p5c·p5g BASE에서 같은 case다.
- **0.01/CCD에서 남은 통과 19건**:
  - S2 BoostSprint/a30, S2 Sprint/a30
  - S3 Boost/p02·m02(m02는 1~3회), S3 BoostSprint/a30, S3 Sprint/p02·m02
- **CURBFIX**: 34 case 모두 blocked_escaped 또는 recovered다.
- **BoostSprint 정면·근정면(c00·p02·m02)과 a60**: BASE에서도 막혔다(blocked_escaped). 0.02/D에서는 a60만 막혔다.

## 6. 바닥 ①②③ — E2_C 24 case × 3

| 변형 | 낙하 k/n | S3 대비 대응(72쌍) | 낙하 시점(trial 시작 후, 초) |
|---|---|---|---|
| S0a (H0식) | 42/72 | fall→pass 42, 악화 0 | 중앙 2.49 (1.16~6.28) |
| S0b (H1식) | 72/72 | fall→pass 72 | 중앙 0.34 (0.32~1.16) |
| S0p (S0b+R/W) | 42/72 | fall→pass 42 | 중앙 2.49 (1.16~6.28) |
| S1 (받침판) | 0/72 | 변화 0 | — |
| S2 (Convex) | 12/72 | fall→pass 12 | 중앙 2.23 (0.98~3.48) |
| S3 (현재 Box) | 0/72 | 기준 | — |
| H0 (당시 빌드, 6회) | 34/144 | 대응 없음 | 중앙 4.2 (3.22~6.2) |

- S0p와 S0a는 낙하 case 14개(jump 12 + C02·C06 walk)와 시점까지 같다. Read/Write를 켜면 H0식 기본 상태로 돌아갈 뿐, 그 상태에서 남은 42건은 그대로다.
- S0b는 시작 지점에 바닥 콜라이더가 없어 바로 떨어진다(중앙 0.34초).
- S2 낙하는 C01·C07 경로(walk·jump, 3/3)다.
- H0 낙하 case: C01_jump, C06_walk, C08_walk, C11_jump, C11_walk(6/6), C09_jump(4/6).
  - H0의 낙하 case 집합은 S0a와 겹치지만 같지는 않다. 공통은 C01_jump·C06_walk·C09_jump·C11_jump이고, C08_walk·C11_walk는 H0에만 있다.
  - H0는 당시 하네스 판으로 측정했으므로 대응표를 만들지 않는다.

## 7. ③ 기하 확인 (M8 문구 검증)

- S3에서 5mm를 넘는 지점은 2,319개다. 그중 **2,187개(94%)가 C07 경로의 `FloorColliders/road_009_floor`** 위에 있다.
- 단차 소실 6곳도 모두 C07 경로(3개 단면 × 2)에 있다.
- 연석 p95가 5mm를 넘는 경로도 C07(100mm)뿐이다.
- 결론: "S3 연석 p95 100mm = 남쪽 도로 0.1m 단차"라는 연결은 위치로도 확인됐으므로 문구를 유지한다.
- 나머지 132개 지점은 건물 테라스와 도로 경계의 국소 지점이다(C01·C03·C05·C09·C11·S2·S3).
- **기록상 ③ 판정**: freeze에는 "부분" 범주가 없다. 따라서 ③은 **지지 아님(런타임 조건만 충족)**이다. "부분"은 서술 표현으로만 쓴다.

## 8. 미재현·미완료 범위

- freeze 범위 밖:
  - H2~H6 버전
  - 다른 PC(이번 48 run은 모두 i5-12500 한 대)
  - 건물 관통, 화단(fir tree group 2) 버그
  - 차량 cooking(차량 프리팹 Read/Write 오류)
  - 30/120 FPS
- 원인 미확인:
  - 소품 P00·P04 예측 불일치(fix1)
  - 0.01/CCD에서만 끼임이 생기는 원인(수치로만 보고)
- 남은 문제: 남쪽 도로 0.1m 단차(③), 연석 충돌 직후 최대 2.6cm 침투(⑤ 보조)
- M10(반복·배포 조건 확인)은 실시하지 않았다.

M9-audit.json의 `tables.time`은 분석기 출력 step에 trial의 fixedDeltaTime을 곱한 값이다. 접촉 시각(`firstWallContactTime`)은 게임 전체 fixedTime 기준이라 trial 시작 시각을 빼서 바꿨다.
