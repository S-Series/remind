using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace REmind.Gameplay.Demo
{
    /// <summary>Navigation for the standalone Result screen.</summary>
    public sealed class ResultMenuActions : MonoBehaviour
    {
        [SerializeField] private Button initialSelection;

        public void SetInitialSelection(Button button) => initialSelection = button;

        private void Start()
        {
            if (initialSelection && EventSystem.current)
                EventSystem.current.SetSelectedGameObject(initialSelection.gameObject);
        }

        public void Retry() => AppRoot.NavigateToScene("Game");
        public void Next() => AppRoot.NavigateToScene("MusicSelect");
        public void MusicSelect() => AppRoot.NavigateToScene("MusicSelect");
    }
}
