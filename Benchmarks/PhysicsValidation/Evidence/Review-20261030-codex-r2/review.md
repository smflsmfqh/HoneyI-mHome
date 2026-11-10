# smoke-02 및 진행 현황 재검토

검토일: 2026-10-30 (Asia/Seoul). 대상: 최신 진행 문서, smoke-02 하네스, 기존 Mac 원시 기록, Windows 빌드 묶음. 게임/하네스/임계값/원시 기록은 수정하지 않았다. Unity·Windows 실행은 새로 하지 않았다. 새 출력은 이 검토 디렉터리에만 저장했다.

## 결론

수정 대부분이 실제 코드와 빌드에 반영됐다. smoke-01은 Windows 실행·회수 완료이고, smoke-02는 제작·Mac 검증 완료, Windows 실행 대기다. 따라서 M4 `WAITING_USER_RUN`, M5 이후 미착수라는 구분은 타당하다.

다만 R3의 이탈 시점 처리에는 아래 P2 판정 오류가 남아 있다. R6 문서 모순도 일부 남아 있으므로 “이전 지적 모두 해결”로 확정하지 않는다. 기존 Windows smoke-01을 실행 경로 검증에만 쓰고 개선율 근거에서 제외한 처리는 적절하다.

## 남은 발견 사항

### P2 — 실제 이탈 전 자식 낙하를 성공 판정에서 제외할 수 있음

위치: `Harness/Tools/analyze_physics_bench.py:289-295`.

`escapePhase`에 해당하는 **단계의 첫 행**을 이탈 시점으로 사용한다. 그러나 실제 이탈 조건은 그 단계 도중 2r 이동·침투 해소·0.1초 연속 지지 조건을 충족해야 성립한다. 단계 시작과 이탈 완료 사이에 자식 body가 지속 낙하해도 `childFallStep < esc_step`이 거짓이 되어 종합 실패에서 제외된다. 주석의 “이탈 뒤 구제 단계”보다 넓게 후진/좌우 단계 도중 사건까지 제외한다.

분석기 수준 반례: H1 smoke-02 CK05의 root/접촉/입력 기록을 유지하고, 복사한 trace의 자식 위치만 reverse 첫 행부터 0.1초 이상 낙하 상태로 바꿨다. 그 시점 root 이동은 밀기 종료점에서 약 **0.704r**여서 **2r 이탈 조건 미충족**이다. 그런데 `rootOutcome=blocked_escaped`, `outcome=blocked_escaped`, `flags=[child_fall]`가 나왔다. 이는 새로운 Unity 물리 재현이나 기존 run에 실제 낙하가 있었다는 주장이 아니다.

권장 수정: 이탈 조건의 연속 충족이 확정되는 실제 `escapeStep`을 기록하고 비교한다. 이탈 전 낙하와 이탈 후 구제 단계 낙하를 각각 검증하는 분석기 사례를 추가한다. 분석기만 변경하면 되는 문제이므로 런타임·case를 그대로 유지하는 경우 기존 smoke-02 raw 기록을 새 분석 revision으로 재분석할 수 있다.

근거: `visual-escape-counterexample.json`.

### P2 — 최신 상태와 충돌하는 인계 문장이 남음

위치: `Docs/physics-collider-progress.md:3,7,17,38,49-50`, 환경 표의 Windows PC 상세 행; `Benchmarks/PhysicsValidation/README.md:23`.

- 최종 갱신은 10/29·작업자 M3로 표시되지만 본문에는 10/30 smoke-02 완료가 있다.
- smoke-01 Windows 회수를 적은 뒤 “Windows 실행·회수 미수행”을 범위 구분 없이 반복한다.
- 승인 표에는 아직 “모듈 미설치”, “빌드·테스트 미수행”이 남아 있다.
- Windows PC 사양을 본문에 적었지만 환경 표에는 OS/GPU 등이 미확인이다.
- README에는 Windows 실행 결과가 아직 없다고 적혀 있다.

최상단의 “다음 한 작업”과 M4 상태표는 smoke-02 실행 대기로 맞게 갱신됐다. 나머지도 smoke-01 완료 / smoke-02 대기 / formal 미수행으로 구분해야 한다. 문서 ZIP과 checkpoint ZIP의 progress 파일은 현재 파일과 일치하므로, 파일 복사 누락이 아니라 문서 내용 자체에 남은 모순이다.

## 이전 지적 반영 확인

| 항목 | 재검토 결과 |
|---|---|
| R1 대상 벽 오인 | 법선 + 접점의 기준 벽면 거리 + 선택적 경로 필터가 추가됐다. H1 SM05는 이전 V_surface2를 첫 접촉으로 세지 않고 firstWallContactTime=-1로 남긴다. 실제 맵 case의 wallTargets는 아직 비어 있으므로 정식 M5/M6에서는 Collider 식별/물리 벽면 감사가 필요하다. 현재 smoke 보완은 확인했다. |
| R2 복합 입력 일부 수신 | requiredActions/receivedActions를 분리하고 모두 수신해야 완료로 처리한다. CK16에서 Move만 수신하고 Sprint가 빠진 사실이 남으며 하네스 오류로 분류된다. |
| R3 시각 body 실패 | rootOutcome/종합 outcome이 분리됐다. 바닥 hold trace에 지속 자식 낙하를 넣은 반례는 visual_fall로 바뀌었다. 다만 위 이탈 시점 예외는 남는다. |
| R4 접촉 법선 평균 | 접점별 기록으로 수정됐다. CK17 동일 MeshCollider에서 바닥·벽 접점이 함께 있는 스텝이 H1 57개, H7 114개 기록되어 새 검증이 실제로 해당 조건을 다룬다. |
| R5 Ground 레이어 | 반환 전에 적용하도록 수정됐다. 실제 fixture 통제 기록에도 layer=Ground가 남는다. |
| R6 인계 문서 | 핵심 상태와 다음 작업은 갱신됐으나 위 모순이 남는다. |

## 독립 재검증 결과

| 검증 | 결과 |
|---|---|
| H1 복원 원본 | 5,889개 변경 0·누락 0 |
| H7 복원 원본 | 16,766개 변경 0·누락 0 |
| H1 Mac smoke-02 재분석 | 22/22 완료, 기대 판정 17/17 일치, 누락·중복 없음 |
| H7 Mac smoke-02 재분석 | 22/22 완료, 기대 판정 16/17 일치, 누락·중복 없음. CK12 recovered_after_penetration 대 blocked_escaped 불일치 유지 |
| 의도한 분석기 하네스 오류 | 각 run 4개: 전체 입력 누락·부분 입력 누락·timeScale 0·stall. 17/17은 물리 성공 17건이라는 의미가 아니다. 런타임 status의 오류 2건과 분석기 오류 4건은 집계 단계가 다르다. |
| Windows smoke-02 ZIP | 279,241,318 bytes, 외부 SHA 일치, 내부 654개 SHA 모두 일치, CRC 정상 |
| Windows 빌드 추적 | H1/H7 모두 x64(0x8664), case 22개, case/profile SHA 일치, build-info의 하네스 6개 SHA가 현재 소스와 모두 일치 |

증거: `H1-source-review.json`, `H7-source-review.json`, `reanalysis-H1/`, `reanalysis-H7/`, `bundle-integrity.json`, `contact-validation.json`, `visual-floor-fixed.json`.

## 다음 단계 해석

smoke-02 Windows 실행은 현재 묶음으로 진행할 수 있다. 자식 낙하 판정 예외는 분석기 수정·재분석 후 완료 판단에 반영해야 한다. Windows 결과가 돌아와도 이를 곧바로 개선율로 쓰지는 않는다. M5에서 숨은 지지 Collider·실제 연석 면·원래 낙하/끼임 재현을 확인한 뒤 M6에서 정식 조건을 동결한다. H7 CK12의 기대 불일치는 이미 공개돼 있으며, 판정기 오류와 버전별 물리 차이를 구분해 추적해야 한다.
