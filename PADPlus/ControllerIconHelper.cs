using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace PADPlus {
    public static class ControllerIconHelper {
        private static readonly ConcurrentDictionary<string, Image> _iconCache = new ConcurrentDictionary<string, Image>();
        private static readonly string IconsDir = Path.Combine(AppContext.BaseDirectory, "Icons");

        public static Image GetIcon(string key) {
            if (string.IsNullOrEmpty(key)) return null;

            if (_iconCache.TryGetValue(key, out var cached) && cached != null) {
                return cached;
            }

            Image img = LoadCustomIcon(key);
            if (img == null) {
                img = LoadDefaultResourceRecolored(key);
            }

            if (img != null) {
                _iconCache[key] = img;
            }

            return img;
        }

        private static Image LoadCustomIcon(string key) {
            try {
                string[] candidates = new string[] {
                    Path.Combine(IconsDir, $"custom_{key}.png"),
                    Path.Combine(IconsDir, $"{key}.png")
                };

                foreach (var path in candidates) {
                    if (File.Exists(path)) {
                        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                            return Image.FromStream(fs);
                        }
                    }
                }
            } catch { }
            return null;
        }

        private static Image LoadDefaultResourceRecolored(string key) {
            Bitmap src = null;
            switch (key.ToLowerInvariant()) {
                case "pro":
                    src = Properties.Resources.pro;
                    break;
                case "jc_left":
                    src = Properties.Resources.jc_left;
                    break;
                case "jc_right":
                    src = Properties.Resources.jc_right;
                    break;
                case "jc_left_s":
                    src = Properties.Resources.jc_left_s;
                    break;
                case "jc_right_s":
                    src = Properties.Resources.jc_right_s;
                    break;
                case "snes":
                    src = Properties.Resources.snes;
                    break;
                case "n64":
                    src = Properties.Resources.ultra;
                    break;
                case "empty":
                case "cross":
                    src = Properties.Resources.cross;
                    break;
                default:
                    return null;
            }

            if (src == null) return null;

            try {
                return RecolorDarkSilhouetteToHighContrast(src);
            } catch {
                return src;
            }
        }

        private static Bitmap RecolorDarkSilhouetteToHighContrast(Bitmap original) {
            Bitmap result = new Bitmap(original.Width, original.Height, PixelFormat.Format32bppArgb);
            Color highContrast = Color.FromArgb(232, 240, 255); // Blanco/Cian claro

            int visiblePixels = 0;
            long totalBrightness = 0;

            for (int y = 0; y < original.Height; y++) {
                for (int x = 0; x < original.Width; x++) {
                    Color p = original.GetPixel(x, y);
                    if (p.A > 30) {
                        visiblePixels++;
                        totalBrightness += (p.R + p.G + p.B) / 3;
                    }
                }
            }

            bool isMostlyDark = visiblePixels > 0 && (totalBrightness / visiblePixels) < 110;

            for (int y = 0; y < original.Height; y++) {
                for (int x = 0; x < original.Width; x++) {
                    Color p = original.GetPixel(x, y);
                    if (p.A <= 10) {
                        result.SetPixel(x, y, Color.Transparent);
                    } else if (isMostlyDark) {
                        // Mantener canal alfa original pero aplicar tinte blanco luminoso
                        Color tinted = Color.FromArgb(p.A, highContrast.R, highContrast.G, highContrast.B);
                        result.SetPixel(x, y, tinted);
                    } else {
                        result.SetPixel(x, y, p);
                    }
                }
            }

            return result;
        }

        public static void SelectCustomIcon(string key, IWin32Window owner, Action onUpdated) {
            using (OpenFileDialog ofd = new OpenFileDialog()) {
                ofd.Title = $"Seleccionar Icono Personalizado para [{key}]";
                ofd.Filter = "Imágenes PNG (*.png)|*.png|Todos los archivos (*.*)|*.*";
                if (ofd.ShowDialog(owner) == DialogResult.OK) {
                    try {
                        Directory.CreateDirectory(IconsDir);
                        string dest = Path.Combine(IconsDir, $"custom_{key}.png");
                        File.Copy(ofd.FileName, dest, true);

                        _iconCache.TryRemove(key, out _);
                        onUpdated?.Invoke();
                        MessageBox.Show("¡Icono personalizado aplicado correctamente!", "PAD+", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    } catch (Exception ex) {
                        MessageBox.Show("Error al guardar el icono: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        public static void ResetCustomIcon(string key, Action onUpdated) {
            try {
                string dest = Path.Combine(IconsDir, $"custom_{key}.png");
                if (File.Exists(dest)) {
                    File.Delete(dest);
                }
                _iconCache.TryRemove(key, out _);
                onUpdated?.Invoke();
                MessageBox.Show("Se ha restablecido el icono por defecto.", "PAD+", MessageBoxButtons.OK, MessageBoxIcon.Information);
            } catch (Exception ex) {
                MessageBox.Show("Error al restablecer icono: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}

