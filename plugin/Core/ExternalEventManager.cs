using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using RevitMCPSDK.API.Interfaces;
using System;
using System.Collections.Generic;

namespace revit_mcp_plugin.Core
{
    /// <summary>
    /// Manage the creation and lifecycle of external events.
    /// Manages the creation and lifecycle of external events.
    /// </summary>
    public class ExternalEventManager
    {
        private static ExternalEventManager _instance;
        private Dictionary<string, ExternalEventWrapper> _events = new Dictionary<string, ExternalEventWrapper>();
        private bool _isInitialized = false;
        private UIApplication _uiApp;
        private ILogger _logger;

        /// <summary>
        /// Manages the creation and lifecycle of external events.
        /// </summary>
        public static ExternalEventManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new ExternalEventManager();
                return _instance;
            }
        }

        private ExternalEventManager() { }

        public void Initialize(UIApplication uiApp, ILogger logger)
        {
            _uiApp = uiApp;
            _logger = logger;
            _isInitialized = true;
        }

        /// <summary>
        /// Get or create external event
        /// Obtain or create external events.
        /// </summary>
        public ExternalEvent GetOrCreateEvent(IWaitableExternalEventHandler handler, string key)
        {
            if (!_isInitialized)
                throw new InvalidOperationException($"{nameof(ExternalEventManager)}Not yet initialized\n{nameof(ExternalEventManager)}has not been initialized.");

            // If it exists and the handler matches, return directly.
            // If it exists and the processor matches, return directly.
            if (_events.TryGetValue(key, out var wrapper) &&
                wrapper.Handler == handler)
            {
                return wrapper.Event;
            }

            // 需要在UI线程中创建事件
            // You need to create events in the UI thread. 
            ExternalEvent externalEvent = null;

            // Execute the operation to create an event using the context of the active document.
            // Perform the operation that created the event using the context of the active document.
            _uiApp.ActiveUIDocument.Document.Application.ExecuteCommand(
                (uiApp) => {
                    externalEvent = ExternalEvent.Create(handler);
                }
            );

            if (externalEvent == null)
                throw new InvalidOperationException("无法创建外部事件\nUnable to create external events.");

            // 存储事件
            // Storage events.
            _events[key] = new ExternalEventWrapper
            {
                Event = externalEvent,
                Handler = handler
            };

            _logger.Info($"为 {key} Created new external event\nCreated a new external event for key {key}.");

            return externalEvent;
        }

        /// <summary>
        /// <para>清除事件缓存</para>
        /// <para>Clears the event cache.</para>
        /// </summary>
        public void ClearEvents()
        {
            _events.Clear();
        }

        private class ExternalEventWrapper
        {
            public ExternalEvent Event { get; set; }
            public IWaitableExternalEventHandler Handler { get; set; }
        }
    }
}

namespace Autodesk.Revit.DB
{
    public static class ApplicationExtensions
    {
        public delegate void CommandDelegate(UIApplication uiApp);

        /// <summary>
        /// <para>在 Revit 上下文中执行命令</para>
        /// <para>Execute commands in the Revit context.</para>
        /// </summary>
        public static void ExecuteCommand(this Autodesk.Revit.ApplicationServices.Application app, CommandDelegate command)
        {
            // this method is in Revit called within context, can be safely created. ExternalEvent
            // This method is called in the Revit context and can safely create an ExternalEvent.
            command?.Invoke(new UIApplication(app));
        }
    }
}