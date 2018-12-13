﻿using Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace MultiCommentViewer
{
    class Program
    {
        static ILogger _logger;
        [STAThread]
        //static async Task Main(string[] args)
        static void Main()
        {
            _logger = new Common.LoggerTest();
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            var app = new AppNoStartupUri
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown
            };
            app.InitializeComponent();
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());

            var p = new Program();
            p.ExitRequested += (sender, e) =>
            {
                app.Shutdown();
            };

            var t = p.StartAsync();
            Handle(t);
            app.Run();
        }
        static async void Handle(Task t)
        {
            try
            {
                await t;
            }catch(Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }
        private string GetOptionsPath()
        {
            var currentDir = Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName;
            return Path.Combine(currentDir, "settings", "options.txt");
        }
        public async Task StartAsync()
        {
            var io = new IOTest();
            var options = new DynamicOptionsTest();
            try
            {
                var s = io.ReadFile(GetOptionsPath());
                options.Deserialize(s);
            }
            catch (Exception ex)
            {
                _logger.LogException(ex);
            }
            var model = new Model(new SitePluginLoaderTest(), options, _logger, io);
            IMainViewModel viewModel = new MainViewModel(model);
            viewModel.CloseRequested += ViewModel_CloseRequested;

            void windowClosed(object sender, EventArgs e)
            {
                viewModel.RequestClose();
            }

            SplashScreen splashScreen = new SplashScreen();  //not disposable, but I'm keeping the same structure
            {
                splashScreen.Closed += windowClosed; //if user closes splash screen, let's quit
                splashScreen.Show();

                await viewModel.InitializeAsync();

                var mainForm = new MainWindow();
                mainForm.Closed += windowClosed;
                mainForm.DataContext = viewModel;
                mainForm.Show();

                splashScreen.Owner = mainForm;
                splashScreen.Closed -= windowClosed;
                splashScreen.Close();
            }
        }

        public event EventHandler<EventArgs> ExitRequested;
        void ViewModel_CloseRequested(object sender, EventArgs e)
        {
            OnExitRequested(EventArgs.Empty);
        }

        protected virtual void OnExitRequested(EventArgs e)
        {
            ExitRequested?.Invoke(this, e);
        }
        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;

            try
            {
                _logger.LogException(ex, "UnhandledException");
                var s = _logger.GetExceptions();
                using (var sw = new System.IO.StreamWriter("error.txt", true))
                {
                    sw.WriteLine(s);
                }
            }
            catch { }
        }
    }
}