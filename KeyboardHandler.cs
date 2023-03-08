namespace WinMover {
  internal class KeyboardHandler {
    static public Keys[] keysToWatch = new Keys[] {
      Keys.LControlKey,
      Keys.RControlKey,
      Keys.LMenu,
      Keys.Menu,
      Keys.LShiftKey,
      Keys.RShiftKey
    };

    private int altDown = 0;
    private int ctrlDown = 0;
    private int shiftDown = 0;

    public KeyboardHandler() {
    }

    public bool tryToHandle(LowLevelKeyboardInputEvent data, GlobalKeyboardHook.KeyboardState state) {
      bool isKeyUp = state.HasFlag(GlobalKeyboardHook.KeyboardState.SysKeyUp) || state.HasFlag(GlobalKeyboardHook.KeyboardState.KeyUp);
      switch (data.Key) {
        case Keys.LControlKey:
        case Keys.RControlKey:
          ctrlDown = isKeyUp ? 0 : data.TimeStamp;
          break;
        case Keys.LMenu:
        case Keys.RMenu:
          altDown = isKeyUp ? 0 : data.TimeStamp;
          break;
        case Keys.LShiftKey:
        case Keys.RShiftKey:
          shiftDown = isKeyUp ? 0 : data.TimeStamp;
          break;
        default:
          // Anything we need to do for other keys?
          break;
      }
      if (ctrlDown != 0 && altDown != 0 && shiftDown != 0) {
        switch (data.Key) {
          case Keys.A:
            MessageBox.Show("Hyper A!", isKeyUp ? "Released" : "Pressed");
            return true;
        }
      }
      return false;
    }
  }
}
