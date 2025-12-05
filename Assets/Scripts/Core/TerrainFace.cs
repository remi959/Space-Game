using UnityEngine;

public class TerrainFace
{
    private ShapeGenerator shapeGenerator;
    private Mesh mesh;
    private int resolution;
    private Vector3 localUp;
    private Vector3 axisA;
    private Vector3 axisB;

    public TerrainFace(ShapeGenerator shapeGenerator, Mesh mesh, int resolution, Vector3 localUp)
    {
        this.mesh = mesh;
        this.resolution = resolution;
        this.localUp = localUp;
        this.shapeGenerator = shapeGenerator;

        axisA = new Vector3(localUp.y, localUp.z, localUp.x);
        axisB = Vector3.Cross(localUp, axisA);
    }

    public void ConstructMesh()
    {
        int quadsPerFace = (resolution - 1) * (resolution - 1);
        int trianglesPerQuad = 2;
        int verticesPerTriangle = 3;

        Vector3[] vertices = new Vector3[resolution * resolution];
        int[] triangles = new int[quadsPerFace * trianglesPerQuad * verticesPerTriangle];
        int triIndex = 0;

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int i = x + y * resolution;
                // Tells us how far along we are in the grid from 0 to 1
                Vector2 percent = new Vector2(x, y) / (resolution - 1);

                // Move one step in each axis direction from the localUp point
                Vector3 pointOnUnitCube = localUp + (percent.x - 0.5f) * 2 * axisA + (percent.y - 0.5f) * 2 * axisB;

                // Project onto unit sphere
                Vector3 pointOnUnitSphere = pointOnUnitCube.normalized;

                // Assign vertex position
                vertices[i] = shapeGenerator.CalculatePointOnPlanet(pointOnUnitSphere);

                // Check if we are not on the last row/column to avoid out of bounds triangles
                if (x != resolution - 1 && y != resolution - 1)
                {
                    // First triangle of the quad
                    triangles[triIndex] = i;                        // Top left
                    triangles[triIndex + 1] = i + resolution + 1;   // Bottom right
                    triangles[triIndex + 2] = i + resolution;       // Bottom left

                    // Second triangle of the quad
                    triangles[triIndex + 3] = i;                    // Top left
                    triangles[triIndex + 4] = i + 1;                // Top right
                    triangles[triIndex + 5] = i + resolution + 1;   // Bottom right

                    triIndex += 6;
                }
            }
        }

        // Clear mesh for resolution updates
        mesh.Clear();

        // Assign vertices and triangles to the mesh
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
    }
}