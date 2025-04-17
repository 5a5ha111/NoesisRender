using UnityEngine;
using UnityEngine.SceneManagement;

namespace NoesisRender.Portals
{
    public class ScenePortalManager : MonoBehaviour
    {
        public static ScenePortalManager instance;

        Portal[] portals;

        void OnEnable()
        {
            SceneManager.sceneLoaded += OnLevelFinishedLoading;
            instance = this;
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= OnLevelFinishedLoading;
        }

        void OnLevelFinishedLoading(Scene scene, LoadSceneMode mode)
        {
            Debug.Log("Level Loaded");
            Debug.Log(scene.name);
            //Debug.Log(mode);

            portals = FindObjectsOfType<Portal>(true);
        }

        public Portal[] RequestPortals()
        {
            return portals;
        }


        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {

        }
    }

}
