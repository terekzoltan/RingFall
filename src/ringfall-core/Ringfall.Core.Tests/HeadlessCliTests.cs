using Ringfall.Core;

namespace Ringfall.Core.Tests;

[TestClass]
public sealed class HeadlessCliTests
{
    [TestMethod]
    public void Shell_exposes_only_the_approved_commands()
    {
        CollectionAssert.AreEqual(new[] { "--help", "--version" }, CoreInfo.SupportedCommands);
    }
}
