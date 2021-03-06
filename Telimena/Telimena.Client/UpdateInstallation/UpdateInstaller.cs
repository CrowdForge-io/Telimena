﻿using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace TelimenaClient
{
    internal class UpdateInstaller : IInstallUpdates
    {
        public async Task InstallUpdaterUpdate(FileInfo updaterPackage, FileInfo targetPath)
        {
            var wasUnpacked = await UnpackUpdater(updaterPackage, targetPath).ConfigureAwait(false);
            if (wasUnpacked)
            {
                await Cleanup(updaterPackage).ConfigureAwait(false);
            }
        }

        public void InstallUpdates(FileInfo instructionsFile, FileInfo updaterFile)
        {
            this.VerifyFilesExist(instructionsFile, updaterFile);
            Process process = new Process {StartInfo = StartInfoCreator.CreateStartInfo(instructionsFile, updaterFile)};

            process.Start();
        }

        private static async Task Cleanup(FileInfo updaterPackage)
        {
            for (int i = 0; i < 4; i++)
            {
                try
                {
                    updaterPackage.Delete();
                    break;
                }
                catch (Exception ex)
                {
                    await Task.Delay(150 * (i + 1)).ConfigureAwait(false);
                    if (i == 4)
                    {
                        throw new InvalidOperationException("Error occurred while cleaning up the updater package", ex);
                    }
                }
            }
        }

        private static async Task<bool> UnpackUpdater(FileInfo updaterPackage, FileInfo targetPath)
        {

            if (updaterPackage.FullName == targetPath.FullName)
            {
                return false; //not an archive
            }
            for (int i = 0; i < 4; i++)
            {
                try
                {
                    ZipFile.ExtractToDirectory(updaterPackage.FullName, targetPath.DirectoryName);
                    return true;
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains(targetPath.FullName) && targetPath.Exists)
                    {
                        try
                        {
                            targetPath.Delete();
                        }
                        catch (Exception) { }
                    }
                    await Task.Delay(150 * (i + 1)).ConfigureAwait(false);
                    if (i == 4)
                    {
                        throw new InvalidOperationException("Error occurred while extracting the updater package", ex);
                    }
                }
            }

            return false;
        }

        private void VerifyFilesExist(FileInfo instructionsFile, FileInfo updaterFile)
        {
            if (instructionsFile == null || !File.Exists(instructionsFile.FullName))
            {
                throw new FileNotFoundException($"Failed to find instructions file at path: {instructionsFile?.FullName}", instructionsFile?.FullName);
            }

            if (updaterFile == null || !File.Exists(updaterFile.FullName))
            {
                throw new FileNotFoundException($"Failed to find updater executable at path: {updaterFile?.FullName}", updaterFile?.FullName);
            }
        }
    }
}