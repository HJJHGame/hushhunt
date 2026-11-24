using UnityEngine;

// 1인칭 이동 + 마우스 시점 + 스태미나. 총/규칙/네트워크는 이후 단계.
[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("Move")]
    public float walkSpeed = 4f;
    public float sprintSpeed = 7f;
    public float jumpHeight = 1.2f;
    public float gravity = -19.62f;

    [Header("Stamina")]
    public bool useStamina = true;
    public float staminaMax = 100f;
    public float drainPerSecond = 22f;      // 달리는 동안 초당 소모
    public float regenPerSecond = 16f;      // 쉬는 동안 초당 회복
    public float regenDelay = 1f;           // 달리기를 멈춘 뒤 회복 시작까지(초)
    public float resumeThreshold = 0.25f;   // 바닥나면 이 비율까지 회복해야 다시 달릴 수 있다

    [Header("Look")]
    public float mouseSensitivity = 2f;
    public Transform cameraPivot;

    public bool IsSprinting { get; private set; }
    public bool IsExhausted { get; private set; }
    public float Stamina01 { get { return staminaMax > 0f ? stamina / staminaMax : 1f; } }

    CharacterController controller;
    float verticalVelocity;
    float pitch;
    float stamina;
    float regenTimer;
    bool escaped; // Esc로 직접 커서를 푼 상태

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        stamina = staminaMax;

        // 기존 씬도 재생성 없이 보조 기능들을 자동으로 붙인다.
        AddIfMissing<PlayerBody>();     // 다리/발 모델
        AddIfMissing<Crosshair>();      // 크로스헤어
        AddIfMissing<TreeModels>();     // 소나무 3D 모델 (없으면 기존 나무 유지)
        AddIfMissing<NightAtmosphere>();// 밤 조명/안개
        AddIfMissing<GroundTexture>();  // 바닥 텍스처
        AddIfMissing<NightSky>();       // 별/달
        AddIfMissing<HorrorOverlay>();  // 비네팅/그레인
        AddIfMissing<FogMotes>();       // 떠다니는 먼지 입자
        AddIfMissing<AmbientSound>();   // 바람/먼 소리
        AddIfMissing<Flashlight>();     // 손전등 (F)
        AddIfMissing<PlayerHud>();      // 스태미나/손전등 표시
    }

    void AddIfMissing<T>() where T : Component
    {
        if (GetComponent<T>() == null) gameObject.AddComponent<T>();
    }

    void Start()
    {
        // 모델 머리 중심 y=0.66, 얼굴 앞쪽 z=0.10. 발바닥(-1) 기준 눈높이 1.72m.
        // 기존 씬에도 적용되도록 시작할 때 맞춘다.
        if (cameraPivot != null)
            cameraPivot.localPosition = new Vector3(0f, 0.72f, 0.10f);
        LockCursor(true);
    }

    void Update()
    {
        Look();
        Move();

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            escaped = true;
            LockCursor(false);
        }
        else if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked)
        {
            escaped = false;
            LockCursor(true);
        }
    }

    void Look()
    {
        if (Cursor.lockState != CursorLockMode.Locked) return;

        float mx = Input.GetAxis("Mouse X") * mouseSensitivity;
        float my = Input.GetAxis("Mouse Y") * mouseSensitivity;

        transform.Rotate(0f, mx, 0f);
        pitch = Mathf.Clamp(pitch - my, -89f, 89f);
        if (cameraPivot != null)
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    void Move()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 dir = (transform.right * h + transform.forward * v).normalized;

        bool moving = dir.sqrMagnitude > 0.01f;
        bool wantSprint = Input.GetKey(KeyCode.LeftShift) && moving;
        bool canSprint = !useStamina || (!IsExhausted && stamina > 0f);
        IsSprinting = wantSprint && canSprint;
        UpdateStamina();

        float speed = IsSprinting ? sprintSpeed : walkSpeed;

        if (controller.isGrounded)
        {
            verticalVelocity = -2f;
            if (Input.GetButtonDown("Jump"))
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        Vector3 velocity = dir * speed + Vector3.up * verticalVelocity;
        controller.Move(velocity * Time.deltaTime);

        // 점프 중 천장(집 지붕 안쪽 등)에 머리를 부딪히면 위로 계속 밀리지 않고 바로 떨어진다.
        if ((controller.collisionFlags & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
            verticalVelocity = 0f;
    }

    void UpdateStamina()
    {
        if (!useStamina) { stamina = staminaMax; IsExhausted = false; return; }

        if (IsSprinting)
        {
            stamina -= drainPerSecond * Time.deltaTime;
            regenTimer = regenDelay;
            if (stamina <= 0f)
            {
                stamina = 0f;
                IsExhausted = true;
            }
        }
        else
        {
            regenTimer -= Time.deltaTime;
            if (regenTimer <= 0f)
                stamina = Mathf.Min(staminaMax, stamina + regenPerSecond * Time.deltaTime);
            if (IsExhausted && Stamina01 >= resumeThreshold)
                IsExhausted = false;
        }
    }

    // 창 포커스를 얻을 때(게임 시작 직후 포함) 다시 잠가서 커서를 숨긴다.
    void OnApplicationFocus(bool focused)
    {
        if (focused && !escaped)
            LockCursor(true);
    }

    void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
