using System;
using Xunit;
using xml_buzzsaw;
using xml_buzzsaw.utils;
using System.IO;

namespace xml_buzzsaw.tests
{
    public class IntegrationTests
    {
        private readonly GraphCacheService _cache;
        private readonly ILogger _logger;

        public IntegrationTests()
        {
            _logger = new SimpleLogger("integration-tests");
            _cache = new GraphCacheService(_logger);
        }

        [Fact]
        public void LoadCache_With_Valid_TopLevelPath()
        {
            string error = null;
            _cache.Load(Path.Combine(Environment.CurrentDirectory, "..\\..\\..\\data"), out error);
            Assert.Equal(error, String.Empty);
        }

        [Fact]
        public void LoadCache_With_InValid_TopLevelPath()
        {
            string error = null;

            // Load the cache from a malformed top level folder path
            //
            var load_results = _cache.Load("", out error);

            // Verify the load method populated the error string is not empty
            //
            Assert.NotEqual(error, String.Empty);

            // Verify the load method returned false
            //
            Assert.False(load_results);
        }
        
    }
}
