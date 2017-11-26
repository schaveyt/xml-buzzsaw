using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace xml_buzzsaw
{
    public class GraphElement
    {
        #region Fields

        /// <summary>
        /// List of xml identifiers for the element chilren
        /// </summary>
        private List<string> _childIdentifiers;

        /// <summary>
        /// Dictionary of xml element ids for elements with incoming relationships grouped by relationship name
        /// </summary>
        private Dictionary<string, List<string>> _inReferenceIdentifiers;

        /// <summary>
        /// Dictionary of xml element ids for elements with outgoings relationships grouped by relationship name
        /// </summary>
        private Dictionary<string, List<string>> _outReferenceIdentifiers;

        private readonly ConcurrentDictionary<string, GraphElement> _children =
            new ConcurrentDictionary<string, GraphElement>();

        private readonly ConcurrentDictionary<string, List<GraphElement>> _incomingElements =
        new ConcurrentDictionary<string, List<GraphElement>>();

        private readonly ConcurrentDictionary<string, List<GraphElement>> _outgoingElements =
            new ConcurrentDictionary<string, List<GraphElement>>();

        private SortedList<string, string> _attributes = new SortedList<string, string>();

        #endregion

        #region Properties

        public string Id { get; set; }

        public string Uri { get; set; }

        public int LineNum { get; set; }

        public string Name { get; set; }

        public string XmlElementName { get; set; }

        public string ParentId { get; set; }

        public GraphElement Parent { get; set; }

        public List<string> ChildrenIdenifiers
        {
            get
            {
                if (_childIdentifiers == null)
                {
                    _childIdentifiers = new List<string>();
                }
                return _childIdentifiers;
            }
        }

        public Dictionary<string, List<string>> InRefernceIdentifiers
        {
            get
            {
                if (_inReferenceIdentifiers == null)
                {
                    _inReferenceIdentifiers = new Dictionary<string, List<string>>();
                }
                return _inReferenceIdentifiers;
            }
        }

        public Dictionary<string, List<string>> OutRefernceIdentifiers
        {
            get
            {
                if (_outReferenceIdentifiers == null)
                {
                    _outReferenceIdentifiers = new Dictionary<string, List<string>>();
                }
                return _outReferenceIdentifiers;
            }
        }

        public ConcurrentDictionary<string, GraphElement> Children
        {
            get
            {
                return _children;
            }
        }

        public ConcurrentDictionary<string, List<GraphElement>> IncomingElements
        {
            get
            {
                return _incomingElements;
            }
        }

        public ConcurrentDictionary<string, List<GraphElement>> OutgoingElements
        {
            get
            {
                return _outgoingElements;
            }
        }

        public SortedList<string, string> Attributes
        {
            get
            {
                return _attributes;
            }
        }

        #endregion

        #region Methods

        public void AddInReference(string referenceType, GraphElement element)
        {
            List<GraphElement> list;
            if(!_incomingElements.TryGetValue(referenceType, out list))
            {
                list = new List<GraphElement>();
                _incomingElements.GetOrAdd(referenceType, list);
            }

            list.Add(element);
        }

        public void AddOutReference(string referenceType, GraphElement element)
        {
            List<GraphElement> list;
            if(!_outgoingElements.TryGetValue(referenceType, out list))
            {
                list = new List<GraphElement>();
                _outgoingElements.GetOrAdd(referenceType, list);
            }

            list.Add(element);
        }

        public void RemoveInReference(string referenceType, GraphElement element)
        {
            List<GraphElement> list;
            if (!_incomingElements.TryGetValue(referenceType, out list))
            {
                return;
            }

            list.Remove(element);
        }

        public void RemoveOutReference(string referenceType, GraphElement element)
        {
            List<GraphElement> list;
            if (!_outgoingElements.TryGetValue(referenceType, out list))
            {
                return;
            }

            list.Remove(element);
        }

        #endregion

    }

}