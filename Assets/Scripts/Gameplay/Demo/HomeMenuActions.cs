using UnityEngine;
using UnityEngine.SceneManagement;

namespace REmind.Gameplay.Demo
{
    /// <summary>Actions for the scene-authored Home buttons.</summary>
    [DisallowMultipleComponent]
    public sealed class HomeMenuActions : MonoBehaviour
    {
        public void OpenStory() => SceneManager.LoadScene("Story");

        public void OpenMusic() => SceneManager.LoadScene("MusicSelect");

        public void OpenCharacter() => SceneManager.LoadScene("Character");

        public void OpenReMind() => SceneManager.LoadScene("ReMind");

        public void OpenOption() => SceneManager.LoadScene("Option");

        public void OpenSettings() => SceneManager.LoadScene("Settings");

        public void ExitGame() => Application.Quit();
    }
}
