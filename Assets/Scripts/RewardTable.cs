using UnityEngine;

namespace EarthquakeGame
{
    // The economy of the construction-company game, in one place:
    //   報酬 = 高さ(段) × 現場のリスク倍率 × 耐震ボーナス × ScaleToMan
    //
    // Risk multiplier comes from the monthly forecast for the prefecture the
    // player is currently working in - nobody wants to build where the ground
    // shakes, so those contracts pay far better. The shindo bonus is the
    // building's own track record: a tower that stood through a shindo 6 is
    // worth several times one that never felt anything.
    public static class RewardTable
    {
        // Yen (in 万円) per point of height x multipliers.
        public const float ScaleToMan = 10f;

        private static readonly float[] RiskByRank =
        {
            1.0f, // 0: not in this month's forecast
            1.1f, // 1: 震度1
            1.3f, // 2: 震度2
            1.5f, // 3: 震度3
            1.8f, // 4: 震度4
            2.2f, // 5: 震度5弱
            2.6f, // 6: 震度5強
            3.0f, // 7: 震度6弱
            3.5f, // 8: 震度6強
            4.0f, // 9: 震度7
        };

        private static readonly float[] BonusByRank =
        {
            1.0f, // 0: 揺れを経験していない
            1.1f, // 1
            1.2f, // 2
            1.4f, // 3
            1.7f, // 4
            2.1f, // 5: 5弱
            2.5f, // 6: 5強
            3.0f, // 7: 6弱
            3.6f, // 8: 6強
            4.5f, // 9: 7
        };

        public static float RiskMultiplier(int forecastRank)
        {
            return RiskByRank[Mathf.Clamp(forecastRank, 0, RiskByRank.Length - 1)];
        }

        public static float ShindoBonus(int survivedRank)
        {
            return BonusByRank[Mathf.Clamp(survivedRank, 0, BonusByRank.Length - 1)];
        }

        public static int Reward(int heightInBlocks, int forecastRank, int survivedRank)
        {
            float value = heightInBlocks * RiskMultiplier(forecastRank) * ShindoBonus(survivedRank) * ScaleToMan;
            return Mathf.RoundToInt(value);
        }

        // "5弱" style label for a numeric rank, for the UI.
        public static string RankLabel(int rank)
        {
            switch (rank)
            {
                case 1: return "1";
                case 2: return "2";
                case 3: return "3";
                case 4: return "4";
                case 5: return "5弱";
                case 6: return "5強";
                case 7: return "6弱";
                case 8: return "6強";
                case 9: return "7";
                default: return "－";
            }
        }
    }
}
