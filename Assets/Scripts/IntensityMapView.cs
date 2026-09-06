using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace EarthquakeGame
{
    // A schematic but geographically real "map" of Japan: each prefecture
    // is drawn as its own filled polygon (simplified from actual public
    // boundary data, see tools/ for the source), positioned by projecting
    // real lon/lat onto a fixed-size panel. No map image asset is used -
    // every shape is generated at runtime. Prefectures light up by
    // intensity when an earthquake hits.
    public class IntensityMapView : MonoBehaviour
    {
        [Tooltip("Fixed-size panel the map is drawn within.")]
        public RectTransform mapArea;

        public string shapesResourcePath = "Data/prefecture_shapes";
        public Color defaultColor = new Color(0.75f, 0.78f, 0.72f);
        public Color seaColor = new Color(0.72f, 0.83f, 0.92f);
        public Color outlineColor = new Color(0.35f, 0.4f, 0.35f);

        private readonly Dictionary<string, UIPolygon> prefecturePolygons = new Dictionary<string, UIPolygon>();

        // Roughly covers mainland Japan (Hokkaido to Kyushu).
        private const float LatMin = 30.5f, LatMax = 45.7f;
        private const float LonMin = 129f, LonMax = 145.8f;

        void Awake()
        {
            BuildSea();
            BuildPrefectureShapes();
        }

        private Vector2 Project(float lon, float lat)
        {
            float w = mapArea.rect.width;
            float h = mapArea.rect.height;
            float tx = Mathf.InverseLerp(LonMin, LonMax, lon);
            float ty = Mathf.InverseLerp(LatMin, LatMax, lat);
            return new Vector2(tx * w - w * 0.5f, ty * h - h * 0.5f);
        }

        private void BuildSea()
        {
            GameObject obj = new GameObject("Sea");
            RectTransform rt = obj.AddComponent<RectTransform>();
            rt.SetParent(mapArea, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            Image img = obj.AddComponent<Image>();
            img.color = seaColor;
        }

        private void BuildPrefectureShapes()
        {
            if (mapArea == null) return;

            TextAsset json = Resources.Load<TextAsset>(shapesResourcePath);
            if (json == null)
            {
                Debug.LogError($"IntensityMapView: could not find Resources/{shapesResourcePath}.json");
                return;
            }

            var root = MiniJson.Deserialize(json.text) as Dictionary<string, object>;
            var list = (List<object>)root["shapes"];

            foreach (var entryObj in list)
            {
                var entry = (Dictionary<string, object>)entryObj;
                string name = (string)entry["name"];
                var pointsRaw = (List<object>)entry["points"];

                var points = new List<Vector2>(pointsRaw.Count);
                foreach (var pRaw in pointsRaw)
                {
                    var pair = (List<object>)pRaw;
                    float lon = (float)Convert.ToDouble(pair[0]);
                    float lat = (float)Convert.ToDouble(pair[1]);
                    points.Add(Project(lon, lat));
                }

                CreatePrefectureShape(name, points);
            }
        }

        private void CreatePrefectureShape(string prefectureName, List<Vector2> points)
        {
            GameObject obj = new GameObject($"Pref_{prefectureName}");
            RectTransform rt = obj.AddComponent<RectTransform>();
            rt.SetParent(mapArea, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = mapArea.rect.size;

            var polygon = obj.AddComponent<UIPolygon>();
            polygon.color = defaultColor;
            polygon.SetPoints(points);

            prefecturePolygons[prefectureName] = polygon;
        }

        // Colors each affected prefecture's shape by intensity; everything
        // else fades back to the default color.
        public void SetIntensities(Dictionary<string, string> intensities)
        {
            ClearAll();
            foreach (var kv in intensities)
            {
                if (!prefecturePolygons.TryGetValue(kv.Key, out var polygon)) continue;
                if (ColorUtility.TryParseHtmlString("#" + IntensityScale.GetColorHex(kv.Value), out var color))
                {
                    polygon.color = color;
                }
            }
        }

        public void ClearAll()
        {
            foreach (var kv in prefecturePolygons)
            {
                kv.Value.color = defaultColor;
            }
        }
    }
}
