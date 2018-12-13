﻿using Common;
using ryu_s.BrowserCookie;
using SitePlugin;
using System;
using System.Collections.Generic;

namespace MultiCommentViewer
{
    class Connection:IConnection
    {
        static readonly IBrowserProfile _emptyBrowserProfile=new EmptyBrowserProfile();
        static readonly ISiteContext _emptySiteContext = new EmptySitePlugin();
        private readonly ILogger _logger;

        public event EventHandler<NameChangedEventArgs> NameChanged;
        public event EventHandler<CurrentSiteEventArgs> CurrentSiteChanged;
        public event EventHandler<IBrowserProfile> CurrentBrowserChanged;
        public event EventHandler<IBrowserProfile> BrowserAdded;
        public event EventHandler<SitePluginInfo> SiteAdded;
        public event EventHandler<Guid> SiteRemoved;
        public event EventHandler<ICommentViewModel> CommentReceived;
        public event EventHandler CanConnectChanged;
        public event EventHandler CanDisconnectChanged;
        public event EventHandler<IMetadata> MetadataUpdated;
        public event EventHandler LoggedInUserInfoChanged;

        public string Name
        {
            get => ConnectionName2.Name;
            set
            {
                if(ConnectionName2.Name == value)
                {
                    return;
                }
                ConnectionName2.Name = value;
                NameChanged?.Invoke(this, new NameChangedEventArgs(_beforeName, ConnectionName2.Name));
                _beforeName = ConnectionName2.Name;
            }
        }
        /// <summary>
        /// 現在ログインしているユーザの名前
        /// </summary>
        public string LoggedInUserInfo { get; private set; }
        public ConnectionName2 ConnectionName => ConnectionName2;
        private string _beforeName;
        public Guid Guid => ConnectionName2.Guid;

        /// <summary>
        /// 
        /// 直接触ってはダメ。必ずCurrentSiteGuidを経由すること
        /// </summary>
        private ISiteContext _currentSite;
        /// <summary>
        /// 
        /// </summary>
        public Guid CurrentSiteGuid
        {
            get => _currentSite.Guid;
            set
            {
                if (_currentSite != null && _currentSite.Guid.Equals(value))
                {
                    return;
                }
                var before = _cp;
                if(before != null)
                {
                    before.CanConnectChanged -= Cp_CanConnectChanged;
                    before.CanDisconnectChanged -= Cp_CanDisconnectChanged;
                    before.CommentReceived -= Cp_CommentReceived;
                    before.MetadataUpdated -= Cp_MetadataUpdated;
                }
                var siteContext = GetSiteContext(value);
                var next = siteContext.CreateCommentProvider();
                next.CanConnectChanged += Cp_CanConnectChanged;
                next.CanDisconnectChanged += Cp_CanDisconnectChanged;
                next.CommentReceived += Cp_CommentReceived;
                next.MetadataUpdated += Cp_MetadataUpdated;

                _cp = next;
                _currentSite = siteContext;
                CurrentSiteChanged?.Invoke(this, new CurrentSiteEventArgs(Guid, value));
                if(before?.CanConnect != next.CanConnect)
                {
                    CanConnectChanged?.Invoke(this, EventArgs.Empty);
                }
                if (before?.CanDisconnect != next.CanDisconnect)
                {
                    CanDisconnectChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        private async void UpdateLoggedInUserInfo()
        {
            if(_cp == null || CurrentBrowserProfile == null)
            {
                return;
            }
            var userInfo = await _cp.GetCurrentUserInfo(CurrentBrowserProfile);
            if (userInfo.IsLoggedIn)
            {
                LoggedInUserInfo = userInfo.Username;
            }
            else
            {
                LoggedInUserInfo = "(未ログイン)";
            }
            LoggedInUserInfoChanged?.Invoke(this, EventArgs.Empty);
        }

        private void Cp_MetadataUpdated(object sender, IMetadata e)
        {
            MetadataUpdated?.Invoke(this, e);
        }

        private void Cp_CommentReceived(object sender, ICommentViewModel e)
        {
            CommentReceived?.Invoke(this, e);
        }

        private void Cp_CanDisconnectChanged(object sender, EventArgs e)
        {
            CanDisconnectChanged?.Invoke(this, e);
        }

        private void Cp_CanConnectChanged(object sender, EventArgs e)
        {
            CanConnectChanged?.Invoke(this, e);
        }

        /// <summary>
        /// 
        /// 直接参照してはいけない。必ずGetSiteContext()を使うこと。
        /// </summary>
        Dictionary<Guid, ISiteContext> _siteDitct = new Dictionary<Guid, ISiteContext>();
        /// <summary>
        /// SiteContextのGuidからSiteContextを取得する
        /// </summary>
        /// <param name="guid">SiteContextのGuid</param>
        /// <returns></returns>
        private ISiteContext GetSiteContext(Guid guid)
        {
            if (_siteDitct.Count > 0)
            {
                return _siteDitct[guid];
            }
            else
            {
                return _emptySiteContext;
            }
        }

        List<ISiteContext> _sites = new List<ISiteContext>();
        public IReadOnlyList<ISiteContext> Sites
        {
            get
            {
                if (_siteDitct.Count > 0)
                {
                    return _sites;
                }
                else
                {
                    return new List<ISiteContext> { _emptySiteContext };
                }
            }
        }

        public void AddSiteContext(ISiteContext sitePlugin)
        {
            if(_sites.Count == 0)
            {
                SiteRemoved?.Invoke(this, _emptySiteContext.Guid);
            }
            _sites.Add(sitePlugin);
            _siteDitct.Add(sitePlugin.Guid, sitePlugin);

            SiteAdded?.Invoke(this, new SitePluginInfo(sitePlugin.DisplayName, sitePlugin.Guid));
            if (_sites.Count == 1)
            {
                //CurrentSiteGuidを設定する前にSiteAddedを発する必要がある。そうしないと参照している側ではこのSitePluginの存在を知らない。
                CurrentSiteGuid = sitePlugin.Guid;
                //_selectedSite = sitePlugin;
            }
        }
        public void AddSiteContext(List<ISiteContext> sitePlugins)
        {
            foreach(var site in sitePlugins)
            {
                AddSiteContext(site);
            }
        }
        public void RemoveSite(ISiteContext siteContext)
        {
            _sites.Remove(siteContext);
            _siteDitct.Remove(siteContext.Guid);
            SiteRemoved?.Invoke(this, siteContext.Guid);
        }
        List<IBrowserProfile> _browsers = new List<IBrowserProfile>();
        public IReadOnlyList<IBrowserProfile> Browsers
        {
            get
            {
                if(_browsers.Count > 0)
                {
                    return _browsers;
                }
                else
                {
                    return new List<IBrowserProfile> {_emptyBrowserProfile };
                }
            }
        }
        public void AddBrowser(IBrowserProfile browserProfile)
        {
            try
            {
                if (_browsers.Count == 0)
                {
                    //TODO:BrowserRemovedを実装する
                    //BrowserRemoved?.Invoke(this, _emptyBrowserProfile);
                }
                _browsers.Add(browserProfile);
                if (_browsers.Count == 1)
                {
                    _currentBrowserProfile = browserProfile;
                }
                BrowserAdded?.Invoke(this, browserProfile);
            }
            catch (Exception ex)
            {
                _logger.LogException(ex);
            }
        }
        public void AddBrowser(List<IBrowserProfile> browserProfiles)
        {
            try
            {
                foreach (var browser in browserProfiles)
                {
                    AddBrowser(browser);
                }
            }catch(Exception ex)
            {
                _logger.LogException(ex);
            }
        }
        IBrowserProfile _currentBrowserProfile;
        public IBrowserProfile CurrentBrowserProfile
        {
            get => _currentBrowserProfile;
            set
            {
                if (_currentBrowserProfile != null && _currentBrowserProfile.Equals(value))
                    return;
                _currentBrowserProfile = value;
                CurrentBrowserChanged?.Invoke(this, _currentBrowserProfile);
            }
        }
        private string _input;
        /// <summary>
        /// 入力値
        /// 値をセットする場合はSetInput()を使用する
        /// </summary>
        public string Input
        {
            get => _input;
            private set
            {
                if (_input == value) return;
                _input = value;
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="input"></param>
        /// <param name="isAutoSiteSelectionEnabled">入力された値を解析して最適な配信サイトを自動的に選択するか</param>
        public void SetInput(string input, bool isAutoSiteSelectionEnabled)
        {
            try
            {
                if (isAutoSiteSelectionEnabled)
                {
                    foreach (var site in _sites)
                    {
                        var b = site.IsValidInput(input);
                        if (b)
                        {
                            CurrentSiteGuid = site.Guid;
                            break;
                        }
                    }
                }
                Input = input;
            }catch(Exception ex)
            {
                _logger.LogException(ex);
            }
        }
        ConnectionName2 ConnectionName2 { get; } = new ConnectionName2();
        public bool CanConnect => _cp.CanConnect;
        public bool CanDisconnect => _cp.CanDisconnect;

        public bool NeedSave { get; set; }

        ICommentProvider _cp;
        public Connection(ILogger logger)
        {
            _beforeName = ConnectionName2.Name;
            CurrentSiteGuid = Sites[0].Guid;
            CurrentBrowserProfile = Browsers[0];
            _logger = logger;
        }

        public async void Connect()
        {
            try
            {
                await _cp.ConnectAsync(Input, CurrentBrowserProfile);
            }
            catch (Exception ex)
            {
                _logger.LogException(ex);
            }
        }
        public void Disconnect()
        {
            try
            {
                _cp.Disconnect();
            }
            catch (Exception ex)
            {
                _logger.LogException(ex);
            }
        }
    }
}