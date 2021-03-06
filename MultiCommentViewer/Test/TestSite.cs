﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SitePlugin;
using Plugin;
using ryu_s.BrowserCookie;
using System.Windows.Controls;
using System.Diagnostics;
using System.Threading;
using GalaSoft.MvvmLight;
using System.Windows.Media;
using System.Windows;
using Common;
using System.Net;

namespace MultiCommentViewer.Test
{

    public class TestSiteContext : ISiteContext
    {
        public Guid Guid { get { return new Guid("609B4057-A5CE-49BA-A30F-211C4DFE838E"); } }

        public string DisplayName { get { return "テスト"; } }

        public IOptionsTabPage TabPanel
        {
            get
            {
                var panel = new TestSiteOptionsPagePanel();
                panel.SetViewModel(new TestSiteOptionsViewModel(_siteOptions));
                return new TestOptionsTabPage(DisplayName, panel);
            }
        }
        public bool IsValidInput(string input)
        {
            //always true
            return true;
        }
        public ICommentProvider CreateCommentProvider()
        {
            return new TestSiteCommentProvider(_options, _siteOptions, _userStore);
        }
        private TestSiteOptions _siteOptions;
        public void LoadOptions(string siteOptionsStr, IIo io)
        {
            _siteOptions = new TestSiteOptions();
        }

        public void SaveOptions(string path, IIo io)
        {
        }
        public void Init()
        {
            _userStore.Init();
        }
        public void Save()
        {
            _userStore.Save();
        }
        public UserControl GetCommentPostPanel(ICommentProvider commentProvider)
        {
            throw new NotImplementedException();
        }
        protected virtual IUserStore CreateUserStore()
        {
            return new UserStoreTest();
        }
        private readonly ICommentOptions _options;
        private readonly ILogger _logger;
        private readonly IUserStore _userStore;
        public TestSiteContext(ICommentOptions options, ILogger logger)
        {
            _options = options;
            _logger = logger;
            _userStore = CreateUserStore();
        }
    }
    public class TestSiteOptionsViewModel:ViewModelBase
    {
        public bool IsCheckBox { get { return _changed.IsCheckBox; } set { _changed.IsCheckBox = value; } }
        public string TextBoxText { get { return _changed.TextBoxText; } set { _changed.TextBoxText = value; } }
        private readonly TestSiteOptions _origin;
        private readonly TestSiteOptions _changed;
        public TestSiteOptions OriginOptions { get { return _origin; } }
        public TestSiteOptions ChangedOptions { get { return _changed; } }
        public TestSiteOptionsViewModel(TestSiteOptions siteOptions)
        {
            _origin = siteOptions;
            _changed = siteOptions.Clone();
        }
    }
    public class TestSiteOptions
    {
        public bool IsCheckBox { get; set; }
        public string TextBoxText { get; set; }
        public TestSiteOptions()
        {
            IsCheckBox = false;
            TextBoxText = "test";
        }

        internal TestSiteOptions Clone()
        {
            return (TestSiteOptions)this.MemberwiseClone();
        }

        internal void Set(TestSiteOptions changedOptions)
        {
            var properties = changedOptions.GetType().GetProperties();
            foreach (var property in properties)
            {
                if (property.SetMethod != null)
                {
                    property.SetValue(this, property.GetValue(changedOptions));
                }
            }
        }
    }
    public class TestOptionsTabPage : IOptionsTabPage
    {
        public string HeaderText { get; }

        private readonly TestSiteOptionsPagePanel _tabPagePanel;
        public UserControl TabPagePanel { get { return _tabPagePanel; } }

        public void Apply()
        {
            //TODO:なんかもっとすっきりとした実装にしたい
            var optionsVm = _tabPagePanel.GetViewModel();
            optionsVm.OriginOptions.Set(optionsVm.ChangedOptions);
        }

        public void Cancel()
        {
        }
        public TestOptionsTabPage(string displayName, TestSiteOptionsPagePanel panel)
        {
            HeaderText = displayName;
            _tabPagePanel = panel;
        }
    }
    public class MessageTextTest : IMessageText
    {
        public string Text { get; }
        public MessageTextTest(string text)
        {
            Text = text;
        }
    }
    public class TestSiteCommentProvider : ICommentProvider
    {
        private bool _canConnect=true;
        public bool CanConnect
        {
            get { return _canConnect; }
            set
            {
                _canConnect = value;
                CanConnectChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        private bool _canDisconnect = false;
        public bool CanDisconnect
        {
            get { return _canDisconnect; }
            set
            {
                _canDisconnect = value;
                CanDisconnectChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler<List<ICommentViewModel>> InitialCommentsReceived;
        public event EventHandler<ICommentViewModel> CommentReceived;
        public event EventHandler<IMetadata> MetadataUpdated;
        public event EventHandler CanConnectChanged;
        public event EventHandler CanDisconnectChanged;
        public event EventHandler<ConnectedEventArgs> Connected;

        private CancellationTokenSource _cts;
        public async Task ConnectAsync(string input, IBrowserProfile browserProfile)
        {
            CanConnect = false;
            CanDisconnect = true;
            if(_cts != null)
            {
                Debugger.Break();                                
            }
            //Debug.Assert(_cts == null);
            _cts = new CancellationTokenSource();
            _metaTimer.Interval = 10 * 1000;
            _metaTimer.Elapsed += _metaTimer_Elapsed;
            _metaTimer.Enabled = true;

            try
            {
                var list = Enumerable.Range(0, 100000).Select(n =>new TestSiteCommentViewModel(new List<IMessagePart> { MessagePartFactory.CreateMessageText("name") }, new List<IMessagePart> { MessagePartFactory.CreateMessageText("message") }, _options, SiteOptions)).Cast<ICommentViewModel>().ToList();
                InitialCommentsReceived?.Invoke(this, list);
                while (!_cts.IsCancellationRequested)
                {
                    var name = new List<IMessagePart>
                {
                    new MessageTextTest(RandomString(2)),
                };
                    var message = new List<IMessagePart>
                {
                    new MessageTextTest(RandomString()),
                };
                    var comment = new TestSiteCommentViewModel(name, message, _options, SiteOptions);
                    CommentReceived?.Invoke(this, comment );
                    await Task.Delay(500);
                }
            }
            finally
            {
                CanConnect = true;
                CanDisconnect = false;
                _metaTimer.Enabled = false;
                _cts.Dispose();
                _cts = null;
            }
        }

        private void _metaTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            MetadataUpdated?.Invoke(this, new Metadata
            {
                Title = RandomString(),
                Active = RandomNum(2).ToString(), 
                 CurrentViewers = "-",
                  Elapsed = "-",
                  TotalViewers = "-",
                IsLive = true,

            });
        }

        private readonly System.Timers.Timer _metaTimer = new System.Timers.Timer();
        public TestSiteOptions SiteOptions { get; set; }
        private readonly ICommentOptions _options;
        private readonly IUserStore _userStore;

        public TestSiteCommentProvider(ICommentOptions options, TestSiteOptions siteOptions, IUserStore userStore)
        {
            _options = options;
            SiteOptions = siteOptions;
            _userStore = userStore;
        }
        public TestSiteCommentProvider()
        {

        }
        public IUser GetUser(string userId)
        {
            return _userStore.GetUser(userId);
        }

        public void Disconnect()
        {
            if(_cts != null)
            {
                _cts.Cancel();
            }
        }

        public IEnumerable<ICommentViewModel> GetUserComments(IUser user)
        {
            throw new NotImplementedException();
        }

        public Task PostCommentAsync(string text)
        {
            throw new NotImplementedException();
        }
        private static Random _random = new Random();
        public static string RandomString()
        {
            return RandomString(_random.Next(2, 40));
        }
        public static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789\n";
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[_random.Next(s.Length)]).ToArray());
        }
        public static int RandomNum(int length)
        {
            const string chars = "0123456789";
            var str = new string(Enumerable.Repeat(chars, length)
              .Select(s => s[_random.Next(s.Length)]).ToArray());
            return int.Parse(str);
        }

        public Task<ICurrentUserInfo> GetCurrentUserInfo(CookieContainer cc)
        {
            throw new NotImplementedException();
        }

        public Task<ICurrentUserInfo> GetCurrentUserInfo(IBrowserProfile browserProfile)
        {
            throw new NotImplementedException();
        }
    }
    class Metadata : IMetadata
    {
        public string Title { get; set; }

        public string Elapsed { get; set; }

        public string CurrentViewers { get; set; }

        public string Active { get; set; }

        public string TotalViewers { get; set; }

        public bool? IsLive { get; set; }
    }
}
