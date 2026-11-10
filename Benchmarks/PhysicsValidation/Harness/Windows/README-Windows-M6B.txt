Physics bench M6-r1 정식 측정 묶음 B (①②③ 바닥 구성, H0 당시 버전)

1. ZIP을 아무 폴더에 풀기
2. RunM6B.cmd 더블클릭
   - 24 run, 약 1시간 10분. 창을 최소화하지 말 것(창 포커스는 바꿔도 됨)
   - 마우스·키보드를 테스트 창 위에서 건드리지 말 것. 절전·화면 꺼짐 끄기 권장
3. 끝나면 같은 폴더에 PhysicsBench-M6B-Results-<시각>.zip 생성
   → Mac의 Benchmarks/PhysicsValidation/Evidence/ 에 넣기

구성
  S0a S0b S0p S1 S2 S3\  바닥 구성 변형 빌드(Mesh 2종·Mesh+Read/Write·받침판·Convex·Box)
  H0\       당시 버전(2026-05-18) 빌드, repeat-01과 같은 파일
  cases\    m6-floor-<변형>.json(각 28건), m6-H0.json(28건)
  profiles\ profile-H7.json
  MANIFEST.json, SHA256SUMS.txt

실행 순서: 바닥 변형 18(S3 → S0a → S0b → S0p → S1 → S2) → H0 6
지난번 실행과 같은 PC(i5-12500)에서 실행해 주세요. 묶음 A와 순서는 상관없습니다.
