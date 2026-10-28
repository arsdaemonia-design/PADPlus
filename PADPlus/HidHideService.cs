using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace PADPlus {
    public static class HidHideService {
        private static readonly string[] PossibleCliPaths = new string[] {
            @"C:\Program Files\Nefarius Software Solutions\HidHideCLI\HidHideCLI.exe",
            @"C:\Program Files\Nefarius Software Solutions\HidHide\x64\HidHideCLI.exe",
            @"C:\Program Files\Nefarius Software Solutions\HidHide\HidHideCLI.exe",
            Path.Combine(AppContext.BaseDirectory, "HidHideCLI.exe"),
            Path.Combine(AppContext.BaseDirectory, "Drivers", "HidHideCLI.exe")
        };

        private static string _cliPath = null;
        private static readonly HashSet<string> _hiddenDevices = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static bool _unhideCompleted;

        public static bool IsInstalled {
            get {
                if (_cliPath != null && File.Exists(_cliPath)) return true;
                foreach (var path in PossibleCliPaths) {
                    if (File.Exists(path)) {
                        _cliPath = path;
                        return true;
                    }
                }
                return false;
            }
        }

        public static event Action<string> OnLog;

        public static void Initialize() {
            if (!IsInstalled) {
                OnLog?.Invoke("[HidHide] No instalado (opcional para evitar doble input).");
                return;
            }

            try {
                _unhideCompleted = false;
                // Registrar PAD+.exe en la lista blanca de HidHide
                string currentExe = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(currentExe)) {
                    RunCli($"--app-reg \"{currentExe}\"");
                    RunCli("--cloak-on");
                    OnLog?.Invoke("[HidHide] PAD+ registrado en lista blanca y cloaking activado.");
                }
            } catch (Exception ex) {
                OnLog?.Invoke($"[HidHide] Error al inicializar: {ex.Message}");
            }
        }

        public static void HideDevice(string hidPath) {
            if (!IsInstalled || string.IsNullOrWhiteSpace(hidPath)) return;

            try {
                // Extraer el Device Instance Path típico de Windows HID
                // Formato hidapi: \\?\hid#vid_057e&pid_2009&col01#...#{4d1e55b2-f16f-11cf-88cb-001111000030}
                string devInstance = FormatDeviceInstanceId(hidPath);
                if (!string.IsNullOrEmpty(devInstance) && !_hiddenDevices.Contains(devInstance)) {
                    _unhideCompleted = false;
                    RunCli($"--dev-hide \"{devInstance}\"");
                    _hiddenDevices.Add(devInstance);
                    OnLog?.Invoke($"[HidHide] Ocultado mando Switch DirectInput: {devInstance}");
                }
            } catch (Exception ex) {
                OnLog?.Invoke($"[HidHide] Error al ocultar dispositivo: {ex.Message}");
            }
        }

        public static void UnhideDevice(string hidPath) {
            if (!IsInstalled || string.IsNullOrWhiteSpace(hidPath)) return;

            string devInstance = FormatDeviceInstanceId(hidPath);
            if (string.IsNullOrEmpty(devInstance) || !_hiddenDevices.Contains(devInstance)) return;

            if (RunCli($"--dev-unhide \"{devInstance}\"") != null) {
                _hiddenDevices.Remove(devInstance);
                OnLog?.Invoke($"[HidHide] Dispositivo devuelto a Windows: {devInstance}");
            } else {
                OnLog?.Invoke($"[HidHide] No se pudo restaurar el dispositivo: {devInstance}");
            }
        }

        public static void UnhideAll() {
            if (!IsInstalled) return;
            if (_unhideCompleted) return;

            try {
                foreach (var devInstance in _hiddenDevices) {
                    RunCli($"--dev-unhide \"{devInstance}\"");
                }
                _hiddenDevices.Clear();
                RunCli("--cloak-off");
                _unhideCompleted = true;
                OnLog?.Invoke("[HidHide] Mandos desocultados y cloaking desactivado.");
            } catch (Exception ex) {
                OnLog?.Invoke($"[HidHide] Error al desocultar dispositivos: {ex.Message}");
            }
        }

        private static string FormatDeviceInstanceId(string path) {
            // Elimina prefijo \\?\ y sufijos de GUID de interfaz
            string p = path.TrimStart('\\', '?', '/');
            int guidIdx = p.LastIndexOf("#{", StringComparison.Ordinal);
            if (guidIdx > 0) {
                p = p.Substring(0, guidIdx);
            }
            return p.Replace('#', '\\').ToUpperInvariant();
        }

        private static string RunCli(string args) {
            if (!IsInstalled) return null;

            try {
                using var p = new Process {
                    StartInfo = new ProcessStartInfo {
                        FileName = _cliPath,
                        Arguments = args,
                        CreateNoWindow = true,
                        UseShellExecute = false
                    }
                };
                p.Start();
                // HidHideCLI can stall while the driver/service is shutting down. Do not
                // read redirected pipes before waiting: ReadToEnd can block indefinitely.
                if (!p.WaitForExit(3000)) {
                    try {
                        p.Kill(entireProcessTree: true);
                        p.WaitForExit(500);
                    } catch { }
                    OnLog?.Invoke($"[HidHide] Tiempo de espera agotado al ejecutar {Path.GetFileName(_cliPath)} {args}.");
                    return null;
                }

                return p.ExitCode == 0 ? string.Empty : null;
            } catch {
                return null;
            }
        }
    }
}

