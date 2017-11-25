using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using xml_buzzsaw.utils;

namespace xml_buzzsaw
{
    public class XmlGraphService
    {
        #region Fields

        private ConcurrentDictionary<string, GraphXmlElement> _elementsById;

        private static XmlGraphService _instance;

        private string _logContext = "XmlGraphService";

        #endregion

        #region Constructors

        public XmlGraphService()
        {
            WorkspaceHasChangedSinceLastLoad = true;
            Reset();
        }

        #endregion

        #region Properties

        public static XmlGraphService Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new XmlGraphService();
                }
                return _instance;
            }
        }

        public int Count
        {
            get
            {
                return _elementsById.Count;
            }
        }

        public IDictionary<string, GraphXmlElement> Cache
        {
            get
            {
                return _elementsById;
            }
        }


        private FileSystemWatcher FsWatcher
        {
            get;
            set;
        }

        private string TopLevelFolder
        {
            get;
            set;
        }

        private bool WorkspaceHasChangedSinceLastLoad
        {
            get;
            set;
        }


        #endregion

        #region Methods

        public bool Load(string topLevelFolder, out string error, bool forceReload = false)
        {
            error = String.Empty;

            if (String.IsNullOrWhiteSpace(topLevelFolder))
            {
                error = "The provided top level folder is either null or only whitespace.";
                return false;
            }

            // Skip reloading the cache if nothing on the file system has change and
            // the user is not asking to force a reload.
            //
            if (!WorkspaceHasChangedSinceLastLoad && !forceReload)
            {
                return true;
            }

            //
            // Now start the loading process
            //
            WorkspaceHasChangedSinceLastLoad = false;
            SetupFileSystemWatchers(topLevelFolder);
            Reset();

            try
            {
                FileSystemUtils.TraverseTreeParallelForEach(topLevelFolder, ParallelMainBody);
            }
            catch (Exception e)
            {
                Log.Error(_logContext, "9494", $"While loading the graph cache the following execption was thrown: {e.Message}");
                return false;
            }

            return true;
        }

        private void SetupFileSystemWatchers(string topLevelFolder)
        {
            if (TopLevelFolder == topLevelFolder)
            {
                return;
            }

            TopLevelFolder = topLevelFolder;
            InitializeFileSystemWatcher();
        }

        private void ParallelMainBody(string filePath)
        {
            try
            {
                var extractedGraphXmlElements = ExtractFromFile(filePath);

                foreach (var element in extractedGraphXmlElements)
                {
                    _elementsById.TryAdd(element.Id, element);
                }
            }
            catch (Exception)
            {
                // all exceptions are no-ops for now
                // TODO - impelement execptions.
            }

            // Perform resolution processing in parallel
            //
            ParallelForEach(_logContext, _elementsById, Resolve);
        }

        private void InitializeFileSystemWatcher()
        {
            if (String.IsNullOrWhiteSpace(TopLevelFolder))
            {
                return;
            }

            FsWatcher = null;
            FsWatcher = new FileSystemWatcher();
            FsWatcher.Filter = "*.xml";
            FsWatcher.NotifyFilter = NotifyFilters.LastAccess |
                                     NotifyFilters.LastWrite |
                                     NotifyFilters.FileName |
                                     NotifyFilters.DirectoryName;

            FsWatcher.IncludeSubdirectories = true;
            FsWatcher.Path = TopLevelFolder;

            FsWatcher.Changed += new FileSystemEventHandler(OnFsChanged);
            FsWatcher.Created += new FileSystemEventHandler(OnFsChanged);
            FsWatcher.Deleted += new FileSystemEventHandler(OnFsChanged);
            FsWatcher.Renamed += new RenamedEventHandler(OnFsRenamed);

            FsWatcher.EnableRaisingEvents = true;
        }

        private void Reset()
        {
            // The pattern of clearing, set to null, then re-initialize is to
            // elevate this variable as eligble for garbage collection
            //
            if (_elementsById != null)
            {
                _elementsById.Clear();
            }
            _elementsById = null;
            _elementsById = new ConcurrentDictionary<string, GraphXmlElement>();
        }

        private void Resolve(GraphXmlElement element)
        {
            ResolveChildren(element);
            ResolveReferences(element);
        }

        private void ResolveChildren(GraphXmlElement element)
        {
            if (!String.IsNullOrWhiteSpace(element.ParentId))
            {
                GraphXmlElement parent = null;
                if (_elementsById.TryGetValue(element.ParentId, out parent))
                {
                    element.Parent = parent;
                }
            }

            foreach (var childId in element.ChildrenIdenifiers)
            {
                GraphXmlElement child = null;
                if (_elementsById.TryGetValue(element.ParentId, out child))
                {
                    element.Children.GetOrAdd(childId, child);
                }
            }
        }

        private void ResolveReferences(GraphXmlElement element)
        {
            foreach (var entry in element.InRefernceIdentifiers)
            {
                foreach (var referenceId in entry.Value)
                {
                    GraphXmlElement referencedElement = null;
                    if (_elementsById.TryGetValue(referenceId, out referencedElement))
                    {
                        referencedElement.AddInReference(entry.Key, element);
                        element.AddOutReference(entry.Key, referencedElement);
                    }
                }
            }

            foreach (var entry in element.OutRefernceIdentifiers)
            {
                foreach (var referenceId in entry.Value)
                {
                    GraphXmlElement referencedElement = null;
                    if (_elementsById.TryGetValue(referenceId, out referencedElement))
                    {
                        referencedElement.AddOutReference(entry.Key, element);
                        element.AddInReference(entry.Key, referencedElement);
                    }
                }
            }
        }

        #region Element Extraction

        private List<GraphXmlElement> ExtractFromFile(string filePath)
        {
            var results = new List<GraphXmlElement>();

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
                        if (!xmlChild.Name.LocalName.EndsWith(StrLits.Ref))
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

        private static GraphXmlElement CreateGraphXmlElement(XElement xmlElement, string file)
        {
            var graphElement = new GraphXmlElement();

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

        private void ParallelForEach(string logContext, ConcurrentDictionary<string, GraphXmlElement> cache, Action<GraphXmlElement> action)
        {
            var sw = Stopwatch.StartNew();

            // To help determine if parallelism is overkill, we need to first get the number of proc available.
            //
            int procCount = System.Environment.ProcessorCount;

            // Execute in parallel if there are enough files in the directory
            // Otherwise, just do a plain-jane for loop.
            //
            try
            {
                if (cache.Count < procCount)
                {
                    foreach (var item in cache.Values)
                    {
                        action(item);
                    }
                }
                else
                {
                    Parallel.ForEach(cache.Values, (item) => {
                        action(item);
                    });
                }
            }
            catch (Exception e)
            {
                Log.Error(_logContext, "1839", e.Message + "\n" + e.StackTrace);
            }

        }

        #endregion

        #region Event Handlers

        protected void OnFsChanged(object sender, FileSystemEventArgs e)
        {
            WorkspaceHasChangedSinceLastLoad = true;
        }

        private void OnFsRenamed(object sender, RenamedEventArgs e)
        {
            WorkspaceHasChangedSinceLastLoad = true;
        }

        #endregion


        #endregion
    }
}