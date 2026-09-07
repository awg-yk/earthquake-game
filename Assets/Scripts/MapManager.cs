using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace EarthquakeGame
{
    // Shows only the player's current prefecture and its direct neighbors
    // (rebuilt every time the player moves), instead of all 46 at once.
    // This intentionally skips a hand-drawn/geographic map image - the
    // spec asks to prioritize the game system over visuals for now.
    public class MapManager : MonoBehaviour
    {
        [Tooltip("Prefab with a Button + Text/TMP child, used for each prefecture.")]
        public Button prefectureButtonPrefab;

        [Tooltip("Parent transform (e.g. a Grid Layout Group) that buttons are instantiated under.")]
        public Transform buttonContainer;

        [Tooltip("Whole panel (frame + header) to show/hide with the list. Falls back to buttonContainer when unset.")]
        public GameObject panelRoot;

        // Deep green, so the button's white label stays readable on it.
        public Color reachableColor = new Color(0.18f, 0.49f, 0.32f);

        private GameManager gameManager;

        public void Init(GameManager owner, IEnumerable<string> allPrefectures)
        {
            gameManager = owner;
            // allPrefectures is no longer used to pre-build every button -
            // Refresh() builds only the current + neighboring prefectures.
        }

        public void Refresh(string currentPrefecture, List<string> neighbors, bool canMove)
        {
            foreach (Transform child in buttonContainer)
            {
                Destroy(child.gameObject);
            }

            // Only shown once movement actually becomes available, instead
            // of always displaying a list of not-yet-reachable prefectures.
            GameObject toggled = panelRoot != null ? panelRoot : buttonContainer.gameObject;
            toggled.SetActive(canMove);
            if (!canMove) return;

            foreach (var neighbor in neighbors)
            {
                CreateButton(neighbor);
            }
        }

        private void CreateButton(string prefectureName)
        {
            Button btn = Instantiate(prefectureButtonPrefab, buttonContainer);
            btn.gameObject.SetActive(true);
            btn.interactable = true;

            var label = btn.GetComponentInChildren<Text>();
            if (label != null) label.text = prefectureName;

            var image = btn.GetComponent<Image>();
            if (image != null) image.color = reachableColor;

            string captured = prefectureName;
            btn.onClick.AddListener(() => gameManager.OnPrefectureClicked(captured));
        }
    }
}
