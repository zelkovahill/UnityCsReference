// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using System.Reflection;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Build.Reporting;
using UnityEditor.Profiling;
using UnityEditor.Rendering;
using UnityEngine.Scripting;
using UnityEditor.AssetImporters;
using UnityEngine.SceneManagement;
using UnityEditor.Build.Profile;

namespace UnityEditor.Build
{
    public interface IOrderedCallback
    {
        int callbackOrder { get; }
    }

    [Obsolete("Use IPreprocessBuildWithReport instead")]
    public interface IPreprocessBuild : IOrderedCallback
    {
        void OnPreprocessBuild(BuildTarget target, string path);
    }

    public abstract class BuildPlayerProcessor : IOrderedCallback
    {
        public virtual int callbackOrder => 0;
        public abstract void PrepareForBuild(BuildPlayerContext buildPlayerContext);
    }

    public interface IPreprocessBuildWithReport : IOrderedCallback
    {
        void OnPreprocessBuild(BuildReport report);
    }

    public interface IFilterBuildAssemblies : IOrderedCallback
    {
        string[] OnFilterAssemblies(BuildOptions buildOptions, string[] assemblies);
    }

    [Obsolete("Use IPostprocessBuildWithReport instead")]
    public interface IPostprocessBuild : IOrderedCallback
    {
        void OnPostprocessBuild(BuildTarget target, string path);
    }

    public interface IPostprocessBuildWithReport : IOrderedCallback
    {
        void OnPostprocessBuild(BuildReport report);
    }

    public interface IPostBuildPlayerScriptDLLs : IOrderedCallback
    {
        void OnPostBuildPlayerScriptDLLs(BuildReport report);
    }

    [Obsolete("Use IProcessSceneWithReport instead")]
    public interface IProcessScene : IOrderedCallback
    {
        void OnProcessScene(UnityEngine.SceneManagement.Scene scene);
    }

    public interface IProcessSceneWithReport : IOrderedCallback
    {
        void OnProcessScene(UnityEngine.SceneManagement.Scene scene, BuildReport report);
    }

    public interface IActiveBuildTargetChanged : IOrderedCallback
    {
        void OnActiveBuildTargetChanged(BuildTarget previousTarget, BuildTarget newTarget);
    }

    public interface IPreprocessShaders : IOrderedCallback
    {
        void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data);
    }

    public interface IPreprocessComputeShaders : IOrderedCallback
    {
        void OnProcessComputeShader(ComputeShader shader, string kernelName, IList<ShaderCompilerData> data);
    }

    // This API lets you generate native plugins to be integrated into the player build,
    // during the incremental player build. The incremental player build platform implementations will know
    // how to consume these plugins and link them into the build.
    internal interface IGenerateNativePluginsForAssemblies : IOrderedCallback
    {
        // Arguments to the PrepareOnMainThread method
        public struct PrepareArgs
        {
            // The currently active build report.
            public BuildReport report { get; set; }
        }

        // Return value of the PrepareOnMainThread method
        public struct PrepareResult
        {
            // Any pathname in here will be considered an input file to the generated plugins;
            // Any changes to any of these files will trigger a rebuild of the plugins.
            public string[] additionalInputFiles { get; set; }

            // Message to be shown in the progress bar when the GenerateNativePluginsForAssemblies method is run.
            public string displayName { get; set; }
        }

        // Prepare method which is called on the main thread before the incremental player build starts.
        // Use this to do any work which must happen on the main thread, and to set up dependencies which must trigger a
        // rebuild of the generated plugins.
        public PrepareResult PrepareOnMainThread(PrepareArgs args);

        // Arguments to the GenerateNativePluginsForAssemblies method

        public struct GenerateArgs
        {
            // Path names to the managed assembly files on disk as built for the currently active player target
            public string[] assemblyFiles { get; set; }
        }

        // Return value of the GenerateNativePluginsForAssemblies method
        public struct GenerateResult
        {
            // Any pathname returned in this array will be treated as a plugin to be linked into the player
            public string[] generatedPlugins { get; set; }
            public string[] generatedSymbols { get; set; }
        }

        // Method to generate native plugins during the player build. This will be called on a thread by the incremental
        // player build pipeline to allow generating native plugins from editor code which will be linked into the player.
        // If the plugins have already be generated in a previous build, this will only be called if any of the input
        // files have changed. Input files are all assemblies (as specified in args.assemblyFiles) and all input files
        // returned by `PrepareOnMainThread` in `additionalInputFiles`.
        public GenerateResult GenerateNativePluginsForAssemblies(GenerateArgs args);
    }

    public interface IUnityLinkerProcessor : IOrderedCallback
    {
        string GenerateAdditionalLinkXmlFile(BuildReport report, UnityLinker.UnityLinkerBuildPipelineData data);
    }

    [Obsolete("The IIl2CppProcessor interface has been removed from Unity. Use IPostBuildPlayerScriptDLLs if you need to access player assemblies before il2cpp runs.", true)]
    public interface IIl2CppProcessor : IOrderedCallback
    {
    }

    internal static class BuildPipelineInterfaces
    {
        internal class Processors
        {
#pragma warning disable 618
            public List<IPreprocessBuild> buildPreprocessors;
            public List<IPostprocessBuild> buildPostprocessors;
            public List<IProcessScene> sceneProcessors;
#pragma warning restore 618

            public List<BuildPlayerProcessor> buildPlayerProcessors;

            public List<IPreprocessBuildWithReport> buildPreprocessorsWithReport;
            public List<IPostprocessBuildWithReport> buildPostprocessorsWithReport;
            public List<IPostprocessLaunch> launchPostprocessors;
            public List<IProcessSceneWithReport> sceneProcessorsWithReport;

            public List<IFilterBuildAssemblies> filterBuildAssembliesProcessor;
            public List<IActiveBuildTargetChanged> buildTargetProcessors;
            public List<IPreprocessShaders> shaderProcessors;
            public List<IPreprocessComputeShaders> computeShaderProcessors;
            public List<IPostBuildPlayerScriptDLLs> buildPlayerScriptDLLProcessors;

            public List<IUnityLinkerProcessor> unityLinkerProcessors;
            public List<IGenerateNativePluginsForAssemblies> generateNativePluginsForAssembliesProcessors;
        }

        private static Processors m_Processors;
        internal static Processors processors
        {
            get
            {
                m_Processors = m_Processors ?? new Processors();
                return m_Processors;
            }
            set { m_Processors = value; }
        }

        [Flags]
        internal enum BuildCallbacks
        {
            None = 0,
            BuildProcessors = 1,
            SceneProcessors = 2,
            BuildTargetProcessors = 4,
            FilterAssembliesProcessors = 8,
            ShaderProcessors = 16,
            BuildPlayerScriptDLLProcessors = 32,
            UnityLinkerProcessors = 64,
            GenerateNativePluginsForAssembliesProcessors = 128,
            ComputeShaderProcessors = 256
        }

        //common comparer for all callback types
        internal static int CompareICallbackOrder(IOrderedCallback a, IOrderedCallback b)
        {
            return a.callbackOrder.CompareTo(b.callbackOrder);
        }

        static void AddToList<T>(object o, ref List<T> list) where T : class
        {
            T inst = o as T;
            if (inst == null)
                return;
            if (list == null)
                list = new List<T>();
            list.Add(inst);
        }

        static void AddToListIfTypeImplementsInterface<T>(Type t, ref object o, ref List<T> list) where T : class
        {
            if (!ValidateType<T>(t))
                return;

            if (o == null)
                o = Activator.CreateInstance(t);
            AddToList(o, ref list);
        }

        private class AttributeCallbackWrapper : IPostprocessBuildWithReport, IProcessSceneWithReport, IActiveBuildTargetChanged
        {
            internal int m_callbackOrder;
            internal MethodInfo m_method;

            public int callbackOrder { get { return m_callbackOrder; } }

            public AttributeCallbackWrapper(MethodInfo m)
            {
                m_callbackOrder = ((CallbackOrderAttribute)Attribute.GetCustomAttribute(m, typeof(CallbackOrderAttribute))).callbackOrder;
                m_method = m;
            }

            public void OnActiveBuildTargetChanged(BuildTarget previousTarget, BuildTarget newTarget)
            {
                m_method.Invoke(null, new object[] { previousTarget, newTarget });
            }

            public void OnPostprocessBuild(BuildReport report)
            {
                m_method.Invoke(null, new object[] { report.summary.platform, report.summary.outputPath });
            }

            public void OnProcessScene(UnityEngine.SceneManagement.Scene scene, BuildReport report)
            {
                m_method.Invoke(null, null);
            }
        }

        //this variable is reinitialized on domain reload so any calls to Init after a domain reload will set things up correctly
        static BuildCallbacks previousFlags = BuildCallbacks.None;
        [RequiredByNativeCode]
        internal static void InitializeBuildCallbacks(BuildCallbacks findFlags)
        {
            if (findFlags == previousFlags)
                return;

            CleanupBuildCallbacks();
            previousFlags = findFlags;

            bool findBuildProcessors = (findFlags & BuildCallbacks.BuildProcessors) == BuildCallbacks.BuildProcessors;
            bool findSceneProcessors = (findFlags & BuildCallbacks.SceneProcessors) == BuildCallbacks.SceneProcessors;
            bool findTargetProcessors = (findFlags & BuildCallbacks.BuildTargetProcessors) == BuildCallbacks.BuildTargetProcessors;
            bool findFilterProcessors = (findFlags & BuildCallbacks.FilterAssembliesProcessors) == BuildCallbacks.FilterAssembliesProcessors;
            bool findShaderProcessors = (findFlags & BuildCallbacks.ShaderProcessors) == BuildCallbacks.ShaderProcessors;
            bool findComputeShaderProcessors = (findFlags & BuildCallbacks.ComputeShaderProcessors) == BuildCallbacks.ComputeShaderProcessors;
            bool findBuildPlayerScriptDLLsProcessors = (findFlags & BuildCallbacks.BuildPlayerScriptDLLProcessors) == BuildCallbacks.BuildPlayerScriptDLLProcessors;
            bool findUnityLinkerProcessors = (findFlags & BuildCallbacks.UnityLinkerProcessors) == BuildCallbacks.UnityLinkerProcessors;
            bool findGenerateNativePluginsForAssembliesProcessors = (findFlags & BuildCallbacks.GenerateNativePluginsForAssembliesProcessors) == BuildCallbacks.GenerateNativePluginsForAssembliesProcessors;

            var postProcessBuildAttributeParams = new Type[] { typeof(BuildTarget), typeof(string) };
            foreach (var t in TypeCache.GetTypesDerivedFrom<IOrderedCallback>())
            {
                if (t.IsAbstract || t.IsInterface)
                    continue;

                // Defer creating the instance until we actually add it to one of the lists
                object instance = null;

                if (findBuildProcessors)
                {
                    AddToListIfTypeImplementsInterface(t, ref instance, ref processors.buildPlayerProcessors);
                    AddToListIfTypeImplementsInterface(t, ref instance, ref processors.buildPreprocessors);
                    AddToListIfTypeImplementsInterface(t, ref instance, ref processors.buildPreprocessorsWithReport);
                    AddToListIfTypeImplementsInterface(t, ref instance, ref processors.buildPostprocessors);
                    AddToListIfTypeImplementsInterface(t, ref instance, ref processors.buildPostprocessorsWithReport);
                }

                if (findSceneProcessors)
                {
                    AddToListIfTypeImplementsInterface(t, ref instance, ref processors.sceneProcessors);
                    AddToListIfTypeImplementsInterface(t, ref instance, ref processors.sceneProcessorsWithReport);
                }

                if (findTargetProcessors)
                {
                    AddToListIfTypeImplementsInterface(t, ref instance, ref processors.buildTargetProcessors);
                }

                if (findFilterProcessors)
                {
                    AddToListIfTypeImplementsInterface(t, ref instance, ref processors.filterBuildAssembliesProcessor);
                }

                if (findUnityLinkerProcessors)
                {
                    AddToListIfTypeImplementsInterface(t, ref instance, ref processors.unityLinkerProcessors);
                }

                if (findGenerateNativePluginsForAssembliesProcessors)
                {
                    AddToListIfTypeImplementsInterface(t, ref instance, ref processors.generateNativePluginsForAssembliesProcessors);
                }

                if (findShaderProcessors)
                {
                    AddToListIfTypeImplementsInterface(t, ref instance, ref processors.shaderProcessors);
                }

                if (findComputeShaderProcessors)
                {
                    AddToListIfTypeImplementsInterface(t, ref instance, ref processors.computeShaderProcessors);
                }

                if (findBuildPlayerScriptDLLsProcessors)
                {
                    AddToListIfTypeImplementsInterface(t, ref instance, ref processors.buildPlayerScriptDLLProcessors);
                }
            }

            if (findBuildProcessors)
            {
                foreach (var m in EditorAssemblies.GetAllMethodsWithAttribute<Callbacks.PostProcessBuildAttribute>())
                    if (ValidateMethod<Callbacks.PostProcessBuildAttribute>(m, postProcessBuildAttributeParams))
                        AddToList(new AttributeCallbackWrapper(m), ref processors.buildPostprocessorsWithReport);
            }

            if (findSceneProcessors)
            {
                foreach (var m in EditorAssemblies.GetAllMethodsWithAttribute<Callbacks.PostProcessSceneAttribute>())
                    if (ValidateMethod<Callbacks.PostProcessSceneAttribute>(m, Type.EmptyTypes))
                        AddToList(new AttributeCallbackWrapper(m), ref processors.sceneProcessorsWithReport);
            }

            processors.buildPlayerProcessors?.Sort(CompareICallbackOrder);
            if (processors.buildPreprocessors != null)
                processors.buildPreprocessors.Sort(CompareICallbackOrder);
            if (processors.buildPreprocessorsWithReport != null)
                processors.buildPreprocessorsWithReport.Sort(CompareICallbackOrder);
            if (processors.buildPostprocessors != null)
                processors.buildPostprocessors.Sort(CompareICallbackOrder);
            if (processors.buildPostprocessorsWithReport != null)
                processors.buildPostprocessorsWithReport.Sort(CompareICallbackOrder);
            if (processors.buildTargetProcessors != null)
                processors.buildTargetProcessors.Sort(CompareICallbackOrder);
            if (processors.sceneProcessors != null)
                processors.sceneProcessors.Sort(CompareICallbackOrder);
            if (processors.sceneProcessorsWithReport != null)
                processors.sceneProcessorsWithReport.Sort(CompareICallbackOrder);
            if (processors.filterBuildAssembliesProcessor != null)
                processors.filterBuildAssembliesProcessor.Sort(CompareICallbackOrder);
            if (processors.unityLinkerProcessors != null)
                processors.unityLinkerProcessors.Sort(CompareICallbackOrder);
            if (processors.generateNativePluginsForAssembliesProcessors != null)
                processors.generateNativePluginsForAssembliesProcessors.Sort(CompareICallbackOrder);
            if (processors.shaderProcessors != null)
                processors.shaderProcessors.Sort(CompareICallbackOrder);
            if (processors.computeShaderProcessors != null)
                processors.computeShaderProcessors.Sort(CompareICallbackOrder);
            if (processors.buildPlayerScriptDLLProcessors != null)
                processors.buildPlayerScriptDLLProcessors.Sort(CompareICallbackOrder);
        }

        internal static bool ValidateType<T>(Type t)
        {
            return (typeof(T).IsAssignableFrom(t) && t != typeof(AttributeCallbackWrapper));
        }

        static bool ValidateMethod<T>(MethodInfo method, Type[] expectedArguments)
        {
            Type attribute = typeof(T);
            if (method.IsDefined(attribute, false))
            {
                // Remove the `Attribute` from the name.
                if (!method.IsStatic)
                {
                    string atributeName = attribute.Name.Replace("Attribute", "");
                    Debug.LogErrorFormat("Method {0} with {1} attribute must be static.", method.Name, atributeName);
                    return false;
                }

                if (method.IsGenericMethod || method.IsGenericMethodDefinition)
                {
                    string atributeName = attribute.Name.Replace("Attribute", "");
                    Debug.LogErrorFormat("Method {0} with {1} attribute cannot be generic.", method.Name, atributeName);
                    return false;
                }

                var parameters = method.GetParameters();
                bool signatureCorrect = parameters.Length == expectedArguments.Length;
                if (signatureCorrect)
                {
                    // Check types match
                    for (int i = 0; i < parameters.Length; ++i)
                    {
                        if (parameters[i].ParameterType != expectedArguments[i])
                        {
                            signatureCorrect = false;
                            break;
                        }
                    }
                }

                if (!signatureCorrect)
                {
                    string atributeName = attribute.Name.Replace("Attribute", "");
                    string expectedArgumentsString = "static void " + method.Name + "(";

                    for (int i = 0; i < expectedArguments.Length; ++i)
                    {
                        expectedArgumentsString += expectedArguments[i].Name;
                        if (i != expectedArguments.Length - 1)
                            expectedArgumentsString += ", ";
                    }
                    expectedArgumentsString += ")";

                    Debug.LogErrorFormat("Method {0} with {1} attribute does not have the correct signature, expected: {2}.", method.Name, atributeName, expectedArgumentsString);
                    return false;
                }
                return true;
            }
            return false;
        }

        private static bool InvokeCallbackInterfacesPair<T1, T2>(List<T1> oneInterfaces, Action<T1> invocationOne, List<T2> twoInterfaces, Action<T2> invocationTwo, bool exitOnFailure) where T1 : IOrderedCallback where T2 : IOrderedCallback
        {
            if (oneInterfaces == null && twoInterfaces == null)
                return true;

            // We want to walk both interface lists and invoke the callbacks, but if we just did the whole of list 1 followed by the whole of list 2, the ordering would be wrong.
            // So, we have to walk both lists simultaneously, calling whichever callback has the lower ordering value
            IEnumerator<T1> e1 = (oneInterfaces != null) ? (IEnumerator<T1>)oneInterfaces.GetEnumerator() : null;
            IEnumerator<T2> e2 = (twoInterfaces != null) ? (IEnumerator<T2>)twoInterfaces.GetEnumerator() : null;
            if (e1 != null && !e1.MoveNext())
                e1 = null;
            if (e2 != null && !e2.MoveNext())
                e2 = null;

            while (e1 != null || e2 != null)
            {
                try
                {
                    if (e1 != null && (e2 == null || e1.Current.callbackOrder < e2.Current.callbackOrder))
                    {
                        var callback = e1.Current;
                        if (!e1.MoveNext())
                            e1 = null;
                        invocationOne(callback);
                    }
                    else if (e2 != null)
                    {
                        var callback = e2.Current;
                        if (!e2.MoveNext())
                            e2 = null;
                        invocationTwo(callback);
                    }
                }
                catch (TargetInvocationException e)
                {
                    // Note: Attribute based callbacks are called via reflection.
                    // Exceptions in those calls are wrapped in TargetInvocationException
                    Debug.LogException(e.InnerException);
                    if (exitOnFailure)
                        return false;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    if (exitOnFailure)
                        return false;
                }
            }

            return true;
        }

        internal static void PreparePlayerBuild(BuildPlayerContext context)
        {
            foreach (var p in processors.buildPlayerProcessors ?? new List<BuildPlayerProcessor>())
                p.PrepareForBuild(context);
        }

        [RequiredByNativeCode]
        internal static void OnBuildPreProcess(BuildReport report)
        {
#pragma warning disable 618
            InvokeCallbackInterfacesPair(
                processors.buildPreprocessors, bpp => bpp.OnPreprocessBuild(report.summary.platform, report.summary.outputPath),
                processors.buildPreprocessorsWithReport, bpp => bpp.OnPreprocessBuild(report),
                (report.summary.options & BuildOptions.StrictMode) != 0 || (report.summary.assetBundleOptions & BuildAssetBundleOptions.StrictMode) != 0);
#pragma warning restore 618

            // NOTE: This is a workaround for PLAT-11795.
            // Sometimes, when a player settings override is modified in one of the callbacks, its internal
            // serialized version is not updated prior to the build. As a result it will be restored to the
            // serialized values. To avoid that situation we force the update here.
            var profile = BuildProfile.GetActiveBuildProfile();
            if (profile != null)
                profile.SerializePlayerSettings();
        }

        [RequiredByNativeCode]
        internal static void OnSceneProcess(UnityEngine.SceneManagement.Scene scene, BuildReport report)
        {
#pragma warning disable 618
            InvokeCallbackInterfacesPair(
                processors.sceneProcessors, spp =>
                {
                    using (new EditorPerformanceMarker($"{spp.GetType().Name}.{nameof(spp.OnProcessScene)}", spp.GetType()).Auto())
                        spp.OnProcessScene(scene);
                },
                processors.sceneProcessorsWithReport, spp =>
                {
                    using (new EditorPerformanceMarker($"{spp.GetType().Name}.{nameof(spp.OnProcessScene)}", spp.GetType()).Auto())
                        spp.OnProcessScene(scene, report);
                },
                report && ((report.summary.options & BuildOptions.StrictMode) != 0 || (report.summary.assetBundleOptions & BuildAssetBundleOptions.StrictMode) != 0));
#pragma warning restore 618
        }

        [RequiredByNativeCode]
        internal static Hash128 OnSceneProcess_HashVersion()
        {
            Hash128 hashVersion = new Hash128();

            Type versionAttrribute = typeof(BuildCallbackVersionAttribute);
#pragma warning disable 618
            if (processors.sceneProcessors != null)
            {
                foreach (IProcessScene processor in processors.sceneProcessors)
                {
                    Type processorType = processor.GetType();
                    hashVersion.Append(processorType.AssemblyQualifiedName);

                    BuildCallbackVersionAttribute attribute = Attribute.GetCustomAttribute(processorType, versionAttrribute, false) as BuildCallbackVersionAttribute;
                    hashVersion.Append(attribute != null ? attribute.Version : 1);
                }
            }
#pragma warning restore 618

            if (processors.sceneProcessorsWithReport != null)
            {
                foreach (IProcessSceneWithReport processor in processors.sceneProcessorsWithReport)
                {
                    Type processorType = processor.GetType();
                    hashVersion.Append(processorType.AssemblyQualifiedName);

                    BuildCallbackVersionAttribute attribute = Attribute.GetCustomAttribute(processorType, versionAttrribute, false) as BuildCallbackVersionAttribute;
                    hashVersion.Append(attribute != null ? attribute.Version : 1);
                }
            }
            return hashVersion;
        }

        [RequiredByNativeCode]
        internal static void OnBuildPostProcess(BuildReport report)
        {
#pragma warning disable 618
            InvokeCallbackInterfacesPair(
                processors.buildPostprocessors, bpp => bpp.OnPostprocessBuild(report.summary.platform, report.summary.outputPath),
                processors.buildPostprocessorsWithReport, bpp => bpp.OnPostprocessBuild(report),
                (report.summary.options & BuildOptions.StrictMode) != 0 || (report.summary.assetBundleOptions & BuildAssetBundleOptions.StrictMode) != 0);
#pragma warning restore 618
        }


        // Some platforms like Desktop, instead of launching the app via C#, perform their launch in C++
        // See BuildPlayer.cpp LaunchPlayerIfSupported, which calls native LaunchApplication
        // Internal_OnPostprocessLaunch is used by this code path
        [RequiredByNativeCode]
        internal static void Internal_OnPostprocessLaunch(BuildTarget buildTarget, bool success)
        {
            OnPostprocessLaunch(new DefaultLaunchReport(NamedBuildTarget.FromActiveSettings(buildTarget), success ? LaunchResult.Succeeded : LaunchResult.Failed));
        }

        internal static void OnPostprocessLaunch(ILaunchReport launchReport)
        {
            // Domain reload happens after player build, so anything collected in InitializeBuildCallbacks gets invalidated
            // Thus collect callbacks here as necessary
            if (processors.launchPostprocessors == null)
            {
                foreach (var t in TypeCache.GetTypesDerivedFrom<IPostprocessLaunch>())
                {
                    if (t.IsAbstract || t.IsInterface)
                        continue;

                    object instance = null;
                    AddToListIfTypeImplementsInterface(t, ref instance, ref processors.launchPostprocessors);
                }

                processors?.launchPostprocessors?.Sort(CompareICallbackOrder);
            }

            if (processors.launchPostprocessors == null)
                return;

            foreach (var run in processors.launchPostprocessors)
            {
                try
                {
                    run.OnPostprocessLaunch(launchReport);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        [RequiredByNativeCode]
        internal static void OnActiveBuildTargetChanged(BuildTarget previousPlatform, BuildTarget newPlatform)
        {
            if (processors.buildTargetProcessors != null)
            {
                foreach (IActiveBuildTargetChanged abtc in processors.buildTargetProcessors)
                {
                    try
                    {
                        abtc.OnActiveBuildTargetChanged(previousPlatform, newPlatform);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
            }
        }

        [RequiredByNativeCode]
        internal static ShaderCompilerData[] OnPreprocessShaders(Shader shader, ShaderSnippetData snippet, ShaderCompilerData[] data)
        {
            var dataList = data.ToList();
            if (processors.shaderProcessors != null)
            {
                foreach (IPreprocessShaders abtc in processors.shaderProcessors)
                {
                    abtc.OnProcessShader(shader, snippet, dataList);
                }
            }
            return dataList.ToArray();
        }

        [RequiredByNativeCode]
        internal static ShaderCompilerData[] OnPreprocessComputeShaders(ComputeShader shader, string kernelName, ShaderCompilerData[] data)
        {
            var dataList = data.ToList();
            if (processors.computeShaderProcessors != null)
            {
                foreach (IPreprocessComputeShaders abtc in processors.computeShaderProcessors)
                {
                    try
                    {
                        abtc.OnProcessComputeShader(shader, kernelName, dataList);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
            }
            return dataList.ToArray();
        }

        [RequiredByNativeCode]
        internal static bool HasOnPostBuildPlayerScriptDLLs()
        {
            return (processors.buildPlayerScriptDLLProcessors != null && processors.buildPlayerScriptDLLProcessors.Any());
        }

        [RequiredByNativeCode]
        internal static void OnPostBuildPlayerScriptDLLs(BuildReport report)
        {
            if (processors.buildPlayerScriptDLLProcessors != null)
            {
                foreach (var step in processors.buildPlayerScriptDLLProcessors)
                {
                    try
                    {
                        step.OnPostBuildPlayerScriptDLLs(report);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                        if ((report.summary.options & BuildOptions.StrictMode) != 0 || (report.summary.assetBundleOptions & BuildAssetBundleOptions.StrictMode) != 0)
                            return;
                    }
                }
            }
        }

        [RequiredByNativeCode]
        internal static string[] FilterAssembliesIncludedInBuild(BuildOptions buildOptions, string[] assemblies)
        {
            if (processors.filterBuildAssembliesProcessor == null)
            {
                return assemblies;
            }

            string[] startAssemblies = assemblies;
            string[] filteredAssemblies = assemblies;


            foreach (var filteredAssembly in processors.filterBuildAssembliesProcessor)
            {
                int assemblyCount = filteredAssemblies.Length;
                filteredAssemblies = filteredAssembly.OnFilterAssemblies(buildOptions, filteredAssemblies);
                if (filteredAssemblies.Length > assemblyCount)
                {
                    throw new Exception("More Assemblies in the list than delivered. Only filtering, not adding extra assemblies");
                }
            }

            if (!filteredAssemblies.All(x => startAssemblies.Contains(x)))
            {
                throw new Exception("New Assembly names are in the list. Only filtering are allowed");
            }

            return filteredAssemblies;
        }

        [RequiredByNativeCode]
        internal static void CleanupBuildCallbacks()
        {
            processors.buildTargetProcessors = null;
            processors.buildPlayerProcessors = null;
            processors.buildPreprocessors = null;
            processors.buildPostprocessors = null;
            processors.sceneProcessors = null;
            processors.buildPreprocessorsWithReport = null;
            processors.buildPostprocessorsWithReport = null;
            processors.sceneProcessorsWithReport = null;
            processors.filterBuildAssembliesProcessor = null;
            processors.unityLinkerProcessors = null;
            processors.generateNativePluginsForAssembliesProcessors = null;
            processors.shaderProcessors = null;
            processors.computeShaderProcessors = null;
            processors.buildPlayerScriptDLLProcessors = null;
            previousFlags = BuildCallbacks.None;
        }
    }
}
