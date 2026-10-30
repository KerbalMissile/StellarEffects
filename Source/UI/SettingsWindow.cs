using System.Reflection;
using UnityEngine;

namespace StellarEffects.UI
{
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    internal sealed class SettingsWindow : MonoBehaviour
    {
        private const string LockId = "StellarEffects_SettingsWindow";
        private const int WindowId = 0x53544C52;

        private static string version = ReadVersion();

        private Rect rect = new Rect(120, 120, 500, 400);
        private bool visible;

        void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        void Update()
        {
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

            if (ctrl && alt && Input.GetKeyDown(KeyCode.V))
            {
                visible = !visible;
                if (!visible) ReleaseLock();
            }
        }

        void OnGUI()
        {
            if (!visible) return;

            GUI.skin = HighLogic.Skin;
            rect = GUI.Window(WindowId, rect, Draw, "StellarEffects v" + version);

            if (rect.Contains(Event.current.mousePosition))
                InputLockManager.SetControlLock(ControlTypes.ALLBUTCAMERAS, LockId);
            else
                ReleaseLock();
        }

        void OnDisable()
        {
            ReleaseLock();
        }

        private void Draw(int id)
        {
            GUI.DragWindow();
        }

        private static void ReleaseLock()
        {
            InputLockManager.RemoveControlLock(LockId);
        }

        private static string ReadVersion()
        {
            var asm = typeof(SettingsWindow).Assembly;
            var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            string v = info != null ? info.InformationalVersion : asm.GetName().Version.ToString(3);

            int plus = v.IndexOf('+');
            if (plus >= 0) v = v.Substring(0, plus);
            return v;
        }
    }
}