﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Castle.Components.DictionaryAdapter;
using DataTables.AspNet.Core;
using DotNetLittleHelpers;
using Newtonsoft.Json;
using Telimena.WebApp.Core.DTO;
using Telimena.WebApp.Core.DTO.MappableToClient;
using Telimena.WebApp.Core.Models;
using Telimena.WebApp.Core.Models.Portal;
using Telimena.WebApp.Core.Models.Telemetry;
using Telimena.WebApp.Infrastructure.Database;
using Telimena.WebApp.Infrastructure.Repository.Implementation;
using Telimena.WebApp.Utils;

namespace Telimena.WebApp.Infrastructure.Repository
{
    #region Using

    #endregion

    public class ProgramsDashboardUnitOfWork : IProgramsDashboardUnitOfWork
    {
        private class OrderingMethodFinder : ExpressionVisitor
        {
            private bool _orderingMethodFound;

            public static bool OrderMethodExists(Expression expression)
            {
                OrderingMethodFinder visitor = new OrderingMethodFinder();
                visitor.Visit(expression);
                return visitor._orderingMethodFound;
            }

            protected override Expression VisitMethodCall(MethodCallExpression node)
            {
                string name = node.Method.Name;

                if (node.Method.DeclaringType == typeof(Queryable) &&
                    (name.StartsWith("OrderBy", StringComparison.Ordinal) || name.StartsWith("ThenBy", StringComparison.Ordinal)))
                {
                    this._orderingMethodFound = true;
                }

                return base.VisitMethodCall(node);
            }
        }

        public ProgramsDashboardUnitOfWork(TelimenaPortalContext portalContext, TelimenaTelemetryContext telemetryContext)
        {
            this.portalContext = portalContext;
            this.telemetryContext = telemetryContext;
            this.Programs = new ProgramRepository(this.portalContext);
            this.Views = new ViewRepository(this.telemetryContext);
            this.Events = new Repository<Event>(this.telemetryContext);
            this.UpdatePackages = new UpdatePackageRepository(this.portalContext, null);
            this.Users = new Repository<TelimenaUser>(this.portalContext);
        }

        public IUpdatePackageRepository UpdatePackages { get; set; }

        private readonly TelimenaPortalContext portalContext;
        private readonly TelimenaTelemetryContext telemetryContext;
        public IRepository<View> Views { get; }
        public IRepository<Event> Events { get; }

        public IRepository<TelimenaUser> Users { get; }
        public IProgramRepository Programs { get; }


        public async Task<IEnumerable<ProgramSummary>> GetProgramSummary(List<Program> programs)
        {
            List<ProgramSummary> returnData = new List<ProgramSummary>();

            foreach (Program program in programs)
            {

                ProgramSummary summary;
                try
                {
                    List<View> views = await this.Views.FindAsync(x => x.ProgramId == program.Id).ConfigureAwait(false);
                    List<ViewTelemetrySummary> viewSummaries = views.SelectMany(x => x.TelemetrySummaries).ToList();
                    List<Event> events = await this.Events.FindAsync(x => x.ProgramId == program.Id).ConfigureAwait(false);
                    List<EventTelemetrySummary> eventSummaries = events.SelectMany(x => x.TelemetrySummaries).ToList();

                    List<TelemetrySummary> allSummaries = viewSummaries.Cast<TelemetrySummary>().Concat(eventSummaries).ToList();
                    ProgramUpdatePackageInfo latestPkg = await this.UpdatePackages.GetLatestPackage(program.Id).ConfigureAwait(false);
                    summary = new ProgramSummary
                    {
                        ProgramName = program.Name
                        ,DeveloperName = program.DeveloperTeam?.Name ?? "N/A"
                        ,TelemetryKey = program.TelemetryKey
                        ,RegisteredDate = program.RegisteredDate
                        ,UsersCount = allSummaries.DistinctBy(x => x.ClientAppUserId).Count()
                        ,LastUpdateDate = latestPkg?.UploadedDate
                        ,LatestVersion = latestPkg?.SupportedToolkitVersion??"?"
                        ,ToolkitVersion = latestPkg?.SupportedToolkitVersion??"?"
                    };
                }
                catch (Exception)
                {
                    summary = new ProgramSummary();
                    summary.ProgramName = program?.Name ?? "Error while loading summary";
                    summary.DeveloperName = "Error while loading summary";
                }

                returnData.Add(summary);
            }

            return returnData;
        }

        public async Task<DataTable> GetDailyActivityScore(List<Program> programs, DateTime startDate, DateTime endDate)
        {
            List<TelemetryRootObject> telemetryRootObjects = await this.GetTelemetryRootObjects(programs).ConfigureAwait(false);
            IEnumerable<int> programIds = telemetryRootObjects.Select(x => x.ProgramId);

            (List<View> views, List<ViewTelemetrySummary> summaries) viewData = await this.GetViewsAndSummaries(telemetryRootObjects).ConfigureAwait(false); ;
            (List<Event> events, List<EventTelemetrySummary> summaries) eventData = await this.GetEventsAndSummaries(telemetryRootObjects).ConfigureAwait(false);

            List<ExceptionInfo> errorData = await this.telemetryContext.Exceptions.Where(x =>
                    programIds.Contains(x.ProgramId) && x.Timestamp >= startDate.Date &&
                    x.Timestamp <= endDate.Date)
                .ToListAsync().ConfigureAwait(false);

            return PrepareDailyActivityScoreTable(startDate, endDate, eventData.summaries, viewData.summaries, errorData);
        }

        public async Task<DataTable> GetVersionDistribution(List<Program> programs, DateTime startDate, DateTime endDate)
        {
            List<TelemetryRootObject> telemetryRootObjects = await this.GetTelemetryRootObjects(programs).ConfigureAwait(false);
            IEnumerable<int> programIds = programs.Select(x => x.Id);

            List<Event> events = telemetryRootObjects.SelectMany(x => x.Events.Where(ev=>ev.Name == "TelimenaSessionStarted")).ToList();
            IEnumerable<Guid> ids = events.Select(x => x.Id);
            List<EventTelemetrySummary> telemetrySummaries = await this.telemetryContext.EventTelemetrySummaries
                .Where(usg => ids.Contains(usg.EventId)).ToListAsync().ConfigureAwait(false);

            List<EventTelemetryDetail> details = telemetrySummaries.SelectMany(x =>
                x.TelemetryDetails.Where(d => d.Timestamp >= startDate && d.Timestamp <= endDate)).ToList();

            List<IGrouping<string, EventTelemetryDetail>> grouped = details.GroupBy(x => x.FileVersion).ToList();

            DataTable data = new DataTable();

            data.Columns.Add("Version");
            data.Columns.Add("Count", typeof(int));
            foreach (IGrouping<string, EventTelemetryDetail> eventTelemetryDetails in grouped)
            {
                DataRow row = data.NewRow();
                row["Version"] = eventTelemetryDetails.Key;
                row["Count"] = eventTelemetryDetails.Count();
                
                data.Rows.Add(row);
            }

            return data;
        }

        public async Task<DataTable> GetDailyUsersCount(List<Program> programs, DateTime startDate, DateTime endDate)
        {
            List<TelemetryRootObject> telemetryRootObjects = await this.GetTelemetryRootObjects(programs).ConfigureAwait(false);

            (List<Event> events, List<EventTelemetrySummary> summaries) eventData = await this.GetEventsAndSummaries(telemetryRootObjects).ConfigureAwait(false);

            List<EventTelemetryDetail> details = eventData.summaries.SelectMany(x => x.TelemetryDetails).ToList();

            DataTable data = new DataTable();
            data.Columns.Add("Date");
            data.Columns.Add("Users", typeof(int));

            DateTime dateRecord = startDate;

            while (dateRecord.Date <= endDate.Date) //include empty days
            {
                DataRow row = data.NewRow();
                row["Date"] = ToDashboardDateString(dateRecord);
                var detailsForDay = details.Where(detail => detail.Timestamp.Date == dateRecord.Date).ToList();
                var distinct = detailsForDay.DistinctBy(x => x.TelemetrySummary.ClientAppUserId).ToList();
                row["Users"] = details.Where(detail=>detail.Timestamp.Date == dateRecord.Date).DistinctBy(x => x.TelemetrySummary.ClientAppUserId).Count();
                data.Rows.Add(row);
                dateRecord = dateRecord.AddDays(1);
            }

            return data;


        }

        public async Task<Dictionary<string, int>> GetEventNames(Program program)
        {
            List<TelemetryRootObject> telemetryRootObjects = await this.GetTelemetryRootObjects(new List<Program>{program}).ConfigureAwait(false);
            List<Event> events = telemetryRootObjects.SelectMany(x => x.Events).ToList();

            return events.ToDictionary(x => x.Name, x => x.TelemetrySummaries.Sum(s => s.SummaryCount)).OrderByDescending(x => x.Value).ToDictionary(x => x.Key, x => x.Value);
        }

        public async Task<Dictionary<string, int>> GetViewNames(Program program)
        {
            List<TelemetryRootObject> telemetryRootObjects = await this.GetTelemetryRootObjects(new List<Program> { program }).ConfigureAwait(false);
            List<View> views = telemetryRootObjects.SelectMany(x => x.Views).ToList();

            return views.ToDictionary(x => x.Name, x => x.TelemetrySummaries.Sum(s => s.SummaryCount)).OrderByDescending(x => x.Value).ToDictionary(x => x.Key, x => x.Value);
        }

        internal static DataTable PrepareDailyActivityScoreTable(DateTime startDate, DateTime endDate
            , List<EventTelemetrySummary> eventDataSummaries, List<ViewTelemetrySummary> viewDataSummaries, List<ExceptionInfo> errorData)
        {
            DataTable data = new DataTable();

            data.Columns.Add("Date");
            data.Columns.Add("Events", typeof(int));
            data.Columns.Add("Views", typeof(int));
            data.Columns.Add("Errors", typeof(int));
            
            DateTime dateRecord = startDate;

            while (dateRecord.Date <= endDate.Date) //include empty days
            {
                DataRow row = data.NewRow();
                row["Date"] = ToDashboardDateString(dateRecord);

                row["Events"] =
                    eventDataSummaries?.Sum(s => s.TelemetryDetails?.Count(d => d.Timestamp.Date == dateRecord.Date));
                row["Views"] =
                    viewDataSummaries?.Sum(x => x.TelemetryDetails?.Count(d => d.Timestamp.Date == dateRecord.Date));

                row["Errors"] = errorData?.Count(x => x.Timestamp.Date == dateRecord.Date);

                data.Rows.Add(row);
                dateRecord = dateRecord.AddDays(1);
            }

            return data;
        }

        internal static string ToDashboardDateString(DateTime date)
        {
            return date.ToString("ddd, dd.MM", new CultureInfo("en-US"));
        }
        

        public async Task<IEnumerable<ProgramUsageSummary>> GetProgramUsagesSummary(List<Program> programs)
        {
            List<ProgramUsageSummary> returnData = new List<ProgramUsageSummary>();

            foreach (Program program in programs)
            {
                
                ProgramUsageSummary usageSummary;
                try
                {
                    List<View> views = await this.Views.FindAsync(x => x.ProgramId == program.Id).ConfigureAwait(false);
                    List<ViewTelemetrySummary> viewSummaries = views.SelectMany(x => x.TelemetrySummaries).ToList();
                    List<Event> events = await this.Events.FindAsync(x => x.ProgramId == program.Id).ConfigureAwait(false);
                    List<EventTelemetrySummary> eventSummaries = events.SelectMany(x => x.TelemetrySummaries).ToList();

                    List<TelemetrySummary> allSummaries = viewSummaries.Cast<TelemetrySummary>().Concat(eventSummaries).ToList();

                    usageSummary = new ProgramUsageSummary
                    {
                        ProgramName = program.Name
                        , LastUsage = allSummaries.MaxOrNull(x => x.LastTelemetryUpdateTimestamp)
                        , TodayUsageCount = allSummaries.Where(x => (DateTime.UtcNow - x.LastTelemetryUpdateTimestamp).TotalHours <= 24).Sum(smr =>
                                smr.GetTelemetryDetails().Count(detail => (DateTime.UtcNow - detail.Timestamp).TotalHours <= 24))
                        , TotalUsageCount = allSummaries.Sum(x => x.SummaryCount)
                        , ViewsCount = views.Count
                        , TotalViewsUsageCount = viewSummaries.Sum(s => s.SummaryCount)
                        , TotalTodayViewsUsageCount = viewSummaries.Where(x => (DateTime.UtcNow - x.LastTelemetryUpdateTimestamp).TotalHours <= 24).Sum(smr =>
                                smr.TelemetryDetails.Count(detail => (DateTime.UtcNow - detail.Timestamp).TotalHours <= 24))
                        , EventsCount = events.Count
                        , TotalEventsUsageCount = eventSummaries.Sum(x=>x.SummaryCount)
                        , TotalTodayEventsUsageCount = eventSummaries.Where(x => (DateTime.UtcNow - x.LastTelemetryUpdateTimestamp).TotalHours <= 24).Sum(smr =>
                            smr.TelemetryDetails.Count(detail => (DateTime.UtcNow - detail.Timestamp).TotalHours <= 24))
                        
                    };
                }
                catch (Exception)
                {
                    usageSummary = new ProgramUsageSummary();
                    usageSummary.ProgramName = program?.Name ?? "Error while loading summary";
                }

                returnData.Add(usageSummary);
            }

            return returnData;
        }

        public async Task<PortalSummaryData> GetPortalSummary()
        {
            PortalSummaryData summary = new PortalSummaryData
            {
                TotalUsersCount = await this.portalContext.Users.CountAsync().ConfigureAwait(false)
                , NewestUser = await this.portalContext.Users.OrderByDescending(x => x.UserNumber).FirstAsync().ConfigureAwait(false)
                , LastActiveUser = await this.portalContext.Users.OrderByDescending(x => x.LastLoginDate).FirstAsync().ConfigureAwait(false)
                , UsersActiveInLast24Hrs = await this.portalContext.Users.CountAsync(x =>
                    x.LastLoginDate != null && DbFunctions.DiffDays(DateTime.UtcNow, x.LastLoginDate.Value) < 1).ConfigureAwait(false)
            };
            return summary;
        }

        public async Task<List<AppUsersSummaryData>> GetAppUsersSummary(List<Program> programs, DateTime? startDate, DateTime? endDate)
        {
            List<TelemetryRootObject> telemetryRootObjects = await this.GetTelemetryRootObjects(programs).ConfigureAwait(false);
            (List<View> views, List<ViewTelemetrySummary> summaries) viewData = await this.GetViewsAndSummaries(telemetryRootObjects).ConfigureAwait(false); ;
            (List<Event> events, List<EventTelemetrySummary> summaries) eventData = await this.GetEventsAndSummaries(telemetryRootObjects).ConfigureAwait(false);

            List<TelemetrySummary> allData = viewData.summaries.Cast<TelemetrySummary>().Concat(eventData.summaries).ToList();
            List<ClientAppUser> allUsers = GetClientAppUsers(viewData.summaries, eventData.summaries);

           

            List<AppUsersSummaryData> list = new List<AppUsersSummaryData>();
            foreach (ClientAppUser clientAppUser in allUsers)
            {
                List<TelemetrySummary> userData = allData.Where(x => x.ClientAppUserId == clientAppUser.Id).ToList();
              
                if (startDate != null && endDate != null)
                 {
                     if (userData.All(summary =>
                         summary.GetTelemetryDetails().All(detail =>
                             detail.Timestamp < startDate || detail.Timestamp > endDate)))
                     {
                         continue; //this user was not active in our timeframe
                     }
                }

                list.Add(new AppUsersSummaryData()
                {
                    FirstSeenDate = clientAppUser.FirstSeenDate,
                    UserName = clientAppUser.UserIdentifier,
                    LastActiveDate = userData.OrderByDescending(x=>x.LastTelemetryUpdateTimestamp).FirstOrDefault()?.LastTelemetryUpdateTimestamp??new DateTimeOffset(),
                    ActivityScore = userData.Sum(x =>x.SummaryCount),
                    NumberOfApps = userData.DistinctBy(x=>x.GetComponent().Program.ProgramId).Count()

                });
            }

            return list;
        }

        private async Task<List<TelemetryRootObject>> GetTelemetryRootObjects(List<Program> programs)
        {
            List<TelemetryRootObject> telemetryRootObjects = new List<TelemetryRootObject>();
            foreach (Program program in programs)
            {
                TelemetryRootObject telePrg = await this.telemetryContext.TelemetryRootObjects
                    .FirstOrDefaultAsync(x => x.ProgramId == program.Id).ConfigureAwait(false);
                if (telePrg != null)
                {
                    telemetryRootObjects.Add(telePrg);
                }
            }

            return telemetryRootObjects;
        }

        public async Task<AllProgramsSummaryData> GetAllProgramsSummaryCounts(List<Program> programs)
        {
            List<TelemetryRootObject> telemetryRootObjects = await this.GetTelemetryRootObjects(programs).ConfigureAwait(false);

            (List<View> views, List<ViewTelemetrySummary> summaries) viewData = await this.GetViewsAndSummaries(telemetryRootObjects).ConfigureAwait(false);
            (List<Event> events, List<EventTelemetrySummary> summaries) eventData = await this.GetEventsAndSummaries(telemetryRootObjects).ConfigureAwait(false);

            List<ClientAppUser> allUsers = GetClientAppUsers(viewData.summaries, eventData.summaries);

            IEnumerable<int> programIds = telemetryRootObjects.Select(x => x.ProgramId);
            List<ExceptionInfo> exceptions =
                await this.telemetryContext.Exceptions.Where(ex=> programIds.Contains(ex.ProgramId)).ToListAsync().ConfigureAwait(false);
            List<ExceptionInfo> recentExceptions = exceptions.Where(x => (DateTime.UtcNow - x.Timestamp).TotalDays <= 7).ToList();
                string mostPopularException = recentExceptions.GroupBy(x => x.TypeName).OrderByDescending(x => x.Count()).FirstOrDefault()?.Key;

            AllProgramsSummaryData summary = new AllProgramsSummaryData
            {
                TotalProgramsCount = programs.Count()
                , TotalAppUsersCount = allUsers.Count()
                , AppUsersRegisteredLast7DaysCount = allUsers.Count(x => (DateTime.UtcNow - x.FirstSeenDate).TotalDays <= 7)
                , TotalViewsCount = viewData.views.Count()
                , TotalViewsUsageCount = viewData.summaries.Sum(x=>x.SummaryCount)
                , TotalEventUsageCount = eventData.summaries.Sum(x=>x.SummaryCount)
                , TotalEventsCount = eventData.events.Count()
                , TotalExceptionsInLast7Days = recentExceptions.Count
                , MostPopularExceptionInLast7Days = mostPopularException
            };

            summary.NewestProgram = programs.OrderByDescending(x => x.Id) /*.Include(x=>x.Developer)*/.FirstOrDefault();

            return summary;
        }

        private static List<ClientAppUser> GetClientAppUsers(List<ViewTelemetrySummary> viewSummaries, List<EventTelemetrySummary> eventSummaries)
        {
            List<ClientAppUser> viewUsers =
                viewSummaries.DistinctBy(x => x.ClientAppUserId).Select(x => x.ClientAppUser).ToList();
            List<ClientAppUser> eventUsers =
                eventSummaries.DistinctBy(x => x.ClientAppUserId).Select(x => x.ClientAppUser).ToList();
            List<ClientAppUser> allUsers = viewUsers.Concat(eventUsers).DistinctBy(x => x.Id).ToList();
            return allUsers;
        }

        private async Task<(List<View> views, List<ViewTelemetrySummary> summaries)> GetViewsAndSummaries(List<TelemetryRootObject> telemetryRootObjects)
        {
            List<View> views = telemetryRootObjects.SelectMany(x => x.Views).ToList();
            IEnumerable<Guid> viewIds = views.Select(x => x.Id);
            List<ViewTelemetrySummary> viewTelemetrySummaries = await this.telemetryContext.ViewTelemetrySummaries
                .Where(usg => viewIds.Contains(usg.ViewId)).ToListAsync().ConfigureAwait(false);
            return (views, viewTelemetrySummaries);
        }

        private async Task<(List<Event> events, List<EventTelemetrySummary> summaries)> GetEventsAndSummaries(List<TelemetryRootObject> telemetryRootObjects)
        {
            List<Event> events = telemetryRootObjects.SelectMany(x => x.Events).ToList();
            IEnumerable<Guid> ids = events.Select(x => x.Id);
            List<EventTelemetrySummary> telemetrySummaries = await this.telemetryContext.EventTelemetrySummaries
                .Where(usg => ids.Contains(usg.EventId)).ToListAsync().ConfigureAwait(false);
            return (events, telemetrySummaries);
        }

        public async Task CompleteAsync()
        {
            await this.portalContext.SaveChangesAsync().ConfigureAwait(false);
        }

        public async Task<UsageDataTableResult> GetExceptions(Guid telemetryKey, TelemetryItemTypes itemType, int skip
            , int take, IEnumerable<Tuple<string, bool>> sortBy , ISearch requestSearch 
            , List<string> searchableColumns)
        {
            Program program = await this.portalContext.Programs.FirstOrDefaultAsync(x => x.TelemetryKey == telemetryKey).ConfigureAwait(false);

            if (program == null)
            {
                throw new ArgumentException($"Program with key {telemetryKey} does not exist");
            }

            IQueryable<ExceptionInfo> query = this.telemetryContext.Exceptions.Where(x => x.ProgramId == program.Id);
            int totalCount = await this.telemetryContext.Exceptions.CountAsync(x => x.ProgramId == program.Id).ConfigureAwait(false);

            if (take == -1)
            {
                take = totalCount;
            }



            IQueryable<ExceptionInfo> filteredQuery = EntityFilter.Match(query
                , property => property.Contains(requestSearch.Value)
                , new List<Expression<Func<ExceptionInfo, string>>>()
                {
                    {x=>x.Sequence },
                    {x=>x.Message },
                    { x=>x.TypeName},
                    { x=>x.ProgramVersion},
                    {x=>x.ParsedStack},
                    {x=>x. UserName},
                });


           List<ExceptionInfo> ordered = await ApplyOrderingQuery(sortBy, filteredQuery, skip, take).ConfigureAwait(false);

            List<ExceptionData> result = new List<ExceptionData>();
            foreach (ExceptionInfo exception in ordered)
            {
                ExceptionData data = new ExceptionData
                {
                    Timestamp = exception.Timestamp
                    ,UserName = exception.UserName
                    ,EntryKey = exception.TypeName
                    ,Note = exception.Note
                    ,ProgramVersion = exception.ProgramVersion
                    ,Sequence = exception.Sequence
                    ,ErrorMessage = exception.Message
                    ,StackTrace = GetStackTrace(exception.ParsedStack)
                    ,Properties = exception.TelemetryUnits.Where(x=>x.UnitType == TelemetryUnit.UnitTypes.Property).ToDictionary(x => x.Key, x => x.ValueString)
                    ,Metrics = exception.TelemetryUnits.Where(x => x.UnitType == TelemetryUnit.UnitTypes.Metric).ToDictionary(x => x.Key, x => x.ValueDouble)
                };
                result.Add(data);
            }

            return new UsageDataTableResult { TotalCount = totalCount, FilteredCount = totalCount, UsageData = result };
        }


        public async Task<UsageDataTableResult> GetLogs(Guid telemetryKey, int skip, int take, IEnumerable<Tuple<string, bool>> sortBy, string searchPhrase)
        {
            Program program = await this.portalContext.Programs.FirstOrDefaultAsync(x => x.TelemetryKey == telemetryKey).ConfigureAwait(false);

            if (program == null)
            {
                throw new ArgumentException($"Program with key {telemetryKey} does not exist");
            }

            IQueryable<LogMessage> query = this.telemetryContext.LogMessages.Where(x => x.ProgramId == program.Id);
            int totalCount = await this.telemetryContext.LogMessages.CountAsync(x => x.ProgramId == program.Id).ConfigureAwait(false);

            if (take == -1)
            {
                take = totalCount;
            }



            IQueryable<LogMessage> filteredQuery = EntityFilter.Match(query
                , property => property.Contains(searchPhrase)
                , new List<Expression<Func<LogMessage, string>>>()
                {
                    {x=>x.Sequence },
                    {x=>x.Message },
                    { x=>x.ProgramVersion},
                    {x=>x. UserName},
                });


            List<LogMessage> ordered = await ApplyOrderingQuery(sortBy, filteredQuery, skip, take).ConfigureAwait(false);

            List<LogMessageData> result = new List<LogMessageData>();
            foreach (LogMessage item in ordered)
            {
                LogMessageData data = new LogMessageData
                {
                    Timestamp = item.Timestamp,
                    UserName = item.UserName,
                    Message= item.Message
                    ,ProgramVersion = item.ProgramVersion
                    ,Sequence = item.Sequence
                    ,LogLevel= item.Level.ToString()
                };
                result.Add(data);
            }

            return new UsageDataTableResult { TotalCount = totalCount, FilteredCount = totalCount, UsageData = result };
        }

        public async Task<UsageDataTableResult> GetSequenceHistory(Guid telemetryKey, string sequenceId, string searchValue)
        {

            Program program = await this.portalContext.Programs.FirstOrDefaultAsync(x => x.TelemetryKey == telemetryKey).ConfigureAwait(false);

            if (program == null)
            {
                throw new ArgumentException($"Program with key {telemetryKey} does not exist");
            }

           string sequencePrefix = SequenceIdParser.GetPrefix(sequenceId);
           if (string.IsNullOrEmpty(sequencePrefix))
           {
               return new UsageDataTableResult { TotalCount = 0, FilteredCount = 0, UsageData = new List<DataTableTelemetryData>()};
            }
            IEnumerable<TelemetryDetail> viewQuery = (await this.telemetryContext.ViewTelemetryDetails.Where(x => x.Sequence.StartsWith(sequencePrefix)).ToListAsync().ConfigureAwait(false)).Cast<TelemetryDetail>();
           IEnumerable<TelemetryDetail> eventQuery = (await this.telemetryContext.EventTelemetryDetails.Where(x => x.Sequence.StartsWith(sequencePrefix)).ToListAsync().ConfigureAwait(false)).Cast<TelemetryDetail>();
           List<LogMessage> logsQuery = (await this.telemetryContext.LogMessages.Where(x =>x.ProgramId == program.Id && x.Sequence.StartsWith(sequencePrefix)).ToListAsync().ConfigureAwait(false));
           List<ExceptionInfo> exceptionsQuery = (await this.telemetryContext.Exceptions.Where(x => x.ProgramId == program.Id && x.Sequence.StartsWith(sequencePrefix)).ToListAsync().ConfigureAwait(false));


            List<object> allResults =  viewQuery.Concat(eventQuery).Cast<object>().ToList();
            allResults.AddRange(logsQuery);
            allResults.AddRange(exceptionsQuery);

            int totalCount = allResults.Count;
            
            List<SequenceHistoryData> result = new List<SequenceHistoryData>();
            foreach (object detail in allResults)
            {

                SequenceHistoryData data = this.BuildSequenceItem(detail);
                result.Add(data);
              
            }

            result = result.OrderByDescending(x => x.Order).ToList();

            return new UsageDataTableResult { TotalCount = totalCount, FilteredCount = totalCount, UsageData = result };
        }


        private SequenceHistoryData BuildSequenceItem(object item)
        {
            SequenceHistoryData data= null;
            if (item is TelemetryDetail telemetryDetail)
            {
                data= new SequenceHistoryData(telemetryDetail);
                data.Order = SequenceIdParser.GetOrder(telemetryDetail.Sequence);
            }
            else if (item is LogMessage logMessage)
            {
                data = new SequenceHistoryData(logMessage);
                data.Order = SequenceIdParser.GetOrder(logMessage.Sequence);

            }
            else if (item is ExceptionInfo error)
            {
                data = new SequenceHistoryData(error);
                data.Order = SequenceIdParser.GetOrder(error.Sequence);

            }

            return data;
        }

        private List<TelemetryItem.ExceptionInfo.ParsedStackTrace> GetStackTrace(string input)
        {
            try
            {
                return JsonConvert.DeserializeObject<List<TelemetryItem.ExceptionInfo.ParsedStackTrace>>(input);
            }
            catch
            {
                //failsafe approach
                return new List<TelemetryItem.ExceptionInfo.ParsedStackTrace>(){new TelemetryItem.ExceptionInfo.ParsedStackTrace(){Method = input}};
            }
        }

        public async Task<UsageDataTableResult> GetProgramUsageData(Guid telemetryKey, TelemetryItemTypes itemType
            , int skip, int take, IEnumerable<Tuple<string, bool>> sortBy, ISearch requestSearch 
            , List<string> searchableColumns)
        {

            Program program = await this.portalContext.Programs.FirstOrDefaultAsync(x => x.TelemetryKey == telemetryKey).ConfigureAwait(false);

            if (program == null)
            {
                throw new ArgumentException($"Program with key {telemetryKey} does not exist");
            }

            IQueryable<TelemetryDetail> query;
            int totalCount;
            if (itemType == TelemetryItemTypes.View)
            {
                 query = this.telemetryContext.ViewTelemetryDetails.Where(x => x.TelemetrySummary.View.ProgramId == program.Id);
                 totalCount = await this.telemetryContext.ViewTelemetryDetails.CountAsync(x => x.TelemetrySummary.View.ProgramId == program.Id).ConfigureAwait(false);

            }
            else
            {
                 query = this.telemetryContext.EventTelemetryDetails.Where(x => x.TelemetrySummary.Event.ProgramId == program.Id && !string.IsNullOrEmpty(x.TelemetrySummary.Event.Name)); //todo remove this empty string check after dealing with heartbeat data
                 totalCount = await this.telemetryContext.EventTelemetryDetails.CountAsync(x => x.TelemetrySummary.Event.ProgramId == program.Id && !string.IsNullOrEmpty(x.TelemetrySummary.Event.Name)).ConfigureAwait(false);
            }



            IQueryable<TelemetryDetail> filteredQuery = EntityFilter.Match(query
                , property => property.Contains(requestSearch.Value)
                , new List<Expression<Func<TelemetryDetail, string>>>()
                {
                    {x=>x.Sequence },
                    {x=>x.IpAddress },
                    {x=>x.UserIdentifier },
                    {x=>x.EntryKey },
                });


            if (take == -1)
            {
                take = totalCount;
            }

            List<TelemetryDetail> usages = await ApplyOrderingQuery(sortBy, filteredQuery, skip, take).ConfigureAwait(false);

            List<DataTableTelemetryData> result = new List<DataTableTelemetryData>();
            foreach (TelemetryDetail detail in usages)
            {
                DataTableTelemetryData data = new DataTableTelemetryData()
                {
                    Timestamp = detail.Timestamp
                    , UserName = detail.UserIdentifier
                    , IpAddress = detail.IpAddress
                    , EntryKey = detail.EntryKey
                    , ProgramVersion = detail.FileVersion
                    , Sequence = detail.Sequence
                    ,Properties = detail.GetTelemetryUnits().Where(x => x.UnitType == TelemetryUnit.UnitTypes.Property).ToDictionary(x => x.Key,x=> x.ValueString)
                    ,Metrics = detail.GetTelemetryUnits().Where(x => x.UnitType == TelemetryUnit.UnitTypes.Metric).ToDictionary(x => x.Key,x=> x.ValueDouble)
                };
                result.Add(data);
            }

            return new UsageDataTableResult {TotalCount = totalCount, FilteredCount = totalCount, UsageData = result};
        }

        public async Task<TelemetryInfoTable> GetPivotTableData(TelemetryItemTypes type, Guid telemetryKey)
        {
            Program program = await this.portalContext.Programs.FirstOrDefaultAsync(x => x.TelemetryKey == telemetryKey).ConfigureAwait(false);

            if (program == null)
            {
                throw new ArgumentException($"Program with key {telemetryKey} does not exist");
            }

            List<TelemetryDetail> details = program.GetTelemetryDetails(this.telemetryContext, type).ToList();
            List<TelemetryPivotTableRow> rows = new List<TelemetryPivotTableRow>();

            foreach (TelemetryDetail detail in details)
            {
                List<TelemetryUnit> units = detail.GetTelemetryUnits().ToList();
                if (units.Any())
                {
                    foreach (TelemetryUnit detailTelemetryUnit in units.Where(x=>x.UnitType == TelemetryUnit.UnitTypes.Property))
                    {
                        TelemetryPivotTableRow row = new TelemetryPivotTableRow(detail)
                        {
                            PropertyValue = detailTelemetryUnit.ValueString,
                            Key = detailTelemetryUnit.Key,
                        };
                        rows.Add(row);
                    }
                    foreach (TelemetryUnit detailTelemetryUnit in units.Where(x => x.UnitType == TelemetryUnit.UnitTypes.Metric))
                    {
                        TelemetryPivotTableRow row = new TelemetryPivotTableRow(detail)
                        {
                            MetricValue = detailTelemetryUnit.ValueDouble,
                            Key = detailTelemetryUnit.Key,
                        };
                        rows.Add(row);
                    }
                }
                else
                {
                    TelemetryPivotTableRow row = new TelemetryPivotTableRow(detail);
                    rows.Add(row);
                }

                
            }

            TelemetryInfoTableHeader header = new TelemetryInfoTableHeader();
            return new TelemetryInfoTable {Header = header, Rows = rows};
        }

        internal static async Task<List<T>> ApplyOrderingQuery<T>(IEnumerable<Tuple<string, bool>> sortBy, IQueryable<T> query, int skip, int take)
            where T : TelemetryDetail
        {
            List<Tuple<string, bool>> rules = sortBy.ToList();
            try
            {
                for (int index = 0; index < rules.Count; index++)
                {
                    Tuple<string, bool> rule = rules[index];
                    if (rule.Item1 == nameof(DataTableTelemetryData.Timestamp))
                    {
                        query = Order(query, rule.Item1, rule.Item2, index);
                    }
                    else if (rule.Item1 == nameof(DataTableTelemetryData.ProgramVersion))
                    {
                        query = Order(query, x => x.FileVersion, rule.Item2, index);
                    }
                    else if (rule.Item1 == nameof(DataTableTelemetryData.UserName))
                    {
                        query = Order(query, x => x.UserIdentifier, rule.Item2, index);
                    }
                    else if (rule.Item1 == nameof(DataTableTelemetryData.EntryKey) )
                    {
                        query = Order(query, x=> x.EntryKey, rule.Item2, index);
                    }
                }

                IOrderedQueryable<T> orderedQuery = query as IOrderedQueryable<T>;
                if (!OrderingMethodFinder.OrderMethodExists(orderedQuery.Expression))
                {
                    orderedQuery = query.OrderByDescending(x => x.Timestamp);
                }

                return await orderedQuery.Skip(skip).Take(take).ToListAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
                return await query.OrderByDescending(x => x.Timestamp).Skip(skip).Take(take).ToListAsync().ConfigureAwait(false);
            }
        }

        internal static async Task<List<ExceptionInfo>> ApplyOrderingQuery(IEnumerable<Tuple<string, bool>> sortBy, IQueryable<ExceptionInfo> query, int skip, int take)
        {
            List<Tuple<string, bool>> rules = sortBy.ToList();
            try
            {
                for (int index = 0; index < rules.Count; index++)
                {
                    Tuple<string, bool> rule = rules[index];
                    if (rule.Item1 == nameof(DataTableTelemetryData.Timestamp) || 
                        rule.Item1 == nameof(DataTableTelemetryData.ProgramVersion) || 
                        rule.Item1 == nameof(DataTableTelemetryData.UserName))
                    {
                        query = GenericOrder(query, rule.Item1, rule.Item2, index);
                    }
                    else if (rule.Item1 == nameof(DataTableTelemetryData.EntryKey))
                    {
                        query = GenericOrder(query, nameof(ExceptionInfo.TypeName), rule.Item2, index);
                    }
                }

                IOrderedQueryable<ExceptionInfo> orderedQuery = query as IOrderedQueryable<ExceptionInfo>;
                if (!OrderingMethodFinder.OrderMethodExists(orderedQuery.Expression))
                {
                    orderedQuery = query.OrderByDescending(x => x.Timestamp);
                }

                return await orderedQuery.Skip(skip).Take(take).ToListAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
                return await query.OrderByDescending(x => x.Timestamp).Skip(skip).Take(take).ToListAsync().ConfigureAwait(false);
            }
        }


        internal static async Task<List<LogMessage>> ApplyOrderingQuery(IEnumerable<Tuple<string, bool>> sortBy, IQueryable<LogMessage> query, int skip, int take)
        {
            List<Tuple<string, bool>> rules = sortBy.ToList();
            try
            {
                for (int index = 0; index < rules.Count; index++)
                {
                    Tuple<string, bool> rule = rules[index];
                    if (rule.Item1 == nameof(DataTableTelemetryData.Timestamp) ||
                        rule.Item1 == nameof(DataTableTelemetryData.ProgramVersion) ||
                        rule.Item1 == nameof(DataTableTelemetryData.UserName))
                    {
                        query = GenericOrder(query, rule.Item1, rule.Item2, index);
                    }
                    else if (rule.Item1 == nameof(DataTableTelemetryData.EntryKey))
                    {
                        query = GenericOrder(query, nameof(ExceptionInfo.TypeName), rule.Item2, index);
                    }
                }

                IOrderedQueryable<LogMessage> orderedQuery = query as IOrderedQueryable<LogMessage>;
                if (!OrderingMethodFinder.OrderMethodExists(orderedQuery.Expression))
                {
                    orderedQuery = query.OrderByDescending(x => x.Timestamp);
                }

                return await orderedQuery.Skip(skip).Take(take).ToListAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
                return await query.OrderByDescending(x => x.Timestamp).Skip(skip).Take(take).ToListAsync().ConfigureAwait(false);
            }
        }

        private static IOrderedQueryable<T> GenericOrder<T>(IQueryable<T> query, string key, bool desc, int index)
        {
            if (index == 0)
            {
                return query.OrderBy(key, desc);
            }

            return (query as IOrderedQueryable<T>).ThenBy(key, desc);
        }



        private static IOrderedQueryable<T> Order<T>(IQueryable<T> query, string key, bool desc, int index) where T : TelemetryDetail
        {
            if (index == 0)
            {
                return query.OrderBy(key, desc);
            }

            return (query as IOrderedQueryable<T>).ThenBy(key, desc);
        }

        private static IOrderedQueryable<T> Order<T>(IQueryable<T> query, Expression<Func<T, string>> key, bool desc, int index) where T : TelemetryDetail
        {
            if (index == 0)
            {
                return query.OrderBy(key, desc);
            }

            if (desc)
            {
                return (query as IOrderedQueryable<T>).ThenByDescending(key);
            }

            return (query as IOrderedQueryable<T>).ThenBy(key);
        }
    }



}