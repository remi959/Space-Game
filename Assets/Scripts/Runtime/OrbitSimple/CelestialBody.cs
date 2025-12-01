using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Represents a celestial body (star, planet, moon) with separated visual and collision scales.
/// The visual mesh creates the sense of scale while the collision shell keeps physics stable.
/// </summary>
public class CelestialBody : MonoBehaviour
{
    [Header("=== REFERENCES ===")]
    [Tooltip("Child object containing the visual mesh (large scale for visuals)")]
    [SerializeField] private Transform visualTransform;
    
    [Tooltip("Child object containing the collision sphere (small scale for physics)")]
    [SerializeField] private Transform collisionTransform;
    
    [Header("=== GRAVITY SETTINGS ===")]
    [Tooltip("Base gravity strength at the visual surface")]
    [SerializeField] private float surfaceGravity = 20f;
    
    [Tooltip("Radius of the sphere of influence - outside this, another body takes over")]
    [SerializeField] private float sphereOfInfluence = 5000f;
    
    [Header("=== ORBITAL SETTINGS ===")]
    [Tooltip("The body this celestial object orbits around (null for stationary objects like stars)")]
    [SerializeField] private CelestialBody orbitTarget;
    
    [Tooltip("Distance from the orbit target's center")]
    [SerializeField] private float orbitRadius = 5000f;
    
    [Tooltip("Orbital speed in degrees per second")]
    [SerializeField] private float orbitSpeed = 5f;
    
    [Tooltip("Starting angle in the orbit (degrees)")]
    [SerializeField] private float initialOrbitAngle = 0f;
    
    [Tooltip("Axis around which this body orbits")]
    [SerializeField] private Vector3 orbitAxis = Vector3.up;
    
    [Header("=== ROTATION SETTINGS ===")]
    [Tooltip("How fast the body rotates around its own axis (degrees per second)")]
    [SerializeField] private float rotationSpeed = 10f;
    
    [Tooltip("Axis around which the body rotates")]
    [SerializeField] private Vector3 rotationAxis = Vector3.up;
    
    [Header("=== DEBUG VISUALIZATION ===")]
    [SerializeField] private bool showSOI = true;
    [SerializeField] private bool showOrbitPath = true;
    [SerializeField] private bool showCollisionShell = true;
    [SerializeField] private Color soiColor = new Color(1f, 1f, 0f, 0.3f);
    [SerializeField] private Color orbitColor = Color.cyan;
    [SerializeField] private Color collisionColor = new Color(0f, 1f, 0f, 0.5f);
    
    // === PUBLIC PROPERTIES ===
    
    /// <summary>World position of this celestial body's center</summary>
    public Vector3 Position => transform.position;
    
    /// <summary>Current velocity of this body (from orbital motion)</summary>
    public Vector3 Velocity => currentVelocity;
    
    /// <summary>Radius of the visual mesh (what the player sees)</summary>
    public float VisualRadius => visualTransform != null ? visualTransform.lossyScale.x * 0.5f : 1f;
    
    /// <summary>Radius of the collision shell (what the player walks on)</summary>
    public float CollisionRadius => collisionTransform != null ? collisionTransform.lossyScale.x * 0.5f : 1f;
    
    /// <summary>The sphere of influence radius</summary>
    public float SphereOfInfluence => sphereOfInfluence;
    
    /// <summary>Surface gravity strength</summary>
    public float SurfaceGravity => surfaceGravity;
    
    // === PRIVATE STATE ===
    
    private float currentOrbitAngle;
    private Vector3 currentVelocity;
    private Vector3 previousPosition;
    private HashSet<GravityBody> bodiesInSOI = new HashSet<GravityBody>();
    
    // =====================================================================
    // UNITY LIFECYCLE
    // =====================================================================
    
    private void Start()
    {
        ValidateReferences();
        
        // Initialize orbit
        currentOrbitAngle = initialOrbitAngle;
        if (orbitTarget != null)
        {
            CalculateOrbitPosition();
        }
        
        previousPosition = transform.position;
    }
    
    
    
    /// <summary>
    /// FixedUpdate handles all physics-related movement.
    /// This ensures the collision shell moves in sync with the physics simulation.
    /// </summary>
    private void FixedUpdate()
    {
        Vector3 positionBeforeMove = transform.position;
        
        // Update orbital position
        if (orbitTarget != null)
        {
            currentOrbitAngle += orbitSpeed * Time.fixedDeltaTime;
            if (currentOrbitAngle >= 360f) currentOrbitAngle -= 360f;
            CalculateOrbitPosition();
        }
        
        // Calculate velocity from position change
        Vector3 deltaPosition = transform.position - positionBeforeMove;
        currentVelocity = deltaPosition / Time.fixedDeltaTime;
        
        // Move all GravityBodies in our SOI by the same delta
        // This is the key to preventing spring/phasing issues!
        if (deltaPosition.sqrMagnitude > 0.0001f)
        {
            MoveAttachedBodies(deltaPosition);
        }
        
        previousPosition = transform.position;
    }
    
    /// <summary>
    /// Update handles visual-only changes like rotation.
    /// This doesn't affect physics so it can run at frame rate.
    /// </summary>
    private void Update()
    {
        // Rotate the visual mesh (and collision shell rotates with parent)
        if (visualTransform != null)
        {
            visualTransform.Rotate(rotationAxis, rotationSpeed * Time.deltaTime, Space.Self);
        }
    }
    
    // =====================================================================
    // ORBITAL MECHANICS
    // =====================================================================
    
    /// <summary>
    /// Calculates and sets the position based on orbit parameters.
    /// </summary>
    private void CalculateOrbitPosition()
    {
        if (orbitTarget == null) return;
        
        Vector3 orbitCenter = orbitTarget.Position;
        Quaternion rotation = Quaternion.AngleAxis(currentOrbitAngle, orbitAxis);
        Vector3 offset = rotation * (Vector3.right * orbitRadius);
        transform.position = orbitCenter + offset;
    }
    
    /// <summary>
    /// Moves all GravityBodies currently in our SOI by the given delta.
    /// Called during FixedUpdate to keep bodies in sync with our movement.
    /// </summary>
    private void MoveAttachedBodies(Vector3 delta)
    {
        foreach (var body in bodiesInSOI)
        {
            if (body != null && body.Rigidbody != null)
            {
                // Directly adjust the rigidbody position
                // This happens before physics solving, so collisions work correctly
                body.Rigidbody.position += delta;
                body.transform.position = body.Rigidbody.position;
            }
        }
    }
    
    // =====================================================================
    // SPHERE OF INFLUENCE MANAGEMENT
    // =====================================================================
    
    /// <summary>
    /// Called by GravityManager when a GravityBody enters our SOI.
    /// </summary>
    public void OnBodyEnteredSOI(GravityBody body)
    {
        if (bodiesInSOI.Add(body))
        {
            Debug.Log($"[{name}] {body.name} entered sphere of influence");
        }
    }
    
    /// <summary>
    /// Called by GravityManager when a GravityBody leaves our SOI.
    /// </summary>
    public void OnBodyLeftSOI(GravityBody body)
    {
        if (bodiesInSOI.Remove(body))
        {
            Debug.Log($"[{name}] {body.name} left sphere of influence");
        }
    }
    
    /// <summary>
    /// Checks if a point is within our sphere of influence.
    /// </summary>
    public bool IsInSphereOfInfluence(Vector3 point)
    {
        return Vector3.Distance(Position, point) <= sphereOfInfluence;
    }
    
    // =====================================================================
    // GRAVITY CALCULATIONS
    // =====================================================================
    
    /// <summary>
    /// Calculates the gravity vector at a given point.
    /// Uses the VISUAL radius for surface distance, giving proper "feel" of a large planet.
    /// </summary>
    public Vector3 GetGravityAtPoint(Vector3 point)
    {
        Vector3 direction = Position - point;
        float distance = direction.magnitude;
        
        // Avoid division by zero
        if (distance < 0.1f) return Vector3.zero;
        
        // Use visual radius as the "surface" for gravity calculation
        // This makes gravity feel correct for the visible size of the planet
        float surfaceDistance = VisualRadius;
        
        // Clamp distance to at least surface distance (prevents extreme gravity when close)
        float effectiveDistance = Mathf.Max(distance, surfaceDistance);
        
        // Inverse square law: gravity falls off with square of distance
        float distanceRatio = effectiveDistance / surfaceDistance;
        float gravityMagnitude = surfaceGravity / (distanceRatio * distanceRatio);
        
        return direction.normalized * gravityMagnitude;
    }
    
    /// <summary>
    /// Gets the "up" direction at a given point (away from planet center).
    /// </summary>
    public Vector3 GetUpDirection(Vector3 point)
    {
        return (point - Position).normalized;
    }
    
    /// <summary>
    /// Gets the height above the COLLISION surface at a given point.
    /// This is the actual walkable surface height.
    /// </summary>
    public float GetHeightAboveSurface(Vector3 point)
    {
        float distanceFromCenter = Vector3.Distance(Position, point);
        return distanceFromCenter - CollisionRadius;
    }
    
    // =====================================================================
    // VALIDATION
    // =====================================================================
    
    private void ValidateReferences()
    {
        if (visualTransform == null)
        {
            Debug.LogError($"[{name}] Visual Transform is not assigned! Please assign the child object with the mesh.");
        }
        
        if (collisionTransform == null)
        {
            Debug.LogError($"[{name}] Collision Transform is not assigned! Please assign the child object with the collider.");
        }
        
        if (collisionTransform != null && collisionTransform.GetComponent<Collider>() == null)
        {
            Debug.LogError($"[{name}] Collision Transform has no Collider component!");
        }
    }
    
    // =====================================================================
    // DEBUG VISUALIZATION
    // =====================================================================
    
    private void OnDrawGizmos()
    {
        // Draw Sphere of Influence
        if (showSOI)
        {
            Gizmos.color = soiColor;
            Gizmos.DrawWireSphere(transform.position, sphereOfInfluence);
        }
        
        // Draw orbit path
        if (showOrbitPath && orbitTarget != null)
        {
            Gizmos.color = orbitColor;
            Vector3 center = Application.isPlaying ? orbitTarget.Position : orbitTarget.transform.position;
            DrawOrbitGizmo(center, orbitRadius, orbitAxis);
        }
        
        // Draw collision shell (in editor, helps visualize the physics boundary)
        if (showCollisionShell && collisionTransform != null)
        {
            Gizmos.color = collisionColor;
            float radius = collisionTransform.lossyScale.x * 0.5f;
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        // When selected, also show the visual radius
        if (visualTransform != null)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            float radius = visualTransform.lossyScale.x * 0.5f;
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
    
    private void DrawOrbitGizmo(Vector3 center, float radius, Vector3 axis)
    {
        int segments = 64;
        Vector3 prevPoint = center + Quaternion.AngleAxis(0, axis) * (Vector3.right * radius);
        
        for (int i = 1; i <= segments; i++)
        {
            float angle = (i / (float)segments) * 360f;
            Vector3 point = center + Quaternion.AngleAxis(angle, axis) * (Vector3.right * radius);
            Gizmos.DrawLine(prevPoint, point);
            prevPoint = point;
        }
    }
}