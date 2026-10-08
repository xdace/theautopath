using Betaknight.Core.Encounters;
using Betaknight.Core.Map;
using Betaknight.Overworld.Config;
using UnityEngine;

namespace Betaknight.Overworld.Views
{
    public enum CellHighlight
    {
        None,
        Route,
        Invalid,
    }

    /// <summary>
    /// Darstellung eines einzelnen Feldes: eingefärbtes Hexagon plus Textlabel ("?", Inhalts- oder Event-Symbol).
    /// Erledigte Events werden abgedunkelt.
    /// Kennt keine Spielregeln, sondern bildet nur den Zustand einer <see cref="HexCell"/> ab.
    /// </summary>
    public sealed class HexCellView : MonoBehaviour
    {
        private const int SortingBase = 0;

        private SpriteRenderer _background;
        private TextMesh _label;
        private MeshRenderer _labelRenderer;
        private OverworldSettings _settings;
        private EncounterCatalog _encounters;
        private HexCell _cell;
        private CellHighlight _highlight;

        public HexCell Cell => _cell;

        public static HexCellView Create(Transform parent, HexCell cell, Vector3 position, OverworldSettings settings, EncounterCatalog encounters)
        {
            var go = new GameObject($"Hex {cell.Coord}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;

            HexCellView view = go.AddComponent<HexCellView>();
            view.Build(cell, settings, encounters);
            return view;
        }

        private void Build(HexCell cell, OverworldSettings settings, EncounterCatalog encounters)
        {
            _cell = cell;
            _settings = settings;
            _encounters = encounters;

            float scale = settings.hexSize * (1f - settings.hexGap);

            var bg = new GameObject("Background");
            bg.transform.SetParent(transform, false);
            bg.transform.localScale = new Vector3(scale, scale, 1f);
            _background = bg.AddComponent<SpriteRenderer>();
            _background.sprite = ProceduralSprites.Hex;
            _background.sortingOrder = SortingBase;

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            _label = labelGo.AddComponent<TextMesh>();
            _label.font = ProceduralSprites.LabelFont;
            _label.fontSize = 64;
            _label.characterSize = settings.hexSize * 0.09f;
            _label.anchor = TextAnchor.MiddleCenter;
            _label.alignment = TextAlignment.Center;
            _label.fontStyle = FontStyle.Bold;

            _labelRenderer = labelGo.GetComponent<MeshRenderer>();
            if (_label.font != null) _labelRenderer.sharedMaterial = _label.font.material;
            _labelRenderer.sortingOrder = SortingBase + 1;

            Refresh();
        }

        public void SetHighlight(CellHighlight highlight)
        {
            if (_highlight == highlight) return;
            _highlight = highlight;
            Refresh();
        }

        /// <summary>Liest den aktuellen Zellzustand neu ein.</summary>
        public void Refresh()
        {
            if (_cell == null) return;

            Color color;
            string text;
            Color textColor = Color.white;
            bool visible = true;

            switch (_cell.Visibility)
            {
                case CellVisibility.Hidden:
                    visible = _settings.showHiddenCells;
                    color = _settings.hiddenColor;
                    text = string.Empty;
                    break;

                case CellVisibility.Unexplored:
                    color = _settings.unexploredColor;
                    text = "?";
                    textColor = _settings.unexploredLabelColor;
                    if (_cell.IsScouted)
                    {
                        // Ausgekundschaftet: Inhalt ist bekannt, das Feld aber noch nicht betreten.
                        ResolveContentLook(out Color contentColor, out string contentText);
                        color = Color.Lerp(_settings.unexploredColor, contentColor, 0.5f);
                        if (!string.IsNullOrEmpty(contentText)) text = contentText;
                    }
                    break;

                default:
                    ResolveContentLook(out color, out text);
                    if (_cell.IsResolved)
                    {
                        color = Color.Lerp(color, _settings.resolvedColor, 0.7f);
                        textColor = new Color(1f, 1f, 1f, 0.35f);
                    }
                    break;
            }

            if (_cell.IsUnderAttack && _cell.IsContentKnown)
            {
                // Angegriffene oder verlorene eigene Mine: rot pulsierend wäre schöner, fürs Erste fest rot getönt.
                color = Color.Lerp(color, _settings.blockedHighlightColor, 0.6f);
                text = "!G";
                textColor = Color.white;
            }

            if (!_cell.IsWalkable) color *= 0.5f;

            switch (_highlight)
            {
                case CellHighlight.Route:
                    color = Color.Lerp(color, _settings.highlightColor, 0.55f);
                    break;
                case CellHighlight.Invalid:
                    color = Color.Lerp(color, _settings.blockedHighlightColor, 0.45f);
                    break;
            }

            color.a = 1f;
            _background.enabled = visible;
            _background.color = color;
            _label.text = text;
            _label.color = textColor;
            _labelRenderer.enabled = visible && !string.IsNullOrEmpty(text);
        }

        private void ResolveContentLook(out Color color, out string text)
        {
            if (_cell.Content == CellContent.Encounter
                && _encounters != null && _encounters.TryGet(_cell.EncounterId, out EncounterDefinition encounter))
            {
                color = encounter.Size == EncounterSize.Medium ? _settings.mediumEventColor : _settings.minorEventColor;
                text = encounter.Symbol;
                return;
            }

            OverworldSettings.ContentStyle style = _settings.GetStyle(_cell.Content);
            color = style.color;
            text = style.label;
        }
    }
}
