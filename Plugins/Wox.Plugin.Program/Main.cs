using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using Wox.Infrastructure.Logger;
using Wox.Infrastructure.Storage;
using Wox.Plugin.Program.Programs;
using Wox.Plugin.Program.Views;
using Stopwatch = Wox.Infrastructure.Stopwatch;

namespace Wox.Plugin.Program
{
    public class Main : ISettingProvider, IPlugin, IPluginI18n, IContextMenu, ISavable, IReloadable
    {
        private static readonly object IndexLock = new object();
        internal static Win32[] _win32s { get; set; }
        internal static UWP.Application[] _uwps { get; set; }
        internal static Settings _settings { get; set; }

        private static bool IsStartupIndexProgramsRequired => _settings.LastIndexTime.AddDays(3) < DateTime.Today;

        private static PluginInitContext _context;

        private static BinaryStorage<Win32[]> _win32Storage;
        private static BinaryStorage<UWP.Application[]> _uwpStorage;        
        private readonly PluginJsonStorage<Settings> _settingsStorage;

        public Main()
        {
            _settingsStorage = new PluginJsonStorage<Settings>();
            _settings = _settingsStorage.Load();

            var preloadcost = Stopwatch.Normal("|Wox.Plugin.Program.Main|Preload programs cost", () =>
            {
                _win32Storage = new BinaryStorage<Win32[]>("Win32");
                _win32s = _win32Storage.TryLoad(new Win32[] { });
                _uwpStorage = new BinaryStorage<UWP.Application[]>("UWP");
                _uwps = _uwpStorage.TryLoad(new UWP.Application[] { });
            });
            Log.Info($"|Wox.Plugin.Program.Main|Number of preload win32 programs <{_win32s.Length}>");
            Log.Info($"|Wox.Plugin.Program.Main|Number of preload uwps <{_uwps.Length}>");

            //########DELETE
            long win32indexcost = 0;
            long uwpindexcost = 0;
            
            var a = Task.Run(() =>
            {
                if (IsStartupIndexProgramsRequired || !_win32s.Any())
                    Stopwatch.Normal("|Wox.Plugin.Program.Main|Win32Program index cost", IndexWin32Programs);
            });

            var b = Task.Run(() =>
            {
                if (IsStartupIndexProgramsRequired || !_uwps.Any())
                    Stopwatch.Normal("|Wox.Plugin.Program.Main|Win32Program index cost", IndexUWPPrograms);
            });

            Task.WaitAll(a, b);

            _settings.LastIndexTime = DateTime.Today;

            //########DELETE
            /*
             *  With roaming folder already 
                Preload programs cost <24ms>
                Program index cost <3163ms>

                no roaming yet (clean)
                Preload programs cost <79ms>
                Program index cost <2900ms>
             *
             * 
             */

            long totalindexcost = win32indexcost + uwpindexcost;

            if (preloadcost > 70 || totalindexcost > 4000)
            {
#if DEBUG
#else
    throw e
#endif
            }

            if(_uwps.Count() > 36 || _win32s.Count() > 142)
            {

            }

            var win32 = _win32s.Where(t1 => _settings.ProgramSources.Any(x => x.Name == t1.Name)).ToList();
            var uwp = _uwps.Where(t1 => _settings.ProgramSources.Any(x => x.Name == t1.DisplayName)).ToList();
            //if (win32.Count()>0)
            //{

            //}
            //if (uwp.Count() > 0)
            //{

            //}          

            var queryuwps = _uwps.GroupBy(x => x.UniqueIdentifier)
              .Where(g => g.Count() > 1)
              .Select(y => y.Key)
              .ToList();
            if (queryuwps.Count > 0)
            {
                var duplicates = _uwps.Where(t1 => queryuwps.Any(x => x == t1.UniqueIdentifier)).Select(x => x).ToList();
            }

            var querywin32 = _win32s.GroupBy(x => x.UniqueIdentifier)
             .Where(g => g.Count() > 1)
             .Select(y => y.Key)
             .ToList();
            if (querywin32.Count > 0)
            {                
                var duplicates = _win32s.Where(t1 => querywin32.Any(x => x == t1.UniqueIdentifier)).Select(x => x).ToList();
            }


            var duplicateLocations = _uwps.Where(x => x.Package.Location == @"C:\Program Files\WindowsApps\microsoft.windowscommunicationsapps_16005.11629.20174.0_x64__8wekyb3d8bbwe").Select(x => x).ToList();

            if(_win32s.Where(x => _settings.DisabledProgramSources.Any(t1 => t1.UniqueIdentifier == x.UniqueIdentifier && x.Enabled)).Count() > 0)
            {
                var win32exists = _win32s.Where(x => _settings.DisabledProgramSources.Any(t1 => t1.UniqueIdentifier == x.UniqueIdentifier)).Select(x => x).ToList();
            }

            if (_uwps.Where(x => _settings.DisabledProgramSources.Any(t1 => t1.UniqueIdentifier == x.UniqueIdentifier && x.Enabled)).Count() > 0)
            {
                var uwpsexists = _uwps.Where(x => _settings.DisabledProgramSources.Any(t1 => t1.UniqueIdentifier == x.UniqueIdentifier)).Select(x => x).ToList();
            }

            //########DELETE
        }

        public void Save()
        {
            _settingsStorage.Save();
            _win32Storage.Save(_win32s);
            _uwpStorage.Save(_uwps);
        }

        public List<Result> Query(Query query)
        {
            lock (IndexLock)
            {
                var results1 = _win32s.AsParallel()
                    .Where(p => p.Enabled)
                    .Select(p => p.Result(query.Search, _context.API));

                var results2 = _uwps.AsParallel()
                    .Where(p => p.Enabled)
                    .Select(p => p.Result(query.Search, _context.API));

                var result = results1.Concat(results2).Where(r => r.Score > 0).ToList();
                return result;
            }
        }

        public void Init(PluginInitContext context)
        {
            _context = context;
        }

        public static void IndexWin32Programs()
        {
            lock (IndexLock)
            {
                _win32s = Win32.All(_settings);
            }
        }

        public static void IndexUWPPrograms()
        {
            var windows10 = new Version(10, 0);
            var support = Environment.OSVersion.Version.Major >= windows10.Major;

            lock (IndexLock)
            {
                _uwps = support ? UWP.All() : new UWP.Application[] { };
            }
        }

        public static void IndexPrograms()
        {
            var t1 = Task.Run(() => { IndexWin32Programs(); });

            var t2 = Task.Run(() => { IndexUWPPrograms(); });

            Task.WaitAll(t1, t2);

            _settings.LastIndexTime = DateTime.Today;
        }        

        public Control CreateSettingPanel()
        {
            return new ProgramSetting(_context, _settings, _win32s, _uwps);
        }

        public string GetTranslatedPluginTitle()
        {
            return _context.API.GetTranslation("wox_plugin_program_plugin_name");
        }

        public string GetTranslatedPluginDescription()
        {
            return _context.API.GetTranslation("wox_plugin_program_plugin_description");
        }

        public List<Result> LoadContextMenus(Result selectedResult)
        {
            var menuOptions = new List<Result>();
            var program = selectedResult.ContextData as IProgram;
            if (program != null)
            {
                menuOptions = program.ContextMenus(_context.API);                
            }

            menuOptions.Add(
                                new Result
                                {
                                    Title = _context.API.GetTranslation("wox_plugin_program_disable_program"),
                                    Action = c =>
                                    {
                                        DisableProgram(program);
                                        _context.API.ShowMsg(_context.API.GetTranslation("wox_plugin_program_disable_dlgtitle_success"), 
                                                                _context.API.GetTranslation("wox_plugin_program_disable_dlgtitle_success_message"));
                                        return false;
                                    },
                                    IcoPath = "Images/disable.png"
                                }
                           );

            return menuOptions;
        }

        private void DisableProgram(IProgram programToDelete)
        {
            if (_settings.DisabledProgramSources.Any(x => x.UniqueIdentifier == programToDelete.UniqueIdentifier))
                return;

            if (_uwps.Any(x => x.UniqueIdentifier == programToDelete.UniqueIdentifier))
                _uwps.Where(x => x.UniqueIdentifier == programToDelete.UniqueIdentifier).FirstOrDefault().Enabled = false;

            if (_win32s.Any(x => x.UniqueIdentifier == programToDelete.UniqueIdentifier))
                _win32s.Where(x => x.UniqueIdentifier == programToDelete.UniqueIdentifier).FirstOrDefault().Enabled = false;

            _settings.DisabledProgramSources
                     .Add(
                             new Settings.DisabledProgramSource
                             {
                                 Name = programToDelete.Name,
                                 Location = programToDelete.Location,
                                 UniqueIdentifier = programToDelete.UniqueIdentifier,
                                 Enabled = false
                             }
                         );
        }

        public static bool StartProcess(ProcessStartInfo info)
        {
            bool hide;
            try
            {
                Process.Start(info);
                hide = true;
            }
            catch (Exception)
            {
                var name = "Plugin: Program";
                var message = $"Can't start: {info.FileName}";
                _context.API.ShowMsg(name, message, string.Empty);
                hide = false;
            }
            return hide;
        }

        public void ReloadData()
        {
            IndexPrograms();
        }
    }
}