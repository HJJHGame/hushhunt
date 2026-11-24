using UnityEngine;

// 1단계: 1인칭 이동 + 마우스 시점만. 총/규칙/네트워크는 이후 단계.
[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("Move")]
    public float walkSpeed = 4f;
    public float sprintSpeed = 7f;
    public float jumpHeight = 1.2f;
    public float gravity = -19.62f;

    [Header("Look")]
    public float mouseSensitivity = 2f;
    public Transform cameraPivot;

    CharacterController controller;
    float verticalVelocity;
    float pitch;
    bool escaped; // Esc로 직접 커서를 푼 상태

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        // 기존 씬도 재생성 없이 다리/발 모델을 표시한다.
        if (GetComponent<PlayerBody>() == null)
            gameObject.AddComponent<PlayerBody>();
        // 크로스헤어도 기존 씬에 자동으로 붙인다.
        if (GetComponent<Crosshair>() == null)
            gameObject.AddComponent<Crosshair>();
        // 소나무를 실제 3D 모델로 교체 (모델이 없으면 기존 나무 유지).
        if (GetComponent<TreeModels>() == null)
            gameObject.AddComponent<TreeModels>();
        // 밤 조명(그림자/환경광/안개)도 기존 씬에 자동 적용.
        if (GetComponent<NightAtmosphere>() == null)
            gameObject.AddComponent<NightAtmosphere>();
        // 바닥 텍스처(흙/이끼/낙엽)도 기존 씬에 자동 적용.
        if (GetComponent<GroundTexture>() == null)
            gameObject.AddComponent<GroundTexture>();
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
        float speed = Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : walkSpeed;

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
