using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PADPlus {
    public static class BluetoothAutoPairService {
        private const int BLUETOOTH_MAX_NAME_SIZE = 248;
        private const uint BLUETOOTH_SERVICE_ENABLE = 0x00000001;

        // Standard Human Interface Device (HID) Service Class UUID
        private static readonly Guid HidServiceGuid = new Guid("00001124-0000-1000-8000-00805f9b34fb");

        [StructLayout(LayoutKind.Sequential)]
        private struct BLUETOOTH_FIND_RADIO_PARAMS {
            public uint dwSize;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct BLUETOOTH_DEVICE_SEARCH_PARAMS {
            public uint dwSize;
            public bool fReturnAuthenticated;
            public bool fReturnRemembered;
            public bool fReturnUnknown;
            public bool fReturnConnected;
            public bool fIssueInquiry;
            public byte cTimeoutMultiplier;
            public IntPtr hRadio;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SYSTEMTIME {
            public ushort wYear;
            public ushort wMonth;
            public ushort wDayOfWeek;
            public ushort wDay;
            public ushort wHour;
            public ushort wMinute;
            public ushort wSecond;
            public ushort wMilliseconds;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct BLUETOOTH_DEVICE_INFO {
            public uint dwSize;
            public ulong Address;
            public uint ulClassofDevice;
            public bool fConnected;
            public bool fRemembered;
            public bool fAuthenticated;
            public SYSTEMTIME stLastSeen;
            public SYSTEMTIME stLastUsed;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = BLUETOOTH_MAX_NAME_SIZE)]
            public string szName;
        }

        [DllImport("bthprops.cpl", SetLastError = true)]
        private static extern IntPtr BluetoothFindFirstRadio(ref BLUETOOTH_FIND_RADIO_PARAMS pbtfrp, out IntPtr phRadio);

        [DllImport("bthprops.cpl", SetLastError = true)]
        private static extern bool BluetoothFindNextRadio(IntPtr hFind, out IntPtr phRadio);

        [DllImport("bthprops.cpl", SetLastError = true)]
        private static extern bool BluetoothFindRadioClose(IntPtr hFind);

        [DllImport("bthprops.cpl", SetLastError = true)]
        private static extern bool BluetoothFindDeviceClose(IntPtr hFind);

        [DllImport("bthprops.cpl", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr BluetoothFindFirstDevice(ref BLUETOOTH_DEVICE_SEARCH_PARAMS pbtsp, ref BLUETOOTH_DEVICE_INFO pbtdi);

        [DllImport("bthprops.cpl", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool BluetoothFindNextDevice(IntPtr hFind, ref BLUETOOTH_DEVICE_INFO pbtdi);

        [DllImport("bthprops.cpl", SetLastError = true)]
        private static extern uint BluetoothRemoveDevice(ref ulong pAddress);

        [DllImport("bthprops.cpl", SetLastError = true)]
        private static extern uint BluetoothSetServiceState(IntPtr hRadio, ref BLUETOOTH_DEVICE_INFO pbtdi, ref Guid pGuidService, uint dwServiceFlags);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        public static event Action<string> OnLog;

        private static CancellationTokenSource _cts;
        private static bool _isRunning = false;

        public static bool IsRunning => _isRunning;

        public static void StartAutoPairing() {
            if (_isRunning) return;
            _cts = new CancellationTokenSource();
            _isRunning = true;
            Task.Run(() => ScanLoop(_cts.Token));
        }

        public static void StopAutoPairing() {
            if (!_isRunning) return;
            _cts?.Cancel();
            _isRunning = false;
        }

        private static async Task ScanLoop(CancellationToken token) {
            OnLog?.Invoke("[Auto-Pair] Monitor Bluetooth iniciado.");
            while (!token.IsCancellationRequested) {
                try {
                    ScanAndPairOnce();
                } catch (Exception ex) {
                    OnLog?.Invoke($"[Auto-Pair] Error en ciclo: {ex.Message}");
                }

                try {
                    await Task.Delay(3000, token);
                } catch (OperationCanceledException) {
                    break;
                }
            }
            OnLog?.Invoke("[Auto-Pair] Monitor Bluetooth detenido.");
        }

        public static int ScanAndPairOnce() {
            int pairedCount = 0;
            BLUETOOTH_FIND_RADIO_PARAMS radioParams = new BLUETOOTH_FIND_RADIO_PARAMS {
                dwSize = (uint)Marshal.SizeOf<BLUETOOTH_FIND_RADIO_PARAMS>()
            };

            IntPtr hRadioFind = BluetoothFindFirstRadio(ref radioParams, out IntPtr hRadio);
            if (hRadioFind == IntPtr.Zero) {
                return 0; // Sin radio Bluetooth disponible
            }

            try {
                do {
                    pairedCount += SearchDevicesOnRadio(hRadio);
                    CloseHandle(hRadio);
                } while (BluetoothFindNextRadio(hRadioFind, out hRadio));
            } finally {
                BluetoothFindRadioClose(hRadioFind);
            }

            return pairedCount;
        }

        private static int SearchDevicesOnRadio(IntPtr hRadio) {
            int count = 0;
            BLUETOOTH_DEVICE_SEARCH_PARAMS searchParams = new BLUETOOTH_DEVICE_SEARCH_PARAMS {
                dwSize = (uint)Marshal.SizeOf<BLUETOOTH_DEVICE_SEARCH_PARAMS>(),
                fReturnAuthenticated = true,
                fReturnRemembered = true,
                fReturnUnknown = true,
                fReturnConnected = false,
                fIssueInquiry = true,
                cTimeoutMultiplier = 2, // ~2.5s inquiry
                hRadio = hRadio
            };

            BLUETOOTH_DEVICE_INFO deviceInfo = new BLUETOOTH_DEVICE_INFO {
                dwSize = (uint)Marshal.SizeOf<BLUETOOTH_DEVICE_INFO>()
            };

            IntPtr hDeviceFind = BluetoothFindFirstDevice(ref searchParams, ref deviceInfo);
            if (hDeviceFind == IntPtr.Zero) {
                return 0;
            }

            try {
                do {
                    if (IsSwitchController(deviceInfo.szName)) {
                        OnLog?.Invoke($"[Auto-Pair] Mando Switch detectado: {deviceInfo.szName}");

                        // Si está atascado o no conectado pero visible en modo sync, forzamos habilitación del servicio HID
                        Guid guid = HidServiceGuid;
                        uint res = BluetoothSetServiceState(hRadio, ref deviceInfo, ref guid, BLUETOOTH_SERVICE_ENABLE);
                        if (res == 0) {
                            OnLog?.Invoke($"[Auto-Pair] Servicio HID activado con éxito para {deviceInfo.szName}");
                            count++;
                        }
                    }
                } while (BluetoothFindNextDevice(hDeviceFind, ref deviceInfo));
            } finally {
                BluetoothFindDeviceClose(hDeviceFind);
            }

            return count;
        }

        private static bool IsSwitchController(string name) {
            if (string.IsNullOrWhiteSpace(name)) return false;
            return name.StartsWith("Joy-Con", StringComparison.OrdinalIgnoreCase) ||
                   name.StartsWith("Pro Controller", StringComparison.OrdinalIgnoreCase) ||
                   name.StartsWith("SNES Controller", StringComparison.OrdinalIgnoreCase) ||
                   name.StartsWith("N64 Controller", StringComparison.OrdinalIgnoreCase) ||
                   name.IndexOf("Nintendo", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Switch", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("GameCube", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}

