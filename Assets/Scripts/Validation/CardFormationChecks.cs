#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using CardCards.Battle;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace CardCards.Validation
{
    /// <summary>Checks the authored formation through the same isolated pointer used by the UI suite.</summary>
    public static class CardFormationChecks
    {
        public static IEnumerator Run(BattleView view, List<string> checks, Mouse mouse, Keyboard keyboard)
        {
            CardFormationLayout allies = view.AllyFormation;
            CardFormationLayout enemies = view.EnemyFormation;
            Require(allies != null && enemies != null, "Both card formation presenters must be connected.");
            Vector2 empty = BattlefieldEmptyPoint(view);
            yield return Move(mouse, empty);
            allies.ClearPin(); enemies.ClearPin();
            VerifyVisualOrder(allies); VerifyVisualOrder(enemies);
            VerifyDefaultLayers(allies); VerifyDefaultLayers(enemies);
            VerifyGeometry(allies); VerifyGeometry(enemies);
            VerifyPerspective(allies); VerifyPerspective(enemies);
            VerifyProjectedBoundaryHit(allies); VerifyProjectedBoundaryHit(enemies);
            VerifyBattleOrder(view);
            checks.Add("Both card formations overlap, stay within the Canvas, expose clickable projected bodies, and layer display positions 1/3 above 2/4");
            yield return VerifyFloating(allies, enemies, mouse, checks, empty);

            foreach (CardFormationLayout formation in new[] { allies, enemies })
            {
                CharacterCardView pinned = formation.Cards[1];
                CharacterCardView hovered = formation.Cards[0];
                yield return Move(mouse, ClickablePoint(pinned));
                Require(ReferenceEquals(formation.HoveredCard, pinned), "Pointer hover did not identify the exposed projected card.");
                Require(IsTopCard(formation, pinned), "Hover did not raise the projected card above its neighbours.");
                yield return Move(mouse, empty);
                Require(formation.HoveredCard == null, "Pointer exit did not release the hovered card.");
                VerifyDefaultLayers(formation);
                yield return Click(mouse, pinned);
                yield return Move(mouse, empty);
                Require(ReferenceEquals(formation.PinnedCard, pinned) && IsTopCard(formation, pinned),
                    "Click did not keep the same card on top after pointer exit.");
                yield return Move(mouse, ClickablePoint(hovered));
                Require(ReferenceEquals(formation.HoveredCard, hovered) && IsTopCard(formation, hovered)
                    && ReferenceEquals(formation.PinnedCard, pinned), "Temporary hover must cover a pinned card without replacing its pin.");
                yield return Move(mouse, empty);
                Require(IsTopCard(formation, pinned), "Leaving another card did not restore the pinned card's layer.");
                yield return Click(mouse, hovered);
                yield return Move(mouse, empty);
                Require(ReferenceEquals(formation.PinnedCard, hovered) && IsTopCard(formation, hovered),
                    "Clicking another card on the same side did not move that side's pin.");
            }
            Require(allies.PinnedCard != null && enemies.PinnedCard != null,
                "Ally and enemy card pins must coexist independently.");
            yield return PressKey(keyboard, Key.Escape);
            Require(allies.PinnedCard == null && enemies.PinnedCard == null, "Escape must clear both formation pins.");
            yield return Click(mouse, allies.Cards[0]);
            yield return Click(mouse, enemies.Cards[0]);
            yield return ClickAt(mouse, empty);
            Require(allies.PinnedCard == null && enemies.PinnedCard == null,
                "A battlefield blank click must clear both formation pins.");
            yield return Move(mouse, empty);
            VerifyDefaultLayers(allies); VerifyDefaultLayers(enemies);
            checks.Add("Real pointer hover raises either side and restores layers on exit; each side retains one click pin, hover temporarily covers it, another click replaces it, and blank/Escape clears both");
        }

        public static void VerifyVisualOrder(CardFormationLayout formation)
        {
            float previous = float.NegativeInfinity, previousActual = float.NegativeInfinity;
            for (int index = 0; index < formation.Cards.Count; index++)
            {
                CharacterCardView card = formation.Cards[index];
                float current = formation.GetCardScreenPosition(card).x;
                Require(current > previous, "Formation's display order must run from screen left to right.");
                float actual = RectTransformUtility.WorldToScreenPoint(null, card.transform.position).x;
                Require(actual > previousActual, "Settled card roots must appear in their formation's left-to-right display order.");
                previous = current;
                previousActual = actual;
            }
        }

        public static Vector2 BattlefieldEmptyPoint(BattleView view)
        {
            RectTransform canvas = (RectTransform)view.GetComponent<Canvas>().rootCanvas.transform;
            return RectTransformUtility.WorldToScreenPoint(null,
                canvas.TransformPoint(new Vector3(canvas.rect.xMin + 640f, canvas.rect.yMax - 157f, 0f)));
        }

        public static void VerifyBattleOrder(BattleView view)
        {
            var living = new List<BattleUnitState>();
            foreach (BattleUnitState ally in view.Controller.Allies) if (ally.IsAlive) living.Add(ally);
            Require(living.Count == view.AllyFormation.Cards.Count, "Ally display count no longer matches living battle positions.");
            for (int i = 0; i < living.Count; i++)
                Require(ReferenceEquals(view.AllyFormation.Cards[i].Unit, living[living.Count - 1 - i]),
                    "Ally display order must remain the reverse of front-to-back battle order after a reorder.");
            living.Clear();
            foreach (BattleUnitState enemy in view.Controller.Enemies) if (enemy.IsAlive) living.Add(enemy);
            Require(living.Count == view.EnemyFormation.Cards.Count, "Enemy display count no longer matches living battle positions.");
            for (int i = 0; i < living.Count; i++)
                Require(ReferenceEquals(view.EnemyFormation.Cards[i].Unit, living[i]),
                    "Enemy display order must retain front-to-back battle order.");
        }

        private static void VerifyPerspective(CardFormationLayout formation)
        {
            if (formation.Cards.Count < 2) return;
            float first = ProjectedHeight(formation.Cards[0]);
            float last = ProjectedHeight(formation.Cards[formation.Cards.Count - 1]);
            Require(formation.IsEnemy ? last > first : first > last,
                "Default perspective must make outer characters larger than characters near the screen centre.");
            foreach (CharacterCardView card in formation.Cards)
            {
                Vector3[] corners = card.GetComponent<CardPresentation>().GetWorldCorners();
                float left = Vector3.Distance(corners[0], corners[1]);
                float right = Vector3.Distance(corners[3], corners[2]);
                Require(formation.IsEnemy ? right > left : left > right,
                    "Mirrored card perspective must make each card's outer vertical edge larger.");
            }
        }

        private static float ProjectedHeight(CharacterCardView card)
        {
            Vector3[] corners = card.GetComponent<CardPresentation>().GetWorldCorners();
            return (Vector3.Distance(corners[0], corners[1]) + Vector3.Distance(corners[3], corners[2])) * .5f;
        }

        private static void VerifyProjectedBoundaryHit(CardFormationLayout formation)
        {
            CharacterCardView top = formation.Cards[0];
            foreach (CharacterCardView card in formation.Cards)
                if (card.transform.GetSiblingIndex() > top.transform.GetSiblingIndex()) top = card;
            CardPresentation presentation = top.GetComponent<CardPresentation>();
            Vector3[] corners = presentation.GetWorldCorners();
            Vector3 center = (corners[0] + corners[1] + corners[2] + corners[3]) * .25f;
            Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (int i = 0; i < 4; i++)
            {
                Vector2 inset = RectTransformUtility.WorldToScreenPoint(null, Vector3.Lerp(corners[i], center, .03f));
                Require(presentation.ContainsScreenPoint(inset) && HitsCard(top, inset),
                    "An exposed projected card corner must be clickable inside its visible border.");
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, corners[i]);
                minimum = Vector2.Min(minimum, screen); maximum = Vector2.Max(maximum, screen);
            }
            bool checkedOutside = false;
            foreach (Vector2 candidate in new[] { minimum + Vector2.one, maximum - Vector2.one,
                new Vector2(minimum.x + 1f, maximum.y - 1f), new Vector2(maximum.x - 1f, minimum.y + 1f) })
            {
                if (presentation.ContainsScreenPoint(candidate)) continue;
                checkedOutside = true;
                Require(!HitsCard(top, candidate),
                    "The transparent bounding-box corner outside the projected polygon must not intercept clicks.");
            }
            Require(checkedOutside, "Default trapezoid perspective must expose an outside-polygon bounding-box corner for hit validation.");
        }

        private static IEnumerator VerifyFloating(CardFormationLayout allies, CardFormationLayout enemies, Mouse mouse,
            List<string> checks, Vector2 empty)
        {
            CardFormationSettings originalAllies = allies.Settings;
            CardFormationSettings originalEnemies = enemies.Settings;
            CardFormationSettings temporary = UnityEngine.Object.Instantiate(originalAllies);
            try
            {
                // Test a temporary in-memory clone; no authored tuning asset is changed or saved.
                temporary.pauseFloat = false;
                foreach (CardFormationSettings.SideSettings side in new[] { temporary.ally, temporary.enemy })
                {
                    side.floatEnabled = true; side.floatAmplitude = 6f; side.floatPeriod = .6f;
                    side.transitionDuration = 0f; side.hoverLift = 0f; side.hoverScale = 1f;
                }
                allies.Configure(temporary, false); enemies.Configure(temporary, true);
                foreach (CardFormationLayout formation in new[] { allies, enemies })
                {
                    CharacterCardView card = formation.Cards[0];
                    float minimum = float.PositiveInfinity, maximum = float.NegativeInfinity;
                    for (int sample = 0; sample < 9; sample++)
                    {
                        yield return new WaitForSecondsRealtime(.09f);
                        float y = BodyCenter(card).y;
                        minimum = Mathf.Min(minimum, y); maximum = Mathf.Max(maximum, y);
                    }
                    Require(maximum - minimum > 3f, "Idle projected card did not visibly float with nonzero floating parameters.");
                    yield return Move(mouse, ClickablePoint(card));
                    Require(ReferenceEquals(formation.HoveredCard, card), "Floating pause check did not hover its intended card.");
                    float frozen = BodyCenter(card).y;
                    yield return new WaitForSecondsRealtime(.2f);
                    Require(Mathf.Abs(BodyCenter(card).y - frozen) < .05f,
                        "Hovered card must pause its idle floating motion.");
                    yield return Move(mouse, empty);
                }
                temporary.pauseFloat = true;
                yield return null; yield return null;
                Vector2 allyFrozen = BodyCenter(allies.Cards[0]), enemyFrozen = BodyCenter(enemies.Cards[0]);
                yield return new WaitForSecondsRealtime(.25f);
                Require(Vector2.Distance(BodyCenter(allies.Cards[0]), allyFrozen) < .05f
                    && Vector2.Distance(BodyCenter(enemies.Cards[0]), enemyFrozen) < .05f,
                    "The tuning tool's global floating pause must freeze both sides.");
                checks.Add("Mirrored perspective makes outer cards and outer edges larger; live temporary float parameters move both sides, hovering freezes that card, and the global pause freezes both without saving test values");
                foreach (CardFormationSettings.SideSettings side in new[] { temporary.ally, temporary.enemy })
                {
                    side.transitionDuration = .16f; side.hoverLift = 24f; side.hoverScale = 1.15f;
                }
                foreach (CardFormationLayout formation in new[] { allies, enemies })
                {
                    CharacterCardView card = formation.Cards[0];
                    CardPresentation presentation = card.GetComponent<CardPresentation>();
                    Vector3[] corners = presentation.GetWorldCorners();
                    Vector3 center = (corners[0] + corners[1] + corners[2] + corners[3]) * .25f;
                    Vector2 edge = RectTransformUtility.WorldToScreenPoint(null,
                        Vector3.Lerp((corners[0] + corners[3]) * .5f, center, .015f));
                    Require(presentation.ContainsScreenPoint(edge) && HitsCard(card, edge),
                        "Hover retention must start at an exposed point just inside the card's original bottom edge.");
                    yield return Move(mouse, edge);
                    for (int sample = 0; sample < 10; sample++)
                    {
                        yield return new WaitForSecondsRealtime(.05f);
                        Require(ReferenceEquals(formation.HoveredCard, card) && IsTopCard(formation, card),
                            "Nonzero hover lift/scale must retain an edge hover throughout the transition without layer flicker.");
                    }
                    Require(!presentation.ContainsScreenPoint(edge) && presentation.RetainsHover(edge),
                        "Raised-card retention must preserve the entry area while actual click geometry follows the current body.");
                    yield return Move(mouse, empty);
                    yield return new WaitForSecondsRealtime(.35f);
                    Require(formation.HoveredCard == null, "Leaving both current and entry areas must release hover retention.");
                    VerifyDefaultLayers(formation);
                }
                checks.Add("With a live temporary 24-unit hover lift and 1.15 scale, both sides retain bottom-edge hover through the full transition, keep actual click geometry strict, and restore default layers on exit");
            }
            finally
            {
                allies.Configure(originalAllies, false); enemies.Configure(originalEnemies, true);
                UnityEngine.Object.Destroy(temporary);
            }
        }

        private static Vector2 BodyCenter(CharacterCardView card)
        {
            Vector3[] corners = card.GetComponent<CardPresentation>().GetWorldCorners();
            return RectTransformUtility.WorldToScreenPoint(null, (corners[0] + corners[1] + corners[2] + corners[3]) * .25f);
        }

        public static void VerifyGeometry(CardFormationLayout formation)
        {
            Require(formation.Cards.Count > 0, "Formation has no living cards to verify.");
            Rect? previous = null;
            bool overlaps = false;
            foreach (CharacterCardView card in formation.Cards)
            {
                CardPresentation presentation = card.GetComponent<CardPresentation>();
                Require(presentation != null, "Character card has no projection presenter.");
                Vector3[] corners = presentation.GetWorldCorners();
                Require(corners != null && corners.Length == 4, "Projected card must provide four body corners.");
                Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
                Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
                foreach (Vector3 corner in corners)
                {
                    Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, corner);
                    minimum = Vector2.Min(minimum, screen); maximum = Vector2.Max(maximum, screen);
                    Require(screen.x >= -.5f && screen.x <= Screen.width + .5f && screen.y >= -.5f && screen.y <= Screen.height + .5f,
                        "Projected character card body extends beyond the screen.");
                }
                Rect bounds = Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
                if (previous.HasValue && previous.Value.Overlaps(bounds)) overlaps = true;
                previous = bounds;
                Vector2 point = ClickablePoint(card);
                Require(presentation.ContainsScreenPoint(point), "Projected visible point is outside the card's hit polygon.");
            }
            Require(formation.Cards.Count < 2 || overlaps, "Formation lost the requested card overlap.");
        }

        // Card centres can be obscured by a neighbour. Sample the actual projected body and ask
        // the real EventSystem which card would receive each candidate before injecting input.
        public static Vector2 ClickablePoint(CharacterCardView card)
        {
            Require(card != null, "Cannot find a visible point for a missing card.");
            Canvas.ForceUpdateCanvases();
            CardPresentation presentation = card.GetComponent<CardPresentation>();
            if (presentation == null) return RectTransformUtility.WorldToScreenPoint(null, card.transform.position);
            Vector2 preferred = presentation.GetClickableScreenPoint();
            if (HitsCard(card, preferred)) return preferred;
            Vector3[] corners = presentation.GetWorldCorners();
            // Inset samples avoid exact shared edges, where floating-point hit results are ambiguous.
            float[] steps = { .5f, .25f, .75f, .1f, .9f, .04f, .96f };
            foreach (float v in steps)
                foreach (float u in steps)
                {
                    Vector3 bottom = Vector3.Lerp(corners[0], corners[3], u);
                    Vector3 top = Vector3.Lerp(corners[1], corners[2], u);
                    Vector2 point = RectTransformUtility.WorldToScreenPoint(null, Vector3.Lerp(bottom, top, v));
                    if (presentation.ContainsScreenPoint(point) && HitsCard(card, point)) return point;
                }
            throw new InvalidOperationException("No visible EventSystem hit point for " + (card.Unit == null ? card.name : card.Unit.Name));
        }

        public static bool HitsCard(CharacterCardView card, Vector2 point)
        {
            EventSystem events = EventSystem.current;
            Require(events != null, "Card hit verification requires an EventSystem.");
            var hits = new List<RaycastResult>();
            events.RaycastAll(new PointerEventData(events) { position = point }, hits);
            return hits.Count > 0 && ReferenceEquals(hits[0].gameObject.GetComponentInParent<CharacterCardView>(), card);
        }

        private static void VerifyDefaultLayers(CardFormationLayout formation)
        {
            for (int odd = 0; odd < formation.Cards.Count; odd += 2)
                for (int even = 1; even < formation.Cards.Count; even += 2)
                    Require(formation.Cards[odd].transform.GetSiblingIndex() > formation.Cards[even].transform.GetSiblingIndex(),
                        "Default display positions 1/3 must cover positions 2/4, independently of battle position.");
        }

        private static bool IsTopCard(CardFormationLayout formation, CharacterCardView expected)
        {
            foreach (CharacterCardView card in formation.Cards)
                if (!ReferenceEquals(card, expected) && card.transform.GetSiblingIndex() > expected.transform.GetSiblingIndex()) return false;
            return true;
        }

        private static IEnumerator Move(Mouse mouse, Vector2 point)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return null; yield return null;
        }

        private static IEnumerator Click(Mouse mouse, CharacterCardView card)
        {
            yield return Move(mouse, ClickablePoint(card));
            // Hover can move/enlarge the card. Resolve the exposed hit point again before press.
            yield return ClickAt(mouse, ClickablePoint(card));
        }

        private static IEnumerator ClickAt(Mouse mouse, Vector2 point)
        {
            yield return Move(mouse, point);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return null; yield return null;
        }

        private static IEnumerator PressKey(Keyboard keyboard, Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null; yield return null;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
#endif
