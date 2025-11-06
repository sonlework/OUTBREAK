using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class FPSController : MonoBehaviour
{
    [Header("References")]
    public Camera playerCamera;                 // camera con (set trong inspector)

    [Header("Movement")]
    public float walkSpeed = 4f;
    public float sprintSpeed = 8f;
    public float crouchSpeed = 2f;
    public float acceleration = 10f;            // how fast we change speed
    public float gravity = -9.81f;
    public float jumpHeight = 1.6f;

    [Header("Mouse Look")]
    public float mouseSensitivity = 2.0f;
    public float mouseSmoothing = 2.0f;         // higher = smoother (laggy)
    public float minPitch = -85f;
    public float maxPitch = 85f;

    [Header("Crouch")]
    public float standingHeight = 1.8f;
    public float crouchHeight = 1.0f;
    public float crouchTransitionSpeed = 8f;

    [Header("Head Bob")]
    public bool enableHeadBob = true;
    public float bobFrequency = 1.5f;
    public float bobHorizontalAmplitude = 0.02f;
    public float bobVerticalAmplitude = 0.03f;

    [Header("Ground Check")]
    public LayerMask groundMask;
    public float groundedOffset = -0.14f;      // offset for CharacterController.isGrounded check

    // private
    CharacterController cc;
    Vector2 currentMouseDelta;
    Vector2 smoothMouseDelta;
    float yaw;    // rotation around y
    float pitch;  // rotation around x
    float verticalVelocity;
    float currentSpeed;
    float targetSpeed;
    bool isCrouching = false;

    // headbob state
    float bobTimer = 0f;
    Vector3 cameraInitialLocalPos;

    void Start()
    {
        cc = GetComponent<CharacterController>();
        if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>();
        cameraInitialLocalPos = playerCamera.transform.localPosition;

        // set initial heights based on CharacterController if possible
        standingHeight = Mathf.Max(standingHeight, cc.height);
        // lock cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        HandleMouseLook();
        HandleMovement();
        HandleCrouchHeight();
        HandleHeadBob();
    }

    void HandleMouseLook()
    {
        // raw mouse
        Vector2 rawMouse = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
        currentMouseDelta = rawMouse * mouseSensitivity;

        // smoothing
        smoothMouseDelta.x = Mathf.Lerp(smoothMouseDelta.x, currentMouseDelta.x, 1f / mouseSmoothing);
        smoothMouseDelta.y = Mathf.Lerp(smoothMouseDelta.y, currentMouseDelta.y, 1f / mouseSmoothing);

        yaw += smoothMouseDelta.x;
        pitch -= smoothMouseDelta.y;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    void HandleMovement()
    {
        // input
        float inputX = Input.GetAxisRaw("Horizontal");
        float inputZ = Input.GetAxisRaw("Vertical");
        Vector3 inputDir = new Vector3(inputX, 0, inputZ);
        inputDir = Vector3.ClampMagnitude(inputDir, 1f);

        // determine target speed (walk, sprint, crouch)
        bool sprinting = Input.GetKey(KeyCode.LeftShift) && !isCrouching && inputZ > 0.01f;
        targetSpeed = sprinting ? sprintSpeed : (isCrouching ? crouchSpeed : walkSpeed);

        // smooth speed change
        currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, Time.deltaTime * acceleration);

        // movement in local space (relative to player yaw)
        Vector3 move = transform.TransformDirection(inputDir) * currentSpeed;

        // gravity & jump
        bool grounded = IsGrounded();
        if (grounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f; // small downward to keep grounded
        }

        if (grounded && Input.GetButtonDown("Jump") && !isCrouching)
        {
            verticalVelocity = Mathf.Sqrt(2f * jumpHeight * -gravity);
        }

        verticalVelocity += gravity * Time.deltaTime;
        move.y = verticalVelocity;

        // apply move
        cc.Move(move * Time.deltaTime);
    }

    bool IsGrounded()
    {
        // CharacterController.isGrounded is usually reliable but we offset slightly
        // We also raycast downward as backup
        if (cc.isGrounded) return true;

        Vector3 origin = transform.position + Vector3.up * 0.1f;
        float distance = 0.2f;
        return Physics.Raycast(origin, Vector3.down, distance, groundMask);
    }

    void HandleCrouchHeight()
    {
        if (Input.GetKeyDown(KeyCode.C))
        {
            isCrouching = !isCrouching;
        }

        float targetHeight = isCrouching ? crouchHeight : standingHeight;
        float current = cc.height;
        float newHeight = Mathf.Lerp(current, targetHeight, Time.deltaTime * crouchTransitionSpeed);

        // adjust center so player doesn't sink into floor
        cc.height = newHeight;
        cc.center = new Vector3(0, newHeight / 2f, 0);

        // lower/raise camera smoothly
        Vector3 camLocal = playerCamera.transform.localPosition;
        float eyeY = newHeight - 0.15f; // camera slightly below top
        camLocal.y = Mathf.Lerp(camLocal.y, eyeY, Time.deltaTime * crouchTransitionSpeed);
        playerCamera.transform.localPosition = camLocal;
    }

    void HandleHeadBob()
    {
        if (!enableHeadBob) return;

        Vector3 horizontalVel = new Vector3(cc.velocity.x, 0, cc.velocity.z);
        float speed = horizontalVel.magnitude;

        if (speed > 0.1f && cc.isGrounded)
        {
            bobTimer += Time.deltaTime * (speed / walkSpeed) * bobFrequency;
            float bobX = Mathf.Sin(bobTimer * Mathf.PI * 2f) * bobHorizontalAmplitude;
            float bobY = Mathf.Cos(bobTimer * Mathf.PI * 2f) * bobVerticalAmplitude;
            playerCamera.transform.localPosition = cameraInitialLocalPos + new Vector3(bobX, bobY, 0);
        }
        else
        {
            // return to original
            bobTimer = 0f;
            playerCamera.transform.localPosition = Vector3.Lerp(playerCamera.transform.localPosition, cameraInitialLocalPos, Time.deltaTime * 8f);
        }
    }

    // optional: public method to unlock cursor (for menus)
    public void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
