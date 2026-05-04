using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Unmasker : DiscoverableVisibilityTrigger
{
    protected override string StoragePrefix => "Unmasker.";
    protected override float InitialHiddenProgress => 1f;
    protected override float TriggeredHiddenProgress => 0f;

    public void Reveal()
    {
        Activate();
        Debug.Log("object revealed!");
    }

    public void Hide()
    {
        ResetVisibility();
    }
}
