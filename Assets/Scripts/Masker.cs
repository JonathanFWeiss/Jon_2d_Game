using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Masker : DiscoverableVisibilityTrigger
{
    protected override string StoragePrefix => "Masker.";
    protected override float InitialHiddenProgress => 0f;
    protected override float TriggeredHiddenProgress => 1f;

    public void Uncover()
    {
        Activate();
    }

    public void Cover()
    {
        ResetVisibility();
    }
}
