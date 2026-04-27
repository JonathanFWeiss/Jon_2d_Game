using UnityEngine;

public class GroundPersuerEnemy : GroundWalkerEnemy
{
    protected Transform playerTransform;
    protected float nextPlayerSearchTime = float.NegativeInfinity;

    protected override void Awake()
    {
        base.Awake();

        ResolvePlayerTransform();
    }

    protected override void FixedUpdate()
    {
        if (isDead) return;

        FacePlayerIfBehind();
        base.FixedUpdate();
    }

    protected virtual void ResolvePlayerTransform()
    {
        if (playerTransform != null)
            return;

        if (Time.time < nextPlayerSearchTime)
            return;

        nextPlayerSearchTime = Time.time + 0.5f;

        JonCharacterController jonCharacter = FindAnyObjectByType<JonCharacterController>();
        if (jonCharacter != null)
        {
            playerTransform = jonCharacter.transform;
            return;
        }

        Hero hero = FindAnyObjectByType<Hero>();
        if (hero != null)
        {
            playerTransform = hero.transform;
        }
    }

    protected virtual void FacePlayerIfBehind()
    {
        ResolvePlayerTransform();

        if (playerTransform == null)
            return;

        float directionToPlayer = playerTransform.position.x - transform.position.x;

        if (Mathf.Approximately(directionToPlayer, 0f))
            return;

        if (Mathf.Sign(directionToPlayer) != Mathf.Sign(facingDirection))
        {
            TurnAround();
        }
    }
}
