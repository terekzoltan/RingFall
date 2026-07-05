using Ringfall.Core;

[assembly: DoNotParallelize]

namespace Ringfall.Core.Tests;

[TestClass]
public sealed class CoreInfoTests
{
    [TestMethod]
    public void Metadata_is_stable_and_shell_only()
    {
        Assert.AreEqual("Ringfall Headless", CoreInfo.ProductName);
        Assert.AreEqual("0.1.0-shell", CoreInfo.Version);
        Assert.AreEqual("core/headless shell only", CoreInfo.ShellStatus);
    }
}
