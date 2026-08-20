using CccdCheckIn.Core;
using Xunit;

namespace CccdCheckIn.Tests;

public class DuplicateScanGuardTests
{
    [Fact]
    public void FirstScan_NotDuplicate()
    {
        var g = new DuplicateScanGuard(TimeSpan.FromSeconds(5));
        Assert.False(g.IsDuplicate("094083123451"));
        Assert.False(g.IsDuplicate("094083123452"));
    }

    [Fact]
    public void SameCardWithinWindow_IsDuplicate()
    {
        var g = new DuplicateScanGuard(TimeSpan.FromSeconds(5));
        g.IsDuplicate("094083123451");
        Assert.True(g.IsDuplicate("094083123451"));
        Assert.True(g.IsDuplicate("094083123451"));
    }

    [Fact]
    public void SameCardAfterWindow_NotDuplicate()
    {
        var g = new DuplicateScanGuard(TimeSpan.FromMilliseconds(1));
        g.IsDuplicate("094083123451");
        Thread.Sleep(20);
        Assert.False(g.IsDuplicate("094083123451"));
    }

    [Fact]
    public void DifferentCards_NotDuplicate()
    {
        var g = new DuplicateScanGuard(TimeSpan.FromSeconds(5));
        g.IsDuplicate("094083123451");
        for (var i = 0; i < 10; i++)
            Assert.False(g.IsDuplicate($"0940831234{99 - i}"));
    }

    [Fact]
    public void EmptyNumber_NeverDuplicate()
    {
        var g = new DuplicateScanGuard(TimeSpan.FromSeconds(5));
        Assert.False(g.IsDuplicate(""));
        Assert.False(g.IsDuplicate(null!));
    }

    [Fact]
    public void Clear_ResetsState()
    {
        var g = new DuplicateScanGuard(TimeSpan.FromSeconds(5));
        g.IsDuplicate("094083123451");
        Assert.True(g.IsDuplicate("094083123451"));
        g.Clear();
        Assert.False(g.IsDuplicate("094083123451"));
    }

    [Fact]
    public void SetWindow_TakesEffectWithoutRestart()
    {
        var g = new DuplicateScanGuard(TimeSpan.FromSeconds(5));
        g.IsDuplicate("094083123451");

        // Đổi cửa sổ về 0 lúc chạy → quét lại cùng thẻ phải được tính là lượt mới ngay.
        g.SetWindow(TimeSpan.Zero);
        Assert.False(g.IsDuplicate("094083123451"));
    }
}