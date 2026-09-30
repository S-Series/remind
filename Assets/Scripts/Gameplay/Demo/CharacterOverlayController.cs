using REmind.Common.UI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace REmind.Gameplay.Demo
{
    /// <summary>Presentation and navigation for the persistent Character overlay.</summary>
    [DisallowMultipleComponent]
    public sealed class CharacterOverlayController : MonoBehaviour
    {
        [SerializeField] private NavigationScope scope;
        [SerializeField] private GameObject[] tabPages;
        [SerializeField] private Image[] tabBackgrounds;
        [SerializeField] private TMP_Text[] tabLabels;
        [SerializeField] private Sprite selectedTabSprite;
        [SerializeField] private Sprite idleTabSprite;

        private MenuNavigationController navigation;

        public bool IsOverlayOpen => gameObject.activeInHierarchy &&
            navigation && navigation.ActiveScope == scope;

        private void OnEnable() =>
            SceneManager.activeSceneChanged += OnActiveSceneChanged;

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            ReleaseNavigation();
        }

        public void OpenOverlay(MenuNavigationController hostNavigation)
        {
            if (!hostNavigation || !scope || IsOverlayOpen) return;
            navigation = hostNavigation;
            scope.BindController(hostNavigation);
            gameObject.SetActive(true);
            SelectTab(0);
            hostNavigation.Push(scope);
        }

        public void SelectTab(int index)
        {
            if (tabPages == null || index < 0 || index >= tabPages.Length)
                return;
            for (int i = 0; i < tabPages.Length; i++)
            {
                if (tabPages[i]) tabPages[i].SetActive(i == index);
                if (tabBackgrounds != null && i < tabBackgrounds.Length &&
                    tabBackgrounds[i])
                {
                    tabBackgrounds[i].sprite = i == index
                        ? selectedTabSprite : idleTabSprite;
                    tabBackgrounds[i].color = Color.white;
                }
                if (tabLabels != null && i < tabLabels.Length &&
                    tabLabels[i])
                    tabLabels[i].color = i == index
                        ? new Color(0.15f, 0.23f, 0.52f)
                        : new Color(0.95f, 0.96f, 1f);
            }
        }

        public void Begin() => CloseOverlay();

        public void CloseOverlay()
        {
            ReleaseNavigation();
            gameObject.SetActive(false);
        }

        private void OnActiveSceneChanged(Scene previous, Scene next) =>
            CloseOverlay();

        private void ReleaseNavigation()
        {
            if (navigation && navigation.ActiveScope == scope)
                navigation.Pop();
            scope?.BindController(null);
            navigation = null;
        }
    }
}
