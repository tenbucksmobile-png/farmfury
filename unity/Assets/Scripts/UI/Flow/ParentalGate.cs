using System;
using UnityEngine;
using UnityEngine.UI;

namespace FarmFury.UI
{
    /// <summary>
    /// Arcade's arithmetic parental gate (ParentalGateController), shown in front of every real-money purchase:
    /// "What is a + b?" with three answers. The right one runs the purchase; a wrong one rolls a new question;
    /// the back button cancels. A plain dark modal rather than the themed poster backdrop.
    /// </summary>
    public class ParentalGate : OverlayScreen
    {
        private readonly Text _question;
        private readonly Button[] _answers = new Button[3];
        private int _correctIndex;
        private Action _onPassed;

        public ParentalGate(Transform canvas, MenuArt art, ShopArt shop) : base(canvas, "ParentalGate", art, shop, posterBackdrop: false)
        {
            var heading = UIKit.Label(Rect, "Heading", "Ask a grown-up", 56, TextAnchor.MiddleCenter, UIKit.Accent);
            heading.fontStyle = FontStyle.Bold;
            UIKit.Place(heading.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 250f), new Vector2(1200f, 80f));

            _question = UIKit.Label(Rect, "Question", "", 72, TextAnchor.MiddleCenter);
            _question.fontStyle = FontStyle.Bold;
            UIKit.Place(_question.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(1200f, 110f));

            for (int i = 0; i < _answers.Length; i++)
            {
                int index = i;
                _answers[i] = PlaqueButton(Rect, $"Answer{i}", "", 60, () => Answer(index));
                UIKit.Place(_answers[i].image.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2((i - 1) * 300f, -80f), new Vector2(240f, 130f));
            }
        }

        /// <summary>Asks a new question; onPassed runs once it's answered correctly.</summary>
        public void Ask(Action onPassed)
        {
            _onPassed = onPassed;
            Show();
            RollQuestion();
        }

        public override void Hide()
        {
            base.Hide();
            _onPassed = null;
        }

        private void RollQuestion()
        {
            int a = UnityEngine.Random.Range(2, 10);
            int b = UnityEngine.Random.Range(2, 10);
            int correct = a + b;
            _question.text = $"What is {a} + {b}?";

            int wrong1 = WrongAnswer(correct, correct);
            int wrong2 = WrongAnswer(correct, wrong1);
            int[] wrong = { wrong1, wrong2 };
            _correctIndex = UnityEngine.Random.Range(0, _answers.Length);
            int cursor = 0;
            for (int i = 0; i < _answers.Length; i++)
            {
                _answers[i].GetComponentInChildren<Text>().text = (i == _correctIndex ? correct : wrong[cursor++]).ToString();
            }
        }

        private static int WrongAnswer(int correct, int avoid)
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                int candidate = correct + UnityEngine.Random.Range(-4, 5);
                if (candidate > 0 && candidate != correct && candidate != avoid)
                {
                    return candidate;
                }
            }
            return correct + 1;
        }

        private void Answer(int index)
        {
            if (index != _correctIndex)
            {
                RollQuestion();
                return;
            }

            var passed = _onPassed;
            Hide();
            passed?.Invoke();
        }
    }
}
