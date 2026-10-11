using System;
using REmind.Common.UI;
using REmind.Gameplay.Characters;
using REmind.Gameplay.Presentation;
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
        [SerializeField] private OverlayPresentation presentation;
        [SerializeField] private GameObject[] tabPages;
        [SerializeField] private Image[] tabBackgrounds;
        [SerializeField] private TMP_Text[] tabLabels;
        [SerializeField] private Sprite selectedTabSprite;
        [SerializeField] private Sprite idleTabSprite;
        [Header("Character Data")]
        [SerializeField] private Button[] rosterCardButtons;
        [SerializeField] private TMP_Text[] rosterCaptions;
        [SerializeField] private GameObject[] rosterLockOverlays;
        [SerializeField] private TMP_Text characterName;
        [SerializeField] private TMP_Text englishName;
        [SerializeField] private TMP_Text profileText;
        [SerializeField] private RawImage portrait;
        [SerializeField] private TMP_Text[] statLabels;
        [SerializeField] private TMP_Text[] statValues;
        [SerializeField] private Image[] statTracks;
        [SerializeField] private Image[] statFills;
        [SerializeField] private TMP_Text gaugeLabel;
        [SerializeField] private TMP_Text skillName;
        [SerializeField] private TMP_Text skillDescription;
        [SerializeField] private TMP_Text equipStatus;
        [SerializeField] private TMP_Text errorMessage;
        [SerializeField] private Button beginButton;

        private MenuNavigationController navigation;
        private string previewCharacterId;
        private static readonly CharacterStatType[] DisplayStats =
        {
            CharacterStatType.WorldUnlock,
            CharacterStatType.RegularCurrency,
            CharacterStatType.HiddenUnlock,
            CharacterStatType.Reserved
        };
        private static readonly string[] StatNames =
        { "월드 해금", "통상 재화", "히든 해금", "미정" };

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
            if (!presentation) presentation = GetComponent<OverlayPresentation>();
            if (!presentation) return;
            navigation = hostNavigation;
            scope.BindController(hostNavigation);
            gameObject.SetActive(true);
            previewCharacterId = AppRoot.Current
                ? AppRoot.Current.EquippedCharacterId : null;
            if (errorMessage) errorMessage.text = string.Empty;
            RefreshCharacter();
            SelectTab(0);
            hostNavigation.Push(scope);
            presentation.PlayOpen();
        }

        /// <summary>Roster choice changes only the preview until Begin succeeds.</summary>
        public void SelectRosterIndex(int index)
        {
            if (presentation && (presentation.IsOpening ||
                presentation.IsClosing)) return;
            if (!AppRoot.Current || !AppRoot.Current.TryGetCharacterEntryAt(
                index, out CharacterDefinitionAsset entry)) return;
            previewCharacterId = entry.CharacterId;
            if (errorMessage) errorMessage.text = string.Empty;
            RefreshCharacter();
        }

        public void SelectTab(int index)
        {
            if (presentation && (presentation.IsOpening ||
                presentation.IsClosing)) return;
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

        public void Begin()
        {
            if (!IsOverlayOpen || !presentation ||
                !presentation.IsInteractive) return;
            string error = null;
            if (!AppRoot.Current ||
                !AppRoot.Current.TryEquipCharacter(previewCharacterId,
                    out error))
            {
                if (errorMessage) errorMessage.text =
                    string.IsNullOrWhiteSpace(error)
                        ? "캐릭터를 장착할 수 없습니다." : error;
                RefreshCharacter();
                return;
            }
            CloseOverlay();
        }

        private void RefreshCharacter()
        {
            AppRoot app = AppRoot.Current;
            int cardCount = rosterCardButtons?.Length ?? 0;
            for (int i = 0; i < cardCount; i++)
            {
                CharacterDefinitionAsset entry = null;
                bool available = app && app.TryGetCharacterEntryAt(i,
                    out entry);
                if (rosterCardButtons[i])
                    rosterCardButtons[i].interactable = available;
                if (rosterCaptions != null && i < rosterCaptions.Length &&
                    rosterCaptions[i])
                    rosterCaptions[i].text = available
                        ? entry.EnglishName : "???";
                if (rosterLockOverlays != null &&
                    i < rosterLockOverlays.Length &&
                    rosterLockOverlays[i])
                    rosterLockOverlays[i].SetActive(!available ||
                        !app.IsCharacterOwned(entry.CharacterId));
            }

            if (!app || !app.TryGetCharacterEntry(previewCharacterId,
                out CharacterDefinitionAsset selected) ||
                !app.TryGetCharacterAbility(previewCharacterId,
                    out CharacterAbilityDefinition ability,
                    out GameRuleConfig gaugeRule))
            {
                if (characterName) characterName.text = "-";
                if (englishName) englishName.text = "-";
                if (profileText) profileText.text = string.Empty;
                if (portrait) portrait.texture = null;
                if (gaugeLabel) gaugeLabel.text = "-";
                if (skillName) skillName.text = "-";
                if (skillDescription) skillDescription.text = string.Empty;
                for (int i = 0; i < DisplayStats.Length; i++)
                {
                    if (statLabels != null && i < statLabels.Length &&
                        statLabels[i]) statLabels[i].text = StatNames[i];
                    if (statValues != null && i < statValues.Length &&
                        statValues[i]) statValues[i].text = "-";
                    if (statFills != null && i < statFills.Length &&
                        statFills[i]) statFills[i].enabled = false;
                }
                if (beginButton) beginButton.interactable = false;
                if (equipStatus) equipStatus.text = "선택할 캐릭터가 없습니다";
                if (errorMessage && string.IsNullOrEmpty(errorMessage.text))
                    errorMessage.text = app &&
                        !string.IsNullOrEmpty(app.PlayerDataError)
                            ? app.PlayerDataError
                            : "캐릭터 데이터를 확인할 수 없습니다.";
                return;
            }

            if (characterName) characterName.text = selected.DisplayName;
            if (englishName) englishName.text = selected.EnglishName;
            if (profileText) profileText.text = selected.ProfileText;
            if (portrait)
            {
                portrait.texture = selected.PortraitTexture;
                portrait.uvRect = selected.PortraitUv;
            }
            if (gaugeLabel)
                gaugeLabel.text = $"게이지 {gaugeRule.InitialHealth} / {gaugeRule.MaxHealth}";
            if (skillName) skillName.text = "스킬 없음";
            if (skillDescription)
                skillDescription.text = "장착 시 별도 스킬 효과가 없습니다.";
            for (int i = 0; i < DisplayStats.Length; i++)
                RefreshStat(i, ability.Stats);

            bool owned = app.IsCharacterOwned(previewCharacterId);
            if (beginButton) beginButton.interactable = owned;
            if (equipStatus)
                equipStatus.text = !owned ? "미해금" :
                    string.Equals(app.EquippedCharacterId,
                        previewCharacterId, StringComparison.Ordinal)
                        ? "장착 중" : "장착 가능";
        }

        private void RefreshStat(int index, CharacterAbilityStats stats)
        {
            if (statLabels != null && index < statLabels.Length &&
                statLabels[index]) statLabels[index].text = StatNames[index];
            bool defined = stats.TryGetRawValue(DisplayStats[index],
                out int raw);
            if (statValues != null && index < statValues.Length &&
                statValues[index])
            {
                if (defined && stats.TryGetMultiplier(DisplayStats[index],
                    out decimal multiplier))
                    statValues[index].text = raw + " / " +
                        multiplier.ToString("P0");
                else
                    statValues[index].text = "-";
            }
            if (statFills == null || index >= statFills.Length ||
                !statFills[index]) return;
            Image fill = statFills[index];
            if (statTracks == null || index >= statTracks.Length ||
                !statTracks[index] || !defined)
            {
                fill.enabled = false;
                return;
            }
            fill.enabled = true;
            RectTransform track = statTracks[index].rectTransform;
            RectTransform fillRect = fill.rectTransform;
            float width = track.rect.width * Mathf.Clamp01(raw / 100f);
            fillRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
                width);
            Vector2 position = fillRect.anchoredPosition;
            position.x = track.anchoredPosition.x - track.rect.width / 2f +
                width / 2f;
            fillRect.anchoredPosition = position;
        }

        public void CloseOverlay()
        {
            if (!IsOverlayOpen || !presentation ||
                presentation.IsClosing) return;
            scope.SetNavigationActive(false);
            presentation.PlayClose(CompleteClose);
        }

        private void CompleteClose()
        {
            ReleaseNavigation();
            gameObject.SetActive(false);
        }

        private void OnActiveSceneChanged(Scene previous, Scene next)
        {
            if (!gameObject.activeSelf) return;
            presentation?.ForceHide();
            CompleteClose();
        }

        private void ReleaseNavigation()
        {
            if (navigation && navigation.ActiveScope == scope)
                navigation.Pop();
            scope?.BindController(null);
            navigation = null;
        }
    }
}
