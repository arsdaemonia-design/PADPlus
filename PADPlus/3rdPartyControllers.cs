using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using static PADPlus.HIDapi;

namespace PADPlus {
    public partial class _3rdPartyControllers : Form {
        public class SController {
            public String name;
            public ushort product_id;
            public ushort vendor_id;
            public string serial_number;
            public byte type; // 1 is pro, 2 is left joy, 3 is right joy

            public SController(String name, ushort vendor_id, ushort product_id, byte type, string serial_number) {
                this.product_id = product_id; this.vendor_id = vendor_id; this.type = type;
                this.serial_number = serial_number;
                this.name = name;
            }

            public override bool Equals(object obj) {
                //Check for null and compare run-time types.
                if ((obj == null) || !this.GetType().Equals(obj.GetType())) {
                    return false;
                } else {
                    SController s = (SController)obj;
                    return (s.product_id == product_id) && (s.vendor_id == vendor_id) && (s.serial_number == serial_number);
                }
            }

            public override int GetHashCode() {
                return Tuple.Create(product_id, vendor_id, serial_number).GetHashCode();
            }

            public override string ToString() {
                if (type == 0) return name ?? $"Dispositivo ({this.product_id:X4})";
                string typeStr = type switch {
                    1 => "[Switch Pro]",
                    2 => "[Joy-Con L]",
                    3 => "[Joy-Con R]",
                    4 => "[DirectInput ➔ Xbox]",
                    _ => "[Switch Pro]"
                };
                return $"{typeStr} {name ?? $"Dispositivo ({this.product_id:X4})"}";
            }

            public string Serialise() {
                return String.Format("{0}|{1}|{2}|{3}|{4}", name, vendor_id, product_id, type, serial_number);
            }
        }

        static readonly string path;

        static _3rdPartyControllers() {
            path = Path.Combine(AppContext.BaseDirectory, "3rdPartyControllers");
        }

        public _3rdPartyControllers() {
            InitializeComponent();
            string icoPath = Path.Combine(AppContext.BaseDirectory, "Icons", "padplus_icon.ico");
            if (File.Exists(icoPath)) {
                this.Icon = new Icon(icoPath);
            }
            this.BackColor = Color.FromArgb(22, 24, 29);
            this.ForeColor = Color.White;
            list_allControllers.BackColor = Color.FromArgb(32, 34, 40);
            list_allControllers.ForeColor = Color.FromArgb(240, 243, 248);
            list_customControllers.BackColor = Color.FromArgb(32, 34, 40);
            list_customControllers.ForeColor = Color.FromArgb(240, 243, 248);
            group_props.ForeColor = Color.White;
            chooseType.BackColor = Color.FromArgb(32, 34, 40);
            chooseType.ForeColor = Color.White;

            list_allControllers.HorizontalScrollbar = true; list_customControllers.HorizontalScrollbar = true;

            chooseType.Items.AddRange(new String[] { "Pro Controller (Switch)", "Joy-Con Izquierdo (Switch)", "Joy-Con Derecho (Switch)", "Mando DirectInput Genérico (PC -> XInput)" });

            chooseType.FormattingEnabled = true;
            group_props.Enabled = false;

            if (File.Exists(path)) {
                using (StreamReader file = new StreamReader(path)) {
                    string line = String.Empty;
                    while ((line = file.ReadLine()) != null && (line != String.Empty)) {
                        String[] split = line.Split('|');
                        //won't break existing config file
                        String serial_number = "";
                        if (split.Length > 4) {
                            serial_number = split[4];
                        }
                        list_customControllers.Items.Add(new SController(split[0], ushort.Parse(split[1]), ushort.Parse(split[2]), byte.Parse(split[3]), serial_number));
                    }
                }
            }

            CopyCustomControllers();
            RefreshControllerList();
        }

        public void CopyCustomControllers() {
            Program.thirdPartyCons.Clear();
            foreach (SController v in list_customControllers.Items) {
                Program.thirdPartyCons.Add(v);
            }
        }

        private bool ContainsText(ListBox a, String manu) {
            foreach (SController v in a.Items) {
                if (v == null)
                    continue;
                if (v.name == null)
                    continue;
                if (v.name.Equals(manu))
                    return true;
            }
            return false;
        }

        private void RefreshControllerList() {
            list_allControllers.Items.Clear();
            IntPtr ptr = HIDapi.hid_enumerate(0x0, 0x0);
            IntPtr top_ptr = ptr;

            hid_device_info enumerate; // Add device to list
            while (ptr != IntPtr.Zero) {
                enumerate = (hid_device_info)Marshal.PtrToStructure(ptr, typeof(hid_device_info));

                // Filter out non-controller hardware (LED lighting, webcams, audio cards)
                if ((enumerate.vendor_id == 0x0B05 && enumerate.product_id == 0x1872) || // ASUS AURA LED
                    (enumerate.vendor_id == 0x0951 && enumerate.product_id == 0x16A4) || // HyperX Surround Sound
                    (enumerate.vendor_id == 0x046D && enumerate.product_id == 0x0944) || // Logitech MX Brio Webcam
                    (enumerate.vendor_id == 0x534D && enumerate.product_id == 0x2109)) { // USB Video Capture
                    ptr = enumerate.next;
                    continue;
                }

                string serial = enumerate.serial_number ?? "000000000001";
                string prodName = enumerate.product_string;

                // Handle binary mojibake (e.g. Razer auxiliary endpoints)
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

                String name = prodName + " (" + enumerate.vendor_id + "-" + enumerate.product_id + "-" + serial + ")";
                if (!ContainsText(list_customControllers, name) && !ContainsText(list_allControllers, name)) {
                    list_allControllers.Items.Add(new SController(name, enumerate.vendor_id, enumerate.product_id, 0, serial));
                    Console.WriteLine("Found controller " + name);
                }

                ptr = enumerate.next;
            }
            HIDapi.hid_free_enumeration(top_ptr);
        }

        private void btn_add_Click(object sender, EventArgs e) {
            if (list_allControllers.SelectedItem != null) {
                list_customControllers.Items.Add(list_allControllers.SelectedItem);
                list_allControllers.Items.Remove(list_allControllers.SelectedItem);

                list_allControllers.ClearSelected();
            }
        }

        private void btn_remove_Click(object sender, EventArgs e) {
            if (list_customControllers.SelectedItem != null) {
                list_allControllers.Items.Add(list_customControllers.SelectedItem);
                list_customControllers.Items.Remove(list_customControllers.SelectedItem);

                list_customControllers.ClearSelected();
            }
        }

        private void btn_apply_Click(object sender, EventArgs e) {
            String sc = "";
            foreach (SController v in list_customControllers.Items) {
                sc += v.Serialise() + "\r\n";
            }
            File.WriteAllText(path, sc);
            CopyCustomControllers();
            Program.mgr?.CheckForNewControllers();
        }

        private void btn_applyAndClose_Click(object sender, EventArgs e) {
            btn_apply_Click(sender, e);
            Close();
        }

        private void _3rdPartyControllers_FormClosing(object sender, FormClosingEventArgs e) {
            btn_apply_Click(sender, e);
        }

        private void btn_refresh_Click(object sender, EventArgs e) {
            RefreshControllerList();
        }

        private void list_allControllers_SelectedValueChanged(object sender, EventArgs e) {
            if (list_allControllers.SelectedItem != null)
                tip_device.Show((list_allControllers.SelectedItem as SController).name, list_allControllers);
        }

        private void list_customControllers_SelectedValueChanged(object sender, EventArgs e) {
            if (list_customControllers.SelectedItem != null) {
                SController v = (list_customControllers.SelectedItem as SController);
                tip_device.Show(v.name, list_customControllers);

                chooseType.SelectedIndex = v.type - 1;

                group_props.Enabled = true;
            } else {
                chooseType.SelectedIndex = -1;
                group_props.Enabled = false;
            }
        }

        private void list_customControllers_MouseDown(object sender, MouseEventArgs e) {
            if (e.Y > list_customControllers.ItemHeight * list_customControllers.Items.Count)
                list_customControllers.SelectedItems.Clear();
        }

        private void list_allControllers_MouseDown(object sender, MouseEventArgs e) {
            if (e.Y > list_allControllers.ItemHeight * list_allControllers.Items.Count)
                list_allControllers.SelectedItems.Clear();
        }

        private void chooseType_SelectedValueChanged(object sender, EventArgs e) {
            if (list_customControllers.SelectedItem != null && chooseType.SelectedIndex >= 0) {
                SController v = (list_customControllers.SelectedItem as SController);
                v.type = (byte)(chooseType.SelectedIndex + 1);
                int idx = list_customControllers.SelectedIndex;
                list_customControllers.Items[idx] = v;
            }
        }
    }
}

