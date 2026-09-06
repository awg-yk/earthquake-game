using System.Collections.Generic;
using UnityEngine;

namespace EarthquakeGame
{
    // Tracks where the player is, which prefectures are adjacent (and
    // therefore reachable), and the 10-day movement cooldown.
    public class PlayerManager : MonoBehaviour
    {
        [Tooltip("Resources-relative path, without extension, to the prefecture adjacency JSON file.")]
        public string dataResourcePath = "Data/prefectures";

        [Tooltip("How many days must pass between moves.")]
        public int moveIntervalDays = 5;

        public string CurrentPrefecture { get; private set; }
        public int DaysUntilNextMove { get; private set; }

        private Dictionary<string, List<string>> adjacency = new Dictionary<string, List<string>>();
        private Dictionary<string, (double lat, double lon)> coordinates = new Dictionary<string, (double, double)>();

        public void LoadData()
        {
            adjacency.Clear();
            coordinates.Clear();

            TextAsset json = Resources.Load<TextAsset>(dataResourcePath);
            if (json == null)
            {
                Debug.LogError($"PlayerManager: could not find Resources/{dataResourcePath}.json");
                return;
            }

            var root = MiniJson.Deserialize(json.text) as Dictionary<string, object>;
            var list = (List<object>)root["prefectures"];
            foreach (var entryObj in list)
            {
                var entry = (Dictionary<string, object>)entryObj;
                string name = (string)entry["name"];
                var neighborsList = new List<string>();
                if (entry.TryGetValue("neighbors", out var neighborsObj))
                {
                    foreach (var n in (List<object>)neighborsObj)
                    {
                        neighborsList.Add((string)n);
                    }
                }
                adjacency[name] = neighborsList;

                double lat = entry.TryGetValue("lat", out var la) ? System.Convert.ToDouble(la) : 0.0;
                double lon = entry.TryGetValue("lon", out var lo) ? System.Convert.ToDouble(lo) : 0.0;
                coordinates[name] = (lat, lon);
            }
        }

        // Returns (0,0) for an unknown prefecture name.
        public (double lat, double lon) GetCoordinates(string prefectureName)
        {
            return coordinates.TryGetValue(prefectureName, out var coord) ? coord : (0.0, 0.0);
        }

        public IEnumerable<string> AllPrefectureNames => adjacency.Keys;

        public void StartAt(string prefectureName)
        {
            CurrentPrefecture = prefectureName;
            DaysUntilNextMove = moveIntervalDays;
        }

        public List<string> GetNeighbors(string prefectureName)
        {
            return adjacency.TryGetValue(prefectureName, out var list) ? list : new List<string>();
        }

        public bool CanMoveNow => DaysUntilNextMove <= 0;

        // Call once per day, after the day's earthquake check.
        public void AdvanceOneDay()
        {
            if (DaysUntilNextMove > 0) DaysUntilNextMove--;
        }

        // Returns true and moves the player if the destination is adjacent
        // and the movement cooldown has elapsed; otherwise leaves state untouched.
        public bool TryMoveTo(string destinationPrefecture)
        {
            if (!CanMoveNow) return false;
            var neighbors = GetNeighbors(CurrentPrefecture);
            if (!neighbors.Contains(destinationPrefecture)) return false;

            CurrentPrefecture = destinationPrefecture;
            DaysUntilNextMove = moveIntervalDays;
            return true;
        }
    }
}
