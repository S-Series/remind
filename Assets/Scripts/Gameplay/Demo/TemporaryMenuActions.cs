using UnityEngine;

namespace REmind.Gameplay.Demo
{
    /// <summary>Navigation for the scene-authored temporary menu screens.</summary>
    [DisallowMultipleComponent]
    public sealed class TemporaryMenuActions : MonoBehaviour
    {
        public void ReturnHome() => AppRoot.NavigateToScene("Home");

        public void PlaySample() => AppRoot.NavigateToScene("Game");
    }
}
