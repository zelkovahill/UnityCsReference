// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

// #define QUICKSEARCH_DEBUG
// #define QUICKSEARCH_ANALYTICS_LOGGING
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Analytics;

namespace UnityEditor.Search
{
    internal static class SearchAnalytics
    {
        const string vendorKey = "unity.quicksearch";

        [Serializable]
        internal class ProviderData : IAnalytic.IData
        {
            // Was the provider enabled for the search
            public bool isEnabled;
            // Id of the provider
            public string id;
            // Average time of the last 10 search performed by this provider (in ms).
            public long avgTime;
            // Custom provider data
            public string custom;
        }

        [Serializable]
        internal class PreferenceData : IAnalytic.IData
        {
            // BEGIN- Not used anymore
            public bool useDockableWindow;
            public bool closeWindowByDefault;
            // END - Not used anymore

            public bool trackSelection;
        }

        [Serializable]
        internal class SearchEvent : IAnalytic.IData
        {
            public SearchEvent()
            {
                startTime = DateTime.Now;
                package = Package;
                package_ver = PackageVersion;
            }

            public void Success(SearchItem item, SearchAction action = null)
            {
                Done();
                success = true;
                providerId = item.provider.id;
                if (action != null)
                    actionId = action.content.text;
            }

            public void Done()
            {
                if (duration == 0)
                    duration = elapsedTimeMs;
            }
            

            public long elapsedTimeMs => (long)(DateTime.Now - startTime).TotalMilliseconds;

            // Start time when the SearchWindow opened
            private DateTime startTime;
            // Duration (in ms) for which the search window was opened
            public long duration;
            // Was the search result a success: did an item gets activated.
            public bool success;
            // If search successful: Provider of the item activated.
            public string providerId;
            // If search successful: ActionId of the item activated.
            public string actionId;
            // What was in the search box when the tool window was closed.
            public string searchText;
            // UI Usage
            // Was the item activated using the Enter key (false: user clicked in the item)
            public bool endSearchWithKeyboard;
            // Was the history shortcut used.
            public bool useHistoryShortcut;
            // Was the FilterMenu shortcut used.
            public bool useFilterMenuShortcut;
            // Was the Action Menu shortcut used.
            public bool useActionMenuShortcut;
            // Was drag and drop used.
            public bool useDragAndDrop;
            // Provider specific data
            public ProviderData[] providerDatas;

            public bool useOverrideFilter;
            public bool isDeveloperMode;
            public PreferenceData preferences;

            public bool useQueryBuilder;

            public string package;
            public string package_ver;

            // Future:
            // useFilterId
            // useActionQuery
            // nbCharacterInSearch
            // useActionMenu
            // useFilterWindow
            // useRightClickOnItem
            // useRightClickContextAction
        }

        [AnalyticInfo(eventName: "quickSearch", vendorKey: vendorKey)]
        internal class SearchEventAnalytic : IAnalytic
        {
            public SearchEventAnalytic(SearchEvent data)
            {
                m_data = data;
            }

            public bool TryGatherData(out IAnalytic.IData data, out Exception error)
            {
                error = null;
                data = m_data;
                return data != null;
            }

            private SearchEvent m_data = null;
        }

        [Serializable]
        internal struct GenericEvent : IAnalytic.IData
        {
            public static GenericEvent Create(string windowId, GenericEventType type, string name = null)
            {
                return new GenericEvent()
                {
                    windowId = windowId,
                    category = type.ToString(),
                    categoryId = (int)type,
                    name = name,
                    package = Package,
                    package_ver = PackageVersion
                };
            }

            public string package;
            public string package_ver;

            // Message category
            public string category;
            // Enum id of the message category
            public int categoryId;
            // Message name
            public string name;
            // Message type
            public string message;
            // Message data
            public string description;
            // Event duration
            public long duration;

            public string windowId;
            public string stringPayload1;
            public string stringPayload2;
            public float floatPayload1;
            public float floatPayload2;
            public int intPayload1;
            public int intPayload2;
        }

        [AnalyticInfo(eventName: "quickSearchGeneric", vendorKey: vendorKey)]
        internal class GenericEventAnalytic : IAnalytic
        {
            public GenericEventAnalytic(GenericEvent data)
            {
                m_data = data;
            }

            public bool TryGatherData(out IAnalytic.IData data, out Exception error)
            {
                error = null;
                data = m_data;
                return data != null;
            }

            private GenericEvent m_data;
        }

        [Serializable]
        internal class SearchUsageReport : IAnalytic.IData
        {
            public int indexCount;
            public float maxIndexSize;
            public int savedSearchesCount;
            public int sessionQueryCount;
            public int sessionQuerySearchExecutionCount;
            public int sessionSearchOpenWindow;
            public bool trackSelection;
            public bool fetchPreview;
            public bool wantsMore;
            public bool keepOpen;
            public float itemIconSize;
            public bool showPackageIndexes;
            public int debounceMs;
            public string savedSearchesSortOrder;
            public string sceneSearchEngine;
            public string projectSearchEngine;
            public string objectSelectorEngine;
            public bool useQueryBuilder;
        }

        [AnalyticInfo(eventName: "quickSearchUsageReport", vendorKey: vendorKey)]
        internal class SearchUsageReportAnalytic : IAnalytic
        {
            public SearchUsageReportAnalytic(SearchUsageReport data)
            {
                m_data = data;
            }

            public bool TryGatherData(out IAnalytic.IData data, out Exception error)
            {
                error = null;
                data = m_data;
                return data != null;
            }

            private SearchUsageReport m_data;
        }

        public enum GenericEventType
        {
            Information,
            Warning,
            Error,

            QuickSearchOpen,
            QuickSearchShowActionMenu,
            QuickSearchOpenPreferences,
            QuickSearchOpenDocLink,
            QuickSearchListSizeChanged,
            QuickSearchDragItem,
            QuickSearchCreateSearchQuery,
            QuickSearchToggleHelpProviderF1,
            QuickSearchClearSearch,
            QuickSearchDismissEsc,

            PreferenceChanged,
            PreferenceReset,

            FilterWindowOpen,
            FilterWindowOpenPreferences,
            FilterWindowToggle,

            SearchQueryOpen,
            SearchQueryExecute,

            IndexManagerOpen,
            IndexManagerCreateIndex,
            IndexManagerRemoveIndex,
            IndexManagerSaveModifiedIndex,
            IndexManagerBuildIndex,

            CreateIndexFromTemplate,

            SetupWizardOpenFromMenu,
            SetupWizardOpenFirstUse,
            SetupWizardExecute,
            SetupWizardCancel,

            ExpressionBuilderOpenFromMenu,
            ExpressionBuilderSave,
            ExpressionBuilderOpenExpression,
            ExpressionBuilderCreateExpressionFromMenu,

            QuickSearchAutoCompleteTab,
            QuickSearchAutoCompleteInsertSuggestion,
            QuickSearchSavedSearchesSorted,
            QuickSearchSavedSearchesExecuted,
            QuickSearchOpenToggleToggleSidePanel,

            QuickSearchJumpToSearch,
            QuickSearchSyncViewButton,
            QuickSearchSwitchTab,

            QuickSearchRemoveFavoriteItem,
            QuickSearchRemoveFavoriteQuery,
            QuickSearchAddFavoriteItem,
            QuickSearchAddFavoriteQuery,
            QuickSearchSizeRadioButton,

            QuickSearchTableAddColumn,
            QuickSearchTableReset,
            QuickSearchTableRemoveColumn,
            QuickSearchTableEditColumn,
            QuickSearchTableChangeColumnFormat,
            QuickSearchTableToggleColumnVisibility,

            QuickSearchQueryChange,

            QuickSearchExportReport,
            QuickSearchImportReport,
            ReportViewOpen,

            QuickSearchPickerOpens,

            QuickSearchToggleBuilder,
            QuickSearchHelperWidgetExecuted,

            ObjectSelectorSettingsReset,

            BrowseAssetStoreWeb
        }

        public static readonly string Package = "com.unity.quicksearch";
        public static string PackageVersion;
        private static readonly HashSet<int> s_OnceHashCodes = new HashSet<int>();
        private static Delayer m_Debouncer;

        public static int sessionQueryCount
        {
            get => SessionState.GetInt($"Search.{nameof(sessionQueryCount)}", 0);
            set => SessionState.SetInt($"Search.{nameof(sessionQueryCount)}", value);
        }

        public static int sessionSearchOpenWindow
        {
            get => SessionState.GetInt($"Search.{nameof(sessionSearchOpenWindow)}", 0);
            set => SessionState.SetInt($"Search.{nameof(sessionSearchOpenWindow)}", value);
        }

        public static int sessionQuerySearchExecutionCount
        {
            get => SessionState.GetInt($"Search.{nameof(sessionQuerySearchExecutionCount)}", 0);
            set => SessionState.SetInt($"Search.{nameof(sessionQuerySearchExecutionCount)}", value);
        }

        static SearchAnalytics()
        {
            var v = InternalEditorUtility.GetUnityVersion();
            PackageVersion = $"{v.Major}.{v.Minor}";

            EditorApplication.delayCall += () =>
            {
                Application.logMessageReceived += (condition, trace, type) =>
                {
                    if (type == LogType.Exception &&
                        !string.IsNullOrEmpty(trace) &&
                        trace.Contains("quicksearch"))
                    {
                        if (s_OnceHashCodes.Add(trace.GetHashCode()))
                        {
                            SendErrorEvent("__uncaught__", condition, trace);
                        }
                    }
                };
            };


            EditorApplication.wantsToQuit += UnityQuit;
            m_Debouncer = Delayer.Debounce(SendEventFromEventCreator);
        }

        public static void SendExceptionOnce(string name, Exception ex)
        {
            if (ex == null)
            {
                return;
            }

            var hashCode = ex.StackTrace.GetHashCode();
            if (s_OnceHashCodes.Add(hashCode))
            {
                SendException(name, ex);
            }
        }

        public static void SendException(string name, Exception ex)
        {
            if (ex == null)
            {
                return;
            }

            SendErrorEvent(name, ex.Message, ex.ToString());
        }

        public static void SendErrorEvent(string name, string message = null, string description = null)
        {
            SendEvent(null, GenericEventType.Error, name, message, description);
        }

        public static void SendEvent(string windowId, GenericEventType category, string name = null, string message = null, string description = null, long durationInMs = 0)
        {
            var e = GenericEvent.Create(windowId, category, name);
            e.message = message;
            e.description = description;
            e.duration = durationInMs;
            SendEvent(e);
        }

        public static void SendEvent(GenericEvent evt)
        {
            switch ((GenericEventType)evt.categoryId)
            {
                case GenericEventType.SearchQueryExecute:
                case GenericEventType.SearchQueryOpen:
                case GenericEventType.QuickSearchSavedSearchesExecuted:
                    sessionQuerySearchExecutionCount++;
                    break;
                case GenericEventType.QuickSearchOpen:
                    sessionSearchOpenWindow++;
                    break;
            }

            GenericEventAnalytic analytic = new GenericEventAnalytic(evt);
            EditorAnalytics.SendAnalytic(analytic);
        }

        public static void SendReportUsage()
        {
            var report = CreateSearchUsageReport();
            SearchUsageReportAnalytic analytic = new SearchUsageReportAnalytic(report);
            EditorAnalytics.SendAnalytic(analytic);
        }

        public static void DebounceSendEvent(Func<GenericEvent> evtCreator)
        {
            m_Debouncer.Execute(evtCreator);
        }


        public static void SendSearchEvent(SearchEvent evt, SearchContext searchContext)
        {
            evt.useOverrideFilter = searchContext.filterId != null;
            evt.isDeveloperMode = Utils.isDeveloperBuild;
            evt.preferences = new PreferenceData()
            {
                closeWindowByDefault = true,
                useDockableWindow = false,
                trackSelection = SearchSettings.trackSelection
            };

            var providers = searchContext.providers;
            evt.providerDatas = providers.Select(provider => new ProviderData()
            {
                id = provider.id,
                avgTime = (long)searchContext.searchElapsedTime,
                isEnabled = evt.useOverrideFilter || searchContext.IsEnabled(provider.id),
                custom = ""
            }).ToArray();

            if (evt.success)
                sessionQueryCount++;

            SearchEventAnalytic analytic = new SearchEventAnalytic(evt);
            EditorAnalytics.SendAnalytic(analytic);
        }

        private static bool UnityQuit()
        {
            SendReportUsage();
            return true;
        }

        private static SearchUsageReport CreateSearchUsageReport()
        {
            var report = new SearchUsageReport();
            report.trackSelection = SearchSettings.trackSelection;
            report.fetchPreview = SearchSettings.fetchPreview;
            report.wantsMore = SearchSettings.defaultFlags.HasAny(SearchFlags.WantsMore);
            report.keepOpen = SearchSettings.keepOpen;
            report.itemIconSize = SearchSettings.itemIconSize;
            report.showPackageIndexes = SearchSettings.showPackageIndexes;
            report.debounceMs = SearchSettings.debounceMs;
            report.savedSearchesSortOrder = SearchSettings.savedSearchesSortOrder.ToString();
            report.savedSearchesCount = SearchQueryAsset.savedQueries.Count() + SearchQuery.userQueries.Count();
            report.sessionQueryCount = sessionQueryCount;
            report.sessionQuerySearchExecutionCount = sessionQuerySearchExecutionCount;
            report.sessionSearchOpenWindow = sessionSearchOpenWindow;
            report.sceneSearchEngine = UnityEditor.SearchService.SceneSearch.GetActiveSearchEngine().GetType().FullName;
            report.projectSearchEngine = UnityEditor.SearchService.ProjectSearch.GetActiveSearchEngine().GetType().FullName;
            report.objectSelectorEngine = UnityEditor.SearchService.ObjectSelectorSearch.GetActiveSearchEngine().GetType().FullName;

            report.useQueryBuilder = SearchSettings.queryBuilder;

            var allIndexes = SearchDatabase.Enumerate(SearchDatabase.IndexLocation.assets).ToArray();
            report.indexCount = allIndexes.Length;
            if (allIndexes.Length > 0)
            {
                var maxSize = allIndexes.Max(index => index.indexSize);
                report.maxIndexSize = maxSize / 1048576f;
            }
            else
                report.maxIndexSize = 0;
            return report;
        }

        private static void SendEventFromEventCreator(object eventCreator)
        {
            if (eventCreator is Func<GenericEvent> functor)
            {
                SendEvent(functor());
            }
        }
    }
}
