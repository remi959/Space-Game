using UnityEngine;

/// <summary>
/// Player controller that works with the GravityBody system.
/// Handles movement relative to the current gravity direction.
/// 
/// FIX: Mouse rotation now properly accumulates yaw separately and applies it
/// in LateUpdate after GravityBody has aligned the player to gravity.
/// </summary>
[RequireComponent(typeof(GravityBody))]
public class PlayerController : MonoBehaviour
{
    [Header("=== MOVEMENT ===")]
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float sprintMultiplier = 2f;
    [SerializeField] private float jumpForce = 15f;
    
    [Header("=== GROUND CHECK ===")]
    [SerializeField] private float groundCheckDistance = 0.2f;
    [SerializeField] private LayerMask groundLayers = ~0; // Everything by default
    [SerializeField] private float groundCheckRadius = 0.3f;
    
    [Header("=== CAMERA ===")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float maxLookAngle = 80f;
    
    [Header("=== DEBUG ===")]
    [SerializeField] private bool showDebugGizmos = false;
    
    // Components
    private GravityBody gravityBody;
    private Rigidbody rb;
    
    // Camera rotation state - stored separately from transform
    private float yawRotation = 0f;   // Horizontal rotation (around gravity up)
    private float pitchRotation = 0f; // Vertical rotation (camera only)
    
    // Ground check state
    private bool isGrounded;
    private Vector3 groundNormal = Vector3.up;
    
    // Cached values
    private Vector3 lastGravityUp = Vector3.up;
    
    private void Awake()
    {
        gravityBody = GetComponent<GravityBody>();
        rb = GetComponent<Rigidbody>();
    }
    
    private void Start()
    {
        // Lock cursor for FPS controls
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        // If no camera assigned, try to find one in children
        if (cameraTransform == null)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null)
            {
                cameraTransform = cam.transform;
            }
        }
        
        // Initialize yaw from current rotation
        yawRotation = transform.eulerAngles.y;
    }
    
    private void Update()
    {
        // Accumulate mouse input (don't apply yet - wait for LateUpdate)
        AccumulateMouseInput();
        
        // Check ground
        CheckGround();
        
        // Handle cursor lock toggle
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = Cursor.lockState == CursorLockMode.Locked 
                ? CursorLockMode.None 
                : CursorLockMode.Locked;
            Cursor.visible = Cursor.lockState != CursorLockMode.Locked;
        }
    }
    
    private void FixedUpdate()
    {
        HandleMovement();
        HandleJump();
    }
    
    /// <summary>
    /// Apply rotation AFTER GravityBody has done its alignment.
    /// This prevents the rotation from being overwritten.
    /// </summary>
    private void LateUpdate()
    {
        ApplyRotation();
    }
    
    /// <summary>
    /// Accumulates mouse input into yaw and pitch values.
    /// Does NOT directly rotate the transform.
    /// </summary>
    private void AccumulateMouseInput()
    {
        if (Cursor.lockState != CursorLockMode.Locked)
            return;
        
        // Get mouse input
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
        
        // Accumulate yaw (horizontal rotation around gravity up)
        yawRotation += mouseX;
        
        // Keep yaw in 0-360 range to prevent floating point issues over time
        if (yawRotation > 360f) yawRotation -= 360f;
        if (yawRotation < 0f) yawRotation += 360f;
        
        // Accumulate pitch (vertical rotation for camera only)
        pitchRotation -= mouseY;
        pitchRotation = Mathf.Clamp(pitchRotation, -maxLookAngle, maxLookAngle);
    }
    
    /// <summary>
    /// Applies the accumulated rotation on top of gravity alignment.
    /// Called in LateUpdate after GravityBody has set the base rotation.
    /// </summary>
    private void ApplyRotation()
    {
        // Get the current gravity "up" direction from the player's alignment
        Vector3 gravityUp = transform.up;
        
        // Build a rotation that:
        // 1. Has the player's "up" aligned with gravity up
        // 2. Has the player's "forward" rotated by yaw around gravity up
        
        // Find a reference forward direction (perpendicular to gravity up)
        Vector3 referenceForward;
        if (Mathf.Abs(Vector3.Dot(gravityUp, Vector3.forward)) < 0.99f)
        {
            referenceForward = Vector3.ProjectOnPlane(Vector3.forward, gravityUp).normalized;
        }
        else
        {
            // Fallback if gravity up is nearly aligned with world forward
            referenceForward = Vector3.ProjectOnPlane(Vector3.right, gravityUp).normalized;
        }
        
        // Apply yaw rotation around gravity up
        Quaternion yawQuat = Quaternion.AngleAxis(yawRotation, gravityUp);
        Vector3 targetForward = yawQuat * referenceForward;
        
        // Create the final rotation
        Quaternion targetRotation = Quaternion.LookRotation(targetForward, gravityUp);
        transform.rotation = targetRotation;
        
        // Apply pitch to camera (local X rotation)
        if (cameraTransform != null)
        {
            cameraTransform.localRotation = Quaternion.Euler(pitchRotation, 0f, 0f);
        }
        
        // Store for next frame
        lastGravityUp = gravityUp;
    }
    
    /// <summary>
    /// Handles WASD movement relative to current gravity.
    /// </summary>
    private void HandleMovement()
    {
        // Get input
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        
        if (Mathf.Approximately(horizontal, 0f) && Mathf.Approximately(vertical, 0f))
            return;
        
        // Calculate movement direction relative to player orientation
        Vector3 moveDirection = (transform.forward * vertical + transform.right * horizontal).normalized;
        
        // Apply sprint
        float currentSpeed = moveSpeed;
        if (Input.GetKey(KeyCode.LeftShift))
        {
            currentSpeed *= sprintMultiplier;
        }
        
        // Calculate target velocity
        Vector3 targetVelocity = moveDirection * currentSpeed;
        
        // Get current velocity in local gravity space
        Vector3 gravityUp = transform.up;
        Vector3 currentHorizontalVelocity = Vector3.ProjectOnPlane(rb.linearVelocity, gravityUp);
        
        // Apply movement force
        Vector3 velocityChange = targetVelocity - currentHorizontalVelocity;
        rb.AddForce(velocityChange, ForceMode.VelocityChange);
    }
    
    /// <summary>
    /// Handles jumping relative to current gravity direction.
    /// </summary>
    private void HandleJump()
    {
        // Use GetButton to check in FixedUpdate properly
        if (Input.GetButton("Jump") && isGrounded)
        {
            // Jump in the direction opposite to gravity (our local up)
            Vector3 jumpDirection = transform.up;
            rb.AddForce(jumpDirection * jumpForce, ForceMode.VelocityChange);
        }
    }
    
    /// <summary>
    /// Checks if the player is standing on ground using a sphere cast.
    /// </summary>
    private void CheckGround()
    {
        // Cast a sphere in the direction of gravity (our local down)
        Vector3 rayOrigin = transform.position;
        Vector3 rayDirection = -transform.up;
        
        // Account for player height (assuming collider is at center)
        float playerHeight = 1f;
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            playerHeight = col.bounds.extents.y * 2f;
        }
        
        float rayLength = (playerHeight * 0.5f) + groundCheckDistance;
        
        // Use SphereCast for more reliable ground detection
        if (Physics.SphereCast(rayOrigin, groundCheckRadius, rayDirection, out RaycastHit hit, 
            rayLength - groundCheckRadius, groundLayers, QueryTriggerInteraction.Ignore))
        {
            isGrounded = true;
            groundNormal = hit.normal;
        }
        else
        {
            isGrounded = false;
            groundNormal = transform.up;
        }
        
        // Debug visualization
        if (showDebugGizmos)
        {
            Debug.DrawRay(rayOrigin, rayDirection * rayLength, isGrounded ? Color.green : Color.red);
        }
    }
    
    /// <summary>
    /// Resets yaw rotation (call when teleporting player).
    /// </summary>
    public void ResetYawRotation(float newYaw = 0f)
    {
        yawRotation = newYaw;
    }
    
    /// <summary>
    /// Sets camera pitch (useful for cutscenes or resets).
    /// </summary>
    public void SetCameraPitch(float pitch)
    {
        pitchRotation = Mathf.Clamp(pitch, -maxLookAngle, maxLookAngle);
    }
    
    // Public accessors
    public bool IsGrounded => isGrounded;
    public Vector3 GroundNormal => groundNormal;
    public float CurrentYaw => yawRotation;
    public float CurrentPitch => pitchRotation;
    
    private void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos) return;
        
        // Draw ground check sphere
        Vector3 rayOrigin = transform.position;
        Vector3 rayDirection = -transform.up;
        float playerHeight = 1f;
        float rayLength = (playerHeight * 0.5f) + groundCheckDistance;
        
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(rayOrigin + rayDirection * (rayLength - groundCheckRadius), groundCheckRadius);
    }
}