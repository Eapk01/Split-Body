using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelTransition : MonoBehaviour
{
    [SerializeField] string nextSceneName;

    bool childInZone = false;
    bool ghostInZone = false;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Child"))
            childInZone = true;

        if (other.CompareTag("Ghost"))
            ghostInZone = true;

        if (childInZone && ghostInZone)
            LoadNextLevel();
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Child"))
            childInZone = false;

        if (other.CompareTag("Ghost"))
            ghostInZone = false;
    }

    void LoadNextLevel()
    {
        SceneManager.LoadScene(nextSceneName);
    }
}
