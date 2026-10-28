namespace PADPlus {
    partial class _3rdPartyControllers {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing) {
            if (disposing && (components != null)) {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(_3rdPartyControllers));
            this.list_allControllers = new System.Windows.Forms.ListBox();
            this.list_customControllers = new System.Windows.Forms.ListBox();
            this.btn_add = new System.Windows.Forms.Button();
            this.btn_remove = new System.Windows.Forms.Button();
            this.group_props = new System.Windows.Forms.GroupBox();
            this.lbl_propTip = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.chooseType = new System.Windows.Forms.ComboBox();
            this.btn_applyAndClose = new System.Windows.Forms.Button();
            this.btn_apply = new System.Windows.Forms.Button();
            this.lbl_all = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.lbl_helpHint = new System.Windows.Forms.Label();
            this.tip_device = new System.Windows.Forms.ToolTip(this.components);
            this.btn_refresh = new System.Windows.Forms.Button();
            this.group_props.SuspendLayout();
            this.SuspendLayout();
            // 
            // list_allControllers
            // 
            this.list_allControllers.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(34)))), ((int)(((byte)(40)))));
            this.list_allControllers.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.list_allControllers.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.list_allControllers.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(243)))), ((int)(((byte)(248)))));
            this.list_allControllers.FormattingEnabled = true;
            this.list_allControllers.ItemHeight = 15;
            this.list_allControllers.Location = new System.Drawing.Point(16, 42);
            this.list_allControllers.Name = "list_allControllers";
            this.list_allControllers.Size = new System.Drawing.Size(240, 332);
            this.list_allControllers.TabIndex = 0;
            this.list_allControllers.SelectedValueChanged += new System.EventHandler(this.list_allControllers_SelectedValueChanged);
            this.list_allControllers.MouseDown += new System.Windows.Forms.MouseEventHandler(this.list_allControllers_MouseDown);
            // 
            // list_customControllers
            // 
            this.list_customControllers.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(34)))), ((int)(((byte)(40)))));
            this.list_customControllers.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.list_customControllers.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.list_customControllers.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(243)))), ((int)(((byte)(248)))));
            this.list_customControllers.FormattingEnabled = true;
            this.list_customControllers.ItemHeight = 15;
            this.list_customControllers.Location = new System.Drawing.Point(364, 42);
            this.list_customControllers.Name = "list_customControllers";
            this.list_customControllers.Size = new System.Drawing.Size(240, 182);
            this.list_customControllers.TabIndex = 1;
            this.list_customControllers.SelectedValueChanged += new System.EventHandler(this.list_customControllers_SelectedValueChanged);
            this.list_customControllers.MouseDown += new System.Windows.Forms.MouseEventHandler(this.list_customControllers_MouseDown);
            // 
            // btn_add
            // 
            this.btn_add.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(45)))), ((int)(((byte)(50)))), ((int)(((byte)(62)))));
            this.btn_add.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(70)))), ((int)(((byte)(78)))), ((int)(((byte)(96)))));
            this.btn_add.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btn_add.Font = new System.Drawing.Font("Segoe UI", 8.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.btn_add.ForeColor = System.Drawing.Color.White;
            this.btn_add.Location = new System.Drawing.Point(266, 80);
            this.btn_add.Name = "btn_add";
            this.btn_add.Size = new System.Drawing.Size(88, 36);
            this.btn_add.TabIndex = 2;
            this.btn_add.Text = "Agregar ➔";
            this.btn_add.UseVisualStyleBackColor = false;
            this.btn_add.Click += new System.EventHandler(this.btn_add_Click);
            // 
            // btn_remove
            // 
            this.btn_remove.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(45)))), ((int)(((byte)(50)))), ((int)(((byte)(62)))));
            this.btn_remove.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(70)))), ((int)(((byte)(78)))), ((int)(((byte)(96)))));
            this.btn_remove.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btn_remove.Font = new System.Drawing.Font("Segoe UI", 8.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.btn_remove.ForeColor = System.Drawing.Color.White;
            this.btn_remove.Location = new System.Drawing.Point(266, 126);
            this.btn_remove.Name = "btn_remove";
            this.btn_remove.Size = new System.Drawing.Size(88, 36);
            this.btn_remove.TabIndex = 3;
            this.btn_remove.Text = "⬅ Quitar";
            this.btn_remove.UseVisualStyleBackColor = false;
            this.btn_remove.Click += new System.EventHandler(this.btn_remove_Click);
            // 
            // group_props
            // 
            this.group_props.Controls.Add(this.lbl_propTip);
            this.group_props.Controls.Add(this.label2);
            this.group_props.Controls.Add(this.chooseType);
            this.group_props.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.group_props.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(220)))), ((int)(((byte)(226)))), ((int)(((byte)(235)))));
            this.group_props.Location = new System.Drawing.Point(364, 234);
            this.group_props.Name = "group_props";
            this.group_props.Size = new System.Drawing.Size(240, 140);
            this.group_props.TabIndex = 4;
            this.group_props.TabStop = false;
            this.group_props.Text = "Configuración del Mando";
            // 
            // lbl_propTip
            // 
            this.lbl_propTip.Font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.lbl_propTip.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(150)))), ((int)(((byte)(160)))), ((int)(((byte)(175)))));
            this.lbl_propTip.Location = new System.Drawing.Point(12, 82);
            this.lbl_propTip.Name = "lbl_propTip";
            this.lbl_propTip.Size = new System.Drawing.Size(216, 48);
            this.lbl_propTip.TabIndex = 2;
            this.lbl_propTip.Text = "💡 Elige Pro Controller para mandos estilo GameCube (PDP Fight Pad), PowerA o genéricos de Switch.";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(205)))), ((int)(((byte)(215)))));
            this.label2.Location = new System.Drawing.Point(12, 26);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(126, 15);
            this.label2.TabIndex = 1;
            this.label2.Text = "Emular en PAD+ como:";
            // 
            // chooseType
            // 
            this.chooseType.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(34)))), ((int)(((byte)(40)))));
            this.chooseType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.chooseType.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.chooseType.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.chooseType.ForeColor = System.Drawing.Color.White;
            this.chooseType.FormattingEnabled = true;
            this.chooseType.Location = new System.Drawing.Point(12, 48);
            this.chooseType.Name = "chooseType";
            this.chooseType.Size = new System.Drawing.Size(216, 23);
            this.chooseType.TabIndex = 0;
            this.chooseType.SelectedValueChanged += new System.EventHandler(this.chooseType_SelectedValueChanged);
            // 
            // btn_applyAndClose
            // 
            this.btn_applyAndClose.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(120)))), ((int)(((byte)(212)))));
            this.btn_applyAndClose.FlatAppearance.BorderSize = 0;
            this.btn_applyAndClose.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btn_applyAndClose.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.btn_applyAndClose.ForeColor = System.Drawing.Color.White;
            this.btn_applyAndClose.Location = new System.Drawing.Point(474, 388);
            this.btn_applyAndClose.Name = "btn_applyAndClose";
            this.btn_applyAndClose.Size = new System.Drawing.Size(130, 36);
            this.btn_applyAndClose.TabIndex = 5;
            this.btn_applyAndClose.Text = "Guardar y Cerrar";
            this.btn_applyAndClose.UseVisualStyleBackColor = false;
            this.btn_applyAndClose.Click += new System.EventHandler(this.btn_applyAndClose_Click);
            // 
            // btn_apply
            // 
            this.btn_apply.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(45)))), ((int)(((byte)(50)))), ((int)(((byte)(62)))));
            this.btn_apply.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(70)))), ((int)(((byte)(78)))), ((int)(((byte)(96)))));
            this.btn_apply.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btn_apply.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.btn_apply.ForeColor = System.Drawing.Color.White;
            this.btn_apply.Location = new System.Drawing.Point(374, 388);
            this.btn_apply.Name = "btn_apply";
            this.btn_apply.Size = new System.Drawing.Size(92, 36);
            this.btn_apply.TabIndex = 6;
            this.btn_apply.Text = "Aplicar";
            this.btn_apply.UseVisualStyleBackColor = false;
            this.btn_apply.Click += new System.EventHandler(this.btn_apply_Click);
            // 
            // lbl_all
            // 
            this.lbl_all.AutoSize = true;
            this.lbl_all.Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.lbl_all.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(230)))), ((int)(((byte)(235)))), ((int)(((byte)(245)))));
            this.lbl_all.Location = new System.Drawing.Point(14, 18);
            this.lbl_all.Name = "lbl_all";
            this.lbl_all.Size = new System.Drawing.Size(193, 17);
            this.lbl_all.TabIndex = 7;
            this.lbl_all.Text = "Dispositivos HID en el Sistema:";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.label1.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(230)))), ((int)(((byte)(235)))), ((int)(((byte)(245)))));
            this.label1.Location = new System.Drawing.Point(362, 18);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(183, 17);
            this.label1.TabIndex = 8;
            this.label1.Text = "Mandos Mapeados a PAD+:";
            // 
            // lbl_helpHint
            // 
            this.lbl_helpHint.AutoSize = true;
            this.lbl_helpHint.Font = new System.Drawing.Font("Segoe UI", 8.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.lbl_helpHint.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(150)))), ((int)(((byte)(160)))), ((int)(((byte)(175)))));
            this.lbl_helpHint.Location = new System.Drawing.Point(14, 399);
            this.lbl_helpHint.Name = "lbl_helpHint";
            this.lbl_helpHint.Size = new System.Drawing.Size(306, 15);
            this.lbl_helpHint.TabIndex = 10;
            this.lbl_helpHint.Text = "💡 Selecciona tu mando y haz clic en Agregar ➔ para activarlo.";
            // 
            // btn_refresh
            // 
            this.btn_refresh.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(45)))), ((int)(((byte)(50)))), ((int)(((byte)(62)))));
            this.btn_refresh.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(70)))), ((int)(((byte)(78)))), ((int)(((byte)(96)))));
            this.btn_refresh.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btn_refresh.Font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.btn_refresh.ForeColor = System.Drawing.Color.White;
            this.btn_refresh.Location = new System.Drawing.Point(266, 190);
            this.btn_refresh.Name = "btn_refresh";
            this.btn_refresh.Size = new System.Drawing.Size(88, 36);
            this.btn_refresh.TabIndex = 9;
            this.btn_refresh.Text = "🔄 Actualizar";
            this.btn_refresh.UseVisualStyleBackColor = false;
            this.btn_refresh.Click += new System.EventHandler(this.btn_refresh_Click);
            // 
            // _3rdPartyControllers
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(22)))), ((int)(((byte)(24)))), ((int)(((byte)(29)))));
            this.ClientSize = new System.Drawing.Size(622, 436);
            this.Controls.Add(this.lbl_helpHint);
            this.Controls.Add(this.btn_refresh);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.lbl_all);
            this.Controls.Add(this.btn_apply);
            this.Controls.Add(this.btn_applyAndClose);
            this.Controls.Add(this.group_props);
            this.Controls.Add(this.btn_remove);
            this.Controls.Add(this.btn_add);
            this.Controls.Add(this.list_customControllers);
            this.Controls.Add(this.list_allControllers);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Name = "_3rdPartyControllers";
            this.Text = "PAD+ — Mandos de Terceros (3rd-Party)";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this._3rdPartyControllers_FormClosing);
            this.group_props.ResumeLayout(false);
            this.group_props.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ListBox list_allControllers;
        private System.Windows.Forms.ListBox list_customControllers;
        private System.Windows.Forms.Button btn_add;
        private System.Windows.Forms.Button btn_remove;
        private System.Windows.Forms.GroupBox group_props;
        private System.Windows.Forms.Button btn_applyAndClose;
        private System.Windows.Forms.Button btn_apply;
        private System.Windows.Forms.Label lbl_all;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ToolTip tip_device;
        private System.Windows.Forms.Button btn_refresh;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ComboBox chooseType;
        private System.Windows.Forms.Label lbl_propTip;
        private System.Windows.Forms.Label lbl_helpHint;
    }
}

