using System;
using UnityEngine;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace REmind.Common.Systems
{
    /// <summary>
    /// 프로젝트 전역에서 재사용하는 모달 팝업 기반 클래스입니다.
    /// 파생 클래스는 팝업 화면 생성과 열릴 때의 데이터 갱신만 구현합니다.
    /// </summary>
    public abstract class PopupContext : MonoBehaviour
    {
        [SerializeField] private bool closeOnEscape = true;

        private GameObject popupRoot;

        public static bool HasOpenPopup => OpenPopupCount > 0;
        public static int OpenPopupCount { get; private set; }

        public bool IsOpen { get; private set; }

        public event Action Opening;
        public event Action Opened;
        public event Action Closed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            OpenPopupCount = 0;
        }

        protected virtual void Awake()
        {
            popupRoot = BuildPopup();

            if (!popupRoot)
            {
                Debug.LogError(
                    $"{GetType().Name} failed to build its popup.",
                    this);
                enabled = false;
                return;
            }

            popupRoot.SetActive(false);
        }

        private void Update()
        {
            if (IsOpen && closeOnEscape && WasEscapePressed())
            {
                Close();
            }
        }

        protected virtual void OnDisable()
        {
            Close();
        }

        protected virtual void OnDestroy()
        {
            if (IsOpen)
            {
                Close();
            }

            if (popupRoot)
            {
                Destroy(popupRoot);
            }
        }

        /// <summary>파생 팝업의 uGUI 루트를 생성해 반환합니다.</summary>
        protected abstract GameObject BuildPopup();

        /// <summary>팝업이 열린 직후 포커스를 받을 입력 요소입니다.</summary>
        protected virtual GameObject InitialSelection => null;

        protected virtual void OnOpening()
        {
        }

        protected virtual void OnOpened()
        {
        }

        protected virtual void OnClosed()
        {
        }

        public void Open()
        {
            if (IsOpen || !popupRoot)
            {
                return;
            }

            OnOpening();
            Opening?.Invoke();
            popupRoot.SetActive(true);
            popupRoot.transform.SetAsLastSibling();
            IsOpen = true;
            OpenPopupCount++;

            GameObject initialSelection = InitialSelection;

            if (EventSystem.current != null && initialSelection)
            {
                EventSystem.current.SetSelectedGameObject(initialSelection);
            }

            OnOpened();
            Opened?.Invoke();
        }

        public void Close()
        {
            if (!IsOpen)
            {
                if (popupRoot)
                {
                    popupRoot.SetActive(false);
                }

                return;
            }

            IsOpen = false;
            OpenPopupCount = Mathf.Max(0, OpenPopupCount - 1);

            if (popupRoot)
            {
                popupRoot.SetActive(false);
            }

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(gameObject);
            }

            OnClosed();
            Closed?.Invoke();
        }

        public void Toggle()
        {
            if (IsOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        private static bool WasEscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current?.escapeKey.wasPressedThisFrame == true;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Escape);
#else
            return false;
#endif
        }
    }
}
