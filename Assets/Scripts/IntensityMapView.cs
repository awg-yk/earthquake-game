using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace EarthquakeGame
{
    // A schematic "map" of Japan: a few simplified landmass silhouettes
    // (Hokkaido / Honshu / Shikoku / Kyushu, hand-approximated - not real
    // survey data) drawn as filled UI polygons, with one small marker per
    // prefecture positioned by projecting its real lat/lon onto the same
    // panel. No map image asset is used. Markers light up by intensity
    // when an earthquake hits.
    public class IntensityMapView : MonoBehaviour
    {
        [Tooltip("Fixed-size panel the landmass and markers are positioned within.")]
        public RectTransform mapArea;

        public string dataResourcePath = "Data/prefectures";
        public float markerSize = 11f;
        public Color defaultColor = new Color(0.8f, 0.8f, 0.82f);
        public Color landColor = new Color(0.62f, 0.78f, 0.6f);
        public Color seaColor = new Color(0.72f, 0.83f, 0.92f);

        private readonly Dictionary<string, Image> markers = new Dictionary<string, Image>();

        // Roughly covers mainland Japan (Hokkaido to Kyushu).
        private const float LatMin = 30.5f, LatMax = 45.7f;
        private const float LonMin = 129f, LonMax = 145.8f;

        // Hand-simplified island outlines as (longitude, latitude) points.
        // These are rough approximations for a schematic map, not survey data.
        private static readonly float[][] HokkaidoOutline =
        {
            new[] { 141.0f, 45.5f }, new[] { 143.2f, 44.4f }, new[] { 145.3f, 43.6f },
            new[] { 145.0f, 42.9f }, new[] { 144.0f, 42.0f }, new[] { 142.4f, 41.5f },
            new[] { 140.4f, 41.7f }, new[] { 139.8f, 42.8f }, new[] { 140.0f, 44.2f },
            new[] { 140.3f, 45.2f },
        };

        private static readonly float[][] HonshuOutline =
        {
            new[] { 140.9f, 41.5f }, new[] { 141.9f, 40.6f }, new[] { 141.6f, 39.4f },
            new[] { 141.9f, 38.3f }, new[] { 140.9f, 37.3f }, new[] { 140.7f, 36.2f },
            new[] { 140.9f, 35.7f }, new[] { 139.9f, 35.2f }, new[] { 138.9f, 34.6f },
            new[] { 137.6f, 34.6f }, new[] { 136.9f, 34.6f }, new[] { 135.4f, 33.5f },
            new[] { 133.6f, 33.5f }, new[] { 131.5f, 34.0f }, new[] { 131.5f, 34.6f },
            new[] { 132.5f, 35.5f }, new[] { 134.2f, 35.5f }, new[] { 135.8f, 35.6f },
            new[] { 137.0f, 36.8f }, new[] { 137.4f, 37.4f }, new[] { 138.6f, 37.9f },
            new[] { 139.9f, 39.2f }, new[] { 140.0f, 40.0f }, new[] { 140.3f, 40.8f },
        };

        private static readonly float[][] ShikokuOutline =
        {
            new[] { 132.4f, 33.9f }, new[] { 132.9f, 34.3f }, new[] { 133.9f, 34.2f },
            new[] { 134.7f, 34.2f }, new[] { 134.6f, 33.7f }, new[] { 133.3f, 33.2f },
            new[] { 132.5f, 33.4f },
        };

        private static readonly float[][] KyushuOutline =
        {
            new[] { 130.0f, 34.0f }, new[] { 131.2f, 33.9f }, new[] { 131.9f, 33.5f },
            new[] { 131.7f, 32.4f }, new[] { 131.0f, 31.2f }, new[] { 130.5f, 31.0f },
            new[] { 129.7f, 31.4f }, new[] { 129.4f, 32.7f }, new[] { 129.9f, 33.3f },
        };

        void Awake()
        {
            BuildSea();
            BuildLandmass(HokkaidoOutline);
            BuildLandmass(HonshuOutline);
            BuildLandmass(ShikokuOutline);
            BuildLandmass(KyushuOutline);
            BuildMarkers();
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

        private void BuildLandmass(float[][] outline)
        {
            GameObject obj = new GameObject("Landmass");
            RectTransform rt = obj.AddComponent<RectTransform>();
            rt.SetParent(mapArea, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = mapArea.rect.size;

            var polygon = obj.AddComponent<UIPolygon>();
            polygon.color = landColor;

            var points = new List<Vector2>(outline.Length);
            foreach (var p in outline) points.Add(Project(p[0], p[1]));
            polygon.SetPoints(points);
        }

        private void BuildMarkers()
        {
            if (mapArea == null) return;

            TextAsset json = Resources.Load<TextAsset>(dataResourcePath);
            if (json == null)
            {
                Debug.LogError($"IntensityMapView: could not find Resources/{dataResourcePath}.json");
                return;
            }

            var root = MiniJson.Deserialize(json.text) as Dictionary<string, object>;
            var list = (List<object>)root["prefectures"];

            foreach (var entryObj in list)
            {
                var entry = (Dictionary<string, object>)entryObj;
                string name = (string)entry["name"];
                float lat = entry.TryGetValue("lat", out var la) ? (float)Convert.ToDouble(la) : 0f;
                float lon = entry.TryGetValue("lon", out var lo) ? (float)Convert.ToDouble(lo) : 0f;
                CreateMarker(name, lat, lon);
            }
        }

        private void CreateMarker(string prefectureName, float lat, float lon)
        {
            GameObject obj = new GameObject($"Marker_{prefectureName}");
            var rt = obj.AddComponent<RectTransform>();
            rt.SetParent(mapArea, false);
            rt.sizeDelta = new Vector2(markerSize, markerSize);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Project(lon, lat);

            var img = obj.AddComponent<Image>();
            img.color = defaultColor;
            markers[prefectureName] = img;
        }

        // Colors each affected prefecture's marker by intensity; everything
        // else fades back to the default gray.
        public void SetIntensities(Dictionary<string, string> intensities)
        {
            ClearAll();
            foreach (var kv in intensities)
            {
                if (!markers.TryGetValue(kv.Key, out var img)) continue;
                if (ColorUtility.TryParseHtmlString("#" + IntensityScale.GetColorHex(kv.Value), out var color))
                {
                    img.color = color;
                }
            }
        }

        public void ClearAll()
        {
            foreach (var kv in markers)
            {
                kv.Value.color = defaultColor;
            }
        }
    }
}
