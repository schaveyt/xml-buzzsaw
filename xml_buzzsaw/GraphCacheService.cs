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

        #region Load and Supporting Methods

        public bool Load(string topLevelFolder, bool forceReload = false)
        {
            if (String.IsNullOrWhiteSpace(topLevelFolder))
            {
                _logger.Error("3959", "The provided top level folder is either null or only whitespace.");
                return false;
            }

            if (!Directory.Exists(topLevelFolder))
            {
                 _logger.Error("4089", $"The specified folder does not exist: {topLevelFolder}.");
                return false;
            }

            // Skip reloading the cache if nothing on the file system has change and
            // the user is not asking to force a reload.
            //
            if (!WorkspaceHasChangedSinceLastLoad && !forceReload)
            {
                return true;
            }

            // Reset all cached state
            //
            Reset();

            //
            // Now start the loading process
            //
            WorkspaceHasChangedSinceLastLoad = false;

            // Prepare the file watchers on our new set of files
            //
            SetupFileSystemWatchers(topLevelFolder);

            try
            {
                // Translate the xml to elements and add them to the cache.
                //
                if (!XmlToGraphTranslator.TranslateToCache(_logger, topLevelFolder, _elementsByIdCollection))
                {
                    return false;
                }

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
                // Type is the _Ref xml element minus the _Ref. (e.g. <Mother_Ref> => Mother)
                //
                var referenceType = entry.Key.Substring(0, entry.Key.Length - (StrLits.Ref.Length + 1));

                foreach (var referenceId in entry.Value)
                {
                    GraphElement referencedElement = null;
                    if (_elementsByIdCollection.TryGetValue(referenceId, out referencedElement))
                    {
                        referencedElement.AddOutReference(referenceType, element);
                        element.AddInReference(referenceType, referencedElement);
                        
                    }
                }
            }

            foreach (var entry in element.OutRefernceIdentifiers)
            {
                // Type is the _Ref xml element minus the _Ref. (e.g. <Mother_Ref> => Mother)
                var referenceType = entry.Key.Substring(0, entry.Key.Length - (StrLits.Ref.Length + 1));

                foreach (var referenceId in entry.Value)
                {
                    GraphElement referencedElement = null;
                    if (_elementsByIdCollection.TryGetValue(referenceId, out referencedElement))
                    {
                        referencedElement.AddInReference(referenceType, element);
                        element.AddOutReference(referenceType, referencedElement);
                    }
                }
            }
        }

        #endregion

        #region Collection Interfaces

        public IDictionary<string, GraphElement> Elements
        {
            get
            {
                return _elementsByIdCollection;
            }
            
        }

        public void Reset()
        {
            WorkspaceHasChangedSinceLastLoad = true;

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