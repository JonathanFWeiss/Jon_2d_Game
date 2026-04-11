using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class SpikeBlock : MonoBehaviour
{
    [Tooltip("Cooldown between damage hits in seconds.")]
    public float damageCooldown = 1f;

    private float lastHitTime = -1f;

    void OnCollisionEnter2D(Collision2D collision)
    {
        DamagePlayerIfReady(collision.gameObject);
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        DamagePlayerIfReady(collision.gameObject);
    }

    void OnTriggerStay2D(Collider2D collision)
    {
        DamagePlayerIfReady(collision.gameObject);
    }

    void DamagePlayerIfReady(GameObject hitObject)
    {
        if (hitObject.CompareTag("Player") && Time.time >= lastHitTime + damageCooldown)
        {
            lastHitTime = Time.time;
            PlayerData.RemoveHP(1);
        }
    }
}
