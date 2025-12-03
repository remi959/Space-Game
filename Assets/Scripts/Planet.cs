using UnityEngine;

public class Planet : MonoBehaviour
{
    [Range(2, 256)]
    [SerializeField] private int resolution = 10;
    [SerializeField] private Material terrainMaterial;

    [SerializeField, HideInInspector]
    private MeshFilter[] meshFilters;
    private TerrainFace[] terrainFaces;

    private void OnValidate()
    {
        #if UNITY_EDITOR
        // Defer initialization to avoid SendMessage errors
        UnityEditor.EditorApplication.delayCall += OnValidateDeferred;
        #endif
    }

    void Initialize()
    {
        if (meshFilters == null || meshFilters.Length == 0) meshFilters = new MeshFilter[6];
        terrainFaces = new TerrainFace[6];

        Vector3[] directions = {
            Vector3.up,
            Vector3.down,
            Vector3.left,
            Vector3.right,
            Vector3.forward,
            Vector3.back
        };

        for (int i = 0; i < 6; i++)
        {
            if (meshFilters[i] == null)
            {
                GameObject meshObj = new("Terrain Face " + i); 
                meshObj.transform.parent = transform;

                meshObj.AddComponent<MeshRenderer>().sharedMaterial = terrainMaterial;
                meshFilters[i] = meshObj.AddComponent<MeshFilter>();
                meshFilters[i].sharedMesh = new Mesh();
            }

            terrainFaces[i] = new TerrainFace(meshFilters[i].sharedMesh, resolution, directions[i]);
        }
    }

    void GenerateMesh()
    {
        foreach (TerrainFace face in terrainFaces)
        {
            face.ConstructMesh();
        }
    }

    #if UNITY_EDITOR
    private void OnValidateDeferred()
    {
        // Unsubscribe to prevent multiple calls
        UnityEditor.EditorApplication.delayCall -= OnValidateDeferred;
        
        // Check if this object still exists (might have been deleted)
        if (this == null) return;
        
        Initialize();
        GenerateMesh();
    }
    #endif
}