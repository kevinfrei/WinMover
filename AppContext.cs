using WinMover.Properties;

namespace WinMover {
  internal class AppContext : ApplicationContext {
    private NotifyIcon trayIcon;
    private GlobalKeyboardHook globalKeyboardHook;
    private KeyboardHandler keyboardHandler;

    public AppContext() {
      trayIcon = new NotifyIcon() {
        ContextMenuStrip = new ContextMenuStrip() {
          Items = { new ToolStripMenuItem("Toggle Handler", null, MenuItem_Click),
          new ToolStripSeparator(),
          new ToolStripMenuItem("Exit", null, Exit)}
        },
        Icon = Resources.AppIcon,
        Visible = true,
      };
      globalKeyboardHook = new GlobalKeyboardHook();
      globalKeyboardHook.KeyboardPressed += OnKeyPressed;
      keyboardHandler = new KeyboardHandler();
    }

    void Exit(object? sender, EventArgs e) {
      // Hide tray icon, otherwise it will remain shown until user mouses over it
      trayIcon.Visible = false;
      Application.Exit();
    }
    private void MenuItem_Click(object? sender, EventArgs e) {
      GlobalKeyboardHook.RegisteredKeys =
        (GlobalKeyboardHook.RegisteredKeys == null) ? KeyboardHandler.keysToWatch : null;
    }

    private void OnKeyPressed(object? sender, GlobalKeyboardHookEventArgs e) {
      // EDT: No need to filter for VkSnapshot anymore. This now gets handled
      // through the constructor of GlobalKeyboardHook(...).
      // Now you can access both, the key and virtual code
      var kd = e.KeyboardData;
      var ks = e.KeyboardState;
      // MessageBox.Show($"{kd.Key}, {kd.VirtualCode}, {kd.HardwareScanCode}, {kd.TimeStamp}: {ks}", "Pressed");
      e.Handled = keyboardHandler.tryToHandle(kd, ks);
    }
  }
}
