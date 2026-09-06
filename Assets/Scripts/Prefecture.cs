using System.Collections.Generic;

namespace EarthquakeGame
{
    // A single prefecture and the prefectures it is directly adjacent to.
    // Loaded from Assets/Data/prefectures.json so the adjacency map can be
    // edited without touching game code.
    [System.Serializable]
    public class Prefecture
    {
        public string name;
        public List<string> neighbors;
        public double lat;
        public double lon;
    }

    [System.Serializable]
    public class PrefectureList
    {
        public List<Prefecture> prefectures;
    }
}
