﻿using System;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using Telimena.WebApp.UiStrings;
using Telimena.WebApp.UITests.Base;
using Telimena.WebApp.UITests.Base.TestAppInteraction;
using TelimenaClient;

namespace Telimena.WebApp.UITests.Ui
{
    using ExpectedConditions = SeleniumExtras.WaitHelpers.ExpectedConditions;

    [TestFixture]
    public partial class _1_UiTests : UiTestBase
    {

        private void UploadUpdater(string fileName)
        {
            this.GoToAdminHomePage();

            WebDriverWait wait = new WebDriverWait(this.Driver, TimeSpan.FromSeconds(15));

            this.Driver.FindElement(By.Id(Strings.Id.ToolkitManagementLink)).Click();
            wait.Until(ExpectedConditions.ElementIsVisible(By.Id(Strings.Id.ToolkitManagementForm)));

            var updater = TestAppProvider.GetFile(fileName);


            this.Driver.FindElement(By.Id(Strings.Id.UpdaterPackageUploader)).SendKeys(updater.FullName);


            wait.Until(x => x.FindElements(By.Id(Strings.Id.UpdaterUploadInfoBox)).FirstOrDefault(e => e.Text.Contains(updater.Name)));


            this.Driver.FindElement(By.Id(Strings.Id.SubmitUpdaterUpload)).Click();

            var confirmationBox = wait.Until(ExpectedConditions.ElementIsVisible(By.Id(Strings.Id.UpdaterConfirmationBox)));

            Assert.IsTrue(confirmationBox.GetAttribute("class").Contains("label-success"));

            Assert.IsTrue(confirmationBox.Text.Contains("Uploaded package 1.8.0.0 with ID "));

        }

        



        [Test]
        public void _01_UploadToolkit()
        {
            this.GoToAdminHomePage();

            WebDriverWait wait = new WebDriverWait(this.Driver, TimeSpan.FromSeconds(15));

            this.Driver.FindElement(By.Id(Strings.Id.ToolkitManagementLink)).Click();
            wait.Until(ExpectedConditions.ElementIsVisible(By.Id(Strings.Id.ToolkitManagementForm)));

            var file = TestAppProvider.GetFile("Telimena.Client.dll");


            this.Driver.FindElement(By.Id(Strings.Id.ToolkitPackageUploader)).SendKeys(file.FullName);


            wait.Until(x => x.FindElements(By.Id(Strings.Id.ToolkitUploadInfoBox)).FirstOrDefault(e => e.Text.Contains(file.Name)));


            this.Driver.FindElement(By.Id(Strings.Id.SubmitToolkitUpload)).Click();

            var confirmationBox = wait.Until(ExpectedConditions.ElementIsVisible(By.Id(Strings.Id.ToolkitConfirmationBox)));

            Assert.IsTrue(confirmationBox.GetAttribute("class").Contains("label-success"));

            Assert.IsTrue(confirmationBox.Text.Contains("Uploaded package 2.0.0.0 with ID "));

        }

        [Test]
        public void _02_UploadUpdaterExeTest()
        {
            try
            {
                UploadUpdater("Updater.exe");
            }
            catch (Exception ex)
            {
                this.HandlerError(ex);
            }
        }


        [Test]
        public void _03_UploadUpdaterZipTest()
        {
            try
            {
                UploadUpdater("Updater.zip");
            }
            catch (Exception ex)
            {
                this.HandlerError(ex);
            }
        }

        [Test]
        public void _03a_SetDefaultUpdaterPublic()
        {
            try
            {
                this.GoToAdminHomePage();
                this.Driver.FindElement(By.Id(Strings.Id.ToolkitManagementLink)).Click();
                WebDriverWait wait = new WebDriverWait(this.Driver, TimeSpan.FromSeconds(15));

                var checkBox = this.GetIsPublicCheckbox();
                var isChecked = checkBox.GetAttribute("checked");

                if (isChecked == "true")
                {
                    checkBox.Click();
                    Thread.Sleep(1000);
                    var table = wait.Until(ExpectedConditions.ElementIsVisible(By.Id(Strings.Id.UpdaterPackagesTable)));

                    var label = table.FindElement(By.XPath("../../label"));
                    Assert.AreEqual(true, label.Displayed);
                    Assert.AreEqual("Cannot change default updater", label.Text);
                   

                }
                else
                {
                    checkBox.Click();
                    Thread.Sleep(1000);
                    this.Driver.FindElement(By.Id(Strings.Id.ToolkitManagementLink)).Click();

                    checkBox = this.GetIsPublicCheckbox();
                    isChecked = checkBox.GetAttribute("checked");

                    Assert.AreEqual("true", isChecked);

                }


            }
            catch (Exception ex)
            {
                this.HandlerError(ex);
            }
        }

        private IWebElement GetIsPublicCheckbox()
        {
            WebDriverWait wait = new WebDriverWait(this.Driver, TimeSpan.FromSeconds(15));
            var table = wait.Until(ExpectedConditions.ElementIsVisible(By.Id(Strings.Id.UpdaterPackagesTable)));

            var row = table.FindElements(By.TagName("tr")).FirstOrDefault(x => x.Text.Contains(DefaultToolkitNames.UpdaterInternalName));
            if (row == null)
            {
                Assert.Fail("There is no row for the default updater in the system. Cannot set public");
            }

            var checkBox = row.FindElements(By.TagName("input")).FirstOrDefault(x => x.GetAttribute("name") == @Strings.Name.ToggleIsPublic);

            if (checkBox == null)
            {
                Assert.Fail("Failed to find checkbox. Cannot set public");
            }

            return checkBox;
        }

    }

}