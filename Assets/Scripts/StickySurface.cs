using UnityEngine;

public class StickySurface : MonoBehaviour
{
    private Rigidbody2D rb2d;

    private void Awake()
    {
        rb2d = GetComponent<Rigidbody2D>();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.rigidbody == null)
            return;

        collision.rigidbody.linearVelocity += rb2d.linearVelocity;
    }
    private void OnCollisionStay2D(Collision2D collision)
    {
        if (collision.rigidbody == null)
            return;

        collision.rigidbody.linearVelocity += rb2d.linearVelocity;
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        // No special handling needed when objects stop touching the platform.
    }

}
