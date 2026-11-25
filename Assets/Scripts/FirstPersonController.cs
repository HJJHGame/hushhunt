using UnityEngine;

// 1인칭 이동 + 마우스 시점 + 스태미나 + 웅크리기(C/왼쪽 Ctrl). 총/규칙/네트워크는 이후 단계.
[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("Move")]
    public float walkSpeed = 4f;
    public float sprintSpeed = 7f;
    public float jumpHeight = 1.2f;
    public float gravity = -19.62f;

    [Header("Crouch")]
    public float crouchSpeed = 2f;
    public float crouchHeight = 1.2f;   // 웅크렸을 때 캡슐 높이 (서 있을 때는 시작 시 값 사용)

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
    public bool IsCrouching { get; private set; }
    public bool IsExhausted { get; private set; }
    public float Stamina01 { get { return staminaMax > 0f ? stamina / staminaMax : 1f; } }

    CharacterController controller;
    MainMenu menu;

    // 메뉴는 GameSystems가 씬에 하나만 붙인다. 찾으면 캐시한다 (issue #28).
    bool MenuOpen()
    {
        if (menu == null) menu = FindFirstObjectByType<MainMenu>();
        return menu != null && menu.IsOpen;
    }
    float verticalVelocity;
    float pitch;
    float stamina;
    float regenTimer;
    float standingHeight;
    float standingCenterY;
    const float StandingCamY = 0.72f;
    const float CrouchCamY = 0.10f;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        stamina = staminaMax;
        standingHeight = controller.height;
        standingCenterY = controller.center.y;
    }

    void Start()
    {
        // 모델 머리 중심 y=0.66, 얼굴 앞쪽 z=0.10. 발바닥(-1) 기준 눈높이 1.72m.
        // 기존 씬에도 적용되도록 시작할 때 맞춘다.
        if (cameraPivot != null)
            cameraPivot.localPosition = new Vector3(0f, 0.72f, 0.10f);
        // 타이틀 메뉴가 먼저 열려 있으면 메뉴가 커서를 관리한다.
        if (!MenuOpen())
            LockCursor(true);
    }

    void Update()
    {
        // 메뉴가 열려 있으면 커서 잠금/시점/이동은 MainMenu가 관리한다.
        if (MenuOpen()) return;

        mouseSensitivity = GameSettings.Sensitivity;
        Look();
        Move();

        // Esc는 MainMenu가 처리한다. 종료 화면 등에서 커서가 풀려 있으면 클릭으로 다시 잠근다.
        if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked)
            LockCursor(true);
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

        // C 또는 왼쪽 Ctrl을 누르고 있는 동안 웅크린다. 일어설 땐 머리 위 공간을 확인한다.
        bool wantCrouch = Input.GetKey(KeyCode.C) || Input.GetKey(KeyCode.LeftControl);
        if (wantCrouch) IsCrouching = true;
        else if (IsCrouching && CanStand()) IsCrouching = false;
        ApplyCrouch();

        bool wantSprint = Input.GetKey(KeyCode.LeftShift) && moving && !IsCrouching;
        bool canSprint = !useStamina || (!IsExhausted && stamina > 0f);
        IsSprinting = wantSprint && canSprint;
        UpdateStamina();

        float speed = IsCrouching ? crouchSpeed : (IsSprinting ? sprintSpeed : walkSpeed);

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

    void ApplyCrouch()
    {
        // 캡슐 아랫면(발바닥)은 그대로 두고 위만 줄였다 편다.
        float targetH = IsCrouching ? crouchHeight : standingHeight;
        if (!Mathf.Approximately(controller.height, targetH))
        {
            float feetY = standingCenterY - standingHeight * 0.5f;
            controller.height = targetH;
            controller.center = new Vector3(0f, feetY + targetH * 0.5f, 0f);
        }
        if (cameraPivot != null)
        {
            Vector3 p = cameraPivot.localPosition;
            p.y = Mathf.MoveTowards(p.y, IsCrouching ? CrouchCamY : StandingCamY, Time.deltaTime * 3f);
            cameraPivot.localPosition = p;
        }
    }

    bool CanStand()
    {
        // 서 있을 때 캡슐이 차지할 공간에 다른 콜라이더가 있으면 일어나지 않는다.
        float r = controller.radius * 0.95f;
        // 캡슐 중심의 X/Z는 현재 컨트롤러 설정을 따른다 (center를 바꿔도 검사 위치가 어긋나지 않게)
        Vector3 center = transform.TransformPoint(new Vector3(controller.center.x, standingCenterY, controller.center.z));
        float feetY = center.y - standingHeight * 0.5f;
        Vector3 p1 = new Vector3(center.x, feetY + r, center.z);
        Vector3 p2 = new Vector3(center.x, feetY + standingHeight - r, center.z);
        foreach (var col in Physics.OverlapCapsule(p1, p2, r, ~0, QueryTriggerInteraction.Ignore))
            if (col != controller && !col.transform.IsChildOf(transform))
                return false;
        return true;
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

    // 창 포커스를 얻을 때 다시 잠가서 커서를 숨긴다 (메뉴가 열려 있으면 메뉴 우선).
    void OnApplicationFocus(bool focused)
    {
        if (focused && !MenuOpen())
            LockCursor(true);
    }

    void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}

