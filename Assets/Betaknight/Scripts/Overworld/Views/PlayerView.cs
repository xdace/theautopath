using System.Collections;
using Betaknight.Overworld.Config;
using UnityEngine;

namespace Betaknight.Overworld.Views
{
    /// <summary>
    /// Platzhalter-Figur des Betaknight. Bewegt sich weich von Feld zu Feld.
    /// </summary>
    public sealed class PlayerView : MonoBehaviour
    {
        private const int SortingOrder = 10;

        private float _stepDuration;

        public bool IsAnimating { get; private set; }

        public static PlayerView Create(Transform parent, Vector3 position, OverworldSettings settings)
        {
            var go = new GameObject("Betaknight");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;

            PlayerView view = go.AddComponent<PlayerView>();
            view._stepDuration = settings.stepDuration;

            float radius = settings.hexSize * 0.42f;

            // Rand
            var outline = new GameObject("Outline");
            outline.transform.SetParent(go.transform, false);
            outline.transform.localScale = new Vector3(radius * 1.18f, radius * 1.18f, 1f);
            SpriteRenderer outlineRenderer = outline.AddComponent<SpriteRenderer>();
            outlineRenderer.sprite = ProceduralSprites.Circle;
            outlineRenderer.color = new Color(0.02f, 0.03f, 0.05f);
            outlineRenderer.sortingOrder = SortingOrder;

            // Körper
            var body = new GameObject("Body");
            body.transform.SetParent(go.transform, false);
            body.transform.localScale = new Vector3(radius, radius, 1f);
            SpriteRenderer bodyRenderer = body.AddComponent<SpriteRenderer>();
            bodyRenderer.sprite = ProceduralSprites.Circle;
            bodyRenderer.color = settings.playerColor;
            bodyRenderer.sortingOrder = SortingOrder + 1;

            return view;
        }

        public void SnapTo(Vector3 position)
        {
            StopAllCoroutines();
            IsAnimating = false;
            transform.position = position;
        }

        /// <summary>Animiert einen einzelnen Schritt. Mit yield return in einer Coroutine abwarten.</summary>
        public IEnumerator AnimateStep(Vector3 target)
        {
            IsAnimating = true;
            Vector3 start = transform.position;
            float elapsed = 0f;

            while (elapsed < _stepDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / _stepDuration));
                transform.position = Vector3.Lerp(start, target, t);
                yield return null;
            }

            transform.position = target;
            IsAnimating = false;
        }
    }
}
