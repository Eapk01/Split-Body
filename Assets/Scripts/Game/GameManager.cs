using UnityEngine;

public class GameManager : MonoBehaviour
{
    GameObject Child;
    GameObject Ghost;
    public static GameManager Instance;

    void Awake()
    {
        Instance = this;
    }

    public void RegisterPlayers(GameObject child, GameObject ghost)
    {
        Child = child;
        Ghost = ghost;
    }

    public bool TryGetChild(out GameObject child)
    {
        child = Child;
        return Child != null;
    }

    public bool TryGetGhost(out GameObject ghost)
    {
        ghost = Ghost;
        return Ghost != null;
    }
}
