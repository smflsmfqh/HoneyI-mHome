# M5 진행 사항 독립 검토

검토일: 2026-10-30 (Asia/Seoul). 최신 진행 문서, 역사 이동 코드, 현재 하네스·분석기·case 생성기, Windows pilot-01 원시 기록, pilot-02b 전달 ZIP을 검토했다. Unity나 Windows 실행 파일을 새로 실행하지 않았다. 기존 코드·case·임계값·원시 결과·진행 문서는 수정하지 않았으며 이 디렉터리에만 검토 증거를 추가했다.

## 결론과 현재 단계

M4 DONE, M5 IN_PROGRESS / pilot-02b Windows 실행 대기라는 상태는 증거와 부합한다. M6 이후 정식 실험은 아직 없다. 원래 버그와 같은 과거 환경을 재현했다거나 개선율을 확정할 단계는 아니다.

가장 중요한 진전은 **Windows에서도 H1 받침판 제거 통제에서 낙하 35/68건**이 재현됐다는 점이다. 이는 H1의 Mesh 바닥이 받침판에 의해 가려져 있었음을 지지한다. 원래 게임 그대로인 H1의 버그율 또는 Box 전환 개선율 51.5%라는 의미는 아니다. H0의 원래 낙하는 탐색 범위에서 아직 미재현이다.

새 Hm1-unfreezeY 낙하 결과는 **재구성 통제의 Mac 탐색**이다. 역사 원본·Windows 증거와 분리한 문서 처리는 적절하다. 다만 아래 지원 동작·성공 판정·통제 정의를 명확히 해야 한다.

## 발견 사항

### P2 — 구현되지 않은 동작이 Jump/Sprint 시험으로 기록됨

위치: `Tools/make_explore_e2.py:255-261,273-279`, `Harness/Runtime/PhysicsBenchRunner.cs:290-318,376-381`, `Harness/Runtime/PhysicsBenchPlayerApi.cs`의 Hm1/H0 분기.

Hm1 역사 PlayerMovement에는 OnMove와 MovePosition 이동만 있고 OnJump/OnSprint가 없다. H0에는 OnJump가 있지만 OnSprint는 없다. 그런데 pilot-02b의 Hm1 case 146개에는 scenario=jump가 26개, Sprint가 4개 들어 있다. 구제 점프도 Hm1에서는 구현되지 않은 동작이다. H0에도 Sprint 표기의 단차 대조군 4개가 있다.

가상 장치의 Jump 액션 콜백은 실제 플레이어 점프 구현과 별개다. 실제 Hm1-unfreezeY Mac `E2_C01_jump`는 completed이고 Jump 요청/수신도 기록돼 있지만 역사 코드에 점프를 적용하는 함수 자체가 없다. 따라서 문서의 “walk·jump 모두”를 두 가지 실제 이동 동작에서 재현된 것으로 해석할 수 없다. 현재 StartMode는 Boost 미지원만 거르고 이 경우를 검출하지 않는다.

권장: 버전별 capability 표를 만들고 미지원 동작은 unsupported로 표시하거나 보행/해당 키 입력 시도로 명확히 분리한다. 정식 비교에서는 Jump 수신뿐 아니라 실제 도약 여부도 확인한다. 기존 결과는 삭제하지 않는다. 동결된 pilot-02b를 바꿀 경우 새 case/bundle revision을 쓴다. 기존 묶음을 그대로 실행하면 이 행들은 점프/달리기 효과의 수치에서 제외해 해석한다.

근거: `Hm1-historical-PlayerMovement.cs.txt`, `H0-historical-PlayerMovement.cs.txt`, `capability-example.json`, `bundle-integrity.json`.

### P2 — 공중 고정도 지지 성공 pass와 같은 결과로 집계됨

위치: `Harness/Tools/analyze_physics_bench.py:155-163`.

끝부분에서 수직 속도·높이 변화가 작으면 접촉이나 probe 지지가 없어도 pass로 만든다. CharacterController 관측 누락을 보완하려는 의도지만 FreezePositionY로 공중에 고정된 상태도 같은 성공으로 처리한다.

이는 실제 Hm1 base Mac 기록에 있다. 예를 들어 `E2_S05_drop`은 minAllowedY=-0.1인데 캡슐 최저점이 약 0.9005m에 고정돼 있으며 outcome=pass, flags=[support_inferred_from_rest]다. 문서가 공중 고정을 이미 공개한 점은 적절하나, outcome별 합계만 보면 정상 착지와 구분되지 않는다. CK03 구멍 sanity가 pass가 되는 것도 동일한 문제다.

권장: 낙하 부재와 정상 지지·착지를 서로 다른 지표로 둔다. 접촉으로 확인하지 못한 정지는 별도 상태로 남기고, FreezePositionY/중력/kinematic 상태와 허용 지면 간격을 확인해야 정식 지지 성공에 포함한다. 이번 검토는 r4를 변경하지 않았다.

근거: `unsupported-rest-examples.json`.

### 실험 정의 주의 — 116→80은 Y 위치 고정만 해제하는 변경이 아님

profile-Hm1-unfreezeY는 constraints 116을 80으로 덮어쓴다. 제거되는 비트는 36=4+32여서 FreezePositionY와 FreezeRotationY가 함께 해제된다. 순수하게 Y 위치 고정만 해제하려면 112다.

현재 기록의 “H0 constraints만 적용”이라는 설명은 값과 일치하지만, 이 조건을 단일 요인 Y 위치 해제 효과로 해석하면 안 된다. M5에서는 “Hm1 + H0 constraints(80)” 재구성으로 설명하고, M6에서 Y 위치만 해제한 조건이 필요한지 구분한다. 낙하 원인이 회전 해제라는 주장은 아니다.

근거: `constraints-difference.json`, 실제 통제 기록 override:constraints=116->80.

### 인계 문서에 남은 이전 상태

진행 문서의 최상단과 M5 상태표는 최신이다. 그러나 “정식(pilot/formal) Windows 실행은 미수행” 및 미수행 목록에는 pilot-01 완료 전 표현이 남아 있고, PhysicsValidation README에도 pilot/formal 결과가 없다고 적혀 있다. “M5 탐색 pilot-01 완료 / pilot-02b 대기 / M6 이후 정식 실행 미수행”으로 용어를 통일해야 한다. Hm1 감사 JSON의 placement_only 어댑터 설명도 이전 유효하지 않은 빌드 설명으로 구분해야 한다(현재는 준비 중 카메라 yaw 필드 설정).

## 독립적으로 확인한 결과

1. 이전 리뷰의 이탈 시점 오류는 escapeStep 기준으로 수정됐다. 기존 Windows H1 smoke-02 자료에 대한 test_analyzer.py는 **6/6 통과**했다. 분석기 규칙 검증이며 새로운 물리 재현은 아니다.
2. Windows pilot-01 네 run을 현재 분석기와 thresholds-r4로 새 폴더에 재분석했다. 각 **72/72, 기대 판정 4/4**, 누락·중복 없음. 문서 집계와 일치했다.
   - H0: pass 69 / fall 1 / 의도한 하네스 오류 2.
   - H1: pass 67 / fall 3 / 의도한 오류 2.
   - H1-nocatch: pass 34 / fall 36 / 의도한 오류 2.
   - H7: pass 67 / fall 3 / 의도한 오류 2.
   - fall에는 CK03 의도 낙하 1개가 포함된다. H1/H7의 e1 시작 높이 문제 2개도 실제 맵 버그율에 넣지 않는다. H1-nocatch의 지도 35건을 분리한 문서 집계는 타당하다.
3. H0/H1/H1-nocatch는 대응 Mac run과 case별 **각 72개 모두 동일**했다. 서로 다른 OS 결과의 구분을 유지한다.
4. pilot-02b ZIP **445,604,785 bytes**, 외부 SHA 일치, 내부 **1,243개 SHA 모두 일치**, CRC 정상이다. Hm1/H0/H1/H7 모두 x64다. 외부 e2b case는 현재 로컬 파일과 일치한다(Hm1/H0/H1 각 146개, H7 154개). Hm1 base/unfreezeY가 같은 exe를 profile만 바꿔 쓰므로 총 5개 run이다.
5. 빌드별 하네스 revision은 같지 않다. Hm1 M5-Hm1-win-03은 현재 소스와 일치한다. H0/H1/H7는 M5-*-win-01을 재사용해 현재 하네스의 일부 SHA와 다르다. MANIFEST/build-info에 각 해시가 남아 있으므로 이를 즉시 잘못된 빌드로 보지는 않지만, “모두 현재 하네스로 재빌드”했다고 보고하면 안 된다. 정식 실험에서는 버전별 어댑터 차이와 공통 계측 revision을 정리한다.
6. 기존 최신 원본 대조 기록상 H1 5,889개·H7 16,766개는 변경/누락 0이다. H0 2,429개·Hm1 2,378개는 누락 0, 렌더링/프로젝트 설정 파일 5개씩 변경됐다고 기록돼 있다. 따라서 초기 두 환경을 원본 전 파일 불변이라고 표현하면 안 된다. 이번에는 원본 전 파일 해시를 다시 계산하지 않고 기존 대조 기록을 확인했다. 사용자 원본 루트와 에셋 저장소의 tracked diff는 없다.

## 다음 단계

pilot-02b는 Windows 탐색용으로 실행 가능한 묶음이지만, 미지원 동작과 공중 고정을 분리해 해석해야 한다. 이미 실행했다면 원시 기록을 그대로 보존하고 해당 한계를 붙여 분석한다. 100분 실행 전에 조건을 수정한다면 동결된 e2b/기존 ZIP을 덮어쓰지 말고 새 revision을 만든다.

M5에서 남은 것은 Windows pilot-02b 검증, 원래 증상과 재구성 조건의 연결 확인, H2~H6 복원·계측이다. 이후 M6에서 지점·속도·실제 지원 동작·낙하/지지/끼임 지표를 동결하고 역사 비교와 통제 비교를 수행한다. 현재 자료로 Mesh→Box 또는 CCD/dt의 정식 개선율을 제시하지 않는다.

검토 증거: `pilot01-reanalysis.json`, `reanalysis-W-*/`, `mac-windows-comparison.json`, `bundle-integrity.json`, `analyzer-regression.txt`, `reviewed-source-hashes.json` 및 위 반례/역사 코드 파일.
