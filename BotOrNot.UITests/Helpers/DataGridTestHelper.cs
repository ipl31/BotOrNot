using System.Collections;
using System.Collections.ObjectModel;
using System.Reflection;
using global::Avalonia.Collections;
using global::Avalonia.Controls;
using BotOrNot.Avalonia.ViewModels;
using BotOrNot.Avalonia.Views;
using BotOrNot.Core.Models;

namespace BotOrNot.UITests.Helpers;

public static class DataGridTestHelper
{
    public static MainWindow CreateMainWindowWithData(
        ObservableCollection<PlayerRow> players,
        ObservableCollection<PlayerRow> ownerEliminations)
    {
        var window = new MainWindow();
        window.Show();

        var vm = (MainWindowViewModel)window.DataContext!;

        var allPlayersField = typeof(MainWindowViewModel)
            .GetField("_allPlayers", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var allOwnerElimsField = typeof(MainWindowViewModel)
            .GetField("_allOwnerEliminations", BindingFlags.NonPublic | BindingFlags.Instance)!;

        var allPlayers = (List<PlayerRow>)allPlayersField.GetValue(vm)!;
        var allOwnerElims = (List<PlayerRow>)allOwnerElimsField.GetValue(vm)!;

        allPlayers.Clear();
        allOwnerElims.Clear();

        foreach (var p in players) allPlayers.Add(p);
        foreach (var p in ownerEliminations) allOwnerElims.Add(p);

        // Trigger filter to populate the displayed collections.
        vm.FilterText = " ";
        vm.FilterText = "";

        return window;
    }

    public static DataGrid GetDataGrid(MainWindow window, string name)
    {
        return window.FindControl<DataGrid>(name)
            ?? throw new InvalidOperationException($"DataGrid '{name}' not found");
    }

    /// <summary>
    /// Simulates clicking a column header by:
    /// 1. Firing the DataGrid's Sorting event (which sets CustomSortComparer on the column)
    /// 2. Applying the sort via the CollectionView's SortDescriptions
    ///
    /// The comparer created by the MainWindow's Sorting handler is designed for the
    /// DataGrid's internal sort, which negates comparison results for descending sort.
    /// Since we apply sorting via SortDescriptions (always ascending), we detect
    /// descending comparers and wrap them in a negating adapter to produce the correct order.
    /// </summary>
    public static void ClickColumnHeader(Window window, DataGrid dataGrid, string headerText)
    {
        var column = dataGrid.Columns.FirstOrDefault(c => c.Header?.ToString() == headerText)
            ?? throw new InvalidOperationException(
                $"Column '{headerText}' not found. Available: " +
                string.Join(", ", dataGrid.Columns.Select(c => c.Header?.ToString() ?? "(null)")));

        // Step 1: Fire the Sorting event via OnColumnSorting (sets CustomSortComparer)
        var args = new DataGridColumnEventArgs(column);
        var onColumnSorting = typeof(DataGrid).GetMethod(
            "OnColumnSorting",
            BindingFlags.NonPublic | BindingFlags.Instance);
        onColumnSorting!.Invoke(dataGrid, new object[] { args });

        // Step 2: Apply the sort via the CollectionView
        if (column.CustomSortComparer != null)
        {
            var cv = dataGrid.CollectionView;
            if (cv != null)
            {
                // Check if the comparer is in descending mode (pre-inverted for DataGrid negation)
                var isDescending = IsComparerDescending(column.CustomSortComparer);
                IComparer comparer = isDescending
                    ? new NegatingComparer(column.CustomSortComparer)
                    : (IComparer)column.CustomSortComparer;

                var sd = DataGridSortDescription.FromComparer(comparer);
                cv.SortDescriptions.Clear();
                cv.SortDescriptions.Add(sd);
            }
        }
    }

    /// <summary>
    /// Gets the currently displayed values from the DataGrid's CollectionView,
    /// which reflects the sorted/filtered order.
    /// </summary>
    public static List<string?> GetDisplayedValues(DataGrid dataGrid, Func<PlayerRow, string?> selector)
    {
        var collectionView = dataGrid.CollectionView;
        if (collectionView == null)
            throw new InvalidOperationException("DataGrid CollectionView is null");

        return collectionView.Cast<PlayerRow>().Select(selector).ToList();
    }

    private static bool IsComparerDescending(object comparer)
    {
        var descendingField = comparer.GetType().GetField("_descending",
            BindingFlags.NonPublic | BindingFlags.Instance);
        return descendingField != null && (bool)(descendingField.GetValue(comparer) ?? false);
    }

    /// <summary>
    /// Wraps a comparer and negates its result, simulating the DataGrid's
    /// descending sort behavior.
    /// </summary>
    private sealed class NegatingComparer : IComparer
    {
        private readonly object _inner;
        public NegatingComparer(object inner) => _inner = inner;

        public int Compare(object? x, object? y)
        {
            if (_inner is IComparer comparer)
                return -comparer.Compare(x, y);
            return 0;
        }
    }
}
