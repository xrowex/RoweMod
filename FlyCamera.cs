using UnityEngine;

public class FlyCamera : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 10f;
    public float shiftMultiplier = 3f;
    public float climbSpeed = 5f;
    public float scrollSpeed = 5f;

    [Header("Look")]
    public float lookSensitivity = 3f;
    public bool requireRightMouse = true;
    public bool lockCursorOnLook = true;
    public float maxPitch = 89f;

    private float yaw;
    private float pitch;

    private void Start()
    {
        Vector3 euler = transform.eulerAngles;
        yaw = euler.y;
        pitch = euler.x;
    }

    private void Update()
    {
        HandleLook();
        HandleMove();
    }

    private void HandleLook()
    {
        bool lookActive = !requireRightMouse || Input.GetMouseButton(1);

        if (lookActive)
        {
            if (lockCursorOnLook)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            float mouseX = Input.GetAxis("Mouse X") * lookSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * lookSensitivity;

            yaw += mouseX;
            pitch = Mathf.Clamp(pitch - mouseY, -maxPitch, maxPitch);

            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }
        else if (lockCursorOnLook)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void HandleMove()
    {
        float speed = moveSpeed;
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            speed *= shiftMultiplier;
        }

        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");
        float y = 0f;
        if (Input.GetKey(KeyCode.E))
        {
            y += 1f;
        }
        if (Input.GetKey(KeyCode.Q))
        {
            y -= 1f;
        }

        Vector3 move = (transform.right * x + transform.forward * z) * speed;
        move += transform.up * y * climbSpeed;
        transform.position += move * Time.deltaTime;

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0f)
        {
            moveSpeed = Mathf.Max(0.1f, moveSpeed + scroll * scrollSpeed);
        }
    }
}
