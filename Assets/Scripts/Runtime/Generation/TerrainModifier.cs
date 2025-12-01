using UnityEngine;

/// <summary>
/// Optimized terrain modifier that works with the batched update system.
/// </summary>
public class TerrainModifier : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ProceduralPlanet planet;

    [Header("Modification Settings")]
    [SerializeField] private float modifyRadius = 2f;
    [SerializeField] private float modifyStrength = 5f;
    [SerializeField] private KeyCode addTerrainKey = KeyCode.Mouse0;
    [SerializeField] private KeyCode removeTerrainKey = KeyCode.Mouse1;

    [Header("Raycast Settings")]
    [SerializeField] private float maxRayDistance = 100f;
    [SerializeField] private LayerMask terrainLayer = ~0;

    private Camera mainCamera;

    private void Start()
    {
        mainCamera = Camera.main;

        if (planet == null)
        {
            planet = FindFirstObjectByType<ProceduralPlanet>();
        }
    }

    private void Update()
    {
        if (planet == null || mainCamera == null) return;

        if (Input.GetKey(addTerrainKey))
        {
            TryModifyTerrain(modifyStrength);
        }
        else if (Input.GetKey(removeTerrainKey))
        {
            TryModifyTerrain(-modifyStrength);
        }
    }

    private void TryModifyTerrain(float strength)
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, terrainLayer))
        {
            planet.ModifyTerrain(hit.point, modifyRadius, strength * Time.deltaTime);
        }
    }

    public void ModifyAt(Vector3 position, float radius, float strength)
    {
        if (planet != null)
        {
            planet.ModifyTerrain(position, radius, strength);
        }
    }
}