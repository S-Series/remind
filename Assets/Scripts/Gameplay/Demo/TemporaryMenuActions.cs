using UnityEngine;
using UnityEngine.SceneManagement;

namespace REmind.Gameplay.Demo
{
    /// <summary>Navigation for the scene-authored temporary menu screens.</summary>
    [DisallowMultipleComponent]
    public sealed class TemporaryMenuActions : MonoBehaviour
    {
        public void ReturnHome() => SceneManager.LoadScene("Home");

        public void PlaySample() => SceneManager.LoadScene("Game");
    }
}
