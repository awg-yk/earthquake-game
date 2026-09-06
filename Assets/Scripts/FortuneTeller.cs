using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EarthquakeGame
{
    // The in-game "fortune teller" (占い師). Not real earthquake prediction,
    // but here it's deliberately confident and precise (not vague): on the
    // 1st of every month, it peeks at that entire month's future earthquake
    // data and names every prefecture that will feel shindo 2 or stronger
    // at some point during the month - shown directly on the intensity map.
    public class FortuneTeller : MonoBehaviour
    {
        public EarthquakeManager earthquakeManager;

        [Tooltip("Minimum shindo (2 = shindo 2) the monthly forecast bothers marking on the map.")]
        public int minRank = 2;

        public string LastForecastMessage { get; private set; } = "";
        public Dictionary<string, string> LastForecastIntensities { get; private set; } = new Dictionary<string, string>();

        // Called on the 1st of every in-game month. Scans the whole month
        // for every prefecture that will feel shindo >= minRank at least
        // once, and builds both a text summary and a map-ready dictionary.
        public string GetMonthlyForecast(DateTime currentDate)
        {
            var map = earthquakeManager.GetPrefecturesWithQuakesInMonth(currentDate.Year, currentDate.Month, minRank);
            LastForecastIntensities = map;

            LastForecastMessage = map.Count > 0
                ? $"占い師：{currentDate.Month}月は{string.Join("、", map.Keys.OrderByDescending(p => IntensityScale.ToRank(map[p])))}で震度2以上の揺れがあるでしょう。"
                : $"占い師：{currentDate.Month}月は大きな揺れはなさそうです。";

            return LastForecastMessage;
        }
    }
}
