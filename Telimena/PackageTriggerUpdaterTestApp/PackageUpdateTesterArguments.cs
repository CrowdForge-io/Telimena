﻿using System;
using TelimenaClient;

namespace PackageTriggerUpdaterTestApp
{
    public class PackageUpdateTesterArguments
    {
        public string ApiUrl { get; set; }
        
        public Guid TelemetryKey { get; set; }
    }
}