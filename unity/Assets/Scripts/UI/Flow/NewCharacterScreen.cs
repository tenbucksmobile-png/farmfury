using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using FarmFury.Shared.UI;

namespace FarmFury.UI
{
    /// <summary>
    /// The New Character page over the Level Complete results (franchise convention, ported from Stampede's
    /// NewCharacterScreen): one page per animal a boss clear just made available (the next world's animals —
    /// GameManager.RunState.newlyUnlockedAnimals), each its framed card flashing in over turning rays with
    /// confetti. Tap anywhere for the next animal; after the last, back to the results.
    /// </summary>
    public class NewCharacterScreen : OverlayScreen
    {
        // This game's MenuArt (the shared base holds it as MenuArtBase).
        private new MenuArt Art => (MenuArt)base.Art;

        private readonly NewCharacterPage _page;
        private readonly Queue<AnimalType> _queue = new();

        public NewCharacterScreen(Transform canvas, MenuArt art, ShopArt shop) : base(canvas, "NewCharacter", art, shop, posterBackdrop: false)
        {
            BackButton.gameObject.SetActive(false);
            var rootImage = Root.GetComponent<Image>();
            var tap = Root.AddComponent<Button>();
            tap.targetGraphic = rootImage;
            tap.transition = Selectable.Transition.None;
            tap.onClick.AddListener(Next);

            _page = new NewCharacterPage(Rect, Art, CannonfireMenuData.CurrentWorldNewCharacterBackground);
            _page.Frame.SetAsFirstSibling();
            rootImage.color = _page.HasBackdrop ? Color.black : UIKit.Dark;
            Root.AddComponent<RaysSpinner>().Page = _page;
        }

        public void ShowUnlocks(IEnumerable<AnimalType> animals)
        {
            _queue.Clear();
            foreach (var a in animals) { _queue.Enqueue(a); }
            if (_queue.Count == 0) { return; }
            _page.UseWorldBackdrop();
            Show();
            ShowNext();
        }

        private void Next()
        {
            if (_queue.Count > 0) { ShowNext(); } else { Hide(); }
        }

        private void ShowNext()
        {
            var animal = _queue.Dequeue();
            _page.SetCharacter(Art.AnimalCard(animal), animal.ToString());
            _page.Burst();
        }

        // Turns the page's rays while the screen is showing (OverlayScreen isn't a MonoBehaviour).
        private class RaysSpinner : MonoBehaviour
        {
            [System.NonSerialized] public NewCharacterPage Page;
            private void LateUpdate() => Page?.Spin();
        }
    }
}
