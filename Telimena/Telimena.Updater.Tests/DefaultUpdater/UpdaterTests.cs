﻿// -----------------------------------------------------------------------
//  <copyright file="ClientTests.cs" company="SDL plc">
//   Copyright (c) SDL plc. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using NUnit.Framework;
using Telimena.Updater;
using TelimenaClient;
using UpdateInstructions = Telimena.Updater.UpdateInstructions;

namespace TelimenaUpdaterTests.DefaultUpdater
{
    #region Using

    #endregion

    [TestFixture]
    public class UpdaterTests
    {
        [DllImport("shell32.dll", SetLastError = true)]
        private static extern IntPtr CommandLineToArgvW([MarshalAs(UnmanagedType.LPWStr)] string lpCmdLine, out int pNumArgs);

        private static string[] CommandLineToArgs(string commandLine)
        {
            int argc;
            IntPtr argv = CommandLineToArgvW(commandLine, out argc);
            if (argv == IntPtr.Zero)
            {
                throw new Win32Exception();
            }

            try
            {
                string[] args = new string[argc];
                for (int i = 0; i < args.Length; i++)
                {
                    IntPtr p = Marshal.ReadIntPtr(argv, i * IntPtr.Size);
                    args[i] = Marshal.PtrToStringUni(p);
                }

                return args;
            }
            finally
            {
                Marshal.FreeHGlobal(argv);
            }
        }

        [Test]
        public void Test_StartInfoParsing()
        {
            FileInfo instructions = new FileInfo(@"C:\An app\Updates\3.2\Instructions.xml");
            FileInfo updater = new FileInfo(@"C:\An app\Updates\Updater.exe");
            ProcessStartInfo startInfo = StartInfoCreator.CreateStartInfo(instructions, updater);

            Assert.AreEqual(@"C:\An app\Updates\Updater.exe", startInfo.FileName);
            UpdaterStartupSettings settings = CommandLineArgumentParser.GetSettings(CommandLineToArgs(startInfo.Arguments));

            Assert.AreEqual(@"C:\An app\Updates\3.2\Instructions.xml", settings.InstructionsFile.FullName);
        }

        //[Apartment(ApartmentState.STA)]
        //[STAThread]
        public void Test_Manual_NoReleaseNotes()
        {
            var instructions = new UpdateInstructions()
            {
                LatestVersion = "9.9.9.2", Packages = new List<UpdateInstructions.PackageData>()
                {
                    new UpdateInstructions.PackageData(){ Version = "9.8"},
                    new UpdateInstructions.PackageData(){ Version = "9.9.9.2"}
                }
            };

            var win = new MainWindow(instructions);

            win.ShowDialog();
        }
    }

}