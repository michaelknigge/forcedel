namespace MK.Tools.ForceDel
{
    using System;
    using System.Security.Permissions;
    using Microsoft.Win32.SafeHandles;

    /// <summary>
    /// Diese Klasse kapselt ein Object-Handle aus nativem Code.
    /// </summary>
    [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
    internal sealed class SafeObjectHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        /// <summary>
        /// Voll-Konstruktor der Klasse SafeObjectHandle.
        /// </summary>
        /// <param name="preexistingHandle">Bereits existierendes Handls</param>
        /// <param name="ownsHandle">true , um das Handle während der Finalisierungsphase zuverlässig freizugeben, und false, um eine zuverlässige Freigabe zu verhindern (nicht empfohlen)</param>
        internal SafeObjectHandle(IntPtr preexistingHandle, bool ownsHandle)
            : base(ownsHandle)
        {
            this.SetHandle(preexistingHandle);
        }

        /// <summary>
        /// Minimal-Konstruktor der Klasse SafeObjectHandle.
        /// </summary>
        private SafeObjectHandle()
            : base(true)
        {
        }

        /// <summary>
        /// Schliesst das Handle.
        /// </summary>
        /// <returns>true gdw das Handle geschlossen werden konnte.</returns>
        protected override bool ReleaseHandle()
        {
            return NativeMethods.CloseHandle(this.handle);
        }
    }
}
