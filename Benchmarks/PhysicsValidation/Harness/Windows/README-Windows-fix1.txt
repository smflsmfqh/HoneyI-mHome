Physics bench fix1 (b: 연석 수정 회귀 / c: cooking 30 복원 검증)

1. ZIP을 아무 폴더에 풀기 (경로에 한글이 없어도 되고 있어도 됨)
2. RunFix1.cmd 더블클릭
   - 21 run, 약 2시간 30분. 창을 최소화하지 말 것(창 포커스는 바꿔도 됨)
   - 마우스·키보드를 테스트 창 위에서 건드리지 말 것
3. 끝나면 같은 폴더에 PhysicsBench-Fix1-Results-<시각>.zip 생성
   → Mac의 Benchmarks/PhysicsValidation/Evidence/ 에 넣기

구성
  BASE\     원본 게임 씬 그대로
  CURBFIX\  연석 콜라이더 밑면만 -0.3으로 연장(윗면 그대로)
  COOK30\   2026-05-22에 바뀐 프리팹 79개의 MeshCollider cooking을 기본값(30)으로 복원
  cases\    fix1-b.json(62건), fix1-c.json(12건)
  profiles\ profile-H7.json(NPC 등 끔), profile-H7-game.json(NPC·고양이·차량 켬, 무적)

실행 순서: b 통제 6 → c 통제 6 → 게임 조건 9
