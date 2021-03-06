﻿using System;
using TelimenaClient.Model;
using TelimenaClient.Model.Internal;

namespace TelimenaClient
{
    /// <summary>
    /// Properties of Telimena client
    /// </summary>
    public interface ITelimenaProperties : IFluentInterface
    {
        /// <summary>
        ///     If true, then Telimena will swallow any errors. Otherwise, it will rethrow
        /// </summary>
        bool SuppressAllErrors { get; set; }

        /// <summary>
        /// Determines whether and when should the user be prompted about an update
        /// </summary>
        /// <value>The update prompting mode.</value>
        UpdatePromptingModes UpdatePromptingMode {get;set; }

        /// <summary>
        ///     The unique key for this program's telemetry service
        /// </summary>
        Guid TelemetryKey { get; }

        /// <summary>
        ///     Gets the user information.
        /// </summary>
        /// <value>The user information.</value>
        UserInfo UserInfo { get; }

        /// <summary>
        ///     Gets the program information from the assemblies.
        /// </summary>
        /// <value>The program information.</value>
        ProgramInfo StaticProgramInfo { get; }

        /// <summary>
        ///     Gets the program version.
        /// </summary>
        /// <value>The program version.</value>
        VersionData ProgramVersion { get; }

        /// <summary>
        ///     Gets the telimena version.
        /// </summary>
        /// <value>The telimena version.</value>
        string TelimenaVersion { get; }

        /// <summary>
        ///     Gets the live program information
        /// </summary>
        /// <value>The live program information.</value>
        LiveProgramInfo LiveProgramInfo { get; }

        /// <summary>
        /// Gets the telimena base URL.
        /// </summary>
        /// <value>The telimena base URL.</value>
        Uri TelemetryApiBaseUrl { get; }

        /// <summary>
        /// Gets or sets the instrumentation key (If AppInsights is used)
        /// </summary>
        /// <value>The instrumentation key.</value>
        string InstrumentationKey { get; }

        /// <summary>
        ///     Data object containing information used at Telimena client startup
        /// </summary>
        ITelimenaStartupInfo StartupInfo { get; }
    }
}