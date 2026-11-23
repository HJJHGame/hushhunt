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
        Look();
        Move();

        if (Input.GetKeyDown(KeyCode.Escape))
            LockCursor(false);
        else if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked)
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

    void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
