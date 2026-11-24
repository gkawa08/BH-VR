using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class TutorialController : MonoBehaviour
{
    private enum TutorialLanguage
    {
        English,
        Japanese
    }

    [Serializable]
    private class TutorialPage
    {
        [SerializeField] private string title;

        [SerializeField, TextArea(4, 12)] private string body;

        [SerializeField, Min(1)] private int titleFontSize = 36;

        [SerializeField, Min(1)] private int bodyFontSize = 20;

        [Tooltip("Font size for the page-specific visual text. This controls the image credit on page 1 and the black-hole legend labels on page 2.")]
        [SerializeField, Min(1)] private int visualTextFontSize = 18;

        [Tooltip("Exact, case-sensitive words or phrases to show in bold. You can also use <b>text</b> directly in Title or Body.")]
        [SerializeField] private string[] boldTerms = Array.Empty<string>();

        public string Title => ApplyBoldTerms(title, boldTerms);
        public string Body => ApplyBoldTerms(body, boldTerms);
        public int TitleFontSize => titleFontSize > 0 ? titleFontSize : 36;
        public int BodyFontSize => bodyFontSize > 0 ? bodyFontSize : 20;
        public int VisualTextFontSize => visualTextFontSize > 0 ? visualTextFontSize : 18;

        public TutorialPage(string title, string body, params string[] boldTerms)
        {
            this.title = title;
            this.body = body;
            this.boldTerms = boldTerms ?? Array.Empty<string>();
        }
    }

    [Serializable]
    private class BlackHoleLegendText
    {
        [SerializeField] private string defaultBlackHole;
        [SerializeField] private string binaryMembers;
        [SerializeField] private string interloperBlackHole;
        [SerializeField] private string mergerRemnant;

        public string DefaultBlackHole => defaultBlackHole;
        public string BinaryMembers => binaryMembers;
        public string InterloperBlackHole => interloperBlackHole;
        public string MergerRemnant => mergerRemnant;

        public BlackHoleLegendText(
            string defaultBlackHole,
            string binaryMembers,
            string interloperBlackHole,
            string mergerRemnant)
        {
            this.defaultBlackHole = defaultBlackHole;
            this.binaryMembers = binaryMembers;
            this.interloperBlackHole = interloperBlackHole;
            this.mergerRemnant = mergerRemnant;
        }
    }

    [Serializable]
    private class NavigationText
    {
        [SerializeField] private string previous;
        [SerializeField] private string next;
        [SerializeField] private string finish;
        [SerializeField] private string close;

        public string Previous => previous;
        public string Next => next;
        public string Finish => finish;
        public string Close => close;

        public NavigationText(string previous, string next, string finish, string close)
        {
            this.previous = previous;
            this.next = next;
            this.finish = finish;
            this.close = close;
        }
    }

    [Header("Existing UI")]
    [SerializeField] private GameObject controlPanel;
    [SerializeField] private GameObject tutorialPanel;
    [SerializeField] private Text titleText;
    [SerializeField] private Text bodyText;
    [SerializeField] private Text pageText;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button closeButton;

    [Header("Optional UI")]
    [SerializeField] private Button helpButton;
    [SerializeField] private Text previousButtonText;
    [SerializeField] private Text nextButtonText;
    [SerializeField] private Text closeButtonText;

    [Header("Language")]
    [SerializeField] private Button englishButton;
    [SerializeField] private Button japaneseButton;
    [SerializeField] private TutorialLanguage defaultLanguage = TutorialLanguage.English;

    [Header("Tutorial Content - English")]
    [SerializeField] private TutorialPage englishSciencePage = new TutorialPage(
        "Welcome to BH-VR",
        "Globular clusters are dense, ancient systems containing hundreds of thousands to millions of stars. Their black holes move toward the center, where close encounters can form binaries, exchange companions, and lead to mergers.\n\nBH-VR turns an N-body simulation into an interactive 3D view. Explore black-hole motion and inspect MERGE and EXCHANGE events.",
        "Globular clusters",
        "BH-VR",
        "MERGE",
        "EXCHANGE"
    );
    [SerializeField] private TutorialPage englishControlsPage = new TutorialPage(
        "How to use BH-VR",
        "Play / Timeline — Start, pause, or inspect a snapshot.\nEvent — Jump to just before a MERGE or EXCHANGE.\nCoM / Raw — Switch the reference frame.\nSpeed / Size — Adjust playback speed and BH size.\nScience Mode — Show masses, velocities, and pair properties.",
        "Play / Timeline",
        "Event",
        "MERGE",
        "EXCHANGE",
        "CoM / Raw",
        "Speed / Size",
        "Science Mode"
    );
    [SerializeField] private BlackHoleLegendText englishBlackHoleLegend = new BlackHoleLegendText(
        "BH (no event selected)",
        "Binary members",
        "Interloper BH (EXCHANGE)",
        "Merger remnant (MERGE)"
    );
    [SerializeField] private NavigationText englishNavigation = new NavigationText(
        "Previous",
        "Next",
        "Start Exploring",
        "Close"
    );

    [Header("Tutorial Content - Japanese")]
    [SerializeField] private TutorialPage japaneseSciencePage = new TutorialPage(
        "Welcome to BH-VR",
        "球状星団は、数十万から数百万個の星が密集する古い天体です。ブラックホールは中心部に集まり、近接遭遇によって連星の形成、相手の組み替え、合体が起こります。\n\nBH-VRでは、N体シミュレーションをインタラクティブな3D空間で探索し、MERGEとEXCHANGEイベントを観察できます。",
        "球状星団",
        "BH-VR",
        "MERGE",
        "EXCHANGE"
    );
    [SerializeField] private TutorialPage japaneseControlsPage = new TutorialPage(
        "操作方法",
        "Play / Timeline — 再生・一時停止と時刻の移動\nEvent — MERGEまたはEXCHANGE直前へ移動\nCoM / Raw — 表示する座標系を切り替え\nSpeed / Size — 再生速度とBHサイズを調整\nScience Mode — 質量・速度・ペアの物理量を表示",
        "Play / Timeline",
        "Event",
        "MERGE",
        "EXCHANGE",
        "CoM / Raw",
        "Speed / Size",
        "Science Mode"
    );
    [SerializeField] private BlackHoleLegendText japaneseBlackHoleLegend = new BlackHoleLegendText(
        "イベント未選択時のBH",
        "連星のメンバー",
        "侵入BH（EXCHANGE）",
        "合体後のBH（MERGE）"
    );
    [SerializeField] private NavigationText japaneseNavigation = new NavigationText(
        "戻る",
        "次へ",
        "探索を始める",
        "閉じる"
    );

    [Header("Shared Tutorial Content")]
    [SerializeField] private string imageCredit = "M13 — NASA, ESA, and the Hubble Heritage Team (STScI/AURA)";

    [Header("Page Visuals")]
    [SerializeField] private GameObject sciencePageVisual;
    [SerializeField] private Image globularClusterImage;
    [SerializeField] private Sprite globularClusterPhoto;
    [SerializeField] private Text imageCreditText;
    [SerializeField] private GameObject controlsPageVisual;
    [SerializeField] private Image defaultBlackHoleImage;
    [SerializeField] private Image magentaBlackHoleImage;
    [SerializeField] private Image redBlackHoleImage;
    [SerializeField] private Image cyanBlackHoleImage;
    [SerializeField] private Text defaultBlackHoleText;
    [SerializeField] private Text magentaBlackHoleText;
    [SerializeField] private Text redBlackHoleText;
    [SerializeField] private Text cyanBlackHoleText;

    [Header("Recommended 4:6 Layout")]
    [SerializeField] private bool applyFortySixtyLayout = true;
    [SerializeField, Range(0f, 0.2f)] private float contentLeftMargin = 0.06f;
    [SerializeField, Range(0f, 0.2f)] private float contentRightMargin = 0.06f;
    [SerializeField, Range(0f, 0.1f)] private float contentColumnGap = 0.02f;
    [SerializeField] private Vector2 contentVerticalAnchors = new Vector2(0.20f, 0.80f);

    [Header("Black Hole Legend Layout")]
    [SerializeField] private bool applyBlackHoleLegendLayout = true;
    [SerializeField, Range(32f, 96f)] private float blackHoleLegendIconSize = 56f;
    [SerializeField, Range(0f, 32f)] private float blackHoleLegendTextGap = 8f;
    [SerializeField, Range(0f, 0.25f)] private float blackHoleLegendLeftAnchor = 0.04f;

    [Header("Generated Black Hole Icons")]
    [SerializeField] private bool generateMissingBlackHoleIcons = true;
    [SerializeField, Range(32, 256)] private int blackHoleIconResolution = 128;
    [SerializeField, Range(0f, 0.04f)] private float blackHoleIconEdgeSoftness = 0f;
    [SerializeField] private Color defaultBlackHoleColor = Color.black;
    [SerializeField] private Color magentaBlackHoleColor = new Color(1f, 0f, 1f, 1f);
    [SerializeField] private Color redBlackHoleColor = new Color(1f, 0.12f, 0.08f, 1f);
    [SerializeField] private Color cyanBlackHoleColor = new Color(0f, 1f, 1f, 1f);

    [Header("Highlight")]
    [SerializeField] private RectTransform highlightFrame;
    [SerializeField] private CanvasGroup highlightCanvasGroup;
    [SerializeField] private Vector2 highlightPadding = new Vector2(12f, 12f);
    [SerializeField] private float highlightPulseSpeed = 3f;
    [SerializeField, Range(0f, 1f)] private float highlightMinimumAlpha = 0.45f;

    [Header("Controls Target")]
    [SerializeField] private RectTransform controlsTarget;
    [SerializeField] private RectTransform playbackControlsTarget;

    private TutorialPage[] englishPages;
    private TutorialPage[] japanesePages;
    private TutorialLanguage currentLanguage;
    private int pageIndex;
    private readonly List<Sprite> generatedSprites = new List<Sprite>();
    private readonly List<Texture2D> generatedTextures = new List<Texture2D>();

    private void Awake()
    {
        englishPages = new[] { englishSciencePage, englishControlsPage };
        japanesePages = new[] { japaneseSciencePage, japaneseControlsPage };
        currentLanguage = defaultLanguage;

        EnableRichText();

        if (previousButtonText == null && previousButton != null)
        {
            previousButtonText = previousButton.GetComponentInChildren<Text>(true);
        }

        if (nextButtonText == null && nextButton != null)
        {
            nextButtonText = nextButton.GetComponentInChildren<Text>(true);
        }

        if (closeButtonText == null && closeButton != null)
        {
            closeButtonText = closeButton.GetComponentInChildren<Text>(true);
        }

        if (highlightCanvasGroup == null && highlightFrame != null)
        {
            highlightCanvasGroup = highlightFrame.GetComponent<CanvasGroup>();
        }

        if (highlightFrame != null)
        {
            Graphic graphic = highlightFrame.GetComponent<Graphic>();
            if (graphic != null)
            {
                graphic.raycastTarget = false;
            }
        }

        if (globularClusterImage != null)
        {
            globularClusterImage.raycastTarget = false;
            globularClusterImage.preserveAspect = true;

            if (globularClusterPhoto != null)
            {
                globularClusterImage.sprite = globularClusterPhoto;
            }
        }

        ApplyRecommendedLayout();
        ApplyBlackHoleLegendLayout();
        PrepareBlackHoleIcon(defaultBlackHoleImage, defaultBlackHoleColor, "DefaultBlackHoleIcon");
        PrepareBlackHoleIcon(magentaBlackHoleImage, magentaBlackHoleColor, "MagentaBlackHoleIcon");
        PrepareBlackHoleIcon(redBlackHoleImage, redBlackHoleColor, "RedBlackHoleIcon");
        PrepareBlackHoleIcon(cyanBlackHoleImage, cyanBlackHoleColor, "CyanBlackHoleIcon");
    }

    private void Start()
    {
        previousButton?.onClick.AddListener(PreviousPage);
        nextButton?.onClick.AddListener(NextPage);
        closeButton?.onClick.AddListener(CloseTutorial);
        helpButton?.onClick.AddListener(ToggleTutorial);
        englishButton?.onClick.AddListener(UseEnglish);
        japaneseButton?.onClick.AddListener(UseJapanese);

        OpenTutorial();
    }

    private void Update()
    {
        if (highlightCanvasGroup == null ||
            highlightFrame == null ||
            !highlightFrame.gameObject.activeInHierarchy)
        {
            return;
        }

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * highlightPulseSpeed);
        highlightCanvasGroup.alpha = Mathf.Lerp(highlightMinimumAlpha, 1f, pulse);
    }

    private void OnDestroy()
    {
        previousButton?.onClick.RemoveListener(PreviousPage);
        nextButton?.onClick.RemoveListener(NextPage);
        closeButton?.onClick.RemoveListener(CloseTutorial);
        helpButton?.onClick.RemoveListener(ToggleTutorial);
        englishButton?.onClick.RemoveListener(UseEnglish);
        japaneseButton?.onClick.RemoveListener(UseJapanese);

        for (int i = 0; i < generatedSprites.Count; i++)
        {
            Destroy(generatedSprites[i]);
        }

        for (int i = 0; i < generatedTextures.Count; i++)
        {
            Destroy(generatedTextures[i]);
        }
    }

    public void ToggleTutorial()
    {
        if (tutorialPanel != null && tutorialPanel.activeSelf)
        {
            CloseTutorial();
        }
        else
        {
            OpenTutorial();
        }
    }

    public void OpenTutorial()
    {
        TutorialPage[] pages = GetActivePages();
        if (pages.Length == 0)
        {
            return;
        }

        pageIndex = 0;
        controlPanel?.SetActive(true);
        tutorialPanel?.SetActive(true);
        UpdatePage();
    }

    public void OpenHelp()
    {
        OpenTutorial();
    }

    public void UseEnglish()
    {
        SetLanguage(TutorialLanguage.English);
    }

    public void UseJapanese()
    {
        SetLanguage(TutorialLanguage.Japanese);
    }

    public void NextPage()
    {
        TutorialPage[] pages = GetActivePages();
        if (pages.Length == 0)
        {
            CloseTutorial();
            return;
        }

        if (pageIndex >= pages.Length - 1)
        {
            CloseTutorial();
            return;
        }

        pageIndex++;
        UpdatePage();
    }

    public void PreviousPage()
    {
        if (pageIndex <= 0)
        {
            return;
        }

        pageIndex--;
        UpdatePage();
    }

    public void CloseTutorial()
    {
        tutorialPanel?.SetActive(false);
        controlPanel?.SetActive(true);
        HideHighlight();
    }

    public void SkipTutorial()
    {
        CloseTutorial();
    }

    public void ResetTutorialState()
    {
        OpenTutorial();
    }

    private void SetLanguage(TutorialLanguage language)
    {
        currentLanguage = language;
        TutorialPage[] pages = GetActivePages();

        if (pages.Length == 0)
        {
            return;
        }

        pageIndex = Mathf.Clamp(pageIndex, 0, pages.Length - 1);

        if (tutorialPanel != null && tutorialPanel.activeSelf)
        {
            UpdatePage();
        }
        else
        {
            UpdateLanguageButtons();
        }
    }

    private TutorialPage[] GetActivePages()
    {
        return currentLanguage == TutorialLanguage.Japanese
            ? japanesePages
            : englishPages;
    }

    private void UpdatePage()
    {
        TutorialPage[] pages = GetActivePages();
        if (pages.Length == 0)
        {
            return;
        }

        pageIndex = Mathf.Clamp(pageIndex, 0, pages.Length - 1);
        TutorialPage page = pages[pageIndex];

        if (titleText != null)
        {
            titleText.text = page.Title;
            titleText.fontSize = page.TitleFontSize;
        }

        if (bodyText != null)
        {
            bodyText.text = page.Body;
            bodyText.fontSize = page.BodyFontSize;
        }

        if (pageText != null)
        {
            pageText.text = $"{pageIndex + 1} / {pages.Length}";
        }

        if (previousButton != null)
        {
            previousButton.interactable = pageIndex > 0;
        }

        bool isLastPage = pageIndex == pages.Length - 1;
        UpdateNavigationText(isLastPage);
        UpdateLanguageButtons();
        UpdatePageVisuals(page);
        ShowHighlight(GetHighlightTarget(pageIndex));
    }

    private void UpdateNavigationText(bool isLastPage)
    {
        NavigationText navigation = currentLanguage == TutorialLanguage.Japanese
            ? japaneseNavigation
            : englishNavigation;

        if (navigation == null)
        {
            return;
        }

        if (previousButtonText != null)
        {
            previousButtonText.text = navigation.Previous;
        }

        if (nextButtonText != null)
        {
            nextButtonText.text = isLastPage ? navigation.Finish : navigation.Next;
        }

        if (closeButtonText != null)
        {
            closeButtonText.text = navigation.Close;
        }
    }

    private void UpdateLanguageButtons()
    {
        if (englishButton != null)
        {
            englishButton.interactable = currentLanguage != TutorialLanguage.English;
        }

        if (japaneseButton != null)
        {
            japaneseButton.interactable = currentLanguage != TutorialLanguage.Japanese;
        }
    }

    private void UpdatePageVisuals(TutorialPage page)
    {
        bool isSciencePage = pageIndex == 0;

        sciencePageVisual?.SetActive(isSciencePage);
        controlsPageVisual?.SetActive(!isSciencePage);

        SetComponentActive(globularClusterImage, isSciencePage);
        SetComponentActive(imageCreditText, isSciencePage);
        SetComponentActive(defaultBlackHoleImage, !isSciencePage);
        SetComponentActive(magentaBlackHoleImage, !isSciencePage);
        SetComponentActive(redBlackHoleImage, !isSciencePage);
        SetComponentActive(cyanBlackHoleImage, !isSciencePage);
        SetComponentActive(defaultBlackHoleText, !isSciencePage);
        SetComponentActive(magentaBlackHoleText, !isSciencePage);
        SetComponentActive(redBlackHoleText, !isSciencePage);
        SetComponentActive(cyanBlackHoleText, !isSciencePage);

        if (imageCreditText != null)
        {
            imageCreditText.text = isSciencePage
                ? imageCredit
                : string.Empty;
            imageCreditText.fontSize = page.VisualTextFontSize;
        }

        ApplyBlackHoleLegendFontSize(page.VisualTextFontSize);

        BlackHoleLegendText legend = currentLanguage == TutorialLanguage.Japanese
            ? japaneseBlackHoleLegend
            : englishBlackHoleLegend;

        if (legend == null)
        {
            return;
        }

        if (defaultBlackHoleText != null)
        {
            defaultBlackHoleText.text = legend.DefaultBlackHole;
        }

        if (magentaBlackHoleText != null)
        {
            magentaBlackHoleText.text = legend.BinaryMembers;
        }

        if (redBlackHoleText != null)
        {
            redBlackHoleText.text = legend.InterloperBlackHole;
        }

        if (cyanBlackHoleText != null)
        {
            cyanBlackHoleText.text = legend.MergerRemnant;
        }
    }

    private void ApplyBlackHoleLegendFontSize(int fontSize)
    {
        Text[] labels =
        {
            defaultBlackHoleText,
            magentaBlackHoleText,
            redBlackHoleText,
            cyanBlackHoleText
        };

        for (int i = 0; i < labels.Length; i++)
        {
            if (labels[i] != null)
            {
                labels[i].fontSize = fontSize;
            }
        }
    }

    private void EnableRichText()
    {
        Text[] richTextFields =
        {
            titleText,
            bodyText,
            imageCreditText,
            defaultBlackHoleText,
            magentaBlackHoleText,
            redBlackHoleText,
            cyanBlackHoleText
        };

        for (int i = 0; i < richTextFields.Length; i++)
        {
            if (richTextFields[i] != null)
            {
                richTextFields[i].supportRichText = true;
            }
        }
    }

    private static string ApplyBoldTerms(string source, string[] boldTerms)
    {
        if (string.IsNullOrEmpty(source) || boldTerms == null || boldTerms.Length == 0)
        {
            return source ?? string.Empty;
        }

        string[] terms = (string[])boldTerms.Clone();
        Array.Sort(terms, (left, right) =>
            (right?.Length ?? 0).CompareTo(left?.Length ?? 0));

        string result = source;

        for (int i = 0; i < terms.Length; i++)
        {
            if (!string.IsNullOrEmpty(terms[i]))
            {
                result = WrapBoldTerm(result, terms[i]);
            }
        }

        return result;
    }

    private static string WrapBoldTerm(string source, string term)
    {
        StringBuilder result = new StringBuilder(source.Length + term.Length);
        bool insideBold = false;
        int index = 0;

        while (index < source.Length)
        {
            if (source[index] == '<')
            {
                int tagEnd = source.IndexOf('>', index);

                if (tagEnd >= 0)
                {
                    string tag = source.Substring(index, tagEnd - index + 1);
                    result.Append(tag);

                    if (string.Equals(tag, "<b>", StringComparison.OrdinalIgnoreCase))
                    {
                        insideBold = true;
                    }
                    else if (string.Equals(tag, "</b>", StringComparison.OrdinalIgnoreCase))
                    {
                        insideBold = false;
                    }

                    index = tagEnd + 1;
                    continue;
                }
            }

            if (!insideBold &&
                index + term.Length <= source.Length &&
                string.CompareOrdinal(source, index, term, 0, term.Length) == 0)
            {
                result.Append("<b>");
                result.Append(term);
                result.Append("</b>");
                index += term.Length;
                continue;
            }

            result.Append(source[index]);
            index++;
        }

        return result.ToString();
    }

    private void SetComponentActive(Component component, bool isActive)
    {
        if (component != null)
        {
            component.gameObject.SetActive(isActive);
        }
    }

    private void ApplyRecommendedLayout()
    {
        if (!applyFortySixtyLayout)
        {
            return;
        }

        float left = Mathf.Clamp01(contentLeftMargin);
        float right = 1f - Mathf.Clamp01(contentRightMargin);
        float gap = Mathf.Clamp(contentColumnGap, 0f, Mathf.Max(0f, right - left));
        float usableWidth = Mathf.Max(0f, right - left - gap);
        float visualRight = left + usableWidth * 0.4f;
        float textLeft = visualRight + gap;
        float bottom = Mathf.Clamp01(Mathf.Min(contentVerticalAnchors.x, contentVerticalAnchors.y));
        float top = Mathf.Clamp01(Mathf.Max(contentVerticalAnchors.x, contentVerticalAnchors.y));

        SetStretchRect(sciencePageVisual != null ? sciencePageVisual.transform as RectTransform : null,
            new Vector2(left, bottom), new Vector2(visualRight, top));
        SetStretchRect(controlsPageVisual != null ? controlsPageVisual.transform as RectTransform : null,
            new Vector2(left, bottom), new Vector2(visualRight, top));
        SetStretchRect(bodyText != null ? bodyText.rectTransform : null,
            new Vector2(textLeft, bottom), new Vector2(right, top));

    }

    private void ApplyBlackHoleLegendLayout()
    {
        if (!applyBlackHoleLegendLayout)
        {
            return;
        }

        Image[] images =
        {
            defaultBlackHoleImage,
            magentaBlackHoleImage,
            redBlackHoleImage,
            cyanBlackHoleImage
        };

        Text[] labels =
        {
            defaultBlackHoleText,
            magentaBlackHoleText,
            redBlackHoleText,
            cyanBlackHoleText
        };

        float[] rowAnchors = { 0.875f, 0.625f, 0.375f, 0.125f };
        float leftAnchor = Mathf.Clamp(blackHoleLegendLeftAnchor, 0f, 0.25f);
        float iconSize = Mathf.Max(1f, blackHoleLegendIconSize);
        float textGap = Mathf.Max(0f, blackHoleLegendTextGap);

        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] != null)
            {
                RectTransform imageRect = images[i].rectTransform;
                imageRect.anchorMin = new Vector2(leftAnchor, rowAnchors[i]);
                imageRect.anchorMax = imageRect.anchorMin;
                imageRect.pivot = new Vector2(0f, 0.5f);
                imageRect.anchoredPosition = Vector2.zero;
                imageRect.sizeDelta = new Vector2(iconSize, iconSize);
            }

            if (labels[i] != null)
            {
                RectTransform textRect = labels[i].rectTransform;
                textRect.anchorMin = new Vector2(leftAnchor, rowAnchors[i]);
                textRect.anchorMax = new Vector2(0.98f, rowAnchors[i]);
                textRect.pivot = new Vector2(0f, 0.5f);
                textRect.offsetMin = new Vector2(iconSize + textGap, -iconSize * 0.5f);
                textRect.offsetMax = new Vector2(0f, iconSize * 0.5f);

                labels[i].alignment = TextAnchor.MiddleLeft;
                labels[i].horizontalOverflow = HorizontalWrapMode.Wrap;
                labels[i].verticalOverflow = VerticalWrapMode.Truncate;
                labels[i].raycastTarget = false;
            }
        }
    }

    private void SetStretchRect(RectTransform target, Vector2 anchorMin, Vector2 anchorMax)
    {
        if (target == null)
        {
            return;
        }

        target.anchorMin = anchorMin;
        target.anchorMax = anchorMax;
        target.offsetMin = Vector2.zero;
        target.offsetMax = Vector2.zero;
    }

    private RectTransform GetHighlightTarget(int index)
    {
        if (index != 1)
        {
            return null;
        }

        return controlsTarget != null ? controlsTarget : playbackControlsTarget;
    }

    private void ShowHighlight(RectTransform target)
    {
        if (highlightFrame == null || target == null || target.parent == null)
        {
            HideHighlight();
            return;
        }

        highlightFrame.SetParent(target.parent, false);
        highlightFrame.anchorMin = target.anchorMin;
        highlightFrame.anchorMax = target.anchorMax;
        highlightFrame.pivot = target.pivot;
        highlightFrame.anchoredPosition = target.anchoredPosition;
        highlightFrame.sizeDelta = target.sizeDelta + highlightPadding * 2f;
        highlightFrame.localRotation = target.localRotation;
        highlightFrame.localScale = target.localScale;
        highlightFrame.SetAsLastSibling();
        highlightFrame.gameObject.SetActive(true);
    }

    private void HideHighlight()
    {
        if (highlightFrame != null)
        {
            highlightFrame.gameObject.SetActive(false);
        }
    }

    private void PrepareBlackHoleIcon(Image target, Color color, string iconName)
    {
        if (target == null)
        {
            return;
        }

        target.raycastTarget = false;
        target.preserveAspect = true;
        target.color = Color.white;

        if (!generateMissingBlackHoleIcons || target.sprite != null)
        {
            return;
        }

        int resolution = Mathf.Clamp(blackHoleIconResolution, 32, 256);
        Texture2D texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false)
        {
            name = iconName + "Texture",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color[] pixels = new Color[resolution * resolution];
        float center = (resolution - 1) * 0.5f;

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float dx = (x - center) / center;
                float dy = (y - center) / center;
                float radius = Mathf.Sqrt(dx * dx + dy * dy);
                pixels[y * resolution + x] = CalculateBlackHolePixel(dx, dy, radius, color);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, false);

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, resolution, resolution),
            new Vector2(0.5f, 0.5f),
            resolution
        );
        sprite.name = iconName;

        target.sprite = sprite;
        generatedTextures.Add(texture);
        generatedSprites.Add(sprite);
    }

    private Color CalculateBlackHolePixel(float x, float y, float radius, Color sphereColor)
    {
        if (radius >= 0.94f)
        {
            return Color.clear;
        }

        float edgeSoftness = Mathf.Clamp(blackHoleIconEdgeSoftness, 0f, 0.04f);
        float opaqueRadius = 0.94f - edgeSoftness;
        float alpha = edgeSoftness <= 0f || radius <= opaqueRadius
            ? 1f
            : 1f - Mathf.SmoothStep(opaqueRadius, 0.94f, radius);
        float normalizedRadius = Mathf.Clamp01(radius / 0.94f);
        float z = Mathf.Sqrt(Mathf.Max(0f, 1f - normalizedRadius * normalizedRadius));
        Vector3 normal = new Vector3(x / 0.94f, y / 0.94f, z).normalized;
        Vector3 lightDirection = new Vector3(-0.45f, 0.55f, 0.70f).normalized;
        float diffuse = Mathf.Max(0f, Vector3.Dot(normal, lightDirection));
        float edgeShade = Mathf.Lerp(1f, 0.58f, normalizedRadius * normalizedRadius);
        float brightness = (0.52f + 0.58f * diffuse) * edgeShade;
        float highlight = Mathf.Pow(Mathf.Clamp01(diffuse), 18f) * 0.38f;

        Color pixel = new Color(
            Mathf.Clamp01(sphereColor.r * brightness + highlight),
            Mathf.Clamp01(sphereColor.g * brightness + highlight),
            Mathf.Clamp01(sphereColor.b * brightness + highlight),
            alpha
        );

        return pixel;
    }

}
