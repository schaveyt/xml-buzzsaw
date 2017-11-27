# xml-buzzsaw xml graph library

.NET Core library to slice-n-dice XML forests. 

The library provides a smart graph cache that will:

- Load a directory structure filled with Xml files and extract the graph data structure
- Monitor the loaded directory structure for changes and reload when changes are made

## Usage

~~~csharp

var cache = new GraphCacheService(new SimpleLogger());
cache.Load("/path/where/all/my/xml/files/are/at);

GraphElement element;
cache.Elements.TryGet("id-of-element-i-am-looking-for", out element);

Console.WriteLine("The 'Name' attribute of the element is " + element.Attributes["Name"]);

~~~
