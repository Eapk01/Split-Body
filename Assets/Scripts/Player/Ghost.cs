using UnityEngine;

public class Ghost : Player
{
    Renderer rend;
    Material materialInstance;

    void Awake()
    {
        rend = GetComponentInChildren<Renderer>();
        materialInstance = rend.sharedMaterial;
    }

    override protected void Update()
    {
        base.Update();
        materialInstance.SetFloat("_Health", health);
    }
}