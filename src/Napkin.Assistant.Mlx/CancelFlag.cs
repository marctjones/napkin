using System.Runtime.InteropServices;

namespace Napkin.Assistant.Mlx;

/// <summary>
/// The cancel flag napkin owns and the bridge polls (the header's <c>const int32_t* cancel</c>,
/// mlx-runtime.md §1.5): four bytes of unmanaged memory, zero until <see cref="Set"/> writes 1,
/// freed when the handle is released.
/// </summary>
/// <remarks>
/// A <see cref="SafeHandle"/>, so a P/Invoke that is still reading the flag keeps it alive past a
/// <see cref="SafeHandle.Dispose()"/> (the marshaller adds a reference for the call). Reading or
/// setting a flag that is already released is not an error: it reads as set — the owner is gone,
/// so whatever polls it should stop — and setting it does nothing.
/// </remarks>
public sealed unsafe class CancelFlag : SafeHandle
{
    /// <summary>A new flag, not set.</summary>
    public CancelFlag()
        : base(IntPtr.Zero, ownsHandle: true)
    {
        SetHandle((IntPtr)NativeMemory.AllocZeroed(sizeof(int)));
    }

    /// <inheritdoc/>
    public override bool IsInvalid => handle == IntPtr.Zero;

    /// <summary>Whether the flag is set (or released).</summary>
    public bool IsSet
    {
        get
        {
            bool added = false;
            try
            {
                DangerousAddRef(ref added);
                return Volatile.Read(ref *(int*)handle) != 0;
            }
            catch (ObjectDisposedException)
            {
                return true;
            }
            finally
            {
                if (added)
                {
                    DangerousRelease();
                }
            }
        }
    }

    /// <summary>Sets the flag (a release store the bridge's acquire load sees); nothing once released.</summary>
    public void Set()
    {
        bool added = false;
        try
        {
            DangerousAddRef(ref added);
            Volatile.Write(ref *(int*)handle, 1);
        }
        catch (ObjectDisposedException)
        {
            // Released: nothing is polling it any more.
        }
        finally
        {
            if (added)
            {
                DangerousRelease();
            }
        }
    }

    /// <summary>Sets the flag when <paramref name="cancel"/> is cancelled; dispose the registration before the flag.</summary>
    /// <param name="cancel">The token whose cancellation sets the flag.</param>
    public CancellationTokenRegistration SetWhenCancelled(CancellationToken cancel) =>
        cancel.UnsafeRegister(static state => ((CancelFlag)state!).Set(), this);

    /// <inheritdoc/>
    protected override bool ReleaseHandle()
    {
        NativeMemory.Free((void*)handle);
        return true;
    }
}
