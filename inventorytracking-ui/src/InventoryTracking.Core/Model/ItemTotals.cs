namespace InventoryTracking.Core.Model;

/// <summary>Totals computed over a set of item rows.</summary>
public sealed record ItemTotals(int Rows, long TotalCount, double TotalWeight, int LegendaryCount, long TotalValue)
{
    public static ItemTotals Of(IEnumerable<Item> items)
    {
        int rows = 0, legendary = 0;
        long count = 0, value = 0;
        double weight = 0;
        foreach (var item in items)
        {
            rows++;
            count += item.Count;
            weight += item.TotalWeight;
            value += item.TotalValue;
            if (item.Legendary)
            {
                legendary++;
            }
        }

        return new ItemTotals(rows, count, Math.Round(weight, 2), legendary, value);
    }
}
