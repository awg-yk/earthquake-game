using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace EarthquakeGame
{
    // A filled, arbitrary polygon rendered as a regular UI Graphic (so it
    // sits inside a Canvas like any other UI element). Used to draw a
    // simplified Japan landmass silhouette behind the prefecture markers,
    // without needing any imported map image/asset.
    [RequireComponent(typeof(CanvasRenderer))]
    public class UIPolygon : Graphic
    {
        public List<Vector2> points = new List<Vector2>();

        public void SetPoints(List<Vector2> newPoints)
        {
            points = newPoints;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (points.Count < 3) return;

            Vector2 centroid = Vector2.zero;
            foreach (var p in points) centroid += p;
            centroid /= points.Count;

            vh.AddVert(centroid, color, Vector2.zero);
            foreach (var p in points)
            {
                vh.AddVert(p, color, Vector2.zero);
            }

            for (int i = 0; i < points.Count; i++)
            {
                int a = 1 + i;
                int b = 1 + (i + 1) % points.Count;
                vh.AddTriangle(0, a, b);
            }
        }
    }
}
