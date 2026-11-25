# Trees

현재 나무는 코드로 만드는 절차적 소나무(Assets/Scripts/ProceduralTree.cs)라 외부 에셋이 없다.
(이전에 쓰던 Kenney Nature Kit `tree_pineTallA-D.fbx`(CC0)는 저폴리곤 느낌이 강해 제거했다.)

모델을 넣으면 절차적 소나무 대신 그 모델을 자동으로 쓴다:
- 추천: Poly Haven "Fir Tree 01" (CC0, 로그인 불필요) https://polyhaven.com/a/fir_tree_01
- 받는 법: 페이지에서 FBX (권장, Unity가 바로 읽음) 또는 glTF로 다운로드.
- 넣는 곳: 이 폴더(Assets/Resources/Trees/)에 .fbx 파일을 통째로 넣는다.
- 크기/원점 보정은 자동: 실행 중 높이를 9.5m(나무 크기 배율 반영)로 맞추고 가장 낮은 점을 지면에 맞춘다.
- glTF는 Unity 기본 임포터가 없어 별도 패키지(glTFast)가 필요하니 FBX를 권장.
- 중요: 찾는 A4 용지는 Fir Tree 01에만 붙는다. 이름에 "fir_tree_01"이 들어간 모델이
  하나라도 있으면 그 나무가 최소 3그루 심어지고 용지는 그중 하나에 붙는다.
  Fir Tree 01이 없으면 용지는 절차적 소나무 아무 데나 붙는다(게임 진행은 항상 가능).
- 참고: fir_tree_01 FBX는 약 250MB로 매우 크다. 저장소가 무거워지는 게 싫으면
  절차적 소나무를 그대로 써도 된다.
