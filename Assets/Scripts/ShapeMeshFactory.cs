using UnityEngine;

namespace EarthquakeGame
{
    // Builds a simple flat-colored rectangle mesh + matching BoxCollider2D.
    // No art assets required - everything is generated at runtime.
    public static class ShapeMeshFactory
    {
        // width/height let BlockTowerManager randomize each block's aspect
        // ratio (long-and-thin vs. short-and-wide) instead of always a square.
        public static void Apply(GameObject target, Vector2 size, Color color, bool addCollider = true)
        {
            var meshFilter = target.AddComponent<MeshFilter>();
            var meshRenderer = target.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            meshRenderer.sharedMaterial.color = color;
            meshFilter.mesh = BuildRectangleMesh(size);

            if (addCollider)
            {
                var box = target.AddComponent<BoxCollider2D>();
                box.size = size;
            }
        }

        private static Mesh BuildRectangleMesh(Vector2 size)
        {
            float hw = size.x * 0.5f;
            float hh = size.y * 0.5f;
            var mesh = new Mesh();
            mesh.vertices = new[]
            {
                new Vector3(-hw, -hh, 0), new Vector3(hw, -hh, 0),
                new Vector3(hw, hh, 0), new Vector3(-hw, hh, 0)
            };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateNormals();
            return mesh;
        }
    }
}
