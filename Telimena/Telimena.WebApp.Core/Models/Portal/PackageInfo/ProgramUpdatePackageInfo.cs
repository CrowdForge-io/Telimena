﻿using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Telimena.WebApp.Core.Models.Portal
{
    public class ProgramUpdatePackageInfo : RepositoryFileBase, IRepositoryFile
    {
        protected ProgramUpdatePackageInfo()
        {
        }

        public ProgramUpdatePackageInfo(string fileName, int programId, string version, long fileSizeBytes, string supportedToolkitVersion) : base(fileName
            , fileSizeBytes)
        {
            this.ProgramId = programId;
            this.Version = version;
            this.SupportedToolkitVersion = supportedToolkitVersion;
        }
      
        public int Id { get; set; } 

        [Index(IsUnique = true, IsClustered =  false)]
        public Guid PublicId { get; set; } = Guid.NewGuid();


        public int ProgramId { get; set; }
        public string Version { get; set; }
        public string SupportedToolkitVersion { get; set; }

        public bool IsBeta { get; set; }

        public bool IsStandalone { get; set; }

        public string ReleaseNotes { get; set; }

        public void UpdateContentAndMetadata(bool isBeta, string releaseNotes)
        {
            this.UploadedDate = DateTimeOffset.UtcNow;
            this.ReleaseNotes = releaseNotes;
            this.IsBeta = isBeta;
        }
    }
}