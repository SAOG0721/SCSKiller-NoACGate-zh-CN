using SCSKiller.Core;
using SCSKiller.Core.Vendors;

namespace SCSKiller.Tests.Platform;

public partial class AppTests
{
    [Theory]
    [InlineData(GpuVendor.Nvidia)]
    [InlineData(GpuVendor.Amd)]
    public async Task Changing_the_saved_gpu_choice_keeps_the_running_adapter_until_restart(GpuVendor nextVendor)
    {
        var current = Listed(Gpu with { Vendor = GpuVendor.Nvidia });
        var next = Listed(Gpu with { Vendor = nextVendor, Name = "Next GPU", AdapterLuid = 2 }, device: 0x30);
        var k = Killer(new FakeReader(Unreal), vendor: new FakeVendor(current.Gpu));
        k.Adapters = () => [current, next];
        await k.ScanAsync(default);
        await WarmOnce(k, _game.Id);
        var driver = k.DriverId;

        k.Settings = k.Settings with { GpuChoice = GpuBackends.Id(next) };
        await k.ScanAsync(default);

        Assert.Equal(current.Gpu, k.Vendor.Gpu);
        Assert.Equal(driver, k.DriverId);
        Assert.Equal(GameStatus.Warmed, k.Games.Single().Status);
        Assert.Equal(GpuBackends.Id(next), k.Store.LoadSettings().GpuChoice);
    }

    [Fact]
    public void A_saved_gpu_choice_keeps_its_luid_when_matching_adapters_reorder()
    {
        var current = Listed(Gpu with { Vendor = GpuVendor.Nvidia });
        var duplicate = current with { Gpu = current.Gpu with { AdapterLuid = 2 } };
        var k = Killer(vendor: new FakeVendor(current.Gpu));
        k.Settings = k.Settings with { GpuChoice = GpuBackends.Id(current) };
        k.Adapters = () => [current, duplicate];
        k.RedetectGpu();

        k.Adapters = () => [duplicate, current];

        Assert.False(k.RedetectGpu());
        Assert.Equal(current.Gpu, k.Vendor.Gpu);
    }
}
