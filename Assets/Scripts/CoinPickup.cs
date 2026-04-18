using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class CoinPickup : MonoBehaviour
{
    [Tooltip("Number of coins granted when the hero touches this pickup.")]
    public int coinValue = 1;

    [Tooltip("Layer mask used to identify the player object.")]
    public LayerMask playerLayer;

    [Tooltip("Time in seconds before the coin can be picked up after spawning.")]
    public float pickupDelay = 5f;

    private float currentPickupDelay;

    void Awake()
    {
        currentPickupDelay = pickupDelay;
//        Debug.Log("Coin " + gameObject.name + " pickup delay: " + currentPickupDelay.ToString("F2") + " seconds");
    
    }

    void Update()
    {
        if (currentPickupDelay > 0)
        {
            currentPickupDelay -= Time.deltaTime;
        }
        //Debug.Log("Coin " + gameObject.name + " pickup delay: " + currentPickupDelay.ToString("F2") + " seconds");
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (currentPickupDelay > 0) return;


        GameObject other = collision.gameObject;

        // Check if the object is on the player layer
        if ((playerLayer.value & (1 << other.layer)) != 0)
        {
            PlayerData.AddCoins(coinValue);
            Debug.Log("Coin collected! Total coins: " + PlayerData.Coins);
            //Debug.Log("currentPickupDelay: " + currentPickupDelay.ToString("F2") + " seconds");
            Destroy(gameObject);
        }

    }
}
