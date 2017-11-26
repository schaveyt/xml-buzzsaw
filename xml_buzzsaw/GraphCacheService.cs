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
using xml_buzzsaw.xml;

namespace xml_buzzsaw
{
    public class GraphCacheService
    {
        #region Fields

        private ILogger _logger;

        private ConcurrentDictionary<string, GraphElement> _elementsByIdCollection;

        #endregion

        #region Constructors

        public GraphCacheService(ILogger logger)
        {
            _logger = logger;
            WorkspaceHasChangedSinceLastLoad = true;
            Reset();
        }

        #endregion

        #region Properties

        public int Count
        {
            get
            {
                return _elementsByIdCollection.Count;
            }
        }

        public IDictionary<string, GraphElement> Cache
        {
            get
            {
                return _elementsByIdCollection;
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

            if (!Directory.Exists(topLevelFolder))
            {
                error = $"The specified folder does not exist: {topLevelFolder}.";
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

            // Prepare the file watchers on our new set of files
            //
            SetupFileSystemWatchers(topLevelFolder);

            // Reset all cached state
            //
            Reset();

            try
            {
                // Translate the xml to elements and add them to the cache.
                //
                XmlToGraphTranslator.TranslateToCache(topLevelFolder, _elementsByIdCollection);

                 // In parallel, apply the Resolve method to each of the cached elements.
                 //
                 ParallelUtils.ForEach(_logger, _elementsByIdCollection, Resolve);
            }
            catch (Exception e)
            {
                _logger.Error("9494", $"While loading the graph cache the following execption was thrown: {e.Message}");
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
            if (_elementsByIdCollection != null)
            {
                _elementsByIdCollection.Clear();
            }
            _elementsByIdCollection = null;
            _elementsByIdCollection = new ConcurrentDictionary<string, GraphElement>();
        }

        #region Cache Resolvers

        private void Resolve(GraphElement element)
        {
            ResolveChildren(element);
            ResolveReferences(element);
        }

        private void ResolveChildren(GraphElement element)
        {
            if (!String.IsNullOrWhiteSpace(element.ParentId))
            {
                GraphElement parent = null;
                if (_elementsByIdCollection.TryGetValue(element.ParentId, out parent))
                {
                    element.Parent = parent;
                }
            }

            foreach (var childId in element.ChildrenIdenifiers)
            {
                GraphElement child = null;
                if (_elementsByIdCollection.TryGetValue(element.ParentId, out child))
                {
                    element.Children.GetOrAdd(childId, child);
                }
            }
        }

        private void ResolveReferences(GraphElement element)
        {
            foreach (var entry in element.InRefernceIdentifiers)
            {
                foreach (var referenceId in entry.Value)
                {
                    GraphElement referencedElement = null;
                    if (_elementsByIdCollection.TryGetValue(referenceId, out referencedElement))
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
                    GraphElement referencedElement = null;
                    if (_elementsByIdCollection.TryGetValue(referenceId, out referencedElement))
                    {
                        referencedElement.AddOutReference(entry.Key, element);
                        element.AddInReference(entry.Key, referencedElement);
                    }
                }
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