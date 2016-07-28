﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using Splat;

namespace ReactiveUI.Routing
{
    /// <summary>
    /// Defines a class that contains common core functionality for routed apps.
    /// </summary>
    /// <typeparam name="TConfig"></typeparam>
    public class RoutedAppHost : IRoutedAppHost
    {
        public IRoutedAppConfig Config { get; }

        public RoutedAppHost(IRoutedAppConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            Config = config;
        }

        public void Start()
        {
            Task.Run(StartAsync).Wait();
        }

        public virtual async Task StartAsync()
        {
            Config.RegisterDependencies(Locator.CurrentMutable);
            var stateStore = GetService<IObjectStateStore>();
            var notifier = GetService<ISuspensionNotifier>();
            var routerParams = GetService<RouterParams>();
            var existingState = await stateStore.LoadState();
            var activator = GetService<IReActivator>();
            var router = await activator.ResumeAsync<IRouter, RouterParams, RouterState>(routerParams, (RouterState)existingState?.State);

            notifier.OnSuspend
                .FirstAsync()
                .Do(async u =>
                {
                    var state = await activator.SuspendAsync(router);
                    await stateStore.SaveState(state);
                })
                .Subscribe();
        }

        private T GetService<T>()
        {
            var service = Locator.Current.GetService<T>();
            if (service == null)
            {
                throw new InvalidOperationException($"The {nameof(IRoutedAppConfig)} must register a {typeof(T)} service so that the router can be started.");
            }
            return service;
        }
    }
}
