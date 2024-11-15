﻿namespace ExploracionPlanes
{
    partial class PlantillaBlanco
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.BT_GuardarReporte = new System.Windows.Forms.Button();
            this.BT_Imprimir = new System.Windows.Forms.Button();
            this.DGV_Análisis = new System.Windows.Forms.DataGridView();
            this.printDialog1 = new System.Windows.Forms.PrintDialog();
            this.EstructuraCol = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Prioridad = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Column3 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Volumen = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Column4 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Column5 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Ref = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Blanco = new System.Windows.Forms.DataGridViewTextBoxColumn();
            ((System.ComponentModel.ISupportInitialize)(this.DGV_Análisis)).BeginInit();
            this.SuspendLayout();
            // 
            // BT_GuardarReporte
            // 
            this.BT_GuardarReporte.Location = new System.Drawing.Point(82, 391);
            this.BT_GuardarReporte.Name = "BT_GuardarReporte";
            this.BT_GuardarReporte.Size = new System.Drawing.Size(109, 23);
            this.BT_GuardarReporte.TabIndex = 27;
            this.BT_GuardarReporte.Text = "Guardar como PDF";
            this.BT_GuardarReporte.UseVisualStyleBackColor = true;
            this.BT_GuardarReporte.Click += new System.EventHandler(this.BT_GuardarReporte_Click);
            // 
            // BT_Imprimir
            // 
            this.BT_Imprimir.Location = new System.Drawing.Point(209, 391);
            this.BT_Imprimir.Name = "BT_Imprimir";
            this.BT_Imprimir.Size = new System.Drawing.Size(109, 23);
            this.BT_Imprimir.TabIndex = 26;
            this.BT_Imprimir.Text = "Imprimir";
            this.BT_Imprimir.UseVisualStyleBackColor = true;
            this.BT_Imprimir.Click += new System.EventHandler(this.BT_Imprimir_Click);
            // 
            // DGV_Análisis
            // 
            this.DGV_Análisis.AllowUserToAddRows = false;
            this.DGV_Análisis.AllowUserToDeleteRows = false;
            this.DGV_Análisis.AllowUserToResizeRows = false;
            this.DGV_Análisis.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.DGV_Análisis.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.EstructuraCol,
            this.Prioridad,
            this.Column3,
            this.Volumen,
            this.Column4,
            this.Column5,
            this.Ref,
            this.Blanco});
            this.DGV_Análisis.Location = new System.Drawing.Point(12, 27);
            this.DGV_Análisis.Name = "DGV_Análisis";
            this.DGV_Análisis.RowHeadersVisible = false;
            this.DGV_Análisis.Size = new System.Drawing.Size(512, 348);
            this.DGV_Análisis.TabIndex = 25;
            // 
            // printDialog1
            // 
            this.printDialog1.UseEXDialog = true;
            // 
            // EstructuraCol
            // 
            this.EstructuraCol.HeaderText = "Estructura";
            this.EstructuraCol.Name = "EstructuraCol";
            this.EstructuraCol.Width = 60;
            // 
            // Prioridad
            // 
            this.Prioridad.HeaderText = "Prioridad";
            this.Prioridad.Name = "Prioridad";
            this.Prioridad.Visible = false;
            this.Prioridad.Width = 50;
            // 
            // Column3
            // 
            this.Column3.HeaderText = "Métrica";
            this.Column3.Name = "Column3";
            this.Column3.Width = 60;
            // 
            // Volumen
            // 
            this.Volumen.HeaderText = "Vol [cm3]";
            this.Volumen.Name = "Volumen";
            this.Volumen.Width = 60;
            // 
            // Column4
            // 
            this.Column4.HeaderText = "En Plan";
            this.Column4.Name = "Column4";
            // 
            // Column5
            // 
            this.Column5.HeaderText = "Esperado";
            this.Column5.Name = "Column5";
            // 
            // Ref
            // 
            this.Ref.HeaderText = "Ref.";
            this.Ref.Name = "Ref";
            this.Ref.Width = 30;
            // 
            // Blanco
            // 
            this.Blanco.HeaderText = "Blanco";
            this.Blanco.Name = "Blanco";
            this.Blanco.Visible = false;
            // 
            // PlantillaBlanco
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(574, 450);
            this.Controls.Add(this.BT_GuardarReporte);
            this.Controls.Add(this.BT_Imprimir);
            this.Controls.Add(this.DGV_Análisis);
            this.Name = "PlantillaBlanco";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Plantilla en blanco";
            ((System.ComponentModel.ISupportInitialize)(this.DGV_Análisis)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button BT_GuardarReporte;
        private System.Windows.Forms.Button BT_Imprimir;
        private System.Windows.Forms.DataGridView DGV_Análisis;
        private System.Windows.Forms.PrintDialog printDialog1;
        private System.Windows.Forms.DataGridViewTextBoxColumn EstructuraCol;
        private System.Windows.Forms.DataGridViewTextBoxColumn Prioridad;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column3;
        private System.Windows.Forms.DataGridViewTextBoxColumn Volumen;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column4;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column5;
        private System.Windows.Forms.DataGridViewTextBoxColumn Ref;
        private System.Windows.Forms.DataGridViewTextBoxColumn Blanco;
    }
}