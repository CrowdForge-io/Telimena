﻿using System;
using System.Diagnostics;
using System.Reflection;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Telimena.Client;

namespace TelimenaTestSandboxApp
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            this.InitializeComponent();
            this.apiUrlTextBox.Text = string.IsNullOrEmpty(Properties.Settings.Default.baseUri) ? "http://localhost:7757/" : Properties.Settings.Default.baseUri;
            this.teli = new Telimena.Client.Telimena(telemetryApiBaseUrl: new Uri(this.apiUrlTextBox.Text));
            this.Text = $"Sandbox v. {Assembly.GetExecutingAssembly().GetName().Version}";
        }

        private string PresentResponse(TelimenaResponseBase response)
        {
            if (response.Exception != null)
            {
                return response.Exception.ToString();
            }
            return new JavaScriptSerializer().Serialize(response);
        }

        private string PresentResponse(UpdateCheckResult response)
        {
            if (response.Exception != null)
            {
                return response.Exception.ToString();
            }
            return new JavaScriptSerializer().Serialize(response);
        }

        private Telimena.Client.Telimena teli;
        private TelimenaHammer hammer;

        private async void InitializeButton_Click(object sender, EventArgs e)
        {
            RegistrationResponse response = await this.teli.Initialize();

            this.resultTextBox.Text += this.teli.ProgramInfo.Name + " - " + this.PresentResponse(response) + Environment.NewLine;
        }

        private async void SendUpdateAppUsageButton_Click(object sender, EventArgs e)
        {
            StatisticsUpdateResponse result;
            Stopwatch sw = Stopwatch.StartNew();

            if (!string.IsNullOrEmpty(this.functionNameTextBox.Text))
            {
                result = await this.teli.ReportUsage(string.IsNullOrEmpty(this.functionNameTextBox.Text) ? null : this.functionNameTextBox.Text);
                sw.Stop();
            }
            else
            {
                result = await this.teli.ReportUsage();
                sw.Stop();
            }

            if (result.Exception == null)
            {
                this.resultTextBox.Text += $@"INSTANCE: {sw.ElapsedMilliseconds}ms " + this.teli.ProgramInfo.Name + " - " + this.PresentResponse(result) + Environment.NewLine;
            }
            else
            {
                MessageBox.Show(result.Exception.ToString());
            }
        }

        private void UpdateText(string text)
        {
            this.resultTextBox.Text = text + "\r\n" + this.resultTextBox.Text;
        }

        private async void checkForUpdateButton_Click(object sender, EventArgs e)
        {
            var response = await this.teli.CheckForUpdates();
            this.PresentResponse(response);
        }

        private async void handleUpdatesButton_Click(object sender, EventArgs e)
        {
            await this.teli.HandleUpdates(false);
        }

        private void setAppButton_Click(object sender, EventArgs e)
        {
            this.teli = new Telimena.Client.Telimena(telemetryApiBaseUrl: new Uri(this.apiUrlTextBox.Text));
            if (!string.IsNullOrEmpty(this.appNameTextBox.Text))
            {
                this.teli.ProgramInfo = new ProgramInfo
                {
                    Name = this.appNameTextBox.Text
                    , PrimaryAssembly = new AssemblyInfo {Company = "Comp A Ny", Name = this.appNameTextBox.Text + ".dll", Version = "1.0.0.0"}
                };
            }

            if (!string.IsNullOrEmpty(this.userNameTextBox.Text))
            {
                this.teli.UserInfo = new UserInfo {UserName = this.userNameTextBox.Text};
            }

            Properties.Settings.Default.baseUri = this.apiUrlTextBox.Text;
            Properties.Settings.Default.Save();
            
        }

        private void useCurrentAppButton_Click(object sender, EventArgs e)
        {
            this.teli = new Telimena.Client.Telimena(telemetryApiBaseUrl: new Uri(this.apiUrlTextBox.Text));
            Properties.Settings.Default.baseUri = this.apiUrlTextBox.Text;
            Properties.Settings.Default.Save();
        }

        private async void static_sendUsageReportButton_Click(object sender, EventArgs e)
        {
            StatisticsUpdateResponse result;
            Stopwatch sw = Stopwatch.StartNew();

            if (!string.IsNullOrEmpty(this.static_functionNameTextBox.Text))
            {
                result = await Telimena.Client.Telimena.ReportUsageStatic(string.IsNullOrEmpty(this.static_functionNameTextBox.Text)
                    ? null
                    : this.static_functionNameTextBox.Text);
                sw.Stop();
            }
            else
            {
                result = await Telimena.Client.Telimena.ReportUsageStatic();
                sw.Stop();
            }

            if (result.Exception == null)
            {
                this.resultTextBox.Text += $@"STATIC: {sw.ElapsedMilliseconds}ms " + this.PresentResponse(result) + Environment.NewLine;
            }
            else
            {
                MessageBox.Show(result.Exception.ToString());
            }
        }

        private async void hammer_StartButton_Click(object sender, EventArgs e)
        {
            this.hammer?.Stop();
            this.hammer = new TelimenaHammer(this.apiUrlTextBox.Text,
                Convert.ToInt32(this.hammer_AppNumberSeedBox.Text),
                Convert.ToInt32(this.hammer_numberOfApps_TextBox.Text),
                 Convert.ToInt32(this.hammer_numberOfFuncs_TextBox.Text),
                 Convert.ToInt32(this.hammer_numberOfUsers_TextBox.Text),
                 Convert.ToInt32(this.hammer_delayMinTextBox.Text),
                 Convert.ToInt32(this.hammer_delayMaxTextBox.Text),
                 Convert.ToInt32(this.hammer_DurationTextBox.Text),
                this.UpdateText
                );

           await this.hammer.Hit();
        }

        private void hammer_StopBtn_Click(object sender, EventArgs e)
        {
            this.hammer?.Stop();
        }

     
    }
}