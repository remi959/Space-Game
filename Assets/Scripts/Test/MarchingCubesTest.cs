using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class MarchingCubesTest : MonoBehaviour {
    public int gridSize = 32;
    public float cellSize = 0.5f;
    
    void Start() {
        // Create a simple sphere density field
        float[,,] density = new float[gridSize + 1, gridSize + 1, gridSize + 1];
        Vector3 center = new Vector3(gridSize / 2f, gridSize / 2f, gridSize / 2f) * cellSize;
        float radius = gridSize * cellSize * 0.4f;
        
        for (int x = 0; x <= gridSize; x++) {
            for (int y = 0; y <= gridSize; y++) {
                for (int z = 0; z <= gridSize; z++) {
                    Vector3 pos = new Vector3(x, y, z) * cellSize;
                    density[x, y, z] = Vector3.Distance(pos, center) - radius;
                }
            }
        }
        
        // Generate mesh
        MarchingCubes mc = new MarchingCubes();
        Mesh mesh = mc.GenerateMesh(density, cellSize);
        
        // Display
        GetComponent<MeshFilter>().mesh = mesh;
    }
}