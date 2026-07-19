using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TeekayUtils.DevConsole.UI
{
    /// <summary>
    /// Virtualized log renderer: a pool of row widgets covering only the visible slice of the
    /// buffer, manually stacked from per-entry measured heights. Replaces the original
    /// single-TMP-string approach, which rebuilt the whole log every append and made per-line
    /// features (hover, click-to-copy, count badges) impossible.
    ///
    /// Layout groups are deliberately NOT used — hundreds of rows with VerticalLayoutGroup +
    /// ContentSizeFitter is a well-known layout-rebuild trap. Heights are measured once per entry
    /// via <see cref="TMP_Text.GetPreferredValues(string, float, float)"/> and cached by the
    /// entry's stable <see cref="ConsoleLogEntry.Sequence"/>.
    /// </summary>
    sealed class ConsoleLogView
    {
        const float ROW_PAD_V = 3f;         // vertical padding inside each row (top and bottom)
        const float TEXT_PAD_LEFT = 12f;    // room for the severity stripe
        const float TEXT_PAD_RIGHT = 10f;   // keeps text clear of the scrollbar
        const float FLASH_DECAY_PER_SEC = 3f;
        const int MAX_POOL = 96;

        readonly DevConsole _console;
        readonly ScrollRect _scroll;
        readonly RectTransform _viewport;
        readonly RectTransform _content;
        readonly TMP_Text _measurer;
        readonly System.Func<TMP_FontAsset> _font;

        sealed class Row
        {
            public GameObject Go;
            public RectTransform Rt;
            public Image Bg;
            public Image Stripe;
            public TMP_Text Text;
            public ConsoleLogRow Handler;
            public long BoundSequence = -1;
            public int BoundCount = -1;
            public int BoundVisibleIndex = -1;
            public float Flash; // 1 → 0 after click-to-copy
        }

        readonly List<Row> _pool = new();

        // Visible slice of the buffer after filtering. Rebuilt on any change — the buffer holds
        // hundreds of entries at most, so a full pass is cheaper than tracking deltas.
        readonly List<int> _visible = new();
        readonly List<float> _offsets = new();       // top y of each visible row
        readonly List<float> _heights = new();
        float _totalHeight;

        // Height cache keyed by entry sequence; entries are immutable except Count (collapse),
        // so Count is part of the cached value and a mismatch forces a re-measure.
        readonly Dictionary<long, (float height, int count)> _heightCache = new();
        float _cachedWidth = -1f;

        string _search = string.Empty;
        bool _dirty = true;

        // Stick-to-bottom: follow new output only while the user is already at the bottom.
        // While scrolled up, count what arrives so the UI can offer a "N new" jump pill.
        //
        // The scroll position is written ONLY in response to an append/open (a one-shot request),
        // never every frame. Continuously re-pinning fights the ScrollRect: any position the user
        // reaches by wheel or scrollbar is overwritten on the next frame, which reads as "the
        // console simply does not scroll".
        bool _stickToBottom = true;
        bool _forceScrollToBottom;
        public int UnseenCount { get; private set; }
        public bool AtBottom => _stickToBottom;

        float MaxScroll => Mathf.Max(0f, _totalHeight - _viewport.rect.height);

        public ConsoleLogView(DevConsole console, ScrollRect scroll, RectTransform viewport,
                              RectTransform content, TMP_Text measurer, System.Func<TMP_FontAsset> font)
        {
            _console = console;
            _scroll = scroll;
            _viewport = viewport;
            _content = content;
            _measurer = measurer;
            _font = font;
            _scroll.onValueChanged.AddListener(OnScrollChanged);
        }

        public void MarkDirty() => _dirty = true;

        public void NotifyAppended()
        {
            _dirty = true;
            if (_stickToBottom) _forceScrollToBottom = true;
            else UnseenCount++;
        }

        public void NotifyCleared()
        {
            _heightCache.Clear();
            UnseenCount = 0;
            _stickToBottom = true;
            _forceScrollToBottom = true;
            _dirty = true;
        }

        public void SetSearch(string search)
        {
            _search = search ?? string.Empty;
            _dirty = true;
            _forceScrollToBottom = _stickToBottom; // keep the tail in view across a refilter
        }

        /// <summary>Category toggles changed — refilter without touching cached heights.</summary>
        public void NotifyFilterChanged() => _dirty = true;

        public void ScrollToBottom()
        {
            _stickToBottom = true;
            _forceScrollToBottom = true;
            UnseenCount = 0;
            _dirty = true;
        }

        /// <summary>Drive the view — call once per frame while the console window is visible.</summary>
        public void Tick(float unscaledDeltaTime)
        {
            float width = _viewport.rect.width;
            if (!Mathf.Approximately(width, _cachedWidth))
            {
                _cachedWidth = width;
                _heightCache.Clear(); // wrap width changed; every measured height is stale
                _dirty = true;
            }

            if (_dirty)
            {
                Rebuild();
                _dirty = false;
            }

            // One-shot: only ever applied right after content changed or the console opened.
            if (_forceScrollToBottom)
            {
                _forceScrollToBottom = false;
                _content.anchoredPosition = new Vector2(_content.anchoredPosition.x, MaxScroll);
                _scroll.velocity = Vector2.zero;
                _stickToBottom = true;
                UnseenCount = 0;
            }

            BindVisibleRows();
            UpdateRowChrome(unscaledDeltaTime);
        }

        // ─── internals ───────────────────────────────────────────────────────

        // Fires on wheel, drag, and scrollbar movement. This is the ONLY place _stickToBottom is
        // turned off, so scrolling away from the bottom always sticks (nothing re-pins it).
        void OnScrollChanged(Vector2 _)
        {
            bool atBottom = _content.anchoredPosition.y >= MaxScroll - 4f;
            if (atBottom && !_stickToBottom) UnseenCount = 0;
            _stickToBottom = atBottom;
        }

        void Rebuild()
        {
            _visible.Clear();
            _offsets.Clear();
            _heights.Clear();
            _totalHeight = 0f;

            ConsoleLogBuffer buffer = _console.LogBuffer;
            var registry = DevConsole.Registry;

            for (int i = 0; i < buffer.Count; i++)
            {
                ConsoleLogEntry entry = buffer[i];

                // Filter 1: category toggles (same flag log_filter drives). Disabled categories
                // are already dropped at append time; this additionally hides entries that were
                // logged before the category was turned off.
                if (registry.TryGetCategory(entry.Category, out var cat) && !cat.enabled) continue;

                // Filter 2: search box — case-insensitive substring on message or category.
                if (_search.Length > 0 &&
                    entry.Message.IndexOf(_search, System.StringComparison.OrdinalIgnoreCase) < 0 &&
                    entry.Category.IndexOf(_search, System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                float height = MeasureHeight(entry);
                _visible.Add(i);
                _offsets.Add(_totalHeight);
                _heights.Add(height);
                _totalHeight += height;
            }

            _content.sizeDelta = new Vector2(0f, _totalHeight);

            // Content may have shrunk (clear/filter) — keep the scroll position in range.
            if (_content.anchoredPosition.y > MaxScroll)
                _content.anchoredPosition = new Vector2(_content.anchoredPosition.x, MaxScroll);
        }

        float MeasureHeight(in ConsoleLogEntry entry)
        {
            if (_heightCache.TryGetValue(entry.Sequence, out var cached) && cached.count == entry.Count)
                return cached.height;

            float textWidth = Mathf.Max(10f, _cachedWidth - TEXT_PAD_LEFT - TEXT_PAD_RIGHT);
            float height = _measurer.GetPreferredValues(BuildRowText(entry), textWidth, 0f).y
                           + ROW_PAD_V * 2f;
            _heightCache[entry.Sequence] = (height, entry.Count);

            // Sequences only grow, so a simple size trip-wire keeps the cache from accumulating
            // dead entries across trims; the next Rebuild repopulates the live ones.
            if (_heightCache.Count > DevConsoleSettings.MaxLogEntries * 2) _heightCache.Clear();
            return height;
        }

        string BuildRowText(in ConsoleLogEntry entry)
        {
            var sb = new System.Text.StringBuilder(entry.Message.Length + 64);
            sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGBA(entry.Color)).Append('>')
              .Append('[').Append(entry.Category).Append("] ").Append(entry.Message).Append("</color>");
            if (entry.Count > 1)
            {
                sb.Append(" <color=#").Append(ColorUtility.ToHtmlStringRGBA(DevConsoleSettings.AccentColor))
                  .Append("><b>×").Append(entry.Count).Append("</b></color>");
            }
            return sb.ToString();
        }

        void BindVisibleRows()
        {
            float scrollY = _content.anchoredPosition.y;
            float viewportH = _viewport.rect.height;

            int first = FirstVisibleIndex(scrollY);
            int poolIdx = 0;

            for (int v = first; v < _visible.Count && poolIdx < MAX_POOL; v++)
            {
                if (_offsets[v] > scrollY + viewportH) break;

                Row row = GetRow(poolIdx++);
                ConsoleLogEntry entry = _console.LogBuffer[_visible[v]];

                if (row.BoundSequence != entry.Sequence || row.BoundCount != entry.Count)
                {
                    row.Text.text = BuildRowText(entry);
                    row.Stripe.color = entry.Color;
                    row.BoundSequence = entry.Sequence;
                    row.BoundCount = entry.Count;
                    row.Flash = 0f;
                }
                row.BoundVisibleIndex = v;
                row.Rt.anchoredPosition = new Vector2(0f, -_offsets[v]);
                row.Rt.sizeDelta = new Vector2(0f, _heights[v]);
                if (!row.Go.activeSelf) row.Go.SetActive(true);
            }

            for (int i = poolIdx; i < _pool.Count; i++)
            {
                if (_pool[i].Go.activeSelf)
                {
                    _pool[i].Go.SetActive(false);
                    _pool[i].BoundSequence = -1;
                }
            }
        }

        int FirstVisibleIndex(float scrollY)
        {
            // Binary search for the last row whose top sits at or above the scroll position.
            int lo = 0, hi = _visible.Count - 1, result = 0;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                if (_offsets[mid] <= scrollY) { result = mid; lo = mid + 1; }
                else hi = mid - 1;
            }
            return result;
        }

        void UpdateRowChrome(float unscaledDeltaTime)
        {
            foreach (Row row in _pool)
            {
                if (!row.Go.activeSelf) continue;
                if (row.Flash > 0f)
                    row.Flash = Mathf.Max(0f, row.Flash - unscaledDeltaTime * FLASH_DECAY_PER_SEC);

                Color baseColor = row.Handler.Hovered
                    ? DevConsoleSettings.ChromeHoverColor
                    : (row.BoundVisibleIndex % 2 == 1 ? new Color(1f, 1f, 1f, 0.018f)
                                                      : new Color(0f, 0f, 0f, 0f));
                Color accent = DevConsoleSettings.AccentColor; accent.a = 0.25f;
                row.Bg.color = row.Flash > 0f ? Color.Lerp(baseColor, accent, row.Flash) : baseColor;
            }
        }

        Row GetRow(int index)
        {
            while (index >= _pool.Count) _pool.Add(CreateRow(_pool.Count));
            return _pool[index];
        }

        Row CreateRow(int poolIndex)
        {
            var go = new GameObject($"LogRow{poolIndex}", typeof(Image), typeof(ConsoleLogRow));
            go.transform.SetParent(_content, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);

            var bg = go.GetComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0f);
            bg.raycastTarget = true; // hover + click-to-copy; drags still reach the ScrollRect

            var handler = go.GetComponent<ConsoleLogRow>();
            handler.PoolIndex = poolIndex;
            handler.Clicked = CopyRow;

            var stripeGo = new GameObject("Stripe", typeof(Image));
            stripeGo.transform.SetParent(go.transform, false);
            var stripeRt = (RectTransform)stripeGo.transform;
            stripeRt.anchorMin = new Vector2(0f, 0f);
            stripeRt.anchorMax = new Vector2(0f, 1f);
            stripeRt.pivot = new Vector2(0f, 0.5f);
            stripeRt.anchoredPosition = new Vector2(2f, 0f);
            stripeRt.sizeDelta = new Vector2(2f, -4f);
            var stripeImg = stripeGo.GetComponent<Image>();
            stripeImg.raycastTarget = false;

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.font = _font();
            text.fontSize = DevConsoleSettings.FontSize;
            text.richText = true;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.raycastTarget = false;
            var textRt = text.rectTransform;
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(TEXT_PAD_LEFT, ROW_PAD_V);
            textRt.offsetMax = new Vector2(-TEXT_PAD_RIGHT, -ROW_PAD_V);

            return new Row
            {
                Go = go, Rt = rt, Bg = bg, Stripe = stripeImg, Text = text, Handler = handler,
            };
        }

        void CopyRow(int poolIndex)
        {
            if (poolIndex < 0 || poolIndex >= _pool.Count) return;
            Row row = _pool[poolIndex];
            if (row.BoundVisibleIndex < 0 || row.BoundVisibleIndex >= _visible.Count) return;

            ConsoleLogEntry entry = _console.LogBuffer[_visible[row.BoundVisibleIndex]];
            string plain = $"[{entry.Category}] {entry.Message}" + (entry.Count > 1 ? $" ×{entry.Count}" : "");
            GUIUtility.systemCopyBuffer = plain;
            row.Flash = 1f; // visual confirmation — the accent tint decaying back to base
        }

        /// <summary>Copy every visible (filtered) line as plain text.</summary>
        public string BuildPlainText()
        {
            var sb = new System.Text.StringBuilder(4096);
            foreach (int idx in _visible)
            {
                ConsoleLogEntry entry = _console.LogBuffer[idx];
                sb.Append('[').Append(entry.Category).Append("] ").Append(entry.Message);
                if (entry.Count > 1) sb.Append(" ×").Append(entry.Count);
                sb.Append('\n');
            }
            return sb.ToString();
        }
    }
}
