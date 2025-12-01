using UnityEngine;

/// <summary>
/// Bridges the ProceduralPlanet with the gravity system (CelestialBody).
/// Add this component alongside ProceduralPlanet and CelestialBody.
/// 
/// This replaces the need for separate Visual and Collision objects
/// since the procedural chunks serve as both.
/// </summary>
[RequireComponent(typeof(ProceduralPlanet))]
public class ProceduralCelestialBody : MonoBehaviour
{
    [Header("=== GRAVITY SETTINGS ===")]
    [Tooltip("Surface gravity strength")]
    [SerializeField] private float surfaceGravity = 20f;
    
    [Tooltip("Sphere of influence radius (auto-calculated if 0)")]
    [SerializeField] private float sphereOfInfluence = 0f;
    
    [Tooltip("SOI multiplier relative to planet radius")]
    [SerializeField] private float soiMultiplier = 3f;
    
    [Header("=== ORBITAL SETTINGS ===")]
    [Tooltip("Body this planet orbits (null for stationary)")]
    [SerializeField] private ProceduralCelestialBody orbitTarget;
    
    [Tooltip("Orbit radius")]
    [SerializeField] private float orbitRadius = 200f;
    
    [Tooltip("Orbit speed in degrees per second")]
    [SerializeField] private float orbitSpeed = 5f;
    
    [Tooltip("Starting angle")]
    [SerializeField] private float initialOrbitAngle = 0f;
    
    [Tooltip("Orbit axis")]
    [SerializeField] private Vector3 orbitAxis = Vector3.up;
    
    [Header("=== DEBUG ===")]
    [SerializeField] private bool showSOI = true;
    [SerializeField] private Color soiColor = new Color(1f, 1f, 0f, 0.3f);
    
    // Components
    private ProceduralPlanet planet;
    
    // State
    private float currentOrbitAngle;
    private Vector3 currentVelocity;
    private Vector3 previousPosition;
    
    // Bodies in our SOI
    private System.Collections.Generic.HashSet<GravityBody> bodiesInSOI = 
        new System.Collections.Generic.HashSet<GravityBody>();
    
    // =========================================================================
    // PROPERTIES
    // =========================================================================
    
    public Vector3 Position => transform.position;
    public Vector3 Velocity => currentVelocity;
    public float Radius => planet != null ? planet.Radius : 50f;
    
    public float SphereOfInfluence
    {
        get
        {
            if (sphereOfInfluence > 0) return sphereOfInfluence;
            return Radius * soiMultiplier;
        }
    }
    
    // =========================================================================
    // UNITY LIFECYCLE
    // =========================================================================
    
    private void Awake()
    {
        planet = GetComponent<ProceduralPlanet>();
    }
    
    private void Start()
    {
        currentOrbitAngle = initialOrbitAngle;
        
        if (orbitTarget != null)
        {
            CalculateOrbitPosition();
        }
        
        previousPosition = transform.position;
        
        // Register with gravity manager if it exists
        if (GravityManager.Instance != null)
        {
            GravityManager.Instance.RegisterCelestialBody(this);
        }
    }
    
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
        
        // Calculate velocity
        Vector3 deltaPosition = transform.position - positionBeforeMove;
        currentVelocity = deltaPosition / Time.fixedDeltaTime;
        
        // Move bodies in our SOI along with us
        if (deltaPosition.sqrMagnitude > 0.0001f)
        {
            MoveAttachedBodies(deltaPosition);
        }
        
        previousPosition = transform.position;
    }
    
    // =========================================================================
    // ORBITAL MECHANICS
    // =========================================================================
    
    private void CalculateOrbitPosition()
    {
        if (orbitTarget == null) return;
        
        Vector3 orbitCenter = orbitTarget.Position;
        Quaternion rotation = Quaternion.AngleAxis(currentOrbitAngle, orbitAxis);
        Vector3 offset = rotation * (Vector3.right * orbitRadius);
        transform.position = orbitCenter + offset;
    }
    
    private void MoveAttachedBodies(Vector3 delta)
    {
        foreach (var body in bodiesInSOI)
        {
            if (body != null && body.Rigidbody != null)
            {
                body.Rigidbody.position += delta;
                body.transform.position = body.Rigidbody.position;
            }
        }
    }
    
    // =========================================================================
    // GRAVITY CALCULATIONS
    // =========================================================================
    
    /// <summary>
    /// Calculates gravity at a world position.
    /// </summary>
    public Vector3 GetGravityAtPoint(Vector3 point)
    {
        Vector3 direction = Position - point;
        float distance = direction.magnitude;
        
        if (distance < 0.1f) return Vector3.zero;
        
        // Use planet radius as surface distance
        float surfaceDistance = Radius;
        float effectiveDistance = Mathf.Max(distance, surfaceDistance);
        
        // Inverse square falloff
        float distanceRatio = effectiveDistance / surfaceDistance;
        float gravityMagnitude = surfaceGravity / (distanceRatio * distanceRatio);
        
        return direction.normalized * gravityMagnitude;
    }
    
    /// <summary>
    /// Gets the "up" direction at a point (away from planet center).
    /// </summary>
    public Vector3 GetUpDirection(Vector3 point)
    {
        return (point - Position).normalized;
    }
    
    /// <summary>
    /// Checks if a point is within our sphere of influence.
    /// </summary>
    public bool IsInSphereOfInfluence(Vector3 point)
    {
        return Vector3.Distance(Position, point) <= SphereOfInfluence;
    }
    
    // =========================================================================
    // SOI MANAGEMENT
    // =========================================================================
    
    public void OnBodyEnteredSOI(GravityBody body)
    {
        bodiesInSOI.Add(body);
        Debug.Log($"[{name}] {body.name} entered SOI");
    }
    
    public void OnBodyLeftSOI(GravityBody body)
    {
        bodiesInSOI.Remove(body);
        Debug.Log($"[{name}] {body.name} left SOI");
    }
    
    // =========================================================================
    // DEBUG
    // =========================================================================
    
    private void OnDrawGizmos()
    {
        if (showSOI)
        {
            Gizmos.color = soiColor;
            Gizmos.DrawWireSphere(transform.position, SphereOfInfluence);
        }
        
        // Draw orbit path
        if (orbitTarget != null)
        {
            Gizmos.color = Color.cyan;
            Vector3 center = Application.isPlaying ? orbitTarget.Position : orbitTarget.transform.position;
            DrawOrbitGizmo(center, orbitRadius, orbitAxis);
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

    private void OnDestroy()
    {
        if (GravityManager.Instance != null)
        {
            GravityManager.Instance.UnregisterCelestialBody(this);
        }
    }
}