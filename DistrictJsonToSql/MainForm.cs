using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace DistrictJsonToSql
{
    public partial class MainForm : Form
    {
        private Button btnSelectFile;
        private TextBox txtFilePath;
        private Button btnGenerate;
        private TextBox txtOutput;
        private Label lblStatus;

        private static readonly GuidV7Generator _guidGen = new GuidV7Generator();
        private static string GenerateGuidV7() => _guidGen.Generate();

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
                string jsonContent = File.ReadAllText(txtFilePath.Text, Encoding.UTF8);

                var districts = JsonConvert.DeserializeObject<List<DistrictItem>>(jsonContent);
                if (districts == null || districts.Count == 0)
                {
                    MessageBox.Show("JSON文件格式错误或为空");
                    return;
                }

                string sql = GenerateSql(districts);
                txtOutput.Text = sql;

                string sqlFileName = Path.ChangeExtension(txtFilePath.Text, ".sql");
                File.WriteAllText(sqlFileName, sql, Encoding.UTF8);

                lblStatus.Text = $"SQL生成完成，已保存到: {sqlFileName}";
                MessageBox.Show($"SQL文件已生成：{sqlFileName}", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                lblStatus.Text = "生成失败";
                MessageBox.Show($"生成SQL时出错：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string GenerateSql(List<DistrictItem> districts)
        {
            var sqlBuilder = new StringBuilder();
            sqlBuilder.AppendLine("-- 地域数据插入SQL");
            sqlBuilder.AppendLine("-- 生成时间: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sqlBuilder.AppendLine();

            var insertStatements = new List<string>();

            foreach (var district in districts)
            {
                var provinceGuid = GenerateGuidV7();
                var provinceSql = GenerateInsertStatement(provinceGuid, district.Code + "0000", district.Name, 1, null);
                insertStatements.Add(provinceSql);

                if (district.Children != null)
                {
                    foreach (var city in district.Children)
                    {
                        var cityGuid = GenerateGuidV7();
                        var citySql = GenerateInsertStatement(cityGuid, city.Code + "00", city.Name, 2, district.Code + "0000");
                        insertStatements.Add(citySql);

                        if (city.Children != null)
                        {
                            foreach (var county in city.Children)
                            {
                                var countyGuid = GenerateGuidV7();
                                var countySql = GenerateInsertStatement(countyGuid, county.Code, county.Name, 3, city.Code + "00");
                                insertStatements.Add(countySql);

                                if (county.Children != null)
                                {
                                    foreach (var street in county.Children)
                                    {
                                        var streetGuid = GenerateGuidV7();
                                        var streetSql = GenerateInsertStatement(streetGuid, street.Code, street.Name, 4, county.Code);
                                        insertStatements.Add(streetSql);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            var batchSize = 100;
            for (int i = 0; i < insertStatements.Count; i += batchSize)
            {
                var batch = insertStatements.Skip(i).Take(batchSize);
                sqlBuilder.AppendLine("INSERT INTO public.tb_district (guid, code, \"name\", district_type, parent_code) VALUES");
                sqlBuilder.AppendLine(string.Join(",\n", batch) + ";");
                sqlBuilder.AppendLine();
            }

            return sqlBuilder.ToString();
        }

        private string GenerateInsertStatement(string guid, string code, string name, int districtType, string parentCode)
        {
            var parentValue = parentCode == null ? "NULL" : $"'{parentCode}'";
            return $"('{guid}'::uuid, '{code}', '{name.Replace("'", "''")}', {districtType}, {parentValue})";
        }
        private class GuidV7Generator
        {
            private readonly Random _random;
            private long _lastTimestamp;
            private int _counter;
            private readonly object _lock = new object();

            public GuidV7Generator()
            {
                _random = new Random();
                _lastTimestamp = 0;
                _counter = 0;
            }

            public string Generate()
            {
                lock (_lock)
                {
                    var currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                    if (currentTimestamp == _lastTimestamp)
                    {
                        _counter++;
                        if (_counter > 0xFFF)
                        {
                            currentTimestamp++;
                            _counter = 0;
                        }
                    }
                    else
                    {
                        _counter = 0;
                    }

                    _lastTimestamp = currentTimestamp;

                    var guidBytes = new byte[16];

                    var timestampBytes = BitConverter.GetBytes(currentTimestamp);
                    if (BitConverter.IsLittleEndian)
                        Array.Reverse(timestampBytes);
                    Array.Copy(timestampBytes, 2, guidBytes, 0, 6);

                    guidBytes[6] = (byte)((_counter >> 8) & 0x0F | 0x70);
                    guidBytes[7] = (byte)(_counter & 0xFF);

                    var randomBytes = new byte[8];
                    _random.NextBytes(randomBytes);
                    randomBytes[0] = (byte)((randomBytes[0] & 0x3F) | 0x80);
                    Array.Copy(randomBytes, 0, guidBytes, 8, 8);

                    return new Guid(guidBytes).ToString();
                }
            }
        }
    }

    public class DistrictItem
    {
        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("children")]
        public List<DistrictItem> Children { get; set; }
    }

}