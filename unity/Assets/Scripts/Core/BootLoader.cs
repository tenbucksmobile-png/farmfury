using UnityEngine;
using UnityEngine.SceneManagement;

namespace FarmFury.Core
{
    /// <summary>
    /// The only thing in Boot.unity, the first scene in the build: loads the game. Android's Split Application Binary
    /// keeps the first scene and everything it references in the base module (Google Play caps it at 200 MB), and
    /// Game.unity references all the art - with this tiny scene first, the game's data goes into the install-time
    /// asset pack. Ported from FarmFury_Stampede's BootLoader (2026-11-15).
    /// </summary>
    public class BootLoader : MonoBehaviour
    {
        public const string GameScene = "Game";

        private void Start() => SceneManager.LoadScene(GameScene);
    }
}
