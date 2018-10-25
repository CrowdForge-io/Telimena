﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutomaticTestsClient;
using NUnit.Framework;
using NUnit.Framework.Internal;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using Telimena.WebApp.UiStrings;
using Telimena.WebApp.UITests.Base;
using Telimena.WebApp.UITests.IntegrationTests.TestAppInteraction;
using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
using ExpectedConditions = SeleniumExtras.WaitHelpers.ExpectedConditions;

namespace Telimena.WebApp.UITests.IntegrationTests
{
    [TestFixture()]
    public partial class UiTests : PortalTestBase
    {

     
        [SetUp]
        public void ResetLists()
        {
            errors = new List<string>();
            outputs = new List<string>();
        }
        [Test]
        public void Test_AppUsageTable()
        {

          
                this.LaunchTestsApp(Actions.Initialize, TestAppProvider.FileNames.TestAppV1);
                Task.Delay(1000).GetAwaiter().GetResult();
                var previous = this.GetLatestUsageFromTable();
            Task.Delay(1000).GetAwaiter().GetResult();

            this.LaunchTestsApp(Actions.Initialize, TestAppProvider.FileNames.TestAppV1);

              var current = this.GetLatestUsageFromTable();

                Assert.IsTrue(current > previous, $"current {current} is not larger than previous {previous}");


        }

        private DateTime GetLatestUsageFromTable()
        {
            try
            {
                this.GoToAdminHomePage();

                WebDriverWait wait = new WebDriverWait(this.Driver, TimeSpan.FromSeconds(15));

                this.Driver.FindElement(By.Id(TestAppProvider.AutomaticTestsClientAppName + "_menu")).Click();
                IWebElement statLink = wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(By.Id(TestAppProvider.AutomaticTestsClientAppName + "_statsLink")));

                statLink.Click();

                IWebElement programsTable = wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(By.Id(Strings.Id.ProgramUsageTable)));
                wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementToBeClickable(By.Id(Strings.Id.ProgramUsageTable)));
                wait.Until(ExpectedConditions.InvisibilityOfElementLocated(By.Id("ProgramUsageTable_processing")));

                IWebElement latestRow = programsTable.FindElement(By.TagName("tbody")).FindElements(By.TagName("tr")).FirstOrDefault();

                if (latestRow == null)
                {
                    throw new InvalidOperationException("Latest row is null");
                }

                string dateTime = "Not initialized";
                try
                {
                    dateTime = latestRow.FindElements(By.TagName("td")).FirstOrDefault()?.Text;
                    DateTime parsed = DateTime.ParseExact(dateTime, "dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture);
                    return parsed;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Cannot parse [{dateTime}] as DateTime", ex);
                }
            }
            catch (Exception ex)
            {
                this.HandlerError(ex, this.outputs,this.errors);
                return default(DateTime);
            }
        }

    
    }
}
