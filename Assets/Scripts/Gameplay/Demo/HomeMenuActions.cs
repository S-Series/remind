using UnityEngine;
using REmind.Common.UI;

namespace REmind.Gameplay.Demo
{
    /// <summary>Actions for the scene-authored Home buttons.</summary>
    [DisallowMultipleComponent]
    public sealed class HomeMenuActions : MonoBehaviour
    {
        [SerializeField] private MenuNavigationController navigation;
        public void OpenStory() => AppRoot.NavigateToScene("Story");

        public void OpenMusic() => AppRoot.NavigateToScene("MusicSelect");

        public void OpenCharacter()
        {
            if (!AppRoot.Current || !AppRoot.Current.TryOpenCharacter(navigation))
                Debug.LogError("Home Character overlay is unavailable.", this);
        }

        public void OpenReMind() => AppRoot.NavigateToScene("ReMind");

        public void OpenOption() => AppRoot.NavigateToScene("Option");

        public void OpenSettings()
        {
            if (!AppRoot.Current || !AppRoot.Current.TryOpenSettings(navigation))
            {
                Debug.LogError("Home Settings overlay is unavailable.", this);
            }
        }

        public void ExitGame() => Application.Quit();
    }
}
