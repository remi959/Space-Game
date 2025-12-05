using UnityEngine;

public class Planet : MonoBehaviour
{
    [Range(2, 256)]
    [SerializeField] private Material terrainMaterial;

    [Header("Settings")]
    [Tooltip("Starting distance from planet center")]
    public float startDistance = 110f;

    [Tooltip("Movement speed")]
    public float moveSpeed = 20f;

    [Tooltip("Mouse look sensitivity")]
    public float lookSensitivity = 2f;

    [Header("Chunk Loading")]
    [Tooltip("Distance in world units to load chunks around the player")]
    public float loadDistance = 80f;

    [Tooltip("Distance in world units to unload chunks (should be > loadDistance for hysteresis)")]
    public float unloadDistance = 120f;

    [Header("Debug")]
    public bool showStats = true;
    public bool showChunkBounds = true;

    public bool autoUpdate = false;

    private ShapeGenerator shapeGenerator;
    public ShapeSettings shapeSettings;
    public ColorSettings colorSettings;
    public CaveSettings caveSettings;

    [HideInInspector]
    public bool shapeSettingsFoldout;
    [HideInInspector]
    public bool colorSettingsFoldout;
    [HideInInspector]
    public bool caveSettingsFoldout;

    [SerializeField, HideInInspector]
    private MeshFilter[] meshFilters;
    private TerrainFace[] terrainFaces;

    // Components
    private ChunkManager chunkManager;
    private Transform playerTransform;
    private Camera playerCamera;

    // Mouse look
    private float rotationX = 0f;
    private float rotationY = 0f;

    void Start()
    {
        if (!ValidateSetup()) return;

        SetupPlayer();
        SetupChunkManager();
        
        Debug.Log("=== Shared Boundary Test Started ===");
        Debug.Log($"Planet Radius: {shapeSettings.PlanetRadius}");
        Debug.Log($"Chunk Size: {shapeSettings.ChunkSize}");
        Debug.Log($"Chunk Resolution: {shapeSettings.ChunkResolution}");
        Debug.Log("Use WASD + Space/Shift to move, Right-click + Mouse to look");
    }

    bool ValidateSetup()
    {
        if (shapeSettings == null)
        {
            Debug.LogError("ShapeSettings is not assigned! Create one via Right-click > Create > Planet > Shape Settings");
            return false;
        }

        if (terrainMaterial == null)
        {
            Debug.LogWarning("No terrain material assigned. Creating a default material.");
            terrainMaterial = new Material(Shader.Find("Standard"));
            terrainMaterial.color = new Color(0.4f, 0.6f, 0.3f); // Earthy green
        }

        // Validate ShapeSettings has reasonable values
        if (shapeSettings.PlanetRadius <= 0)
        {
            Debug.LogError("PlanetRadius must be greater than 0!");
            return false;
        }

        if (shapeSettings.ChunkSize <= 0)
        {
            Debug.LogError("ChunkSize must be greater than 0!");
            return false;
        }

        if (shapeSettings.ChunkResolution < 2)
        {
            Debug.LogError("ChunkResolution must be at least 2!");
            return false;
        }

        return true;
    }

    void SetupPlayer()
    {
        // Create player object
        GameObject playerObj = new GameObject("Player");
        playerTransform = playerObj.transform;

        // Position player above the planet surface
        playerTransform.position = Vector3.up * startDistance;
        playerTransform.LookAt(Vector3.zero); // Look at planet center

        // Setup camera
        playerCamera = playerObj.AddComponent<Camera>();
        playerCamera.nearClipPlane = 0.1f;
        playerCamera.farClipPlane = 1000f;
        playerCamera.fieldOfView = 60f;

        // Add audio listener
        playerObj.AddComponent<AudioListener>();

        // Initialize rotation from current orientation
        rotationY = playerTransform.eulerAngles.y;
        rotationX = playerTransform.eulerAngles.x;
    }

    void SetupChunkManager()
    {
        // Create chunk manager
        GameObject chunkManagerObj = new("ChunkManager");
        chunkManagerObj.transform.parent = transform;

        chunkManager = chunkManagerObj.AddComponent<ChunkManager>();
        chunkManager.settings = shapeSettings;
        chunkManager.caveSettings = caveSettings;
        chunkManager.terrainMaterial = terrainMaterial;
        chunkManager.player = playerTransform;
        chunkManager.loadDistance = loadDistance;
        chunkManager.unloadDistance = unloadDistance;
        chunkManager.showChunkBounds = showChunkBounds;
    }

    void Update()
    {
        HandleMovement();
        HandleMouseLook();
    }

    void HandleMovement()
    {
        Vector3 moveDir = Vector3.zero;

        // WASD movement
        if (Input.GetKey(KeyCode.W)) moveDir += playerTransform.forward;
        if (Input.GetKey(KeyCode.S)) moveDir -= playerTransform.forward;
        if (Input.GetKey(KeyCode.A)) moveDir -= playerTransform.right;
        if (Input.GetKey(KeyCode.D)) moveDir += playerTransform.right;

        // Vertical movement
        if (Input.GetKey(KeyCode.Space)) moveDir += Vector3.up;
        if (Input.GetKey(KeyCode.LeftShift)) moveDir -= Vector3.up;

        // Apply movement
        float speed = moveSpeed * (Input.GetKey(KeyCode.LeftControl) ? 3f : 1f);
        playerTransform.position += moveDir.normalized * speed * Time.deltaTime;
    }

    void HandleMouseLook()
    {
        // Only rotate when right mouse button is held
        if (Input.GetMouseButton(1))
        {
            rotationX -= Input.GetAxis("Mouse Y") * lookSensitivity;
            rotationY += Input.GetAxis("Mouse X") * lookSensitivity;

            rotationX = Mathf.Clamp(rotationX, -90f, 90f);

            playerTransform.rotation = Quaternion.Euler(rotationX, rotationY, 0f);

            // Lock and hide cursor while looking
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    void OnGUI()
    {
        if (!showStats || playerTransform == null) return;

        GUILayout.BeginArea(new Rect(10, 10, 350, 250));
        GUILayout.BeginVertical("box");

        GUILayout.Label($"Position: {playerTransform.position:F1}");
        GUILayout.Label($"Distance from center: {playerTransform.position.magnitude:F1}");
        GUILayout.Label($"Altitude: {playerTransform.position.magnitude - shapeSettings.PlanetRadius:F1}");

        GUILayout.Label("");
        GUILayout.Label($"Load Distance: {loadDistance} | Unload: {unloadDistance}");
        GUILayout.Label($"Chunk Size: {shapeSettings.ChunkSize} | Resolution: {shapeSettings.ChunkResolution}");

        GUILayout.Label("");
        GUILayout.Label("Controls:");
        GUILayout.Label("WASD - Move | Space/Shift - Up/Down");
        GUILayout.Label("Right-click + Mouse - Look");
        GUILayout.Label("Ctrl - Move faster");

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
}