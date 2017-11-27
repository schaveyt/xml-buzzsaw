
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using xml_buzzsaw.utils;

namespace xml_buzzsaw.xml
{
    public static class XmlToGraphTranslator
    {
        /// <summary>
        /// Translates a folder structure of xml to GraphElements
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="topLevelFolder"></param>
        /// <param name="concurrentCollection"></param>
        public static bool TranslateToCache(ILogger logger, string topLevelFolder, 
            ConcurrentDictionary<string, GraphElement> concurrentCollection)
        {
            bool errorsFound = false;

            // In parallel, process each file in a folder structure
            //
            FileSystemUtils.TraverseTreeParallelForEach(topLevelFolder, (filePath) => 
            {
                try
                {
                    // Process the file extracting any GraphElements...
                    //
                    var extractedGraphXmlElements = ProcessFile(filePath);

                    // Add the extracted graph elements to the concurrent collection
                    //
                    foreach (var element in extractedGraphXmlElements)
                    {
                        if (!concurrentCollection.TryAdd(element.Id, element))
                        {
                            errorsFound = true;
                            logger.Error("3048", $"Encountered duplicate id '{element.Id}' in cache while processing file '{filePath}'");
                        }
                    }
                }
                catch (Exception e)
                {
                    logger.Error("3054", $"Exception occurred while processing file '{filePath}'. Exception: {e.Message}");
                    errorsFound = true;
                }

            });

            return !errorsFound;
        }

        private static List<GraphElement> ProcessFile(string filePath)
        {
            var results = new List<GraphElement>();

            XDocument doc = XDocument.Load(filePath, LoadOptions.SetLineInfo);

            // For now just grab all elements that have a Guid xml attribute
            //
            // This can be made configurable by allowing the user to specify an xpath condition to specify this.
            //
            var xmlElementsWithIdentifiers = doc.Descendants().Where(e => e.Attribute(StrLits.Guid) != null);

            foreach (var xmlElementWithId in xmlElementsWithIdentifiers)
            {
                try
                {
                    var graphElement = CreateGraphXmlElement(xmlElementWithId, filePath);

                    // Loop through all the xml child element to extract the rest of the info
                    //
                    foreach (var xmlChild in xmlElementWithId.Elements())
                    {
                        // extract the reference elements. For now these can be elements whose names end in "Ref"
                        //
                        // This can be made configurable by allowing the user to specify an xpath condition to specify this.
                        //
                        if (xmlChild.Name.LocalName.EndsWith(StrLits.Ref))
                        {
                            var referenceId = xmlChild.Attribute(StrLits.RefId);
                            var referenceDirection = xmlChild.Attribute(StrLits.RefDirection);

                            if (referenceId == null || referenceDirection == null)
                            {
                                continue;
                            }

                            if (referenceDirection.Value == StrLits.InDirection)
                            {
                                List<string> list;
                                if (!graphElement.InRefernceIdentifiers.TryGetValue(xmlChild.Name.LocalName, out list))
                                {
                                    list = new List<string>();
                                    graphElement.InRefernceIdentifiers.Add(xmlChild.Name.LocalName, list);
                                }
                                list.Add(referenceId.Value);
                            }
                            else if (referenceDirection.Value == StrLits.OutDirection)
                            {
                                List<string> list;
                                if (!graphElement.OutRefernceIdentifiers.TryGetValue(xmlChild.Name.LocalName, out list))
                                {
                                    list = new List<string>();
                                    graphElement.OutRefernceIdentifiers.Add(xmlChild.Name.LocalName, list);
                                }
                                list.Add(referenceId.Value);
                            }
                        }
                        else
                        {
                            var xmlChildIdentifierAttribute = xmlChild.Attribute(StrLits.Guid);
                            if (xmlChildIdentifierAttribute == null)
                            {
                                continue;
                            }

                            graphElement.ChildrenIdenifiers.Add(xmlChildIdentifierAttribute.Value);
                        }
                    }

                    results.Add(graphElement);
                }
                catch (Exception e)
                {
                    var lineNum = ((IXmlLineInfo) xmlElementWithId).HasLineInfo() ? ((IXmlLineInfo)xmlElementWithId).LineNumber : -1;
                    var msg = $"While processing line {lineNum} in file {filePath}: encountered the following exception:\n{e.Message}";
                    throw new Exception(msg);
                } 
            }

            return results;
        }

        private static GraphElement CreateGraphXmlElement(XElement xmlElement, string file)
        {
            var graphElement = new GraphElement();

            graphElement.Id = xmlElement.Attribute(StrLits.Guid).Value;
            graphElement.Uri = file;
            graphElement.LineNum = ((IXmlLineInfo) xmlElement).HasLineInfo() ? ((IXmlLineInfo)xmlElement).LineNumber : -1;
            graphElement.XmlElementName = xmlElement.Name.LocalName;

            if (xmlElement.Parent != null)
            {
                var parentId = xmlElement.Parent.Attribute(StrLits.Guid);
                if (parentId != null)
                {
                    graphElement.ParentId = parentId.Value;
                }
            }

            foreach (var attribute in xmlElement.Attributes())
            {
                if (attribute.Name.LocalName == StrLits.Guid)
                {
                    continue;
                }

                if (!graphElement.Attributes.ContainsKey(attribute.Name.LocalName))
                {
                    graphElement.Attributes.Add(attribute.Name.LocalName, attribute.Value);
                }
            }

            return graphElement;
        }
    }
}