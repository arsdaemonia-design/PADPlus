using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using static PADPlus.HIDapi;

namespace PADPlus {
    public partial class MainForm : Form {
        public static SplitButton ActiveMapButton = null;

        public bool allowCalibration = Boolean.Parse(ConfigurationManager.AppSettings["AllowCalibration"]);
        public List<Button> con, loc;
        public bool calibrate;
        public List<KeyValuePair<string, float[]>> caliData;
        private Timer countDown;
        private int count;
        public List<int> xG, yG, zG, xA, yA, zA;
        public bool shakeInputEnabled = Boolean.Parse(ConfigurationManager.AppSettings["EnableShakeInput"]);
        public float shakeSesitivity = float.Parse(ConfigurationManager.AppSettings["ShakeInputSensitivity"]);
        public float shakeDelay = float.Parse(ConfigurationManager.AppSettings["ShakeInputDelay"]);

        public enum NonOriginalController : int {
            Disabled = 0,
            DefaultCalibration = 1,
            ControllerCalibration = 2,
        }

        public MainForm() {
            xG = new List<int>(); yG = new List<int>(); zG = new List<int>();
            xA = new List<int>(); yA = new List<int>(); zA = new List<int>();
            caliData = new List<KeyValuePair<string, float[]>> {
                new KeyValuePair<string, float[]>("0", new float[6] {0,0,0,-710,0,0})
            };

            InitializeComponent();
            ApplyModernTheme();

            picLogo.Image = ControllerIconHelper.GetIcon("logo");
            try {
                this.Icon = Properties.Resources.padplus_icon;
                notifyIcon.Icon = Properties.Resources.padplus_icon;
            } catch { }

            if (!allowCalibration)
                AutoCalibrate.Hide();

            con = new List<Button> { con1, con2, con3, con4 };
            loc = new List<Button> { loc1, loc2, loc3, loc4 };

            for (int i = 0; i < con.Count; i++) {
                con[i].Click += conBtnClick;
                con[i].BackgroundImage = ControllerIconHelper.GetIcon("empty");
                loc[i].Tag = con[i];
                loc[i].Click += locBtnClickAsync;
            }

            PopulateSettingsTable();
            InitializeMappingView();
        }

        public static void UpdateMapButtonLabel(SplitButton c) {
            if (c == null) return;
            string val = Config.Value((string)c.Tag);
            if (string.IsNullOrEmpty(val) || val == "0") {
                if ((string)c.Tag == "home")
                    c.Text = "Guide";
                else
                    c.Text = "Ninguno (Desactivado)";
                return;
            }

            try {
                if (val.StartsWith("joy_")) {
                    int code = int.Parse(val.Substring(4));
                    c.Text = Enum.GetName(typeof(Joycon.Button), code) ?? val;
                } else if (val.StartsWith("key_")) {
                    int code = int.Parse(val.Substring(4));
                    c.Text = Enum.GetName(typeof(WindowsInput.Events.KeyCode), code) ?? ("Key: " + code);
                } else if (val.StartsWith("mse_")) {
                    int code = int.Parse(val.Substring(4));
                    c.Text = Enum.GetName(typeof(WindowsInput.Events.ButtonCode), code) ?? ("Mouse: " + code);
                } else {
                    c.Text = val;
                }
            } catch {
                c.Text = val;
            }
        }

        private static readonly Dictionary<string, (string Title, string Description)> SettingDocs = new(StringComparer.OrdinalIgnoreCase) {
            { "IP", ("IP Servidor UDP", "Dirección IP para transmitir datos de movimiento (giroscopio) a emuladores compatibles (Cemu, Yuzu, Ryujinx, Dolphin).\r\nPor defecto: 127.0.0.1 (localhost). Cambia a 0.0.0.0 o tu IP local si el emulador corre en otra PC de la red.") },
            { "Port", ("Puerto Servidor UDP", "Puerto de red UDP para la transmisión de datos de movimiento.\r\nPor defecto: 26760. Debe coincidir con el puerto configurado en cemuhook.ini o en la configuración de mandos del emulador.") },
            { "MotionServer", ("Activar Servidor UDP", "Habilita o deshabilita el servidor de transmisión de movimiento por red.\r\nActívalo si juegas en emuladores que leen giroscopio por UDP (CemuHook).") },
            { "LowFreqRumble", ("Frecuencia Baja Vibración", "Ajusta la frecuencia del motor de graves para la vibración HD (HD Rumble).\r\nRango recomendado: 40 a 160. Subir el valor incrementa la intensidad del temblor grave.") },
            { "HighFreqRumble", ("Frecuencia Alta Vibración", "Ajusta la frecuencia del motor de agudos para la vibración HD (HD Rumble).\r\nRango recomendado: 120 a 320. Modifica la nitidez y cosquilleo de los efectos rápidos.") },
            { "EnableRumble", ("Activar Vibración (Rumble)", "Activa o desactiva completamente la retroalimentación de vibración en todos los mandos conectados.") },
            { "EnableShakeInput", ("Sacudida como Botón (Shake)", "Convierte el gesto de agitar físicamente el mando en una pulsación de botón (útil para juegos de Wii/PS4 emulados).") },
            { "ShakeInputSensitivity", ("Sensibilidad de Sacudida", "Fuerza o aceleración requerida para registrar el movimiento de sacudida.\r\nValores más bajos hacen la sacudida más sensible.") },
            { "ShakeInputDelay", ("Retardo entre Sacudidas (ms)", "Tiempo mínimo de espera en milisegundos entre dos detecciones de sacudida consecutivas (mínimo recomendado: 15 ms).") },
            { "SwapAB", ("Invertir Botones A ↔ B", "Intercambia los botones A y B para coincidir con la disposición física tradicional de mandos Xbox (A abajo, B a la derecha).") },
            { "SwapXY", ("Invertir Botones X ↔ Y", "Intercambia los botones X e Y para coincidir con la disposición física tradicional de mandos Xbox (X a la izquierda, Y arriba).") },
            { "AllowCalibration", ("Botón de Calibración", "Muestra el botón 'Calibrar' en la pantalla principal para reajustar los sensores de acelerómetro y giroscopio.") },
            { "N64Range", ("Rango Stick Nintendo 64", "Ajusta la escala del stick analógico a 0.8 para replicar con precisión el recorrido original de los mandos de N64 de Switch Online.") },
            { "acc_sensiti", ("Sensibilidad Acelerómetro", "Valores predeterminados de sensibilidad en 3 ejes del acelerómetro para mandos genéricos o clonados sin calibración de fábrica.") },
            { "gyr_sensiti", ("Sensibilidad Giroscopio", "Valores predeterminados de sensibilidad en 3 ejes del giroscopio para mandos genéricos o clonados sin calibración de fábrica.") },
            { "stick_cal", ("Calibración Stick Izquierdo", "Valores hexadecimales de centro y límites para el stick analógico izquierdo de mandos genéricos.") },
            { "deadzone", ("Zona Muerta Stick Izq", "Rango de zona muerta central para el stick izquierdo.\r\nAuméntalo si el personaje o cursor se mueve solo (drift).") },
            { "stick2_cal", ("Calibración Stick Derecho", "Valores hexadecimales de centro y límites para el stick analógico secundario de mandos genéricos.") },
            { "deadzone2", ("Zona Muerta Stick Der", "Rango de zona muerta central para el stick derecho.\r\nAuméntalo si la cámara se mueve sola (drift).") },
            { "StickScalingFactor", ("Escala Stick Izquierdo", "Multiplicador de sensibilidad para el stick analógico izquierdo (1.00 por defecto).\r\nAuméntalo si el stick físico no alcanza el 100% al empujarlo al tope.") },
            { "StickScalingFactor2", ("Escala Stick Derecho", "Multiplicador de sensibilidad para el stick analógico derecho (1.00 por defecto).\r\nAuméntalo si la cámara gira despacio o no llega al borde.") },
            { "GyroAnalogSliders", ("Gatillos con Giroscopio", "Permite modular progresivamente los gatillos analógicos (LT / RT) inclinando el mando hacia adelante o atrás.") },
            { "GyroAnalogSensitivity", ("Sensibilidad Gatillos Giro", "Sensibilidad y dirección para la modulación de gatillos mediante inclinación.\r\nUsa valores negativos (ej. -400) para invertir el sentido del ángulo.") },
            { "HomeLEDOn", ("Luz Anillo Botón HOME", "Mantiene encendido el aro LED azul alrededor del botón HOME en los mandos que cuentan con dicha luz.") },
            { "UseIncrementalLights", ("Luces LED de Jugador", "Usa combinaciones de los 4 LEDs inferiores para representar el número de jugador asignado de forma progresiva.") },
            { "GyroToJoyOrMouse", ("Giroscopio a Ratón / Stick", "Convierte el movimiento físico del giroscopio en control del cursor del ratón ('mouse') o de un stick analógico ('joy_left' / 'joy_right').") },
            { "UseFilteredIMU", ("Filtro IMU (Madgwick AHRS)", "Aplica filtrado matemático avanzado a los sensores de movimiento para suavizar el apuntado y eliminar el temblor natural de la mano.") },
            { "AHRS_beta", ("Parámetro Beta Filtro AHRS", "Tasa de respuesta del filtro de orientación.\r\nValores más altos reaccionan más rápido pero introducen más ruido; valores bajos son más suaves.") },
            { "GyroMouseSensitivityX", ("Sensibilidad Ratón X", "Velocidad de movimiento horizontal del puntero del ratón al apuntar con el giroscopio del mando.") },
            { "GyroMouseSensitivityY", ("Sensibilidad Ratón Y", "Velocidad de movimiento vertical del puntero del ratón al apuntar con el giroscopio del mando.") },
            { "GyroStickSensitivityX", ("Sensibilidad Stick X", "Sensibilidad horizontal cuando el giroscopio emula el movimiento de un stick analógico.") },
            { "GyroStickSensitivityY", ("Sensibilidad Stick Y", "Sensibilidad vertical cuando el giroscopio emula el movimiento de un stick analógico.") },
            { "GyroStickReduction", ("Reducción Stick al Apuntar", "Divide el rango del joystick físico mientras el giroscopio esté activo para permitir micro-apuntado de alta precisión.") },
            { "GyroHoldToggle", ("Modo Botón Giroscopio", "True = Mantener presionado el botón asignado para usar giroscopio.\r\nFalse = Modo alternador (un toque activa, otro desactiva).") },
            { "GyroMouseLeftHanded", ("Giroscopio Joy-Con Zurdo", "En un par de Joy-Cons combinados, usa el sensor del Joy-Con izquierdo para controlar el ratón en vez del derecho.") },
            { "DragToggle", ("Alternar Arrastre de Ratón", "Al mapear clics de ratón en botones del mando, permite alternar entre presionar y soltar sin tener que mantener pulsado el botón.") },
            { "ShowAsXInput", ("Emular Mando Xbox 360", "Emula mandos virtuales de Xbox 360 en Windows a través de ViGEmBus.\r\nOfrece la máxima compatibilidad nativa con juegos de PC, Steam y Xbox Game Pass.") },
            { "ShowAsDS4", ("Emular Mando DualShock 4", "Emula mandos virtuales de PlayStation 4 (DS4) a través de ViGEmBus.\r\nPermite compatibilidad con panel táctil y barra de luz en juegos compatibles.") },
            { "AutoPowerOff", ("Apagar Mandos al Salir", "Envía la orden de apagado o suspensión a los mandos Switch cuando se cierre la aplicación.") },
            { "PowerOffInactivity", ("Apagar por Inactividad (min)", "Minutos de inactividad sin presionar ningún botón antes de apagar los mandos automáticamente (-1 para desactivar).") },
            { "HomeLongPowerOff", ("Mantener HOME para Apagar", "Permite apagar el mando manteniendo presionado el botón HOME (o Capture) durante 2 segundos.") },
            { "ChangeOrientationDoubleClick", ("Doble Clic Stick Girar Joy-Con", "Permite presionar dos veces el stick analógico (L3/R3) para alternar entre posición horizontal y vertical en un Joy-Con individual.") },
            { "DoNotRejoinJoycons", ("No Reunir Joy-Cons", "Evita que dos Joy-Cons que fueron separados manualmente se vuelvan a combinar de forma automática al reconectarse.") },
            { "DebugType", ("Nivel de Registro (Debug)", "Define qué tipo de información de diagnóstico se imprime en la consola de la aplicación (0 = ninguno, 1 = todo, etc.).") }
        };

        private static readonly HashSet<string> IgnoredSettings = new(StringComparer.OrdinalIgnoreCase) {
            "UseHIDG",
            "PurgeAffectedDevices",
            "PurgeWhitelist"
        };

        private void PopulateSettingsTable() {
            settingsTable.SuspendLayout();
            settingsTable.Controls.Clear();
            settingsTable.RowStyles.Clear();

            string[] myConfigs = ConfigurationManager.AppSettings.AllKeys;
            var visibleKeys = myConfigs.Where(k => !IgnoredSettings.Contains(k)).ToArray();
            settingsTable.RowCount = visibleKeys.Length;

            for (int i = 0; i < visibleKeys.Length; i++) {
                string key = visibleKeys[i];
                string value = ConfigurationManager.AppSettings[key] ?? "";

                settingsTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));

                var doc = SettingDocs.TryGetValue(key, out var d) ? d : (Title: key, Description: "Configuración: " + key);

                // Panel en Columna 0 con botón [?] y etiqueta con título en español (usando TableLayoutPanel para alineación perfecta)
                var pnlKey = new TableLayoutPanel {
                    Dock = DockStyle.Fill,
                    RowCount = 1,
                    ColumnCount = 2,
                    Margin = new Padding(0),
                    Padding = new Padding(0),
                    Tag = key
                };
                pnlKey.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 28F));
                pnlKey.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                pnlKey.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

                var btnHelp = new Button {
                    Text = "¿?",
                    Dock = DockStyle.Fill,
                    Margin = new Padding(1, 4, 3, 4),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(40, 48, 60),
                    ForeColor = Color.FromArgb(0, 229, 255),
                    Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                btnHelp.FlatAppearance.BorderSize = 0;
                btnTip.SetToolTip(btnHelp, $"{doc.Title} ({key})\r\n\r\n{doc.Description}\r\n\r\n[Haz clic para leer la descripción detallada]");
                btnHelp.Click += (s, e) => {
                    MessageBox.Show(this, doc.Description, $"PAD+ — {doc.Title} ({key})", MessageBoxButtons.OK, MessageBoxIcon.Information);
                };

                var lbl = new Label {
                    Text = doc.Title,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft,
                    ForeColor = Color.FromArgb(235, 240, 248),
                    Font = new Font("Segoe UI", 8.25F, FontStyle.Regular),
                    AutoEllipsis = true,
                    Margin = new Padding(2, 0, 0, 0)
                };
                btnTip.SetToolTip(lbl, $"{doc.Title} ({key})\r\n{doc.Description}");

                pnlKey.Controls.Add(btnHelp, 0, 0);
                pnlKey.Controls.Add(lbl, 1, 0);
                settingsTable.Controls.Add(pnlKey, 0, i);

                Control childControl;
                if (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value.Equals("false", StringComparison.OrdinalIgnoreCase)) {
                    bool isChecked = Boolean.Parse(value);
                    var cb = new CheckBox {
                        Text = isChecked ? "Activado" : "Desactivado",
                        Checked = isChecked,
                        ForeColor = isChecked ? Color.FromArgb(0, 230, 118) : Color.FromArgb(176, 190, 197),
                        Dock = DockStyle.Left,
                        AutoSize = true,
                        Font = new Font("Segoe UI", 8.5F, isChecked ? FontStyle.Bold : FontStyle.Regular)
                    };
                    cb.CheckedChanged += cbBox_Changed;
                    childControl = cb;
                } else if (key.Equals("GyroToJoyOrMouse", StringComparison.OrdinalIgnoreCase)) {
                    var cmb = new ComboBox {
                        DropDownStyle = ComboBoxStyle.DropDownList,
                        Dock = DockStyle.Fill,
                        BackColor = Color.FromArgb(30, 31, 35),
                        ForeColor = Color.White,
                        FlatStyle = FlatStyle.Flat,
                        Font = new Font("Segoe UI", 8.25F, FontStyle.Regular)
                    };
                    cmb.Items.AddRange(new object[] {
                        "Desactivado (false)",
                        "Ratón (mouse)",
                        "Stick Izquierdo (joy_left)",
                        "Stick Derecho (joy_right)"
                    });
                    string valLower = value.ToLower();
                    if (valLower == "mouse") cmb.SelectedIndex = 1;
                    else if (valLower == "joy_left") cmb.SelectedIndex = 2;
                    else if (valLower == "joy_right") cmb.SelectedIndex = 3;
                    else cmb.SelectedIndex = 0;

                    cmb.SelectedIndexChanged += cbBox_Changed;
                    childControl = cmb;
                } else if (key.Equals("DebugType", StringComparison.OrdinalIgnoreCase)) {
                    var cmb = new ComboBox {
                        DropDownStyle = ComboBoxStyle.DropDownList,
                        Dock = DockStyle.Fill,
                        BackColor = Color.FromArgb(30, 31, 35),
                        ForeColor = Color.White,
                        FlatStyle = FlatStyle.Flat,
                        Font = new Font("Segoe UI", 8.25F, FontStyle.Regular)
                    };
                    cmb.Items.AddRange(new object[] {
                        "0 - Ninguno",
                        "1 - Todo (All)",
                        "2 - Comunicaciones (Comms)",
                        "3 - Hilos (Threading)",
                        "4 - IMU / Giroscopio",
                        "5 - Vibración (Rumble)",
                        "6 - Sacudida (Shake)"
                    });
                    int debugVal = 0;
                    int.TryParse(value, out debugVal);
                    if (debugVal >= 0 && debugVal < cmb.Items.Count) cmb.SelectedIndex = debugVal;
                    else cmb.SelectedIndex = 0;

                    cmb.SelectedIndexChanged += cbBox_Changed;
                    childControl = cmb;
                } else {
                    var tb = new TextBox {
                        Text = value,
                        BackColor = Color.FromArgb(30, 31, 35),
                        ForeColor = Color.White,
                        BorderStyle = BorderStyle.FixedSingle,
                        Dock = DockStyle.Fill,
                        Margin = new Padding(3, 4, 16, 4),
                        Font = new Font("Segoe UI", 8.5F, FontStyle.Regular)
                    };
                    tb.TextChanged += cbBox_Changed;
                    childControl = tb;
                }

                settingsTable.Controls.Add(childControl, 1, i);
            }

            settingsTable.ResumeLayout();
        }

        private void InitializeMappingView() {
            var mappingDefs = new (string label, string key)[] {
                ("Capturar Pantalla", "capture"),
                ("Botón Home (Guía)", "home"),
                ("SL (Joy-Con Izq)", "sl_l"),
                ("SR (Joy-Con Izq)", "sr_l"),
                ("SL (Joy-Con Der)", "sl_r"),
                ("SR (Joy-Con Der)", "sr_r"),
                ("Sacudir (Shake)", "shake"),
                ("Centrar Ratón", "reset_mouse"),
                ("Activar Giroscopio", "active_gyro")
            };

            ContextMenuStrip menu_joy_buttons = new ContextMenuStrip();
            var noneItem = new ToolStripMenuItem("Ninguno (Desactivar)");
            noneItem.Tag = 0;
            menu_joy_buttons.Items.Add(noneItem);
            menu_joy_buttons.Items.Add(new ToolStripSeparator());

            foreach (int i in Enum.GetValues(typeof(Joycon.Button))) {
                var temp = new ToolStripMenuItem(Enum.GetName(typeof(Joycon.Button), i));
                temp.Tag = i;
                menu_joy_buttons.Items.Add(temp);
            }

            menu_joy_buttons.ItemClicked += (sender, e) => {
                if (menu_joy_buttons.Tag is SplitButton caller && e.ClickedItem != null) {
                    if (e.ClickedItem.Tag is int tagVal) {
                        Config.SetValue((string)caller.Tag, tagVal == 0 ? "0" : ("joy_" + tagVal));
                        Config.Save();
                        UpdateMapButtonLabel(caller);
                    }
                }
            };

            mapTable.SuspendLayout();
            mapTable.Controls.Clear();
            mapTable.RowStyles.Clear();
            mapTable.RowCount = mappingDefs.Length;

            for (int i = 0; i < mappingDefs.Length; i++) {
                var def = mappingDefs[i];
                mapTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));

                var lbl = new Label {
                    Text = def.label,
                    ForeColor = Color.FromArgb(220, 225, 235),
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft,
                    AutoEllipsis = true,
                    Font = new Font("Segoe UI", 8.5F, FontStyle.Regular)
                };

                var btn = new SplitButton {
                    Name = "btn_map_" + def.key,
                    Tag = def.key,
                    Dock = DockStyle.Fill,
                    Height = 28,
                    BackColor = Color.FromArgb(42, 45, 54),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Menu = menu_joy_buttons,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Font = new Font("Segoe UI", 8.5F, FontStyle.Regular)
                };
                btn.FlatAppearance.BorderColor = Color.FromArgb(65, 70, 82);

                btnTip.SetToolTip(btn, "Clic izquierdo: presiona una tecla o clic para capturar.\r\nClic central: restablecer por defecto.\r\nFlecha derecha: desplegar lista de botones del mando.");

                btn.MouseDown += (s, e) => {
                    var b = s as SplitButton;
                    if (b == null) return;
                    if (e.Button == MouseButtons.Left) {
                        ActiveMapButton = b;
                        b.Text = "Presiona tecla/clic...";
                    } else if (e.Button == MouseButtons.Middle) {
                        Config.SetValue((string)b.Tag, Config.GetDefaultValue((string)b.Tag));
                        Config.Save();
                        UpdateMapButtonLabel(b);
                    }
                };

                UpdateMapButtonLabel(btn);

                mapTable.Controls.Add(lbl, 0, i);
                mapTable.Controls.Add(btn, 1, i);
            }

            mapTable.ResumeLayout();
        }

        private void menuItemJoinSplit_Click(object sender, EventArgs e) {
            if (contextMenuSlot.SourceControl is Button slotBtn) {
                conBtnClick(slotBtn, EventArgs.Empty);
            }
        }

        private void menuItemVibrate_Click(object sender, EventArgs e) {
            if (contextMenuSlot.SourceControl is Button slotBtn) {
                int idx = con.IndexOf(slotBtn);
                if (idx >= 0 && idx < loc.Count) {
                    locBtnClickAsync(loc[idx], EventArgs.Empty);
                }
            }
        }

        private void HideToTray() {
            this.WindowState = FormWindowState.Minimized;
            notifyIcon.Visible = true;
            notifyIcon.BalloonTipText = "¡Haz doble clic para abrir PAD+!";
            notifyIcon.ShowBalloonTip(0);
            this.ShowInTaskbar = false;
            this.Hide();
        }

        private void ShowFromTray() {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.ShowInTaskbar = true;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            try {
                string icoPath = Path.Combine(AppContext.BaseDirectory, "Icons", "padplus_icon.ico");
                if (File.Exists(icoPath)) {
                    using var fs = new FileStream(icoPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    this.Icon = new Icon(fs);
                }
            } catch { }
            notifyIcon.Visible = false;
        }

        private void MainForm_Resize(object sender, EventArgs e) {
            if (this.WindowState == FormWindowState.Minimized) {
                HideToTray();
                return;
            }

            int leftEdge = Math.Max(conCntrls.Right, console.Right) + 16;
            if (!rightPanel.Visible && this.ClientSize.Width >= (leftEdge + 400)) {
                rightPanel.Visible = true;
                foldLbl.Text = "⚙ Ocultar <";
            }

            UpdateLayoutSizes();
        }

        private void UpdateLayoutSizes() {
            if (this.WindowState == FormWindowState.Minimized) return;
            int leftEdge = Math.Max(conCntrls.Right, console.Right) + 16;
            if (rightPanel.Visible) {
                int rightWidth = Math.Max(380, this.ClientSize.Width - leftEdge - 12);
                rightPanel.SetBounds(leftEdge, 10, rightWidth, this.ClientSize.Height - 20);

                if (panelTabBar != null && btnTabSettings != null && btnTabMapping != null && btnTab3rdParty != null) {
                    int thirdTab = (panelTabBar.Width - 18) / 3;
                    if (thirdTab > 60) {
                        btnTabSettings.Width = thirdTab;
                        btnTabMapping.Left = btnTabSettings.Right + 4;
                        btnTabMapping.Width = thirdTab;
                        btnTab3rdParty.Left = btnTabMapping.Right + 4;
                        btnTab3rdParty.Width = thirdTab;
                    }
                }
            }
        }

        public void ShowRightPanelTab(int tabIndex) {
            int leftEdge = Math.Max(conCntrls.Right, console.Right) + 16;
            int targetWidth = leftEdge + 480 + 12;

            if (!rightPanel.Visible) {
                rightPanel.Visible = true;
                foldLbl.Text = "⚙ Ocultar <";
                if (this.WindowState != FormWindowState.Maximized) {
                    this.MinimumSize = new Size(targetWidth, 480);
                    if (this.ClientSize.Width < targetWidth) {
                        this.ClientSize = new Size(targetWidth, Math.Max(this.ClientSize.Height, 520));
                    }
                }
            } else {
                if (this.WindowState != FormWindowState.Maximized && this.MinimumSize.Width < targetWidth) {
                    this.MinimumSize = new Size(targetWidth, 480);
                }
            }

            panelSettingsView.Visible = (tabIndex == 0);
            panelMappingView.Visible = (tabIndex == 1);
            panel3rdPartyView.Visible = (tabIndex == 2);

            Color activeTabBg = Color.FromArgb(40, 120, 220);
            Color inactiveTabBg = Color.FromArgb(38, 42, 50);

            btnTabSettings.BackColor = (tabIndex == 0) ? activeTabBg : inactiveTabBg;
            btnTabMapping.BackColor = (tabIndex == 1) ? activeTabBg : inactiveTabBg;
            btnTab3rdParty.BackColor = (tabIndex == 2) ? activeTabBg : inactiveTabBg;

            if (tabIndex == 2) {
                Refresh3rdPartyAllControllers();
            }

            UpdateLayoutSizes();
        }

        private void btnTabSettings_Click(object sender, EventArgs e) {
            ShowRightPanelTab(0);
        }

        private void btnTabMapping_Click(object sender, EventArgs e) {
            ShowRightPanelTab(1);
        }

        private void btnTab3rdParty_Click(object sender, EventArgs e) {
            ShowRightPanelTab(2);
        }

        private void notifyIcon_MouseDoubleClick(object sender, MouseEventArgs e) {
            ShowFromTray();
        }

        private void MainForm_Load(object sender, EventArgs e) {
            Config.Init(caliData);

            BluetoothAutoPairService.OnLog += (msg) => AppendTextBox(msg + "\r\n");

            Program.Start();

            chkMapSwapAB.Checked = Config.Value("SwapAB").ToLower() == "true";
            chkMapSwapXY.Checked = Config.Value("SwapXY").ToLower() == "true";

            passiveScanBox.Checked = Config.IntValue("ProgressiveScan") == 1;
            startInTrayBox.Checked = Config.IntValue("StartInTray") == 1;

            if (Program.emClient != null) {
                lblStatusViGEm.Text = "● ViGEm: Activo";
                lblStatusViGEm.ForeColor = Color.FromArgb(70, 210, 100);
            } else {
                lblStatusViGEm.Text = "● ViGEm: No instalado";
                lblStatusViGEm.ForeColor = Color.FromArgb(240, 80, 80);
            }

            if (HidHideService.IsInstalled) {
                lblStatusHidHide.Text = "● HidHide: Activo";
                lblStatusHidHide.ForeColor = Color.FromArgb(70, 210, 100);
            } else {
                lblStatusHidHide.Text = "● HidHide: N/A";
                lblStatusHidHide.ForeColor = Color.FromArgb(140, 145, 155);
            }

            lblStatusBT.Text = "● BT: Listo";
            lblStatusBT.ForeColor = Color.FromArgb(70, 160, 240);

            AppendTextBox("=== PAD+ v1.0 — Switch Controller Bridge (@Skasorpg) ===\r\n");
            AppendTextBox($"ViGEmBus: {(Program.emClient != null ? "Activo (Emulación Xbox 360 OK)" : "No detectado (Instalar ViGEmBus)")}\r\n");
            AppendTextBox($"HidHide: {(HidHideService.IsInstalled ? "Instalado (Anti-Doble Input OK)" : "No instalado (Opcional)")}\r\n");
            AppendTextBox("Listo para conectar mandos Switch (USB / Bluetooth).\r\n");

            if (Config.IntValue("StartInTray") == 1) {
                HideToTray();
            } else {
                ShowFromTray();
            }

            Init3rdPartyTab();
            UpdateLayoutSizes();
        }

        private static readonly string thirdPartyConfigPath = Path.Combine(AppContext.BaseDirectory, "3rdPartyControllers");

        private void Init3rdPartyTab() {
            cbo3rdType.Items.Clear();
            cbo3rdType.Items.AddRange(new string[] { "Pro Controller (Switch)", "Joy-Con Izquierdo (Switch)", "Joy-Con Derecho (Switch)", "Mando DirectInput Genérico (PC -> XInput)" });
            cbo3rdType.SelectedIndex = 0;

            Load3rdPartyCustomControllers();
            Refresh3rdPartyAllControllers();
        }

        private void Load3rdPartyCustomControllers() {
            list3rdCustom.Items.Clear();
            if (File.Exists(thirdPartyConfigPath)) {
                using (StreamReader file = new StreamReader(thirdPartyConfigPath)) {
                    string line = string.Empty;
                    while ((line = file.ReadLine()) != null && (line != string.Empty)) {
                        string[] split = line.Split('|');
                        string serial_number = split.Length > 4 ? split[4] : "";
                        list3rdCustom.Items.Add(new _3rdPartyControllers.SController(split[0], ushort.Parse(split[1]), ushort.Parse(split[2]), byte.Parse(split[3]), serial_number));
                    }
                }
            }
            Copy3rdPartyControllersToProgram();
        }

        private void Copy3rdPartyControllersToProgram() {
            Program.thirdPartyCons.Clear();
            foreach (_3rdPartyControllers.SController v in list3rdCustom.Items) {
                Program.thirdPartyCons.Add(v);
            }
        }

        private void Refresh3rdPartyAllControllers() {
            list3rdAll.Items.Clear();
            IntPtr ptr = HIDapi.hid_enumerate(0x0, 0x0);
            IntPtr top_ptr = ptr;

            while (ptr != IntPtr.Zero) {
                hid_device_info enumerate = (hid_device_info)Marshal.PtrToStructure(ptr, typeof(hid_device_info));

                // Filter out non-gamepad devices (webcams, capture cards, audio)
                if ((enumerate.vendor_id == 0x046D && enumerate.product_id == 0x0944) ||
                    (enumerate.vendor_id == 0x534D && enumerate.product_id == 0x2109)) {
                    ptr = enumerate.next;
                    continue;
                }

                string serial = enumerate.serial_number ?? "000000000001";
                string prodName = enumerate.product_string;

                if (enumerate.vendor_id == 0x1532) {
                    prodName = "Razer Synapse Device";
                } else if (!string.IsNullOrEmpty(prodName)) {
                    bool hasMojibake = false;
                    foreach (char c in prodName) {
                        if (c >= 0x4E00 && c <= 0x9FFF) { hasMojibake = true; break; }
                    }
                    if (hasMojibake) {
                        prodName = $"Generic Device ({enumerate.vendor_id:X4}:{enumerate.product_id:X4})";
                    }
                } else {
                    prodName = $"Device ({enumerate.vendor_id:X4}:{enumerate.product_id:X4})";
                }

                string name = prodName + " (" + enumerate.vendor_id + "-" + enumerate.product_id + "-" + serial + ")";
                if (!ContainsController(list3rdCustom, name) && !ContainsController(list3rdAll, name)) {
                    list3rdAll.Items.Add(new _3rdPartyControllers.SController(name, enumerate.vendor_id, enumerate.product_id, 1, serial));
                }

                ptr = enumerate.next;
            }
            HIDapi.hid_free_enumeration(top_ptr);
        }

        private bool ContainsController(ListBox box, string name) {
            foreach (_3rdPartyControllers.SController v in box.Items) {
                if (v != null && v.name == name) return true;
            }
            return false;
        }

        private void btn3rdAdd_Click(object sender, EventArgs e) {
            if (list3rdAll.SelectedItem != null) {
                _3rdPartyControllers.SController item = (_3rdPartyControllers.SController)list3rdAll.SelectedItem;
                if (cbo3rdType.SelectedIndex >= 0) {
                    item.type = (byte)(cbo3rdType.SelectedIndex + 1);
                } else {
                    bool isDInput = item.vendor_id != 0x057e || (item.name != null && (item.name.IndexOf("fight", StringComparison.OrdinalIgnoreCase) >= 0 || item.name.IndexOf("directinput", StringComparison.OrdinalIgnoreCase) >= 0));
                    item.type = (byte)(isDInput ? 4 : 1);
                }
                list3rdCustom.Items.Add(item);
                list3rdAll.Items.Remove(item);
                list3rdAll.ClearSelected();
            }
        }

        private void btn3rdRemove_Click(object sender, EventArgs e) {
            if (list3rdCustom.SelectedItem != null) {
                _3rdPartyControllers.SController item = (_3rdPartyControllers.SController)list3rdCustom.SelectedItem;
                list3rdAll.Items.Add(item);
                list3rdCustom.Items.Remove(item);
                list3rdCustom.ClearSelected();
            }
        }

        private void btn3rdRefresh_Click(object sender, EventArgs e) {
            Refresh3rdPartyAllControllers();
        }

        private void list3rdCustom_SelectedIndexChanged(object sender, EventArgs e) {
            if (list3rdCustom.SelectedItem != null) {
                _3rdPartyControllers.SController v = (_3rdPartyControllers.SController)list3rdCustom.SelectedItem;
                int idx = v.type - 1;
                cbo3rdType.SelectedIndex = (idx >= 0 && idx < cbo3rdType.Items.Count) ? idx : 0;
                grp3rdConfig.Enabled = true;
            } else {
                grp3rdConfig.Enabled = false;
            }
        }

        private void cbo3rdType_SelectedIndexChanged(object sender, EventArgs e) {
            if (list3rdCustom.SelectedItem != null && cbo3rdType.SelectedIndex >= 0) {
                _3rdPartyControllers.SController v = (_3rdPartyControllers.SController)list3rdCustom.SelectedItem;
                v.type = (byte)(cbo3rdType.SelectedIndex + 1);
                int idx = list3rdCustom.SelectedIndex;
                list3rdCustom.Items[idx] = v;
            }
        }

        private void btn3rdApply_Click(object sender, EventArgs e) {
            string sc = "";
            foreach (_3rdPartyControllers.SController v in list3rdCustom.Items) {
                sc += v.Serialise() + "\r\n";
            }
            File.WriteAllText(thirdPartyConfigPath, sc);
            Copy3rdPartyControllersToProgram();
            Program.mgr?.CheckForNewControllers();
            AppendTextBox("Mandos de terceros actualizados. Escaneando mandos...\r\n");
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e) {
            try {
                Program.Stop();
                Environment.Exit(0);
            } catch { }
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e) { // this does not work, for some reason. Fix before release
            try {
                Program.Stop();
                Close();
                Environment.Exit(0);
            } catch { }
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
            donationLink.LinkVisited = true;
            try {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://www.youtube.com/@Skasorpg") { UseShellExecute = true });
            } catch { }
        }

        private void btnYoutube_Click(object sender, EventArgs e) {
            try {
                Process.Start(new ProcessStartInfo("https://www.youtube.com/@Skasorpg") { UseShellExecute = true });
            } catch { }
        }

        private async void btnAutoPair_Click(object sender, EventArgs e) {
            btnAutoPair.Enabled = false;
            btnAutoPair.Text = "⏳ Buscando...";
            AppendTextBox("[BT Auto-Pair] Buscando Joy-Cons / Pro Controllers en modo sincronización...\r\n");
            int paired = await Task.Run(() => BluetoothAutoPairService.ScanAndPairOnce());
            if (paired > 0) {
                AppendTextBox($"[BT Auto-Pair] ¡Se detectó y habilitó {paired} mando(s) Switch!\r\n");
            } else {
                AppendTextBox("[BT Auto-Pair] No se encontraron mandos nuevos en modo Sync (mantén presionado el botón redondo de sync del Joy-Con hasta que las luces corran).\r\n");
            }
            btnAutoPair.Text = "🔄 Sync BT";
            btnAutoPair.Enabled = true;
        }

        private void passiveScanBox_CheckedChanged(object sender, EventArgs e) {
            Config.SetValue("ProgressiveScan", passiveScanBox.Checked ? "1" : "0");
            Config.Save();
        }

        public void AppendTextBox(string value) { // https://stackoverflow.com/questions/519233/writing-to-a-textbox-from-another-thread
            if (InvokeRequired) {
                this.Invoke(new Action<string>(AppendTextBox), new object[] { value });
                return;
            }
            console.AppendText(value);
        }

        bool toRumble = Boolean.Parse(ConfigurationManager.AppSettings["EnableRumble"]);
        bool showAsXInput = Boolean.Parse(ConfigurationManager.AppSettings["ShowAsXInput"]);
        bool showAsDS4 = Boolean.Parse(ConfigurationManager.AppSettings["ShowAsDS4"]);

        public async void locBtnClickAsync(object sender, EventArgs e) {
            Button bb = sender as Button;
            if (bb?.Tag is Button button && button.Tag is Joycon v) {
                v.SetRumble(160.0f, 320.0f, 1.0f);
                await Task.Delay(300);
                v.SetRumble(160.0f, 320.0f, 0);
            }
        }

        bool doNotRejoin = Boolean.Parse(ConfigurationManager.AppSettings["DoNotRejoinJoycons"]);

        public void conBtnClick(object sender, EventArgs e) {
            Button button = sender as Button;
            if (button?.Tag is Joycon v) {

                if (v.other == null && !v.isPro) { // needs connecting to other joycon (so messy omg)
                    bool succ = false;

                    if (Program.mgr.j.Count == 1 || doNotRejoin) { // when want to have a single joycon in vertical mode
                        v.other = v; // hacky; implement check in Joycon.cs to account for this
                        succ = true;
                    } else {
                        foreach (Joycon jc in Program.mgr.j) {
                            if (!jc.isPro && jc.isLeft != v.isLeft && jc != v && jc.other == null) {
                                v.other = jc;
                                jc.other = v;

                                if (v.out_xbox != null) {
                                    v.out_xbox.Disconnect();
                                    v.out_xbox = null;
                                }

                                if (v.out_ds4 != null) {
                                    v.out_ds4.Disconnect();
                                    v.out_ds4 = null;
                                }

                                // setting the other joycon's button image
                                foreach (Button b in con)
                                    if (b.Tag == jc)
                                        b.BackgroundImage = ControllerIconHelper.GetIcon(jc.isLeft ? "jc_left" : "jc_right");

                                succ = true;
                                break;
                            }
                        }
                    }

                    if (succ)
                        foreach (Button b in con)
                            if (b.Tag == v)
                                b.BackgroundImage = ControllerIconHelper.GetIcon(v.isLeft ? "jc_left" : "jc_right");
                } else if (v.other != null && !v.isPro) { // needs disconnecting from other joycon
                    ReenableViGEm(v);
                    ReenableViGEm(v.other);

                    button.BackgroundImage = ControllerIconHelper.GetIcon(v.isLeft ? "jc_left_s" : "jc_right_s");

                    foreach (Button b in con)
                        if (b.Tag == v.other)
                            b.BackgroundImage = ControllerIconHelper.GetIcon(v.other.isLeft ? "jc_left_s" : "jc_right_s");

                    v.other.other = null;
                    v.other = null;
                }
            }
        }

        private void startInTrayBox_CheckedChanged(object sender, EventArgs e) {
            Config.SetValue("StartInTray", startInTrayBox.Checked ? "1" : "0");
            Config.Save();
        }

        private void btn_open3rdP_Click(object sender, EventArgs e) {
            if (rightPanel.Visible && panel3rdPartyView.Visible) {
                foldLbl_Click(sender, e);
            } else {
                ShowRightPanelTab(2);
            }
        }

        private void ReloadAllControllers() {
            if (Program.mgr?.j != null) {
                foreach (Joycon jc in Program.mgr.j) {
                    jc.ReloadSettings();
                }
            }
        }

        private void settingsApply_Click(object sender, EventArgs e) {
            var configFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            var settings = configFile.AppSettings.Settings;

            for (int row = 0; row < settingsTable.RowCount; row++) {
                var pnlKey = settingsTable.GetControlFromPosition(0, row);
                string keyCtl = (pnlKey?.Tag as string) ?? pnlKey?.Text;
                var valCtl = settingsTable.GetControlFromPosition(1, row);
                if (string.IsNullOrEmpty(keyCtl) || valCtl == null || settings[keyCtl] == null) continue;

                if (valCtl is CheckBox cb) {
                    settings[keyCtl].Value = cb.Checked.ToString().ToLower();
                } else if (valCtl is ComboBox cmb) {
                    if (keyCtl.Equals("GyroToJoyOrMouse", StringComparison.OrdinalIgnoreCase)) {
                        switch (cmb.SelectedIndex) {
                            case 1: settings[keyCtl].Value = "mouse"; break;
                            case 2: settings[keyCtl].Value = "joy_left"; break;
                            case 3: settings[keyCtl].Value = "joy_right"; break;
                            default: settings[keyCtl].Value = "false"; break;
                        }
                    } else if (keyCtl.Equals("DebugType", StringComparison.OrdinalIgnoreCase)) {
                        settings[keyCtl].Value = cmb.SelectedIndex.ToString();
                    } else {
                        settings[keyCtl].Value = (cmb.SelectedItem?.ToString() ?? cmb.Text).ToLower();
                    }
                } else if (valCtl is TextBox tb) {
                    settings[keyCtl].Value = tb.Text.Trim();
                }
            }

            try {
                configFile.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");
                ReloadAllControllers();
                AppendTextBox("Ajustes guardados y aplicados correctamente (Hot-Reload OK).\r\n");
            } catch (Exception ex) {
                AppendTextBox("Error al guardar ajustes: " + ex.Message + "\r\n");
            }
        }

        void ReenableViGEm(Joycon v) {
            if (showAsXInput && v.out_xbox == null) {
                v.out_xbox = new Controller.OutputControllerXbox360();

                if (toRumble)
                    v.out_xbox.FeedbackReceived += v.ReceiveRumble;
                v.out_xbox.Connect();
            }

            if (showAsDS4 && v.out_ds4 == null) {
                v.out_ds4 = new Controller.OutputControllerDualShock4();

                if (toRumble)
                    v.out_ds4.FeedbackReceived += v.Ds4_FeedbackReceived;
                v.out_ds4.Connect();
            }
        }

        private void foldLbl_Click(object sender, EventArgs e) {
            if (rightPanel.Visible) {
                rightPanel.Visible = false;
                foldLbl.Text = "⚙ Ajustes >";
                if (this.WindowState != FormWindowState.Maximized) {
                    int leftEdge = Math.Max(conCntrls.Right, console.Right) + 16;
                    this.MinimumSize = new Size(conCntrls.Width + 40, 480);
                    this.ClientSize = new Size(leftEdge, Math.Max(this.ClientSize.Height, 520));
                }
            } else {
                ShowRightPanelTab(0);
            }
        }

        private void chkMapSwapAB_CheckedChanged(object sender, EventArgs e) {
            Config.SetValue("SwapAB", chkMapSwapAB.Checked.ToString().ToLower());
            Config.Save();
            ReloadAllControllers();
            SyncSettingControl("SwapAB", chkMapSwapAB.Checked);
        }

        private void chkMapSwapXY_CheckedChanged(object sender, EventArgs e) {
            Config.SetValue("SwapXY", chkMapSwapXY.Checked.ToString().ToLower());
            Config.Save();
            ReloadAllControllers();
            SyncSettingControl("SwapXY", chkMapSwapXY.Checked);
        }

        private void SyncSettingControl(string key, bool isChecked) {
            for (int r = 0; r < settingsTable.RowCount; r++) {
                var pnlKey = settingsTable.GetControlFromPosition(0, r);
                string k = (pnlKey?.Tag as string) ?? pnlKey?.Text;
                if (k == key) {
                    if (settingsTable.GetControlFromPosition(1, r) is CheckBox cb) {
                        cb.CheckedChanged -= cbBox_Changed;
                        cb.Checked = isChecked;
                        cb.Text = isChecked ? "Activado" : "Desactivado";
                        cb.ForeColor = isChecked ? Color.FromArgb(0, 230, 118) : Color.FromArgb(176, 190, 197);
                        cb.Font = new Font("Segoe UI", 8.5F, isChecked ? FontStyle.Bold : FontStyle.Regular);
                        cb.CheckedChanged += cbBox_Changed;
                    }
                    break;
                }
            }
        }

        private void cbBox_Changed(object sender, EventArgs e) {
            Control ctl = sender as Control;
            if (ctl == null) return;
            var coord = settingsTable.GetPositionFromControl(ctl);
            var valCtl = settingsTable.GetControlFromPosition(coord.Column, coord.Row);
            var pnlKey = settingsTable.GetControlFromPosition(0, coord.Row);
            string keyCtl = (pnlKey?.Tag as string) ?? pnlKey?.Text;
            if (string.IsNullOrEmpty(keyCtl) || valCtl == null) return;

            try {
                var configFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                var settings = configFile.AppSettings.Settings;
                if (settings[keyCtl] == null) return;

                if (valCtl is CheckBox cb) {
                    settings[keyCtl].Value = cb.Checked.ToString().ToLower();
                    cb.Text = cb.Checked ? "Activado" : "Desactivado";
                    cb.ForeColor = cb.Checked ? Color.FromArgb(0, 230, 118) : Color.FromArgb(176, 190, 197);
                    cb.Font = new Font("Segoe UI", 8.5F, cb.Checked ? FontStyle.Bold : FontStyle.Regular);
                    if (keyCtl == "SwapAB" && chkMapSwapAB != null) {
                        chkMapSwapAB.CheckedChanged -= chkMapSwapAB_CheckedChanged;
                        chkMapSwapAB.Checked = cb.Checked;
                        chkMapSwapAB.CheckedChanged += chkMapSwapAB_CheckedChanged;
                    }
                    if (keyCtl == "SwapXY" && chkMapSwapXY != null) {
                        chkMapSwapXY.CheckedChanged -= chkMapSwapXY_CheckedChanged;
                        chkMapSwapXY.Checked = cb.Checked;
                        chkMapSwapXY.CheckedChanged += chkMapSwapXY_CheckedChanged;
                    }
                } else if (valCtl is ComboBox cmb) {
                    if (keyCtl.Equals("GyroToJoyOrMouse", StringComparison.OrdinalIgnoreCase)) {
                        switch (cmb.SelectedIndex) {
                            case 1: settings[keyCtl].Value = "mouse"; break;
                            case 2: settings[keyCtl].Value = "joy_left"; break;
                            case 3: settings[keyCtl].Value = "joy_right"; break;
                            default: settings[keyCtl].Value = "false"; break;
                        }
                    } else if (keyCtl.Equals("DebugType", StringComparison.OrdinalIgnoreCase)) {
                        settings[keyCtl].Value = cmb.SelectedIndex.ToString();
                    } else {
                        settings[keyCtl].Value = (cmb.SelectedItem?.ToString() ?? cmb.Text).ToLower();
                    }
                } else if (valCtl is TextBox tb) {
                    settings[keyCtl].Value = tb.Text.Trim();
                }

                if (keyCtl == "HomeLEDOn") {
                    bool on = settings[keyCtl].Value.ToLower() == "true";
                    foreach (Joycon j in Program.mgr.j) {
                        j.SetHomeLight(on);
                    }
                }

                configFile.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");
                ReloadAllControllers();
            } catch (Exception ex) {
                AppendTextBox("Error al actualizar configuración: " + ex.Message + "\r\n");
            }
        }

        private void StartCalibrate(object sender, EventArgs e) {
            if (Program.mgr.j.Count == 0) {
                this.console.Text = "Por favor, conecta un solo Switch Pro Controller para calibrar.\r\n";
                return;
            }
            if (Program.mgr.j.Count > 1) {
                this.console.Text = "Por favor, calibra un solo mando a la vez (desconecta los demás).\r\n";
                return;
            }
            this.AutoCalibrate.Enabled = false;
            countDown = new Timer();
            this.count = 4;
            this.CountDown(null, null);
            countDown.Tick += new EventHandler(CountDown);
            countDown.Interval = 1000;
            countDown.Enabled = true;
        }

        private void StartGetData() {
            this.xG.Clear(); this.yG.Clear(); this.zG.Clear();
            this.xA.Clear(); this.yA.Clear(); this.zA.Clear();
            countDown = new Timer();
            this.count = 3;
            this.calibrate = true;
            countDown.Tick += new EventHandler(CalcData);
            countDown.Interval = 1000;
            countDown.Enabled = true;
        }

        private void btn_reassign_open_Click(object sender, EventArgs e) {
            ShowRightPanelTab(1);
        }

        private void CountDown(object sender, EventArgs e) {
            if (this.count == 0) {
                this.console.Text = "Calibrando sensores...\r\n";
                countDown.Stop();
                this.StartGetData();
            } else {
                this.console.Text = "Por favor, mantén el mando completamente plano y quieto sobre la mesa.\r\n";
                this.console.Text += "La calibración iniciará en " + this.count + " segundo(s)...";
                this.count--;
            }
        }
        private void CalcData(object sender, EventArgs e) {
            if (this.count == 0) {
                countDown.Stop();
                this.calibrate = false;
                string serNum = Program.mgr.j.First().serial_number;
                int serIndex = this.findSer(serNum);
                float[] Arr = new float[6] { 0, 0, 0, 0, 0, 0 };
                if (serIndex == -1) {
                    this.caliData.Add(new KeyValuePair<string, float[]>(
                         serNum,
                         Arr
                    ));
                } else {
                    Arr = this.caliData[serIndex].Value;
                }
                Random rnd = new Random();
                Arr[0] = (float)quickselect_median(this.xG, rnd.Next);
                Arr[1] = (float)quickselect_median(this.yG, rnd.Next);
                Arr[2] = (float)quickselect_median(this.zG, rnd.Next);
                Arr[3] = (float)quickselect_median(this.xA, rnd.Next);
                Arr[4] = (float)quickselect_median(this.yA, rnd.Next);
                Arr[5] = (float)quickselect_median(this.zA, rnd.Next) - 4010; //Joycon.cs acc_sen 16384
                this.console.Text += "¡Calibración completada con éxito!\r\n";
                Config.SaveCaliData(this.caliData);
                Program.mgr.j.First().getActiveData();
                this.AutoCalibrate.Enabled = true;
            } else {
                this.count--;
            }
        }
        private double quickselect_median(List<int> l, Func<int, int> pivot_fn) {
            if (l == null || l.Count == 0)
                return 0;
            List<int> sorted = l.OrderBy(x => x).ToList();
            int ll = sorted.Count;
            if (ll % 2 == 1) {
                return sorted[ll / 2];
            } else {
                return 0.5 * (sorted[ll / 2 - 1] + sorted[ll / 2]);
            }
        }

        private void ApplyModernTheme() {
            Color darkBg = Color.FromArgb(26, 27, 32);
            Color cardBg = Color.FromArgb(36, 38, 45);
            Color textLight = Color.FromArgb(235, 235, 240);
            Color textDim = Color.FromArgb(170, 175, 185);
            Color accentBtn = Color.FromArgb(40, 120, 220);

            this.BackColor = darkBg;
            this.ForeColor = textLight;

            conCntrls.BackColor = cardBg;
            conCntrls.ForeColor = textLight;

            groupBox1.BackColor = cardBg;
            groupBox1.ForeColor = textLight;
            rightPanel.BackColor = darkBg;

            panelTabBar.BackColor = Color.FromArgb(20, 21, 25);
            panelSettingsView.BackColor = darkBg;
            panelMappingView.BackColor = darkBg;
            grpXboxLayout.BackColor = cardBg;
            grpXboxLayout.ForeColor = textLight;
            grpMapActions.BackColor = cardBg;
            grpMapActions.ForeColor = textLight;

            console.BackColor = Color.FromArgb(18, 19, 22);
            console.ForeColor = Color.FromArgb(140, 230, 160); // Soft green console text
            console.BorderStyle = BorderStyle.FixedSingle;

            settingsTable.BackColor = cardBg;

            Button[] buttons = new Button[] { btn_reassign_open, btn_open3rdP, AutoCalibrate, btnAutoPair, settingsApply };
            foreach (var btn in buttons) {
                if (btn != null) {
                    btn.BackColor = Color.FromArgb(50, 54, 64);
                    btn.ForeColor = Color.White;
                    btn.FlatStyle = FlatStyle.Flat;
                    btn.FlatAppearance.BorderColor = Color.FromArgb(70, 75, 88);
                }
            }

            if (settingsApply != null) {
                settingsApply.BackColor = accentBtn;
                settingsApply.FlatAppearance.BorderColor = accentBtn;
            }

            passiveScanBox.ForeColor = textLight;
            startInTrayBox.ForeColor = textLight;
            version_lbl.ForeColor = textDim;
            foldLbl.ForeColor = Color.FromArgb(64, 196, 255);
        }

        public float[] activeCaliData(string serNum) {
            for (int i = 0; i < this.caliData.Count; i++) {
                if (this.caliData[i].Key == serNum) {
                    return this.caliData[i].Value;
                }
            }
            return this.caliData[0].Value;
        }

        private int findSer(string serNum) {
            for (int i = 0; i < this.caliData.Count; i++) {
                if (this.caliData[i].Key == serNum) {
                    return i;
                }
            }
            return -1;
        }

        protected override void WndProc(ref Message m) {
            const int WM_DEVICECHANGE = 0x0219;
            const int DBT_DEVNODES_CHANGED = 0x0007;
            const int DBT_DEVICEARRIVAL = 0x8000;
            const int DBT_DEVICEREMOVECOMPLETE = 0x8004;

            if (m.Msg == WM_DEVICECHANGE) {
                if (Program.mgr != null && Environment.TickCount64 - Program.mgr.LastConnectTick < 2000) {
                    base.WndProc(ref m);
                    return;
                }

                int wParam = m.WParam.ToInt32();
                if (wParam == DBT_DEVNODES_CHANGED || wParam == DBT_DEVICEARRIVAL || wParam == DBT_DEVICEREMOVECOMPLETE) {
                    Task.Run(async () => {
                        await Task.Delay(200);
                        Program.mgr?.CheckForNewControllers();
                    });
                }
            }

            base.WndProc(ref m);
        }
    }
}

