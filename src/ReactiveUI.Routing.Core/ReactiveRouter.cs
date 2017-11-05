﻿using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using ReactiveUI.Routing.Presentation;
using Splat;

namespace ReactiveUI.Routing
{
    public class ReactiveRouter : IReactiveRouter, IEnableLogger
    {
        private readonly Stack<NavigationRequest> navigationStack = new Stack<NavigationRequest>();
        private readonly Subject<NavigationRequest> navigated = new Subject<NavigationRequest>();

        public Interaction<PresenterRequest, PresenterResponse> PresentationRequested { get; } = new Interaction<PresenterRequest, PresenterResponse>();
        public IEnumerable<NavigationRequest> NavigationStack => navigationStack;

        public IObservable<Unit> Navigate(NavigationRequest request)
        {
            return Observable.Defer(() =>
            {
                return Observable.FromAsync(async () =>
                {
                    if (!CanNavigateToRequest(request))
                    {
                        throw new InvalidOperationException("Cannot perform the given navigation request because it would result in an invalid state.");
                    }
                    if (request is BackNavigationRequest)
                    {
                        navigationStack.Pop();
                        await PresentationRequested.Handle(navigationStack.Peek().PresenterRequest);
                    }
                    else if (request is ResetNavigationRequest)
                    {
                        navigationStack.Clear();
                    }
                    else if (request is CombinedNavigationRequest combined)
                    {
                        foreach (var r in combined)
                        {
                            await Navigate(r);
                        }
                        return;
                    }
                    else
                    {
                        var result = await PresentationRequested.Handle(request.PresenterRequest);
                        navigationStack.Push(request);
                    }
                    navigated.OnNext(request);
                });
            });
        }

        public IObservable<bool> CanNavigate(NavigationRequest request)
        {
            if (CanObserveRequest(request))
            {
                return navigated
                    .Select(req => CanNavigateToRequest(request))
                    .StartWith(CanNavigateToRequest(request));
            }
            else
            {
                this.Log().Warn("Cannot calculate whether the given request will be navigatable. " +
                                "This usually happens with complicated navigation requests such as " +
                                "combined navigation requests.");
                return Observable.Return(false);
            }
        }

        private bool CanObserveRequest(NavigationRequest request)
        {
            return !(request is CombinedNavigationRequest);
        }

        private bool CanNavigateToRequest(NavigationRequest request)
        {
            if (request is BackNavigationRequest)
            {
                return CanNavigateBack();
            }

            return true;
        }

        private bool CanNavigateBack()
        {
            if (navigationStack.Count > 1)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}