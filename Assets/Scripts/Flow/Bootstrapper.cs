using UnityEngine;

namespace SSRP.Flow
{
    /// <summary>[D1] Boot scene entry point: create the persistent services, then go to the main menu.</summary>
    public class Bootstrapper : MonoBehaviour
    {
        private void Start()
        {
            GameServices.Ensure();
            _ = MatchSession.Config;
            SceneLoader.Load(SceneNames.MainMenu);
        }
    }
}
