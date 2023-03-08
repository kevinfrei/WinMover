namespace WinMover {
  internal class KeyboardHandler {
    private static Keys[] modifiers = new Keys[] {
      Keys.LControlKey,
      Keys.RControlKey,
      Keys.LMenu,
      Keys.Menu,
      Keys.LShiftKey,
      Keys.RShiftKey
    };
    public static List<Keys>? keysToWatch;

    private int altDown = 0;
    private int ctrlDown = 0;
    private int shiftDown = 0;

    private Keypress[] triggers;

    private Keypress[] LoadFile() {
      triggers = new Keypress[] {
        new Keypress(true, true, true, Keys.J),
        new Keypress(true, true, true, Keys.H),
        new Keypress(true, true, true, Keys.N),
        new Keypress(true, true, true, Keys.M),
      };
      keysToWatch = modifiers.Concat(triggers.Select(a => a.GetTriggerKey()).Distinct()).ToList();
      return triggers;
    }
    public KeyboardHandler() {
      // Next steps: Parse a config file
      // Make it easy to hand-write: JSON or YAML
      // Maybe JSON5 so it can have *comments*
      // (Or just JSON + comments...)
      // Initially, just make it move windows
      // I can add other stuff as time goes on. I miss hammerspoon...
      triggers = LoadFile();
    }

    public bool TryToHandle(LowLevelKeyboardInputEvent data, GlobalKeyboardHook.KeyboardState state) {
      bool isKeyUp = state.HasFlag(GlobalKeyboardHook.KeyboardState.SysKeyUp) || state.HasFlag(GlobalKeyboardHook.KeyboardState.KeyUp);
      bool isModifier = true;
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
          isModifier = false;
          break;
      }
      if (!isModifier) {
        // We only trigger from non-modifiers, right?
        // Honestly, it seems like we could trigger from random other combinations with an NKRO keyboard
        // We should also probably use a map here...
        foreach (var trig in triggers) {
          if (trig.IsTriggered(ctrlDown, altDown, shiftDown, data.Key)) {
            return true;
          }
        }
      }
      return false;
    }
  }

  internal class Keypress {
    readonly bool altPress;
    readonly bool ctrlPress;
    readonly bool shiftPress;
    readonly Keys keyPress;
    public Keypress(bool ctrl, bool alt, bool shift, Keys key) {
      ctrlPress = ctrl;
      altPress = alt;
      shiftPress = shift;
      keyPress = key;
    }

    public bool IsTriggered(int ctrl, int alt, int shift, Keys key) {
      return (key == keyPress)
        && (ctrlPress == (ctrl != 0))
        && (altPress == (alt != 0))
        && (shiftPress == (shift != 0));
    }

    public Keys GetTriggerKey() {
      return keyPress;
    }
  }
}
