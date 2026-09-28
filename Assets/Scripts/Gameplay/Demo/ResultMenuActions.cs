using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
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

        public void Retry() => SceneManager.LoadScene("Game");
        public void Next() => SceneManager.LoadScene("MusicSelect");
        public void MusicSelect() => SceneManager.LoadScene("MusicSelect");
    }
}
