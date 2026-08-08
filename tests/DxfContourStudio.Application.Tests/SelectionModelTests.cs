#nullable enable

using DxfContourStudio.Application.Selection;
using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Application.Tests;

/// <summary>
/// Selection model behavior: single/ctrl/clear/select-all semantics, the
/// primary (focused) id and pruning of dangling ids.
/// </summary>
public class SelectionModelTests
{
    [Fact]
    public void SelectSingle_ReplacesSelectionAndSetsPrimary()
    {
        var s = new SelectionModel();
        s.Add(1);
        s.Add(2);

        s.SelectSingle(5);

        Assert.Equal(1, s.Count);
        Assert.True(s.IsSelected(5));
        Assert.False(s.IsSelected(1));
        Assert.Equal(5, s.PrimaryId);
    }

    [Fact]
    public void Add_AppendsWithoutReplacing()
    {
        var s = new SelectionModel();
        s.SelectSingle(1);

        s.Add(2);

        Assert.Equal(2, s.Count);
        Assert.True(s.IsSelected(1));
        Assert.True(s.IsSelected(2));
        Assert.Equal(2, s.PrimaryId);
    }

    [Fact]
    public void Toggle_AddsThenRemoves()
    {
        var s = new SelectionModel();
        Assert.True(s.Toggle(7));
        Assert.True(s.IsSelected(7));
        Assert.False(s.Toggle(7));
        Assert.False(s.IsSelected(7));
        Assert.Null(s.PrimaryId);
    }

    [Fact]
    public void Toggle_RemovingPrimaryFallsBackToFirst()
    {
        var s = new SelectionModel();
        s.Add(1);
        s.Add(2);
        s.Add(3);
        Assert.Equal(3, s.PrimaryId);

        s.Toggle(3);

        Assert.Equal(1, s.PrimaryId);
    }

    [Fact]
    public void Clear_EmptiesAndClearsPrimary()
    {
        var s = new SelectionModel();
        s.Add(1);

        s.Clear();

        Assert.Equal(0, s.Count);
        Assert.Null(s.PrimaryId);
    }

    [Fact]
    public void SelectAll_ReplacesSelection()
    {
        var s = new SelectionModel();
        s.Add(9);

        s.SelectAll([1, 2, 3]);

        Assert.Equal(3, s.Count);
        Assert.True(s.IsSelected(1) && s.IsSelected(2) && s.IsSelected(3));
        Assert.Equal(1, s.PrimaryId);
    }

    [Fact]
    public void ApplyClickPick_AdditiveToggles_PlainSelects()
    {
        var s = new SelectionModel();
        s.ApplyClickPick(1, additive: false);
        Assert.Equal([1L], s.Ids);

        s.ApplyClickPick(2, additive: true);
        Assert.Equal(2, s.Count);

        s.ApplyClickPick(1, additive: true);
        Assert.Equal(1, s.Count); // toggled off
        Assert.True(s.IsSelected(2));
    }

    [Fact]
    public void Remove_RemovesOnlyThatId()
    {
        var s = new SelectionModel();
        s.Add(1);
        s.Add(2);

        s.Remove(1);

        Assert.Single(s.Ids);
        Assert.True(s.IsSelected(2));
    }

    [Fact]
    public void Prune_DropsIdsWherePredicateIsFalse()
    {
        var s = new SelectionModel();
        s.Add(1);
        s.Add(2);
        s.Add(3);

        s.Prune(id => id % 2 == 0);

        Assert.Equal([2L], s.Ids);
    }

    [Fact]
    public void SelectionChanged_RaisedOnMutations()
    {
        var s = new SelectionModel();
        int count = 0;
        s.SelectionChanged += () => count++;

        s.SelectSingle(1);
        s.Add(2);
        s.Remove(1);
        s.Clear();

        Assert.Equal(4, count);
    }

    [Fact]
    public void Clear_WhenEmpty_DoesNotRaise()
    {
        var s = new SelectionModel();
        int count = 0;
        s.SelectionChanged += () => count++;

        s.Clear();

        Assert.Equal(0, count);
    }
}
