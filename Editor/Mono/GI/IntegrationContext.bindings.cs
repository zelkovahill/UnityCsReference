// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using System.Runtime.InteropServices;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Unity.RenderPipelines.Core.Editor")]
namespace UnityEngine.LightBaking
{
    [StructLayout(LayoutKind.Sequential)]
    internal class IntegrationContext : IDisposable
    {
        static extern IntPtr Internal_Create();
        static extern void Internal_Destroy(IntPtr ptr);

        internal IntPtr m_Ptr;
        internal bool m_OwnsPtr;

        public IntegrationContext()
        {
            m_Ptr = Internal_Create();
            m_OwnsPtr = true;
        }
        public IntegrationContext(IntPtr ptr)
        {
            m_Ptr = ptr;
            m_OwnsPtr = false;
        }
        ~IntegrationContext()
        {
            Destroy();
        }
        public void Dispose()
        {
            Destroy();
            GC.SuppressFinalize(this);
        }
        void Destroy()
        {
            if (m_OwnsPtr && m_Ptr != IntPtr.Zero)
            {
                Internal_Destroy(m_Ptr);
                m_Ptr = IntPtr.Zero;
            }
        }
        internal static class BindingsMarshaller
        {
            public static IntPtr ConvertToNative(IntegrationContext obj) => obj.m_Ptr;
        }
    }
}
