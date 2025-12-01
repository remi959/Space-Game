# Unity Static Orbit Gravity System - Implementation Guide

## Architecture Overview

This system uses **static orbital paths** for celestial bodies (planets/moons) and **dynamic gravity** only for small objects (player, items, etc.). Celestial bodies follow predetermined orbital animations while small objects respond to the nearest gravity source.

### Core Components

1. **CelestialBody** - Handles orbital animation and acts as a gravity source (no physics)
2. **GravityBody** - Component for objects dynamically affected by gravity
3. **GravityManager** - Manages gravity calculations for dynamic objects only

---

## Step 1: Create the CelestialBody Class

```csharp
using UnityEngine;

public class CelestialBody : MonoBehaviour
{
    [Header("Gravity Properties")]
    [SerializeField] private float mass = 1000f;
    [SerializeField] private float sphereOfInfluence = 100f;
    
    [Header("Orbital Properties")]
    [SerializeField] private CelestialBody orbitTarget; // null for stars
    [SerializeField] private float orbitSpeed = 10f;
    [SerializeField] private float orbitRadius = 50f;
    [SerializeField] private float initialOrbitAngle = 0f;
    [SerializeField] private Vector3 orbitAxis = Vector3.up;
    
    [Header("Rotation")]
    [SerializeField] private float rotationSpeed = 20f;
    [SerializeField] private Vector3 rotationAxis = Vector3.up;
    
    [Header("Visual Debug")]
    [SerializeField] private bool showSOI = true;
    [SerializeField] private bool showOrbitPath = true;
    [SerializeField] private Color soiColor = Color.yellow;
    
    // Public properties
    public float Mass => mass;
    public float SphereOfInfluence => sphereOfInfluence;
    public Vector3 Position => transform.position;
    public Vector3 SurfaceGravity => GetGravityAtPoint(transform.position + transform.up * (transform.localScale.y / 2));
    
    private float currentOrbitAngle;
    private Vector3 orbitCenter;
    
    private void Start()
    {
        GravityManager.Instance.RegisterCelestialBody(this);
        currentOrbitAngle = initialOrbitAngle;
        
        if (orbitTarget != null)
        {
            UpdateOrbitPosition();
        }
    }
    
    private void OnDestroy()
    {
        if (GravityManager.Instance != null)
        {
            GravityManager.Instance.UnregisterCelestialBody(this);
        }
    }
    
    private void Update()
    {
        // Update orbital position
        if (orbitTarget != null)
        {
            UpdateOrbitPosition();
            currentOrbitAngle += orbitSpeed * Time.deltaTime;
        }
        
        // Update rotation
        transform.Rotate(rotationAxis, rotationSpeed * Time.deltaTime, Space.Self);
    }
    
    private void UpdateOrbitPosition()
    {
        // Orbit center follows parent body
        orbitCenter = orbitTarget.Position;
        
        // Calculate position on orbit
        Quaternion rotation = Quaternion.AngleAxis(currentOrbitAngle, orbitAxis);
        Vector3 offset = rotation * (Vector3.right * orbitRadius);
        
        transform.position = orbitCenter + offset;
    }
    
    public Vector3 GetGravityAtPoint(Vector3 point)
    {
        Vector3 direction = Position - point;
        float distance = direction.magnitude;
        
        // Prevent division by zero and extreme forces
        if (distance < 0.1f) return Vector3.zero;
        
        // Simple inverse square law
        float forceMagnitude = mass / (distance * distance);
        return direction.normalized * forceMagnitude;
    }
    
    public bool IsInSphereOfInfluence(Vector3 point)
    {
        return Vector3.Distance(Position, point) <= sphereOfInfluence;
    }
    
    public Vector3 GetUpDirection(Vector3 point)
    {
        return (point - Position).normalized;
    }
    
    private void OnDrawGizmos()
    {
        // Draw sphere of influence
        if (showSOI)
        {
            Gizmos.color = soiColor;
            Gizmos.DrawWireSphere(transform.position, sphereOfInfluence);
        }
        
        // Draw orbit path
        if (showOrbitPath && orbitTarget != null)
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
}
```

---

## Step 2: Create the GravityBody Class

```csharp
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class GravityBody : MonoBehaviour
{
    [Header("Gravity Settings")]
    [SerializeField] private float gravityMultiplier = 1f;
    [SerializeField] private bool alignToGravity = true;
    [SerializeField] private float alignmentSpeed = 5f;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;
    
    private Rigidbody rb;
    private CelestialBody currentGravitySource;
    
    public CelestialBody CurrentGravitySource => currentGravitySource;
    public Rigidbody Rigidbody => rb;
    
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false; // Disable Unity's default gravity
        rb.constraints = RigidbodyConstraints.FreezeRotation; // Prevent physics rotation
    }
    
    private void Start()
    {
        GravityManager.Instance.RegisterGravityBody(this);
    }
    
    private void OnDestroy()
    {
        if (GravityManager.Instance != null)
        {
            GravityManager.Instance.UnregisterGravityBody(this);
        }
    }
    
    public void ApplyGravity(CelestialBody newSource)
    {
        // Check if gravity source changed
        if (newSource != currentGravitySource)
        {
            OnGravitySourceChanged(currentGravitySource, newSource);
            currentGravitySource = newSource;
        }
        
        // Apply gravity force
        if (currentGravitySource != null)
        {
            Vector3 gravity = currentGravitySource.GetGravityAtPoint(transform.position);
            rb.AddForce(gravity * gravityMultiplier, ForceMode.Acceleration);
        }
    }
    
    public void AlignToGravity()
    {
        if (currentGravitySource != null && alignToGravity)
        {
            Vector3 gravityUp = currentGravitySource.GetUpDirection(transform.position);
            Quaternion targetRotation = Quaternion.FromToRotation(transform.up, gravityUp) * transform.rotation;
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, alignmentSpeed * Time.fixedDeltaTime);
        }
    }
    
    private void OnGravitySourceChanged(CelestialBody oldSource, CelestialBody newSource)
    {
        // Optional: Add effects or callbacks when switching SOI
        if (newSource != null)
        {
            Debug.Log($"{gameObject.name} entered {newSource.name}'s sphere of influence");
        }
    }
    
    private void OnDrawGizmos()
    {
        if (showDebugInfo && Application.isPlaying && currentGravitySource != null)
        {
            // Draw line to current gravity source
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, currentGravitySource.Position);
            
            // Draw gravity direction
            Vector3 gravityDir = currentGravitySource.GetGravityAtPoint(transform.position).normalized;
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position, gravityDir * 2f);
        }
    }
}
```

---

## Step 3: Create the GravityManager Singleton

```csharp
using System.Collections.Generic;
using UnityEngine;

public class GravityManager : MonoBehaviour
{
    private static GravityManager instance;
    public static GravityManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("GravityManager");
                instance = go.AddComponent<GravityManager>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }
    
    [Header("Performance Settings")]
    [Tooltip("How many times per second to update gravity (lower = better performance)")]
    [SerializeField] private int gravityUpdateRate = 50;
    
    private List<CelestialBody> celestialBodies = new List<CelestialBody>();
    private List<GravityBody> gravityBodies = new List<GravityBody>();
    
    private float updateTimer;
    private float updateInterval;
    
    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        instance = this;
        DontDestroyOnLoad(gameObject);
        updateInterval = 1f / gravityUpdateRate;
    }
    
    public void RegisterCelestialBody(CelestialBody body)
    {
        if (!celestialBodies.Contains(body))
        {
            celestialBodies.Add(body);
        }
    }
    
    public void UnregisterCelestialBody(CelestialBody body)
    {
        celestialBodies.Remove(body);
    }
    
    public void RegisterGravityBody(GravityBody body)
    {
        if (!gravityBodies.Contains(body))
        {
            gravityBodies.Add(body);
        }
    }
    
    public void UnregisterGravityBody(GravityBody body)
    {
        gravityBodies.Remove(body);
    }
    
    private void FixedUpdate()
    {
        updateTimer += Time.fixedDeltaTime;
        
        if (updateTimer >= updateInterval)
        {
            updateTimer = 0f;
            UpdateAllGravityBodies();
        }
        
        // Always align to gravity each physics frame for smooth rotation
        foreach (var body in gravityBodies)
        {
            if (body != null)
            {
                body.AlignToGravity();
            }
        }
    }
    
    private void UpdateAllGravityBodies()
    {
        foreach (var body in gravityBodies)
        {
            if (body != null)
            {
                CelestialBody dominantSource = FindDominantGravitySource(body.transform.position);
                body.ApplyGravity(dominantSource);
            }
        }
    }
    
    private CelestialBody FindDominantGravitySource(Vector3 position)
    {
        CelestialBody dominant = null;
        float smallestSOIDistance = float.MaxValue;
        
        // Priority 1: Find the smallest SOI that contains the point
        // This ensures moons take priority over planets when in overlapping SOIs
        foreach (var body in celestialBodies)
        {
            if (body.IsInSphereOfInfluence(position))
            {
                float soiSize = body.SphereOfInfluence;
                if (soiSize < smallestSOIDistance)
                {
                    smallestSOIDistance = soiSize;
                    dominant = body;
                }
            }
        }
        
        // Priority 2: If not in any SOI, find the closest celestial body
        if (dominant == null && celestialBodies.Count > 0)
        {
            float closestDistance = float.MaxValue;
            
            foreach (var body in celestialBodies)
            {
                float distance = Vector3.Distance(position, body.Position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    dominant = body;
                }
            }
        }
        
        return dominant;
    }
    
    public CelestialBody GetClosestCelestialBody(Vector3 position)
    {
        return FindDominantGravitySource(position);
    }
    
    public List<CelestialBody> GetAllCelestialBodies()
    {
        return new List<CelestialBody>(celestialBodies);
    }
}
```

---

## Step 4: Setup Instructions

### Creating Your Solar System

#### 1. Create the Sun (Star)
```
- Create a Sphere GameObject named "Sun"
- Scale it up (e.g., 20, 20, 20)
- Add CelestialBody component
  • Mass: 10000
  • Sphere of Influence: 500
  • Orbit Target: None (leave empty)
  • Rotation Speed: 5
```

#### 2. Create a Planet
```
- Create a Sphere GameObject named "Planet"
- Scale it (e.g., 5, 5, 5)
- Add CelestialBody component
  • Mass: 1000
  • Sphere of Influence: 100
  • Orbit Target: Sun
  • Orbit Speed: 15
  • Orbit Radius: 200
  • Initial Orbit Angle: 0
  • Orbit Axis: (0, 1, 0)
  • Rotation Speed: 30
```

#### 3. Create a Moon
```
- Create a Sphere GameObject named "Moon"
- Scale it (e.g., 2, 2, 2)
- Add CelestialBody component
  • Mass: 100
  • Sphere of Influence: 30
  • Orbit Target: Planet
  • Orbit Speed: 40
  • Orbit Radius: 15
  • Initial Orbit Angle: 0
  • Orbit Axis: (0, 1, 0)
  • Rotation Speed: 50
```

#### 4. Create Player/Dynamic Object
```
- Create a Capsule GameObject named "Player"
- Add Rigidbody component
  • Mass: 1
  • Drag: 0.5
  • Angular Drag: 0.5
- Add GravityBody component
  • Gravity Multiplier: 1
  • Align To Gravity: true
  • Alignment Speed: 5
```

### Recommended Hierarchy
```
Scene
├── GravityManager (auto-created)
├── Sun (CelestialBody)
├── Planet (CelestialBody, orbits Sun)
├── Moon (CelestialBody, orbits Planet)
└── Player (GravityBody + Rigidbody)
```

---

## Step 5: Tuning Your System

### Gravity Feel
- **Too slow fall?** Increase Mass on celestial bodies or Gravity Multiplier on GravityBody
- **Too fast fall?** Decrease Mass or Gravity Multiplier
- **Floaty feel?** Reduce Rigidbody Drag
- **Sticky landing?** Increase Rigidbody Drag

### Orbital Motion
- **Faster orbits:** Increase Orbit Speed
- **Larger orbits:** Increase Orbit Radius
- **Tilted orbits:** Change Orbit Axis (e.g., (1, 1, 0) for diagonal)
- **Multiple planets:** Use different Initial Orbit Angle values

### Sphere of Influence
- **Moon too weak?** Increase Moon's SOI or decrease Planet's SOI
- **Transition too abrupt?** Adjust SOI sizes to overlap slightly
- **Player stuck between bodies?** Ensure SOIs don't overlap too much

---

## Performance Notes

### This Architecture is Efficient Because:
1. **No physics on celestial bodies** - they use simple transform updates
2. **Batched gravity updates** - configurable update rate (50 Hz default)
3. **Simple SOI checks** - distance calculations only
4. **No collision checks** needed between celestial bodies

### For Large Solar Systems:
- Lower the Gravity Update Rate to 30 Hz
- Use spatial partitioning if you have 50+ celestial bodies
- Only calculate gravity for bodies near the player

---

## Example Player Controller Integration

```csharp
using UnityEngine;

[RequireComponent(typeof(GravityBody))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 10f;
    [SerializeField] private LayerMask groundLayer;
    
    private GravityBody gravityBody;
    private Rigidbody rb;
    private bool isGrounded;
    
    private void Start()
    {
        gravityBody = GetComponent<GravityBody>();
        rb = gravityBody.Rigidbody;
    }
    
    private void Update()
    {
        CheckGrounded();
        HandleMovement();
        
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            Jump();
        }
    }
    
    private void CheckGrounded()
    {
        if (gravityBody.CurrentGravitySource == null) return;
        
        Vector3 down = -gravityBody.CurrentGravitySource.GetUpDirection(transform.position);
        isGrounded = Physics.Raycast(transform.position, down, 1.1f, groundLayer);
    }
    
    private void HandleMovement()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        
        Vector3 moveDir = (transform.right * h + transform.forward * v).normalized;
        rb.AddForce(moveDir * moveSpeed, ForceMode.Acceleration);
    }
    
    private void Jump()
    {
        Vector3 up = gravityBody.CurrentGravitySource.GetUpDirection(transform.position);
        rb.AddForce(up * jumpForce, ForceMode.Impulse);
    }
}
```

---

## Extension Ideas

### Visual Polish
- Add trail renderers to show orbital paths in-game
- Particle effects when transitioning between SOIs
- Camera shake when entering strong gravity fields
- Atmosphere fade effects near planets

### Gameplay Features
- Launch pads with trajectory prediction lines
- Gravity wells that slow projectiles
- Collectibles that orbit automatically when collected
- Gravity-based puzzles (orbital slingshots)

### Advanced Features
- Atmospheric drag within planets (reduce velocity near surface)
- Tidal forces for objects near SOI boundaries
- Day/night cycle based on planetary rotation
- Temperature zones (hot near sun, cold far away)

---

## Common Issues & Solutions

**Problem:** Objects orbit too perfectly, looks unnatural  
**Solution:** Add slight variation to Orbit Speed or Initial Orbit Angle

**Problem:** Player gets flung when switching SOI  
**Solution:** Add velocity damping in OnGravitySourceChanged

**Problem:** Moon doesn't stay with planet  
**Solution:** Ensure Moon's Orbit Target is set to the Planet, not the Sun

**Problem:** Gravity feels inconsistent  
**Solution:** Keep Gravity Update Rate at 50+ Hz, or use FixedUpdate more frequently

This architecture gives you full control over your solar system while keeping dynamic objects responsive and fun to control!