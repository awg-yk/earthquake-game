using UnityEngine;

namespace EarthquakeGame
{
    // Builds simple flat-colored rectangle meshes (+ matching BoxCollider2D).
    // No art assets required - everything is generated at runtime.
    public static class ShapeMeshFactory
    {
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

        // A block reads as a solid tile rather than a flat rectangle: a dark
        // border (the collider-bearing body), a lighter inner face, and a
        // thin gloss bar along the top. Purely visual children, no colliders,
        // stacked on slightly nearer Z so they draw over the border.
        public static void ApplyBlock(GameObject target, Vector2 size, Color color, bool addCollider = true)
        {
            Apply(target, size, Shade(color, 0.55f), addCollider);

            float inset = Mathf.Min(size.x, size.y) * 0.22f;
            Vector2 faceSize = new Vector2(Mathf.Max(size.x - inset, size.x * 0.5f),
                                           Mathf.Max(size.y - inset, size.y * 0.5f));
            AddDecal(target, "Face", faceSize, color, -0.01f, 0f);

            Vector2 glossSize = new Vector2(faceSize.x * 0.82f, faceSize.y * 0.18f);
            AddDecal(target, "Gloss", glossSize, Shade(color, 1.35f, 0.5f), -0.02f, faceSize.y * 0.28f);
        }

        private static void AddDecal(GameObject parent, string name, Vector2 size, Color color, float z, float yOffset)
        {
            GameObject decal = new GameObject(name);
            decal.transform.SetParent(parent.transform, false);
            decal.transform.localPosition = new Vector3(0f, yOffset, z);
            Apply(decal, size, color, addCollider: false);
        }

        // Multiplies brightness while keeping the original alpha (optionally
        // overridden), so borders/highlights stay in the same color family.
        public static Color Shade(Color color, float factor, float alpha = -1f)
        {
            return new Color(
                Mathf.Clamp01(color.r * factor),
                Mathf.Clamp01(color.g * factor),
                Mathf.Clamp01(color.b * factor),
                alpha < 0f ? color.a : color.a * alpha);
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
