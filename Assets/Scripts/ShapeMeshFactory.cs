using UnityEngine;

namespace EarthquakeGame
{
    // Builds a simple flat-colored 2D mesh + matching Physics2D collider for
    // each BlockShape. No art assets required - everything is generated at
    // runtime so the block tower works without any imported sprites.
    public static class ShapeMeshFactory
    {
        public static void Apply(GameObject target, BlockShape shape, float size, Color color, bool addCollider = true)
        {
            var meshFilter = target.AddComponent<MeshFilter>();
            var meshRenderer = target.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            meshRenderer.sharedMaterial.color = color;

            switch (shape)
            {
                case BlockShape.Square:
                    meshFilter.mesh = BuildSquareMesh(size);
                    if (addCollider)
                    {
                        var box = target.AddComponent<BoxCollider2D>();
                        box.size = new Vector2(size, size);
                    }
                    break;

                case BlockShape.Triangle:
                    meshFilter.mesh = BuildTriangleMesh(size);
                    if (addCollider)
                    {
                        var poly = target.AddComponent<PolygonCollider2D>();
                        poly.points = GetTrianglePoints(size);
                    }
                    break;

                case BlockShape.Circle:
                    meshFilter.mesh = BuildCircleMesh(size * 0.5f, 24);
                    if (addCollider)
                    {
                        var circle = target.AddComponent<CircleCollider2D>();
                        circle.radius = size * 0.5f;
                    }
                    break;
            }
        }

        private static Mesh BuildSquareMesh(float size)
        {
            float h = size * 0.5f;
            var mesh = new Mesh();
            mesh.vertices = new[]
            {
                new Vector3(-h, -h, 0), new Vector3(h, -h, 0),
                new Vector3(h, h, 0), new Vector3(-h, h, 0)
            };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateNormals();
            return mesh;
        }

        private static Vector2[] GetTrianglePoints(float size)
        {
            float h = size * 0.5f;
            return new[]
            {
                new Vector2(0, h),
                new Vector2(h, -h),
                new Vector2(-h, -h)
            };
        }

        private static Mesh BuildTriangleMesh(float size)
        {
            var points = GetTrianglePoints(size);
            var mesh = new Mesh();
            mesh.vertices = new[]
            {
                new Vector3(points[0].x, points[0].y, 0),
                new Vector3(points[1].x, points[1].y, 0),
                new Vector3(points[2].x, points[2].y, 0)
            };
            mesh.triangles = new[] { 0, 1, 2 };
            mesh.RecalculateNormals();
            return mesh;
        }

        private static Mesh BuildCircleMesh(float radius, int segments)
        {
            var vertices = new Vector3[segments + 1];
            var triangles = new int[segments * 3];

            vertices[0] = Vector3.zero;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0);
            }

            for (int i = 0; i < segments; i++)
            {
                int a = i + 1;
                int b = (i + 1) % segments + 1;
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = a;
                triangles[i * 3 + 2] = b;
            }

            var mesh = new Mesh();
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            return mesh;
        }
    }
}
