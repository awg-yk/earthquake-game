using System;
using UnityEngine;

namespace EarthquakeGame
{
    // The in-game "fortune teller" (占い師). Not real earthquake prediction,
    // but here it's deliberately confident and precise (not vague) - every
    // forecastIntervalDays it peeks at future earthquake data and names the
    // exact prefecture, timing and intensity of the next big one (shindo 5
    // or stronger) within the following forecastWindowDays, anywhere in
    // Japan. This gives the player a real choice: move toward it for a
    // shot at a big score multiplier, or stay well away from it.
    public class FortuneTeller : MonoBehaviour
    {
        public EarthquakeManager earthquakeManager;

        [Tooltip("How many days ahead the forecast looks.")]
        public int forecastWindowDays = 10;

        [Tooltip("Minimum shindo (5=5弱) the forecast bothers announcing.")]
        public int minRank = 5;

        public string GetPeriodicForecast(DateTime currentDate)
        {
            var result = earthquakeManager.FindNextBigQuake(currentDate, forecastWindowDays, minRank);
            if (result == null)
            {
                return $"占い師：今後{forecastWindowDays}日間、大きな地震（震度5弱以上）の心配はなさそうです。";
            }

            var (daysUntil, prefecture, intensity) = result.Value;
            return $"占い師：{daysUntil}日後、{prefecture}で震度{intensity}の地震が起きるでしょう。";
        }
    }
}
