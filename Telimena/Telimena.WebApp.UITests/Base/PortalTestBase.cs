﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Web.Configuration;
using System.Xml.Linq;
using AutomaticTestsClient;
using NUnit.Framework;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.IE;
using OpenQA.Selenium.Remote;
using OpenQA.Selenium.Support.PageObjects;
using OpenQA.Selenium.Support.UI;
using Telimena.WebApp.UiStrings;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Telimena.WebApp.UITests.IntegrationTests.TestAppInteraction;
using TelimenaClient;
using TestContext = Microsoft.VisualStudio.TestTools.UnitTesting.TestContext;

namespace Telimena.WebApp.UITests.Base
{
    [TestFixture]
    public abstract class PortalTestBase
    {


        protected List<string> errors = new List<string>();
        protected List<string> outputs = new List<string>();

        protected void LaunchTestsApp(Actions action, string appName, ProgramInfo pi = null, string functionName = null, bool waitForExit = true)
        {
            FileInfo exe = TestAppProvider.ExtractApp(appName);

            Arguments args = new Arguments() { ApiUrl = this.BaseUrl, Action = action };
            args.ProgramInfo = pi;
            args.FunctionName = functionName;


            Process process = ProcessCreator.Create(exe, args, this.outputs, this.errors);
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            if (waitForExit)
            {
                process.WaitForExit();
            }
        }

        protected T LaunchTestsAppAndGetResult<T>(Actions action, string appName, ProgramInfo pi = null, string functionName = null, bool waitForExit = true) where T : class
        {
            this.LaunchTestsApp(action,appName, pi, functionName,waitForExit);

            T result = this.ParseOutput<T>();
            this.outputs.Clear();
            this.errors.Clear();
            return result;
        }

        protected T ParseOutput<T>() where T : class
        {
            foreach (string output in this.outputs)
            {
                if (!string.IsNullOrWhiteSpace(output))
                {
                    try
                    {
                        T obj = JsonConvert.DeserializeObject<T>(output);
                        if (obj != null)
                        {
                            return obj;
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
            }

            return null;
        }
        protected static string GetSetting(string key)
        {
            if (NUnit.Framework.TestContext.Parameters.Count == 0)
            {
                return TryGetSettingFromXml(key);
            }
            var x =  NUnit.Framework.TestContext.Parameters[key];
            return x;
        }

        private static string TryGetSettingFromXml(string key)
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);

            var file = dir.GetFiles("*.runsettings", SearchOption.AllDirectories).FirstOrDefault();
            if (file != null)
            {
                XDocument xDoc = XDocument.Load(file.FullName);
                var ele = xDoc.Root.Element("TestRunParameters").Elements().FirstOrDefault(x => x.Attribute("name")?.Value == key);
                return ele?.Attribute("value")?.Value;
            }

            return null;
        }


        protected static T GetSetting<T>(string key) 
        {
            string val = GetSetting(key);
            if (val == null)
            {
                throw new ArgumentException($"Missing setting: {key}");
            }
            return (T)Convert.ChangeType(val, typeof(T));
        }

        public readonly string AdminName = GetSetting<string>(ConfigKeys.AdminName);
        public readonly string UserName = GetSetting(ConfigKeys.UserName);
        public readonly string AdminPassword = GetSetting(ConfigKeys.AdminPassword);
        public readonly string UserPassword = GetSetting(ConfigKeys.UserPassword);

        private readonly bool isLocalTestSetting = GetSetting<bool>(ConfigKeys.IsLocalTest);


        internal static RemoteWebDriver RemoteDriver;
        internal IWebDriver Driver => RemoteDriver;
        internal ITakesScreenshot Screenshooter => this.Driver as ITakesScreenshot;

        protected ITestEngine TestEngine { get; set; }

        protected string BaseUrl => this.TestEngine.BaseUrl;

        [OneTimeTearDown]
        public void TestCleanup()
        {
            this.TestEngine.BaseCleanup();

        }

        [OneTimeSetUp]
        public void TestInitialize()
        {
            
            if (this.isLocalTestSetting)
            {
                this.TestEngine = new LocalHostTestEngine();
            }
            else
            {
                this.TestEngine = new DeployedTestEngine(GetSetting<string>(ConfigKeys.PortalUrl));
            }
            this.TestEngine.BaseInitialize();
        }

      

        public void GoToAdminHomePage()
        {
            try
            {

                this.Driver.Navigate().GoToUrl(this.GetAbsoluteUrl(""));
                this.LoginAdminIfNeeded();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Error while logging in admin", ex);
            }
        }


        public string GetAbsoluteUrl(string relativeUrl)
        {
            return this.TestEngine.GetAbsoluteUrl(relativeUrl);
        }

        public void RecognizeAdminDashboardPage()
        {
            WebDriverWait wait = new WebDriverWait(this.Driver, TimeSpan.FromSeconds(15));
            if (this.Driver.Url.Contains("ChangePassword"))
            {
                Log("Going from change password page to Admin dashboard");
                this.Driver.Navigate().GoToUrl(this.GetAbsoluteUrl(""));
            }
            wait.Until(x => x.FindElement(By.Id(Strings.Id.PortalSummary)));
        }

        public IWebElement TryFind(string nameOrId, int timeoutSeconds = 10)
        {
            try
            {
                WebDriverWait wait = new WebDriverWait(this.Driver, TimeSpan.FromSeconds(timeoutSeconds));
                return wait.Until(x => x.FindElement(By.Id(nameOrId)));
            }
            catch { }

            return null;
        }

        public IWebElement TryFind(By by, int timeoutSeconds = 10)
        {
            try
            {
                WebDriverWait wait = new WebDriverWait(this.Driver, TimeSpan.FromSeconds(timeoutSeconds));
                return wait.Until(x => x.FindElement(by));
            }
            catch { }

            return null;
        }

        protected  bool IsLoggedIn()
        {
            if (this.TryFind(Strings.Id.MainHeader) != null)
            {
                return true;
            }

            return false;
        }


        protected void HandlerError(Exception ex, List<string> outputs = null, List<string> errors = null, [CallerMemberName] string memberName = "")
        {
            Screenshot screen = this.Screenshooter.GetScreenshot();
            var path = Common.CreatePngPath(memberName);
            screen.SaveAsFile(path, ScreenshotImageFormat.Png);
            var page = this.Driver.PageSource;

            string errorOutputs = "";
            if (errors != null)
            {
                errorOutputs = String.Join("\r\n", errors);
            }
            string normalOutputs = "";
            if (outputs != null)
            {
                normalOutputs = String.Join("\r\n", outputs);
            }

            throw new AssertFailedException($"{ex}\r\n\r\n{this.PresentParams()}\r\n\r\n{errorOutputs}\r\n\r\n{normalOutputs}\r\n\r\n{page}", ex);

            //this.TestContext.AddResultFile(path);
        }

        private string PresentParams()
        {
            var sb = new StringBuilder();
            sb.Append("Url: " + this.Driver.Url);
            sb.Append("TestContext Parameters: ");
            foreach (var testParameter in NUnit.Framework.TestContext.Parameters.Names)
            {
                sb.Append(testParameter + ": " + NUnit.Framework.TestContext.Parameters[testParameter] + " ");
            }

            return sb.ToString();
        }

        protected static void Log(string info)
        {
            Debug.WriteLine("UiTestsLogger:" + info);
        }

        public void LoginAdminIfNeeded()
        {
            this.LoginIfNeeded(this.AdminName, this.AdminPassword);
        }

        private void LoginIfNeeded(string userName, string password)
        {
            if (!this.IsLoggedIn())
            {
                this.Driver.Navigate().GoToUrl(this.GetAbsoluteUrl("Account/Login"));
            }

            if (this.Driver.Url.IndexOf("Login", StringComparison.InvariantCultureIgnoreCase) != -1 && this.Driver.FindElement(new ByIdOrName(Strings.Id.LoginForm)) != null)
            {
                Log("Trying to log in...");
                IWebElement login = this.Driver.FindElement(new ByIdOrName(Strings.Id.Email));

                if (login != null)
                {
                    IWebElement pass = this.Driver.FindElement(new ByIdOrName(Strings.Id.Password));
                    login.SendKeys(userName);
                    pass.SendKeys(password);
                    IWebElement submit = this.Driver.FindElement(new ByIdOrName(Strings.Id.SubmitLogin));
                    submit.Click();
                    this.GoToAdminHomePage();
                    this.RecognizeAdminDashboardPage();

                }
            }
            else
            {
                Log("Skipping logging in");
            }
        }
    }
}