﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using AutomaticTestsClient;
using DotNetLittleHelpers;
using NUnit.Framework;
using Telimena.WebApp.UITests.Base;
using Telimena.WebApp.UITests.IntegrationTests.TestAppInteraction;
using TelimenaClient;
using TestStack.White.UIItems;
using TestStack.White.UIItems.Finders;
using TestStack.White.UIItems.WindowItems;
using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace Telimena.WebApp.UITests.IntegrationTests.BackwardCompatibilityIntegrationTests
{
    public class VersionTuple
    {
        public string AssemblyVersion { get; set; }
        public string FileVersion { get; set; }
    }



    [TestFixture()]
    public partial class NonUiTests : PortalTestBase
    {
        public async Task<VersionTuple> GetVersionsFromApp(string appName)
        {
            FileInfo exe = TestAppProvider.ExtractApp(appName);
            Process.Start(exe.FullName);
            Window appWarning = await TestHelpers.WaitForWindowAsync(x => x.Title.Equals("AutomaticTestsClient - This app requires arguments to run"), TimeSpan.FromMinutes(2));
            return await this.GetVersionFromMsgBox(appWarning);
        }

        public async Task<VersionTuple> GetVersionFromMsgBox(Window msgBox)
        {
            string text = msgBox.Get<Label>(SearchCriteria.Indexed(0)).Text;
            string[] versions = text.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            msgBox.Get<Button>(SearchCriteria.ByText("OK")).Click();
            await Task.Delay(500);
            return new VersionTuple() { AssemblyVersion = versions[0].Trim().Replace("AssemblyVersion: ",""), FileVersion = versions[1].Trim().Replace("FileVersion: ", "") };
        }

        [Test]
        public async Task HandleUpdatesNonBetaTests()
        {
            VersionTuple initialVersions =await this.GetVersionsFromApp(TestAppProvider.FileNames.TestAppV1);
            
            this.LaunchTestsAppAndGetResult<UpdateCheckResult>(Actions.HandleUpdates, TestAppProvider.FileNames.TestAppV1, waitForExit: false);
            Window updateNowMsgBox = await TestHelpers.WaitForWindowAsync(x=>x.Title.Equals("AutomaticTestsClient update installation"), TimeSpan.FromMinutes(2));
            updateNowMsgBox.Get<Button>(SearchCriteria.ByText("Yes")).Click();

            Window updater = await TestHelpers.WaitForWindowAsync(x => x.Title.Contains("Updater"), TimeSpan.FromMinutes(2));
            updater.Get<Button>(SearchCriteria.ByText("Install now!")).Click();

            Window doneMsg = await TestHelpers.WaitForMessageBoxAsync(updater, "Update complete", TimeSpan.FromMinutes(2));
            doneMsg.Get<Button>(SearchCriteria.ByText("Yes")).Click();

            Window appWarning = await TestHelpers.WaitForWindowAsync(x => x.Title.Equals("AutomaticTestsClient - This app requires arguments to run"), TimeSpan.FromMinutes(2));
            VersionTuple newVersions = await this.GetVersionFromMsgBox(appWarning);

            Assert.IsTrue(newVersions.AssemblyVersion.IsNewerVersionThan(initialVersions.AssemblyVersion));
            Assert.IsTrue(newVersions.FileVersion.IsNewerVersionThan(initialVersions.FileVersion));

        }
    }
}