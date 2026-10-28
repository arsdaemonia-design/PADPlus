using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Timers;
using System.Windows.Forms;
using PADPlus.Collections;
using Nefarius.ViGEm.Client;
using static PADPlus._3rdPartyControllers;
using static PADPlus.HIDapi;

namespace PADPlus {
    public class JoyconManager {
        public bool EnableIMU = true;
        public bool EnableLocalize = false;

        private const ushort vendor_id = 0x57e;
        private const ushort product_l = 0x2006;
        private const ushort product_r = 0x2007;
        private const ushort product_pro = 0x2009;
        private const ushort product_grip = 0x200e;
        private const ushort product_snes = 0x2017;
        private const ushort product_n64 = 0x2019;
        private const ushort product_genesis = 0x201e;
        // Switch 2 Product IDs
        private const ushort product_jc2_l = 0x2066;   // Switch 2 Joy-Con Left
        private const ushort product_jc2_r = 0x2067;   // Switch 2 Joy-Con Right
        private const ushort product_pro2 = 0x2069;    // Switch 2 Pro Controller
        private const ushort product_gc = 0x2073;      // Switch 2 NSO GameCube Controller
        // DirectInput Bridge
        private const ushort product_generic_dinput = 0xFFFF; // Generic DirectInput Gamepad

        public ConcurrentList<Joycon> j { get; private set; } // Array of all connected Joy-Cons
        static JoyconManager instance;

        public MainForm form;

        System.Timers.Timer controllerCheck;
        private readonly object _scanLock = new object();
        private readonly HashSet<string> _manuallyDisconnectedPaths = new(StringComparer.OrdinalIgnoreCase);
        private long _lastConnectTick = 0;
        public long LastConnectTick => _lastConnectTick;

        public static JoyconManager Instance {
            get { return instance; }
        }

        public void Awake() {
            instance = this;
            j = new ConcurrentList<Joycon>();
            HIDapi.hid_init();
        }

        public void Start() {
            controllerCheck = new System.Timers.Timer(2000); // check for new controllers every 2 seconds
            controllerCheck.Elapsed += CheckForNewControllersTime;
            controllerCheck.Start();
        }

        bool ControllerAlreadyAdded(string path) {
            foreach (Joycon v in j)
                if (v.path == path)
                    return true;
            return false;
        }

        void CleanUp() { // removes dropped controllers from list
            if (!Monitor.TryEnter(_scanLock)) return;
            try {
                List<Joycon> rem = new List<Joycon>();
                foreach (Joycon joycon in j) {
                    if (joycon.state == Joycon.state_.DROPPED) {
                        if (joycon.other != null)
                            joycon.other.other = null; // The other of the other is the joycon itself

                        joycon.Detach(true);
                        HidHideService.UnhideDevice(joycon.path);
                        rem.Add(joycon);

                        foreach (Button b in form.con) {
                            if (b.Enabled & b.Tag == joycon) {
                                b.Invoke(new MethodInvoker(delegate {
                                    b.BackColor = System.Drawing.Color.FromArgb(30, 32, 38);
                                    b.Enabled = false;
                                    b.Text = "";
                                    b.BackgroundImage = ControllerIconHelper.GetIcon("empty");
                                    form.SetDisconnectButtonVisible(b, false);
                                }));
                                break;
                            }
                        }

                        form.AppendTextBox("Mando desconectado eliminado. Listo para reconectar.\r\n");
                    }
                }

                foreach (Joycon v in rem)
                    j.Remove(v);
            } finally {
                Monitor.Exit(_scanLock);
            }
        }

        public bool DisconnectController(Joycon joycon) {
            if (joycon == null) return false;

            lock (_scanLock) {
                if (!j.Any(v => ReferenceEquals(v, joycon))) return false;

                _manuallyDisconnectedPaths.Add(joycon.path);
                if (joycon.state != Joycon.state_.DROPPED)
                    joycon.state = Joycon.state_.DROPPED;
            }

            CleanUp();
            return true;
        }

        void CheckForNewControllersTime(Object source, ElapsedEventArgs e) {
            CleanUp();
            if (Config.IntValue("ProgressiveScan") == 1) {
                CheckForNewControllers();
            }
        }

        private ushort TypeToProdId(byte type) {
            switch (type) {
                case 1:
                    return product_pro;
                case 2:
                    return product_l;
                case 3:
                    return product_r;
                case 4:
                    return product_generic_dinput;
            }
            return 0;
        }

        private static bool ProbeDeviceAwake(IntPtr handle) {
            byte[] testBuf = new byte[49];
            int res = HIDapi.hid_read_timeout(handle, testBuf, new UIntPtr(49), 40);
            return res > 0;
        }

        public void CheckForNewControllers() {
            if (!Monitor.TryEnter(_scanLock)) return;
            try {
                // move all code for initializing devices here and well as the initial code from Start()
                bool isLeft = false;
                IntPtr ptr = HIDapi.hid_enumerate(0x0, 0x0);
                IntPtr top_ptr = ptr;
                HashSet<string> enumeratedPaths = new(StringComparer.OrdinalIgnoreCase);

                hid_device_info enumerate; // Add device to list
                bool foundNew = false;
                while (ptr != IntPtr.Zero) {
                    SController thirdParty = null;
                    enumerate = (hid_device_info)Marshal.PtrToStructure(ptr, typeof(hid_device_info));
                    if (!string.IsNullOrEmpty(enumerate.path))
                        enumeratedPaths.Add(enumerate.path);

                    string serial = enumerate.serial_number;
                    if (string.IsNullOrEmpty(serial)) {
                        serial = "000000000001";
                    }

                    bool validController = (enumerate.product_id == product_l || enumerate.product_id == product_r ||
                                            enumerate.product_id == product_pro || enumerate.product_id == product_grip ||
                                            enumerate.product_id == product_snes || enumerate.product_id == product_n64 ||
                                            enumerate.product_id == product_genesis ||
                                            enumerate.product_id == product_jc2_l || enumerate.product_id == product_jc2_r ||
                                            enumerate.product_id == product_pro2 || enumerate.product_id == product_gc) && enumerate.vendor_id == vendor_id;
                    // check list of custom controllers specified
                    foreach (SController v in Program.thirdPartyCons) {
                        if (enumerate.vendor_id == v.vendor_id && enumerate.product_id == v.product_id && (string.IsNullOrEmpty(v.serial_number) || serial == v.serial_number)) {
                            validController = true;
                            thirdParty = v;
                            break;
                        }
                    }

                    if (!validController) {
                        ptr = enumerate.next;
                        continue;
                    }

                    ushort prod_id = thirdParty == null ? enumerate.product_id : TypeToProdId(thirdParty.type);
                    if (prod_id == 0) {
                        ptr = enumerate.next; // controller was not assigned a type, but advance ptr anyway
                        continue;
                    }

                    if (_manuallyDisconnectedPaths.Contains(enumerate.path)) {
                        bool deviceSleeping = false;
                        if (thirdParty == null && serial != "000000000001") {
                            IntPtr probeHandle = HIDapi.hid_open_path(enumerate.path);
                            if (probeHandle != IntPtr.Zero) {
                                deviceSleeping = !ProbeDeviceAwake(probeHandle);
                                HIDapi.hid_close(probeHandle);
                            }
                        }

                        if (deviceSleeping) {
                            _manuallyDisconnectedPaths.Remove(enumerate.path);
                        } else {
                            ptr = enumerate.next;
                            continue;
                        }

                        // Let the next scan reopen a Bluetooth controller that was powered off.
                        ptr = enumerate.next;
                        continue;
                    }

                    if (validController && !ControllerAlreadyAdded(enumerate.path)) {
                        IntPtr handle = HIDapi.hid_open_path(enumerate.path);
                        if (handle == IntPtr.Zero) {
                            ptr = enumerate.next;
                            continue;
                        }

                        // The serial fallback distinguishes many Nintendo BT nodes, but custom
                        // third-party USB pads can also have a serial. Do not run the BT wake
                        // probe on explicitly registered third-party devices.
                        bool isBT = thirdParty == null && serial != "000000000001";
                        if (isBT && !ProbeDeviceAwake(handle)) {
                            HIDapi.hid_close(handle);
                            ptr = enumerate.next;
                            continue;
                        }

                        switch (prod_id) {
                            case product_l:
                            case product_jc2_l:
                                isLeft = true;
                                form.AppendTextBox(prod_id == product_jc2_l ? "Switch 2 Joy-Con Izquierdo conectado.\r\n" : "Joy-Con Izquierdo conectado.\r\n"); break;
                            case product_r:
                            case product_jc2_r:
                                isLeft = false;
                                form.AppendTextBox(prod_id == product_jc2_r ? "Switch 2 Joy-Con Derecho conectado.\r\n" : "Joy-Con Derecho conectado.\r\n"); break;
                            case product_pro:
                            case product_pro2:
                                isLeft = true;
                                form.AppendTextBox(prod_id == product_pro2 ? "Switch 2 Pro Controller conectado.\r\n" : "Switch Pro Controller conectado.\r\n"); break;
                            case product_snes:
                                isLeft = true;
                                form.AppendTextBox("Mando SNES Switch conectado.\r\n"); break;
                            case product_n64:
                                isLeft = true;
                                form.AppendTextBox("Mando N64 Switch conectado.\r\n"); break;
                            case product_genesis:
                                isLeft = true;
                                form.AppendTextBox("Mando SEGA Genesis conectado.\r\n"); break;
                            case product_gc:
                                isLeft = true;
                                form.AppendTextBox("Switch 2 GameCube Controller conectado.\r\n"); break;
                            case product_generic_dinput:
                                isLeft = true;
                                form.AppendTextBox("Mando DirectInput Genérico conectado (Puente a XInput).\r\n"); break;
                            case product_grip:
                                byte[] gripCmd = new byte[64];
                                gripCmd[0] = 0x80; gripCmd[1] = 0x01;
                                HIDapi.hid_write(handle, gripCmd, new UIntPtr(2));
                                byte[] gripResp = new byte[64];
                                int gripRet = HIDapi.hid_read_timeout(handle, gripResp, new UIntPtr(64), 100);
                                if (gripRet > 3 && gripResp[3] == 0x02) {
                                    isLeft = false;
                                    form.AppendTextBox("Charging Grip (Joy-Con Derecho) conectado.\r\n");
                                } else {
                                    isLeft = true;
                                    form.AppendTextBox("Charging Grip (Joy-Con Izquierdo) conectado.\r\n");
                                }
                                break;
                            default:
                                form.AppendTextBox("Dispositivo Nintendo no compatible omitido.\r\n"); break;
                        }

                        // Hide device in HidHide to avoid double-input
                        HidHideService.HideDevice(enumerate.path);
                        // -------------------- //

                        try {
                            HIDapi.hid_set_nonblocking(handle, 1);
                        } catch {
                            form.AppendTextBox("No se pudo abrir la conexión con el dispositivo. ¿Está siendo utilizado por otro programa?\r\n");
                            HIDapi.hid_close(handle);
                            ptr = enumerate.next;
                            continue;
                        }

                        bool isGenericDirectInput = (prod_id == product_generic_dinput);
                        bool isSwitch2 = (prod_id == product_pro2 || prod_id == product_jc2_l || prod_id == product_jc2_r || prod_id == product_gc);
                        bool isPro = prod_id == product_pro || prod_id == product_grip || prod_id == product_pro2 || prod_id == product_gc || isGenericDirectInput;
                        bool isSnes = prod_id == product_snes;
                        bool is64 = prod_id == product_n64;
                        var newJoycon = new Joycon(handle, EnableIMU, EnableLocalize & EnableIMU, 0.05f, isLeft, enumerate.path, serial, j.Count, isPro, isSnes, is64, thirdParty != null);
                        newJoycon.isSwitch2 = isSwitch2;
                        newJoycon.isGenericDirectInput = isGenericDirectInput;
                        if (isGenericDirectInput)
                            newJoycon.isUSB = true;
                        j.Add(newJoycon);

                        foundNew = true;
                        _lastConnectTick = Environment.TickCount64;
                        j.Last().form = form;

                        if (j.Count < 5) {
                            int ii = -1;
                            foreach (Button v in form.con) {
                                ii++;
                                if (!v.Enabled) {
                                    string iconKey;
                                    if (isGenericDirectInput || thirdParty != null) {
                                        if (thirdParty != null && thirdParty.name != null && (thirdParty.name.IndexOf("fight", StringComparison.OrdinalIgnoreCase) >= 0 || thirdParty.name.IndexOf("gamecube", StringComparison.OrdinalIgnoreCase) >= 0)) {
                                            iconKey = "fightpro";
                                        } else {
                                            iconKey = "directinput";
                                        }
                                    } else {
                                        switch (prod_id) {
                                            case (product_l):
                                            case (product_jc2_l):
                                                iconKey = "jc_left_s"; break;
                                            case (product_r):
                                            case (product_jc2_r):
                                                iconKey = "jc_right_s"; break;
                                            case (product_pro):
                                            case (product_pro2):
                                            case (product_grip):
                                                iconKey = "pro"; break;
                                            case (product_snes):
                                                iconKey = "snes"; break;
                                            case (product_n64):
                                            case (product_gc):
                                                iconKey = "n64"; break;
                                            default:
                                                iconKey = isPro ? "pro" : (isLeft ? "jc_left_s" : "jc_right_s"); break;
                                        }
                                    }
                                    System.Drawing.Image temp = ControllerIconHelper.GetIcon(iconKey);

                                    v.Invoke(new MethodInvoker(delegate {
                                        v.Tag = j.Last(); // assign controller to button
                                        v.Enabled = true;
                                        v.BackgroundImage = temp;
                                    form.SetDisconnectButtonVisible(v, true);
                                    }));

                                    form.loc[ii].Invoke(new MethodInvoker(delegate {
                                        form.loc[ii].Tag = v;
                                    }));

                                    break;
                                }
                            }
                        }

                        byte[] mac = new byte[6];
                        try {
                            if (!string.IsNullOrEmpty(enumerate.serial_number) && enumerate.serial_number.Length >= 12) {
                                for (int n = 0; n < 6; n++)
                                    mac[n] = byte.Parse(enumerate.serial_number.Substring(n * 2, 2), System.Globalization.NumberStyles.HexNumber);
                            }
                        } catch (Exception e) {
                            // could not parse mac address
                        }
                        j[j.Count - 1].PadMacAddress = new PhysicalAddress(mac);
                    }

                    ptr = enumerate.next;
                }

                if (foundNew) { // attempt to auto join-up joycons on connection
                    Joycon temp = null;
                    foreach (Joycon v in j) {
                        // Do not attach two controllers if they are either:
                        // - Not a Joycon
                        // - Already attached to another Joycon (that isn't itself)
                        if (v.isPro || (v.other != null && v.other != v)) {
                            continue;
                        }

                        // Otherwise, iterate through and find the Joycon with the lowest
                        // id that has not been attached already (Does not include self)
                        if (temp == null)
                            temp = v;
                        else if (temp.isLeft != v.isLeft && v.other == null) {
                            temp.other = v;
                            v.other = temp;

                            // Preserve Player 1 slot: keep temp.out_xbox alive and disconnect secondary Joy-Con's virtual pad
                            if (v.out_xbox != null) {
                                try {
                                    v.out_xbox.Disconnect();
                                } catch { }
                                v.out_xbox = null;
                            }
                            if (v.out_ds4 != null) {
                                try {
                                    v.out_ds4.Disconnect();
                                } catch { }
                                v.out_ds4 = null;
                            }

                            foreach (Button b in form.con)
                                if (b.Tag == v || b.Tag == temp) {
                                    Joycon tt = (b.Tag == v) ? v : (b.Tag == temp) ? temp : v;
                                    b.BackgroundImage = ControllerIconHelper.GetIcon(tt.isLeft ? "jc_left" : "jc_right");
                                }

                            temp = null;    // repeat
                        }
                    }
                }

                _manuallyDisconnectedPaths.RemoveWhere(path => !enumeratedPaths.Contains(path));
                HIDapi.hid_free_enumeration(top_ptr);

                bool on = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None).AppSettings.Settings["HomeLEDOn"].Value.ToLower() == "true";
                foreach (Joycon jc in j) { // Connect device straight away
                    if (jc.state == Joycon.state_.NOT_ATTACHED) {
                        if (jc.out_xbox != null)
                            jc.out_xbox.Connect();
                        if (jc.out_ds4 != null)
                            jc.out_ds4.Connect();

                        try {
                            jc.Attach();
                        } catch (Exception e) {
                            jc.state = Joycon.state_.DROPPED;
                            continue;
                        }

                        jc.SetHomeLight(on);

                        jc.Begin();
                        if (form.allowCalibration) {
                            jc.getActiveData();
                        }
                    }
                }
            } finally {
                Monitor.Exit(_scanLock);
            }
        }

        public void OnApplicationQuit() {
            foreach (Joycon v in j) {
                if (Boolean.Parse(ConfigurationManager.AppSettings["AutoPowerOff"]))
                    v.PowerOff();

                v.Detach();

                if (v.out_xbox != null) {
                    v.out_xbox.Disconnect();
                }

                if (v.out_ds4 != null) {
                    v.out_ds4.Disconnect();
                }
            }

            controllerCheck.Stop();
            HIDapi.hid_exit();
        }
    }

    class Program {
        public static PhysicalAddress btMAC = new PhysicalAddress(new byte[] { 0, 0, 0, 0, 0, 0 });
        public static UdpServer server;

        public static ViGEmClient emClient;

        private static readonly HttpClient client = new HttpClient();

        public static JoyconManager mgr;

        static MainForm form;

        public static List<SController> thirdPartyCons = new List<SController>();

        private static WindowsInput.Events.Sources.IKeyboardEventSource keyboard;
        private static WindowsInput.Events.Sources.IMouseEventSource mouse;

        public static void Start() {
            HidHideService.OnLog += msg => form?.console?.AppendText(msg + "\r\n");
            BluetoothAutoPairService.OnLog += msg => form?.console?.AppendText(msg + "\r\n");
            HidHideService.Initialize();

            if (Boolean.Parse(ConfigurationManager.AppSettings["ShowAsXInput"]) || Boolean.Parse(ConfigurationManager.AppSettings["ShowAsDS4"])) {
                try {
                    emClient = new ViGEmClient(); // Manages emulated XInput
                } catch (Nefarius.ViGEm.Client.Exceptions.VigemBusNotFoundException) {
                    form.console.AppendText("Could not start VigemBus. Make sure drivers are installed correctly.\r\n");
                }
            }

            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces()) {
                // Get local BT host MAC
                if (nic.NetworkInterfaceType != NetworkInterfaceType.FastEthernetFx && nic.NetworkInterfaceType != NetworkInterfaceType.Wireless80211) {
                    if (nic.Name.Split()[0] == "Bluetooth") {
                        btMAC = nic.GetPhysicalAddress();
                    }
                }
            }

            // a bit hacky
            _3rdPartyControllers partyForm = new _3rdPartyControllers();
            partyForm.CopyCustomControllers();

            mgr = new JoyconManager();
            mgr.form = form;
            mgr.Awake();
            mgr.CheckForNewControllers();
            mgr.Start();

            server = new UdpServer(mgr.j);
            server.form = form;

            server.Start(IPAddress.Parse(ConfigurationManager.AppSettings["IP"]), Int32.Parse(ConfigurationManager.AppSettings["Port"]));

            // Capture keyboard + mouse events for binding's sake
            keyboard = WindowsInput.Capture.Global.KeyboardAsync();
            keyboard.KeyEvent += Keyboard_KeyEvent;
            mouse = WindowsInput.Capture.Global.MouseAsync();
            mouse.MouseEvent += Mouse_MouseEvent;

            form.console.AppendText("All systems go\r\n");
        }

        private static void Mouse_MouseEvent(object sender, WindowsInput.Events.Sources.EventSourceEventArgs<WindowsInput.Events.Sources.MouseEvent> e) {
            if (e.Data.ButtonDown != null && MainForm.TryCaptureMapping("mse_" + ((int)e.Data.ButtonDown.Button))) {
                e.Next_Hook_Enabled = false;
                return;
            }

            if (e.Data.ButtonDown != null) {
                string res_val = Config.Value("reset_mouse");
                if (res_val.StartsWith("mse_"))
                    if ((int)e.Data.ButtonDown.Button == Int32.Parse(res_val.Substring(4)))
                        WindowsInput.Simulate.Events().MoveTo(
                            Screen.PrimaryScreen.Bounds.Left + Screen.PrimaryScreen.Bounds.Width / 2,
                            Screen.PrimaryScreen.Bounds.Top + Screen.PrimaryScreen.Bounds.Height / 2).Invoke();

                res_val = Config.Value("active_gyro");
                if (res_val.StartsWith("mse_"))
                    if ((int)e.Data.ButtonDown.Button == Int32.Parse(res_val.Substring(4)))
                        foreach (var i in mgr.j)
                            i.active_gyro = true;
            }

            if (e.Data.ButtonUp != null) {
                string res_val = Config.Value("active_gyro");
                if (res_val.StartsWith("mse_"))
                    if ((int)e.Data.ButtonUp.Button == Int32.Parse(res_val.Substring(4)))
                        foreach (var i in mgr.j)
                            i.active_gyro = false;
            }
        }

        private static void Keyboard_KeyEvent(object sender, WindowsInput.Events.Sources.EventSourceEventArgs<WindowsInput.Events.Sources.KeyboardEvent> e) {
            if (e.Data.KeyDown != null && MainForm.TryCaptureMapping("key_" + ((int)e.Data.KeyDown.Key))) {
                e.Next_Hook_Enabled = false;
                return;
            }

            if (e.Data.KeyDown != null) {
                string res_val = Config.Value("reset_mouse");
                if (res_val.StartsWith("key_"))
                    if ((int)e.Data.KeyDown.Key == Int32.Parse(res_val.Substring(4)))
                        WindowsInput.Simulate.Events().MoveTo(
                            Screen.PrimaryScreen.Bounds.Left + Screen.PrimaryScreen.Bounds.Width / 2,
                            Screen.PrimaryScreen.Bounds.Top + Screen.PrimaryScreen.Bounds.Height / 2).Invoke();

                res_val = Config.Value("active_gyro");
                if (res_val.StartsWith("key_"))
                    if ((int)e.Data.KeyDown.Key == Int32.Parse(res_val.Substring(4)))
                        foreach (var i in mgr.j)
                            i.active_gyro = true;
            }

            if (e.Data.KeyUp != null) {
                string res_val = Config.Value("active_gyro");
                if (res_val.StartsWith("key_"))
                    if ((int)e.Data.KeyUp.Key == Int32.Parse(res_val.Substring(4)))
                        foreach (var i in mgr.j)
                            i.active_gyro = false;
            }
        }

        public static void Stop() {
            keyboard.Dispose(); mouse.Dispose();
            HidHideService.UnhideAll();
            BluetoothAutoPairService.StopAutoPairing();
            server.Stop();
            mgr.OnApplicationQuit();
        }

        private static string appGuid = "1bf709e9-c133-41df-933a-c9ff3f664c7b"; // randomly-generated
        static void Main(string[] args) {
            AppDomain.CurrentDomain.UnhandledException += (s, e) => {
                try {
                    File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "crash.log"), e.ExceptionObject?.ToString() ?? "Unknown error");
                } catch { }
            };
            Application.ThreadException += (s, e) => {
                try {
                    File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "crash.log"), e.Exception?.ToString() ?? "Unknown error");
                } catch { }
            };
            AppDomain.CurrentDomain.ProcessExit += (s, e) => {
                try {
                    HidHideService.UnhideAll();
                } catch { }
            };

            // Setting the culturesettings so float gets parsed correctly
            CultureInfo.CurrentCulture = new CultureInfo("en-US", false);

            // Set the correct DLL for the current OS
            SetupDlls();

            using (Mutex mutex = new Mutex(false, "Global\\" + appGuid)) {
                if (!mutex.WaitOne(0, false)) {
                    MessageBox.Show("Instance already running.", "PAD+");
                    return;
                }

                Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                form = new MainForm();
                Application.Run(form);
            }
        }

        static void SetupDlls() {
            string archPath = $"{AppDomain.CurrentDomain.BaseDirectory}{(Environment.Is64BitProcess ? "x64" : "x86")}\\";
            string pathVariable = Environment.GetEnvironmentVariable("PATH");
            pathVariable = $"{archPath};{pathVariable}";
            Environment.SetEnvironmentVariable("PATH", pathVariable);
        }

        // Helper funtions to set the hidapi dll location acording to the system instruction set.
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SetDefaultDllDirectories(int directoryFlags);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern void AddDllDirectory(string lpPathName);
    }
}

