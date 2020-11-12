// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.PackageManager.Requests;
using System.Linq;

namespace UnityEditor.PackageManager.UI
{
    internal class AddPackageByNameDropdown : DropdownContent
    {
        private static readonly Vector2 k_DefaultWindowSize = new Vector2(320, 72);
        private static readonly Vector2 k_WindowSizeWithError = new Vector2(320, 110);
        internal override Vector2 windowSize => string.IsNullOrEmpty(errorInfoBox.text) ? k_DefaultWindowSize : k_WindowSizeWithError;

        private EditorWindow m_AnchorWindow;
        private UpmSearchOperation m_ExtraFetchOperation;

        private TextFieldPlaceholder m_PackageNamePlaceholder;
        private TextFieldPlaceholder m_PackageVersionPlaceholder;

        private ResourceLoader m_ResourceLoader;
        private PackageFiltering m_PackageFiltering;
        private UpmClient m_UpmClient;
        private PackageDatabase m_PackageDatabase;
        private PageManager m_PageManager;
        private void ResolveDependencies(ResourceLoader resourceLoader, PackageFiltering packageFiltering, UpmClient upmClient, PackageDatabase packageDatabase, PageManager packageManager)
        {
            m_ResourceLoader = resourceLoader;
            m_PackageFiltering = packageFiltering;
            m_UpmClient = upmClient;
            m_PackageDatabase = packageDatabase;
            m_PageManager = packageManager;
        }

        public AddPackageByNameDropdown(ResourceLoader resourceLoader, PackageFiltering packageFiltering, UpmClient upmClient, PackageDatabase packageDatabase, PageManager packageManager, EditorWindow anchorWindow)
        {
            ResolveDependencies(resourceLoader, packageFiltering, upmClient, packageDatabase, packageManager);

            styleSheets.Add(m_ResourceLoader.GetInputDropdownWindowStylesheet());

            var root = m_ResourceLoader.GetTemplate("AddPackageByNameDropdown.uxml");
            Add(root);
            cache = new VisualElementCache(root);

            Init(anchorWindow);
        }

        private void Init(EditorWindow anchorWindow)
        {
            m_AnchorWindow = anchorWindow;

            packageNameField.RegisterCallback<ChangeEvent<string>>(OnTextFieldChange);
            packageNameField.visualInput.RegisterCallback<KeyDownEvent>(OnKeyDownShortcut);
            packageVersionField.visualInput.RegisterCallback<KeyDownEvent>(OnKeyDownShortcut);

            m_PackageNamePlaceholder = new TextFieldPlaceholder(packageNameField, L10n.Tr("Name"));
            m_PackageVersionPlaceholder = new TextFieldPlaceholder(packageVersionField, L10n.Tr("Version (optional)"));

            submitButton.clickable.clicked += SubmitClicked;
        }

        internal override void OnDropdownShown()
        {
            inputForm.SetEnabled(true);
            m_AnchorWindow?.rootVisualElement?.SetEnabled(false);

            if (string.IsNullOrEmpty(errorInfoBox.text) || packageNameField.ClassListContains("error"))
                packageNameField.visualInput.Focus();
            else
                packageVersionField.visualInput.Focus();
            submitButton.SetEnabled(!string.IsNullOrEmpty(packageNameField.value));
        }

        internal override void OnDropdownClosed()
        {
            m_PackageNamePlaceholder.OnDisable();
            m_PackageVersionPlaceholder.OnDisable();

            packageNameField.UnregisterCallback<ChangeEvent<string>>(OnTextFieldChange);
            packageNameField.visualInput.UnregisterCallback<KeyDownEvent>(OnKeyDownShortcut);
            packageVersionField.visualInput.UnregisterCallback<KeyDownEvent>(OnKeyDownShortcut);

            if (m_ExtraFetchOperation != null)
            {
                m_ExtraFetchOperation.onOperationError -= OnExtraFetchError;
                m_ExtraFetchOperation.onProcessResult -= OnExtraFetchResult;
                m_ExtraFetchOperation.Cancel();
                m_ExtraFetchOperation = null;
            }

            if (m_AnchorWindow != null)
            {
                m_AnchorWindow.rootVisualElement.SetEnabled(true);
                m_AnchorWindow = null;
            }

            submitButton.clickable.clicked -= SubmitClicked;
        }

        private void SetError(string errorMessage, bool isNameError = false, bool isVersionError = false)
        {
            packageVersionField.RemoveFromClassList("error");
            packageNameField.RemoveFromClassList("error");

            if (string.IsNullOrEmpty(errorMessage))
            {
                RemoveFromClassList("inputError");
                errorInfoBox.text = string.Empty;
            }
            else
            {
                AddToClassList("inputError");
                errorInfoBox.text = errorMessage;
                if (isNameError)
                    packageNameField.AddToClassList("error");
                if (isVersionError)
                    packageVersionField.AddToClassList("error");
            }
        }

        private void ShowWithNewWindowSize()
        {
            // There's no direct `resize` function for a dropdown window but setting min/max size does the same trick.
            container.minSize = windowSize;
            container.maxSize = windowSize;
            OnDropdownShown();
        }

        private void SubmitClicked()
        {
            var packageName = packageNameField.value;
            if (string.IsNullOrEmpty(packageName))
                return;
            var version = packageVersionField.value;

            var package = m_PackageDatabase.GetPackage(packageName);
            if (package != null)
            {
                if (string.IsNullOrEmpty(version))
                {
                    InstallByNameAndVersion(packageName);
                    return;
                }
                else if (package.versions.Any(v => v.versionString == version))
                {
                    InstallByNameAndVersion(packageName, version);
                    return;
                }
            }

            m_UpmClient.onExtraFetchOperation += OnExtraFetchOperation;
            m_UpmClient.ExtraFetch(packageName);

            inputForm.SetEnabled(false);
        }

        private void InstallByNameAndVersion(string packageName, string packageVersion = null)
        {
            var packageId = string.IsNullOrEmpty(packageVersion) ? packageName : $"{packageName}@{packageVersion}";
            m_UpmClient.AddById(packageId);

            PackageManagerWindowAnalytics.SendEvent("addByNameAndVersion", packageId);

            Close();

            var package = m_PackageDatabase.GetPackage(packageName);
            if (package != null)
            {
                m_PackageFiltering.currentFilterTab = m_PageManager.FindTab(package);
                m_PageManager.SetSelected(package, package.versions?.FirstOrDefault(v => v.versionString == packageVersion));
            }
        }

        private void OnExtraFetchOperation(IOperation operation)
        {
            m_ExtraFetchOperation = operation as UpmSearchOperation;
            m_ExtraFetchOperation.onOperationError += OnExtraFetchError;
            m_ExtraFetchOperation.onProcessResult += OnExtraFetchResult;

            m_UpmClient.onExtraFetchOperation -= OnExtraFetchOperation;
        }

        private void OnExtraFetchResult(SearchRequest request)
        {
            var packageInfo = request.Result.FirstOrDefault();
            if (packageInfo.name == packageNameField.value)
            {
                var version = packageVersionField.value;
                if (string.IsNullOrEmpty(version))
                    InstallByNameAndVersion(packageInfo.name);
                else if (packageInfo.versions.all.Contains(version))
                    InstallByNameAndVersion(packageInfo.name, version);
                else
                {
                    SetError(L10n.Tr("Unable to find the package with the specified version.\nPlease check the version and try again."), false, true);
                    ShowWithNewWindowSize();
                }
            }
        }

        private void OnExtraFetchError(IOperation operation, UIError error)
        {
            var searchOperation = operation as UpmSearchOperation;
            if (searchOperation.packageName == packageNameField.value)
            {
                SetError(L10n.Tr("Unable to find the package with the specified name.\nPlease check the name and try again."), true, false);
                ShowWithNewWindowSize();
            }
        }

        private void OnTextFieldChange(ChangeEvent<string> evt)
        {
            submitButton.SetEnabled(!string.IsNullOrEmpty(packageNameField.value));
        }

        private void OnKeyDownShortcut(KeyDownEvent evt)
        {
            switch (evt.keyCode)
            {
                case KeyCode.Escape:
                    Close();
                    break;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    SubmitClicked();
                    break;
            }
        }

        private VisualElementCache cache { get; set; }
        private VisualElement inputForm => cache.Get<VisualElement>("inputForm");
        private TextField packageNameField => cache.Get<TextField>("packageName");
        private TextField packageVersionField => cache.Get<TextField>("packageVersion");
        private HelpBox errorInfoBox => cache.Get<HelpBox>("errorInfoBox");
        private Button submitButton => cache.Get<Button>("submitButton");
    }
}