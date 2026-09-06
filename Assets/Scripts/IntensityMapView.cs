using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace EarthquakeGame
{
    // A schematic "map" made of one small marker per prefecture, positioned
    // by projecting each prefecture's real lat/lon onto a fixed-size panel.
    // No map artwork is used - the dot positions alone roughly trace the
    // shape of Japan. Markers light up by intensity when an earthquake hits.
    public class IntensityMapView : MonoBehaviour
    {
        [Tooltip("Fixed-size panel the markers are positioned within.")]
        public RectTransform mapArea;

        public string dataResourcePath = "Data/prefectures";
        public float markerSize = 12f;
        public Color defaultColor = new Color(0.8f, 0.8f, 0.82f);

        private readonly Dictionary<string, Image> markers = new Dictionary<string, Image>();

        // Roughly covers mainland Japan (Hokkaido to Kyushu/Okinawa-adjacent).
        private const float LatMin = 30.5f, LatMax = 45.5f;
        private const float LonMin = 129f, LonMax = 145.5f;

        void Awake()
        {
            BuildMarkers();
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

            float w = mapArea.rect.width;
            float h = mapArea.rect.height;
            float tx = Mathf.InverseLerp(LonMin, LonMax, lon);
            float ty = Mathf.InverseLerp(LatMin, LatMax, lat);
            rt.anchoredPosition = new Vector2(tx * w - w * 0.5f, ty * h - h * 0.5f);

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
