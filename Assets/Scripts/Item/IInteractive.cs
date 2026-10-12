public interface IInteractive
{
    bool CanInteract { get; }
    void Interact(PlayerController player);
    string GetItemName();
}
