Physics bench M10-r1 측정 묶음 (⑤ 연석 수정, 설계에 쓰지 않은 연석 8지점)

1. ZIP을 아무 폴더에 풀기
2. RunM10.cmd 더블클릭
   - 6 run, 약 1시간. 창을 최소화하지 말 것(창 포커스는 바꿔도 됨)
   - 마우스·키보드를 테스트 창 위에서 건드리지 말 것. 절전·화면 꺼짐 끄기 권장
3. 끝나면 같은 폴더에 PhysicsBench-M10-Results-<시각>.zip 생성
   → Mac의 Benchmarks/PhysicsValidation/Evidence/ 에 넣기

구성
  BASE\     원본 게임 씬(연석 수정 전) - M6-r1 묶음 A와 같은 빌드
  CURBFIX\  연석 콜라이더 밑면 -0.3 연장(커밋 6c26d2a와 같은 씬) - M6-r1 묶음 A와 같은 빌드
  cases\    m10-curb.json(84건 = 점검 4 + 8지점 x 10조건)
  profiles\ profile-H7.json(NPC 등 끔)
  MANIFEST.json, SHA256SUMS.txt

실행 순서: BASE, CURBFIX 번갈아 3회씩
지난번 실행과 같은 PC(i5-12500)에서 실행해 주세요.
