using UnityEngine;
using UnityEngine.UI;

namespace CyberDefender.UI
{
    /// <summary>Кинорамка катсцены: чёрные полосы сверху и снизу, имя говорящего и субтитры.</summary>
    public sealed class CutsceneOverlay : MonoBehaviour
    {
        private const float TopBarHeight = 130f;
        private const float BottomBarHeight = 230f;
        private const string HiddenColorTag = "<color=#00000000>";

        [SerializeField] private float slideSpeed = 3f;

        private RectTransform topBar;
        private RectTransform bottomBar;
        private CanvasGroup textGroup;
        private Text speakerText;
        private Text subtitleText;
        private Text hintText;
        private float shown;
        private float target;

        private void Awake()
        {
            BuildUi();
            Apply();
        }

        public void Show(bool visible) => target = visible ? 1f : 0f;

        public void SetSpeaker(string speaker, Color color)
        {
            speakerText.text = speaker;
            speakerText.color = color;
        }

        /// <summary>
        /// Печатает строку посимвольно. Невидимый хвост остаётся в тексте прозрачным,
        /// поэтому переносы строк не «прыгают», пока слово допечатывается.
        /// </summary>
        public void SetSubtitle(string line, int visibleCharacters)
        {
            if (string.IsNullOrEmpty(line))
            {
                subtitleText.text = "";
                return;
            }

            visibleCharacters = Mathf.Clamp(visibleCharacters, 0, line.Length);
            subtitleText.text = visibleCharacters >= line.Length
                ? line
                : line.Substring(0, visibleCharacters) + HiddenColorTag + line.Substring(visibleCharacters) + "</color>";
        }

        private void Update()
        {
            if (Mathf.Approximately(shown, target))
                return;
            shown = Mathf.MoveTowards(shown, target, Time.unscaledDeltaTime * slideSpeed);
            Apply();
        }

        private void Apply()
        {
            float ease = shown * shown * (3f - 2f * shown);
            topBar.anchoredPosition = new Vector2(0f, (1f - ease) * TopBarHeight);
            bottomBar.anchoredPosition = new Vector2(0f, -(1f - ease) * BottomBarHeight);
            textGroup.alpha = Mathf.Clamp01(shown * 2f - 1f);
        }

        private void BuildUi()
        {
            RectTransform root = UiFactory.CreateCanvas(transform, "Cutscene Canvas", 20);

            topBar = UiFactory.CreateRect("Top Bar", root, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(4000f, TopBarHeight));
            UiFactory.AddImage(topBar, Color.black);
            bottomBar = UiFactory.CreateRect("Bottom Bar", root, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(4000f, BottomBarHeight));
            UiFactory.AddImage(bottomBar, Color.black);

            RectTransform texts = UiFactory.CreateStretched("Texts", root);
            textGroup = texts.gameObject.AddComponent<CanvasGroup>();
            textGroup.blocksRaycasts = false;
            textGroup.interactable = false;

            speakerText = UiFactory.CreateText(texts, "", 30, TextAnchor.LowerCenter, new Vector2(0.5f, 0f), new Vector2(0f, 160f), new Vector2(1500f, 44f), UiFactory.Danger);
            subtitleText = UiFactory.CreateText(texts, "", 40, TextAnchor.UpperCenter, new Vector2(0.5f, 0f), new Vector2(0f, 45f), new Vector2(1500f, 110f), Color.white);
            subtitleText.supportRichText = true;
            hintText = UiFactory.CreateText(texts, "Ладонь (J) — дальше    Кулак (S) — пропустить", 20, TextAnchor.LowerRight,
                new Vector2(1f, 0f), new Vector2(-40f, 12f), new Vector2(800f, 30f), UiFactory.Muted);
        }
    }
}
