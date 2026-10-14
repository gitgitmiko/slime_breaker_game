using UnityEngine;
using UnityEngine.SceneManagement;

namespace SlimeBreaker
{
    public class BootLoader : MonoBehaviour
    {
        void Start()
        {
            SceneManager.LoadScene("MainMenu");
        }
    }
}
