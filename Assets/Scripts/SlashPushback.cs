using System;
using System.Reflection;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class SlashPushback : MonoBehaviour
{
    [Tooltip("Impulse force applied to any Rigidbody2D hit by the slash.")]
    public float pushForce = 20f;

    [Tooltip("When enabled, only horizontal pushback is applied.")]
    public bool horizontalOnly = true;

    [Tooltip("Amount of HP removed from NPCs when hit.")]
    public int damage = 1;

    Rigidbody2D ownerRigidbody;
    Hero ownerHero;

    void Awake()
    {
        ownerRigidbody = GetComponentInParent<Rigidbody2D>();
        ownerHero = GetComponentInParent<Hero>();
        Debug.Log("SlashPushback initialized. Owner Rigidbody: " + (ownerRigidbody != null ? ownerRigidbody.name : "None"));
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryPush(other.attachedRigidbody, other.gameObject);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        
            TryPush(collision.rigidbody, collision.gameObject);
        
    }

    void TryPush(Rigidbody2D otherRb, GameObject otherObject)
    {
        if (otherRb == null || otherRb == ownerRigidbody || otherRb.bodyType == RigidbodyType2D.Kinematic)
            return;

        if (ownerHero != null && !ownerHero.isAttacking && !ownerHero.isPogoing)
            return;

        GameObject targetObject = otherRb.gameObject;
        Debug.Log("Hit: " + targetObject.name);

        bool isNpc = IsNpcSortingLayer(targetObject);
        Debug.Log("Is NPC sorting layer: " + isNpc);

        if (isNpc)
        {
            TryRemoveHp(targetObject);

            Vector2 direction = otherRb.position - (Vector2)transform.position;
            if (horizontalOnly)
                direction.y = 0;

            if (direction.sqrMagnitude < 0.001f)
            {
                direction = Vector2.right * Mathf.Sign(transform.lossyScale.x);
                if (direction.x == 0)
                    direction = Vector2.right;
            }

            direction.Normalize();
            otherRb.AddForce(direction * pushForce, ForceMode2D.Impulse);

            if (ownerHero != null && ownerHero.isPogoing && ownerRigidbody != null)
            {
                var heroVel = ownerRigidbody.linearVelocity;
                heroVel.y = ownerHero.JumpVerticalSpeed;
                ownerRigidbody.linearVelocity = heroVel;
            }
        }
    }

    bool IsNpcSortingLayer(GameObject obj)
    {
        var renderer = obj.GetComponentInChildren<Renderer>();
        return renderer != null && string.Equals(renderer.sortingLayerName, "NPCs", StringComparison.OrdinalIgnoreCase);
    }

    void TryRemoveHp(GameObject obj)
    {
        Debug.Log("Trying to remove HP from: " + obj.name);
        foreach (var component in obj.GetComponents<Component>())
        {
            if (component == null)
                continue;

            var type = component.GetType();
            const BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;

            var field = type.GetField("hp", bindingFlags);
            if (field != null)
            {
                if (field.FieldType == typeof(int))
                {
                    int beforeHp = (int)field.GetValue(component);
                    int afterHp = beforeHp - damage;
                    field.SetValue(component, afterHp);
                    Debug.Log($"Hit object ID={obj.GetEntityId()} hp before={beforeHp} hp after={afterHp}");
                    return;
                }
                if (field.FieldType == typeof(float))
                {
                    float beforeHp = (float)field.GetValue(component);
                    float afterHp = beforeHp - damage;
                    field.SetValue(component, afterHp);
                    Debug.Log($"Hit object ID={obj.GetEntityId()} hp before={beforeHp} hp after={afterHp}");
                    return;
                }
            }

            var property = type.GetProperty("hp", bindingFlags);
            if (property != null && property.CanRead && property.CanWrite)
            {
                if (property.PropertyType == typeof(int))
                {
                    int beforeHp = (int)property.GetValue(component);
                    int afterHp = beforeHp - damage;
                    property.SetValue(component, afterHp);
                    Debug.Log($"Hit object ID={obj.GetEntityId()} hp before={beforeHp} hp after={afterHp}");
                    return;
                }
                if (property.PropertyType == typeof(float))
                {
                    float beforeHp = (float)property.GetValue(component);
                    float afterHp = beforeHp - damage;
                    property.SetValue(component, afterHp);
                    Debug.Log($"Hit object ID={obj.GetEntityId()} hp before={beforeHp} hp after={afterHp}");
                    return;
                }
            }
        }
    }
}
