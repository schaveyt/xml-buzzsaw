using System;
using System.IO;
using Xunit;
using xml_buzzsaw;
using xml_buzzsaw.utils;
using Xunit.Abstractions;
using System.Linq;

namespace xml_buzzsaw.tests
{
    public class IntegrationTests
    {
        private readonly ILogger _logger;

        private readonly GraphCacheService _cache;

        private readonly string _testDataFolder;

        public IntegrationTests(ITestOutputHelper output)
        {
            _logger = new XTestLogger(output);
            _testDataFolder = Path.Combine(Environment.CurrentDirectory, "..\\..\\..\\data");
            _cache = new GraphCacheService(_logger);
        }

        [Fact]
        public void Test_Loading_Cache_With_Valid_TopLevelPath()
        {
            var isSuccess = _cache.Load(_testDataFolder, true);
            Assert.True(isSuccess);
            Assert.Equal(_cache.Count, 11);
        }

        [Fact]
        public void Test_Loading_Cache_With_InValid_TopLevelPath()
        {
            // Load the cache from a malformed top level folder path
            //
            var isSuccess = _cache.Load("", true);

            // Verify the load method returned false
            //
            Assert.False(isSuccess);
        }

        [Fact]
        public void Test_A_Loaded_Cache_GetElementById()
        {
            _cache.Load(_testDataFolder, true);          
            
            GraphElement element;
            var isSuccess = _cache.Elements.TryGetValue("abc-123", out element);

            Assert.True(isSuccess);
            Assert.NotNull(element);
            Assert.Equal(element.Id, "abc-123");
            Assert.True(element.Attributes.ContainsKey("Name"));
            Assert.Equal(element.Attributes["Name"], "Papa John's");
        }

        [Fact]
        public void Test_Loaded_Cache_Element_Incoming_References()
        {
            _cache.Load(_testDataFolder, true);          
            
            GraphElement element;
            var isSuccess = _cache.Elements.TryGetValue("abc-123", out element);

            var grandpa = _cache.Elements["abc-123"];
            Assert.NotEmpty(grandpa.OutgoingElements["Father"]);

            var child = grandpa.OutgoingElements["Father"].First();
            var msg = $"{grandpa.Attributes["Name"]} is the father of {child.Attributes["Name"]}";
            
            Assert.Equal(msg, "Papa John's is the father of Mike Brady");
        }
        
    }
}
