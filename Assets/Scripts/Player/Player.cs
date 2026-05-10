using UnityEngine;
using UnityEngine.Events;

public class Player : MonoBehaviour
{
    float MaxHealth = 100;
    public UnityEvent<float> OnDamage;
    protected float health;

    float regenDelay = 0.25f;      // time after last damage
    float regenRate = 10f;      // health per second

    float regenTimer;

    void Start()
    {
        health = MaxHealth;
        transform.position = LocalMultiplayerSpawner.Transform.position;
    }

    virtual protected void Update()
    {
        HandleRegen(Time.deltaTime);
    }

    public void TakeDamage(float amount)
    {
        health = Mathf.Max(0, health - amount);
        OnDamage?.Invoke(health);

        regenTimer = regenDelay; // reset cooldown

        if(health == 0)
        {
            Die();
        }
    }

    void HandleRegen(float dt)
    {
        // Countdown after last damage
        if (regenTimer > 0f)
        {
            regenTimer -= dt;
            return;
        }

        // Regen once delay is over
        if (health < MaxHealth)
        {
            health = Mathf.Min(MaxHealth, health + regenRate * dt);
            OnDamage?.Invoke(health);
        }
    }

    void Die()
    {
        print("Aw!");
    }
}
