Physics bench M6-r1 정식 측정 묶음 A (④ 물리 설정, ⑤ 연석 수정)

1. ZIP을 아무 폴더에 풀기
2. RunM6A.cmd 더블클릭
   - 24 run, 약 2시간 50분. 창을 최소화하지 말 것(창 포커스는 바꿔도 됨)
   - 마우스·키보드를 테스트 창 위에서 건드리지 말 것. 절전·화면 꺼짐 끄기 권장
3. 끝나면 같은 폴더에 PhysicsBench-M6A-Results-<시각>.zip 생성
   → Mac의 Benchmarks/PhysicsValidation/Evidence/ 에 넣기

구성
  BASE\     원본 게임 씬(연석 수정 전)
  CURBFIX\  연석 콜라이더 밑면 -0.3 연장(커밋 6c26d2a와 같은 씬)
  cases\    m6-curb.json(62건)
  profiles\ profile-H7.json(NPC 등 끔), profile-H7-game.json(NPC·고양이·차량 켬, 무적)
  MANIFEST.json, SHA256SUMS.txt

실행 순서: 연석 수정 통제 6 → 물리 설정 2x2 12 → 게임 조건 6
지난번 실행과 같은 PC(i5-12500)에서 실행해 주세요.
