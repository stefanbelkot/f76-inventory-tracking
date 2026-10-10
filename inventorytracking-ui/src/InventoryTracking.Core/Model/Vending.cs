namespace InventoryTracking.Core.Model;

/// <summary>
/// An item that is assigned to one of the player's CAMP machines. <paramref name="Machine"/> is the game's machine type
/// (1 is the player vending machine); <paramref name="Price"/> is the listed price per item and only exists for the vending machine.
/// <paramref name="OnOtherMachine"/> is true when the item is assigned to a different machine than the one that was open.
/// </summary>
public sealed record Vending(int Machine, long? Price, bool OnOtherMachine = false)
{
    public const int VendingMachine = 1;

    public static string MachineName(int machine) => machine switch
    {
        1 => "Vending machine",
        2 => "Display case",
        3 => "Dispenser",
        4 => "Fermenter",
        5 => "Refrigerator",
        6 => "Ally",
        7 => "Recharger",
        8 => "Freezer",
        9 => "Pet",
        _ => $"CAMP machine {machine}",
    };
}
