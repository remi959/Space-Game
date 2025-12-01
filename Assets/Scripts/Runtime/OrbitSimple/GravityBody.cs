using UnityEngine;

/// <summary>
/// Attach this to any object that should be affected by celestial gravity.
/// The object will be attracted to celestial bodies and can walk on their collision shells.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class GravityBody : MonoBehaviour
{
    [Header("=== GRAVITY SETTINGS ===")]
    [Tooltip("Multiplier for gravity strength (1 = normal, 0.5 = half gravity, 2 = double)")]
    [SerializeField] private float gravityMultiplier = 1f;
    
    [Tooltip("Should this object rotate to align 'up' with the gravity direction?")]
    [SerializeField] private bool alignToGravity = true;
    
    [Tooltip("How fast the object rotates to align with gravity (higher = snappier)")]
    [SerializeField] private float alignmentSpeed = 10f;
    
    [Header("=== DEBUG ===")]
    [SerializeField] private bool showDebugVisuals = true;
    
    // === PUBLIC PROPERTIES ===
    
    /// <summary>The celestial body currently providing gravity to this object</summary>
    public ProceduralCelestialBody CurrentGravitySource => currentGravitySource;
    
    /// <summary>The Rigidbody component on this object</summary>
    public Rigidbody Rigidbody => rb;
    
    /// <summary>Current gravity vector being applied</summary>
    public Vector3 CurrentGravity => currentGravity;
    
    // === PRIVATE STATE ===
    
    private Rigidbody rb;
    private ProceduralCelestialBody currentGravitySource;
    private Vector3 currentGravity;
    private Quaternion targetRotation;
    
    // =====================================================================
    // UNITY LIFECYCLE
    // =====================================================================
    
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        ConfigureRigidbody();
    }
    
    private void Start()
    {
        // Register with the gravity manager
        GravityManager.Instance.RegisterGravityBody(this);
        targetRotation = transform.rotation;
    }
    
    private void OnDestroy()
    {
        // Unregister and notify current gravity source
        if (currentGravitySource != null)
        {
            currentGravitySource.OnBodyLeftSOI(this);
        }
        
        if (GravityManager.Instance != null)
        {
            GravityManager.Instance.UnregisterGravityBody(this);
        }
    }
    
    /// <summary>
    /// LateUpdate handles rotation smoothing.
    /// This runs after all other updates for smooth visual rotation.
    /// </summary>
    private void LateUpdate()
    {
        if (alignToGravity && currentGravitySource != null)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation, 
                targetRotation, 
                alignmentSpeed * Time.deltaTime
            );
        }
    }
    
    // =====================================================================
    // RIGIDBODY CONFIGURATION
    // =====================================================================
    
    /// <summary>
    /// Configures the Rigidbody for custom gravity.
    /// </summary>
    private void ConfigureRigidbody()
    {
        // Disable Unity's built-in gravity - we handle gravity ourselves
        rb.useGravity = false;
        
        // Freeze rotation - we handle rotation alignment ourselves
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        
        // Use interpolation for smooth movement
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        
        // Use continuous collision detection for fast-moving objects
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }
    
    // =====================================================================
    // GRAVITY APPLICATION (Called by GravityManager)
    // =====================================================================
    
    /// <summary>
    /// Called by GravityManager each physics frame to apply gravity.
    /// </summary>
    /// <param name="newSource">The celestial body that should provide gravity</param>
    public void ApplyGravity(ProceduralCelestialBody newSource)
    {
        // Handle gravity source changes
        if (newSource != currentGravitySource)
        {
            HandleGravitySourceChange(newSource);
        }
        
        // Apply gravity force
        if (currentGravitySource != null)
        {
            currentGravity = currentGravitySource.GetGravityAtPoint(transform.position);
            rb.AddForce(currentGravity * gravityMultiplier, ForceMode.Acceleration);
        }
        else
        {
            currentGravity = Vector3.zero;
        }
    }
    
    /// <summary>
    /// Called by GravityManager each physics frame to update rotation alignment.
    /// </summary>
    public void UpdateAlignment()
    {
        if (alignToGravity && currentGravitySource != null)
        {
            // Calculate the target "up" direction (away from planet center)
            Vector3 gravityUp = currentGravitySource.GetUpDirection(transform.position);
            
            // Calculate rotation that aligns our up with gravity up
            targetRotation = Quaternion.FromToRotation(transform.up, gravityUp) * transform.rotation;
        }
    }
    
    // =====================================================================
    // GRAVITY SOURCE MANAGEMENT
    // =====================================================================
    
    /// <summary>
    /// Handles the transition from one gravity source to another.
    /// </summary>
    private void HandleGravitySourceChange(ProceduralCelestialBody newSource)
    {
        // Notify old source that we're leaving
        if (currentGravitySource != null)
        {
            currentGravitySource.OnBodyLeftSOI(this);
        }
        
        // Notify new source that we're entering
        if (newSource != null)
        {
            newSource.OnBodyEnteredSOI(this);
        }
        
        // Log the transition
        string oldName = currentGravitySource != null ? currentGravitySource.name : "none";
        string newName = newSource != null ? newSource.name : "none";
        Debug.Log($"[{name}] Gravity source changed: {oldName} → {newName}");
        
        // Update reference
        currentGravitySource = newSource;
    }
    
    // =====================================================================
    // DEBUG VISUALIZATION
    // =====================================================================
    
    private void OnDrawGizmos()
    {
        if (!showDebugVisuals || !Application.isPlaying || currentGravitySource == null)
            return;
        
        // Line to gravity source center
        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, currentGravitySource.Position);
        
        // Gravity direction arrow
        Gizmos.color = Color.red;
        if (currentGravity.sqrMagnitude > 0.01f)
        {
            Vector3 gravityDir = currentGravity.normalized;
            DrawArrow(transform.position, gravityDir * 3f);
        }
        
        // Up direction arrow
        Gizmos.color = Color.blue;
        DrawArrow(transform.position, transform.up * 2f);
        
        // Velocity indicator
        if (rb != null && rb.linearVelocity.magnitude > 0.1f)
        {
            Gizmos.color = Color.yellow;
            DrawArrow(transform.position, rb.linearVelocity.normalized * 2f);
        }
    }
    
    private void DrawArrow(Vector3 start, Vector3 direction)
    {
        Vector3 end = start + direction;
        Gizmos.DrawLine(start, end);
        
        // Arrowhead
        Vector3 right = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 + 20, 0) * Vector3.forward;
        Vector3 left = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 - 20, 0) * Vector3.forward;
        Gizmos.DrawLine(end, end + right * 0.5f);
        Gizmos.DrawLine(end, end + left * 0.5f);
    }
}