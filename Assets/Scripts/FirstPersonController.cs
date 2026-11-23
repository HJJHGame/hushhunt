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
    bool cursorLocked = true; // 원하는 잠금 상태. 시작하면 잠금.

    void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    void Start()
    {
        LockCursor(true);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            cursorLocked = false;
        else if (Input.GetMouseButtonDown(0) && !cursorLocked)
            cursorLocked = true;

        // 잠금이 외부 요인(창 포커스 이동, 에디터, 원격 데스크톱)으로 조용히 풀리면
        // 커서는 보이지 않는 채 자유 커서가 되어 창 끝에서 마우스 회전이 멈춘다.
        // 매 프레임 원하는 상태로 다시 건다.
        bool isLocked = Cursor.lockState == CursorLockMode.Locked;
        if (isLocked != cursorLocked)
            LockCursor(cursorLocked);

        Look();
        Move();
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

    // 에디터/창 포커스를 잃으면 커서 잠금이 풀린다. 돌아올 때 다시 잠가야
    // 마우스가 게임 창 밖으로 나가는 일이 없다.
    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus) LockCursor(true);
    }

    void LockCursor(bool locked)
    {
        cursorLocked = locked;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
