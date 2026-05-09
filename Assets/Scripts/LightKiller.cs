using UnityEngine;

public class LightKiller : MonoBehaviour
{
    public Transform Source;
    float length;

    void Start()
    {
        length = Source.GetComponent<Light>().range;
    }

    void Update()
    {
        if(!GameManager.Instance.TryGetGhost(out var ghost)) return;
        Vector3 direction = ghost.transform.position - Source.position;
        Debug.DrawRay(Source.position, direction.normalized * length, Color.red);
        if(Physics.Raycast(Source.position, direction, out var hit, length))
        {
            if(hit.transform == ghost.transform)
            {
                ghost.GetComponent<Player>().TakeDamage(Time.deltaTime * 50);
            }
        }
    }
}
