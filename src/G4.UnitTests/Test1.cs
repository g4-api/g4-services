using G4.Cache;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;

namespace G4.UnitTests
{
    [TestClass]
    public sealed class Test1
    {
        [TestMethod, TestCategory("ExportData"), ExpectedException(typeof(TypeInitializationException))]
        public void TestMethod1()
        {
            var cache = CacheManager.Instance;
        }
    }
}
