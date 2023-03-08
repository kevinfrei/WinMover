using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

// Based on https://stackoverflow.com/questions/604410/global-keyboard-capture-in-c-sharp-application
// Updated to .NET 6.0 because that's the new hotness?

namespace WinMover {
  class GlobalKeyboardHookEventArgs : HandledEventArgs {
    public GlobalKeyboardHook.KeyboardState KeyboardState { get; private set; }
    public LowLevelKeyboardInputEvent KeyboardData { get; private set; }

    public GlobalKeyboardHookEventArgs(
        LowLevelKeyboardInputEvent keyboardData,
        GlobalKeyboardHook.KeyboardState keyboardState) {
      KeyboardData = keyboardData;
      KeyboardState = keyboardState;
    }
  }

  // Check out https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-kbdllhookstruct
  [StructLayout(LayoutKind.Sequential)]
  public struct LowLevelKeyboardInputEvent {
    // A virtual-key code. The code must be a value in the range 1 to 254.
    // https://learn.microsoft.com/en-us/windows/win32/inputdev/virtual-key-codes
    public int VirtualCode;

    // The VirtualCode converted to typeof(Keys) for higher usability.
    public Keys Key { get { return (Keys)VirtualCode; } }
    public int HardwareScanCode;
    public int Flags;
    public int TimeStamp;
    // Additional information associated with the message. 
    public IntPtr AdditionalInformation;
  }

  class GlobalKeyboardHook : IDisposable {

    #region PInvoke Malarkey
    private IntPtr _user32LibraryHandle;

    // Removed docs: Go read MSDN if you want details on these PInvokes...
    [DllImport("kernel32.dll")] private static extern IntPtr LoadLibrary(string lpFileName);
    [DllImport("kernel32.dll", CharSet = CharSet.Auto)] private static extern bool FreeLibrary(IntPtr hModule);
    [DllImport("USER32", SetLastError = true)] static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, int dwThreadId);
    [DllImport("USER32", SetLastError = true)] public static extern bool UnhookWindowsHookEx(IntPtr hHook);
    [DllImport("USER32", SetLastError = true)] static extern IntPtr CallNextHookEx(IntPtr hHook, int code, IntPtr wParam, IntPtr lParam);
    private const int WH_KEYBOARD_LL = 13;
    #endregion

    delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);
    private IntPtr windowsHookHandle;
    private HookProc hookProc;

    public event EventHandler<GlobalKeyboardHookEventArgs>? KeyboardPressed;

    public GlobalKeyboardHook(IEnumerable<Keys>? registeredKeys = null) {
      RegisteredKeys = registeredKeys != null ? new List<Keys>(registeredKeys) : null;
      windowsHookHandle = IntPtr.Zero;
      hookProc = LowLevelKeyboardProc; // we must keep alive _hookProc, because GC is not aware about SetWindowsHookEx behaviour.

      _user32LibraryHandle = LoadLibrary("User32");
      if (_user32LibraryHandle == IntPtr.Zero) {
        int errorCode = Marshal.GetLastWin32Error();
        throw new Win32Exception(errorCode,
          $"Failed to load library 'User32.dll'. Error {errorCode}: {new Win32Exception(Marshal.GetLastWin32Error()).Message}.");
      }

      // https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowshookexw
      windowsHookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, hookProc, _user32LibraryHandle, 0);
      if (windowsHookHandle == IntPtr.Zero) {
        int errorCode = Marshal.GetLastWin32Error();
        throw new Win32Exception(errorCode, $"Failed to adjust keyboard hooks for '{Process.GetCurrentProcess().ProcessName}'. Error {errorCode}: {new Win32Exception(Marshal.GetLastWin32Error()).Message}.");
      }
    }

    #region stuff for LowLevelKeyboardProc
    public enum KeyboardState {
      KeyDown = 0x0100,
      KeyUp = 0x0101,
      SysKeyDown = 0x0104,
      SysKeyUp = 0x0105
    }
    // const int HC_ACTION = 0;
    private const int KfAltdown = 0x2000;
    private const int LlkhfAltdown = (KfAltdown >> 8);
    #endregion

    public static List<Keys>? RegisteredKeys = null;

    // See https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelkeyboardproc for details
    public IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam) {

      // This is kinda dumb. The Enum.isdefined<KeyboardState> should take an int. Whatever...
      KeyboardState keystate = (KeyboardState)wParam.ToInt32();
      if (!Enum.IsDefined(keystate)) {
        // Try dfiltering off the modifiers
        keystate = (KeyboardState)(wParam.ToInt64() & ~(Int64)Keys.Modifiers);
      }
      if (Enum.IsDefined(keystate)) {
        LowLevelKeyboardInputEvent p = Marshal.PtrToStructure<LowLevelKeyboardInputEvent>(lParam);

        if (RegisteredKeys != null && RegisteredKeys.Contains(p.Key)) {
          var eventArguments = new GlobalKeyboardHookEventArgs(p, (KeyboardState)wParam.ToInt32());
          KeyboardPressed?.Invoke(this, eventArguments);
          if (eventArguments.Handled) {
            return (IntPtr)1;
          }
        }
      }

      return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    #region Disposal/Cleanup
    protected virtual void Dispose(bool disposing) {
      if (disposing) {
        // because we can unhook only in the same thread, not in garbage collector thread
        if (windowsHookHandle != IntPtr.Zero) {
          if (!UnhookWindowsHookEx(windowsHookHandle)) {
            int errorCode = Marshal.GetLastWin32Error();
            throw new Win32Exception(errorCode, $"Failed to remove keyboard hooks for '{Process.GetCurrentProcess().ProcessName}'. Error {errorCode}: {new Win32Exception(Marshal.GetLastWin32Error()).Message}.");
          }
          windowsHookHandle = IntPtr.Zero;

          // ReSharper disable once DelegateSubtraction
          if (hookProc != null) {
#pragma warning disable CS8601 // Possible null reference assignment.
            hookProc -= LowLevelKeyboardProc;
#pragma warning restore CS8601 // Possible null reference assignment.
          }
        }
      }

      if (_user32LibraryHandle != IntPtr.Zero) {
        if (!FreeLibrary(_user32LibraryHandle)) // reduces reference to library by 1.
        {
          int errorCode = Marshal.GetLastWin32Error();
          throw new Win32Exception(errorCode, $"Failed to unload library 'User32.dll'. Error {errorCode}: {new Win32Exception(Marshal.GetLastWin32Error()).Message}.");
        }
        _user32LibraryHandle = IntPtr.Zero;
      }
    }

    ~GlobalKeyboardHook() {
      Dispose(false);
    }

    public void Dispose() {
      Dispose(true);
      GC.SuppressFinalize(this);
    }
    #endregion
  }
}

