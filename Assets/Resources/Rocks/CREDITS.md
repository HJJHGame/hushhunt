# Rocks

현재 바위는 코드로 만드는 절차적 메시(Assets/Scripts/RockModels.cs)라 외부 에셋이 없다.

모델을 넣으면 절차적 바위 대신 그 모델을 자동으로 쓴다:
- 추천: Poly Haven Pine Forest 컬렉션의 Rock Moss Set 01, Rock Moss Set 02
  (둘 다 CC0, 로그인 불필요) https://polyhaven.com/collections/pine_forest
- 받는 법: FBX (권장, Unity가 바로 읽음)로 다운로드.
- 넣는 곳: 이 폴더(Assets/Resources/Rocks/)에 .fbx 파일을 통째로 넣는다. 여러 개 넣으면 섞여서 나온다.
- 크기 보정: 실행 중 기존 바위 자리의 크기에 맞추고 가장 낮은 점을 땅에 맞춘다.
  (Rock Moss Set은 바위 여러 개가 한 세트라, 넣으면 한 자리에 바위 무더기가 생긴다.)
