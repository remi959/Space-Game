using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central manager for the gravity system.
/// Coordinates gravity calculations between celestial bodies and gravity-affected objects.
/// 
/// IMPORTANT: Set Script Execution Order in Project Settings:
///   CelestialBody: -100 (runs first - moves planets)
///   GravityManager: 0 (runs second - applies gravity)
///   GravityBody: 100 (runs last - responds to gravity)
/// </summary>
public class GravityManager : MonoBehaviour
{
    // =====================================================================
    // SINGLETON
    // =====================================================================
    
    public static GravityManager Instance { get; private set; }
    
    // =====================================================================
    // CONFIGURATION
    // =====================================================================
    
    [Header("=== PERFORMANCE SETTINGS ===")]
    [Tooltip("How many times per second to recalculate dominant gravity source (lower = better performance)")]
    [SerializeField] private int sourceUpdateRate = 30;
    
    [Tooltip("How many times per second to apply gravity forces (should match or exceed physics rate)")]
    [SerializeField] private int gravityApplicationRate = 50;
    
    [Header("=== DEBUG ===")]
    [SerializeField] private bool logRegistrations = false;
    
    // =====================================================================
    // STATE
    // =====================================================================
    
    private List<ProceduralCelestialBody> celestialBodies = new List<ProceduralCelestialBody>();
    private List<GravityBody> gravityBodies = new List<GravityBody>();
    
    private float sourceUpdateTimer;
    private float sourceUpdateInterval;
    
    private float gravityUpdateTimer;
    private float gravityUpdateInterval;
    
    // Cache for dominant gravity sources (updated less frequently)
    private Dictionary<GravityBody, ProceduralCelestialBody> dominantSources = new Dictionary<GravityBody, ProceduralCelestialBody>();
    
    // =====================================================================
    // UNITY LIFECYCLE
    // =====================================================================
    
    private void Awake()
    {
        // Singleton setup
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[GravityManager] Duplicate instance destroyed");
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        
        // Calculate update intervals
        sourceUpdateInterval = 1f / sourceUpdateRate;
        gravityUpdateInterval = 1f / gravityApplicationRate;
    }
    
    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
    
    /// <summary>
    /// FixedUpdate runs during the physics simulation.
    /// We apply gravity here so it's in sync with physics.
    /// 
    /// Execution order:
    /// 1. CelestialBody.FixedUpdate - moves planets, carries GravityBodies along
    /// 2. GravityManager.FixedUpdate - applies gravity forces
    /// 3. Physics simulation runs
    /// 4. GravityBody responds to forces
    /// </summary>
    private void FixedUpdate()
    {
        // Update dominant gravity sources (less frequent for performance)
        sourceUpdateTimer += Time.fixedDeltaTime;
        if (sourceUpdateTimer >= sourceUpdateInterval)
        {
            sourceUpdateTimer = 0f;
            UpdateDominantSources();
        }
        
        // Apply gravity forces (more frequent for smooth gravity)
        gravityUpdateTimer += Time.fixedDeltaTime;
        if (gravityUpdateTimer >= gravityUpdateInterval)
        {
            gravityUpdateTimer = 0f;
            ApplyGravityToAllBodies();
        }
        
        // Always update alignment each physics frame for smooth rotation
        UpdateAllAlignments();
    }
    
    // =====================================================================
    // REGISTRATION
    // =====================================================================
    
    /// <summary>
    /// Registers a celestial body with the gravity system.
    /// Called automatically by CelestialBody.Start().
    /// </summary>
    public void RegisterCelestialBody(ProceduralCelestialBody body)
    {
        if (!celestialBodies.Contains(body))
        {
            celestialBodies.Add(body);
            
            if (logRegistrations)
            {
                Debug.Log($"[GravityManager] Registered celestial body: {body.name}");
            }
        }
    }
    
    /// <summary>
    /// Unregisters a celestial body from the gravity system.
    /// Called automatically by CelestialBody.OnDestroy().
    /// </summary>
    public void UnregisterCelestialBody(ProceduralCelestialBody body)
    {
        celestialBodies.Remove(body);
        
        if (logRegistrations)
        {
            Debug.Log($"[GravityManager] Unregistered celestial body: {body.name}");
        }
    }
    
    /// <summary>
    /// Registers a gravity-affected body with the system.
    /// Called automatically by GravityBody.Start().
    /// </summary>
    public void RegisterGravityBody(GravityBody body)
    {
        if (!gravityBodies.Contains(body))
        {
            gravityBodies.Add(body);
            
            // Immediately find and assign the dominant gravity source
            ProceduralCelestialBody source = FindDominantGravitySource(body.transform.position);
            dominantSources[body] = source;
            body.ApplyGravity(source);
            
            if (logRegistrations)
            {
                Debug.Log($"[GravityManager] Registered gravity body: {body.name} (source: {source?.name ?? "none"})");
            }
        }
    }
    
    /// <summary>
    /// Unregisters a gravity-affected body from the system.
    /// Called automatically by GravityBody.OnDestroy().
    /// </summary>
    public void UnregisterGravityBody(GravityBody body)
    {
        gravityBodies.Remove(body);
        dominantSources.Remove(body);
        
        if (logRegistrations)
        {
            Debug.Log($"[GravityManager] Unregistered gravity body: {body.name}");
        }
    }
    
    // =====================================================================
    // GRAVITY UPDATES
    // =====================================================================
    
    /// <summary>
    /// Updates which celestial body is the dominant gravity source for each GravityBody.
    /// This is the "expensive" calculation, so it runs less frequently.
    /// </summary>
    private void UpdateDominantSources()
    {
        foreach (var body in gravityBodies)
        {
            if (body == null) continue;
            
            ProceduralCelestialBody newSource = FindDominantGravitySource(body.transform.position);
            dominantSources[body] = newSource;
        }
    }
    
    /// <summary>
    /// Applies gravity from the cached dominant source to each GravityBody.
    /// </summary>
    private void ApplyGravityToAllBodies()
    {
        foreach (var body in gravityBodies)
        {
            if (body == null) continue;
            
            if (dominantSources.TryGetValue(body, out ProceduralCelestialBody source))
            {
                body.ApplyGravity(source);
            }
        }
    }
    
    /// <summary>
    /// Updates rotation alignment for all gravity bodies.
    /// Runs every physics frame for smooth rotation.
    /// </summary>
    private void UpdateAllAlignments()
    {
        foreach (var body in gravityBodies)
        {
            if (body != null)
            {
                body.UpdateAlignment();
            }
        }
    }
    
    // =====================================================================
    // GRAVITY SOURCE FINDING
    // =====================================================================
    
    /// <summary>
    /// Finds the celestial body that should provide gravity at the given position.
    /// 
    /// Priority:
    /// 1. If inside multiple SOIs, choose the smallest SOI (most local body)
    /// 2. If not in any SOI, choose the closest body
    /// </summary>
    private ProceduralCelestialBody FindDominantGravitySource(Vector3 position)
    {
        ProceduralCelestialBody dominant = null;
        float smallestSOI = float.MaxValue;
        
        // First pass: find smallest SOI that contains the position
        foreach (var body in celestialBodies)
        {
            if (body.IsInSphereOfInfluence(position))
            {
                if (body.SphereOfInfluence < smallestSOI)
                {
                    smallestSOI = body.SphereOfInfluence;
                    dominant = body;
                }
            }
        }
        
        // If found a body, return it
        if (dominant != null)
        {
            return dominant;
        }
        
        // Second pass: not in any SOI, find closest body
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
        
        return dominant;
    }
    
    // =====================================================================
    // PUBLIC QUERIES
    // =====================================================================
    
    /// <summary>
    /// Gets the celestial body providing gravity at a given position.
    /// Useful for spawning objects or checking what body a point is near.
    /// </summary>
    public ProceduralCelestialBody GetDominantBodyAt(Vector3 position)
    {
        return FindDominantGravitySource(position);
    }
    
    /// <summary>
    /// Gets all registered celestial bodies.
    /// </summary>
    public List<ProceduralCelestialBody> GetAllCelestialBodies()
    {
        return new List<ProceduralCelestialBody>(celestialBodies);
    }
    
    /// <summary>
    /// Gets all registered gravity bodies.
    /// </summary>
    public List<GravityBody> GetAllGravityBodies()
    {
        return new List<GravityBody>(gravityBodies);
    }
}