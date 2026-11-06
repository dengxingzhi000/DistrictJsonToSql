using System;
using System.IO;
using System.Windows.Forms;

namespace DistrictJsonToSql
{
    public partial class MainForm : Form
    {
        private Button btnSelectFile;
        private TextBox txtFilePath;
        private Button btnGenerate;
        private TextBox txtOutput;
        private Label lblStatus;

        public MainForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.btnSelectFile = new Button();
            this.txtFilePath = new TextBox();
            this.btnGenerate = new Button();
            this.txtOutput = new TextBox();
            this.lblStatus = new Label();
            this.SuspendLayout();

            this.btnSelectFile.Location = new System.Drawing.Point(12, 12);
            this.btnSelectFile.Name = "btnSelectFile";
            this.btnSelectFile.Size = new System.Drawing.Size(100, 30);
            this.btnSelectFile.Text = "选择JSON文件";
            this.btnSelectFile.UseVisualStyleBackColor = true;
            this.btnSelectFile.Click += new EventHandler(this.btnSelectFile_Click);

            this.txtFilePath.Location = new System.Drawing.Point(130, 17);
            this.txtFilePath.Name = "txtFilePath";
            this.txtFilePath.ReadOnly = true;
            this.txtFilePath.Size = new System.Drawing.Size(400, 20);
            this.txtFilePath.TabIndex = 1;

            this.btnGenerate.Enabled = false;
            this.btnGenerate.Location = new System.Drawing.Point(550, 12);
            this.btnGenerate.Name = "btnGenerate";
            this.btnGenerate.Size = new System.Drawing.Size(100, 30);
            this.btnGenerate.Text = "生成SQL";
            this.btnGenerate.UseVisualStyleBackColor = true;
            this.btnGenerate.Click += new EventHandler(this.btnGenerate_Click);

            this.txtOutput.Location = new System.Drawing.Point(12, 80);
            this.txtOutput.Multiline = true;
            this.txtOutput.Name = "txtOutput";
            this.txtOutput.ReadOnly = true;
            this.txtOutput.ScrollBars = ScrollBars.Both;
            this.txtOutput.Size = new System.Drawing.Size(760, 450);
            this.txtOutput.TabIndex = 3;

            this.lblStatus.AutoSize = true;
            this.lblStatus.Location = new System.Drawing.Point(12, 55);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(53, 13);
            this.lblStatus.Text = "准备就绪";

            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(784, 561);
            this.Controls.Add(this.lblStatus);
            this.Controls.Add(this.txtOutput);
            this.Controls.Add(this.btnGenerate);
            this.Controls.Add(this.txtFilePath);
            this.Controls.Add(this.btnSelectFile);
            this.Name = "MainForm";
            this.Text = "地域JSON转SQL工具";
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private void btnSelectFile_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*";
                openFileDialog.RestoreDirectory = true;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    txtFilePath.Text = openFileDialog.FileName;
                    btnGenerate.Enabled = true;
                    lblStatus.Text = "文件已选择，点击生成SQL";
                }
            }
        }

        private void btnGenerate_Click(object sender, EventArgs e)
        {
            try
            {
                lblStatus.Text = "正在生成SQL...";
                var reader = new JsonDistrictReader();
                var districts = reader.Read(txtFilePath.Text);
                if (districts.Count == 0)
                {
                    MessageBox.Show("JSON文件里没有行政区划数据", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    lblStatus.Text = "JSON为空";
                    return;
                }

                string sql = SqlGenerator.GenerateSql(districts);
                txtOutput.Text = sql;

                string sqlFileName = Path.ChangeExtension(txtFilePath.Text, ".sql");
                SqlGenerator.WriteSqlFile(sqlFileName, sql);

                lblStatus.Text = $"SQL生成完成，已保存到: {sqlFileName}";
                MessageBox.Show($"SQL文件已生成：{sqlFileName}", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (DistrictValidationException ex)
            {
                lblStatus.Text = "数据校验失败";
                ErrorLogger.Log("validate " + ex.NodePath, ex);
                MessageBox.Show(
                    $"行政区划数据不合法（位置 {ex.NodePath}）：{ex.Message}\n\n详细信息见：{ErrorLogger.LogPath}",
                    "数据校验失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                lblStatus.Text = "生成失败";
                ErrorLogger.Log("generate", ex);
                MessageBox.Show(
                    $"生成SQL时出错：{ex.Message}\n\n详细信息见：{ErrorLogger.LogPath}",
                    "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

    }
}