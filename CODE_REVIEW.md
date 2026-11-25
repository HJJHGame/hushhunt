# 코드 리뷰 및 개선 내역

## 1. 고친 버그

| # | 문제 | 원인 | 수정 |
| --- | --- | --- | --- |
| 1 | **R 재시작 / "타이틀로" 뒤에 메뉴·HUD·조명 연출이 모두 사라지고 라운드가 시작되지 않음** | `GameSystems.Boot`는 `RuntimeInitializeOnLoadMethod`라 앱 시작 후 첫 씬에서 한 번만 실행된다. `SceneManager.LoadScene` 뒤에는 `GameSystems` 오브젝트가 씬과 함께 파괴되고 다시 만들어지지 않았다. 메뉴가 없으니 `GameManager`가 `Waiting`에 영원히 머문다. | `sceneLoaded`에 다시 연결해 씬을 불러올 때마다 설정한다. 여러 번 불려도 안전하다(`GameSystems.cs`). |
| 2 | 카메라 눈높이를 두 곳이 서로 덮어씀 | `PlayerCharacter`가 계산한 눈높이를 `FirstPersonController.ApplyCrouch`가 매 프레임 상수(0.72)로 되돌렸다. 즉 앞쪽 계산은 실제로 적용되지 않았다. | 눈 위치를 `FirstPersonController`의 필드(`standingEyeY` 등)가 단독으로 관리한다. **현재 화면에 보이던 값(0.72 / 0.12)은 그대로 유지**했다. 모델 키에 맞추고 싶으면 이 값만 바꾸면 된다. |
| 3 | "먼 곳의 소리"가 플레이어가 아니라 월드 원점 기준으로 재생됨 | `AmbientSound`가 원점에 있는 `GameSystems`의 위치를 기준으로 썼다. | 오디오 리스너 위치 기준으로 배치한다. |
| 4 | 안개 입자가 스폰 지점 주변에만 생김 | 주석은 "플레이어를 따라다닌다"인데 실제로는 따라가는 코드가 없었다. | `LateUpdate`에서 생성 위치만 플레이어를 따라가게 했다(입자 자체는 월드에 남는다). |
| 5 | 일시정지하면 캐릭터가 멈춘 채 걷는 모션을 계속 재생 | `CharacterController.velocity`는 마지막 `Move` 값이 남는다. 일시정지 때 `FirstPersonController`가 꺼지면 그 값이 그대로 남는다. | 실제 위치 변화량으로 속도를 계산한다. 순간이동 방어도 추가했다. |
| 6 | 설정 슬라이더를 끄는 동안 디스크 쓰기가 초당 수십 번 | 값이 바뀔 때마다 `PlayerPrefs.Save()` 호출 | 메모리 값만 바꾸고 설정 화면을 나갈 때·종료할 때 한 번만 저장한다(`GameSettings.Save`). 저장값이 범위를 벗어나도 clamp한다. |
| 7 | 웅크렸다 일어날 때 캡슐 `center`의 X/Z가 0으로 덮임 | `controller.center = new Vector3(0, ..., 0)` | 기존 X/Z를 유지한다. |
| 8 | KayKit 모델로 폴백할 때 UAL(다른 리그) 웅크리기 클립이 섞여 들어갈 수 있음 | 앞선 시도에서 읽은 클립 변수가 그대로 남았다. | 경로마다 새 `ClipSet`을 만들어 섞이지 않게 했다. |
| 9 | (에디터) 저장하지 않은 씬 수정이 조용히 사라질 수 있음 | `NewScene(Single)`과 `OpenProject` 재시작 전에 저장 확인이 없었다. | `SaveCurrentModifiedScenesIfUserWantsTo()`를 먼저 호출하고, 취소하면 중단한다. |
| 10 | (에디터) 씬 생성이 Build Settings의 다른 씬 목록을 덮어씀 | `EditorBuildSettings.scenes = new[]{ Main }` | 없을 때만 맨 앞에 추가한다. |

## 2. 코드 품질 개선

- **중복 제거**: `MenuOpen()`이 7개 클래스에 복사돼 있었다. `MainMenu.IsMenuOpen`(정적) 하나로 통일. 씬 다시 불러오기 로직도 `SceneReloader`로 합쳤다.
- **`PlayerCharacter` 구조 개선**: 약 200줄짜리 `Start()`를 `TryLoadRealistic` / `TryLoadKayKit` / `SpawnModel` / `Apply*Material` / `BuildGraph` 등으로 분리. 문자열 상태("idle", "walk"…)를 `enum State`로, 클립 묶음을 `ClipSet`으로 바꿨다. 사용하지 않던 필드와 중복 대입도 제거. 클립 기준 속도는 `FirstPersonController`의 걷기/달리기/웅크리기 속도를 따라간다(수치가 어긋나지 않게).
- **로딩 성능**: UAL FBX(약 23MB)를 `Resources.LoadAll`로 클립마다 다시 읽던 것을 경로별 캐시로 한 번만 읽게 했다. `MapImage`는 100만 픽셀에 `SetPixel/GetPixel`을 부르던 것을 배열에 그리고 `SetPixels` 한 번으로 바꿨다.
- **GC/할당**: `CanStand`의 `OverlapCapsule`을 `NonAlloc`으로, 손전등 렌즈는 상태가 바뀔 때만 머티리얼을 갱신한다.
- **안정성**: `Shader.Find("Standard")`가 4곳에 흩어져 있었고 빌드에서 셰이더가 빠지면 `new Material(null)`로 예외가 난다. `RuntimeMaterials`에서 한 번만 폴백 처리한다.
- **상수 공유**: 맵 크기(100m), 소나무 규격, 오브젝트 이름 문자열을 `WorldConfig`로 모아 `SetupScene`(씬 생성)과 런타임 코드가 어긋나지 않게 했다. 씬 생성 결과(좌표, 난수 순서)는 바뀌지 않아 `SceneVersion`은 올리지 않았다.
- **문서/설정**: README에 실행 방법·조작키·코드 구조 추가, `.gitignore`에 씬 백업 파일 추가.

## 3. 확인하지 못했거나 남겨둔 것

- **Unity에서 컴파일·실행해 보지 못했습니다.** 이 환경에는 Unity가 없어서 코드를 꼼꼼히 읽고 중괄호/구문만 점검했습니다. 열어서 컴파일 에러와 아래 동작을 확인해 주세요: R 재시작, 타이틀로 → 연습 시작, 웅크리기, 일시정지 중 모션.
- **`ProjectSettings`가 저장소에 `ProjectVersion.txt`뿐입니다.** 입력 설정은 에디터 스크립트가 고치고 에디터를 재시작하는 방식으로 우회하고 있는데, 새로 클론할 때마다 이 재시작이 일어납니다. `ProjectSettings` 폴더를 커밋하면 이 우회가 필요 없어집니다.
- 입력이 레거시 `Input` 기반입니다. Unity 6 기본인 Input System 패키지로 옮기는 것을 고려해 보세요(멀티플레이어 전에 옮기는 편이 비용이 적게 듭니다).
- `SetupScene.cs`(약 600줄)는 한 파일에 집, 나무, 소품 생성이 모두 들어 있습니다. 동작은 그대로 두고 파일 분리를 추천합니다. 난수 순서가 바뀌면 맵이 달라지므로 이번에는 손대지 않았습니다.
- 한글 텍스트가 Unity 내장 폰트에 의존합니다(OS 폴백). 플랫폼에 따라 글자가 비면 한글 폰트 에셋을 넣어야 합니다.
- 자동 테스트가 없습니다. `GameManager`의 단계 전환, `GameSettings` 범위 검증은 EditMode 테스트로 만들기 쉬운 부분입니다.
